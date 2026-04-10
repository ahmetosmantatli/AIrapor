namespace MetaAdsAnalyzer.API.Services;

public sealed record MetaOAuthResult(
    string AccessToken,
    DateTimeOffset? ExpiresAt,
    string MetaUserId,
    string Email);
