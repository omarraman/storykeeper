namespace Storykeeper.Api.Domain;

public enum CampaignStatus
{
    Active = 0,
    Archived = 1,
    Completed = 2
}

public sealed class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Active;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ArchivedAtUtc { get; set; }
    public CampaignSettings? Settings { get; set; }
    public CampaignBible? Bible { get; set; }
    public Party? Party { get; set; }
    public ICollection<Quest> Quests { get; set; } = new List<Quest>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Npc> Npcs { get; set; } = new List<Npc>();
    public ICollection<Location> Locations { get; set; } = new List<Location>();
}
