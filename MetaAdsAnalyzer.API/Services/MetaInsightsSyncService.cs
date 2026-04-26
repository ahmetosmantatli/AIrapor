using System.Globalization;
using System.Text.Json;
using MetaAdsAnalyzer.API.Options;
using MetaAdsAnalyzer.Core;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MetaAdsAnalyzer.API.Services;

public sealed class MetaInsightsSyncService : IMetaInsightsSyncService
{
    private const int PageLimit = 250;
    private static readonly TimeSpan DefaultCacheWindow = TimeSpan.FromHours(4);
    private const string FixedAttributionWindow = "7d_click_1d_view";
    private const string GraphAttributionWindowsParam = "['7d_click','1d_view']";

    /// <summary>
    /// Yalnızca Graph Ads Insights’te geçerli üst-seviye alanlar.
    /// <c>video_*_watched_actions</c> üst seviye alanlar çoğu hesapta reddedilir; ek video metrikleri <see cref="MapRow"/> içinde
    /// <c>video_play_actions</c> ve <c>actions</c> dizisinden türetilir.
    /// </summary>
    private static readonly string[] InsightsFieldList =
    {
        "campaign_id", "campaign_name",
        "adset_id", "adset_name",
        "ad_id", "ad_name",
        "date_start", "date_stop",
        "spend", "impressions", "reach", "frequency",
        "clicks", "inline_link_clicks",
        "ctr", "inline_link_click_ctr", "cpm", "cost_per_inline_link_click",
        "actions", "action_values",
        "video_play_actions",
    };

    private static readonly HashSet<string> PurchaseActionTypes = new(StringComparer.Ordinal)
    {
        "purchase", "omni_purchase", "offsite_conversion.fb_pixel_purchase", "onsite_conversion.purchase",
        "web_in_store_purchase",
    };

    private static readonly HashSet<string> PurchaseValueTypes = PurchaseActionTypes;

    private static readonly HashSet<string> AddToCartTypes = new(StringComparer.Ordinal)
    {
        "add_to_cart", "omni_add_to_cart", "offsite_conversion.fb_pixel_add_to_cart",
    };

    private static readonly HashSet<string> InitiateCheckoutTypes = new(StringComparer.Ordinal)
    {
        "initiate_checkout", "omni_initiated_checkout", "offsite_conversion.fb_pixel_initiate_checkout",
    };

    private static readonly HashSet<string> ViewContentTypes = new(StringComparer.Ordinal)
    {
        "view_content", "omni_view_content", "offsite_conversion.fb_pixel_view_content",
    };

    private static readonly HashSet<string> LandingPageViewTypes = new(StringComparer.Ordinal)
    {
        "landing_page_view",
    };

    private static readonly HashSet<string> Video3sTypes = new(StringComparer.Ordinal)
    {
        "video_view", "video_view_3s", "3_second_video_view",
    };

    private static readonly HashSet<string> ThruplayActionTypes = new(StringComparer.Ordinal)
    {
        "video_thruplay_watched",
    };

    private static readonly HashSet<string> Video15sActionTypes = new(StringComparer.Ordinal)
    {
        "video_15s_watched", "video_15_sec_watched", "15_sec_video_view",
    };

    private static readonly HashSet<string> Video30sActionTypes = new(StringComparer.Ordinal)
    {
        "video_30s_watched", "video_30_sec_watched",
    };

    private static readonly HashSet<string> VideoP25ActionTypes = new(StringComparer.Ordinal)
    {
        "video_p25_watched",
    };

    private static readonly HashSet<string> VideoP50ActionTypes = new(StringComparer.Ordinal)
    {
        "video_p50_watched",
    };

    private static readonly HashSet<string> VideoP75ActionTypes = new(StringComparer.Ordinal)
    {
        "video_p75_watched",
    };

    private static readonly HashSet<string> VideoP95ActionTypes = new(StringComparer.Ordinal)
    {
        "video_p95_watched",
    };

    private static readonly HashSet<string> VideoP100ActionTypes = new(StringComparer.Ordinal)
    {
        "video_p100_watched",
    };

