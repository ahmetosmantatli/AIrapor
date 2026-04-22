using MetaAdsAnalyzer.Core;
using MetaAdsAnalyzer.Core.Entities;

namespace MetaAdsAnalyzer.API.Extensions;

public static class RawInsightQueryableExtensions
{
    /// <summary>Seçili reklam hesabına ait ham insight satırları (hesap seçili değilse yalnızca eski NULL kayıtlar).</summary>
    public static IQueryable<RawInsight> ForUserActiveAdAccount(
        this IQueryable<RawInsight> query,
        int userId,
        string? userMetaAdAccountIdRaw)
    {
        var act = MetaAdAccountIdNormalizer.Normalize(userMetaAdAccountIdRaw);
        var q = query.Where(r => r.UserId == userId);
        if (string.IsNullOrEmpty(act))
        {
            return q.Where(r => r.MetaAdAccountId == null);
        }

        return q.Where(r => r.MetaAdAccountId != null && r.MetaAdAccountId.ToLower() == act.ToLower());
    }
}
