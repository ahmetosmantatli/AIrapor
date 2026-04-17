namespace MetaAdsAnalyzer.API.Options;

/// <summary>Stripe Checkout (abonelik) ve webhook. Anahtarlar User Secrets / ortam değişkeninde tutulmalıdır.</summary>
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>sk_live_… / sk_test_… Boşsa ödeme uçları 503 döner.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>whsec_… Stripe Dashboard → Webhooks → imza sırrı.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Örn. https://app.example.com/app/settings?checkout=success&amp;session_id={CHECKOUT_SESSION_ID}</summary>
    public string SuccessUrl { get; set; } = string.Empty;

    /// <summary>Örn. https://app.example.com/app/settings?checkout=cancel</summary>
    public string CancelUrl { get; set; } = string.Empty;

    /// <summary>Dashboard’da oluşturulan Price id (price_…), Standart plan.</summary>
    public string PriceStandard { get; set; } = string.Empty;

    /// <summary>Price id, Pro plan.</summary>
    public string PricePro { get; set; } = string.Empty;
}
