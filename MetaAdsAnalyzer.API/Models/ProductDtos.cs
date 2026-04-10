using System.ComponentModel.DataAnnotations;

namespace MetaAdsAnalyzer.API.Models;

public sealed class CreateProductRequestDto
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    [MaxLength(512)]
    public string Name { get; set; } = null!;

    public decimal Cogs { get; set; }

    public decimal SellingPrice { get; set; }

    public decimal ShippingCost { get; set; }

    /// <summary>Yüzde olarak, örn. 2.9 = %2,9</summary>
    public decimal PaymentFeePct { get; set; } = 2.9m;

    public decimal ReturnRatePct { get; set; }

    public decimal LtvMultiplier { get; set; } = 1m;

    public decimal TargetMarginPct { get; set; }
}

public sealed class ProductResponseDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public decimal Cogs { get; set; }

    public decimal SellingPrice { get; set; }

    public decimal ShippingCost { get; set; }

    public decimal PaymentFeePct { get; set; }

    public decimal ReturnRatePct { get; set; }

    public decimal LtvMultiplier { get; set; }

    public decimal TargetMarginPct { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
