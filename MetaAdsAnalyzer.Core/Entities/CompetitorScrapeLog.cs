using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaAdsAnalyzer.Core.Entities;

[Table("competitor_scrape_log")]
public class CompetitorScrapeLog
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public int TrackedCompetitorId { get; set; }

    public TrackedCompetitor TrackedCompetitor { get; set; } = null!;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public int FetchedCount { get; set; }

    public int InsertedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int ClosedCount { get; set; }

    [Required]
    [MaxLength(32)]
    public string Status { get; set; } = "running";

    [MaxLength(1000)]
    public string? Error { get; set; }
}
