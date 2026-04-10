using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("campaign_product_map")]
public class CampaignProductMap
{
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string CampaignId { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int UserId { get; set; }

    public User User { get; set; } = null!;
}
