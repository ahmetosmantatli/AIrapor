using MetaAdsAnalyzer.API.Options;
using MetaAdsAnalyzer.API.Services;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth/meta")]
public class MetaAuthController : ControllerBase
{
    public const string StateCookieName = "meta_oauth_state";

    private readonly AppDbContext _db;
    private readonly IMetaOAuthService _metaOAuth;
    private readonly IMetaAccessTokenProtector _tokenProtector;
    private readonly IJwtTokenService _jwt;
    private readonly IMetaInsightsSyncService _metaInsights;
    private readonly MetaOptions _metaOptions;
    private readonly ILogger<MetaAuthController> _logger;
    private readonly IMemoryCache _oauthStateCache;

    public MetaAuthController(
        AppDbContext db,
        IMetaOAuthService metaOAuth,
        IMetaAccessTokenProtector tokenProtector,
        IJwtTokenService jwt,
        IMetaInsightsSyncService metaInsights,
        IOptions<MetaOptions> metaOptions,
        IMemoryCache oauthStateCache,
        ILogger<MetaAuthController> logger)
    {
        _db = db;
        _metaOAuth = metaOAuth;
        _tokenProtector = tokenProtector;
        _jwt = jwt;
        _metaInsights = metaInsights;
        _metaOptions = metaOptions.Value;
        _oauthStateCache = oauthStateCache;
        _logger = logger;
    }

    /// <summary>Meta giriş sayfasına yönlendirir (OAuth code akışı).</summary>
    [HttpGet("start")]
    public IActionResult Start()
    {
        if (!IsMetaConfigured())
        {
            return Problem(
                title: "Meta OAuth yapılandırılmamış",
                detail: "appsettings içinde Meta:AppId, Meta:AppSecret ve Meta:RedirectUri doldurun.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var state = Guid.NewGuid().ToString("N");
        // Sunucu tarafı state: Facebook dönüşünde bazı tarayıcılar / uygulama-içi WebView
        // çerezi göndermeyebilir; cookie yalnızca ek doğrulama olarak kalır.
        _oauthStateCache.Set(
            CacheKeyForState(state),
            true,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });

        Response.Cookies.Append(
            StateCookieName,
            state,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10),
                Path = "/",
            });

        var url = _metaOAuth.BuildAuthorizeUrl(state);
        return Redirect(url);
    }

    /// <summary>Meta OAuth geri dönüşü; jetonu kaydeder ve SPA'ya yönlendirir.</summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        var redirectBase = string.IsNullOrWhiteSpace(_metaOptions.PostLoginRedirectUri)
            ? "http://localhost:5173/"
            : _metaOptions.PostLoginRedirectUri.Trim();

        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Meta OAuth hata: {Error} {Description}", error, errorDescription);
            return Redirect(AppendQuery(redirectBase, "meta_oauth", "denied", errorDescription));
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Redirect(AppendQuery(redirectBase, "meta_oauth", "invalid_request", "code veya state eksik."));
        }

        var cacheKey = CacheKeyForState(state);
        if (!_oauthStateCache.TryGetValue(cacheKey, out _))
        {
            if (!Request.Cookies.TryGetValue(StateCookieName, out var cookieState) ||
                !string.Equals(cookieState, state, StringComparison.Ordinal))
            {
                _logger.LogWarning("Meta OAuth state doğrulanamadı (önbellek ve çerez uyuşmadı).");
                return Redirect(
                    AppendQuery(redirectBase, "meta_oauth", "invalid_state", "Oturum doğrulanamadı. Tekrar deneyin."));
            }
        }

        _oauthStateCache.Remove(cacheKey);
        Response.Cookies.Delete(StateCookieName, new CookieOptions { Path = "/" });

        try
        {
            var result = await _metaOAuth.CompleteAuthorizationAsync(code, cancellationToken).ConfigureAwait(false);
            var user = await UpsertUserAsync(result, cancellationToken).ConfigureAwait(false);
            try
            {
                await _metaInsights.SyncLinkedAdAccountsFromGraphAsync(user.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception syncEx)
            {
                _logger.LogWarning(syncEx, "OAuth sonrası reklam hesapları veritabanına yazılamadı UserId={UserId}", user.Id);
            }

            string? accessToken = null;
            try
            {
                accessToken = _jwt.CreateToken(user.Id, user.Email);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "JWT üretilemedi; SPA el ile giriş kullanmalı.");
            }

            return Redirect(AppendQuery(redirectBase, "meta_oauth", "success", null, accessToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meta OAuth tamamlanamadı");
            return Redirect(AppendQuery(redirectBase, "meta_oauth", "error", ex.Message));
        }
    }

    private bool IsMetaConfigured()
    {
        return !IsPlaceholder(_metaOptions.AppId)
               && !IsPlaceholder(_metaOptions.AppSecret)
               && !string.IsNullOrWhiteSpace(_metaOptions.RedirectUri);
    }

    /// <summary>Yarın eklenecek gerçek değerler gelene kadar YOUR_* placeholder OAuth’u kapalı tutar.</summary>
    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var v = value.Trim();
        return v.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<User> UpsertUserAsync(MetaOAuthResult result, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.MetaUserId == result.MetaUserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            var standardPlanId = await _db.SubscriptionPlans.AsNoTracking()
                .Where(p => p.Code == "standard" && p.IsActive)
                .Select(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (standardPlanId == 0)
            {
                throw new InvalidOperationException("Aktif 'standard' abonelik planı veritabanında yok.");
            }

            user = new User
            {
                Email = result.Email,
                MetaUserId = result.MetaUserId,
                MetaAccessToken = _tokenProtector.Protect(result.AccessToken),
                MetaTokenExpiresAt = result.ExpiresAt,
                CreatedAt = DateTimeOffset.UtcNow,
                Currency = "TRY",
                Timezone = "UTC",
                AttributionWindow = "7d_click_1d_view",
                SubscriptionPlanId = standardPlanId,
            };
            _db.Users.Add(user);
        }
        else
        {
            user.Email = result.Email;
            user.MetaAccessToken = _tokenProtector.Protect(result.AccessToken);
            user.MetaTokenExpiresAt = result.ExpiresAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return user;
    }

    private static string AppendQuery(string baseUrl, string key, string value, string? message, string? accessToken = null)
    {
        var uri = baseUrl.Contains('?', StringComparison.Ordinal)
            ? $"{baseUrl}&{key}={Uri.EscapeDataString(value)}"
            : $"{baseUrl}?{key}={Uri.EscapeDataString(value)}";

        if (!string.IsNullOrWhiteSpace(message))
        {
            uri += $"&message={Uri.EscapeDataString(message)}";
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            uri += $"&access_token={Uri.EscapeDataString(accessToken)}";
        }

        return uri;
    }

    private static string CacheKeyForState(string state) => $"meta_oauth_state:{state}";
}
