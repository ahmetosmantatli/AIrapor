using MetaAdsAnalyzer.Core.Entities;

namespace MetaAdsAnalyzer.API.Services;

public static class AdCreativeScore
{
    public static (int Score, string Health) Compute(RawInsight raw, ComputedMetric m)
    {
        var hook = ScoreFromHook(m.HookRate);
        var conv = ScoreFromConversion(m.Roas, m.TargetRoas, m.BreakEvenRoas, raw);
        var profit = ScoreFromNetMargin(m.NetMarginPct);

        var total = (int)Math.Round(hook * 0.3m + conv * 0.4m + profit * 0.3m, MidpointRounding.AwayFromZero);
        total = Math.Clamp(total, 0, 100);

        var health = ClassifyHealth(total, m, raw);
        return (total, health);
    }

    private static decimal ScoreFromHook(decimal? hookRatePct)
    {
        if (hookRatePct is null)
        {
            return 50m;
        }

        var h = hookRatePct.Value;
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

    private static decimal ScoreFromNetMargin(decimal? netMarginPct)
    {
        if (netMarginPct is null)
        {
            return 45m;
        }

        var n = netMarginPct.Value;
        if (n >= 15m)
        {
            return 100m;
        }

        if (n >= 5m)
        {
            return 70m;
        }

        if (n >= 0m)
        {
            return 45m;
        }

        return 15m;
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
