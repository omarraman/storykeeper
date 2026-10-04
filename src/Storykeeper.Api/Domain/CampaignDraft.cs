namespace Storykeeper.Api.Domain;

public enum CampaignDraftStatus
{
    PendingReview,
    Approved,
    Activated
}

public sealed class CampaignDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignBriefId { get; set; }
    public CampaignBrief? CampaignBrief { get; set; }
    public Guid? CampaignId { get; set; }
    public CampaignDraftStatus Status { get; set; } = CampaignDraftStatus.PendingReview;
    public int GenerationNumber { get; set; } = 1;
    public string ContentJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAtUtc { get; set; }
}

public sealed record CampaignDraftContent(
    string? Title,
    string? Premise,
    string? CentralMystery,
    IReadOnlyList<string?>? WorldRules,
    IReadOnlyList<CampaignDraftNpc?>? Npcs,
    IReadOnlyList<CampaignDraftLocation?>? Locations,
    IReadOnlyList<CampaignDraftHook?>? AdventureHooks,
    CampaignDraftSafety? Safety);

public sealed record CampaignDraftNpc(string? Name, string? Description, string? Disposition, string? LocationName);

public sealed record CampaignDraftLocation(string? Name, string? Description);

public sealed record CampaignDraftHook(string? Title, string? Description);

public sealed record CampaignDraftSafety(
    bool LowFright,
    bool NoGoreOrCruelty,
    bool NoMatureThemes,
    bool NoPermanentCharacterDeath,
    bool NoMandatoryTacticalCombat);

public sealed class CampaignBibleVersion : CampaignEntity
{
    public int Version { get; set; }
    public Guid? SourceDraftId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public DateTimeOffset ActivatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
