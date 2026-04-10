namespace MetaAdsAnalyzer.API.Services;

public interface IJwtTokenService
{
    string CreateToken(int userId, string email);

    DateTimeOffset GetExpiryUtc();
}
