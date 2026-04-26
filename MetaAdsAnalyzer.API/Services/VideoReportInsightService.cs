using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.Core;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Services;

public sealed class VideoReportInsightService : IVideoReportInsightService
{
    private readonly AppDbContext _db;
    private readonly IDataQualityService _dataQualityService;
    private readonly ILogger<VideoReportInsightService> _logger;

    public VideoReportInsightService(
        AppDbContext db,
        IDataQualityService dataQualityService,
        ILogger<VideoReportInsightService> logger)
    {
        _db = db;
        _dataQualityService = dataQualityService;
        _logger = logger;
    }

    public async Task<VideoReportAggregateResponseDto> BuildAggregateAsync(
        int userId,
        string? metaAdAccountId,
        IReadOnlyList<string> adIds,
        CancellationToken cancellationToken = default)
    {
        var idList = adIds.Select(static x => x.Trim()).Where(static x => x.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (idList.Count == 0)
        {
            _logger.LogWarning("VideoReport aggregate: boş adIds UserId={UserId}", userId);
            return Empty("adIds boş veya geçersiz.");
        }

        var act = MetaAdAccountIdNormalizer.Normalize(metaAdAccountId);
        if (string.IsNullOrEmpty(act))
        {
            act = MetaAdAccountIdNormalizer.Normalize(
                await _db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.MetaAdAccountId)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false));
        }

        if (string.IsNullOrEmpty(act))
        {
            _logger.LogWarning("VideoReport aggregate: hesap kimliği yok UserId={UserId} AdCount={Count}", userId, idList.Count);
            return Empty("Reklam hesabı seçili değil.");
        }

