using MetaAdsAnalyzer.Core.Entities;

namespace MetaAdsAnalyzer.API.Services;

internal static class DirectiveRules
{
    public static int ReportingDaySpan(RawInsight raw) =>
        Math.Max(1, raw.DateStop.DayNumber - raw.DateStart.DayNumber + 1);

    public static decimal? LinkCvrPct(RawInsight raw) =>
        raw.LinkClicks <= 0 ? null : (decimal)raw.Purchases / raw.LinkClicks * 100m;

    public static IReadOnlyList<Directive> EvaluateCampaign(int userId, RawInsight raw, ComputedMetric m, int? score, string? health)
    {
        var list = new List<Directive>();
        var days = ReportingDaySpan(raw);

        if (raw.Impressions < DirectiveThresholds.MinImpressionsForConfidence)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "campaign",
                    "WATCH",
                    "info",
                    "Düşük örneklem: gösterim < 1000; ROAS ve CPA yorumlarını dikkatle kullanın.",
                    score,
                    health));
        }

        if (raw.Purchases < DirectiveThresholds.MinPurchasesForRoasDecision
            && raw.Impressions >= DirectiveThresholds.MinImpressionsForConfidence)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "campaign",
                    "WATCH",
                    "warning",
                    "Satın alma sayısı 5’ten az; ROAS hedefi için henüz kesin karar vermeyin.",
                    score,
                    health));
        }

        if (days < DirectiveThresholds.MaxDaysWait
            && raw.Purchases < DirectiveThresholds.MinPurchasesForRoasDecision
            && raw.Spend < DirectiveThresholds.MinSpendLoose)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "campaign",
                    "WATCH",
                    "info",
                    "Veri olgunlaşana kadar bekleyin (düşük harcama, az gün ve az dönüşüm).",
                    score,
                    health));
        }

        if (m.Roas is not null
            && m.TargetRoas is not null
            && m.Roas >= m.TargetRoas
            && raw.Spend >= DirectiveThresholds.MinSpendScale
            && days >= DirectiveThresholds.MinDaysForScale)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "campaign",
                    "SCALE",
                    "info",
                    "ROAS hedefin üzerinde, yeterli harcama ve süre: ölçeklemeyi değerlendir (kademeli bütçe).",
                    score,
                    health));
        }

        if (m.BreakEvenRoas is not null
            && m.Roas is not null
            && m.TargetRoas is not null
            && m.Roas > m.BreakEvenRoas
            && m.Roas < m.TargetRoas
            && raw.Purchases >= DirectiveThresholds.MinPurchasesForRoasDecision)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "campaign",
                    "OPTIMIZE",
                    "warning",
                    "Kâr eşiğinin üzerindesin ancak hedef ROAS altında: kreatif ve teklif optimizasyonu.",
                    score,
                    health));
        }

        if (m.BreakEvenRoas is not null
            && m.Roas is not null
            && m.Roas < m.BreakEvenRoas
            && raw.Purchases >= DirectiveThresholds.MinPurchasesForStop
            && days >= DirectiveThresholds.MinDaysForStop)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "campaign",
                    "STOP",
                    "critical",
                    "ROAS kâr eşiğinin altında ve yeterli dönüşüm/süre var: kampanyayı durdurmayı değerlendir.",
                    score,
                    health));
        }

        return Dedupe(list);
    }

    public static IReadOnlyList<Directive> EvaluateAdset(int userId, RawInsight raw, ComputedMetric m, int? score, string? health)
    {
        var list = new List<Directive>();
        var cvr = LinkCvrPct(raw);

        if (m.TargetCpa is not null && m.Cpa is not null && m.Cpa < m.TargetCpa && raw.Spend >= DirectiveThresholds.MinSpendScale)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "adset",
                    "SCALE",
                    "info",
                    "CPA hedefin altında ve yeterli harcama: kazanan sete bütçe aktarmayı değerlendir.",
                    score,
                    health));
        }

        if (m.MaxCpa is not null && m.Cpa is not null && m.Cpa > m.MaxCpa && raw.Purchases >= DirectiveThresholds.MinPurchasesForAdsetStop)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "adset",
                    "STOP",
                    "critical",
                    "CPA üst güvenlik marjını aştı (10+ dönüşüm): seti kapatmayı değerlendir.",
                    score,
                    health));
        }

        if (raw.Frequency > DirectiveThresholds.FrequencyFatigue)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "adset",
                    "OPTIMIZE",
                    "warning",
                    "Frekans yüksek (3.5+): kreatif yorgunluğu; yeni kreatif veya hedef kısma.",
                    score,
                    health));
        }

        if (raw.CtrLink < DirectiveThresholds.CtrLowPct && raw.Impressions > DirectiveThresholds.ImpressionsCtrCheck)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "adset",
                    "OPTIMIZE",
                    "warning",
                    "Link CTR düşük ve gösterim yeterli: teklif veya hedefleme sorunu olabilir.",
                    score,
                    health));
        }

        if (raw.CtrLink > DirectiveThresholds.CtrHighPct
            && cvr is < DirectiveThresholds.CvrLowPct
            && raw.LinkClicks >= 50)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "adset",
                    "OPTIMIZE",
                    "warning",
                    "CTR iyi ama dönüşüm düşük: açılış sayfası veya teklif uyumu kontrolü.",
                    score,
                    health));
        }

        return Dedupe(list);
    }

    public static IReadOnlyList<Directive> EvaluateAd(int userId, RawInsight raw, ComputedMetric m, int scoreValue, string health)
    {
        int? score = scoreValue;
        var healthLocal = health;
        var list = new List<Directive>();
        var cvr = LinkCvrPct(raw);

        list.Add(
            Dir(
                userId,
                raw.EntityId,
                "ad",
                "WATCH",
                "info",
                $"Özet skor: {scoreValue}/100 — {health}",
                score,
                healthLocal));

        var hookVal = m.ThumbstopRatePct ?? m.HookRate;

        if (hookVal is < DirectiveThresholds.HookPoorPct)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "Hook rate düşük: ilk 3 sn / açılış karesini güçlendir.",
                    score,
                    healthLocal));
        }

        if (m.HoldRate is < DirectiveThresholds.HoldPoorPct && (hookVal is null or >= DirectiveThresholds.HookPoorPct))
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "Hold rate düşük: video ortası senaryo ve ritim iyileştirmesi.",
                    score,
                    healthLocal));
        }

        if (raw.CtrLink < DirectiveThresholds.CtrLowPct && hookVal is >= DirectiveThresholds.HookGoodPct)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "Hook iyi ama CTR düşük: CTA veya statik görsel mesajı netleştir.",
                    score,
                    healthLocal));
        }

        if (raw.CtrLink > DirectiveThresholds.CtrHighPct
            && cvr is < DirectiveThresholds.CvrLowPct
            && raw.LinkClicks >= 30)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "CTR yüksek, dönüşüm düşük: landing page ve teklif uyumu.",
                    score,
                    healthLocal));
        }

        if (raw.Frequency > DirectiveThresholds.FrequencyAdConcern
            && m.TargetRoas is not null
            && m.Roas is not null
            && m.Roas < m.TargetRoas)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "STOP",
                    "warning",
                    "Frekans yüksek ve ROAS hedef altı: reklamı durdur / yenile.",
                    score,
                    healthLocal));
        }

        if (m.MismatchRatio is > DirectiveThresholds.MismatchCta)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "Mismatch oranı yüksek: genel tıklama ile link tıklaması uyumsuz; CTA’yı netleştir.",
                    score,
                    healthLocal));
        }

        if (health == "Durdur")
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "STOP",
                    "critical",
                    "Genel sağlık: Durdur — skor veya kârlılık eşikleri kritik.",
                    score,
                    healthLocal));
        }

        return Dedupe(list);
    }

    private static Directive Dir(
        int userId,
        string entityId,
        string entityType,
        string directiveType,
        string severity,
        string message,
        int? score,
        string? healthStatus) =>
        new()
        {
            UserId = userId,
            EntityId = entityId,
            EntityType = entityType,
            DirectiveType = directiveType,
            Severity = severity,
            Message = message,
            Score = score,
            HealthStatus = healthStatus,
            TriggeredAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };

    private static List<Directive> Dedupe(List<Directive> list)
    {
        var seen = new HashSet<string>();
        var result = new List<Directive>();
        foreach (var d in list)
        {
            var key = $"{d.EntityType}|{d.EntityId}|{d.DirectiveType}|{d.Severity}|{d.Message}";
            if (seen.Add(key))
            {
                result.Add(d);
            }
        }

        return result;
    }
}
