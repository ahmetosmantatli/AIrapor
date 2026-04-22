namespace MetaAdsAnalyzer.API.Services;

public sealed class MetricsRecomputeRequestDto
{
    public int UserId { get; set; }

    /// <summary>Doluysa yalnızca bu <c>ad</c> <see cref="RawInsight.EntityId"/> satırları yeniden hesaplanır.</summary>
    public List<string>? AdIds { get; set; }
}

public sealed class MetricsRecomputeResultDto
{
    public int ComputedRows { get; set; }

    public int SkippedNoCampaignMap { get; set; }

    public int SkippedNoCampaignKey { get; set; }
}
