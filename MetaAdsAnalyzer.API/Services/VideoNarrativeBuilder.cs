using System.Globalization;

namespace MetaAdsAnalyzer.API.Services;

/// <summary>Video analizi için dinamik Türkçe özet ve etiket kuralları.</summary>
public static class VideoNarrativeBuilder
{
    public static IReadOnlyList<string> BuildNarrativeLines(
        decimal? thumbstopPct,
        decimal? holdPct,
        decimal? completionPct,
        decimal? roas,
        decimal? breakEvenRoas,
        decimal? targetRoas)
    {
        var list = new List<string>();
        var tr = CultureInfo.GetCultureInfo("tr-TR");

        if (thumbstopPct is < 20m)
        {
            list.Add(
                $"Hook rate %{thumbstopPct!.Value.ToString("F1", tr)} — ilk 3 saniye izleyiciyi durduramıyor. Açılış karesini değiştir.");
        }

        if (holdPct is < 15m)
        {
            list.Add(
                $"Hold rate %{holdPct!.Value.ToString("F1", tr)} — izleyiciler videoyu yarıda bırakıyor. Orta sahneyi hızlandır.");
        }

        if (completionPct is < 5m)
        {
            list.Add(
                $"Completion rate %{completionPct!.Value.ToString("F1", tr)} — videonun sonuna kimse ulaşmıyor. Video çok uzun veya CTA geç geliyor.");
        }

        if (roas is decimal rLoss && breakEvenRoas is decimal beLoss && beLoss > 0m && rLoss < beLoss)
        {
            list.Add("Her satışta zarar ediliyor. Bütçeyi durdur.");
        }
        else if (roas is decimal rMid
                 && breakEvenRoas is decimal beMid
                 && targetRoas is decimal tgMid
                 && beMid > 0m
                 && tgMid > 0m
                 && rMid > beMid
                 && rMid < tgMid)
        {
            list.Add("Kârlı ama hedefe ulaşamıyor. CTA güçlendir.");
        }
        else if (roas is decimal rHi && targetRoas is decimal tgHi && tgHi > 0m && rHi >= tgHi)
        {
            list.Add("Güçlü performans. Bütçeyi %20-30 artır.");
        }

        return list;
    }

    public static IReadOnlyList<string> BuildProblemTags(
        decimal? thumbstopPct,
        decimal? holdPct,
        decimal? completionPct,
        decimal? roas,
        decimal? breakEvenRoas,
        decimal? targetRoas,
        decimal ctrLinkPct,
        decimal? linkCvrPct,
        long linkClicks)
    {
        var tags = new List<string>();

        void Add(string t)
        {
            if (!tags.Contains(t))
            {
                tags.Add(t);
            }
        }

        if (thumbstopPct is < 20m)
        {
            Add("Hook Problemi");
        }

        if (holdPct is < 15m)
        {
            Add("Mid-Drop Var");
        }

        if (completionPct is < 5m)
        {
            Add("CTA Geç");
        }

        if (thumbstopPct is >= DirectiveThresholds.HookGoodPct && ctrLinkPct < DirectiveThresholds.CtrLowPct)
        {
            Add("CTA Geç");
        }

        if (ctrLinkPct > DirectiveThresholds.CtrHighPct
            && linkCvrPct is < DirectiveThresholds.CvrLowPct
            && linkClicks >= 30)
        {
            Add("Landing Sorunu");
        }

        if (targetRoas is > 0m && roas.HasValue && targetRoas.HasValue && roas.Value >= targetRoas.Value)
        {
            Add("Güçlü Performans");
        }

        return tags;
    }
}
