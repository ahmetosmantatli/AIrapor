using System.Text.Json;
using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services.Competitors;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/competitors")]
public class CompetitorsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICompetitorSyncService _syncService;
    private readonly ICompetitorSyncDispatcher _syncDispatcher;

    public CompetitorsController(
        AppDbContext db,
        ICompetitorSyncService syncService,
        ICompetitorSyncDispatcher syncDispatcher)
    {
        _db = db;
        _syncService = syncService;
        _syncDispatcher = syncDispatcher;
    }

    [HttpGet("by-user/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<CompetitorListItemDto>>> ListByUser(int userId, CancellationToken cancellationToken)
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

        var since = DateTimeOffset.UtcNow.AddDays(-7);
        var list = await _db.TrackedCompetitors.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.DisplayName)
            .Select(
                x => new CompetitorListItemDto
                {
                    Id = x.Id,
                    DisplayName = x.DisplayName,
                    PageRef = x.PageRef,
                    PageId = x.PageId,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    LastSyncedAt = x.LastSyncedAt,
                    LastSyncStatus = x.LastSyncStatus,
                    LastSyncError = x.LastSyncError,
                    NewAdsLast7Days = x.Ads.Count(a => a.FirstSeenAt >= since),
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<CompetitorListItemDto>> Create(
        [FromBody] CreateCompetitorRequestDto body,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.GetUserId();
        if (userId is null || userId <= 0)
        {
            return Unauthorized();
        }

        var uid = userId.Value;
        var count = await _db.TrackedCompetitors.CountAsync(x => x.UserId == uid && x.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (count >= 10)
        {
            return BadRequest(new { message = "Maksimum 10 aktif rakip marka takip edebilirsiniz." });
        }

        var displayName = body.DisplayName.Trim();
        var pageRef = body.PageRef.Trim();
        var pageId = NormalizeOptionalPageId(body.PageId);
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(pageRef))
        {
            return BadRequest(new { message = "Marka adı ve sayfa referansı zorunludur." });
        }

        var dup = await _db.TrackedCompetitors.AsNoTracking()
            .AnyAsync(
                x => x.UserId == uid
                     && x.IsActive
                     && ((pageId != null && x.PageId == pageId) || x.PageRef == pageRef),
                cancellationToken)
            .ConfigureAwait(false);
        if (dup)
        {
            return BadRequest(new { message = "Bu rakip marka zaten takip ediliyor." });
        }

        var now = DateTimeOffset.UtcNow;
        var row = new TrackedCompetitor
        {
            UserId = uid,
            DisplayName = displayName,
            PageRef = pageRef,
            PageId = pageId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            LastSyncStatus = "pending",
        };

        _db.TrackedCompetitors.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _syncDispatcher.TriggerInitialSync(row.Id);

        return Ok(
            new CompetitorListItemDto
            {
                Id = row.Id,
                DisplayName = row.DisplayName,
                PageRef = row.PageRef,
                PageId = row.PageId,
                IsActive = row.IsActive,
                CreatedAt = row.CreatedAt,
                LastSyncedAt = row.LastSyncedAt,
                LastSyncStatus = row.LastSyncStatus,
                LastSyncError = row.LastSyncError,
                NewAdsLast7Days = 0,
            });
    }

    [HttpPatch("{competitorId:int}/deactivate")]
    public async Task<ActionResult> Deactivate(int competitorId, CancellationToken cancellationToken)
    {
        if (competitorId <= 0)
        {
            return BadRequest();
        }

        var row = await _db.TrackedCompetitors.FirstOrDefaultAsync(x => x.Id == competitorId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return NotFound();
        }

        var auth = this.EnsureOwnUser(row.UserId);
        if (auth is not null)
        {
            return auth;
        }

        if (row.IsActive)
        {
            row.IsActive = false;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            row.LastSyncStatus = "paused";
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return NoContent();
    }

    [HttpGet("{competitorId:int}/ads")]
    public async Task<ActionResult<IReadOnlyList<CompetitorAdItemDto>>> ListAds(
        int competitorId,
        [FromQuery] string? format,
        [FromQuery] string? range = "all",
        [FromQuery] string? status = "all",
        [FromQuery] int take = 100,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        if (competitorId <= 0)
        {
            return BadRequest();
        }

        var competitor = await _db.TrackedCompetitors.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitorId, cancellationToken)
            .ConfigureAwait(false);
        if (competitor is null)
        {
            return NotFound();
        }

        var auth = this.EnsureOwnUser(competitor.UserId);
        if (auth is not null)
        {
            return auth;
        }

        var q = _db.CompetitorAds.AsNoTracking().Where(x => x.TrackedCompetitorId == competitorId);
        if (!string.IsNullOrWhiteSpace(format))
        {
            var formatNorm = format.Trim().ToLowerInvariant();
            q = q.Where(x => x.Format == formatNorm);
        }

        var statusNorm = (status ?? "all").Trim().ToLowerInvariant();
        if (statusNorm == "active")
        {
            q = q.Where(x => x.IsActive);
        }
        else if (statusNorm == "inactive")
        {
            q = q.Where(x => !x.IsActive);
        }

        var since = ParseRangeSince(range);
        if (since is not null)
        {
            q = q.Where(x => x.LastSeenAt >= since.Value || x.FirstSeenAt >= since.Value);
        }

        var list = await q.OrderByDescending(x => x.LastSeenAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .Select(
                x => new CompetitorAdItemDto
                {
                    Id = x.Id,
                    MetaAdArchiveId = x.MetaAdArchiveId,
                    Format = x.Format,
                    BodyText = x.BodyText,
                    TitleText = x.TitleText,
                    DescriptionText = x.DescriptionText,
                    SnapshotUrl = x.SnapshotUrl,
                    PublisherPlatforms = ParseArrayJson(x.PublisherPlatforms),
                    Languages = ParseArrayJson(x.Languages),
                    DeliveryStartTime = x.DeliveryStartTime,
                    DeliveryStopTime = x.DeliveryStopTime,
                    FirstSeenAt = x.FirstSeenAt,
                    LastSeenAt = x.LastSeenAt,
                    IsActive = x.IsActive,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(list);
    }

    [HttpPost("{competitorId:int}/sync")]
    public async Task<ActionResult<SyncCompetitorResultDto>> SyncNow(
        int competitorId,
        CancellationToken cancellationToken)
    {
        if (competitorId <= 0)
        {
            return BadRequest();
        }

        var competitor = await _db.TrackedCompetitors.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitorId, cancellationToken)
            .ConfigureAwait(false);
        if (competitor is null)
        {
            return NotFound();
        }

        var auth = this.EnsureOwnUser(competitor.UserId);
        if (auth is not null)
        {
            return auth;
        }

        var result = await _syncService.SyncCompetitorAsync(competitorId, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    private static string? NormalizeOptionalPageId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var norm = raw.Trim();
        return norm.Length > 64 ? norm[..64] : norm;
    }

    private static DateTimeOffset? ParseRangeSince(string? range)
    {
        var norm = (range ?? "all").Trim().ToLowerInvariant();
        return norm switch
        {
            "7d" => DateTimeOffset.UtcNow.AddDays(-7),
            "30d" => DateTimeOffset.UtcNow.AddDays(-30),
            _ => null,
        };
    }

    private static IReadOnlyList<string> ParseArrayJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var hit = JsonSerializer.Deserialize<string[]>(json);
            return hit?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
