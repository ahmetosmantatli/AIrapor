using System.ComponentModel.DataAnnotations;

namespace MetaAdsAnalyzer.API.Models;

public sealed class CompetitorListItemDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PageRef { get; set; } = string.Empty;
    public string? PageId { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string LastSyncStatus { get; set; } = "pending";
    public string? LastSyncError { get; set; }
    public int NewAdsLast7Days { get; set; }
}

public sealed class CreateCompetitorRequestDto
{
    [Required]
    [MaxLength(160)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PageRef { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? PageId { get; set; }
}

public sealed class CompetitorAdItemDto
{
    public int Id { get; set; }
    public string MetaAdArchiveId { get; set; } = string.Empty;
    public string Format { get; set; } = "unknown";
    public string? BodyText { get; set; }
    public string? TitleText { get; set; }
    public string? DescriptionText { get; set; }
    public string? SnapshotUrl { get; set; }
    public IReadOnlyList<string> PublisherPlatforms { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Languages { get; set; } = Array.Empty<string>();
    public DateTimeOffset? DeliveryStartTime { get; set; }
    public DateTimeOffset? DeliveryStopTime { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SyncCompetitorResultDto
{
    public int FetchedCount { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int ClosedCount { get; set; }
}
