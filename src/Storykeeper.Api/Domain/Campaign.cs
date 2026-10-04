namespace Storykeeper.Api.Domain;

public enum CampaignStatus
{
    Active,
    Archived
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
}
