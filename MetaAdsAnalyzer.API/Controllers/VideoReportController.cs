using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using MetaAdsAnalyzer.Core;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

/// <summary>Video özet aggregate. İki kök: <c>/api/video-report</c> (kanonik) ve <c>/api/video-reports</c> (yaygın yazım hatası).</summary>
[ApiController]
[Authorize]
[Route("api/video-report")]
[Route("api/video-reports")]
public class VideoReportController : ControllerBase
{
    private readonly IVideoReportInsightService _insights;
    private readonly IMetaInsightsSyncService _metaInsights;
    private readonly AppDbContext _db;
    private readonly ILogger<VideoReportController> _logger;

    public VideoReportController(
        IVideoReportInsightService insights,
        IMetaInsightsSyncService metaInsights,
        AppDbContext db,
        ILogger<VideoReportController> logger)
    {
        _insights = insights;
        _metaInsights = metaInsights;
        _db = db;
        _logger = logger;
    }

    /// <summary>JWT olmadan route’un Kestrel’de kayıtlı olduğunu doğrulamak için (curl / tarayıcı).</summary>
    [HttpGet("route-ping")]
    [AllowAnonymous]
    public IActionResult RoutePing()
    {
        return Ok(
            new
            {
                ok = true,
                controller = nameof(VideoReportController),
                postAggregate = "/api/video-report/aggregate",
                postAggregateAlias = "/api/video-reports/aggregate",
                note = "aggregate için POST + JSON body + Bearer gerekir.",
            });
    }

    [HttpPost("aggregate")]
    public async Task<ActionResult<VideoReportAggregateResponseDto>> Aggregate(
        [FromBody] VideoReportAggregateRequestDto body,
        CancellationToken cancellationToken)
    {
        var adIds = (body.AdIds ?? new List<string>())
            .Select(static x => x.Trim())
            .Where(static x => x.Length > 0)
            .ToList();
        if (body.UserId <= 0 || adIds.Count == 0)
        {
            return BadRequest(new { message = "userId ve adIds gerekli." });
        }

        var auth = this.EnsureOwnUser(body.UserId);
        if (auth is not null)
        {
            return auth;
        }

        _logger.LogInformation(
            "POST aggregate hit UserId={UserId} MetaAdAccountId={Act} AdIds={AdIds}",
            body.UserId,
            body.MetaAdAccountId ?? "(null)",
            string.Join(",", adIds.Take(20)) + (adIds.Count > 20 ? "…" : string.Empty));

        // Keep video timelines fresh: when ad-level raw insights are older than 4h,
        // force a targeted sync for only the analyzed adIds.
        var normalizedAct = MetaAdAccountIdNormalizer.Normalize(body.MetaAdAccountId);
        var freshSince = DateTimeOffset.UtcNow.AddHours(-4);
        var staleOrMissing = await GetStaleOrMissingAdIdsAsync(body.UserId, normalizedAct, adIds, freshSince, cancellationToken)
            .ConfigureAwait(false);
        if (staleOrMissing.Count > 0)
        {
            _logger.LogInformation(
                "VideoReport aggregate pre-sync started UserId={UserId} Act={Act} AdIds={AdCount} StaleOrMissing={StaleCount}",
                body.UserId,
                normalizedAct ?? "(default)",
                adIds.Count,
                staleOrMissing.Count);

            await _metaInsights.SyncInsightsAsync(
                    body.UserId,
                    "ad",
                    "last_30d",
                    null,
                    normalizedAct,
                    staleOrMissing,
                    true,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var dto = await _insights.BuildAggregateAsync(body.UserId, body.MetaAdAccountId, adIds, cancellationToken)
            .ConfigureAwait(false);
        return Ok(dto);
    }

    private async Task<List<string>> GetStaleOrMissingAdIdsAsync(
        int userId,
        string? metaAdAccountId,
        IReadOnlyList<string> adIds,
        DateTimeOffset freshSince,
        CancellationToken cancellationToken)
    {
        var query = _db.RawInsights
            .AsNoTracking()
            .Where(r =>
                r.UserId == userId
                && r.Level == "ad"
                && adIds.Contains(r.EntityId));

        if (!string.IsNullOrWhiteSpace(metaAdAccountId))
        {
            query = query.Where(r => r.MetaAdAccountId == metaAdAccountId);
        }

        var candidates = await query
            .OrderByDescending(r => r.FetchedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var latestByAd = new Dictionary<string, Core.Entities.RawInsight>(StringComparer.Ordinal);
        foreach (var row in candidates)
        {
            if (!latestByAd.ContainsKey(row.EntityId))
            {
                latestByAd[row.EntityId] = row;
            }
        }

        var staleOrMissing = new List<string>(adIds.Count);
        foreach (var adId in adIds)
        {
            if (!latestByAd.TryGetValue(adId, out var latest))
            {
                staleOrMissing.Add(adId);
                continue;
            }

            var stale = latest.FetchedAt < freshSince;
            var incompleteTimeline =
                latest.VideoPlay3s > 0
                && latest.VideoThruplay == 0
                && latest.VideoP25 == 0
                && latest.VideoP50 == 0
                && latest.VideoP75 == 0
                && latest.VideoP100 == 0;

            if (stale || incompleteTimeline)
            {
                staleOrMissing.Add(adId);
            }
        }

        return staleOrMissing;
    }
}
