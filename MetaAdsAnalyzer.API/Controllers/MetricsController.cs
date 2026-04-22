using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/metrics")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsComputationService _metrics;
    private readonly AppDbContext _db;

    public MetricsController(IMetricsComputationService metrics, AppDbContext db)
    {
        _metrics = metrics;
        _db = db;
    }

    /// <summary>Kullanıcının tüm raw_insights kayıtları için hesaplanmış metrikleri yeniden üretir (kampanya–ürün eşlemesi olanlar).</summary>
    [HttpPost("recompute")]
    public async Task<ActionResult<MetricsRecomputeResultDto>> RecomputeForUser(
        [FromBody] MetricsRecomputeRequestDto body,
        CancellationToken cancellationToken)
    {
        if (body.UserId <= 0)
        {
            return BadRequest(new { message = "UserId gerekli." });
        }

        var auth = this.EnsureOwnUser(body.UserId);
        if (auth is not null)
        {
            return auth;
        }

        try
        {
            var adIds = body.AdIds is { Count: > 0 }
                ? (IReadOnlyList<string>?)body.AdIds
                : null;
            var result = await _metrics.RecomputeForUserAsync(body.UserId, adIds, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Tek bir ham insight satırı için hesap üretir.</summary>
    [HttpPost("recompute/raw/{rawInsightId:int}")]
    public async Task<ActionResult> RecomputeOne(int rawInsightId, CancellationToken cancellationToken)
    {
        var uid = User.GetUserId();
        if (uid is null)
        {
            return Unauthorized();
        }

        var raw = await _db.RawInsights.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rawInsightId, cancellationToken)
            .ConfigureAwait(false);
        if (raw is null)
        {
            return NotFound();
        }

        if (raw.UserId != uid.Value)
        {
            return Forbid();
        }

        var ok = await _metrics.RecomputeRawInsightAsync(rawInsightId, cancellationToken).ConfigureAwait(false);
        return ok ? Ok() : NotFound();
    }
}
