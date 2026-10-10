namespace Storykeeper.Api.Domain;

public sealed class CampaignNarratorGuide : CampaignEntity
{
    public string? ActiveText { get; set; }
    public string PendingText { get; set; } = string.Empty;
    public bool HasPendingRevision { get; set; }
    public int ActiveRevision { get; set; }
    public int PendingRevision { get; set; }
    public DateTimeOffset? ActiveApprovedAtUtc { get; set; }
    public DateTimeOffset? PendingUpdatedAtUtc { get; set; }
}
