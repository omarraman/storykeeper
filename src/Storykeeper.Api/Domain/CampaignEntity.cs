namespace Storykeeper.Api.Domain;

public abstract class CampaignEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
}
