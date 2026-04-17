using MetaAdsAnalyzer.API.Extensions;
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
}
