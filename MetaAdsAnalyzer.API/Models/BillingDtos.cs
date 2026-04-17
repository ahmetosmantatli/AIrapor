using System.ComponentModel.DataAnnotations;

namespace MetaAdsAnalyzer.API.Models;

public sealed class CreateCheckoutSessionRequestDto
{
    [Required]
    [RegularExpression("^(standard|pro)$", ErrorMessage = "planCode: standard veya pro olmalıdır.")]
    public string PlanCode { get; set; } = null!;
}

public sealed class CreateCheckoutSessionResponseDto
{
    public string CheckoutUrl { get; set; } = null!;
}
