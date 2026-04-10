using System.Globalization;
using System.Text.Json;
using MetaAdsAnalyzer.API.Options;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MetaAdsAnalyzer.API.Services;

public sealed class MetaInsightsSyncService : IMetaInsightsSyncService
{
    private const int PageLimit = 250;

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
        "video_play_actions", "video_thruplay_watched_actions",
        "video_p25_watched_actions", "video_p50_watched_actions",
        "video_p75_watched_actions", "video_p100_watched_actions",
        "video_avg_time_watched_actions",
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

    private static readonly HashSet<string> Video3sTypes = new(StringComparer.Ordinal)
    {
        "video_view", "video_view_3s", "3_second_video_view",
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

    public async Task<InsightsSyncResponseDto> SyncInsightsAsync(
        int userId,
        string level,
        string datePreset,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var accessToken = RequirePlainAccessToken(user);
        if (string.IsNullOrWhiteSpace(user.MetaAdAccountId))
        {
            throw new InvalidOperationException(
                "Reklam hesabı seçilmemiş. MetaAdAccountId alanını doldurun veya /api/meta/ad-accounts ile listeyi alın.");
        }

        level = level.Trim().ToLowerInvariant();
        if (level is not ("campaign" or "adset" or "ad"))
        {
            throw new ArgumentException("level campaign, adset veya ad olmalıdır.", nameof(level));
        }

        datePreset = string.IsNullOrWhiteSpace(datePreset) ? "last_7d" : datePreset.Trim();

        var actId = NormalizeAdAccountId(user.MetaAdAccountId);
        var version = string.IsNullOrWhiteSpace(_options.ApiVersion) ? "v19.0" : _options.ApiVersion.Trim().TrimStart('/');
        var fields = string.Join(",", InsightsFieldList);

        var firstUrl =
            $"https://graph.facebook.com/{version}/{actId}/insights" +
            $"?access_token={Uri.EscapeDataString(accessToken)}" +
            $"&fields={Uri.EscapeDataString(fields)}" +
            $"&level={Uri.EscapeDataString(level)}" +
            $"&date_preset={Uri.EscapeDataString(datePreset)}" +
            $"&limit={PageLimit}";

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
                var mapped = MapRow(userId, level, row);
                if (mapped is null)
                {
                    continue;
                }

                var existing = await _db.RawInsights.FirstOrDefaultAsync(
                        r => r.UserId == userId
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

    private static RawInsight? MapRow(int userId, string level, JsonElement row)
    {
        var (entityId, entityName) = GetEntityInfo(level, row);
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

        var metaCampaignId = GetString(row, "campaign_id");
        if (string.IsNullOrEmpty(metaCampaignId) && string.Equals(level, "campaign", StringComparison.OrdinalIgnoreCase))
        {
            metaCampaignId = entityId;
        }

        return new RawInsight
        {
            UserId = userId,
            FetchedAt = DateTimeOffset.UtcNow,
            Level = level,
            EntityId = entityId,
            EntityName = string.IsNullOrWhiteSpace(entityName) ? null : entityName,
            MetaCampaignId = string.IsNullOrWhiteSpace(metaCampaignId) ? null : metaCampaignId,
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
            VideoPlay3s = video3s,
            VideoThruplay = SumAllActionLong(row, "video_thruplay_watched_actions"),
            VideoP25 = SumAllActionLong(row, "video_p25_watched_actions"),
            VideoP50 = SumAllActionLong(row, "video_p50_watched_actions"),
            VideoP75 = SumAllActionLong(row, "video_p75_watched_actions"),
            VideoP100 = SumAllActionLong(row, "video_p100_watched_actions"),
            VideoAvgWatchTime = SumAllActionDecimal(row, "video_avg_time_watched_actions"),
        };
    }

    private static void CopyMetrics(RawInsight target, RawInsight source)
    {
        target.FetchedAt = source.FetchedAt;
        target.EntityName = source.EntityName;
        target.MetaCampaignId = source.MetaCampaignId;
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
        target.VideoPlay3s = source.VideoPlay3s;
        target.VideoThruplay = source.VideoThruplay;
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

    private static string NormalizeAdAccountId(string raw)
    {
        var t = raw.Trim();
        if (t.StartsWith("act_", StringComparison.OrdinalIgnoreCase))
        {
            return t;
        }

        return "act_" + t;
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
