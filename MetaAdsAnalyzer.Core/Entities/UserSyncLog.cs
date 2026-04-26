using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("user_sync_log")]
public class UserSyncLog
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public DateOnly Date { get; set; }

    public int SyncCount { get; set; }

    [MaxLength(64)]
    public string? MetaAdAccountId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
