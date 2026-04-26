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
        IReadOnlyList<string>? adEntityIds = null,
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

        var rawQ = _db.RawInsights.AsNoTracking().ForUserActiveAdAccount(userId, activeMeta);
        if (adEntityIds is { Count: > 0 })
        {
            var set = adEntityIds.ToHashSet(StringComparer.Ordinal);
            rawQ = rawQ.Where(r => r.Level == "ad" && set.Contains(r.EntityId));
        }

        var rawIds = await rawQ.Select(r => r.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

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
            var hookFallback = ProfitMath.HookRatePct(raw.Impressions, raw.VideoPlay3s);
            var thumbFallback = ProfitMath.ThumbstopRatePct(raw.Reach, raw.VideoPlay3s);
            var holdFallback = ProfitMath.HoldRatePct(raw.VideoPlay3s, raw.VideoThruplay);
            var completionFallback = ProfitMath.CompletionRatePct(raw.Impressions, raw.VideoP100);
            var v3f = ProfitMath.VideoViewsPerSpend(raw.VideoPlay3s, raw.Spend);
            var v15f = ProfitMath.VideoViewsPerSpend(raw.Video15Sec, raw.Spend);
            var mismatchFallback = ProfitMath.MismatchRatio(raw.CtrAll, raw.CtrLink);
            var roasFallback = ProfitMath.Roas(raw.PurchaseValue, 1m, raw.Spend);
            var cpaFallback = ProfitMath.Cpa(raw.Spend, raw.Purchases);

            var cm0 = new ComputedMetric
            {
                RawInsightId = rawInsightId,
                Roas = roasFallback,
                Cpa = cpaFallback,
                BreakEvenRoas = null,
                TargetRoas = null,
                MaxCpa = null,
                TargetCpa = null,
                HookRate = hookFallback,
                ThumbstopRatePct = thumbFallback,
                HoldRate = holdFallback,
                CompletionRatePct = completionFallback,
                Video3SecPerSpend = v3f,
                Video15SecPerSpend = v15f,
                NetProfitPerOrder = null,
                NetMarginPct = null,
                MismatchRatio = mismatchFallback,
                ComputedAt = DateTimeOffset.UtcNow,
            };
            cm0.IsVideoCreative = await ResolveIsVideoCreativeAsync(raw, cancellationToken).ConfigureAwait(false);
            var score0 = AdCreativeScore.Compute(raw, cm0);
            cm0.CreativeScoreTotal = score0.Score;
            cm0.CreativeScoreLabel = score0.Label;
            cm0.CreativeScoreColor = score0.Color;
            cm0.VideoMetricsUnavailable = score0.VideoMetricsUnavailable;
            _db.ComputedMetrics.Add(cm0);

            return true;
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
        var thumb = ProfitMath.ThumbstopRatePct(raw.Reach, raw.VideoPlay3s);
        var hold = ProfitMath.HoldRatePct(raw.VideoPlay3s, raw.VideoThruplay);
        var completion = ProfitMath.CompletionRatePct(raw.Impressions, raw.VideoP100);
        var v3 = ProfitMath.VideoViewsPerSpend(raw.VideoPlay3s, raw.Spend);
        var v15 = ProfitMath.VideoViewsPerSpend(raw.Video15Sec, raw.Spend);
        var mismatch = ProfitMath.MismatchRatio(raw.CtrAll, raw.CtrLink);

        var cm1 = new ComputedMetric
        {
            RawInsightId = rawInsightId,
            Roas = roas,
            Cpa = cpa,
            BreakEvenRoas = breakEvenRoas,
            TargetRoas = targetRoas,
            MaxCpa = maxCpa,
            TargetCpa = targetCpa,
            HookRate = hook,
            ThumbstopRatePct = thumb,
            HoldRate = hold,
            CompletionRatePct = completion,
            Video3SecPerSpend = v3,
            Video15SecPerSpend = v15,
            NetProfitPerOrder = netPerOrder,
            NetMarginPct = netMarginPct,
            MismatchRatio = mismatch,
            ComputedAt = DateTimeOffset.UtcNow,
        };
        cm1.IsVideoCreative = await ResolveIsVideoCreativeAsync(raw, cancellationToken).ConfigureAwait(false);
        var score1 = AdCreativeScore.Compute(raw, cm1);
        cm1.CreativeScoreTotal = score1.Score;
        cm1.CreativeScoreLabel = score1.Label;
        cm1.CreativeScoreColor = score1.Color;
        cm1.VideoMetricsUnavailable = score1.VideoMetricsUnavailable;
        _db.ComputedMetrics.Add(cm1);

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

    private async Task<bool> ResolveIsVideoCreativeAsync(RawInsight raw, CancellationToken cancellationToken)
    {
        if (!string.Equals(raw.Level, "ad", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var act = raw.MetaAdAccountId;
        if (string.IsNullOrWhiteSpace(act))
        {
            return false;
        }

        return await _db.AdVideoLinks.AsNoTracking()
            .AnyAsync(
                x => x.UserId == raw.UserId
                     && x.AdId == raw.EntityId
                     && x.MetaAdAccountId == act
                     && x.VideoId != null
                     && x.VideoId != "",
                cancellationToken)
            .ConfigureAwait(false);
    }
}
