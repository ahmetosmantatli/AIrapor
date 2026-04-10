using System.ComponentModel.DataAnnotations;

namespace MetaAdsAnalyzer.API.Models;

public sealed class CreateCampaignProductMapRequestDto
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    [MaxLength(128)]
    public string CampaignId { get; set; } = null!;

    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }
}

public sealed class CampaignProductMapResponseDto
{
    public int Id { get; set; }

    public string CampaignId { get; set; } = null!;

    public int ProductId { get; set; }

    public int UserId { get; set; }
}
