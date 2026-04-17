using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using MetaAdsAnalyzer.Core;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users/{userId:int}/meta-ad-accounts")]
public class UserMetaAdAccountsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMetaInsightsSyncService _metaInsights;

    public UserMetaAdAccountsController(AppDbContext db, IMetaInsightsSyncService metaInsights)
    {
        _db = db;
        _metaInsights = metaInsights;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserMetaAdAccountItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserMetaAdAccountItemDto>>> List(
        int userId,
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

        var list = await _db.UserMetaAdAccounts.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.LinkedAt)
            .Select(
                x => new UserMetaAdAccountItemDto
                {
                    Id = x.Id,
                    MetaAdAccountId = x.MetaAdAccountId,
                    DisplayName = x.DisplayName,
                    LinkedAt = x.LinkedAt,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(list);
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserMetaAdAccountItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserMetaAdAccountItemDto>> Link(
        int userId,
        [FromBody] LinkUserMetaAdAccountRequestDto body,
        CancellationToken cancellationToken)
    {
        if (userId <= 0 || !ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var auth = this.EnsureOwnUser(userId);
        if (auth is not null)
        {
            return auth;
        }

        var act = MetaAdAccountIdNormalizer.Normalize(body.MetaAdAccountId);
        if (string.IsNullOrEmpty(act))
        {
            return BadRequest(new { message = "Geçerli bir reklam hesabı kimliği gerekli." });
        }

        var user = await _db.Users
            .Include(u => u.SubscriptionPlan)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return NotFound();
        }

        var exists = await _db.UserMetaAdAccounts.AnyAsync(
                x => x.UserId == userId && x.MetaAdAccountId == act,
                cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return BadRequest(new { message = "Bu hesap zaten bağlı." });
        }

        var count = await _db.UserMetaAdAccounts.CountAsync(x => x.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (count >= user.SubscriptionPlan.MaxLinkedMetaAdAccounts)
        {
            return BadRequest(
                new
                {
                    message =
                        $"Planınız en fazla {user.SubscriptionPlan.MaxLinkedMetaAdAccounts} reklam hesabına izin veriyor.",
                });
        }

        IReadOnlyList<MetaAdAccountItemDto> graphAccounts;
        try
        {
            graphAccounts = await _metaInsights.ListAdAccountsAsync(userId, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var allowed = graphAccounts.Any(
            a =>
            {
                var idNorm = MetaAdAccountIdNormalizer.Normalize(a.Id);
                var acctNorm = MetaAdAccountIdNormalizer.Normalize(a.AccountId);
                return string.Equals(idNorm, act, StringComparison.Ordinal)
                       || string.Equals(acctNorm, act, StringComparison.Ordinal);
            });
        if (!allowed)
        {
            return BadRequest(new { message = "Bu hesap Meta hesabınızda görünmüyor veya erişim yok." });
        }

        var row = new UserMetaAdAccount
        {
            UserId = userId,
            MetaAdAccountId = act,
            DisplayName = string.IsNullOrWhiteSpace(body.DisplayName) ? null : body.DisplayName.Trim(),
            LinkedAt = DateTimeOffset.UtcNow,
        };
        _db.UserMetaAdAccounts.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = new UserMetaAdAccountItemDto
        {
            Id = row.Id,
            MetaAdAccountId = row.MetaAdAccountId,
            DisplayName = row.DisplayName,
            LinkedAt = row.LinkedAt,
        };

        return CreatedAtAction(nameof(List), new { userId }, dto);
    }

    [HttpDelete("{linkId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Unlink(int userId, int linkId, CancellationToken cancellationToken)
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

        var row = await _db.UserMetaAdAccounts.FirstOrDefaultAsync(
                x => x.Id == linkId && x.UserId == userId,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return NotFound();
        }

        var removedAct = row.MetaAdAccountId;
        var nextAct = await _db.UserMetaAdAccounts.AsNoTracking()
            .Where(x => x.UserId == userId && x.Id != linkId)
            .OrderBy(x => x.LinkedAt)
            .Select(x => x.MetaAdAccountId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        _db.UserMetaAdAccounts.Remove(row);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is not null
            && string.Equals(
                MetaAdAccountIdNormalizer.Normalize(user.MetaAdAccountId),
                removedAct,
                StringComparison.Ordinal))
        {
            user.MetaAdAccountId = string.IsNullOrEmpty(nextAct) ? null : nextAct;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Sync ve raporlar için kullanılacak aktif reklam hesabı (önceden bağlanmış olmalı).</summary>
    [HttpPost("select-active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SelectActive(
        int userId,
        [FromBody] SelectActiveMetaAdAccountRequestDto body,
        CancellationToken cancellationToken)
    {
        if (userId <= 0 || !ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var auth = this.EnsureOwnUser(userId);
        if (auth is not null)
        {
            return auth;
        }

        var act = MetaAdAccountIdNormalizer.Normalize(body.MetaAdAccountId);
        if (string.IsNullOrEmpty(act))
        {
            return BadRequest(new { message = "Geçerli bir reklam hesabı kimliği gerekli." });
        }

        var linked = await _db.UserMetaAdAccounts.AnyAsync(
                x => x.UserId == userId && x.MetaAdAccountId == act,
                cancellationToken)
            .ConfigureAwait(false);
        if (!linked)
        {
            return BadRequest(new { message = "Bu hesap bağlı değil. Önce hesabı bağlayın." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return NotFound();
        }

        user.MetaAdAccountId = act;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