    private static readonly HashSet<string> VideoAvgTimeActionTypes = new(StringComparer.Ordinal)
    {
        "video_avg_time_watched",
    };

    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly MetaOptions _options;
    private readonly IMetaAccessTokenProtector _tokenProtector;
    private readonly ILogger<MetaInsightsSyncService> _logger;

    public MetaInsightsSyncService(
        HttpClient http,
        AppDbContext db,
        IOptions<MetaOptions> options,
        IMetaAccessTokenProtector tokenProtector,
        ILogger<MetaInsightsSyncService> logger)
    {
        _http = http;
        _db = db;
        _options = options.Value;
        _tokenProtector = tokenProtector;
        _logger = logger;
    }

    private const int MaxAdIdsPerInsightsSync = 80;

    public async Task<InsightsSyncResponseDto> SyncInsightsAsync(
        int userId,
        string level,
        string datePreset,
        string? adId = null,
        string? metaAdAccountId = null,
        IReadOnlyList<string>? adIds = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var accessToken = RequirePlainAccessToken(user);

        level = level.Trim().ToLowerInvariant();
        if (level is not ("campaign" or "adset" or "ad"))
        {
            throw new ArgumentException("level campaign, adset veya ad olmalıdır.", nameof(level));
        }

        datePreset = string.IsNullOrWhiteSpace(datePreset) ? "last_7d" : datePreset.Trim();

        var merged = new List<string>();
        if (adIds is { Count: > 0 })
        {
            foreach (var raw in adIds)
            {
                var t = raw?.Trim();
                if (string.IsNullOrEmpty(t))
                {
                    continue;
                }

                if (!t.All(static c => c is >= '0' and <= '9'))
                {
                    throw new ArgumentException("Ad kimlikleri yalnızca rakamlardan oluşmalıdır.", nameof(adIds));
                }

                if (!merged.Contains(t, StringComparer.Ordinal))
                {
                    merged.Add(t);
                }
            }
        }

        if (merged.Count == 0 && !string.IsNullOrWhiteSpace(adId))
        {
            var t = adId.Trim();
            if (t.Length == 0 || !t.All(static c => c is >= '0' and <= '9'))
            {
                throw new ArgumentException("AdId yalnızca rakamlardan oluşmalıdır.", nameof(adId));
            }

            merged.Add(t);
        }

        string? filteringJson = null;
        if (merged.Count > 0)
        {
            level = "ad";
            if (merged.Count > MaxAdIdsPerInsightsSync)
            {
                throw new ArgumentException($"Bir seferde en fazla {MaxAdIdsPerInsightsSync} reklam için insights çekilebilir.");
            }

            filteringJson = BuildAdIdsFilteringJson(merged);
        }

        var actId = await ResolveActIdForUserAsync(userId, metaAdAccountId, cancellationToken).ConfigureAwait(false);
        if (!force)
        {
            var cacheHit = await HasFreshInsightsAsync(userId, actId, level, merged, cancellationToken).ConfigureAwait(false);
            if (cacheHit)
            {
                return new InsightsSyncResponseDto();
            }
        }

        var version = string.IsNullOrWhiteSpace(_options.ApiVersion) ? "v19.0" : _options.ApiVersion.Trim().TrimStart('/');
        var firstUrl = BuildInsightsFirstUrl(actId, accessToken, version, level, datePreset, filteringJson);

        return await FetchAndUpsertInsightPagesAsync(firstUrl, userId, level, actId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildAdIdsFilteringJson(IReadOnlyList<string> adIds)
    {
        var node = new Dictionary<string, object>
        {
            ["field"] = "ad.id",
            ["operator"] = "IN",
            ["value"] = adIds.ToArray(),
        };
        return JsonSerializer.Serialize(new[] { node });
    }

    /// <summary>Graph <c>act_…/ads</c> için kampanya veya reklam seti filtresi (adset önceliklidir).</summary>
    private static string? BuildCampaignOrAdsetAdsFilteringJson(string? campaignId, string? adsetId)
    {
        if (!string.IsNullOrWhiteSpace(adsetId))
        {
            var id = adsetId.Trim();
            if (!id.All(static c => c is >= '0' and <= '9'))
            {
                throw new ArgumentException("adsetId yalnızca rakamlardan oluşmalıdır.", nameof(adsetId));
            }

            return JsonSerializer.Serialize(
                new[]
                {
                    new Dictionary<string, object>
                    {
                        ["field"] = "adset.id",
                        ["operator"] = "EQUAL",
                        ["value"] = id,
                    },
                });
        }

        if (!string.IsNullOrWhiteSpace(campaignId))
        {
            var id = campaignId.Trim();
            if (!id.All(static c => c is >= '0' and <= '9'))
            {
                throw new ArgumentException("campaignId yalnızca rakamlardan oluşmalıdır.", nameof(campaignId));
            }

            return JsonSerializer.Serialize(
                new[]
                {
                    new Dictionary<string, object>
                    {
                        ["field"] = "campaign.id",
                        ["operator"] = "EQUAL",
                        ["value"] = id,
                    },
                });
        }

        return null;
    }

    private static string BuildInsightsFirstUrl(
        string actId,
        string accessToken,
        string version,
        string level,
        string datePreset,
        string? filteringJson)
    {
        var fields = string.Join(",", InsightsFieldList);
        var url =
            $"https://graph.facebook.com/{version}/{actId}/insights" +
            $"?access_token={Uri.EscapeDataString(accessToken)}" +
            $"&fields={Uri.EscapeDataString(fields)}" +
            $"&level={Uri.EscapeDataString(level)}" +
            $"&date_preset={Uri.EscapeDataString(datePreset)}" +
            $"&action_attribution_windows={Uri.EscapeDataString(GraphAttributionWindowsParam)}" +
            $"&limit={PageLimit}";
        if (!string.IsNullOrEmpty(filteringJson))
        {
            url += $"&filtering={Uri.EscapeDataString(filteringJson)}";
        }

        return url;
    }

    private async Task<InsightsSyncResponseDto> FetchAndUpsertInsightPagesAsync(
        string firstUrl,
        int userId,
        string level,
        string actId,
        CancellationToken cancellationToken)
    {
        string? nextUrl = firstUrl;
        var fetched = 0;
        var upserted = 0;
        var pages = 0;

        while (!string.IsNullOrEmpty(nextUrl))
        {
            pages++;
            using var response = await _http.GetAsync(nextUrl, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Meta insights HTTP {Code}: {Body}",
                    (int)response.StatusCode,
                    MetaLogRedactor.ForLog(body));
                ThrowIfGraphError(body);
                response.EnsureSuccessStatusCode();
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var row in data.EnumerateArray())
            {
                fetched++;
                var mapped = MapRow(userId, level, row, actId);
                if (mapped is null)
                {
                    continue;
                }

                var existing = await _db.RawInsights.FirstOrDefaultAsync(
                        r => r.UserId == userId
                             && r.MetaAdAccountId == actId
                             && r.Level == level
                             && r.EntityId == mapped.EntityId
                             && r.DateStart == mapped.DateStart
                             && r.DateStop == mapped.DateStop,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    _db.RawInsights.Add(mapped);
                }
                else
                {
                    CopyMetrics(existing, mapped);
                }

                upserted++;
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            nextUrl = null;
            if (root.TryGetProperty("paging", out var paging)
                && paging.TryGetProperty("next", out var next)
                && next.ValueKind == JsonValueKind.String)
            {
                nextUrl = next.GetString();
            }
        }

        return new InsightsSyncResponseDto
        {
            RowsFetched = fetched,
            RowsUpserted = upserted,
            PageCount = pages,
        };
    }

    public async Task<IReadOnlyList<MetaAdListItemDto>> ListAccountAdsAsync(
        int userId,
        string? metaAdAccountId = null,
        string? campaignId = null,
        string? adsetId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var accessToken = RequirePlainAccessToken(user);
        var actId = await ResolveActIdForUserAsync(userId, metaAdAccountId, cancellationToken).ConfigureAwait(false);
        var version = string.IsNullOrWhiteSpace(_options.ApiVersion) ? "v19.0" : _options.ApiVersion.Trim().TrimStart('/');
        const string adFields = "id,name,status,effective_status,creative{id,name,title,thumbnail_url,video_id}";
        var filterJson = BuildCampaignOrAdsetAdsFilteringJson(campaignId, adsetId);

        _logger.LogInformation(
            "Meta Graph GET /{Version}/{ActId}/ads (token logda yok). UserId={UserId} QueryFields={Fields} Limit={Limit} Filter={HasFilter}",
            version,
            actId,
            userId,
            adFields,
            PageLimit,
            filterJson != null);

        var list = new List<MetaAdListItemDto>();
        string? nextUrl =
            $"https://graph.facebook.com/{version}/{actId}/ads" +
            $"?access_token={Uri.EscapeDataString(accessToken)}" +
            $"&fields={Uri.EscapeDataString(adFields)}" +
            $"&limit={PageLimit}";
        if (!string.IsNullOrEmpty(filterJson))
        {
            nextUrl += $"&filtering={Uri.EscapeDataString(filterJson)}";
        }

        var pageOrdinal = 0;
        while (!string.IsNullOrEmpty(nextUrl))
        {
            pageOrdinal++;
            using var response = await _http.GetAsync(nextUrl, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Meta ads list başarısız UserId={UserId} ActId={ActId} Page={Page} HTTP={Code} Body={Body}",
                    userId,
                    actId,
                    pageOrdinal,
                    (int)response.StatusCode,
                    MetaLogRedactor.ForLog(body));
                ThrowIfGraphError(body);
                response.EnsureSuccessStatusCode();
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning(
                    "Meta ads yanıtında data[] yok UserId={UserId} ActId={ActId} Page={Page} Body={Body}",
                    userId,
                    actId,
                    pageOrdinal,
                    MetaLogRedactor.ForLog(body));
                break;
            }

            var rowsInPage = 0;
            foreach (var item in data.EnumerateArray())
            {
                var id = GetString(item, "id");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                string? crId = null;
                string? crName = null;
                string? thumb = null;
                string? vid = null;
                string? vtitle = null;
                if (item.TryGetProperty("creative", out var cr) && cr.ValueKind == JsonValueKind.Object)
                {
                    crId = GetString(cr, "id");
                    crName = GetString(cr, "name");
                    thumb = GetString(cr, "thumbnail_url");
                    vid = GetString(cr, "video_id");
                    vtitle = GetString(cr, "title");
                }

                list.Add(
                    new MetaAdListItemDto
                    {
                        Id = id,
                        Name = GetString(item, "name"),
                        Status = GetString(item, "status"),
                        EffectiveStatus = GetString(item, "effective_status"),
                        CreativeId = crId,
                        CreativeName = crName,
                        VideoId = vid,
                        VideoTitle = vtitle,
                        ThumbnailUrl = thumb,
                    });
                rowsInPage++;
            }

            if (pageOrdinal == 1)
            {
                _logger.LogInformation(
                    "Meta ads ilk sayfa OK UserId={UserId} ActId={ActId} Satır={RowsInPage} BirikmişToplam={Total}",
                    userId,
                    actId,
                    rowsInPage,
                    list.Count);
            }

            nextUrl = null;
            if (root.TryGetProperty("paging", out var paging)
                && paging.TryGetProperty("next", out var next)
                && next.ValueKind == JsonValueKind.String)
            {
                nextUrl = next.GetString();
            }
        }

        _logger.LogInformation(
            "Meta ads listesi bitti UserId={UserId} ActId={ActId} ToplamReklam={Total} Sayfa={Pages}",
            userId,
            actId,
            list.Count,
            pageOrdinal);

        return list;
    }

    public async Task<IReadOnlyList<MetaCampaignListItemDto>> ListCampaignsAsync(
        int userId,
        string? metaAdAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var accessToken = RequirePlainAccessToken(user);
        var actId = await ResolveActIdForUserAsync(userId, metaAdAccountId, cancellationToken).ConfigureAwait(false);
        var version = string.IsNullOrWhiteSpace(_options.ApiVersion) ? "v19.0" : _options.ApiVersion.Trim().TrimStart('/');
        const string campaignFields = "id,name,status,objective";

        var list = new List<MetaCampaignListItemDto>();
        string? nextUrl =
            $"https://graph.facebook.com/{version}/{actId}/campaigns" +
            $"?access_token={Uri.EscapeDataString(accessToken)}" +
            $"&fields={Uri.EscapeDataString(campaignFields)}" +
            $"&limit={PageLimit}";

        var pageOrdinal = 0;
        while (!string.IsNullOrEmpty(nextUrl))
        {
            pageOrdinal++;
            using var response = await _http.GetAsync(nextUrl, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Meta campaigns list başarısız UserId={UserId} ActId={ActId} Page={Page} HTTP={Code} Body={Body}",
                    userId,
                    actId,
                    pageOrdinal,
                    (int)response.StatusCode,
                    MetaLogRedactor.ForLog(body));
                ThrowIfGraphError(body);
                response.EnsureSuccessStatusCode();
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var item in data.EnumerateArray())
            {
                var id = GetString(item, "id");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                list.Add(
                    new MetaCampaignListItemDto
                    {
                        Id = id,
                        Name = GetString(item, "name"),
                        Status = GetString(item, "status"),
                        Objective = GetString(item, "objective"),
                    });
            }

            nextUrl = null;
            if (root.TryGetProperty("paging", out var paging)
                && paging.TryGetProperty("next", out var next)
                && next.ValueKind == JsonValueKind.String)
            {
                nextUrl = next.GetString();
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<MetaAdsetListItemDto>> ListAdsetsAsync(
        int userId,
        string campaignId,
        string? metaAdAccountId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId) || !campaignId.Trim().All(static c => c is >= '0' and <= '9'))
        {
            throw new ArgumentException("campaignId zorunludur ve yalnızca rakamlardan oluşmalıdır.", nameof(campaignId));
        }

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var accessToken = RequirePlainAccessToken(user);
        await ResolveActIdForUserAsync(userId, metaAdAccountId, cancellationToken).ConfigureAwait(false);
        var version = string.IsNullOrWhiteSpace(_options.ApiVersion) ? "v19.0" : _options.ApiVersion.Trim().TrimStart('/');
        var cid = campaignId.Trim();
        const string adsetFields = "id,name,status,campaign_id";

        var list = new List<MetaAdsetListItemDto>();
        string? nextUrl =
            $"https://graph.facebook.com/{version}/{cid}/adsets" +
            $"?access_token={Uri.EscapeDataString(accessToken)}" +
            $"&fields={Uri.EscapeDataString(adsetFields)}" +
            $"&limit={PageLimit}";

        while (!string.IsNullOrEmpty(nextUrl))
        {
            using var response = await _http.GetAsync(nextUrl, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Meta adsets list başarısız UserId={UserId} CampaignId={CampaignId} HTTP={Code} Body={Body}",
                    userId,
                    cid,
                    (int)response.StatusCode,
                    MetaLogRedactor.ForLog(body));
                ThrowIfGraphError(body);
                response.EnsureSuccessStatusCode();
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var item in data.EnumerateArray())
            {
                var id = GetString(item, "id");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                list.Add(
                    new MetaAdsetListItemDto
                    {
                        Id = id,
                        Name = GetString(item, "name"),
                        Status = GetString(item, "status"),
                        CampaignId = GetString(item, "campaign_id"),
                    });
            }

            nextUrl = null;
            if (root.TryGetProperty("paging", out var paging)
                && paging.TryGetProperty("next", out var next)
                && next.ValueKind == JsonValueKind.String)
            {
                nextUrl = next.GetString();
            }
        }

        return list;
    }

