namespace MetaAdsAnalyzer.API.Services.Competitors;

public interface ICompetitorAdLibraryClient
{
    Task<IReadOnlyList<AdLibraryAdItem>> FetchAdsAsync(
        string pageRef,
        string? pageId,
        CancellationToken cancellationToken);
}

public sealed class AdLibraryAdItem
{
    public string Id { get; set; } = string.Empty;
    public string? PageId { get; set; }
    public string? PageName { get; set; }
    public string Format { get; set; } = "unknown";
    public string? BodyText { get; set; }
    public string? TitleText { get; set; }
    public string? DescriptionText { get; set; }
    public string? SnapshotUrl { get; set; }
    public string PublisherPlatforms { get; set; } = "[]";
    public string Languages { get; set; } = "[]";
    public DateTimeOffset? DeliveryStartTime { get; set; }
    public DateTimeOffset? DeliveryStopTime { get; set; }
}
