namespace MetaAdsAnalyzer.API.Models;

public sealed class UserProfileResponseDto
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    /// <summary>Sync ve raporlar için seçili aktif hesap (Graph act_…).</summary>
    public string? MetaAdAccountId { get; set; }

    public int MaxLinkedMetaAdAccounts { get; set; }

    public List<UserMetaAdAccountItemDto> LinkedMetaAdAccounts { get; set; } = new();

    public string Currency { get; set; } = null!;

    public string Timezone { get; set; } = null!;

    public string AttributionWindow { get; set; } = null!;

    public string? MetaUserId { get; set; }

    public DateTimeOffset? MetaTokenExpiresAt { get; set; }

    public string PlanCode { get; set; } = null!;

    public string PlanDisplayName { get; set; } = null!;

    public decimal PlanMonthlyPrice { get; set; }

    public string PlanCurrency { get; set; } = null!;

    public bool PlanAllowsPdfExport { get; set; }

    public bool PlanAllowsWatchlist { get; set; }

    public string SubscriptionStatus { get; set; } = null!;

    public DateTimeOffset? PlanExpiresAt { get; set; }
}
