namespace MetaAdsAnalyzer.API.Services;

public sealed class InsightsSyncRequestDto
{
    public int UserId { get; set; }

    /// <summary>campaign | adset | ad</summary>
    public string Level { get; set; } = "campaign";

    /// <summary>Örn. last_7d, last_14d, last_30d, yesterday, today</summary>
    public string DatePreset { get; set; } = "last_7d";
}

public sealed class InsightsSyncResponseDto
{
    public int RowsFetched { get; set; }

    public int RowsUpserted { get; set; }

    public int PageCount { get; set; }
}

public sealed class MetaAdAccountItemDto
{
    public string Id { get; init; } = null!;

    public string? Name { get; init; }

    public string? AccountId { get; init; }
}
