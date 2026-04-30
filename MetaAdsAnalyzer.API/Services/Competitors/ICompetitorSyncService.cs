using MetaAdsAnalyzer.API.Models;

namespace MetaAdsAnalyzer.API.Services.Competitors;

public interface ICompetitorSyncService
{
    Task<SyncCompetitorResultDto> SyncCompetitorAsync(int trackedCompetitorId, CancellationToken cancellationToken);
}
