using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MetaAdsAnalyzer.API.Options;
using MetaAdsAnalyzer.API.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MetaAdsAnalyzer.API.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public DateTimeOffset GetExpiryUtc()
    {
        EnsureSecret();
        return DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _options.ExpiresMinutes));
    }

    public string CreateToken(int userId, string email)
    {
        EnsureSecret();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var idStr = userId.ToString();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, idStr),
            new Claim(AuthorizationExtensions.UserIdClaimType, idStr),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: GetExpiryUtc().UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private void EnsureSecret()
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey) || _options.SecretKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey appsettings içinde tanımlı ve en az 32 karakter olmalıdır.");
        }
    }
}
