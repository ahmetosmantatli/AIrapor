namespace MetaAdsAnalyzer.API.Services;

public interface IPdfReportService
{
    Task<byte[]> BuildAnalysisReportAsync(int userId, CancellationToken cancellationToken = default);

    Task<byte[]> BuildVideoReportPdfAsync(
        int userId,
        string? metaAdAccountId,
        IReadOnlyList<string> adIds,
        string? videoId,
        string? displayName,
        CancellationToken cancellationToken = default);
}
