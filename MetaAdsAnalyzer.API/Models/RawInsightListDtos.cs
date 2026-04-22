namespace MetaAdsAnalyzer.API.Models;

public sealed class RawInsightListRowDto
{
    public int Id { get; set; }

    public string Level { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public string? EntityName { get; set; }

    public string? MetaCampaignId { get; set; }

    public DateOnly DateStart { get; set; }

    public DateOnly DateStop { get; set; }

    public DateTimeOffset FetchedAt { get; set; }

    public decimal Spend { get; set; }

    public long Impressions { get; set; }

    public long Reach { get; set; }

    public long LinkClicks { get; set; }

    public long VideoPlay3s { get; set; }

    public long Video15Sec { get; set; }

    public long VideoP100 { get; set; }

    public long Purchases { get; set; }

    public decimal PurchaseValue { get; set; }

    public decimal? Roas { get; set; }

    public decimal? Cpa { get; set; }

    public decimal? ThumbstopRatePct { get; set; }

    public decimal? HoldRatePct { get; set; }

    public decimal? CompletionRatePct { get; set; }

    public int? CreativeScoreTotal { get; set; }

    public int? ComputedMetricId { get; set; }
}
