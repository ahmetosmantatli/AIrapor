using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users/{userId:int}/settings")]
public class UserSettingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public UserSettingsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Para birimi, zaman dilimi, attribution, reklam hesabı kimliği (JWT ile rota userId eşleşmeli).</summary>
    [HttpPatch]
    public async Task<ActionResult> Patch(
        int userId,
        [FromBody] UserSettingsPatchDto body,
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

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return NotFound();
        }

        if (body.MetaAdAccountId is not null)
        {
            user.MetaAdAccountId = string.IsNullOrWhiteSpace(body.MetaAdAccountId)
                ? null
                : body.MetaAdAccountId.Trim();
        }

        if (body.Currency is not null)
        {
            user.Currency = body.Currency.Trim();
        }

        if (body.Timezone is not null)
        {
            user.Timezone = body.Timezone.Trim();
        }

        if (body.AttributionWindow is not null)
        {
            user.AttributionWindow = body.AttributionWindow.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
