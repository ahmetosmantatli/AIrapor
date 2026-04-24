using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("saved_report_suggestions")]
public class SavedReportSuggestion
{
    public int Id { get; set; }

    public int SavedReportId { get; set; }

    public SavedReport SavedReport { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string SuggestionKey { get; set; } = null!;

    [MaxLength(32)]
    public string? DirectiveType { get; set; }

    [Required]
    [MaxLength(32)]
    public string Severity { get; set; } = "info";

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = null!;

    [MaxLength(1000)]
    public string? Symptom { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [MaxLength(1000)]
    public string? Action { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    public DateTimeOffset? SkippedAt { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? BeforeRoas { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? BeforeHookRate { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? BeforeHoldRate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? BeforeSpend { get; set; }

    public int? BeforePurchases { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? AfterRoas { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? AfterHookRate { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? AfterHoldRate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? AfterSpend { get; set; }

    public int? AfterPurchases { get; set; }

    public DateTimeOffset? ImpactMeasuredAt { get; set; }

    public bool MetaChangeDetected { get; set; } = true;

    [MaxLength(2000)]
    public string? MetaChangeMessage { get; set; }
}

