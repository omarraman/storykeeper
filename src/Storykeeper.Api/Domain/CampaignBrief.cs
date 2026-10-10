namespace Storykeeper.Api.Domain;

public static class CampaignBriefDefaults
{
    public static IReadOnlyList<string> SafetyBoundaries { get; } = Array.AsReadOnly(new[]
    {
        "No gore or cruelty",
        "No mature themes",
        "No permanent character death",
        "No mandatory tactical combat",
        "Keep the adventure low-fright and age-appropriate"
    });
}

public sealed class CampaignBrief
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = "Cozy fantasy";
    public string Tone { get; set; } = "Warm, funny, and adventurous";
    public int CampaignLengthSessions { get; set; } = 6;
    public int SessionLengthMinutes { get; set; } = 45;
    public List<string> Inclusions { get; set; } = [];
    public List<string> Exclusions { get; set; } = [];
    public string? StoryIdea { get; set; }
    public string? NarratorGuide { get; set; }
    public List<string> SafetyBoundaries { get; set; } = CampaignBriefDefaults.SafetyBoundaries.ToList();
    public ParentSafetySettings SafetySettings { get; set; } = ParentSafetySettings.Defaults;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<CampaignDraft> Drafts { get; set; } = new List<CampaignDraft>();
}
