namespace Storykeeper.Api.Domain;

public enum CampaignFactStatus
{
    Proposed,
    Confirmed,
    Superseded,
    Discarded
}

public sealed class CampaignFact : CampaignEntity
{
    public Guid? SourceSessionId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public CampaignFactStatus Status { get; set; } = CampaignFactStatus.Confirmed;
    public int Importance { get; set; } = 3;
}
