namespace Storykeeper.Api.Domain;

public sealed class StoryBeat : CampaignEntity
{
    public Guid SessionId { get; set; }
    public string Narration { get; set; } = string.Empty;
    public string? Action { get; set; }
    public Guid? ActingHeroId { get; set; }
    public string? NpcDialogueJson { get; set; }
    public Guid? CheckResolutionId { get; set; }
    public int? SequenceNumber { get; set; }
    public bool ChronologyEstimated { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
