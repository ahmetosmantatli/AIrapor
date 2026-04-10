namespace MetaAdsAnalyzer.API.Services;

/// <summary>Örneklem ve kural eşikleri (Faza 3.4 + 4).</summary>
public static class DirectiveThresholds
{
    public const long MinImpressionsForConfidence = 1000;

    public const int MinDaysForScale = 7;

    public const int MinDaysForStop = 5;

    public const long MinPurchasesForRoasDecision = 5;

    public const long MinPurchasesForStop = 5;

    public const long MinPurchasesForAdsetStop = 10;

    public const int MaxDaysWait = 3;

    public const decimal MinSpendLoose = 30m;

    public const decimal MinSpendScale = 50m;

    public const decimal FrequencyFatigue = 3.5m;

    public const decimal FrequencyAdConcern = 3m;

    public const decimal CtrLowPct = 1m;

    public const decimal CtrHighPct = 2m;

    public const decimal CvrLowPct = 1m;

    public const long ImpressionsCtrCheck = 2000;

    public const decimal HookPoorPct = 20m;

    public const decimal HookGoodPct = 30m;

    public const decimal HoldPoorPct = 15m;

    public const decimal MismatchCta = 3m;
}
