namespace Storykeeper.Api.Domain;

public sealed class Session : CampaignEntity
{
    public int SessionNumber { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAtUtc { get; set; }
    public string? Summary { get; set; }
    public ICollection<CampaignFact> Facts { get; set; } = new List<CampaignFact>();
}
