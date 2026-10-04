namespace Storykeeper.Api.Domain;

public sealed class Location : CampaignEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
