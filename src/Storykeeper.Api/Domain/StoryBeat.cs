namespace Storykeeper.Api.Domain;

public sealed class StoryBeat : CampaignEntity
{
    public Guid SessionId { get; set; }
    public string Narration { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
