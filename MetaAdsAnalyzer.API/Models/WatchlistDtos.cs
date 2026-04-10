using System.ComponentModel.DataAnnotations;

namespace MetaAdsAnalyzer.API.Models;

public sealed class WatchlistItemResponseDto
{
    public int Id { get; set; }

    public string Level { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AddWatchlistItemRequestDto
{
    [Required]
    [RegularExpression("^(campaign|adset|ad)$", ErrorMessage = "level: campaign, adset veya ad olmalıdır.")]
    public string Level { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string EntityId { get; set; } = null!;
}

public sealed class WatchlistToggleResponseDto
{
    public bool IsWatching { get; set; }

    public int? WatchlistItemId { get; set; }
}
