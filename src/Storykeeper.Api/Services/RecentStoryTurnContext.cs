using System.Text.Json;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

internal sealed record RecentStoryTurnContext(
    string? Action,
    RecentTurnHero? ActingHero,
    string Narration,
    IReadOnlyList<StoryTurnDialogue> NpcDialogue,
    RecentTurnCheck? ResolvedCheck,
    bool Truncated,
    bool ChronologyEstimated);

internal sealed record RecentTurnHero(Guid Id, string? Name);

internal sealed record RecentTurnCheck(
    int Roll,
    string Difficulty,
    int Total,
    int Target,
    string Outcome,
    bool ForwardProgressRequired,
    string Consequence,
    int HeartsAfter,
    int SparkleTokensAfter,
    string? Strength);

internal static class RecentStoryTurnContextBuilder
{
    public static IReadOnlyList<RecentStoryTurnContext> Build(
        IReadOnlyList<StoryBeat> chronologicalBeats,
        IReadOnlyDictionary<Guid, Hero> heroes,
        IReadOnlyDictionary<Guid, CheckResolution> checks,
        int characterBudget)
    {
        var newestFirst = new List<RecentStoryTurnContext>();
        var remaining = characterBudget;
        for (var index = chronologicalBeats.Count - 1; index >= 0; index--)
        {
            var beat = chronologicalBeats[index];
            var turn = Create(beat, heroes, checks);
            var textLength = GetTextLength(turn);
            if (textLength <= remaining)
            {
                newestFirst.Add(turn);
                remaining -= textLength;
                continue;
            }

            if (newestFirst.Count == 0)
            {
                newestFirst.Add(Truncate(turn, characterBudget));
            }

            break;
        }

        newestFirst.Reverse();
        return newestFirst;
    }

    private static RecentStoryTurnContext Create(
        StoryBeat beat,
        IReadOnlyDictionary<Guid, Hero> heroes,
        IReadOnlyDictionary<Guid, CheckResolution> checks)
    {
        var dialogue = beat.NpcDialogueJson is null
            ? Array.Empty<StoryTurnDialogue>()
            : JsonSerializer.Deserialize<StoryTurnDialogue[]>(
                beat.NpcDialogueJson, AdventureDraftJson.Options) ?? [];
        var actingHero = beat.ActingHeroId is { } heroId
            ? new RecentTurnHero(heroId, heroes.GetValueOrDefault(heroId)?.Name)
            : null;
        var check = beat.CheckResolutionId is { } checkId && checks.TryGetValue(checkId, out var resolution)
            ? ToContext(resolution)
            : null;

        return new RecentStoryTurnContext(
            beat.Action,
            actingHero,
            beat.Narration,
            dialogue,
            check,
            false,
            beat.ChronologyEstimated || beat.SequenceNumber is null);
    }

    private static RecentTurnCheck ToContext(CheckResolution check) => new(
        check.Roll,
        check.Difficulty.ToString(),
        check.Total,
        check.Target,
        check.Outcome.ToString(),
        check.ForwardProgressRequired,
        check.ConsequenceCategory.ToString(),
        check.HeartsAfter,
        check.SparkleTokensAfter,
        check.Strength);

    private static RecentStoryTurnContext Truncate(RecentStoryTurnContext turn, int characterBudget)
    {
        var checkLength = turn.ResolvedCheck is null ? 0 : GetCheckTextLength(turn.ResolvedCheck);
        var textBudget = characterBudget - checkLength;
        var mainBudget = Math.Max(2, textBudget * 3 / 4);
        var actionQuota = turn.Action is null ? 0 : Math.Max(1, mainBudget / 3);
        var action = TakePrefix(turn.Action, actionQuota);
        var narration = TakePrefix(turn.Narration, mainBudget - action.Length);
        var remaining = textBudget - action.Length - narration.Length;
        var heroName = TakePrefix(turn.ActingHero?.Name, remaining);
        remaining -= heroName.Length;

        var dialogue = new List<StoryTurnDialogue>();
        foreach (var line in turn.NpcDialogue)
        {
            if (remaining == 0)
            {
                break;
            }

            var name = TakePrefix(line.NpcName, Math.Max(1, remaining / 3));
            remaining -= name.Length;
            var text = TakePrefix(line.Text, remaining);
            remaining -= text.Length;
            dialogue.Add(new StoryTurnDialogue(name, text));
        }

        return turn with
        {
            Action = turn.Action is null ? null : action,
            ActingHero = turn.ActingHero is null ? null : turn.ActingHero with { Name = heroName },
            Narration = narration,
            NpcDialogue = dialogue,
            Truncated = true
        };
    }

    private static int GetTextLength(RecentStoryTurnContext turn) =>
        (turn.Action?.Length ?? 0) +
        (turn.ActingHero?.Name?.Length ?? 0) +
        turn.Narration.Length +
        turn.NpcDialogue.Sum(line => (line.NpcName?.Length ?? 0) + (line.Text?.Length ?? 0)) +
        (turn.ResolvedCheck is null ? 0 : GetCheckTextLength(turn.ResolvedCheck));

    private static int GetCheckTextLength(RecentTurnCheck check) =>
        check.Difficulty.Length + check.Outcome.Length + check.Consequence.Length +
        (check.Strength?.Length ?? 0);

    private static string TakePrefix(string? value, int maximum) =>
        string.IsNullOrEmpty(value) || maximum <= 0
            ? string.Empty
            : value.Length <= maximum ? value : value[..maximum];
}
