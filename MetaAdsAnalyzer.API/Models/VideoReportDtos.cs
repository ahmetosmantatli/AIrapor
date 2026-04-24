namespace MetaAdsAnalyzer.API.Models;

public sealed class VideoReportAggregateRequestDto
{
    public int UserId { get; set; }

    public List<string> AdIds { get; set; } = new();

    public string? MetaAdAccountId { get; set; }
}

public sealed class VideoReportAggregateResponseDto
{
    public decimal Spend { get; set; }

    public long Impressions { get; set; }

    public long Reach { get; set; }

    public long LinkClicks { get; set; }

    public long Purchases { get; set; }

    public decimal PurchaseValue { get; set; }

    public long AddToCart { get; set; }

    public long InitiateCheckout { get; set; }

    public long VideoPlay3s { get; set; }

    public long VideoP25 { get; set; }

    public long VideoP50 { get; set; }

    public long VideoP75 { get; set; }

    public long VideoP100 { get; set; }

    public long ThruPlay { get; set; }

    public decimal CtrLinkPct { get; set; }

    public decimal? LinkCvrPct { get; set; }

    public decimal? ThumbstopPct { get; set; }

    public decimal? HoldPct { get; set; }

    public decimal? CompletionPct { get; set; }

    public decimal? Roas { get; set; }

    public decimal? Cpa { get; set; }

    public decimal? BreakEvenRoas { get; set; }

    public decimal? TargetRoas { get; set; }

    public decimal? MaxCpa { get; set; }

    public decimal? TargetCpa { get; set; }

    public bool HasProductMap { get; set; }

    public VideoReportDataQualityDto DataQuality { get; set; } = new();

    public int? CreativeScore { get; set; }

    public IReadOnlyList<string> NarrativeLines { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> ProblemTags { get; set; } = Array.Empty<string>();

    /// <summary>Ham insight satırı bulunduysa true (aksi halde UI boş kalmasın diye yine 200 döner).</summary>
    public bool HasInsightRows { get; set; }

    public string? DiagnosticMessage { get; set; }
}

public sealed class VideoReportDataQualityDto
{
    public bool InsufficientImpressions { get; set; }

    public bool LowPurchases { get; set; }

    public bool EarlyData { get; set; }

    public bool LearningPhase { get; set; }

    public bool InsufficientSpend { get; set; }

    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class VideoReportPdfRequestDto
{
    public int UserId { get; set; }

    public List<string> AdIds { get; set; } = new();

    public string? MetaAdAccountId { get; set; }

    public string? VideoId { get; set; }

    public string? DisplayName { get; set; }
}
