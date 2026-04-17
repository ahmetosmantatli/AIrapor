using System.ComponentModel.DataAnnotations;

namespace MetaAdsAnalyzer.API.Models;

public sealed class UserMetaAdAccountItemDto
{
    public int Id { get; set; }

    public string MetaAdAccountId { get; set; } = null!;

    public string? DisplayName { get; set; }

    public DateTimeOffset LinkedAt { get; set; }
}

public sealed class LinkUserMetaAdAccountRequestDto
{
    [Required]
    [MaxLength(64)]
    public string MetaAdAccountId { get; set; } = null!;

    [MaxLength(512)]
    public string? DisplayName { get; set; }
}

public sealed class SelectActiveMetaAdAccountRequestDto
{
    [Required]
    [MaxLength(64)]
    public string MetaAdAccountId { get; set; } = null!;
}
