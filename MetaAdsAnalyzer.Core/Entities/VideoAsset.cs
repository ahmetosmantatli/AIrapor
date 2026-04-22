using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("video_assets")]
public class VideoAsset
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string MetaAdAccountId { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string VideoId { get; set; } = null!;

    [MaxLength(2048)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>Temsilci reklam/kreatif adı (çoğunlukla en yüksek harcamalı).</summary>
    [MaxLength(512)]
    public string? RepresentativeAdName { get; set; }

    public DateOnly FirstSeenDate { get; set; }

    public DateOnly LastSeenDate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalSpend { get; set; }

    public long TotalPurchases { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalPurchaseValue { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? TotalRoas { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? HookRateAvg { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? HoldRateAvg { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? CompletionRateAvg { get; set; }

    public DateTimeOffset AggregatedAt { get; set; }
}
