using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("raw_insights")]
public class RawInsight
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTimeOffset FetchedAt { get; set; }

    [Required]
    [MaxLength(32)]
    public string Level { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string EntityId { get; set; } = null!;

    [MaxLength(1024)]
    public string? EntityName { get; set; }

    /// <summary>Meta kampanya kimliği (ad/adset satırlarında ürün eşlemesi için).</summary>
    [MaxLength(128)]
    public string? MetaCampaignId { get; set; }

    /// <summary>Insights kaynağı reklam hesabı (Graph act_…).</summary>
    [MaxLength(64)]
    public string? MetaAdAccountId { get; set; }

    public DateOnly DateStart { get; set; }

    public DateOnly DateStop { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Spend { get; set; }

    public long Impressions { get; set; }

    public long Reach { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal Frequency { get; set; }

    public long LinkClicks { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal CtrLink { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal CtrAll { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal Cpm { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal CpcLink { get; set; }

    public long Purchases { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal PurchaseValue { get; set; }

    public long AddToCart { get; set; }

    public long InitiateCheckout { get; set; }

    public long ViewContent { get; set; }

    public long VideoPlay3s { get; set; }

    public long VideoThruplay { get; set; }

    /// <summary>15 sn video izlenme (Graph <c>video_15_sec_watched_actions</c> toplamı).</summary>
    public long Video15Sec { get; set; }

    /// <summary>30 sn video izlenme (Graph <c>video_30_sec_watched_actions</c>).</summary>
    public long Video30Sec { get; set; }

    /// <summary>P95 izlenme (Graph <c>video_p95_watched_actions</c>).</summary>
    public long VideoP95 { get; set; }

    public long VideoP25 { get; set; }

    public long VideoP50 { get; set; }

    public long VideoP75 { get; set; }

    public long VideoP100 { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal VideoAvgWatchTime { get; set; }

    public ICollection<ComputedMetric> ComputedMetrics { get; set; } = new List<ComputedMetric>();
}
