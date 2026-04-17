using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using MetaAdsAnalyzer.Core.Subscription;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Route("api/subscription")]
public class SubscriptionPlansController : ControllerBase
{
    private readonly AppDbContext _db;

    public SubscriptionPlansController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Aktif planlar ve güncel fiyatlar (landing / ayarlar).</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SubscriptionPlanResponseDto>>> ListPlans(
        CancellationToken cancellationToken)
    {
        var list = await _db.SubscriptionPlans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .Select(
                p => new SubscriptionPlanResponseDto
                {
                    Code = p.Code,
                    DisplayName = p.DisplayName,
                    Description = p.Description,
                    MonthlyPrice = p.MonthlyPrice,
                    Currency = p.Currency,
                    SortOrder = p.SortOrder,
                    UpdatedAt = p.UpdatedAt,
                    AllowsPdfExport = p.AllowsPdfExport,
                    AllowsWatchlist = p.AllowsWatchlist,
                    MaxLinkedMetaAdAccounts = p.MaxLinkedMetaAdAccounts,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(list);
    }

    /// <summary>Ödeme entegrasyonu öncesi: kullanıcı planını değiştirir (JWT).</summary>
    [HttpPost("my-plan")]
    [Authorize]
    public async Task<ActionResult> SelectMyPlan(
        [FromBody] SelectMyPlanRequestDto body,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var uid = User.GetUserId();
        if (uid is null)
        {
            return Unauthorized();
        }

        var code = body.PlanCode.Trim().ToLowerInvariant();
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == code && p.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (plan is null)
        {
            return BadRequest(new { message = "Geçersiz veya pasif plan." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid.Value, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return NotFound();
        }

        user.SubscriptionPlanId = plan.Id;
        user.SubscriptionStatus = SubscriptionStatuses.Active;
        user.PlanExpiresAt = null;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await LinkedMetaAdAccountTrimHelper.EnforcePlanLimitAsync(_db, uid.Value, cancellationToken)
            .ConfigureAwait(false);
        return NoContent();
    }
}
