using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("tracked_competitors")]
public class TrackedCompetitor
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    [Required]
    [MaxLength(160)]
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Kullanıcının verdiği page adı veya id girişi (ham referans).
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string PageRef { get; set; } = null!;

    /// <summary>
    /// Biliniyorsa net Facebook sayfa id.
    /// </summary>
    [MaxLength(64)]
    public string? PageId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    [MaxLength(32)]
    public string LastSyncStatus { get; set; } = "pending";

    [MaxLength(500)]
    public string? LastSyncError { get; set; }

    public ICollection<CompetitorAd> Ads { get; set; } = new List<CompetitorAd>();
}
