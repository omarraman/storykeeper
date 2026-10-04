namespace Storykeeper.Api.Domain;

public sealed class Party : CampaignEntity
{
    public string Name { get; set; } = "Adventuring Party";
    public ICollection<Hero> Heroes { get; set; } = new List<Hero>();
}
