namespace MetaAdsAnalyzer.API.Models;

public sealed class SavedReportCreateRequestDto
{
    public int UserId { get; set; }
    public string AdId { get; set; } = null!;
    public string? AdName { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public string? AdsetId { get; set; }
    public string? AdsetName { get; set; }
    public decimal? AggregateRoas { get; set; }
    public decimal? AggregateHookRate { get; set; }
    public decimal? AggregateHoldRate { get; set; }
    public decimal? AggregateSpend { get; set; }
    public int? AggregatePurchases { get; set; }
    public List<SavedReportSuggestionCreateDto> Suggestions { get; set; } = new();
}

public sealed class SavedReportSuggestionCreateDto
{
    public string SuggestionKey { get; set; } = null!;
    public string? DirectiveType { get; set; }
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = null!;
    public string? Symptom { get; set; }
    public string? Reason { get; set; }
    public string? Action { get; set; }
}

public sealed class SavedReportSuggestionUpdateRequestDto
{
    public string Status { get; set; } = null!; // applied | skipped
}

public sealed class SavedReportListItemDto
{
    public int Id { get; set; }
    public string AdId { get; set; } = null!;
    public string? AdName { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public string? AdsetId { get; set; }
    public string? AdsetName { get; set; }
    public DateTimeOffset AnalyzedAt { get; set; }
    public decimal? AggregateRoas { get; set; }
    public decimal? AggregateHookRate { get; set; }
    public decimal? AggregateHoldRate { get; set; }
    public decimal? AggregateSpend { get; set; }
    public int? AggregatePurchases { get; set; }
    public List<SavedReportSuggestionDto> Suggestions { get; set; } = new();
}

public sealed class SavedReportSuggestionDto
{
    public int Id { get; set; }
    public string SuggestionKey { get; set; } = null!;
    public string? DirectiveType { get; set; }
    public string Severity { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Symptom { get; set; }
    public string? Reason { get; set; }
    public string? Action { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public DateTimeOffset? SkippedAt { get; set; }
    public decimal? BeforeRoas { get; set; }
    public decimal? BeforeHookRate { get; set; }
    public decimal? BeforeHoldRate { get; set; }
    public decimal? BeforeSpend { get; set; }
    public int? BeforePurchases { get; set; }
    public decimal? AfterRoas { get; set; }
    public decimal? AfterHookRate { get; set; }
    public decimal? AfterHoldRate { get; set; }
    public decimal? AfterSpend { get; set; }
    public int? AfterPurchases { get; set; }
    public DateTimeOffset? ImpactMeasuredAt { get; set; }
    public bool MetaChangeDetected { get; set; }
    public string? MetaChangeMessage { get; set; }
}

public class SavedReportImpactFeedItemDto
{
    public int SuggestionId { get; set; }
    public int SavedReportId { get; set; }
    public string AdId { get; set; } = null!;
    public string? AdName { get; set; }
    public string? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public string? AdsetId { get; set; }
    public string? AdsetName { get; set; }
    public DateTimeOffset AppliedAt { get; set; }
    public DateTimeOffset? ImpactMeasuredAt { get; set; }
    public decimal? BeforeRoas { get; set; }
    public decimal? AfterRoas { get; set; }
    public decimal? BeforeSpend { get; set; }
    public decimal? AfterSpend { get; set; }
    public int? BeforePurchases { get; set; }
    public int? AfterPurchases { get; set; }
    public decimal? BeforeHookRate { get; set; }
    public decimal? AfterHookRate { get; set; }
    public decimal? BeforeHoldRate { get; set; }
    public decimal? AfterHoldRate { get; set; }
    public string? DirectiveType { get; set; }
    public string? Severity { get; set; }
    public string? Message { get; set; }
    public string? Symptom { get; set; }
    public string? Reason { get; set; }
    public string? Action { get; set; }
    public bool MetaChangeDetected { get; set; }
    public string? MetaChangeMessage { get; set; }
}

public sealed class SavedReportImpactDetailDto : SavedReportImpactFeedItemDto
{
    public DateTimeOffset AnalyzedAt { get; set; }
}

