using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("user_meta_ad_accounts")]
public class UserMetaAdAccount
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Graph biçimi: act_…</summary>
    [Required]
    [MaxLength(64)]
    public string MetaAdAccountId { get; set; } = null!;

    [MaxLength(512)]
    public string? DisplayName { get; set; }

    public DateTimeOffset LinkedAt { get; set; }
}
