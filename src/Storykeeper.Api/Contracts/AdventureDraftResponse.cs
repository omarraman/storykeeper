using System.Text.Json;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record AdventureDraftResponse(
    Guid Id,
    Guid CampaignId,
    int SessionLengthMinutes,
    string? ParentPreferences,
    string Status,
    int GenerationNumber,
    AdventureDraftContent Content,
    Guid? ActivatedQuestId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ActivatedAtUtc)
{
    public static AdventureDraftResponse From(AdventureDraft draft) => new(
        draft.Id,
        draft.CampaignId,
        draft.SessionLengthMinutes,
        draft.ParentPreferences,
        draft.Status.ToString(),
        draft.GenerationNumber,
        JsonSerializer.Deserialize<AdventureDraftContent>(draft.ContentJson, AdventureDraftJson.Options)
            ?? throw new JsonException("Stored adventure draft content was empty."),
        draft.ActivatedQuestId,
        draft.CreatedAtUtc,
        draft.UpdatedAtUtc,
        draft.ActivatedAtUtc);
}
