namespace MetaAdsAnalyzer.API.Models;

public sealed class UserProfileResponseDto
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string? MetaAdAccountId { get; set; }

    public string Currency { get; set; } = null!;

    public string Timezone { get; set; } = null!;

    public string AttributionWindow { get; set; } = null!;

    public string? MetaUserId { get; set; }

    public DateTimeOffset? MetaTokenExpiresAt { get; set; }

    public string PlanCode { get; set; } = null!;

    public string PlanDisplayName { get; set; } = null!;

    public decimal PlanMonthlyPrice { get; set; }

    public string PlanCurrency { get; set; } = null!;
}
