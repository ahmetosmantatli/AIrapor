namespace MetaAdsAnalyzer.Core.Subscription;

/// <summary>Ödeme webhook’ları ve yönetim API’si ile güncellenir.</summary>
public static class SubscriptionStatuses
{
    public const string None = "none";

    public const string Active = "active";

    public const string Canceled = "canceled";

    public const string PastDue = "past_due";

    public const string Expired = "expired";
}
