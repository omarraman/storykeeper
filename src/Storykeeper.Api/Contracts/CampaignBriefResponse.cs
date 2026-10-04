using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record CampaignBriefResponse(
    Guid Id,
    string Title,
    string Genre,
    string Tone,
    int CampaignLengthSessions,
    int SessionLengthMinutes,
    IReadOnlyList<string> Inclusions,
    IReadOnlyList<string> Exclusions,
    string? StoryIdea,
    IReadOnlyList<string> SafetyBoundaries,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static CampaignBriefResponse From(CampaignBrief brief) => new(
        brief.Id,
        brief.Title,
        brief.Genre,
        brief.Tone,
        brief.CampaignLengthSessions,
        brief.SessionLengthMinutes,
        brief.Inclusions,
        brief.Exclusions,
        brief.StoryIdea,
        brief.SafetyBoundaries,
        brief.CreatedAtUtc,
        brief.UpdatedAtUtc);
}
