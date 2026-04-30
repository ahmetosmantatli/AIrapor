using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("competitor_ads")]
public class CompetitorAd
{
    public int Id { get; set; }

    public int TrackedCompetitorId { get; set; }

    public TrackedCompetitor TrackedCompetitor { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string MetaAdArchiveId { get; set; } = null!;

    [MaxLength(64)]
    public string? PageId { get; set; }

    [MaxLength(255)]
    public string? PageName { get; set; }

    [Required]
    [MaxLength(32)]
    public string Format { get; set; } = "unknown";

    public string? BodyText { get; set; }

    public string? TitleText { get; set; }

    public string? DescriptionText { get; set; }

    public string? SnapshotUrl { get; set; }

    /// <summary>JSON array (facebook, instagram, messenger...)</summary>
    public string? PublisherPlatforms { get; set; }

    /// <summary>JSON array (tr, en...)</summary>
    public string? Languages { get; set; }

    public DateTimeOffset? DeliveryStartTime { get; set; }

    public DateTimeOffset? DeliveryStopTime { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public bool IsActive { get; set; }
}
