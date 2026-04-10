using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
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
                    Currency = u.Currency,
                    Timezone = u.Timezone,
                    AttributionWindow = u.AttributionWindow,
                    MetaUserId = u.MetaUserId,
                    MetaTokenExpiresAt = u.MetaTokenExpiresAt,
                    PlanCode = u.SubscriptionPlan.Code,
                    PlanDisplayName = u.SubscriptionPlan.DisplayName,
                    PlanMonthlyPrice = u.SubscriptionPlan.MonthlyPrice,
                    PlanCurrency = u.SubscriptionPlan.Currency,
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return dto is null ? NotFound() : Ok(dto);
    }
}
