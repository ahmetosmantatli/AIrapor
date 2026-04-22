using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using MetaAdsAnalyzer.API.Options;
using Microsoft.Extensions.Options;

namespace MetaAdsAnalyzer.API.Services;

public sealed class MetaOAuthService : IMetaOAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly MetaOptions _options;
    private readonly ILogger<MetaOAuthService> _logger;

    public MetaOAuthService(HttpClient http, IOptions<MetaOptions> options, ILogger<MetaOAuthService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string BuildAuthorizeUrl(string state)
    {
        var version = string.IsNullOrWhiteSpace(_options.ApiVersion) ? "v19.0" : _options.ApiVersion.Trim();
        var scopes = string.Join(
            ',',
            _options.Scopes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.AppId,
            ["redirect_uri"] = _options.RedirectUri,
            ["state"] = state,
            ["scope"] = scopes,
            ["response_type"] = "code",
        };

        var qs = string.Join("&", query.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        return $"https://www.facebook.com/{version}/dialog/oauth?{qs}";
    }

    public async Task<MetaOAuthResult> CompleteAuthorizationAsync(string code, CancellationToken cancellationToken = default)
    {
        var shortLived = await ExchangeCodeForTokenAsync(code, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(shortLived.AccessToken))
        {
            throw new InvalidOperationException("Meta kod takası erişim jetonu döndürmedi.");
        }

        var longLived = await ExchangeFbTokenAsync(shortLived.AccessToken, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(longLived.AccessToken))
        {
            throw new InvalidOperationException("Meta uzun ömürlü jeton yanıtı geçersiz.");
        }
        var profile = await GetMeAsync(longLived.AccessToken, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            throw new InvalidOperationException("Meta /me yanıtında kullanıcı kimliği yok.");
        }

        var email = profile.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            // Meta çoğu uygulama türünde `email` scope'u vermez veya App Review gerektirir.
            // Reklam analizi için zorunlu değil; kullanıcıyı benzersiz yer tutucu e-posta ile oluştururuz.
            email = $"meta-{profile.Id}@oauth.metaadsanalyzer.local";
            _logger.LogInformation("Meta /me e-posta dönmedi; yer tutucu kullanılıyor: {Email}", email);
        }

        var expiresAt = longLived.ExpiresIn is > 0
            ? DateTimeOffset.UtcNow.AddSeconds(longLived.ExpiresIn.Value)
            : (DateTimeOffset?)null;

        return new MetaOAuthResult(longLived.AccessToken, expiresAt, profile.Id, email);
    }

    public async Task<MetaAccessTokenResult> RefreshLongLivedTokenAsync(
        string currentAccessToken,
        CancellationToken cancellationToken = default)
    {
        var token = await ExchangeFbTokenAsync(currentAccessToken, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Meta jeton yenileme yanıtı geçersiz.");
        }

        var expiresAt = token.ExpiresIn is > 0
            ? DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn.Value)
            : (DateTimeOffset?)null;

        return new MetaAccessTokenResult(token.AccessToken, expiresAt);
    }

    private async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken)
    {
        var version = ApiVersionPath();
        var url =
            $"https://graph.facebook.com/{version}/oauth/access_token" +
            $"?client_id={Uri.EscapeDataString(_options.AppId)}" +
            $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
            $"&client_secret={Uri.EscapeDataString(_options.AppSecret)}" +
            $"&code={Uri.EscapeDataString(code)}";

        return await GetJsonAsync<OAuthTokenResponse>(url, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OAuthTokenResponse> ExchangeFbTokenAsync(string fbExchangeToken, CancellationToken cancellationToken)
    {
        var version = ApiVersionPath();
        var url =
            $"https://graph.facebook.com/{version}/oauth/access_token" +
            $"?grant_type=fb_exchange_token" +
            $"&client_id={Uri.EscapeDataString(_options.AppId)}" +
            $"&client_secret={Uri.EscapeDataString(_options.AppSecret)}" +
            $"&fb_exchange_token={Uri.EscapeDataString(fbExchangeToken)}";

        return await GetJsonAsync<OAuthTokenResponse>(url, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MeResponse> GetMeAsync(string accessToken, CancellationToken cancellationToken)
    {
        var version = ApiVersionPath();
        var url =
            $"https://graph.facebook.com/{version}/me?fields=id,email&access_token={Uri.EscapeDataString(accessToken)}";

        return await GetJsonAsync<MeResponse>(url, cancellationToken).ConfigureAwait(false);
    }

    private string ApiVersionPath()
    {
        var v = string.IsNullOrWhiteSpace(_options.ApiVersion) ? "v19.0" : _options.ApiVersion.Trim();
        return v.TrimStart('/');
    }

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Meta Graph HTTP {Status}: {Body}",
                (int)response.StatusCode,
                MetaLogRedactor.ForLog(body));
            TryThrowGraphError(body);
            response.EnsureSuccessStatusCode();
        }

        var parsed = JsonSerializer.Deserialize<T>(body, JsonOptions);
        if (parsed is null)
        {
            throw new InvalidOperationException("Meta API yanıtı çözümlenemedi.");
        }

        return parsed;
    }

    private static void TryThrowGraphError(string body)
    {
        try
        {
            var err = JsonSerializer.Deserialize<GraphErrorEnvelope>(body, JsonOptions);
            if (err?.Error?.Message is { } msg)
            {
                throw new InvalidOperationException($"Meta API: {msg}");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            // yut — genel HTTP hatasına düş
        }
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public long? ExpiresIn { get; set; }
    }

    private sealed class MeResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    private sealed class GraphErrorEnvelope
    {
        [JsonPropertyName("error")]
        public GraphErrorBody? Error { get; set; }
    }

    private sealed class GraphErrorBody
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
