using System.Net;
using System.Text.Json;
using MetaAdsAnalyzer.API.Options;
using Microsoft.Extensions.Options;

namespace MetaAdsAnalyzer.API.Services.Competitors;

public sealed class CompetitorAdLibraryClient : ICompetitorAdLibraryClient
{
    private const string GraphBaseUrl = "https://graph.facebook.com";

    private readonly HttpClient _httpClient;
    private readonly MetaAdLibraryOptions _options;
    private readonly ILogger<CompetitorAdLibraryClient> _logger;

    public CompetitorAdLibraryClient(
        HttpClient httpClient,
        IOptions<MetaAdLibraryOptions> options,
        ILogger<CompetitorAdLibraryClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdLibraryAdItem>> FetchAdsAsync(
        string pageRef,
        string? pageId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AppToken))
        {
            throw new InvalidOperationException("MetaAdLibrary:AppToken tanımlı değil.");
        }

        var pageSize = Math.Clamp(_options.PageSize, 1, 100);
        var maxPages = Math.Max(1, _options.MaxPagesPerRun);
        var since = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-Math.Max(1, _options.DeliveryDateWindowDays)));
        var nextUrl = BuildUrl(pageRef, pageId, since, pageSize);
        var all = new List<AdLibraryAdItem>();

        for (var page = 0; page < maxPages && !string.IsNullOrWhiteSpace(nextUrl); page++)
        {
            var body = await GetWithRetryAsync(nextUrl, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                var msg = err.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : "AdLibrary error";
                throw new InvalidOperationException(msg ?? "AdLibrary error");
            }

            if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataEl.EnumerateArray())
                {
                    var adId = TryGetString(item, "id");
                    if (string.IsNullOrWhiteSpace(adId))
                    {
                        continue;
                    }

                    all.Add(
                        new AdLibraryAdItem
                        {
                            Id = adId,
                            PageId = TryGetString(item, "page_id"),
                            PageName = TryGetString(item, "page_name"),
                            BodyText = JoinFirstOrDefault(item, "ad_creative_bodies"),
                            TitleText = JoinFirstOrDefault(item, "ad_creative_link_titles"),
                            DescriptionText = JoinFirstOrDefault(item, "ad_creative_link_descriptions"),
                            SnapshotUrl = TryGetString(item, "ad_snapshot_url"),
                            PublisherPlatforms = SerializeJsonArray(item, "publisher_platforms"),
                            Languages = SerializeJsonArray(item, "languages"),
                            DeliveryStartTime = TryGetDateTimeOffset(item, "ad_delivery_start_time"),
                            DeliveryStopTime = TryGetDateTimeOffset(item, "ad_delivery_stop_time"),
                            Format = ResolveFormat(item),
                        });
                }
            }

            nextUrl = TryGetPagingNext(doc.RootElement);
            if (!string.IsNullOrWhiteSpace(nextUrl) && _options.InterRequestDelayMs > 0)
            {
                await Task.Delay(_options.InterRequestDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        return all;
    }

    private async Task<string> GetWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        var retries = Math.Max(0, _options.RetryCount);
        for (var attempt = 0; ; attempt++)
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return body;
            }

            if (attempt >= retries || !IsRetryable(response.StatusCode))
            {
                throw new InvalidOperationException($"AdLibrary hata {(int)response.StatusCode}: {body}");
            }

            var delay = (int)(_options.RetryBaseDelayMs * Math.Pow(2, attempt));
            _logger.LogWarning(
                "AdLibrary retry attempt={Attempt} status={StatusCode} delayMs={DelayMs}",
                attempt + 1,
                (int)response.StatusCode,
                delay);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsRetryable(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private string BuildUrl(string pageRef, string? pageId, DateOnly since, int pageSize)
    {
        var accessToken = Uri.EscapeDataString(_options.AppToken);
        var fields = Uri.EscapeDataString(
            "id,page_id,page_name,ad_creative_bodies,ad_creative_link_titles,ad_creative_link_descriptions,ad_snapshot_url,publisher_platforms,ad_delivery_start_time,ad_delivery_stop_time,languages");
        var countries = Uri.EscapeDataString("[\"TR\"]");
        var minDate = Uri.EscapeDataString(since.ToString("yyyy-MM-dd"));
        var query = $"ad_reached_countries={countries}&ad_active_status=ACTIVE&ad_delivery_date_min={minDate}&fields={fields}&limit={pageSize}&access_token={accessToken}";
        if (!string.IsNullOrWhiteSpace(pageId))
        {
            query += $"&search_page_ids={Uri.EscapeDataString(pageId.Trim())}";
        }
        else
        {
            query += $"&search_terms={Uri.EscapeDataString(pageRef)}";
        }

        return $"{GraphBaseUrl}/{_options.ApiVersion}/ads_archive?{query}";
    }

    private static string? TryGetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
        {
            return null;
        }

        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }

    private static string JoinFirstOrDefault(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }
        }

        return string.Empty;
    }

    private static string SerializeJsonArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return "[]";
        }

        return el.GetRawText();
    }

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement root, string name)
    {
        var raw = TryGetString(root, name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, out var dt) ? dt : null;
    }

    private static string ResolveFormat(JsonElement root)
    {
        var body = JoinFirstOrDefault(root, "ad_creative_bodies");
        var title = JoinFirstOrDefault(root, "ad_creative_link_titles");
        var text = $"{body} {title}".ToLowerInvariant();

        if (text.Contains("carousel", StringComparison.Ordinal))
        {
            return "carousel";
        }

        if (text.Contains("video", StringComparison.Ordinal))
        {
            return "video";
        }

        return "static";
    }

    private static string? TryGetPagingNext(JsonElement root)
    {
        if (!root.TryGetProperty("paging", out var paging) || paging.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryGetString(paging, "next");
    }
}