        var cand = await _db.RawInsights.AsNoTracking()
            .Where(r => r.UserId == userId && r.Level == "ad" && idList.Contains(r.EntityId))
            .OrderByDescending(r => r.FetchedAt)
            .Take(4000)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "VideoReport aggregate: UserId={UserId} Act={Act} AdIds={AdCount} HamSatir={Rows} (hesap eşleşmesi öncesi)",
            userId,
            act,
            idList.Count,
            cand.Count);

        var latestByAd = new Dictionary<string, RawInsight>(StringComparer.Ordinal);
        foreach (var r in cand)
        {
            var acc = r.MetaAdAccountId?.Trim();
            if (string.IsNullOrEmpty(acc))
            {
                continue;
            }

            if (!string.Equals(acc, act, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!latestByAd.ContainsKey(r.EntityId))
            {
                latestByAd[r.EntityId] = r;
            }
        }

        if (latestByAd.Count == 0)
        {
            var sampleEntities = cand.Take(5).Select(x => $"{x.EntityId}:{x.MetaAdAccountId}").ToArray();
            _logger.LogWarning(
                "VideoReport aggregate: eşleşen satır yok UserId={UserId} BeklenenAct={Act} ÖrnekEntity={Sample}",
                userId,
                act,
                string.Join(", ", sampleEntities));
            return Empty(
                $"Seçilen reklamlar için bu hesapta ({act}) ham insight bulunamadı. Önce senkronu tamamlayın veya hesap kimliğinin eşleştiğini doğrulayın.");
        }

        var rawIds = latestByAd.Values.Select(x => x.Id).ToList();
        var comps = await _db.ComputedMetrics.AsNoTracking()
            .Where(c => rawIds.Contains(c.RawInsightId))
            .OrderByDescending(c => c.ComputedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bestByRaw = new Dictionary<int, ComputedMetric>();
        foreach (var c in comps)
        {
            if (!bestByRaw.ContainsKey(c.RawInsightId))
            {
                bestByRaw[c.RawInsightId] = c;
            }
        }

        decimal spend = 0;
        long impressions = 0;
        long reach = 0;
        long linkClicks = 0;
        long purchases = 0;
        decimal purchaseValue = 0;
        long addToCart = 0;
        long initiateCheckout = 0;
        long videoPlay3s = 0;
        long videoP25 = 0;
        long videoP50 = 0;
        long videoP75 = 0;
        long videoP100 = 0;
        long thruPlay = 0;
        decimal wCtr = 0;
        decimal wThumb = 0;
        decimal wHold = 0;
        decimal wComp = 0;
        decimal denThumb = 0;
        decimal denHold = 0;
        decimal denComp = 0;
        decimal wBe = 0;
        decimal wTg = 0;
        decimal denBe = 0;
        decimal denTg = 0;
        decimal wScore = 0;
        decimal denScore = 0;
        decimal wMaxCpa = 0;
        decimal denMaxCpa = 0;
        decimal wTargetCpa = 0;
        decimal denTargetCpa = 0;
        decimal wNetProfitPerOrder = 0;
        decimal denNetProfitPerOrder = 0;
        decimal wNetMarginPct = 0;
        decimal denNetMarginPct = 0;

        foreach (var raw in latestByAd.Values)
        {
            spend += raw.Spend;
            impressions += raw.Impressions;
            reach += raw.Reach;
            linkClicks += raw.LinkClicks;
            purchases += raw.Purchases;
            purchaseValue += raw.PurchaseValue;
            addToCart += raw.AddToCart;
            initiateCheckout += raw.InitiateCheckout;
            videoPlay3s += raw.VideoPlay3s;
            videoP25 += raw.VideoP25;
            videoP50 += raw.VideoP50;
            videoP75 += raw.VideoP75;
            videoP100 += raw.VideoP100;
            thruPlay += raw.VideoThruplay;

            if (raw.Spend > 0)
            {
                wCtr += raw.CtrLink * raw.Spend;
            }

            var thumb = ProfitMath.ThumbstopRatePct(raw.Reach, raw.VideoPlay3s)
                        ?? ProfitMath.HookRatePct(raw.Impressions, raw.VideoPlay3s);
            var holdPctAd = ProfitMath.HoldRatePct(raw.VideoPlay3s, raw.VideoThruplay);
            var comp = ProfitMath.CompletionRatePct(raw.Impressions, raw.VideoP100);

            if (raw.Spend > 0)
            {
                if (thumb is not null)
                {
                    wThumb += thumb.Value * raw.Spend;
                    denThumb += raw.Spend;
                }

                if (holdPctAd is not null)
                {
                    wHold += holdPctAd.Value * raw.Spend;
                    denHold += raw.Spend;
                }

                if (comp is not null)
                {
                    wComp += comp.Value * raw.Spend;
                    denComp += raw.Spend;
                }
            }

            if (bestByRaw.TryGetValue(raw.Id, out var c))
            {
                if (c.BreakEvenRoas is > 0m && raw.Spend > 0)
                {
                    wBe += c.BreakEvenRoas.Value * raw.Spend;
                    denBe += raw.Spend;
                }

                if (c.TargetRoas is > 0m && raw.Spend > 0)
                {
                    wTg += c.TargetRoas.Value * raw.Spend;
                    denTg += raw.Spend;
                }

                if (c.CreativeScoreTotal is { } sc && raw.Spend > 0)
                {
                    wScore += sc * raw.Spend;
                    denScore += raw.Spend;
                }

                if (c.MaxCpa is > 0m && raw.Spend > 0)
                {
                    wMaxCpa += c.MaxCpa.Value * raw.Spend;
                    denMaxCpa += raw.Spend;
                }

                if (c.TargetCpa is > 0m && raw.Spend > 0)
                {
                    wTargetCpa += c.TargetCpa.Value * raw.Spend;
                    denTargetCpa += raw.Spend;
                }

                if (c.NetProfitPerOrder is not null && raw.Spend > 0)
                {
                    wNetProfitPerOrder += c.NetProfitPerOrder.Value * raw.Spend;
                    denNetProfitPerOrder += raw.Spend;
                }

                if (c.NetMarginPct is not null && raw.Spend > 0)
                {
                    wNetMarginPct += c.NetMarginPct.Value * raw.Spend;
                    denNetMarginPct += raw.Spend;
                }
            }
        }

        var roas = spend > 0 ? purchaseValue / spend : (decimal?)null;
        var cpa = purchases > 0 ? spend / purchases : (decimal?)null;
        var ctrLink = spend > 0 ? wCtr / spend : 0m;
        var linkCvr = linkClicks > 0 ? (decimal)purchases / linkClicks * 100m : (decimal?)null;
        var thumbstop = denThumb > 0 ? wThumb / denThumb : (decimal?)null;
        var hold = denHold > 0 ? wHold / denHold : (decimal?)null;
        var completion = denComp > 0 ? wComp / denComp : (decimal?)null;
        var breakEven = denBe > 0 ? wBe / denBe : (decimal?)null;
        var target = denTg > 0 ? wTg / denTg : (decimal?)null;
        var maxCpa = denMaxCpa > 0 ? wMaxCpa / denMaxCpa : (decimal?)null;
        var targetCpa = denTargetCpa > 0 ? wTargetCpa / denTargetCpa : (decimal?)null;
        var netProfitPerOrder = denNetProfitPerOrder > 0 ? wNetProfitPerOrder / denNetProfitPerOrder : (decimal?)null;
        var netMarginPct = denNetMarginPct > 0 ? wNetMarginPct / denNetMarginPct : (decimal?)null;
        int? creativeScore = denScore > 0 ? (int)Math.Round(wScore / denScore, MidpointRounding.AwayFromZero) : null;
        var campaignIds = latestByAd.Values
            .Select(r => r.MetaCampaignId?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var hasProductMap = campaignIds.Count > 0
                            && await _db.CampaignProductMaps.AsNoTracking()
                                .AnyAsync(m => m.UserId == userId && campaignIds.Contains(m.CampaignId), cancellationToken)
                                .ConfigureAwait(false);
        var dq = _dataQualityService.Evaluate(latestByAd.Values.ToList(), learningPhase: false, dailyBudget: null);

        var narrative = VideoNarrativeBuilder.BuildNarrativeLines(
            thumbstop,
            hold,
            completion,
            roas,
            breakEven,
            target);
        var tags = VideoNarrativeBuilder.BuildProblemTags(
            thumbstop,
            hold,
            completion,
            roas,
            breakEven,
            target,
            ctrLink,
            linkCvr,
            linkClicks);

        _logger.LogInformation(
            "VideoReport aggregate OK UserId={UserId} Act={Act} Reklam={AdCount}",
            userId,
            act,
            latestByAd.Count);

        return new VideoReportAggregateResponseDto
        {
            HasInsightRows = true,
            Spend = spend,
            Impressions = impressions,
            Reach = reach,
            LinkClicks = linkClicks,
            Purchases = purchases,
            PurchaseValue = purchaseValue,
            AddToCart = addToCart,
            InitiateCheckout = initiateCheckout,
            VideoPlay3s = videoPlay3s,
            VideoP25 = videoP25,
            VideoP50 = videoP50,
            VideoP75 = videoP75,
            VideoP100 = videoP100,
            ThruPlay = thruPlay,
            CtrLinkPct = ctrLink,
            LinkCvrPct = linkCvr,
            ThumbstopPct = thumbstop,
            HoldPct = hold,
            CompletionPct = completion,
            Roas = roas,
            Cpa = cpa,
            BreakEvenRoas = breakEven,
            TargetRoas = target,
            MaxCpa = maxCpa,
            TargetCpa = targetCpa,
            NetProfitPerOrder = netProfitPerOrder,
            NetMarginPct = netMarginPct,
            HasProductMap = hasProductMap,
            DataQuality = new VideoReportDataQualityDto
            {
                InsufficientImpressions = dq.InsufficientImpressions,
                LowPurchases = dq.LowPurchases,
                EarlyData = dq.EarlyData,
                LearningPhase = dq.LearningPhase,
                InsufficientSpend = dq.InsufficientSpend,
                Warnings = dq.Warnings,
            },
            CreativeScore = creativeScore,
            NarrativeLines = narrative,
            ProblemTags = tags,
        };
    }

    private static VideoReportAggregateResponseDto Empty(string message) =>
        new()
        {
            HasInsightRows = false,
            DiagnosticMessage = message,
            NarrativeLines = Array.Empty<string>(),
            ProblemTags = Array.Empty<string>(),
        };
}
