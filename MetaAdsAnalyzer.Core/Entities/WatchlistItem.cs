using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("watchlist_items")]
public class WatchlistItem
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>campaign, adset veya ad</summary>
    [Required]
    [MaxLength(16)]
    public string Level { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string EntityId { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
