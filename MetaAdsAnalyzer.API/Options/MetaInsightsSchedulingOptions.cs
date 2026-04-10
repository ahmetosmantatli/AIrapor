namespace MetaAdsAnalyzer.API.Options;

/// <summary>
/// Yerleşik arka plan zamanlayıcı (ek NuGet yok). Çok örnek ölçeklemede tekrarlı çalışmayı
/// önlemek için dağıtık kilit veya tek worker önerilir.
/// </summary>
public sealed class MetaInsightsSchedulingOptions
{
    public const string SectionName = "MetaInsightsScheduling";

    public bool Enabled { get; set; }

    /// <summary>Ana döngü uyku süresi (saniye).</summary>
    public int TickSeconds { get; set; } = 60;

    /// <summary>Bugün verisi en az bu kadar saatte bir çekilir.</summary>
    public double TodayIntervalHours { get; set; } = 4;

    /// <summary>Dün verisi (UTC saat; pencere: bu saatin ilk 30 dakikası).</summary>
    public int YesterdayRunUtcHour { get; set; } = 8;

    /// <summary>7/14/30 gün özetleri (UTC saat; aynı pencere).</summary>
    public int SummaryRunUtcHour { get; set; } = 3;

    public string[] Levels { get; set; } = { "campaign", "adset", "ad" };

    public string[] SummaryDatePresets { get; set; } = { "last_7d", "last_14d", "last_30d" };

    /// <summary>Meta saatlik kota için ardışık Graph çağrıları arası gecikme (ms).</summary>
    public int DelayMsBetweenSyncCalls { get; set; } = 600;

    public string TodayDatePreset { get; set; } = "today";

    public string YesterdayDatePreset { get; set; } = "yesterday";
}
