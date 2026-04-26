namespace MetaAdsAnalyzer.API.Services;

public interface IMetaInsightsSyncService
{
    Task<InsightsSyncResponseDto> SyncInsightsAsync(
        int userId,
        string level,
        string datePreset,
        string? adId = null,
        string? metaAdAccountId = null,
        IReadOnlyList<string>? adIds = null,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetaAdAccountItemDto>> ListAdAccountsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary><c>me/adaccounts</c> ile Graph’tan çekip <c>user_meta_ad_accounts</c> tablosuna yazar.</summary>
    Task<int> SyncLinkedAdAccountsFromGraphAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetaAdListItemDto>> ListAccountAdsAsync(
        int userId,
        string? metaAdAccountId = null,
        string? campaignId = null,
        string? adsetId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetaCampaignListItemDto>> ListCampaignsAsync(
        int userId,
        string? metaAdAccountId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetaAdsetListItemDto>> ListAdsetsAsync(
        int userId,
        string campaignId,
        string? metaAdAccountId = null,
        CancellationToken cancellationToken = default);
}
