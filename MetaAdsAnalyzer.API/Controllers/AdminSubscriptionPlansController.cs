using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Options;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/admin/subscription-plans")]
public class AdminSubscriptionPlansController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AdminOptions _admin;

    public AdminSubscriptionPlansController(AppDbContext db, IOptions<AdminOptions> admin)
    {
        _db = db;
        _admin = admin.Value;
    }

    /// <summary>
    /// Plan fiyatını / metnini günceller. <c>Admin:PlansApiKey</c> dolu olmalı; istekte <c>X-Admin-Key</c> başlığı aynı değeri taşımalıdır.
    /// </summary>
    [HttpPut("{code}")]
    public async Task<ActionResult<SubscriptionPlanResponseDto>> UpdatePlan(
        string code,
        [FromBody] AdminUpdateSubscriptionPlanDto body,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_admin.PlansApiKey))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Admin plan API kapalı (Admin:PlansApiKey boş)." });
        }

        if (!string.Equals(adminKey, _admin.PlansApiKey, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var c = code.Trim().ToLowerInvariant();
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == c, cancellationToken)
            .ConfigureAwait(false);
        if (plan is null)
        {
            return NotFound();
        }

        if (body.MonthlyPrice is null && body.DisplayName is null && body.Description is null
            && body.Currency is null && body.IsActive is null && body.SortOrder is null)
        {
            return BadRequest(new { message = "En az bir alan gönderilmelidir." });
        }

        if (body.MonthlyPrice is not null)
        {
            plan.MonthlyPrice = body.MonthlyPrice.Value;
        }

        if (body.DisplayName is not null)
        {
            plan.DisplayName = body.DisplayName.Trim();
        }

        if (body.Description is not null)
        {
            plan.Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
        }

        if (body.Currency is not null)
        {
            plan.Currency = body.Currency.Trim().ToUpperInvariant();
        }

        if (body.IsActive is not null)
        {
            plan.IsActive = body.IsActive.Value;
        }

        if (body.SortOrder is not null)
        {
            plan.SortOrder = body.SortOrder.Value;
        }

        plan.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(
            new SubscriptionPlanResponseDto
            {
                Code = plan.Code,
                DisplayName = plan.DisplayName,
                Description = plan.Description,
                MonthlyPrice = plan.MonthlyPrice,
                Currency = plan.Currency,
                SortOrder = plan.SortOrder,
                UpdatedAt = plan.UpdatedAt,
            });
    }
}
