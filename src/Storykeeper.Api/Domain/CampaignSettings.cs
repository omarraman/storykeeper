namespace Storykeeper.Api.Domain;

public sealed class CampaignSettings : CampaignEntity
{
    public string Theme { get; set; } = "Cozy Fantasy";
    public string Tone { get; set; } = "Warm, funny, and adventurous";
    public bool LowFright { get; set; } = true;
    public ParentSafetySettings SafetySettings { get; set; } = ParentSafetySettings.Defaults;
}
