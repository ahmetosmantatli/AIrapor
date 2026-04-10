namespace MetaAdsAnalyzer.API.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 için en az 32 karakter önerilir.</summary>
    public string SecretKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "MetaAdsAnalyzer";

    public string Audience { get; set; } = "MetaAdsAnalyzer";

    public int ExpiresMinutes { get; set; } = 10080;
}
