using MetaAdsAnalyzer.API.Options;
using MetaAdsAnalyzer.Core.Entities;
using CoreSub = MetaAdsAnalyzer.Core.Subscription;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace MetaAdsAnalyzer.API.Services;

/// <summary>
/// Stripe abonelik webhook’ları: <c>checkout.session.completed</c> (ilk ödeme),
/// <c>customer.subscription.updated</c> (durum / dönem / plan değişimi),
/// <c>customer.subscription.deleted</c> (abonelik bitti → Standart),
/// <c>invoice.paid</c> (yenileme sonrası dönem sonu senkronu),
/// <c>invoice.payment_failed</c> (ödeme hatası → genelde <c>past_due</c> senkronu).
/// Fatura e-postası ve müşteri portalı Stripe Dashboard / Customer Portal ile yürütülür.
/// </summary>
public interface IStripeBillingService
{
    Task<string> CreateCheckoutSessionUrlAsync(int userId, string planCode, CancellationToken cancellationToken);

    Task HandleWebhookAsync(string json, string stripeSignature, CancellationToken cancellationToken);
}

public sealed class StripeBillingService : IStripeBillingService
{
    private readonly AppDbContext _db;
    private readonly StripeOptions _opt;
    private readonly ILogger<StripeBillingService> _logger;

    public StripeBillingService(
        AppDbContext db,
        IOptions<StripeOptions> options,
        ILogger<StripeBillingService> logger)
    {
        _db = db;
        _opt = options.Value;
        _logger = logger;
    }

