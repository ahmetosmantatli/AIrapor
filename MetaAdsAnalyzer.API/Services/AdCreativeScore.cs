using MetaAdsAnalyzer.Core.Entities;

namespace MetaAdsAnalyzer.API.Services;

public static class AdCreativeScore
{
    public static (int Score, string Health) Compute(RawInsight raw, ComputedMetric m)
    {
        var hook = ScoreFromThumbstop(m.ThumbstopRatePct ?? m.HookRate);
        var hold = ScoreFromHold(m.HoldRate);
        var completion = ScoreFromCompletion(m.CompletionRatePct);
        var roasPart = ScoreFromConversion(m.Roas, m.TargetRoas, m.BreakEvenRoas, raw);

        var total = (int)Math.Round(
            hook * 0.3m + hold * 0.25m + completion * 0.15m + roasPart * 0.3m,
            MidpointRounding.AwayFromZero);
        total = Math.Clamp(total, 0, 100);

        var health = ClassifyHealth(total, m, raw);
        return (total, health);
    }

    private static decimal ScoreFromThumbstop(decimal? thumbOrHookPct)
    {
        if (thumbOrHookPct is null)
        {
            return 50m;
        }

        var h = thumbOrHookPct.Value;
        if (h >= DirectiveThresholds.HookGoodPct)
        {
            return 100m;
        }

        if (h >= DirectiveThresholds.HookPoorPct)
        {
            return 60m;
        }

        return 25m;
    }

    private static decimal ScoreFromHold(decimal? holdPct)
    {
        if (holdPct is null)
        {
            return 50m;
        }

        var h = holdPct.Value;
        if (h >= 40m)
        {
            return 100m;
        }

        if (h >= DirectiveThresholds.HoldPoorPct)
        {
            return 65m;
        }

        return 25m;
    }

    private static decimal ScoreFromCompletion(decimal? completionPct)
    {
        if (completionPct is null)
        {
            return 50m;
        }

        var c = completionPct.Value;
        if (c >= 15m)
        {
            return 100m;
        }

        if (c >= 5m)
        {
            return 65m;
        }

        return 25m;
    }

    private static decimal ScoreFromConversion(
        decimal? roas,
        decimal? targetRoas,
        decimal? breakEvenRoas,
        RawInsight raw)
    {
        if (raw.Purchases < DirectiveThresholds.MinPurchasesForRoasDecision)
        {
            return 45m;
        }

        if (roas is null || targetRoas is null || targetRoas <= 0)
        {
            return 40m;
        }

        var ratioToTarget = roas.Value / targetRoas.Value;
        if (ratioToTarget >= 1m)
        {
            return 100m;
        }

        if (breakEvenRoas is > 0 && roas < breakEvenRoas.Value)
        {
            return 20m;
        }

        if (breakEvenRoas is > 0 && targetRoas.Value > breakEvenRoas.Value)
        {
            var span = targetRoas.Value - breakEvenRoas.Value;
            var t = Math.Clamp((roas.Value - breakEvenRoas.Value) / span, 0m, 1m);
            return 40m + 45m * t;
        }

        return Math.Clamp(55m * ratioToTarget, 10m, 90m);
    }

    private static string ClassifyHealth(int score, ComputedMetric m, RawInsight raw)
    {
        if (score < 30 || (m.Roas is not null && m.BreakEvenRoas is not null && m.Roas < m.BreakEvenRoas
                                                      && raw.Purchases >= DirectiveThresholds.MinPurchasesForStop))
        {
            return "Durdur";
        }

        if (m.NetMarginPct is < 0 || (m.Roas is not null && m.BreakEvenRoas is not null && m.Roas < m.BreakEvenRoas))
        {
            return "Zarar";
        }

        if (m.Roas is not null && m.TargetRoas is not null && m.Roas >= m.TargetRoas)
        {
            return "Karlı";
        }

        return "Test";
    }
}
