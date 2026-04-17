namespace MetaAdsAnalyzer.Core.Subscription;

/// <summary>Plan satırındaki özellik bayraklarından önce: abonelik dönemi geçerli mi?</summary>
public static class SubscriptionAccess
{
    public static bool GrantsPlanFeatures(
        string subscriptionStatus,
        DateTimeOffset? planExpiresAtUtc,
        DateTimeOffset utcNow)
    {
        var s = (subscriptionStatus ?? string.Empty).Trim().ToLowerInvariant();

        if (s == SubscriptionStatuses.Expired)
        {
            return false;
        }

        if (planExpiresAtUtc is { } ex && utcNow > ex)
        {
            return false;
        }

        if (s == SubscriptionStatuses.Canceled && planExpiresAtUtc is null)
        {
            return false;
        }

        return s switch
        {
            SubscriptionStatuses.None or SubscriptionStatuses.Active or SubscriptionStatuses.PastDue => true,
            SubscriptionStatuses.Canceled => true,
            _ => false,
        };
    }
}
