using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("products")]
public class Product
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    [Required]
    [MaxLength(512)]
    public string Name { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Cogs { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal SellingPrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ShippingCost { get; set; }

    [Column(TypeName = "decimal(9,4)")]
    public decimal PaymentFeePct { get; set; }

    [Column(TypeName = "decimal(9,4)")]
    public decimal ReturnRatePct { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal LtvMultiplier { get; set; } = 1m;

    [Column(TypeName = "decimal(9,4)")]
    public decimal TargetMarginPct { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<CampaignProductMap> CampaignProductMaps { get; set; } = new List<CampaignProductMap>();
}
