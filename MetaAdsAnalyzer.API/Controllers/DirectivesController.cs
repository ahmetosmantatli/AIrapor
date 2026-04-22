using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/directives")]
public class DirectivesController : ControllerBase
{
    private readonly IDirectiveEngineService _engine;

    public DirectivesController(IDirectiveEngineService engine)
    {
        _engine = engine;
    }

    [HttpGet("active/by-user/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<DirectiveListItemDto>>> ListActive(
        int userId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return BadRequest();
        }

        var auth = this.EnsureOwnUser(userId);
        if (auth is not null)
        {
            return auth;
        }

        var list = await _engine.GetActiveDirectivesAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    /// <summary>
    /// Ham insight + hesaplanmış metrikten kampanya / ad set / reklam kurallarını çalıştırır.
    /// Aynı kullanıcı için önceki aktif direktifler pasifleştirilir.
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<ActionResult<DirectiveEvaluateResultDto>> Evaluate(
        [FromBody] DirectiveEvaluateRequestDto body,
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
            var result = await _engine.EvaluateForUserAsync(body.UserId, adIds, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
