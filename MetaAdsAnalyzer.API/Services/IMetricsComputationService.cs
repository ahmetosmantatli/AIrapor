namespace MetaAdsAnalyzer.API.Services;

public interface IMetricsComputationService
{
    Task<MetricsRecomputeResultDto> RecomputeForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> RecomputeRawInsightAsync(int rawInsightId, CancellationToken cancellationToken = default);
}
