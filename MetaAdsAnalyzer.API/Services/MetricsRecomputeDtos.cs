namespace MetaAdsAnalyzer.API.Services;

public sealed class MetricsRecomputeRequestDto
{
    public int UserId { get; set; }
}

public sealed class MetricsRecomputeResultDto
{
    public int ComputedRows { get; set; }

    public int SkippedNoCampaignMap { get; set; }

    public int SkippedNoCampaignKey { get; set; }
}
