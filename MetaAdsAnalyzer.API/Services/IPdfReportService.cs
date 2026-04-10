namespace MetaAdsAnalyzer.API.Services;

public interface IPdfReportService
{
    Task<byte[]> BuildAnalysisReportAsync(int userId, CancellationToken cancellationToken = default);
}
