using MetaAdsAnalyzer.Core;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MetaAdsAnalyzer.API.Services;

public sealed class VideoAssetSyncService : IVideoAssetSyncService
{
    private readonly AppDbContext _db;
    private readonly ILogger<VideoAssetSyncService> _logger;

    public VideoAssetSyncService(AppDbContext db, ILogger<VideoAssetSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SyncFromAdListAsync(
        int userId,
        string metaAdAccountId,
        IReadOnlyList<MetaAdListItemDto> ads,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SyncFromAdListCoreAsync(userId, metaAdAccountId, ads, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogWarning(
                ex,
                "Video senkronu atlandı: veritabanında tablo yok (migration uygulanmamış olabilir). " +
                "Çözüm: MetaAdsAnalyzer.Infrastructure dizininde " +
                "\"dotnet ef database update --startup-project ../MetaAdsAnalyzer.API/MetaAdsAnalyzer.API.csproj\"");
        }
    }

    private async Task SyncFromAdListCoreAsync(
        int userId,
        string metaAdAccountId,
        IReadOnlyList<MetaAdListItemDto> ads,
        CancellationToken cancellationToken)
    {
        var act = MetaAdAccountIdNormalizer.Normalize(metaAdAccountId);
        if (string.IsNullOrEmpty(act))
        {
            var u = await _db.Users.AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => x.MetaAdAccountId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            act = MetaAdAccountIdNormalizer.Normalize(u);
        }

        if (string.IsNullOrEmpty(act))
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;

        foreach (var ad in ads)
        {
            if (string.IsNullOrWhiteSpace(ad.Id))
            {
                continue;
            }

            var row = await _db.AdVideoLinks.FirstOrDefaultAsync(
                    x => x.UserId == userId && x.MetaAdAccountId == act && x.AdId == ad.Id,
                    cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                _db.AdVideoLinks.Add(
                    new AdVideoLink
                    {
                        UserId = userId,
                        MetaAdAccountId = act,
                        AdId = ad.Id,
                        VideoId = string.IsNullOrWhiteSpace(ad.VideoId) ? null : ad.VideoId.Trim(),
                        ThumbnailUrl = TruncateThumbnailUrl(ad.ThumbnailUrl),
                        AdName = ad.Name ?? ad.CreativeName,
                        UpdatedAt = now,
                    });
            }
            else
            {
                row.VideoId = string.IsNullOrWhiteSpace(ad.VideoId) ? null : ad.VideoId.Trim();
                row.ThumbnailUrl = TruncateThumbnailUrl(ad.ThumbnailUrl);
                row.AdName = ad.Name ?? ad.CreativeName;
                row.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var videoIds = ads
            .Select(a => a.VideoId?.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var vid in videoIds)
        {
            if (string.IsNullOrEmpty(vid))
            {
                continue;
            }

            var adIdsForVideo = await _db.AdVideoLinks.AsNoTracking()
                .Where(x => x.UserId == userId && x.MetaAdAccountId == act && x.VideoId == vid)
                .Select(x => x.AdId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (adIdsForVideo.Count == 0)
            {
                continue;
            }

            var raws = await _db.RawInsights.AsNoTracking()
                .Where(r => r.UserId == userId && r.MetaAdAccountId == act && r.Level == "ad"
                            && adIdsForVideo.Contains(r.EntityId))
                .OrderByDescending(r => r.FetchedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var latestByAd = new Dictionary<string, RawInsight>(StringComparer.Ordinal);
            foreach (var r in raws)
            {
                if (!latestByAd.ContainsKey(r.EntityId))
                {
                    latestByAd[r.EntityId] = r;
                }
            }

            if (latestByAd.Count == 0)
            {
                var shell = ads.Where(a => string.Equals(a.VideoId?.Trim(), vid, StringComparison.Ordinal))
                    .OrderByDescending(a => a.Name?.Length ?? 0)
                    .FirstOrDefault();
                await UpsertShellVideoAssetAsync(
                        userId,
                        act,
                        vid,
                        shell?.ThumbnailUrl,
                        shell?.CreativeName ?? shell?.Name,
                        today,
                        now,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            decimal totalSpend = 0;
            long totalPurchases = 0;
            decimal totalPurchaseValue = 0;
            decimal wThumb = 0;
            decimal wHold = 0;
            decimal wComp = 0;
            decimal denThumb = 0;
            decimal denHold = 0;
            decimal denComp = 0;

            string? repName = null;
            decimal repSpend = -1;
            string? thumbUrl = ads
                .FirstOrDefault(a => string.Equals(a.VideoId?.Trim(), vid, StringComparison.Ordinal))
                ?.ThumbnailUrl;

            foreach (var raw in latestByAd.Values)
            {
                totalSpend += raw.Spend;
                totalPurchases += raw.Purchases;
                totalPurchaseValue += raw.PurchaseValue;

                var thumb = ProfitMath.ThumbstopRatePct(raw.Reach, raw.VideoPlay3s)
                            ?? ProfitMath.HookRatePct(raw.Impressions, raw.VideoPlay3s);
                var hold = ProfitMath.HoldRatePct(raw.VideoPlay3s, raw.Video15Sec);
                var comp = ProfitMath.CompletionRatePct(raw.Impressions, raw.VideoP100);

                if (raw.Spend > 0)
                {
                    if (thumb is not null)
                    {
                        wThumb += thumb.Value * raw.Spend;
                        denThumb += raw.Spend;
                    }

                    if (hold is not null)
                    {
                        wHold += hold.Value * raw.Spend;
                        denHold += raw.Spend;
                    }

                    if (comp is not null)
                    {
                        wComp += comp.Value * raw.Spend;
                        denComp += raw.Spend;
                    }
                }

                if (raw.Spend > repSpend)
                {
                    repSpend = raw.Spend;
                    repName = raw.EntityName ?? raw.EntityId;
                }
            }

            if (string.IsNullOrEmpty(thumbUrl))
            {
                thumbUrl = ads.FirstOrDefault(a => string.Equals(a.VideoId?.Trim(), vid, StringComparison.Ordinal))
                    ?.ThumbnailUrl;
            }

            thumbUrl = TruncateThumbnailUrl(thumbUrl);

            decimal? roas = totalSpend > 0 ? totalPurchaseValue / totalSpend : null;
            decimal? hookAvg = denThumb > 0 ? wThumb / denThumb : null;
            decimal? holdAvg = denHold > 0 ? wHold / denHold : null;
            decimal? compAvg = denComp > 0 ? wComp / denComp : null;

            var firstSeen = latestByAd.Values.Min(x => x.DateStart);
            var lastSeen = latestByAd.Values.Max(x => x.DateStop);

            var asset = await _db.VideoAssets.FirstOrDefaultAsync(
                    x => x.UserId == userId && x.MetaAdAccountId == act && x.VideoId == vid,
                    cancellationToken)
                .ConfigureAwait(false);
            if (asset is null)
            {
                _db.VideoAssets.Add(
                    new VideoAsset
                    {
                        UserId = userId,
                        MetaAdAccountId = act,
                        VideoId = vid,
                        ThumbnailUrl = thumbUrl,
                        RepresentativeAdName = repName,
                        FirstSeenDate = firstSeen,
                        LastSeenDate = lastSeen,
                        TotalSpend = totalSpend,
                        TotalPurchases = totalPurchases,
                        TotalPurchaseValue = totalPurchaseValue,
                        TotalRoas = roas,
                        HookRateAvg = hookAvg,
                        HoldRateAvg = holdAvg,
                        CompletionRateAvg = compAvg,
                        AggregatedAt = now,
                    });
            }
            else
            {
                asset.ThumbnailUrl = TruncateThumbnailUrl(thumbUrl ?? asset.ThumbnailUrl);
                asset.RepresentativeAdName = repName ?? asset.RepresentativeAdName;
                asset.FirstSeenDate = asset.FirstSeenDate == default ? firstSeen : asset.FirstSeenDate;
                asset.LastSeenDate = lastSeen > asset.LastSeenDate ? lastSeen : asset.LastSeenDate;
                asset.TotalSpend = totalSpend;
                asset.TotalPurchases = totalPurchases;
                asset.TotalPurchaseValue = totalPurchaseValue;
                asset.TotalRoas = roas;
                asset.HookRateAvg = hookAvg;
                asset.HoldRateAvg = holdAvg;
                asset.CompletionRateAvg = compAvg;
                asset.AggregatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertShellVideoAssetAsync(
        int userId,
        string act,
        string videoId,
        string? thumbnailUrl,
        string? repName,
        DateOnly today,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        thumbnailUrl = TruncateThumbnailUrl(thumbnailUrl);
        var asset = await _db.VideoAssets.FirstOrDefaultAsync(
                x => x.UserId == userId && x.MetaAdAccountId == act && x.VideoId == videoId,
                cancellationToken)
            .ConfigureAwait(false);
        if (asset is null)
        {
            _db.VideoAssets.Add(
                new VideoAsset
                {
                    UserId = userId,
                    MetaAdAccountId = act,
                    VideoId = videoId,
                    ThumbnailUrl = thumbnailUrl,
                    RepresentativeAdName = repName,
                    FirstSeenDate = today,
                    LastSeenDate = today,
                    AggregatedAt = now,
                });
        }
        else
        {
            asset.ThumbnailUrl = TruncateThumbnailUrl(thumbnailUrl ?? asset.ThumbnailUrl);
            asset.RepresentativeAdName = repName ?? asset.RepresentativeAdName;
            asset.LastSeenDate = today;
            asset.AggregatedAt = now;
        }
    }

    private static string? TruncateThumbnailUrl(string? thumbnailUrl) =>
        thumbnailUrl?.Length > 2000 ? thumbnailUrl.Substring(0, 2000) : thumbnailUrl;
}
