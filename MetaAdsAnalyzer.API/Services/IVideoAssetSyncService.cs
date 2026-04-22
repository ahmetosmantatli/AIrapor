namespace MetaAdsAnalyzer.API.Services;

public interface IVideoAssetSyncService
{
    /// <summary>Reklam listesinden bağlantıları yazar ve <c>video_assets</c> özetlerini günceller.</summary>
    Task SyncFromAdListAsync(
        int userId,
        string metaAdAccountId,
        IReadOnlyList<MetaAdListItemDto> ads,
        CancellationToken cancellationToken = default);
}
