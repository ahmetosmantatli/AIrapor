namespace MetaAdsAnalyzer.API.Services.Competitors;

public sealed class CompetitorSyncDispatcher : ICompetitorSyncDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CompetitorSyncDispatcher> _logger;

    public CompetitorSyncDispatcher(IServiceScopeFactory scopeFactory, ILogger<CompetitorSyncDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void TriggerInitialSync(int trackedCompetitorId)
    {
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var syncService = scope.ServiceProvider.GetRequiredService<ICompetitorSyncService>();
                    await syncService.SyncCompetitorAsync(trackedCompetitorId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Immediate competitor sync failed competitorId={CompetitorId}", trackedCompetitorId);
                }
            });
    }
}
