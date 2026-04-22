using MetaAdsAnalyzer.API.Extensions;
using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IPdfReportService _pdf;
    private readonly AppDbContext _db;

    public ReportsController(IPdfReportService pdf, AppDbContext db)
    {
        _pdf = pdf;
        _db = db;
    }

    /// <summary>Özet metrik ve aktif direktifleri içeren PDF (Faza 6).</summary>
    [HttpGet("analysis.pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> AnalysisPdf(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var ent = await _db.GetPlanEntitlementsForUserAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        if (ent is null)
        {
            return Unauthorized();
        }

        if (!ent.AllowsPdfExport)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "PDF dışa aktarma Pro planda. Ayarlar üzerinden Pro’ya geçebilirsiniz.",
                    requiredPlanCode = "pro",
                });
        }

        try
        {
            var bytes = await _pdf.BuildAnalysisReportAsync(userId.Value, cancellationToken).ConfigureAwait(false);
            return File(bytes, "application/pdf", $"meta-analiz-{userId}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Seçilen videoya ait birleşik metrik, özet metin, etiketler ve direktifler (PDF).</summary>
    [HttpPost("video.pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> VideoReportPdf(
        [FromBody] VideoReportPdfRequestDto body,
        CancellationToken cancellationToken)
    {
        if (body.UserId <= 0 || body.AdIds.Count == 0)
        {
            return BadRequest(new { message = "userId ve adIds gerekli." });
        }

        var auth = this.EnsureOwnUser(body.UserId);
        if (auth is not null)
        {
            return auth;
        }

        var ent = await _db.GetPlanEntitlementsForUserAsync(body.UserId, cancellationToken).ConfigureAwait(false);
        if (ent is null)
        {
            return Unauthorized();
        }

        if (!ent.AllowsPdfExport)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "PDF dışa aktarma Pro planda. Ayarlar üzerinden Pro’ya geçebilirsiniz.",
                    requiredPlanCode = "pro",
                });
        }

        try
        {
            var bytes = await _pdf.BuildVideoReportPdfAsync(
                    body.UserId,
                    body.MetaAdAccountId,
                    body.AdIds,
                    body.VideoId,
                    body.DisplayName,
                    cancellationToken)
                .ConfigureAwait(false);
            var safe = string.IsNullOrWhiteSpace(body.VideoId) ? "video" : body.VideoId.Trim();
            return File(bytes, "application/pdf", $"video-rapor-{safe}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
