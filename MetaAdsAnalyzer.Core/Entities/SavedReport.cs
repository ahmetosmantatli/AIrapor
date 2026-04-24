using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("saved_reports")]
public class SavedReport
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string AdId { get; set; } = null!;

    [MaxLength(1024)]
    public string? AdName { get; set; }

    [MaxLength(2048)]
    public string? ThumbnailUrl { get; set; }

    [MaxLength(128)]
    public string? CampaignId { get; set; }

    [MaxLength(1024)]
    public string? CampaignName { get; set; }

    [MaxLength(128)]
    public string? AdsetId { get; set; }

    [MaxLength(1024)]
    public string? AdsetName { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? AggregateRoas { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? AggregateHookRate { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? AggregateHoldRate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? AggregateSpend { get; set; }

    public int? AggregatePurchases { get; set; }

    public DateTimeOffset AnalyzedAt { get; set; }

    public ICollection<SavedReportSuggestion> Suggestions { get; set; } = new List<SavedReportSuggestion>();
}

