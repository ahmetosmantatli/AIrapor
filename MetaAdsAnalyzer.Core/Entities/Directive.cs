using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("directives")]
public class Directive
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string EntityId { get; set; } = null!;

    [Required]
    [MaxLength(32)]
    public string EntityType { get; set; } = null!;

    [Required]
    [MaxLength(32)]
    public string DirectiveType { get; set; } = null!;

    [Required]
    [MaxLength(32)]
    public string Severity { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = null!;

    /// <summary>0–100; çoğunlukla reklam (ad) seviyesi.</summary>
    public int? Score { get; set; }

    /// <summary>Örn. Karlı, Test, Zarar, Durdur (reklam özeti).</summary>
    [MaxLength(32)]
    public string? HealthStatus { get; set; }

    public DateTimeOffset TriggeredAt { get; set; }

    public bool IsActive { get; set; } = true;
}
