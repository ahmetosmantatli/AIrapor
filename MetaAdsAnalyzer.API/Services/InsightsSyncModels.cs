namespace MetaAdsAnalyzer.API.Services;

public sealed class InsightsSyncRequestDto
{
    public int UserId { get; set; }

    /// <summary>campaign | adset | ad</summary>
    public string Level { get; set; } = "campaign";

    /// <summary>Örn. last_7d, last_14d, last_30d, yesterday, today</summary>
    public string DatePreset { get; set; } = "last_7d";

    /// <summary>
    /// Doluysa yalnızca bu reklam için <c>level=ad</c> insights çekilir (hesap düzeyinde filtre).
    /// </summary>
    public string? AdId { get; set; }

    /// <summary>
    /// Birden fazla reklam kimliği (yalnızca rakam). <see cref="AdId"/> yerine veya onunla birlikte kullanılabilir;
    /// birleşik filtre için tüm kimlikler tek Graph isteğinde <c>IN</c> operatörüyle gönderilir.
    /// </summary>
    public List<string>? AdIds { get; set; }

    /// <summary>İsteğe bağlı <c>act_…</c>; kullanıcının bağlı hesaplarından biri olmalıdır.</summary>
    public string? MetaAdAccountId { get; set; }
}

public sealed class InsightsRefreshRequestDto
{
    public int UserId { get; set; }

    public string? MetaAdAccountId { get; set; }
}

public sealed class InsightsSyncResponseDto
{
    public int RowsFetched { get; set; }

    public int RowsUpserted { get; set; }

    public int PageCount { get; set; }
}

public sealed class InsightsRefreshResponseDto
{
    public string Status { get; set; } = "ok";

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset? LastSync { get; set; }

    public int? DailyCount { get; set; }
}

public sealed class MetaAdAccountItemDto
{
    public string Id { get; init; } = null!;

    public string? Name { get; init; }

    public string? AccountId { get; init; }
}

/// <summary>Graph <c>act_…/campaigns</c> satırı.</summary>
public sealed class MetaCampaignListItemDto
{
    public string Id { get; init; } = null!;

    public string? Name { get; init; }

    public string? Status { get; init; }

    public string? Objective { get; init; }
}

/// <summary>Graph <c>{campaign_id}/adsets</c> satırı.</summary>
public sealed class MetaAdsetListItemDto
{
    public string Id { get; init; } = null!;

    public string? Name { get; init; }

    public string? Status { get; init; }

    public string? CampaignId { get; init; }
}

/// <summary>Graph <c>act_…/ads</c> satırı; video metrikleri için reklam seçiminde kullanılır.</summary>
public sealed class MetaAdListItemDto
{
    public string Id { get; init; } = null!;

    public string? Name { get; init; }

    public string? Status { get; init; }

    public string? EffectiveStatus { get; init; }

    public string? CreativeId { get; init; }

    public string? CreativeName { get; init; }

    public string? VideoId { get; init; }

    public string? VideoTitle { get; init; }

    public string? ThumbnailUrl { get; init; }
}
