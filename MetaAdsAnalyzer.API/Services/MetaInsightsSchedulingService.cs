using MetaAdsAnalyzer.API.Options;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MetaAdsAnalyzer.API.Services;

/// <summary>
/// Meta insights periyodik çekimi. Sadece <see cref="IMetaInsightsSyncService"/> ve EF’e bağlıdır.
/// </summary>
public sealed class MetaInsightsSchedulingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<MetaInsightsSchedulingOptions> _options;
    private readonly ILogger<MetaInsightsSchedulingService> _logger;

    private DateTime _lastTodayRunUtc = DateTime.MinValue;
    private DateOnly? _lastYesterdayRunDate;
    private DateOnly? _lastSummaryRunDate;

    public MetaInsightsSchedulingService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<MetaInsightsSchedulingOptions> options,
        ILogger<MetaInsightsSchedulingService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Meta insights zamanlayıcı başlatıldı (Enabled={Enabled}).", _options.CurrentValue.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            var delay = TimeSpan.FromSeconds(Math.Clamp(opts.TickSeconds, 15, 600));

            try
            {
                if (opts.Enabled)
                {
                    await RunScheduledWorkAsync(opts, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Meta insights zamanlayıcı tick sırasında hata");
            }

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunScheduledWorkAsync(MetaInsightsSchedulingOptions opts, CancellationToken ct)
    {
        var utc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(utc);

        if (utc - _lastTodayRunUtc >= TimeSpan.FromHours(Math.Max(0.5, opts.TodayIntervalHours)))
        {
            _logger.LogInformation("Zamanlanmış görev: bugünkü veri (preset={Preset})", opts.TodayDatePreset);
            await SyncAllEligibleUsersAsync(opts, opts.TodayDatePreset, ct).ConfigureAwait(false);
            _lastTodayRunUtc = utc;
        }

        if (IsInHourWindow(utc, opts.YesterdayRunUtcHour)
            && _lastYesterdayRunDate != today)
        {
            _logger.LogInformation("Zamanlanmış görev: dünkü veri (preset={Preset})", opts.YesterdayDatePreset);
            await SyncAllEligibleUsersAsync(opts, opts.YesterdayDatePreset, ct).ConfigureAwait(false);
            _lastYesterdayRunDate = today;
        }

        if (IsInHourWindow(utc, opts.SummaryRunUtcHour)
            && _lastSummaryRunDate != today)
        {
            foreach (var preset in opts.SummaryDatePresets ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(preset))
                {
                    continue;
                }

                _logger.LogInformation("Zamanlanmış görev: özet aralığı {Preset}", preset.Trim());
                await SyncAllEligibleUsersAsync(opts, preset.Trim(), ct).ConfigureAwait(false);
            }

            _lastSummaryRunDate = today;
        }
    }

    private static bool IsInHourWindow(DateTime utc, int hourUtc)
    {
        hourUtc = Math.Clamp(hourUtc, 0, 23);
        return utc.Hour == hourUtc && utc.Minute < 30;
    }

    private async Task SyncAllEligibleUsersAsync(MetaInsightsSchedulingOptions opts, string datePreset, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sync = scope.ServiceProvider.GetRequiredService<IMetaInsightsSyncService>();

        var userIds = await db.Users.AsNoTracking()
            .Where(u => u.MetaAccessToken != null && u.MetaAccessToken != "" && u.MetaAdAccountId != null && u.MetaAdAccountId != "")
            .Select(u => u.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var levels = opts.Levels is { Length: > 0 }
            ? opts.Levels
            : new[] { "campaign" };

        var delay = Math.Max(0, opts.DelayMsBetweenSyncCalls);

        foreach (var userId in userIds)
        {
            foreach (var level in levels)
            {
                if (string.IsNullOrWhiteSpace(level))
                {
                    continue;
                }

                try
                {
                    await sync.SyncInsightsAsync(userId, level.Trim(), datePreset, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Insights senkron başarısız UserId={UserId} Level={Level} Preset={Preset}", userId, level, datePreset);
                }

                if (delay > 0)
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }
    }
}
