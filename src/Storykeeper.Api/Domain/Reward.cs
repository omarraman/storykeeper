namespace Storykeeper.Api.Domain;

public sealed class Reward : CampaignEntity
{
    public Guid? HeroId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsClaimed { get; set; }
}
