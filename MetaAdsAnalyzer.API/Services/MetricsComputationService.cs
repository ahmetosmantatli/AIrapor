using MetaAdsAnalyzer.API.Extensions;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Services;

public sealed class MetricsComputationService : IMetricsComputationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<MetricsComputationService> _logger;

    public MetricsComputationService(AppDbContext db, ILogger<MetricsComputationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<MetricsRecomputeResultDto> RecomputeForUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var result = new MetricsRecomputeResultDto();
        var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (!userExists)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var activeMeta = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.MetaAdAccountId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var rawIds = await _db.RawInsights.AsNoTracking()
            .ForUserActiveAdAccount(userId, activeMeta)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rawIds.Count > 0)
        {
            await _db.ComputedMetrics
                .Where(c => rawIds.Contains(c.RawInsightId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var id in rawIds)
        {
            var ok = await TryAddComputedAsync(id, userId, result, cancellationToken).ConfigureAwait(false);
            if (ok)
            {
                result.ComputedRows++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<bool> RecomputeRawInsightAsync(int rawInsightId, CancellationToken cancellationToken = default)
    {
        var raw = await _db.RawInsights.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rawInsightId, cancellationToken)
            .ConfigureAwait(false);
        if (raw is null)
        {
            return false;
        }

        await _db.ComputedMetrics
            .Where(c => c.RawInsightId == rawInsightId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var agg = new MetricsRecomputeResultDto();
        var ok = await TryAddComputedAsync(rawInsightId, raw.UserId, agg, cancellationToken).ConfigureAwait(false);
        if (ok)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ok;
    }

    private async Task<bool> TryAddComputedAsync(
        int rawInsightId,
        int userId,
        MetricsRecomputeResultDto aggregate,
        CancellationToken cancellationToken)
    {
        var raw = await _db.RawInsights.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rawInsightId && r.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (raw is null)
        {
            return false;
        }

        var campaignKey = ResolveCampaignKey(raw);
        if (string.IsNullOrWhiteSpace(campaignKey))
        {
            aggregate.SkippedNoCampaignKey++;
            return false;
        }

        var map = await _db.CampaignProductMaps.AsNoTracking()
            .Include(m => m.Product)
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.CampaignId == campaignKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (map?.Product is null)
        {
            aggregate.SkippedNoCampaignMap++;
            _logger.LogDebug(
                "Hesap atlandı RawInsight {Id}: kampanya {Campaign} için ürün eşlemesi yok.",
                rawInsightId,
                campaignKey);
            return false;
        }

        var p = map.Product;
        var cm = ProfitMath.ContributionMargin(
            p.SellingPrice,
            p.Cogs,
            p.ShippingCost,
            p.PaymentFeePct,
            p.ReturnRatePct);

        var roas = ProfitMath.Roas(raw.PurchaseValue, p.LtvMultiplier, raw.Spend);
        var cpa = ProfitMath.Cpa(raw.Spend, raw.Purchases);
        var breakEvenRoas = ProfitMath.BreakEvenRoas(p.SellingPrice, cm);
        var targetRoas = ProfitMath.TargetRoas(p.SellingPrice, cm, p.TargetMarginPct);
        var maxCpa = ProfitMath.MaxCpa(cm);
        var targetCpa = ProfitMath.TargetCpa(cm, p.SellingPrice, p.TargetMarginPct);
        var netPerOrder = ProfitMath.NetProfitPerOrder(cm, cpa);
        var netMarginPct = ProfitMath.NetMarginPct(netPerOrder, p.SellingPrice);
        var hook = ProfitMath.HookRatePct(raw.Impressions, raw.VideoPlay3s);
        var hold = ProfitMath.HoldRatePct(raw.VideoPlay3s, raw.VideoThruplay);
        var mismatch = ProfitMath.MismatchRatio(raw.CtrAll, raw.CtrLink);

        _db.ComputedMetrics.Add(
            new ComputedMetric
            {
                RawInsightId = rawInsightId,
                Roas = roas,
                Cpa = cpa,
                BreakEvenRoas = breakEvenRoas,
                TargetRoas = targetRoas,
                MaxCpa = maxCpa,
                TargetCpa = targetCpa,
                HookRate = hook,
                HoldRate = hold,
                NetProfitPerOrder = netPerOrder,
                NetMarginPct = netMarginPct,
                MismatchRatio = mismatch,
                ComputedAt = DateTimeOffset.UtcNow,
            });

        return true;
    }

    private static string? ResolveCampaignKey(RawInsight raw)
    {
        if (!string.IsNullOrWhiteSpace(raw.MetaCampaignId))
        {
            return raw.MetaCampaignId;
        }

        return string.Equals(raw.Level, "campaign", StringComparison.OrdinalIgnoreCase)
            ? raw.EntityId
            : null;
    }
}
