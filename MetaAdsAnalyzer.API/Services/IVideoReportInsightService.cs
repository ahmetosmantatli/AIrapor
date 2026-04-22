using MetaAdsAnalyzer.API.Models;

namespace MetaAdsAnalyzer.API.Services;

public interface IVideoReportInsightService
{
    Task<VideoReportAggregateResponseDto> BuildAggregateAsync(
        int userId,
        string? metaAdAccountId,
        IReadOnlyList<string> adIds,
        CancellationToken cancellationToken = default);
}
