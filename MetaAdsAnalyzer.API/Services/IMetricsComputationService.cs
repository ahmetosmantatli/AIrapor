namespace MetaAdsAnalyzer.API.Services;

public interface IMetricsComputationService
{
    Task<MetricsRecomputeResultDto> RecomputeForUserAsync(
        int userId,
        IReadOnlyList<string>? adEntityIds = null,
        CancellationToken cancellationToken = default);

    Task<bool> RecomputeRawInsightAsync(int rawInsightId, CancellationToken cancellationToken = default);
}
