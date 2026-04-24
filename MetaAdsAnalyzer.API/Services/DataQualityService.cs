using MetaAdsAnalyzer.Core.Entities;

namespace MetaAdsAnalyzer.API.Services;

public sealed class DataQualityResult
{
    public bool InsufficientImpressions { get; set; }
    public bool LowPurchases { get; set; }
    public bool EarlyData { get; set; }
    public bool LearningPhase { get; set; }
    public bool InsufficientSpend { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public interface IDataQualityService
{
    DataQualityResult Evaluate(
        IReadOnlyCollection<RawInsight> rows,
        bool learningPhase = false,
        decimal? dailyBudget = null);
}

public sealed class DataQualityService : IDataQualityService
{
    public DataQualityResult Evaluate(
        IReadOnlyCollection<RawInsight> rows,
        bool learningPhase = false,
        decimal? dailyBudget = null)
    {
        var result = new DataQualityResult();
        if (rows.Count == 0)
        {
            return result;
        }

        var impressions = rows.Sum(r => r.Impressions);
        var purchases = rows.Sum(r => r.Purchases);
        var spend = rows.Sum(r => r.Spend);
        var minStart = rows.Min(r => r.DateStart.DayNumber);
        var maxStop = rows.Max(r => r.DateStop.DayNumber);
        var daySpan = Math.Max(1, maxStop - minStart + 1);

        result.InsufficientImpressions = impressions < 1000;
        result.LowPurchases = purchases < 5;
        result.EarlyData = daySpan < 3;
        result.LearningPhase = learningPhase;
        result.InsufficientSpend = dailyBudget.HasValue && dailyBudget.Value > 0 && spend < (dailyBudget.Value * 3m);

        if (result.InsufficientImpressions)
        {
            result.Warnings.Add("Yeterli gösterim yok — metrikler yanıltıcı olabilir");
        }

        if (result.LowPurchases)
        {
            result.Warnings.Add("Satın alma sayısı düşük — ROAS volatil, yorumlarken dikkatli ol");
        }

        if (result.EarlyData)
        {
            result.Warnings.Add("Çok erken — en az 3 gün bekle");
        }

        if (result.LearningPhase)
        {
            result.Warnings.Add("Adset öğrenme fazında — direktif bekletiliyor");
        }

        return result;
    }
}
