namespace Storykeeper.Api.Domain;

public enum CampaignFactStatus
{
    Proposed = 0,
    Active = 1,
    Resolved = 2,
    Superseded = 3,
    Discarded = 4
}

public sealed class CampaignFact : CampaignEntity
{
    public Guid? SourceSessionId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public CampaignFactStatus Status { get; set; } = CampaignFactStatus.Active;
    public int Importance { get; set; } = 3;
}
