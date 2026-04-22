using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

/// <summary>Reklam → Meta video_id eşlemesi (video varlık özetleri için).</summary>
[Table("ad_video_links")]
public class AdVideoLink
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string MetaAdAccountId { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string AdId { get; set; } = null!;

    [MaxLength(128)]
    public string? VideoId { get; set; }

    [MaxLength(2048)]
    public string? ThumbnailUrl { get; set; }

    [MaxLength(512)]
    public string? AdName { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
