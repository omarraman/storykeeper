namespace Storykeeper.Api.Domain;

public sealed class Npc : CampaignEntity
{
    public Guid? LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Disposition { get; set; } = string.Empty;
}
