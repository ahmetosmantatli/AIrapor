using MetaAdsAnalyzer.Core.Entities;

namespace MetaAdsAnalyzer.API.Services;

public static class AdCreativeScore
{
    public static (int Score, string Health, string Label, string Color, bool VideoMetricsUnavailable) Compute(
        RawInsight raw,
        ComputedMetric m)
    {
        if (m.IsVideoCreative)
        {
            return ComputeVideo(raw, m);
        }

        return ComputeStatic(raw, m);
    }

    private static (int Score, string Health, string Label, string Color, bool VideoMetricsUnavailable) ComputeVideo(
        RawInsight raw,
        ComputedMetric m)
    {
        var missingVideoMetrics = raw.VideoPlay3s <= 0;

        var hook = missingVideoMetrics ? 50m : ScoreHook(m.ThumbstopRatePct ?? m.HookRate);
        var hold = missingVideoMetrics ? 50m : ScoreHold(m.HoldRate);
        var clickQuality = ScoreClickQuality(raw, m);
        var conversion = ScoreConversion(raw, m);

        var total = (int)Math.Round(
            hook * 0.25m + hold * 0.20m + clickQuality * 0.30m + conversion * 0.25m,
            MidpointRounding.AwayFromZero);
        total = Math.Clamp(total, 0, 100);

        var (label, color) = LabelForScore(total);
        var health = HealthFromLabel(label);
        return (total, health, label, color, missingVideoMetrics);
    }

    private static (int Score, string Health, string Label, string Color, bool VideoMetricsUnavailable) ComputeStatic(
        RawInsight raw,
        ComputedMetric m)
    {
        var attention = ScoreClickQuality(raw, m);
        var audience = ScoreAudience(raw);
        var conversion = ScoreConversion(raw, m);

        var total = (int)Math.Round(
            attention * 0.35m + audience * 0.25m + conversion * 0.40m,
            MidpointRounding.AwayFromZero);
        total = Math.Clamp(total, 0, 100);

        var (label, color) = LabelForScore(total);
        var health = HealthFromLabel(label);
        return (total, health, label, color, false);
    }

    private static decimal ScoreHook(decimal? pct)
    {
        if (pct is null) return 50m;
        if (pct > 40m) return 100m;
        if (pct >= 30m) return 80m;
        if (pct >= 20m) return 60m;
        if (pct >= 10m) return 35m;
        return 10m;
    }

    private static decimal ScoreHold(decimal? pct)
    {
        if (pct is null) return 50m;
        if (pct > 35m) return 100m;
        if (pct >= 25m) return 80m;
        if (pct >= 15m) return 55m;
        if (pct >= 8m) return 30m;
        return 10m;
    }

    private static decimal ScoreClickQuality(RawInsight raw, ComputedMetric m)
    {
        var ctr = raw.CtrLink;
        decimal baseScore;
        if (ctr > 3m) baseScore = 100m;
        else if (ctr >= 2m) baseScore = 80m;
        else if (ctr >= 1m) baseScore = 55m;
        else if (ctr >= 0.5m) baseScore = 30m;
        else baseScore = 10m;

        if (m.MismatchRatio is > 3m)
        {
            baseScore -= 15m;
        }

        return Math.Clamp(baseScore, 0m, 100m);
    }

    private static decimal ScoreConversion(RawInsight raw, ComputedMetric m)
    {
        if (m.TargetCpa is > 0m && m.Cpa is > 0m)
        {
            var cpaScore = (m.TargetCpa.Value / m.Cpa.Value) * 100m;
            return Math.Clamp(cpaScore, 0m, 100m);
        }

        if (raw.LinkClicks <= 0)
        {
            return 10m;
        }

        var cvr = (decimal)raw.Purchases / raw.LinkClicks * 100m;
        if (cvr > 4m) return 100m;
        if (cvr >= 2m) return 80m;
        if (cvr >= 1m) return 55m;
        if (cvr >= 0.5m) return 30m;
        return 10m;
    }

    private static decimal ScoreAudience(RawInsight raw)
    {
        var highCpm = raw.Cpm > 50m;
        var highCtr = raw.CtrLink >= 2m;
        decimal score = (highCpm, highCtr) switch
        {
            (false, true) => 100m,
            (false, false) => 50m,
            (true, true) => 70m,
            (true, false) => 20m,
        };
        if (raw.Frequency > 3.5m)
        {
            score -= 20m;
        }

        return Math.Clamp(score, 0m, 100m);
    }

    private static (string Label, string Color) LabelForScore(int score)
    {
        if (score >= 80) return ("Winner", "green");
        if (score >= 60) return ("Potansiyel", "blue");
        if (score >= 40) return ("Zayıf", "orange");
        return ("Kapat", "red");
    }

    private static string HealthFromLabel(string label) =>
        label switch
        {
            "Winner" => "Karlı",
            "Potansiyel" => "Test",
            "Zayıf" => "Zarar",
            _ => "Durdur",
        };
}
