using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed record AdventureGenerationInput(
    string CampaignName,
    string Theme,
    string Tone,
    string WorldDescription,
    string? CurrentSituation,
    int SessionLengthMinutes,
    string? ParentPreferences,
    IReadOnlyList<string> ActiveFacts,
    IReadOnlyList<string> RecentSessionSummaries,
    IReadOnlyList<string> ExistingQuestTitles,
    IReadOnlyList<string> NpcNames,
    IReadOnlyList<string> LocationNames,
    ParentSafetySettings? SafetySettings = null);

public interface IAdventureDraftGenerator
{
    Task<AdventureDraftContent> GenerateAsync(
        AdventureGenerationInput input,
        CancellationToken cancellationToken = default);
}
