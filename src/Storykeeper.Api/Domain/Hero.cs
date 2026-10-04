namespace Storykeeper.Api.Domain;

public sealed class Hero : CampaignEntity
{
    public Guid PartyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = [];
    public int Hearts { get; set; } = 3;
    public int SparkleTokens { get; set; }
    public ICollection<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
}
