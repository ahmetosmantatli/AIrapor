namespace MetaAdsAnalyzer.API.Services;

public sealed class DirectiveEvaluateRequestDto
{
    public int UserId { get; set; }

    /// <summary>Doluysa yalnızca bu reklam varlıkları için direktif üretilir; diğer aktif direktiflere dokunulmaz.</summary>
    public List<string>? AdIds { get; set; }
}

public sealed class DirectiveEvaluateResultDto
{
    public int DirectivesCreated { get; set; }

    public int EntitiesEvaluated { get; set; }
}

public sealed class DirectiveListItemDto
{
    public int Id { get; set; }

    public string EntityId { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public string DirectiveType { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public string Message { get; set; } = null!;

    public int? Score { get; set; }

    public string? HealthStatus { get; set; }

    public DateTimeOffset TriggeredAt { get; set; }
}
