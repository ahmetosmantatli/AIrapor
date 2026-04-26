using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

namespace MetaAdsAnalyzer.API.Controllers;

/// <summary>
/// Meta Graph’tan kampanya / reklam seti keşfi (hesap → kampanya → adset → reklam akışı).
/// Rotalar: <c>GET /api/meta/campaigns/{userId}</c>, <c>GET /api/meta/adsets/{userId}</c> (query parametreleri Swagger’da).
/// </summary>
[ApiController]
[Authorize]
[Route("api/meta")]
public sealed class MetaMarketingExplorerController : ControllerBase
{
    private readonly IMetaInsightsSyncService _insights;

    public MetaMarketingExplorerController(IMetaInsightsSyncService insights)
    {
        _insights = insights;
    }

    /// <summary>Seçili <c>act_…</c> hesabındaki kampanyalar (<c>…/campaigns?fields=id,name,status,objective</c>).</summary>
    [HttpGet("campaigns/{userId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<MetaCampaignListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MetaCampaignListItemDto>>> GetCampaigns(
        int userId,
        [FromQuery] string? metaAdAccountId,
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
            var list = await _insights.ListCampaignsAsync(userId, metaAdAccountId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(list);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Meta servisine şu anda erişilemiyor. Lütfen biraz sonra tekrar deneyin." });
        }
    }

    /// <summary>Kampanyaya ait reklam setleri (<c>campaignId</c> zorunlu).</summary>
    [HttpGet("adsets/{userId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<MetaAdsetListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MetaAdsetListItemDto>>> GetAdsets(
        int userId,
        [FromQuery] string campaignId,
        [FromQuery] string? metaAdAccountId,
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
            var list = await _insights.ListAdsetsAsync(userId, campaignId, metaAdAccountId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(list);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Meta servisine şu anda erişilemiyor. Lütfen biraz sonra tekrar deneyin." });
        }
    }
}
