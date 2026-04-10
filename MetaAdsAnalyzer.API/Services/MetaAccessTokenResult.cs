namespace MetaAdsAnalyzer.API.Services;

public sealed record MetaAccessTokenResult(string AccessToken, DateTimeOffset? ExpiresAt);
