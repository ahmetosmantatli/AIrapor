namespace MetaAdsAnalyzer.API.Services;

public interface IDirectiveEngineService
{
    Task<DirectiveEvaluateResultDto> EvaluateForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DirectiveListItemDto>> GetActiveDirectivesAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
