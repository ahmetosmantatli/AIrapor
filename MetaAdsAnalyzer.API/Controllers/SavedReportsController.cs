using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/saved-reports")]
public class SavedReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SavedReportsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("by-user/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<SavedReportListItemDto>>> ListByUser(int userId, CancellationToken ct)
    {
        if (userId <= 0) return BadRequest();
        var auth = this.EnsureOwnUser(userId);
        if (auth is not null) return auth;

        var latestIds = _db.SavedReports.AsNoTracking()
            .Where(x => x.UserId == userId)
            .GroupBy(x => x.AdId)
            .Select(g => g.OrderByDescending(x => x.AnalyzedAt).ThenByDescending(x => x.Id).Select(x => x.Id).First());

        var list = await _db.SavedReports.AsNoTracking()
            .Where(x => latestIds.Contains(x.Id))
            .OrderByDescending(x => x.AnalyzedAt)
            .Select(x => new SavedReportListItemDto
            {
                Id = x.Id,
                AdId = x.AdId,
                AdName = x.AdName,
                ThumbnailUrl = x.ThumbnailUrl,
                CampaignId = x.CampaignId,
                CampaignName = x.CampaignName,
                AdsetId = x.AdsetId,
                AdsetName = x.AdsetName,
                AnalyzedAt = x.AnalyzedAt,
                AggregateRoas = x.AggregateRoas,
                AggregateHookRate = x.AggregateHookRate,
                AggregateHoldRate = x.AggregateHoldRate,
                AggregateSpend = x.AggregateSpend,
                AggregatePurchases = x.AggregatePurchases,
                Suggestions = x.Suggestions.Select(s => new SavedReportSuggestionDto
                {
                    Id = s.Id,
                    SuggestionKey = s.SuggestionKey,
                    DirectiveType = s.DirectiveType,
                    Severity = s.Severity,
                    Message = s.Message,
                    Symptom = s.Symptom,
                    Reason = s.Reason,
                    Action = s.Action,
                    AppliedAt = s.AppliedAt,
                    SkippedAt = s.SkippedAt,
                    BeforeRoas = s.BeforeRoas,
                    BeforeHookRate = s.BeforeHookRate,
                    BeforeHoldRate = s.BeforeHoldRate,
                    BeforeSpend = s.BeforeSpend,
                    BeforePurchases = s.BeforePurchases,
                    AfterRoas = s.AfterRoas,
                    AfterHookRate = s.AfterHookRate,
                    AfterHoldRate = s.AfterHoldRate,
                    AfterSpend = s.AfterSpend,
                    AfterPurchases = s.AfterPurchases,
                    ImpactMeasuredAt = s.ImpactMeasuredAt,
                    MetaChangeDetected = s.MetaChangeDetected,
                    MetaChangeMessage = s.MetaChangeMessage,
                }).ToList(),
            })
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<SavedReportListItemDto>> Create([FromBody] SavedReportCreateRequestDto body, CancellationToken ct)
    {
        if (body.UserId <= 0 || string.IsNullOrWhiteSpace(body.AdId)) return BadRequest();
        var auth = this.EnsureOwnUser(body.UserId);
        if (auth is not null) return auth;

        var adId = body.AdId.Trim();
        var existingForAd = await _db.SavedReports
            .Include(x => x.Suggestions)
            .Where(x => x.UserId == body.UserId && x.AdId == adId)
            .OrderByDescending(x => x.AnalyzedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        var reusable = existingForAd.FirstOrDefault(x => x.Suggestions.All(s => s.AppliedAt == null));
        var report = reusable ?? new Core.Entities.SavedReport
        {
            UserId = body.UserId,
            AdId = adId,
        };

        report.AdName = body.AdName?.Trim();
        report.ThumbnailUrl = body.ThumbnailUrl?.Trim();
        report.CampaignId = body.CampaignId?.Trim();
        report.CampaignName = body.CampaignName?.Trim();
        report.AdsetId = body.AdsetId?.Trim();
        report.AdsetName = body.AdsetName?.Trim();
        report.AggregateRoas = body.AggregateRoas;
        report.AggregateHookRate = body.AggregateHookRate;
        report.AggregateHoldRate = body.AggregateHoldRate;
        report.AggregateSpend = body.AggregateSpend;
        report.AggregatePurchases = body.AggregatePurchases;
        report.AnalyzedAt = DateTimeOffset.UtcNow;

        if (reusable is null)
        {
            _db.SavedReports.Add(report);
        }
        else
        {
            if (reusable.Suggestions.Count > 0)
            {
                _db.SavedReportSuggestions.RemoveRange(reusable.Suggestions);
            }
            reusable.Suggestions = new List<Core.Entities.SavedReportSuggestion>();
        }

        report.Suggestions = body.Suggestions
            .Where(s => !string.IsNullOrWhiteSpace(s.SuggestionKey))
            .Select(s => new Core.Entities.SavedReportSuggestion
            {
                SuggestionKey = s.SuggestionKey.Trim(),
                DirectiveType = s.DirectiveType?.Trim(),
                Severity = string.IsNullOrWhiteSpace(s.Severity) ? "info" : s.Severity.Trim().ToLowerInvariant(),
                Message = s.Message?.Trim() ?? string.Empty,
                Symptom = s.Symptom?.Trim(),
                Reason = s.Reason?.Trim(),
                Action = s.Action?.Trim(),
            }).ToList();

        // Keep immutable snapshots only for applied-tracking history, drop stale non-applied duplicates.
        var removable = existingForAd
            .Where(x => x.Id != report.Id && x.Suggestions.All(s => s.AppliedAt == null))
            .ToList();
        if (removable.Count > 0)
        {
            _db.SavedReports.RemoveRange(removable);
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new SavedReportListItemDto
        {
            Id = report.Id,
            AdId = report.AdId,
            AdName = report.AdName,
            ThumbnailUrl = report.ThumbnailUrl,
            CampaignId = report.CampaignId,
            CampaignName = report.CampaignName,
            AdsetId = report.AdsetId,
            AdsetName = report.AdsetName,
            AnalyzedAt = report.AnalyzedAt,
            AggregateRoas = report.AggregateRoas,
            AggregateHookRate = report.AggregateHookRate,
            AggregateHoldRate = report.AggregateHoldRate,
            AggregateSpend = report.AggregateSpend,
            AggregatePurchases = report.AggregatePurchases,
            Suggestions = report.Suggestions.Select(s => new SavedReportSuggestionDto
            {
                Id = s.Id,
                SuggestionKey = s.SuggestionKey,
                DirectiveType = s.DirectiveType,
                Severity = s.Severity,
                Message = s.Message,
                Symptom = s.Symptom,
                Reason = s.Reason,
                Action = s.Action,
                MetaChangeDetected = s.MetaChangeDetected,
                MetaChangeMessage = s.MetaChangeMessage,
            }).ToList(),
        });
    }

    [HttpPost("suggestions/{suggestionId:int}/status")]
    public async Task<ActionResult<SavedReportSuggestionDto>> UpdateSuggestionStatus(
        int suggestionId,
        [FromBody] SavedReportSuggestionUpdateRequestDto body,
        CancellationToken ct)
    {
        if (suggestionId <= 0 || string.IsNullOrWhiteSpace(body.Status)) return BadRequest();

        var row = await _db.SavedReportSuggestions
            .Include(x => x.SavedReport)
            .FirstOrDefaultAsync(x => x.Id == suggestionId, ct);
        if (row is null) return NotFound();
        var auth = this.EnsureOwnUser(row.SavedReport.UserId);
        if (auth is not null) return auth;

        var status = body.Status.Trim().ToLowerInvariant();
        if (status is not ("applied" or "skipped")) return BadRequest(new { message = "status applied/skipped olmalıdır." });

        if (status == "applied")
        {
            if (row.AppliedAt is null)
            {
                row.AppliedAt = DateTimeOffset.UtcNow;
                row.SkippedAt = null;
                await FillBeforeMetricsAsync(row, ct);
            }
        }
        else
        {
            if (row.SkippedAt is null)
            {
                row.SkippedAt = DateTimeOffset.UtcNow;
                row.AppliedAt = null;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new SavedReportSuggestionDto
        {
            Id = row.Id,
            SuggestionKey = row.SuggestionKey,
            DirectiveType = row.DirectiveType,
            Severity = row.Severity,
            Message = row.Message,
            Symptom = row.Symptom,
            Reason = row.Reason,
            Action = row.Action,
            AppliedAt = row.AppliedAt,
            SkippedAt = row.SkippedAt,
            BeforeRoas = row.BeforeRoas,
            BeforeHookRate = row.BeforeHookRate,
            BeforeHoldRate = row.BeforeHoldRate,
            BeforeSpend = row.BeforeSpend,
            BeforePurchases = row.BeforePurchases,
            AfterRoas = row.AfterRoas,
            AfterHookRate = row.AfterHookRate,
            AfterHoldRate = row.AfterHoldRate,
            AfterSpend = row.AfterSpend,
            AfterPurchases = row.AfterPurchases,
            ImpactMeasuredAt = row.ImpactMeasuredAt,
            MetaChangeDetected = row.MetaChangeDetected,
            MetaChangeMessage = row.MetaChangeMessage,
        });
    }

    [HttpGet("impacts/by-user/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<SavedReportImpactFeedItemDto>>> ImpactFeed(
        int userId,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        if (userId <= 0) return BadRequest();
        var auth = this.EnsureOwnUser(userId);
        if (auth is not null) return auth;
        var limit = Math.Clamp(take, 1, 50);

        var list = await _db.SavedReportSuggestions.AsNoTracking()
            .Where(s => s.SavedReport.UserId == userId && s.AppliedAt != null)
            .OrderByDescending(s => s.ImpactMeasuredAt ?? s.AppliedAt)
            .Take(limit)
            .Select(s => new SavedReportImpactFeedItemDto
            {
                SuggestionId = s.Id,
                SavedReportId = s.SavedReportId,
                AdId = s.SavedReport.AdId,
                AdName = s.SavedReport.AdName,
                CampaignId = s.SavedReport.CampaignId,
                CampaignName = s.SavedReport.CampaignName,
                AdsetId = s.SavedReport.AdsetId,
                AdsetName = s.SavedReport.AdsetName,
                AppliedAt = s.AppliedAt!.Value,
                ImpactMeasuredAt = s.ImpactMeasuredAt,
                BeforeRoas = s.BeforeRoas,
                AfterRoas = s.AfterRoas,
                BeforeSpend = s.BeforeSpend,
                AfterSpend = s.AfterSpend,
                BeforePurchases = s.BeforePurchases,
                AfterPurchases = s.AfterPurchases,
                BeforeHookRate = s.BeforeHookRate,
                AfterHookRate = s.AfterHookRate,
                BeforeHoldRate = s.BeforeHoldRate,
                AfterHoldRate = s.AfterHoldRate,
                DirectiveType = s.DirectiveType,
                Severity = s.Severity,
                Message = s.Message,
                Symptom = s.Symptom,
                Reason = s.Reason,
                Action = s.Action,
                MetaChangeDetected = s.MetaChangeDetected,
                MetaChangeMessage = s.MetaChangeMessage,
            })
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpGet("impacts/suggestions/{suggestionId:int}")]
    public async Task<ActionResult<SavedReportImpactDetailDto>> ImpactDetailBySuggestion(
        int suggestionId,
        CancellationToken ct = default)
    {
        if (suggestionId <= 0) return BadRequest();
        var hit = await _db.SavedReportSuggestions.AsNoTracking()
            .Where(s => s.Id == suggestionId && s.AppliedAt != null)
            .Select(s => new
            {
                s.SavedReport.UserId,
                Detail = new SavedReportImpactDetailDto
                {
                    SuggestionId = s.Id,
                    SavedReportId = s.SavedReportId,
                    AdId = s.SavedReport.AdId,
                    AdName = s.SavedReport.AdName,
                    CampaignId = s.SavedReport.CampaignId,
                    CampaignName = s.SavedReport.CampaignName,
                    AdsetId = s.SavedReport.AdsetId,
                    AdsetName = s.SavedReport.AdsetName,
                    AppliedAt = s.AppliedAt!.Value,
                    AnalyzedAt = s.SavedReport.AnalyzedAt,
                    ImpactMeasuredAt = s.ImpactMeasuredAt,
                    BeforeRoas = s.BeforeRoas,
                    AfterRoas = s.AfterRoas,
                    BeforeSpend = s.BeforeSpend,
                    AfterSpend = s.AfterSpend,
                    BeforePurchases = s.BeforePurchases,
                    AfterPurchases = s.AfterPurchases,
                    BeforeHookRate = s.BeforeHookRate,
                    AfterHookRate = s.AfterHookRate,
                    BeforeHoldRate = s.BeforeHoldRate,
                    AfterHoldRate = s.AfterHoldRate,
                    DirectiveType = s.DirectiveType,
                    Severity = s.Severity,
                    Message = s.Message,
                    Symptom = s.Symptom,
                    Reason = s.Reason,
                    Action = s.Action,
                    MetaChangeDetected = s.MetaChangeDetected,
                    MetaChangeMessage = s.MetaChangeMessage,
                },
            })
            .FirstOrDefaultAsync(ct);
        if (hit is null) return NotFound();
        var auth = this.EnsureOwnUser(hit.UserId);
        if (auth is not null) return auth;
        return Ok(hit.Detail);
    }

    private async Task FillBeforeMetricsAsync(Core.Entities.SavedReportSuggestion suggestion, CancellationToken ct)
    {
        var adId = suggestion.SavedReport.AdId;
        var userId = suggestion.SavedReport.UserId;
        var raw = await _db.RawInsights.AsNoTracking()
            .Where(r => r.UserId == userId && r.Level == "ad" && r.EntityId == adId)
            .OrderByDescending(r => r.FetchedAt)
            .FirstOrDefaultAsync(ct);
        if (raw is null) return;

        var comp = await _db.ComputedMetrics.AsNoTracking()
            .Where(c => c.RawInsightId == raw.Id)
            .OrderByDescending(c => c.ComputedAt)
            .FirstOrDefaultAsync(ct);

        suggestion.BeforeSpend = raw.Spend;
        suggestion.BeforePurchases = (int)Math.Clamp(raw.Purchases, 0, int.MaxValue);
        suggestion.BeforeRoas = comp?.Roas ?? (raw.Spend > 0 ? raw.PurchaseValue / raw.Spend : null);
        suggestion.BeforeHookRate = comp?.ThumbstopRatePct ?? (raw.Impressions > 0 ? raw.VideoPlay3s * 100m / raw.Impressions : null);
        suggestion.BeforeHoldRate = comp?.HoldRate ?? (raw.VideoPlay3s > 0 ? raw.VideoThruplay * 100m / raw.VideoPlay3s : null);
    }
}

