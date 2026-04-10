using System.ComponentModel.DataAnnotations;

namespace MetaAdsAnalyzer.API.Models;

public sealed class UserSettingsPatchDto
{
    [MaxLength(64)]
    public string? MetaAdAccountId { get; set; }

    [MaxLength(16)]
    public string? Currency { get; set; }

    [MaxLength(128)]
    public string? Timezone { get; set; }

    [MaxLength(64)]
    public string? AttributionWindow { get; set; }
}
