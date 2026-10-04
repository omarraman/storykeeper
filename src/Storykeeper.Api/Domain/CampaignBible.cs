namespace Storykeeper.Api.Domain;

public sealed class CampaignBible : CampaignEntity
{
    public string WorldDescription { get; set; } = string.Empty;
    public string? CurrentSituation { get; set; }
}
