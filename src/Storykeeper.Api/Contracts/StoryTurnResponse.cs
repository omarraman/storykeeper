using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record StoryTurnResponse(
    string Type,
    StoryBeatResponse? StoryBeat,
    AdventureStateResponse? State,
    RollRequiredResponse? RollRequired)
{
    public static StoryTurnResponse ForRoll(StoryTurnRollRequest request) =>
        new("roll_required", null, null, new RollRequiredResponse(
            request.Prompt!,
            request.Difficulty!,
            request.Strength,
            request.Risky));

    public static StoryTurnResponse ForStory(
        Guid storyBeatId,
        StoryTurnContent content,
        IReadOnlyList<CampaignFact> facts,
        Quest? quest,
        IReadOnlyList<Hero> heroes) =>
        new(
            "story_beat",
            new StoryBeatResponse(
                storyBeatId.ToString("N"),
                content.Narration!,
                content.Speaker,
                content.NpcDialogue!.Select(dialogue =>
                    new NpcDialogueResponse(dialogue!.NpcName!, dialogue.Text!)).ToArray(),
                content.Choices!.Select(choice =>
                    new StoryChoiceResponse(choice!.Id!, choice.Text!)).ToArray()),
            new AdventureStateResponse(
                "campaign",
                heroes.Select(hero => new HeroStatusResponse(
                    hero.Id,
                    hero.Name,
                    hero.Role,
                    hero.Hearts,
                    hero.SparkleTokens,
                    hero.Strengths,
                    hero.Inventory.OrderBy(item => item.Name).Select(item =>
                        new InventoryItemStatusResponse(item.Name, item.Description, item.Quantity)).ToArray())).ToArray(),
                quest is null ? null : new QuestStatusResponse(
                    quest.Title, quest.Description, quest.SessionLengthMinutes),
                facts.Select(fact => new ClueResponse(fact.Id, fact.Statement)).ToArray()),
            null);
}

public sealed record StoryBeatResponse(
    string Id,
    string Narration,
    string? Speaker,
    IReadOnlyList<NpcDialogueResponse> NpcDialogue,
    IReadOnlyList<StoryChoiceResponse> SuggestedChoices);

public sealed record NpcDialogueResponse(string NpcName, string Text);
public sealed record StoryChoiceResponse(string Id, string Text);
public sealed record RollRequiredResponse(string Prompt, string Difficulty, string? Strength, bool Risky);
public sealed record AdventureStateResponse(
    string Mode,
    IReadOnlyList<HeroStatusResponse> Heroes,
    QuestStatusResponse? CurrentQuest,
    IReadOnlyList<ClueResponse> Clues);
public sealed record HeroStatusResponse(
    Guid Id,
    string Name,
    string Role,
    int Hearts,
    int SparkleTokens,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<InventoryItemStatusResponse> Inventory);
public sealed record InventoryItemStatusResponse(string Name, string Description, int Quantity);
public sealed record QuestStatusResponse(string Title, string Description, int? SessionLengthMinutes);
public sealed record ClueResponse(Guid Id, string Text);
