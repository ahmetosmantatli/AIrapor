using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Services;

/// <summary>Her gün 08:00'de uygulanmış önerilerin 7 günlük etkisini ölçer.</summary>
public sealed class SuggestionImpactMeasurementService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SuggestionImpactMeasurementService> _logger;
    private DateOnly? _lastRunDate;

    public SuggestionImpactMeasurementService(IServiceScopeFactory scopeFactory, ILogger<SuggestionImpactMeasurementService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowLocal = DateTime.Now;
                var today = DateOnly.FromDateTime(nowLocal);
                if (nowLocal.Hour == 8 && nowLocal.Minute < 30 && _lastRunDate != today)
                {
                    await MeasureAsync(stoppingToken);
                    _lastRunDate = today;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öneri etki ölçüm job hatası");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task MeasureAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var threshold = DateTimeOffset.UtcNow.AddDays(-7);

        var candidates = await db.SavedReportSuggestions
            .Include(x => x.SavedReport)
            .Where(x => x.AppliedAt != null && x.AppliedAt <= threshold && x.ImpactMeasuredAt == null)
            .ToListAsync(ct);

        foreach (var s in candidates)
        {
            var userId = s.SavedReport.UserId;
            var adId = s.SavedReport.AdId;
            var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
            var raws = await db.RawInsights.AsNoTracking()
                .Where(r => r.UserId == userId && r.Level == "ad" && r.EntityId == adId && r.DateStart >= fromDate)
                .OrderByDescending(r => r.FetchedAt)
                .ToListAsync(ct);

            if (raws.Count == 0)
            {
                s.ImpactMeasuredAt = DateTimeOffset.UtcNow;
                continue;
            }

            var spend = raws.Sum(x => x.Spend);
            var purchaseValue = raws.Sum(x => x.PurchaseValue);
            var purchases = raws.Sum(x => x.Purchases);
            var impressions = raws.Sum(x => x.Impressions);
            var video3s = raws.Sum(x => x.VideoPlay3s);
            var videoP50 = raws.Sum(x => x.VideoP50);

            s.AfterSpend = spend;
            s.AfterPurchases = (int)Math.Clamp(purchases, 0, int.MaxValue);
            s.AfterRoas = spend > 0 ? purchaseValue / spend : null;
            s.AfterHookRate = impressions > 0 ? video3s * 100m / impressions : null;
            s.AfterHoldRate = video3s > 0 ? videoP50 * 100m / video3s : null;

            var roasDelta = DeltaPct(s.BeforeRoas, s.AfterRoas);
            var hookDelta = DeltaPct(s.BeforeHookRate, s.AfterHookRate);
            var holdDelta = DeltaPct(s.BeforeHoldRate, s.AfterHoldRate);
            var spendDelta = DeltaPct(s.BeforeSpend, s.AfterSpend);
            var purchasesDiff = (s.AfterPurchases ?? 0) - (s.BeforePurchases ?? 0);

            var meaningful =
                AbsAtLeast(roasDelta, 10m)
                || AbsAtLeast(hookDelta, 10m)
                || AbsAtLeast(holdDelta, 10m)
                || AbsAtLeast(spendDelta, 20m)
                || Math.Abs(purchasesDiff) >= 1;

            if (!meaningful)
            {
                s.MetaChangeDetected = false;
                s.MetaChangeMessage =
                    "Öneriyi uygulandı olarak işaretlediniz ancak Meta tarafında anlamlı bir değişim tespit edilmedi. " +
                    "Lütfen Meta Ads Manager üzerinden değişiklikleri kontrol edin.";
            }
            else
            {
                s.MetaChangeDetected = true;
                s.MetaChangeMessage = null;
            }
            s.ImpactMeasuredAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Öneri etki ölçümü tamamlandı. Ölçülen kayıt: {Count}", candidates.Count);
    }

    private static decimal? DeltaPct(decimal? before, decimal? after)
    {
        if (before is null || after is null) return null;
        if (before.Value == 0m) return null;
        return ((after.Value - before.Value) / Math.Abs(before.Value)) * 100m;
    }

    private static bool AbsAtLeast(decimal? value, decimal threshold) =>
        value is not null && Math.Abs(value.Value) >= threshold;
}

