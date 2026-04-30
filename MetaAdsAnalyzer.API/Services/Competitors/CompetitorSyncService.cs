using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Services.Competitors;

public sealed class CompetitorSyncService : ICompetitorSyncService
{
    private readonly AppDbContext _db;
    private readonly ICompetitorAdLibraryClient _adLibraryClient;
    private readonly ILogger<CompetitorSyncService> _logger;

    public CompetitorSyncService(
        AppDbContext db,
        ICompetitorAdLibraryClient adLibraryClient,
        ILogger<CompetitorSyncService> logger)
    {
        _db = db;
        _adLibraryClient = adLibraryClient;
        _logger = logger;
    }

    public async Task<SyncCompetitorResultDto> SyncCompetitorAsync(int trackedCompetitorId, CancellationToken cancellationToken)
    {
        var target = await _db.TrackedCompetitors
            .Include(x => x.Ads)
            .FirstOrDefaultAsync(x => x.Id == trackedCompetitorId, cancellationToken)
            .ConfigureAwait(false);
        if (target is null)
        {
            throw new InvalidOperationException("Rakip marka bulunamadı.");
        }

        if (!target.IsActive)
        {
            return new SyncCompetitorResultDto();
        }

        var now = DateTimeOffset.UtcNow;
        var scrapeLog = new CompetitorScrapeLog
        {
            UserId = target.UserId,
            TrackedCompetitorId = target.Id,
            StartedAt = now,
            Status = "running",
        };
        _db.CompetitorScrapeLogs.Add(scrapeLog);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<AdLibraryAdItem> fetchedAds = Array.Empty<AdLibraryAdItem>();

        try
        {
            fetchedAds = await _adLibraryClient.FetchAdsAsync(target.PageRef, target.PageId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            target.LastSyncStatus = "exception";
            target.LastSyncError = ex.Message;
            target.UpdatedAt = now;
            scrapeLog.Status = "failed";
            scrapeLog.Error = ex.Message;
            scrapeLog.FinishedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Competitor sync exception competitorId={CompetitorId}", target.Id);
            return new SyncCompetitorResultDto();
        }

        var inserted = 0;
        var updated = 0;
        var fetchedIds = fetchedAds.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var row in fetchedAds)
        {
            var hit = target.Ads.FirstOrDefault(x => x.MetaAdArchiveId == row.Id);
            if (hit is null)
            {
                target.Ads.Add(
                    new CompetitorAd
                    {
                        MetaAdArchiveId = row.Id,
                        PageId = row.PageId,
                        PageName = row.PageName,
                        Format = row.Format,
                        BodyText = row.BodyText,
                        TitleText = row.TitleText,
                        DescriptionText = row.DescriptionText,
                        SnapshotUrl = row.SnapshotUrl,
                        PublisherPlatforms = row.PublisherPlatforms,
                        Languages = row.Languages,
                        DeliveryStartTime = row.DeliveryStartTime,
                        DeliveryStopTime = row.DeliveryStopTime,
                        FirstSeenAt = now,
                        LastSeenAt = now,
                        IsActive = true,
                    });
                inserted++;
                continue;
            }

            hit.PageId = row.PageId;
            hit.PageName = row.PageName;
            hit.Format = row.Format;
            hit.BodyText = row.BodyText;
            hit.TitleText = row.TitleText;
            hit.DescriptionText = row.DescriptionText;
            hit.SnapshotUrl = row.SnapshotUrl;
            hit.PublisherPlatforms = row.PublisherPlatforms;
            hit.Languages = row.Languages;
            hit.DeliveryStartTime = row.DeliveryStartTime;
            hit.DeliveryStopTime = row.DeliveryStopTime;
            hit.LastSeenAt = now;
            hit.IsActive = true;
            updated++;
        }

        var closed = 0;
        foreach (var existing in target.Ads.Where(x => x.IsActive && !fetchedIds.Contains(x.MetaAdArchiveId)))
        {
            existing.IsActive = false;
            existing.DeliveryStopTime ??= now;
            closed++;
        }

        target.LastSyncedAt = now;
        target.LastSyncStatus = "ok";
        target.LastSyncError = null;
        target.UpdatedAt = now;
        scrapeLog.Status = "ok";
        scrapeLog.FetchedCount = fetchedAds.Count;
        scrapeLog.InsertedCount = inserted;
        scrapeLog.UpdatedCount = updated;
        scrapeLog.ClosedCount = closed;
        scrapeLog.FinishedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SyncCompetitorResultDto
        {
            FetchedCount = fetchedAds.Count,
            InsertedCount = inserted,
            UpdatedCount = updated,
            ClosedCount = closed,
        };
    }
}
