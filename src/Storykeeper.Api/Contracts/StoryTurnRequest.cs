namespace Storykeeper.Api.Contracts;

public sealed record StoryTurnRequest(
    string? Action,
    Guid? SessionId,
    Guid? HeroId,
    Guid? CheckResolutionId = null);

public sealed record StoryTurnContent(
    string? Narration,
    string? Speaker,
    IReadOnlyList<StoryTurnDialogue?>? NpcDialogue,
    StoryTurnRollRequest? RollRequest,
    IReadOnlyList<StoryTurnChoice?>? Choices,
    IReadOnlyList<StoryTurnFactProposal?>? ProposedFacts);

public sealed record StoryTurnDialogue(string? NpcName, string? Text);
public sealed record StoryTurnRollRequest(string? Prompt, string? Difficulty, string? Strength, bool Risky);
public sealed record StoryTurnChoice(string? Id, string? Text);
public sealed record StoryTurnFactProposal(string? Category, string? Statement, int Importance);
