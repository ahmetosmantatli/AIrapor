using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MetaAdsAnalyzer.API.Controllers;

/// <summary>Video özet aggregate. İki kök: <c>/api/video-report</c> (kanonik) ve <c>/api/video-reports</c> (yaygın yazım hatası).</summary>
[ApiController]
[Authorize]
[Route("api/video-report")]
[Route("api/video-reports")]
public class VideoReportController : ControllerBase
{
    private readonly IVideoReportInsightService _insights;
    private readonly ILogger<VideoReportController> _logger;

    public VideoReportController(IVideoReportInsightService insights, ILogger<VideoReportController> logger)
    {
        _insights = insights;
        _logger = logger;
    }

    /// <summary>JWT olmadan route’un Kestrel’de kayıtlı olduğunu doğrulamak için (curl / tarayıcı).</summary>
    [HttpGet("route-ping")]
    [AllowAnonymous]
    public IActionResult RoutePing()
    {
        return Ok(
            new
            {
                ok = true,
                controller = nameof(VideoReportController),
                postAggregate = "/api/video-report/aggregate",
                postAggregateAlias = "/api/video-reports/aggregate",
                note = "aggregate için POST + JSON body + Bearer gerekir.",
            });
    }

    [HttpPost("aggregate")]
    public async Task<ActionResult<VideoReportAggregateResponseDto>> Aggregate(
        [FromBody] VideoReportAggregateRequestDto body,
        CancellationToken cancellationToken)
    {
        var adIds = (body.AdIds ?? new List<string>())
            .Select(static x => x.Trim())
            .Where(static x => x.Length > 0)
            .ToList();
        if (body.UserId <= 0 || adIds.Count == 0)
        {
            return BadRequest(new { message = "userId ve adIds gerekli." });
        }

        var auth = this.EnsureOwnUser(body.UserId);
        if (auth is not null)
        {
            return auth;
        }

        _logger.LogInformation(
            "POST aggregate hit UserId={UserId} MetaAdAccountId={Act} AdIds={AdIds}",
            body.UserId,
            body.MetaAdAccountId ?? "(null)",
            string.Join(",", adIds.Take(20)) + (adIds.Count > 20 ? "…" : string.Empty));

        var dto = await _insights.BuildAggregateAsync(body.UserId, body.MetaAdAccountId, adIds, cancellationToken)
            .ConfigureAwait(false);
        return Ok(dto);
    }
}
