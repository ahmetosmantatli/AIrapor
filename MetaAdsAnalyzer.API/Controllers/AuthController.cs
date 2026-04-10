using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _jwt;

    public AuthController(AppDbContext db, IPasswordHasher<User> passwordHasher, IJwtTokenService jwt)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterRequestDto body,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var email = body.Email.Trim();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return Conflict(new { message = "Bu e-posta ile kayıt zaten var." });
        }

        var standardPlanId = await _db.SubscriptionPlans.AsNoTracking()
            .Where(p => p.Code == "standard" && p.IsActive)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (standardPlanId == 0)
        {
            return Problem(
                title: "Abonelik planları yapılandırılmamış",
                detail: "Veritabanında aktif 'standard' planı yok.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var user = new User
        {
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow,
            Currency = "TRY",
            Timezone = "UTC",
            AttributionWindow = "7d_click_1d_view",
            SubscriptionPlanId = standardPlanId,
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, body.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var token = _jwt.CreateToken(user.Id, user.Email);
        return Ok(
            new AuthResponseDto
            {
                AccessToken = token,
                UserId = user.Id,
                Email = user.Email,
                ExpiresAtUtc = _jwt.GetExpiryUtc(),
            });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequestDto body,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var email = body.Email.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken)
            .ConfigureAwait(false);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return Unauthorized(new { message = "E-posta veya şifre hatalı (veya yalnızca Meta ile giriş)." });
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, body.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "E-posta veya şifre hatalı." });
        }

        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, body.Password);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var token = _jwt.CreateToken(user.Id, user.Email);
        return Ok(
            new AuthResponseDto
            {
                AccessToken = token,
                UserId = user.Id,
                Email = user.Email,
                ExpiresAtUtc = _jwt.GetExpiryUtc(),
            });
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me()
    {
        var id = User.GetUserId();
        if (id is null)
        {
            return Unauthorized();
        }

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(JwtRegisteredClaimNames.Email);
        return Ok(new { userId = id.Value, email });
    }
}