    public async Task<int> SyncLinkedAdAccountsFromGraphAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var accessToken = RequirePlainAccessToken(user);
        var accounts = await FetchAdAccountsWithTokenAsync(accessToken, cancellationToken).ConfigureAwait(false);
        var newCount = 0;
        foreach (var item in accounts)
        {
            var act = MetaAdAccountIdNormalizer.Normalize(item.Id);
            if (string.IsNullOrEmpty(act))
            {
                continue;
            }

            var row = await _db.UserMetaAdAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MetaAdAccountId == act, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                _db.UserMetaAdAccounts.Add(
                    new UserMetaAdAccount
                    {
                        UserId = userId,
                        MetaAdAccountId = act,
                        DisplayName = item.Name,
                        LinkedAt = DateTimeOffset.UtcNow,
                    });
                newCount++;
            }
            else
            {
                row.DisplayName = item.Name;
            }
        }

        if (string.IsNullOrEmpty(MetaAdAccountIdNormalizer.Normalize(user.MetaAdAccountId)) && accounts.Count > 0)
        {
            user.MetaAdAccountId = MetaAdAccountIdNormalizer.Normalize(accounts[0].Id);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return newCount;
    }

    public async Task<IReadOnlyList<MetaAdAccountItemDto>> ListAdAccountsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var accessToken = RequirePlainAccessToken(user);
        return await FetchAdAccountsWithTokenAsync(accessToken, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<MetaAdAccountItemDto>> FetchAdAccountsWithTokenAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var version = string.IsNullOrWhiteSpace(_options.ApiVersion) ? "v19.0" : _options.ApiVersion.Trim().TrimStart('/');
        var url =
            $"https://graph.facebook.com/{version}/me/adaccounts" +
            $"?access_token={Uri.EscapeDataString(accessToken)}" +
            $"&fields=id,name,account_id&limit=500";

        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Meta adaccounts HTTP {Code}: {Body}",
                (int)response.StatusCode,
                MetaLogRedactor.ForLog(body));
            ThrowIfGraphError(body);
            response.EnsureSuccessStatusCode();
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MetaAdAccountItemDto>();
        }

        var list = new List<MetaAdAccountItemDto>();
        foreach (var item in data.EnumerateArray())
        {
            var id = GetString(item, "id");
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            list.Add(
                new MetaAdAccountItemDto
                {
                    Id = id,
                    Name = GetString(item, "name"),
                    AccountId = GetString(item, "account_id"),
                });
        }

        return list;
    }

    private async Task<string> ResolveActIdForUserAsync(
        int userId,
        string? requestedAct,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedAct))
        {
            var norm = MetaAdAccountIdNormalizer.Normalize(requestedAct);
            if (string.IsNullOrEmpty(norm))
            {
                throw new InvalidOperationException("Geçersiz reklam hesabı kimliği.");
            }

            var linked = await _db.UserMetaAdAccounts.AsNoTracking()
                .AnyAsync(x => x.UserId == userId && x.MetaAdAccountId == norm, cancellationToken)
                .ConfigureAwait(false);
            if (!linked)
            {
                throw new InvalidOperationException(
                    "Bu reklam hesabı bağlı değil. Meta ile giriş yapın veya Ayarlar’dan hesap bağlayın.");
            }

            return norm;
        }

        var meta = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.MetaAdAccountId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var act = MetaAdAccountIdNormalizer.Normalize(meta);
        if (string.IsNullOrEmpty(act))
        {
            throw new InvalidOperationException("Önce Video raporu veya ayarlardan bir reklam hesabı seçin.");
        }

        var inLinks = await _db.UserMetaAdAccounts.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.MetaAdAccountId == act, cancellationToken)
            .ConfigureAwait(false);
        if (!inLinks)
        {
            throw new InvalidOperationException(
                "Aktif reklam hesabı bağlı listesinde yok. Meta ile yeniden bağlanın veya hesabı seçin.");
        }

        return act;
    }

    private async Task<bool> HasFreshInsightsAsync(
        int userId,
        string metaAdAccountId,
        string level,
        IReadOnlyList<string> adIds,
        CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow - DefaultCacheWindow;
        var query = _db.RawInsights.AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.MetaAdAccountId == metaAdAccountId &&
                x.Level == level &&
                x.FetchedAt >= since);

        if (adIds.Count > 0)
        {
            query = query.Where(x => adIds.Contains(x.EntityId));
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    private string RequirePlainAccessToken(User user)
    {
        if (string.IsNullOrWhiteSpace(user.MetaAccessToken))
        {
            throw new InvalidOperationException("Meta erişim jetonu yok. Önce OAuth ile giriş yapın.");
        }

        if (!_tokenProtector.TryUnprotect(user.MetaAccessToken, out var plain) || string.IsNullOrWhiteSpace(plain))
        {
            throw new InvalidOperationException("Meta erişim jetonu çözülemedi.");
        }

        return plain;
    }

    private static RawInsight? MapRow(int userId, string level, JsonElement row, string actId)
    {
        var (entityIdRaw, entityName) = GetEntityInfo(level, row);
        var entityId = entityIdRaw.Trim();
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }

        var ds = GetString(row, "date_start");
        var de = GetString(row, "date_stop");
        if (!DateOnly.TryParse(ds, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateStart)
            || !DateOnly.TryParse(de, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateStop))
        {
            return null;
        }

        row.TryGetProperty("actions", out var actions);
        row.TryGetProperty("action_values", out var actionValues);

        var purchases = SumMatchingActionLong(actions, PurchaseActionTypes);
        var purchaseValue = SumMatchingActionDecimal(actionValues, PurchaseValueTypes);

        row.TryGetProperty("video_play_actions", out var vPlay);
        var video3s = SumVideo3s(vPlay);

        var videoThru = SumMatchingActionLong(actions, ThruplayActionTypes);

        var metaCampaignId = GetString(row, "campaign_id");
        if (string.IsNullOrEmpty(metaCampaignId) && string.Equals(level, "campaign", StringComparison.OrdinalIgnoreCase))
        {
            metaCampaignId = entityId;
        }

        var metaAdsetId = GetString(row, "adset_id");
        if (string.IsNullOrEmpty(metaAdsetId) && string.Equals(level, "adset", StringComparison.OrdinalIgnoreCase))
        {
            metaAdsetId = entityId;
        }

        return new RawInsight
        {
            UserId = userId,
            MetaAdAccountId = actId,
            FetchedAt = DateTimeOffset.UtcNow,
            Level = level,
            EntityId = entityId,
            EntityName = string.IsNullOrWhiteSpace(entityName) ? null : entityName,
            MetaCampaignId = string.IsNullOrWhiteSpace(metaCampaignId) ? null : metaCampaignId,
            MetaAdsetId = string.IsNullOrWhiteSpace(metaAdsetId) ? null : metaAdsetId,
            AttributionWindow = FixedAttributionWindow,
            DateStart = dateStart,
            DateStop = dateStop,
            Spend = ParseDecimal(row, "spend"),
            Impressions = ParseLong(row, "impressions"),
            Reach = ParseLong(row, "reach"),
            Frequency = ParseDecimal(row, "frequency"),
            LinkClicks = ParseLong(row, "inline_link_clicks"),
            CtrLink = ParseDecimal(row, "inline_link_click_ctr"),
            CtrAll = ParseDecimal(row, "ctr"),
            Cpm = ParseDecimal(row, "cpm"),
            CpcLink = ParseDecimal(row, "cost_per_inline_link_click"),
            Purchases = purchases,
            PurchaseValue = purchaseValue,
            AddToCart = SumMatchingActionLong(actions, AddToCartTypes),
            InitiateCheckout = SumMatchingActionLong(actions, InitiateCheckoutTypes),
            ViewContent = SumMatchingActionLong(actions, ViewContentTypes),
            LandingPageViews = SumMatchingActionLong(actions, LandingPageViewTypes),
            VideoPlay3s = video3s,
            VideoThruplay = videoThru,
            Video15Sec = SumMatchingActionLong(actions, Video15sActionTypes),
            Video30Sec = SumMatchingActionLong(actions, Video30sActionTypes),
            VideoP95 = SumMatchingActionLong(actions, VideoP95ActionTypes),
            VideoP25 = SumMatchingActionLong(actions, VideoP25ActionTypes),
            VideoP50 = SumMatchingActionLong(actions, VideoP50ActionTypes),
            VideoP75 = SumMatchingActionLong(actions, VideoP75ActionTypes),
            VideoP100 = SumMatchingActionLong(actions, VideoP100ActionTypes),
            VideoAvgWatchTime = SumMatchingActionDecimal(actions, VideoAvgTimeActionTypes),
        };
    }

    private static void CopyMetrics(RawInsight target, RawInsight source)
    {
        target.FetchedAt = source.FetchedAt;
        target.EntityName = source.EntityName;
        target.MetaCampaignId = source.MetaCampaignId;
        target.MetaAdsetId = source.MetaAdsetId;
        target.MetaAdAccountId = source.MetaAdAccountId;
        target.AttributionWindow = source.AttributionWindow;
        target.Spend = source.Spend;
        target.Impressions = source.Impressions;
        target.Reach = source.Reach;
        target.Frequency = source.Frequency;
        target.LinkClicks = source.LinkClicks;
        target.CtrLink = source.CtrLink;
        target.CtrAll = source.CtrAll;
        target.Cpm = source.Cpm;
        target.CpcLink = source.CpcLink;
        target.Purchases = source.Purchases;
        target.PurchaseValue = source.PurchaseValue;
        target.AddToCart = source.AddToCart;
        target.InitiateCheckout = source.InitiateCheckout;
        target.ViewContent = source.ViewContent;
        target.LandingPageViews = source.LandingPageViews;
        target.VideoPlay3s = source.VideoPlay3s;
        target.VideoThruplay = source.VideoThruplay;
        target.Video15Sec = source.Video15Sec;
        target.Video30Sec = source.Video30Sec;
        target.VideoP95 = source.VideoP95;
        target.VideoP25 = source.VideoP25;
        target.VideoP50 = source.VideoP50;
        target.VideoP75 = source.VideoP75;
        target.VideoP100 = source.VideoP100;
        target.VideoAvgWatchTime = source.VideoAvgWatchTime;
    }

    private static (string Id, string? Name) GetEntityInfo(string level, JsonElement row)
    {
        return level switch
        {
            "campaign" => (GetString(row, "campaign_id"), GetString(row, "campaign_name")),
            "adset" => (GetString(row, "adset_id"), GetString(row, "adset_name")),
            "ad" => (GetString(row, "ad_id"), GetString(row, "ad_name")),
            _ => (string.Empty, null),
        };
    }

    private static string GetString(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var p))
        {
            return string.Empty;
        }

        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString() ?? string.Empty,
            JsonValueKind.Number => p.GetRawText(),
            _ => string.Empty,
        };
    }

    private static long ParseLong(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p))
        {
            return 0;
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n))
        {
            return n;
        }

        if (p.ValueKind == JsonValueKind.String
            && long.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
        {
            return v;
        }

        return 0;
    }

    private static decimal ParseDecimal(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p))
        {
            return 0m;
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d))
        {
            return d;
        }

        if (p.ValueKind == JsonValueKind.String
            && decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
        {
            return v;
        }

        return 0m;
    }

    private static long SumMatchingActionLong(JsonElement actions, HashSet<string> types)
    {
        if (actions.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        long sum = 0;
        foreach (var item in actions.EnumerateArray())
        {
            var type = GetString(item, "action_type");
            if (string.IsNullOrEmpty(type) || !types.Contains(type))
            {
                continue;
            }

            sum += ParseLong(item, "value");
        }

        return sum;
    }

    private static decimal SumMatchingActionDecimal(JsonElement actions, HashSet<string> types)
    {
        if (actions.ValueKind != JsonValueKind.Array)
        {
            return 0m;
        }

        decimal sum = 0;
        foreach (var item in actions.EnumerateArray())
        {
            var type = GetString(item, "action_type");
            if (string.IsNullOrEmpty(type) || !types.Contains(type))
            {
                continue;
            }

            sum += ParseDecimal(item, "value");
        }

        return sum;
    }

    private static long SumVideo3s(JsonElement videoPlayActions)
    {
        if (videoPlayActions.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        long sum = 0;
        foreach (var item in videoPlayActions.EnumerateArray())
        {
            var type = GetString(item, "action_type");
            if (string.IsNullOrEmpty(type))
            {
                continue;
            }

            if (Video3sTypes.Contains(type) || type.Contains("3_sec", StringComparison.OrdinalIgnoreCase)
                                             || type.Contains("3s", StringComparison.OrdinalIgnoreCase))
            {
                sum += ParseLong(item, "value");
            }
        }

        return sum;
    }

    private static long SumAllActionLong(JsonElement row, string propertyName)
    {
        if (!row.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        long sum = 0;
        foreach (var item in arr.EnumerateArray())
        {
            sum += ParseLong(item, "value");
        }

        return sum;
    }

    private static decimal SumAllActionDecimal(JsonElement row, string propertyName)
    {
        if (!row.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return 0m;
        }

        decimal sum = 0;
        foreach (var item in arr.EnumerateArray())
        {
            sum += ParseDecimal(item, "value");
        }

        return sum;
    }

    private static void ThrowIfGraphError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg)
                                                         && msg.ValueKind == JsonValueKind.String)
            {
                throw new InvalidOperationException($"Meta API: {msg.GetString()}");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            // yut
        }
    }
}
