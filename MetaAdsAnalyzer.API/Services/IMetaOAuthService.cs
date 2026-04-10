namespace MetaAdsAnalyzer.API.Services;

public interface IMetaOAuthService
{
    string BuildAuthorizeUrl(string state);

    Task<MetaOAuthResult> CompleteAuthorizationAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Mevcut uzun ömürlü jetonu yenisiyle değiştirir (süre dolmadan arka planda çağrılabilir).</summary>
    Task<MetaAccessTokenResult> RefreshLongLivedTokenAsync(string currentAccessToken, CancellationToken cancellationToken = default);
}
