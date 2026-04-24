using MetaAdsAnalyzer.API.Extensions;
using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/raw-insights")]
public class RawInsightsController : ControllerBase
{
    private const int MaxRows = 400;

    private readonly AppDbContext _db;

    public RawInsightsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("by-user/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<RawInsightListRowDto>>> ListByUser(
        int userId,
        [FromQuery] string? level,
        [FromQuery] string? campaignId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return BadRequest();
        }

        var auth = this.EnsureOwnUser(userId);
        if (auth is not null)
        {
            return auth;
        }

        var levelNorm = string.IsNullOrWhiteSpace(level) ? null : level.Trim().ToLowerInvariant();
        if (levelNorm is not (null or "campaign" or "adset" or "ad"))
        {
            return BadRequest(new { message = "level: campaign, adset veya ad olmalıdır." });
        }

        var activeMeta = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.MetaAdAccountId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var q = _db.RawInsights.AsNoTracking().ForUserActiveAdAccount(userId, activeMeta);
        if (levelNorm is not null)
        {
            q = q.Where(r => r.Level == levelNorm);
        }

        if (!string.IsNullOrWhiteSpace(campaignId))
        {
            var campaignIdNorm = campaignId.Trim();
            q = q.Where(r => r.MetaCampaignId == campaignIdNorm);
        }

        var raws = await q
            .OrderByDescending(r => r.FetchedAt)
            .Take(MaxRows)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (raws.Count == 0)
        {
            return Ok(Array.Empty<RawInsightListRowDto>());
        }

        var rawIds = raws.Select(r => r.Id).ToList();
        var comps = await _db.ComputedMetrics.AsNoTracking()
            .Where(c => rawIds.Contains(c.RawInsightId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var latestByRaw = comps
            .GroupBy(c => c.RawInsightId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ComputedAt).First());

        var list = raws.Select(
            r =>
            {
                latestByRaw.TryGetValue(r.Id, out var c);
                return new RawInsightListRowDto
                {
                    Id = r.Id,
                    Level = r.Level,
                    EntityId = r.EntityId,
                    EntityName = r.EntityName,
                    MetaCampaignId = r.MetaCampaignId,
                    MetaAdsetId = r.MetaAdsetId,
                    DateStart = r.DateStart,
                    DateStop = r.DateStop,
                    FetchedAt = r.FetchedAt,
                    Spend = r.Spend,
                    Impressions = r.Impressions,
                    Reach = r.Reach,
                    LinkClicks = r.LinkClicks,
                    VideoPlay3s = r.VideoPlay3s,
                    Video15Sec = r.Video15Sec,
                    VideoP100 = r.VideoP100,
                    Purchases = r.Purchases,
                    PurchaseValue = r.PurchaseValue,
                    Roas = c?.Roas,
                    Cpa = c?.Cpa,
                    ThumbstopRatePct = c?.ThumbstopRatePct,
                    HoldRatePct = c?.HoldRate,
                    CompletionRatePct = c?.CompletionRatePct,
                    CreativeScoreTotal = c?.CreativeScoreTotal,
                    ComputedMetricId = c?.Id,
                };
            }).ToList();

        return Ok(list);
    }
}
