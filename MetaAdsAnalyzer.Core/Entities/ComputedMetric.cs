using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("computed_metrics")]
public class ComputedMetric
{
    public int Id { get; set; }

    public int RawInsightId { get; set; }

    public RawInsight RawInsight { get; set; } = null!;

    [Column(TypeName = "decimal(18,6)")]
    public decimal? Roas { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? Cpa { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? BreakEvenRoas { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? TargetRoas { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? MaxCpa { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? TargetCpa { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? HookRate { get; set; }

    /// <summary>3 sn izlenme / erişim ×100 (thumbstop).</summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? ThumbstopRatePct { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? HoldRate { get; set; }

    /// <summary>P100 / gösterim ×100.</summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? CompletionRatePct { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? Video3SecPerSpend { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? Video15SecPerSpend { get; set; }

    /// <summary>0–100 kreatif skor (hook/hold/completion/roas ağırlıklı).</summary>
    public int? CreativeScoreTotal { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? NetProfitPerOrder { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? NetMarginPct { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? MismatchRatio { get; set; }

    public DateTimeOffset ComputedAt { get; set; }
}
