using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// API ve SQL Server bağlantı durumu.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        string database;
        try
        {
            database = await _db.Database.CanConnectAsync(cancellationToken)
                ? "connected"
                : "unreachable";
        }
        catch (Exception)
        {
            database = "error";
        }

        return Ok(new HealthResponse(Status: "ok", Database: database));
    }

    public sealed record HealthResponse(string Status, string Database);
}
