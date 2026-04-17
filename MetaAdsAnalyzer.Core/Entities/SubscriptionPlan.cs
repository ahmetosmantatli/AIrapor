using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

/// <summary>Standart / Pro gibi satılabilir planlar. Fiyatlar yalnızca DB (veya admin API) ile güncellenir.</summary>
[Table("subscription_plans")]
public class SubscriptionPlan
{
    public int Id { get; set; }

    /// <summary>Küçük harf, sabit: standard, pro</summary>
    [Required]
    [MaxLength(32)]
    public string Code { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = null!;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyPrice { get; set; }

    [Required]
    [MaxLength(8)]
    public string Currency { get; set; } = "TRY";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Pro: analiz PDF indirme.</summary>
    public bool AllowsPdfExport { get; set; }

    /// <summary>Pro: kreatif takip listesi.</summary>
    public bool AllowsWatchlist { get; set; }

    /// <summary>Bu planda kullanıcının bağlayabileceği en fazla Meta reklam hesabı sayısı.</summary>
    public int MaxLinkedMetaAdAccounts { get; set; } = 2;

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}
