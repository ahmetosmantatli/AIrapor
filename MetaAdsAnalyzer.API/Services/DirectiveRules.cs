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
                    "Gösterim sayısı güvenilir yorum için düşük seviyede.",
                    "Örneklem 1000 gösterimin altında olduğu için metrik oynaklığı yüksektir.",
                    "İzle: Veriyi büyüt ve karar öncesi daha fazla gösterim topla.",
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
                    "Satın alma sayısı düşük olduğu için performans yorumu zayıf kalıyor.",
                    "Dönüşüm sayısı 5'in altında olduğunda ROAS istatistiksel olarak volatil olur.",
                    "Bekle: Karar vermeden önce daha fazla satın alma verisi topla.",
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
                    "Veri henüz olgunlaşmadığı için erken yorum riski var.",
                    "Harcama düşük, gün sayısı az ve dönüşüm sinyali yeterince birikmedi.",
                    "Bekle: En az birkaç gün daha veri biriktir ve sonra yeniden değerlendir.",
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
                    "Kampanya hedefin üzerinde performans gösteriyor.",
                    "ROAS hedefi geçti ve harcama/süre ölçekleme için yeterli seviyeye ulaştı.",
                    "Artır: Bütçeyi kademeli şekilde yükselt.",
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
                    "Kampanya zarar etmiyor ama hedef kârlılığa ulaşamıyor.",
                    "ROAS break-even üstünde kalsa da target seviyesinin altında.",
                    "Optimize Et: Kreatif, hedefleme ve teklif ayarlarını iyileştir.",
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
                    "Kampanya sürdürülebilir kârlılık eşiğinin altında kaldı.",
                    "ROAS break-even altına düştü ve karar için yeterli dönüşüm/süre oluştu.",
                    "Durdur: Bütçeyi kes veya kampanyayı kapat.",
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
                    "Adset maliyet verimliliği güçlü görünüyor.",
                    "Gerçek CPA hedef CPA'nın altında ve harcama anlamlı seviyede.",
                    "Artır: Kazanan adsete daha fazla bütçe aktar.",
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
                    "Adset satın alma maliyeti kabul edilen sınırı aştı.",
                    "CPA, max CPA üstüne çıktı ve karar için yeterli dönüşüm birikti.",
                    "Durdur: Adseti kapat veya bütçeyi sert azalt.",
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
                    "Aynı kitle reklamı fazla görmeye başladı.",
                    "Frekans 3.5 üstünde olduğunda kreatif yorgunluğu ve performans düşüşü artar.",
                    "Değiştir: Yeni kreatif yükle veya hedef kitleyi yenile.",
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
                    "İlgi çekme performansı düşük kaldı.",
                    "Gösterim yüksek olmasına rağmen link tıklama oranı zayıf.",
                    "Değiştir: Hedefleme veya kreatif açısını güncelle.",
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
                    "Reklam tıklama alıyor ama satışa dönüşmüyor.",
                    "CTR güçlü olsa da dönüşüm oranı düşük; sorun büyük olasılıkla sayfa/teklif tarafında.",
                    "İncele: Landing page ve teklif uyumunu düzelt.",
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
        var atcRate = raw.LinkClicks <= 0 ? (decimal?)null : (decimal)raw.AddToCart / raw.LinkClicks * 100m;
        var purchaseFromAtc = raw.AddToCart <= 0 ? (decimal?)null : (decimal)raw.Purchases / raw.AddToCart;
        var purchaseFromCheckout = raw.InitiateCheckout <= 0 ? (decimal?)null : (decimal)raw.Purchases / raw.InitiateCheckout;
        var p50DropRatio = raw.VideoP25 > 0 ? (decimal)raw.VideoP50 / raw.VideoP25 : (decimal?)null;
        var p100DropRatio = raw.VideoP75 > 0 ? (decimal)raw.VideoP100 / raw.VideoP75 : (decimal?)null;

        list.Add(
            Dir(
                userId,
                raw.EntityId,
                "ad",
                "WATCH",
                "info",
                $"Reklam için genel skor {scoreValue}/100 seviyesinde.",
                "Skor tüm temel sinyallerin birleşik performansını yansıtır.",
                "İncele: Tanılara göre öncelikli aksiyonu uygula.",
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
                    $"İzleyicilerin yalnızca %{hookVal?.ToString("F1")} kadarı ilk 3 saniyeyi geçiyor.",
                    "Açılış karesi veya ilk cümle dikkat çekmiyor.",
                    "Değiştir: Açılışı daha güçlü bir vaad/soru ile başlat.",
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
                    $"Video ortasında izleyici tutma zayıf (%{m.HoldRate?.ToString("F1")}).",
                    "İçerik ritmi orta bölümde düşüyor ve ilgi kaybı oluşuyor.",
                    "Değiştir: Orta bölümü sıkıştır ve tempoyu yükselt.",
                    score,
                    healthLocal));
        }

        // EKSİK VIDEO KURALI #1: p25 -> p50 sert düşüş.
        if (raw.VideoP25 > 0 && p50DropRatio is < 0.5m)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "İzleyici %25 noktasından sonra hızlı şekilde ayrılıyor.",
                    "Orta kısımda içerik gücü düşüyor veya vaat zayıflıyor.",
                    "Ekle: Videonun ortasına güçlü bir sahne ve yeni hook yerleştir.",
                    score,
                    healthLocal));
        }

        // EKSİK VIDEO KURALI #2: p75 -> p100 sert düşüş.
        if (raw.VideoP75 > 0 && p100DropRatio is < 0.5m)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "Video bitişine yakın izleyici kaybı keskinleşiyor.",
                    "Son bölümde CTA geç kalıyor veya final zayıf kalıyor.",
                    "Değiştir: CTA'yı %75 civarında öne çek ve finali kısalt.",
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
                    "Video izleniyor ama tıklama davranışı düşük kalıyor.",
                    "Mesaj-CTA bağı net olmadığı için kullanıcı aksiyona geçmiyor.",
                    "Değiştir: CTA metnini ve görsel mesajı netleştir.",
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
                    "Tıklama güçlü olmasına rağmen satın alma dönüşümü zayıf.",
                    "Reklam vaadi ile ürün sayfası/teklif deneyimi uyumsuz olabilir.",
                    "İncele: Landing page akışını ve teklif netliğini iyileştir.",
                    score,
                    healthLocal));
        }

        // EKSİK VIDEO KURALI #3: CTR > 2 ve Purchase CVR < 1.
        if (raw.CtrLink > 2m && cvr is < 1m && raw.LinkClicks > 0)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "Reklam etkileşim üretiyor ancak satışa dönüşüm düşük.",
                    "Sorun kreatiften çok ürün sayfası ve checkout akışında görünüyor.",
                    "İncele: Ürün sayfası dönüşüm adımlarını optimize et.",
                    score,
                    healthLocal));
        }

        // EKSİK STATİK KURALI #4: CTR düşük + CPM yüksek.
        if (!m.IsVideoCreative && raw.CtrLink < 0.8m && raw.Cpm > 50m)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "Statik kreatif yanlış kitlede zayıf tepki alıyor.",
                    "Düşük CTR ve yüksek CPM birlikte audience-mesaj uyumsuzluğunu gösterir.",
                    "Daralt: Audience segmentini daralt veya yeni kitleye geç.",
                    score,
                    healthLocal));
        }

        // EKSİK STATİK KURALI #5: CTR iyi + ATC düşük.
        if (!m.IsVideoCreative && raw.CtrLink > 1m && atcRate is < 3m)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "Kullanıcı tıklıyor ancak sepete ekleme oranı düşük kalıyor.",
                    "Fiyat algısı, ürün anlatımı veya görsel güven sinyali yetersiz olabilir.",
                    "İncele: Fiyat, ürün açıklaması ve görsel kaliteyi güçlendir.",
                    score,
                    healthLocal));
        }

        // EKSİK STATİK KURALI #6: ATC iyi ama satın alma ATC'ye göre düşük.
        if (!m.IsVideoCreative && atcRate is > 3m && purchaseFromAtc is < 0.3m)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "Sepete ekleme var fakat satın alma adımına geçiş zayıf.",
                    "Kargo, ödeme seçenekleri veya güven unsurları sürtünme yaratıyor olabilir.",
                    "İncele: Kargo/ödeme ve güven sinyallerini iyileştir.",
                    score,
                    healthLocal));
        }

        // EKSİK STATİK KURALI #7: Checkout'tan satın almaya son adım kaybı.
        if (!m.IsVideoCreative && raw.InitiateCheckout > 0 && purchaseFromCheckout is < 0.5m)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "STOP",
                    "critical",
                    "Kullanıcılar ödeme son adımında süreci terk ediyor.",
                    "Fiyat şoku veya ödeme sayfası sürtünmesi dönüşümü düşürüyor.",
                    "İncele: Ödeme sayfasını sadeleştir ve fiyat sürprizlerini kaldır.",
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
                    "Aynı kitlede reklam yorulması nedeniyle verim düşüyor.",
                    "Frekans yükselirken ROAS hedefin altında kaldı.",
                    "Durdur: Reklamı kapat ve yeni varyasyonla yeniden test et.",
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
                    "Etkileşim var ancak link tıklaması kalitesi düşük.",
                    "Mismatch oranı yüksek olduğunda kreatif mesajı aksiyona dönüşmez.",
                    "Değiştir: CTA metnini ve kreatif-ürün bağını netleştir.",
                    score,
                    healthLocal));
        }

        // EKSİK STATİK KURALI #8: Statikte mismatch > 2.5.
        if (!m.IsVideoCreative && m.MismatchRatio is > 2.5m)
        {
            list.Add(
                Dir(
                    userId,
                    raw.EntityId,
                    "ad",
                    "OPTIMIZE",
                    "warning",
                    "Statik kreatifte tıklama kalitesi düşük kalıyor.",
                    "Görsel anlatım ile ürün vaadi arasında zayıf bağ bulunuyor.",
                    "Değiştir: Görseli ürün faydasına daha net bağla.",
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
                    "Genel reklam sağlığı kritik seviyeye düştü.",
                    "Skor ve kârlılık göstergeleri kabul edilebilir eşiğin altında.",
                    "Durdur: Reklamı kapat ve yeni kreatif stratejisiyle yeniden başlat.",
                    score,
                    healthLocal));
        }

        return PrioritizeTop3(Dedupe(list));
    }

    private static Directive Dir(
        int userId,
        string entityId,
        string entityType,
        string directiveType,
        string severity,
        string symptom,
        string reason,
        string action,
        int? score,
        string? healthStatus)
    {
        var message = $"Semptom: {symptom}\nNeden: {reason}\nAksiyon: {action}";
        return
        new()
        {
            UserId = userId,
            EntityId = entityId,
            EntityType = entityType,
            DirectiveType = directiveType,
            Severity = severity,
            Message = message,
            Symptom = symptom,
            Reason = reason,
            Action = action,
            Score = score,
            HealthStatus = healthStatus,
            TriggeredAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };
    }

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

    private static IReadOnlyList<Directive> PrioritizeTop3(List<Directive> list)
    {
        static int SevRank(string sev) =>
            string.Equals(sev, "critical", StringComparison.OrdinalIgnoreCase) ? 0 :
            string.Equals(sev, "warning", StringComparison.OrdinalIgnoreCase) ? 1 : 2;

        return list
            .OrderBy(d => SevRank(d.Severity))
            .ThenByDescending(d => d.TriggeredAt)
            .Take(3)
            .ToList();
    }
}
