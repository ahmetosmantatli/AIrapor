namespace MetaAdsAnalyzer.API.Options;

public sealed class MetaAdLibraryOptions
{
    public const string SectionName = "MetaAdLibrary";

    /// <summary>
    /// App token format: {app_id}|{app_secret}
    /// </summary>
    public string AppToken { get; set; } = string.Empty;

    public string ApiVersion { get; set; } = "v21.0";

    /// <summary>
    /// Fetch window for start date filter (days).
    /// </summary>
    public int DeliveryDateWindowDays { get; set; } = 90;

    /// <summary>
    /// API page size (max 100).
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Per run max pagination depth.
    /// </summary>
    public int MaxPagesPerRun { get; set; } = 10;

    /// <summary>
    /// Retry count for transient/rate-limit failures.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential backoff.
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 1200;

    /// <summary>
    /// Delay between page requests (basic throttle).
    /// </summary>
    public int InterRequestDelayMs { get; set; } = 350;
}
