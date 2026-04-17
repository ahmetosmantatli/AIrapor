using MetaAdsAnalyzer.Core.Subscription;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Extensions;

public sealed class PlanEntitlements
{
    public bool AllowsPdfExport { get; init; }

    public bool AllowsWatchlist { get; init; }
}

public static class AppDbContextPlanExtensions
{
    public static async Task<PlanEntitlements?> GetPlanEntitlementsForUserAsync(
        this AppDbContext db,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(
                u => new
                {
                    u.SubscriptionPlan.AllowsPdfExport,
                    u.SubscriptionPlan.AllowsWatchlist,
                    u.SubscriptionStatus,
                    u.PlanExpiresAt,
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var grants = SubscriptionAccess.GrantsPlanFeatures(
            row.SubscriptionStatus,
            row.PlanExpiresAt,
            DateTimeOffset.UtcNow);
        return new PlanEntitlements
        {
            AllowsPdfExport = grants && row.AllowsPdfExport,
            AllowsWatchlist = grants && row.AllowsWatchlist,
        };
    }
}
