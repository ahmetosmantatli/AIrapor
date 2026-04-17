using MetaAdsAnalyzer.API.Extensions;
using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/watchlist")]
public class WatchlistController : ControllerBase
{
    private readonly AppDbContext _db;

    public WatchlistController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WatchlistItemResponseDto>>> List(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var ent = await _db.GetPlanEntitlementsForUserAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        if (ent is null)
        {
            return Unauthorized();
        }

        if (!ent.AllowsWatchlist)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Takip listesi Pro planda. Ayarlar üzerinden Pro’ya geçebilirsiniz.",
                    requiredPlanCode = "pro",
                });
        }

        var list = await _db.WatchlistItems.AsNoTracking()
            .Where(w => w.UserId == userId.Value)
            .OrderByDescending(w => w.CreatedAt)
            .Select(
                w => new WatchlistItemResponseDto
                {
                    Id = w.Id,
                    Level = w.Level,
                    EntityId = w.EntityId,
                    CreatedAt = w.CreatedAt,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<WatchlistItemResponseDto>> Add(
        [FromBody] AddWatchlistItemRequestDto body,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var ent = await _db.GetPlanEntitlementsForUserAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        if (ent is null)
        {
            return Unauthorized();
        }

        if (!ent.AllowsWatchlist)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Takip listesi Pro planda. Ayarlar üzerinden Pro’ya geçebilirsiniz.",
                    requiredPlanCode = "pro",
                });
        }

        var level = body.Level.Trim().ToLowerInvariant();
        var entityId = body.EntityId.Trim();
        if (entityId.Length == 0)
        {
            return BadRequest(new { message = "EntityId boş olamaz." });
        }

        var existing = await _db.WatchlistItems
            .FirstOrDefaultAsync(
                w => w.UserId == userId.Value && w.Level == level && w.EntityId == entityId,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Ok(
                new WatchlistItemResponseDto
                {
                    Id = existing.Id,
                    Level = existing.Level,
                    EntityId = existing.EntityId,
                    CreatedAt = existing.CreatedAt,
                });
        }

        var item = new WatchlistItem
        {
            UserId = userId.Value,
            Level = level,
            EntityId = entityId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.WatchlistItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(
            nameof(List),
            null,
            new WatchlistItemResponseDto
            {
                Id = item.Id,
                Level = item.Level,
                EntityId = item.EntityId,
                CreatedAt = item.CreatedAt,
            });
    }

    [HttpPost("toggle")]
    public async Task<ActionResult<WatchlistToggleResponseDto>> Toggle(
        [FromBody] AddWatchlistItemRequestDto body,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var ent = await _db.GetPlanEntitlementsForUserAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        if (ent is null)
        {
            return Unauthorized();
        }

        if (!ent.AllowsWatchlist)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Takip listesi Pro planda. Ayarlar üzerinden Pro’ya geçebilirsiniz.",
                    requiredPlanCode = "pro",
                });
        }

        var level = body.Level.Trim().ToLowerInvariant();
        var entityId = body.EntityId.Trim();
        if (entityId.Length == 0)
        {
            return BadRequest(new { message = "EntityId boş olamaz." });
        }

        var existing = await _db.WatchlistItems
            .FirstOrDefaultAsync(
                w => w.UserId == userId.Value && w.Level == level && w.EntityId == entityId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _db.WatchlistItems.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Ok(new WatchlistToggleResponseDto { IsWatching = false, WatchlistItemId = null });
        }

        var item = new WatchlistItem
        {
            UserId = userId.Value,
            Level = level,
            EntityId = entityId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.WatchlistItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new WatchlistToggleResponseDto { IsWatching = true, WatchlistItemId = item.Id });
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var ent = await _db.GetPlanEntitlementsForUserAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        if (ent is null)
        {
            return Unauthorized();
        }

        if (!ent.AllowsWatchlist)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Takip listesi Pro planda. Ayarlar üzerinden Pro’ya geçebilirsiniz.",
                    requiredPlanCode = "pro",
                });
        }

        var item = await _db.WatchlistItems
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (item is null)
        {
            return NotFound();
        }

        _db.WatchlistItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
