using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.Core.Subscription;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Jeton içeriği dönmez; yalnızca güvenli profil alanları.</summary>
    [HttpGet("{userId:int}")]
    public async Task<ActionResult<UserProfileResponseDto>> GetProfile(
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

        var dto = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(
                u => new UserProfileResponseDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    MetaAdAccountId = u.MetaAdAccountId,
                    MaxLinkedMetaAdAccounts = u.SubscriptionPlan.MaxLinkedMetaAdAccounts,
                    Currency = u.Currency,
                    Timezone = u.Timezone,
                    AttributionWindow = u.AttributionWindow,
                    MetaUserId = u.MetaUserId,
                    MetaTokenExpiresAt = u.MetaTokenExpiresAt,
                    PlanCode = u.SubscriptionPlan.Code,
                    PlanDisplayName = u.SubscriptionPlan.DisplayName,
                    PlanMonthlyPrice = u.SubscriptionPlan.MonthlyPrice,
                    PlanCurrency = u.SubscriptionPlan.Currency,
                    PlanAllowsPdfExport = u.SubscriptionPlan.AllowsPdfExport,
                    PlanAllowsWatchlist = u.SubscriptionPlan.AllowsWatchlist,
                    SubscriptionStatus = u.SubscriptionStatus,
                    PlanExpiresAt = u.PlanExpiresAt,
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return NotFound();
        }

        dto.LinkedMetaAdAccounts = await _db.UserMetaAdAccounts.AsNoTracking()
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

        var grants = SubscriptionAccess.GrantsPlanFeatures(
            dto.SubscriptionStatus,
            dto.PlanExpiresAt,
            DateTimeOffset.UtcNow);
        dto.PlanAllowsPdfExport = grants && dto.PlanAllowsPdfExport;
        dto.PlanAllowsWatchlist = grants && dto.PlanAllowsWatchlist;
        return Ok(dto);
    }
}
