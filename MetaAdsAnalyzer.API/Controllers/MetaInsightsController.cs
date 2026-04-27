using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/meta")]
public class MetaInsightsController : ControllerBase
{
    private readonly IMetaInsightsSyncService _insights;
    private readonly IVideoAssetSyncService _videoAssets;
    private readonly AppDbContext _db;
    private readonly ILogger<MetaInsightsController> _logger;
    private const int DailyManualSyncLimit = 10;

    public MetaInsightsController(
        IMetaInsightsSyncService insights,
        IVideoAssetSyncService videoAssets,
        AppDbContext db,
        ILogger<MetaInsightsController> logger)
    {
        _insights = insights;
        _videoAssets = videoAssets;
        _db = db;
        _logger = logger;
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
                    false,
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
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Meta servisine şu anda erişilemiyor. Lütfen biraz sonra tekrar deneyin." });
        }
    }

    [HttpPost("insights/refresh")]
    [ProducesResponseType(typeof(InsightsRefreshResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<InsightsRefreshResponseDto>> RefreshInsights(
        [FromBody] InsightsRefreshRequestDto body,
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
            var actId = string.IsNullOrWhiteSpace(body.MetaAdAccountId) ? null : body.MetaAdAccountId.Trim();
            _logger.LogInformation(
                "Insights refresh request received UserId={UserId} ActId={ActId}",
                body.UserId,
                actId ?? "(default)");

            var lastSync = await GetLastSyncAsync(body.UserId, actId, cancellationToken).ConfigureAwait(false);
            if (lastSync is not null && DateTimeOffset.UtcNow - lastSync.Value < TimeSpan.FromHours(1))
            {
                var mins = Math.Max(1, (int)Math.Round((DateTimeOffset.UtcNow - lastSync.Value).TotalMinutes));
                _logger.LogInformation(
                    "Insights refresh skipped by cooldown UserId={UserId} ActId={ActId} LastSync={LastSync} MinutesAgo={MinutesAgo}",
                    body.UserId,
                    actId ?? "(default)",
                    lastSync,
                    mins);

                return Ok(
                    new InsightsRefreshResponseDto
                    {
                        Status = "skipped",
                        Message = $"Son güncelleme {mins} dk önce",
                        LastSync = lastSync,
                    });
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var log = await _db.UserSyncLogs
                .FirstOrDefaultAsync(x => x.UserId == body.UserId && x.Date == today, cancellationToken)
                .ConfigureAwait(false);
            if (log is not null && log.SyncCount >= DailyManualSyncLimit)
            {
                _logger.LogWarning(
                    "Insights refresh blocked by daily limit UserId={UserId} ActId={ActId} DailyCount={DailyCount}",
                    body.UserId,
                    actId ?? "(default)",
                    log.SyncCount);

                return Ok(
                    new InsightsRefreshResponseDto
                    {
                        Status = "limit",
                        Message = "Gunluk guncelleme limitine ulastiniz. Veriler sabah otomatik guncellenir.",
                        LastSync = lastSync,
                        DailyCount = log.SyncCount,
                    });
            }

            _logger.LogInformation(
                "Insights refresh started sync pipelines UserId={UserId} ActId={ActId}",
                body.UserId,
                actId ?? "(default)");

            await _insights.SyncInsightsAsync(body.UserId, "campaign", "last_30d", null, actId, null, true, cancellationToken)
                .ConfigureAwait(false);
            await _insights.SyncInsightsAsync(body.UserId, "adset", "last_30d", null, actId, null, true, cancellationToken)
                .ConfigureAwait(false);
            await _insights.SyncInsightsAsync(body.UserId, "ad", "last_30d", null, actId, null, true, cancellationToken)
                .ConfigureAwait(false);

            if (log is null)
            {
                log = new Core.Entities.UserSyncLog
                {
                    UserId = body.UserId,
                    Date = today,
                    SyncCount = 0,
                    MetaAdAccountId = actId,
                };
                _db.UserSyncLogs.Add(log);
            }

            log.SyncCount += 1;
            log.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var refreshedAt = await GetLastSyncAsync(body.UserId, actId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Insights refresh completed UserId={UserId} ActId={ActId} LastSync={LastSync} DailyCount={DailyCount}",
                body.UserId,
                actId ?? "(default)",
                refreshedAt,
                log.SyncCount);

            return Ok(
                new InsightsRefreshResponseDto
                {
                    Status = "updated",
                    Message = "Veriler guncellendi",
                    LastSync = refreshedAt,
                    DailyCount = log.SyncCount,
                });
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
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Meta servisine şu anda erişilemiyor. Lütfen biraz sonra tekrar deneyin." });
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
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Meta servisine şu anda erişilemiyor. Lütfen biraz sonra tekrar deneyin." });
        }
    }

    private async Task<DateTimeOffset?> GetLastSyncAsync(int userId, string? metaAdAccountId, CancellationToken cancellationToken)
    {
        var query = _db.RawInsights.AsNoTracking().Where(x => x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(metaAdAccountId))
        {
            query = query.Where(x => x.MetaAdAccountId == metaAdAccountId);
        }

        return await query.MaxAsync(x => (DateTimeOffset?)x.FetchedAt, cancellationToken).ConfigureAwait(false);
    }
}