    public async Task<string> CreateCheckoutSessionUrlAsync(
        int userId,
        string planCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_opt.SecretKey))
        {
            throw new InvalidOperationException("Stripe:SecretKey yapılandırılmamış.");
        }

        if (string.IsNullOrWhiteSpace(_opt.SuccessUrl) || string.IsNullOrWhiteSpace(_opt.CancelUrl))
        {
            throw new InvalidOperationException("Stripe SuccessUrl / CancelUrl gerekli.");
        }

        var priceId = GetPriceId(planCode);
        if (string.IsNullOrWhiteSpace(priceId))
        {
            throw new InvalidOperationException($"Stripe Price id tanımlı değil: {planCode}");
        }

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException("Kullanıcı bulunamadı.");
        }

        var meta = new Dictionary<string, string>
        {
            ["user_id"] = userId.ToString(),
            ["plan_code"] = planCode,
        };

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = _opt.SuccessUrl,
            CancelUrl = _opt.CancelUrl,
            ClientReferenceId = userId.ToString(),
            Metadata = meta,
            SubscriptionData = new SessionSubscriptionDataOptions { Metadata = meta },
            LineItems =
            [
                new SessionLineItemOptions { Price = priceId, Quantity = 1 },
            ],
        };

        if (!string.IsNullOrEmpty(user.StripeCustomerId))
        {
            options.Customer = user.StripeCustomerId;
        }
        else
        {
            options.CustomerEmail = user.Email;
        }

        var client = new StripeClient(_opt.SecretKey);
        var service = new SessionService(client);
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(session.Url))
        {
            throw new InvalidOperationException("Stripe oturum URL üretmedi.");
        }

        return session.Url;
    }

    public async Task HandleWebhookAsync(
        string json,
        string stripeSignature,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_opt.SecretKey))
        {
            throw new InvalidOperationException("Stripe:SecretKey yapılandırılmamış.");
        }

        if (string.IsNullOrWhiteSpace(_opt.WebhookSecret))
        {
            throw new InvalidOperationException("Stripe:WebhookSecret yapılandırılmamış.");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, _opt.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook imza doğrulaması başarısız");
            throw;
        }

        var client = new StripeClient(_opt.SecretKey);
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutSessionCompletedAsync(client, stripeEvent, cancellationToken).ConfigureAwait(false);
                break;
            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync(client, stripeEvent, cancellationToken).ConfigureAwait(false);
                break;
            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent, cancellationToken).ConfigureAwait(false);
                break;
            case "invoice.paid":
                await HandleInvoicePaidAsync(client, stripeEvent, cancellationToken).ConfigureAwait(false);
                break;
            case "invoice.payment_failed":
                await HandleInvoicePaymentFailedAsync(client, stripeEvent, cancellationToken).ConfigureAwait(false);
                break;
            default:
                _logger.LogDebug("Stripe webhook yok sayıldı: {Type}", stripeEvent.Type);
                break;
        }
    }

    private string GetPriceId(string planCode) =>
        planCode.Trim().ToLowerInvariant() switch
        {
            "standard" => _opt.PriceStandard,
            "pro" => _opt.PricePro,
            _ => string.Empty,
        };

    private async Task HandleCheckoutSessionCompletedAsync(
        StripeClient client,
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Session session || session.Mode != "subscription")
        {
            return;
        }

        if (string.IsNullOrEmpty(session.SubscriptionId))
        {
            _logger.LogWarning("checkout.session.completed abonelik id içermiyor");
            return;
        }

        var subService = new SubscriptionService(client);
        var subscription = await subService.GetAsync(session.SubscriptionId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var userId = ParseUserId(session.ClientReferenceId, session.Metadata, subscription.Metadata);
        if (userId is null)
        {
            _logger.LogWarning("Stripe oturumda user_id yok (client_reference_id / metadata)");
            return;
        }

        var planCode = GetPlanCode(session.Metadata, subscription.Metadata);
        if (planCode is null)
        {
            _logger.LogWarning("Stripe oturumda plan_code yok");
            return;
        }

        await ApplySubscriptionAsync(userId.Value, planCode, subscription, session.CustomerId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task HandleSubscriptionUpdatedAsync(
        StripeClient client,
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Subscription subscription)
        {
            return;
        }

        var user = await FindUserForSubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            _logger.LogWarning("subscription.updated için kullanıcı bulunamadı: {SubId}", subscription.Id);
            return;
        }

        var planCode = GetPlanCode(subscription.Metadata, null) ?? user.SubscriptionPlan.Code;
        await ApplySubscriptionAsync(user.Id, planCode, subscription, subscription.CustomerId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Yenileme sonrası <see cref="Subscription.CurrentPeriodEnd"/> güncellemesi; bazı döngülerde yalnızca fatura olayı gelir.</summary>
    private async Task HandleInvoicePaidAsync(
        StripeClient client,
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Invoice invoice)
        {
            return;
        }

        if (string.IsNullOrEmpty(invoice.SubscriptionId))
        {
            return;
        }

        await SyncUserFromStripeSubscriptionAsync(client, invoice.SubscriptionId, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleInvoicePaymentFailedAsync(
        StripeClient client,
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Invoice invoice)
        {
            return;
        }

        _logger.LogWarning(
            "Stripe fatura ödemesi başarısız: invoice {InvoiceId}, subscription {SubscriptionId}, customer {CustomerId}",
            invoice.Id,
            invoice.SubscriptionId ?? "(yok)",
            invoice.CustomerId ?? "(yok)");

        if (string.IsNullOrEmpty(invoice.SubscriptionId))
        {
            return;
        }

        await SyncUserFromStripeSubscriptionAsync(client, invoice.SubscriptionId, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncUserFromStripeSubscriptionAsync(
        StripeClient client,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        Subscription subscription;
        try
        {
            var subService = new SubscriptionService(client);
            subscription = await subService.GetAsync(subscriptionId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe aboneliği okunamadı: {SubscriptionId}", subscriptionId);
            return;
        }

        var user = await FindUserForSubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            _logger.LogWarning("Abonelik için kullanıcı bulunamadı (senkron): {SubscriptionId}", subscriptionId);
            return;
        }

        var planCode = GetPlanCode(subscription.Metadata, null) ?? user.SubscriptionPlan.Code;
        await ApplySubscriptionAsync(user.Id, planCode, subscription, subscription.CustomerId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Subscription subscription)
        {
            return;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.StripeSubscriptionId == subscription.Id, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        var standardId = await _db.SubscriptionPlans.AsNoTracking()
            .Where(p => p.Code == "standard" && p.IsActive)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (standardId == 0)
        {
            _logger.LogError("standard planı yok; kullanıcı {UserId} düşürülemedi", user.Id);
            return;
        }

        user.SubscriptionPlanId = standardId;
        user.SubscriptionStatus = CoreSub.SubscriptionStatuses.Active;
        user.PlanExpiresAt = null;
        user.StripeSubscriptionId = null;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await LinkedMetaAdAccountTrimHelper.EnforcePlanLimitAsync(_db, user.Id, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Stripe aboneliği silindi, kullanıcı {UserId} Standart plana alındı", user.Id);
    }

    private async Task<User?> FindUserForSubscriptionAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        var bySub = await _db.Users
            .Include(u => u.SubscriptionPlan)
            .FirstOrDefaultAsync(u => u.StripeSubscriptionId == subscription.Id, cancellationToken)
            .ConfigureAwait(false);
        if (bySub is not null)
        {
            return bySub;
        }

        var uid = ParseUserId(null, subscription.Metadata, subscription.Metadata);
        if (uid is null)
        {
            return null;
        }

        return await _db.Users
            .Include(u => u.SubscriptionPlan)
            .FirstOrDefaultAsync(u => u.Id == uid.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ApplySubscriptionAsync(
        int userId,
        string planCode,
        Subscription subscription,
        string? customerId,
        CancellationToken cancellationToken)
    {
        var mapped = MapStripeSubscriptionStatus(subscription.Status);
        if (mapped is null)
        {
            _logger.LogInformation(
                "Stripe abonelik durumu {Status} için DB güncellemesi atlandı (user {UserId})",
                subscription.Status,
                userId);
            return;
        }

        var planId = await _db.SubscriptionPlans.AsNoTracking()
            .Where(p => p.Code == planCode && p.IsActive)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (planId == 0)
        {
            _logger.LogError("Geçersiz plan_code {PlanCode} veya pasif plan (user {UserId})", planCode, userId);
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        user.SubscriptionPlanId = planId;
        user.SubscriptionStatus = mapped;
        user.PlanExpiresAt = ToUtcOffset(subscription.CurrentPeriodEnd);
        user.StripeSubscriptionId = subscription.Id;
        if (!string.IsNullOrEmpty(customerId))
        {
            user.StripeCustomerId = customerId;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Stripe abonelik uygulandı: user {UserId}, plan {Plan}, status {Status}, dönem sonu {End}",
            userId,
            planCode,
            mapped,
            user.PlanExpiresAt);
        await LinkedMetaAdAccountTrimHelper.EnforcePlanLimitAsync(_db, userId, cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset ToUtcOffset(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : dt.ToUniversalTime();
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private static string? MapStripeSubscriptionStatus(string stripeStatus)
    {
        return stripeStatus.ToLowerInvariant() switch
        {
            "active" => CoreSub.SubscriptionStatuses.Active,
            "trialing" => CoreSub.SubscriptionStatuses.Active,
            "past_due" => CoreSub.SubscriptionStatuses.PastDue,
            "canceled" => CoreSub.SubscriptionStatuses.Canceled,
            "unpaid" => CoreSub.SubscriptionStatuses.PastDue,
            "paused" => CoreSub.SubscriptionStatuses.PastDue,
            "incomplete_expired" => CoreSub.SubscriptionStatuses.Expired,
            _ => null,
        };
    }

    private static int? ParseUserId(
        string? clientReferenceId,
        IDictionary<string, string>? sessionMeta,
        IDictionary<string, string>? subMeta)
    {
        if (!string.IsNullOrEmpty(clientReferenceId) && int.TryParse(clientReferenceId, out var a))
        {
            return a;
        }

        if (TryMeta(sessionMeta, "user_id", out var s) && int.TryParse(s, out var b))
        {
            return b;
        }

        if (TryMeta(subMeta, "user_id", out var s2) && int.TryParse(s2, out var c))
        {
            return c;
        }

        return null;
    }

    private static string? GetPlanCode(
        IDictionary<string, string>? a,
        IDictionary<string, string>? b)
    {
        if (TryMeta(a, "plan_code", out var c))
        {
            return c.Trim().ToLowerInvariant();
        }

        if (TryMeta(b, "plan_code", out var d))
        {
            return d.Trim().ToLowerInvariant();
        }

        return null;
    }

    private static bool TryMeta(IDictionary<string, string>? meta, string key, out string value)
    {
        value = string.Empty;
        if (meta is null)
        {
            return false;
        }

        if (!meta.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        value = raw;
        return true;
    }
}
