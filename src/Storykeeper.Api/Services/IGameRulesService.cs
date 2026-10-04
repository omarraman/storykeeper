using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public interface IGameRulesService
{
    Task<SessionStartResult> StartSessionAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<CheckResolution?> ResolveCheckAsync(
        Guid campaignId,
        Guid sessionId,
        Guid heroId,
        int roll,
        CheckDifficulty difficulty,
        string? strength,
        bool spendSparkleToken,
        bool risky,
        CancellationToken cancellationToken = default);

    Task<CheckResolution?> ResolveTestCheckAsync(
        Guid campaignId,
        Guid sessionId,
        Guid heroId,
        CheckDifficulty difficulty,
        string? strength,
        bool spendSparkleToken,
        bool risky,
        CancellationToken cancellationToken = default);
}

public sealed record SessionStartResult(Session? Session, IReadOnlyList<Hero> Heroes);
