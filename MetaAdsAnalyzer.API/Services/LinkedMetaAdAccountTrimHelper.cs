using MetaAdsAnalyzer.Core;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Services;

/// <summary>Plan düşürüldüğünde fazla bağlı reklam hesaplarını kaldırır; aktif hesap silinirse sıfırlanır.</summary>
public static class LinkedMetaAdAccountTrimHelper
{
    public static async Task EnforcePlanLimitAsync(AppDbContext db, int userId, CancellationToken cancellationToken)
    {
        var max = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.SubscriptionPlan.MaxLinkedMetaAdAccounts)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (max <= 0)
        {
            return;
        }

        var victims = await db.UserMetaAdAccounts
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.LinkedAt)
            .Skip(max)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (victims.Count > 0)
        {
            db.UserMetaAdAccounts.RemoveRange(victims);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        var remaining = await db.UserMetaAdAccounts.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.MetaAdAccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (remaining.Count == 0)
        {
            user.MetaAdAccountId = null;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var activeNorm = MetaAdAccountIdNormalizer.Normalize(user.MetaAdAccountId);
        if (string.IsNullOrEmpty(activeNorm) || !remaining.Contains(activeNorm, StringComparer.Ordinal))
        {
            user.MetaAdAccountId = remaining[0];
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
