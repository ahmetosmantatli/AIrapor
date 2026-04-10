namespace MetaAdsAnalyzer.API.Options;

/// <summary>Plan fiyatlarını API ile güncellemek için (boşsa admin uçları kapalı).</summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary><c>X-Admin-Key</c> başlığı ile eşleşmeli.</summary>
    public string PlansApiKey { get; set; } = string.Empty;
}
