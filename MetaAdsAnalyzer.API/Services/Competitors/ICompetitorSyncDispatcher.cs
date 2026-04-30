namespace MetaAdsAnalyzer.API.Services.Competitors;

public interface ICompetitorSyncDispatcher
{
    void TriggerInitialSync(int trackedCompetitorId);
}
