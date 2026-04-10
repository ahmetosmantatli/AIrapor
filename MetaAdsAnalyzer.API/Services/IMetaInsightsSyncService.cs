namespace MetaAdsAnalyzer.API.Services;

public interface IMetaInsightsSyncService
{
    Task<InsightsSyncResponseDto> SyncInsightsAsync(
        int userId,
        string level,
        string datePreset,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetaAdAccountItemDto>> ListAdAccountsAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
