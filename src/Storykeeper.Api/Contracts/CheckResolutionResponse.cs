using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record CheckResolutionResponse(
    Guid Id,
    Guid SessionId,
    Guid HeroId,
    int Roll,
    string RollSource,
    string Difficulty,
    string? Strength,
    int StrengthBonus,
    int SparkleBonus,
    int Total,
    int Target,
    string Outcome,
    bool ForwardProgressRequired,
    string ConsequenceCategory,
    string ChildReadableMessage,
    bool SparkleTokenSpent,
    int HeartsBefore,
    int HeartsAfter,
    int SparkleTokensBefore,
    int SparkleTokensAfter,
    DateTimeOffset CreatedAtUtc)
{
    public static CheckResolutionResponse From(CheckResolution resolution) =>
        new(
            resolution.Id,
            resolution.SessionId,
            resolution.HeroId,
            resolution.Roll,
            resolution.RollSource.ToString(),
            resolution.Difficulty.ToString(),
            resolution.Strength,
            resolution.StrengthBonus,
            resolution.SparkleBonus,
            resolution.Total,
            resolution.Target,
            resolution.Outcome.ToString(),
            resolution.ForwardProgressRequired,
            resolution.ConsequenceCategory.ToString(),
            resolution.Outcome switch
            {
                CheckOutcome.StrongSuccess => "You did it, and found an extra helpful detail!",
                CheckOutcome.Success => "You did it!",
                CheckOutcome.SuccessWithComplication =>
                    "You make progress, and a small, fixable complication pops up.",
                CheckOutcome.SetbackWithProgress =>
                    "Things do not go as planned, but you still make progress: a clue or another way forward appears.",
                _ => throw new ArgumentOutOfRangeException(nameof(resolution), "Unknown check outcome.")
            },
            resolution.SparkleTokenSpent,
            resolution.HeartsBefore,
            resolution.HeartsAfter,
            resolution.SparkleTokensBefore,
            resolution.SparkleTokensAfter,
            resolution.CreatedAtUtc);
}
