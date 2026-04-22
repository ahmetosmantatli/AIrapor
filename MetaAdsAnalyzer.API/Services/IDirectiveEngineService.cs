namespace MetaAdsAnalyzer.API.Services;

public interface IDirectiveEngineService
{
    Task<DirectiveEvaluateResultDto> EvaluateForUserAsync(
        int userId,
        IReadOnlyList<string>? adEntityIds = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DirectiveListItemDto>> GetActiveDirectivesAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
