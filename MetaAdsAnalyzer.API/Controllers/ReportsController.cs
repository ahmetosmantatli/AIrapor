using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IPdfReportService _pdf;

    public ReportsController(IPdfReportService pdf)
    {
        _pdf = pdf;
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
