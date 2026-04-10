using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/meta")]
public class MetaInsightsController : ControllerBase
{
    private readonly IMetaInsightsSyncService _insights;

    public MetaInsightsController(IMetaInsightsSyncService insights)
    {
        _insights = insights;
    }

    /// <summary>
    /// Meta Graph insights verisini çeker; <c>raw_insights</c> tablosuna yazar (aynı tarih aralığı + varlık için günceller).
    /// JWT zorunlu; gövde/rotadaki userId, token’daki kullanıcı ile aynı olmalıdır.
    /// </summary>
    [HttpPost("insights/sync")]
    [ProducesResponseType(typeof(InsightsSyncResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InsightsSyncResponseDto>> SyncInsights(
        [FromBody] InsightsSyncRequestDto body,
        CancellationToken cancellationToken)
    {
        if (body.UserId <= 0)
        {
            return BadRequest(new { message = "UserId geçerli olmalıdır." });
        }

        var auth = this.EnsureOwnUser(body.UserId);
        if (auth is not null)
        {
            return auth;
        }

        try
        {
            var result = await _insights.SyncInsightsAsync(body.UserId, body.Level, body.DatePreset, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Kullanıcının erişebildiği reklam hesaplarını listeler (MetaAdAccountId seçimi için).</summary>
    [HttpGet("ad-accounts/{userId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<MetaAdAccountItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MetaAdAccountItemDto>>> ListAdAccounts(
        int userId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return BadRequest(new { message = "userId geçerli olmalıdır." });
        }

        var auth = this.EnsureOwnUser(userId);
        if (auth is not null)
        {
            return auth;
        }

        try
        {
            var list = await _insights.ListAdAccountsAsync(userId, cancellationToken).ConfigureAwait(false);
            return Ok(list);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
