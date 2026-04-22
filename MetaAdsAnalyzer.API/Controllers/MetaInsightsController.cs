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
    private readonly IVideoAssetSyncService _videoAssets;

    public MetaInsightsController(IMetaInsightsSyncService insights, IVideoAssetSyncService videoAssets)
    {
        _insights = insights;
        _videoAssets = videoAssets;
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
            IReadOnlyList<string>? mergedAdIds = null;
            if (body.AdIds is { Count: > 0 })
            {
                mergedAdIds = body.AdIds
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }

            var result = await _insights.SyncInsightsAsync(
                    body.UserId,
                    body.Level,
                    body.DatePreset,
                    body.AdId,
                    body.MetaAdAccountId,
                    mergedAdIds,
                    cancellationToken)
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

    /// <summary>Seçili reklam hesabındaki reklamları listeler (kreatif / video_id bilgisi dahil).</summary>
    [HttpGet("ads/{userId:int}")]
    [HttpGet("account-ads/{userId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<MetaAdListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MetaAdListItemDto>>> ListAds(
        int userId,
        [FromQuery] string? metaAdAccountId,
        [FromQuery] string? campaignId,
        [FromQuery] string? adsetId,
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
            var list = await _insights.ListAccountAdsAsync(
                    userId,
                    metaAdAccountId,
                    campaignId,
                    adsetId,
                    cancellationToken)
                .ConfigureAwait(false);
            await _videoAssets.SyncFromAdListAsync(userId, metaAdAccountId ?? string.Empty, list, cancellationToken)
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
    }
}
