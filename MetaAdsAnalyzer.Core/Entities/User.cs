using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MetaAdsAnalyzer.Core.Subscription;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("users")]
public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(320)]
    public string Email { get; set; } = null!;

    [MaxLength(64)]
    public string? MetaAdAccountId { get; set; }

    /// <summary>Facebook uygulamasına özel kullanıcı kimliği (OAuth).</summary>
    [MaxLength(64)]
    public string? MetaUserId { get; set; }

    /// <summary>Uzun ömürlü kullanıcı erişim jetonu. API katmanında Data Protection ile şifrelenerek saklanır.</summary>
    public string? MetaAccessToken { get; set; }

    public DateTimeOffset? MetaTokenExpiresAt { get; set; }

    [Required]
    [MaxLength(16)]
    public string Currency { get; set; } = "TRY";

    [Required]
    [MaxLength(128)]
    public string Timezone { get; set; } = "UTC";

    [Required]
    [MaxLength(64)]
    public string AttributionWindow { get; set; } = "7d_click_1d_view";

    public DateTimeOffset CreatedAt { get; set; }

    public int SubscriptionPlanId { get; set; }

    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

    /// <summary>Ödeme sağlayıcısı / manuel yönetim: active, canceled, past_due, expired, none.</summary>
    [Required]
    [MaxLength(32)]
    public string SubscriptionStatus { get; set; } = SubscriptionStatuses.Active;

    /// <summary>Faturalı dönem bitişi (UTC). Null = süre sınırı yok (geliştirme veya manuel Pro).</summary>
    public DateTimeOffset? PlanExpiresAt { get; set; }

    [MaxLength(128)]
    public string? StripeCustomerId { get; set; }

    [MaxLength(128)]
    public string? StripeSubscriptionId { get; set; }

    /// <summary>E-posta + şifre ile giriş (Meta OAuth kullanıcılarında genelde boş).</summary>
    [MaxLength(512)]
    public string? PasswordHash { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();

    public ICollection<CampaignProductMap> CampaignProductMaps { get; set; } = new List<CampaignProductMap>();

    public ICollection<RawInsight> RawInsights { get; set; } = new List<RawInsight>();

    public ICollection<Directive> Directives { get; set; } = new List<Directive>();

    public ICollection<WatchlistItem> WatchlistItems { get; set; } = new List<WatchlistItem>();

    public ICollection<UserMetaAdAccount> UserMetaAdAccounts { get; set; } = new List<UserMetaAdAccount>();
}
