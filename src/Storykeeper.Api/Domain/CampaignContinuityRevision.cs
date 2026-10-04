namespace Storykeeper.Api.Domain;

public enum ContinuityRecordType
{
    Fact,
    Summary
}

public sealed class CampaignContinuityRevision : CampaignEntity
{
    public ContinuityRecordType RecordType { get; set; }
    public Guid RecordId { get; set; }
    public Guid? SourceSessionId { get; set; }
    public string? PreviousContent { get; set; }
    public string NewContent { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = "Parent";
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
