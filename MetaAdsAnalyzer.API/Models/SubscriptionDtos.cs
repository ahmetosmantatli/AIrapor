using System.ComponentModel.DataAnnotations;

namespace MetaAdsAnalyzer.API.Models;

public sealed class SubscriptionPlanResponseDto
{
    public string Code { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal MonthlyPrice { get; set; }

    public string Currency { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SelectMyPlanRequestDto
{
    [Required]
    [RegularExpression("^(standard|pro)$", ErrorMessage = "planCode: standard veya pro olmalıdır.")]
    public string PlanCode { get; set; } = null!;
}

public sealed class AdminUpdateSubscriptionPlanDto
{
    [Range(0, 999999999)]
    public decimal? MonthlyPrice { get; set; }

    [MaxLength(128)]
    public string? DisplayName { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(8)]
    public string? Currency { get; set; }

    public bool? IsActive { get; set; }

    public int? SortOrder { get; set; }
}
