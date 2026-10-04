namespace Storykeeper.Api.Domain;

public enum AdventureDraftStatus
{
    PendingReview,
    Approved,
    Activated
}

public enum AdventureArcType
{
    Standalone,
    SeasonArc
}

public sealed class AdventureDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public int SessionLengthMinutes { get; set; }
    public string? ParentPreferences { get; set; }
    public AdventureDraftStatus Status { get; set; } = AdventureDraftStatus.PendingReview;
    public int GenerationNumber { get; set; } = 1;
    public string ContentJson { get; set; } = "{}";
    public Guid? ActivatedQuestId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAtUtc { get; set; }
}

public sealed record AdventureDraftRequest(int SessionLengthMinutes, string? ParentPreferences);

public sealed record AdventureDraftContent(
    string? Title,
    string? Premise,
    AdventureArcType ArcType,
    string? ArcConnection,
    string? Opening,
    IReadOnlyList<AdventureScene?>? Scenes,
    IReadOnlyList<string?>? SolutionPaths,
    IReadOnlyList<AdventureClue?>? Clues,
    AdventureDraftNpc? FeaturedNpc,
    string? Finale,
    string? CelebrationReward);

public sealed record AdventureScene(string? Title, string? Description, string? NpcName);

public sealed record AdventureClue(string? Title, string? Description);

public sealed record AdventureDraftNpc(string? Name, string? Description, string? Disposition);
