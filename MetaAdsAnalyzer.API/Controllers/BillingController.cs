using System.Text;
using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IStripeBillingService _billing;

    public BillingController(IStripeBillingService billing)
    {
        _billing = billing;
    }

    /// <summary>Stripe Checkout oturumu oluşturur; dönen URL’ye yönlendirin.</summary>
    [HttpPost("checkout")]
    [Authorize]
    public async Task<ActionResult<CreateCheckoutSessionResponseDto>> CreateCheckout(
        [FromBody] CreateCheckoutSessionRequestDto body,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var uid = User.GetUserId();
        if (uid is null)
        {
            return Unauthorized();
        }

        try
        {
            var url = await _billing
                .CreateCheckoutSessionUrlAsync(uid.Value, body.PlanCode.Trim().ToLowerInvariant(), cancellationToken)
                .ConfigureAwait(false);
            return Ok(new CreateCheckoutSessionResponseDto { CheckoutUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    /// <summary>Stripe Dashboard’dan gelen imzalı webhook (JWT gerekmez).</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: false);
        var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var sig = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(sig))
        {
            return BadRequest(new { message = "Stripe-Signature eksik." });
        }

        try
        {
            await _billing.HandleWebhookAsync(json, sig, cancellationToken).ConfigureAwait(false);
        }
        catch (StripeException)
        {
            return BadRequest();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }

        return Ok();
    }
}
