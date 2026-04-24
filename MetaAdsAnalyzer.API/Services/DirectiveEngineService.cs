using MetaAdsAnalyzer.API.Extensions;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Services;

public sealed class DirectiveEngineService : IDirectiveEngineService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DirectiveEngineService> _logger;

    public DirectiveEngineService(AppDbContext db, ILogger<DirectiveEngineService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DirectiveEvaluateResultDto> EvaluateForUserAsync(
        int userId,
        IReadOnlyList<string>? adEntityIds = null,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var activeMeta = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.MetaAdAccountId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (adEntityIds is { Count: > 0 })
        {
            var set = adEntityIds.ToHashSet(StringComparer.Ordinal);
            await _db.Directives
                .Where(
                    d => d.UserId == userId && d.IsActive && d.EntityType == "ad" && set.Contains(d.EntityId))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, false), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _db.Directives
                .Where(d => d.UserId == userId && d.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, false), cancellationToken)
                .ConfigureAwait(false);
        }

        var rawQ = _db.RawInsights.AsNoTracking().ForUserActiveAdAccount(userId, activeMeta);
        if (adEntityIds is { Count: > 0 })
        {
            var set = adEntityIds.ToHashSet(StringComparer.Ordinal);
            rawQ = rawQ.Where(r => r.Level == "ad" && set.Contains(r.EntityId));
        }

        var raws = await rawQ.ToListAsync(cancellationToken).ConfigureAwait(false);

        if (raws.Count == 0)
        {
            return new DirectiveEvaluateResultDto();
        }

        var rawIds = raws.Select(r => r.Id).ToList();
        var computeds = await _db.ComputedMetrics.AsNoTracking()
            .Where(c => rawIds.Contains(c.RawInsightId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bestByRaw = computeds
            .GroupBy(c => c.RawInsightId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ComputedAt).First());

        var pairs = new List<(RawInsight R, ComputedMetric C)>();
        foreach (var r in raws)
        {
            if (bestByRaw.TryGetValue(r.Id, out var c))
            {
                pairs.Add((r, c));
            }
        }

        var latestPerEntity = pairs
            .GroupBy(p => (Level: p.R.Level.ToLowerInvariant(), p.R.EntityId))
            .Select(g => g.MaxBy(x => x.R.FetchedAt))
            .ToList();

        var created = 0;
        foreach (var (r, c) in latestPerEntity)
        {
            IReadOnlyList<Directive> batch = r.Level.ToLowerInvariant() switch
            {
                "campaign" => DirectiveRules.EvaluateCampaign(userId, r, c, null, null),
                "adset" => DirectiveRules.EvaluateAdset(userId, r, c, null, null),
                "ad" => EvaluateAdBatch(userId, r, c),
                _ => Array.Empty<Directive>(),
            };

            foreach (var d in batch)
            {
                _db.Directives.Add(d);
                created++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Direktif değerlendirme tamamlandı UserId={UserId}, varlık={Entities}, direktif={Count}",
            userId,
            latestPerEntity.Count,
            created);

        return new DirectiveEvaluateResultDto
        {
            DirectivesCreated = created,
            EntitiesEvaluated = latestPerEntity.Count,
        };
    }

    public async Task<IReadOnlyList<DirectiveListItemDto>> GetActiveDirectivesAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var list = await _db.Directives.AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderBy(d => d.Severity == "critical" ? 0 : d.Severity == "warning" ? 1 : 2)
            .ThenByDescending(d => d.TriggeredAt)
            .Select(
                d => new DirectiveListItemDto
                {
                    Id = d.Id,
                    EntityId = d.EntityId,
                    EntityType = d.EntityType,
                    DirectiveType = d.DirectiveType,
                    Severity = d.Severity,
                    Message = d.Message,
                    Symptom = d.Symptom,
                    Reason = d.Reason,
                    Action = d.Action,
                    Score = d.Score,
                    HealthStatus = d.HealthStatus,
                    TriggeredAt = d.TriggeredAt,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return list;
    }

    private static IReadOnlyList<Directive> EvaluateAdBatch(int userId, RawInsight r, ComputedMetric c)
    {
        var (score, health, _, _, _) = AdCreativeScore.Compute(r, c);
        return DirectiveRules.EvaluateAd(userId, r, c, score, health);
    }
}
