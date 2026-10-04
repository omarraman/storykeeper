using System.Text.Json;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record CampaignDraftResponse(
    Guid Id,
    Guid CampaignBriefId,
    Guid? CampaignId,
    string Status,
    int GenerationNumber,
    CampaignDraftContent Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ActivatedAtUtc)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static CampaignDraftResponse From(CampaignDraft draft) => new(
        draft.Id,
        draft.CampaignBriefId,
        draft.CampaignId,
        draft.Status.ToString(),
        draft.GenerationNumber,
        JsonSerializer.Deserialize<CampaignDraftContent>(draft.ContentJson, JsonOptions)
            ?? throw new JsonException("Stored campaign draft content was empty."),
        draft.CreatedAtUtc,
        draft.UpdatedAtUtc,
        draft.ActivatedAtUtc);
}

public sealed record CampaignBibleVersionResponse(
    Guid Id,
    Guid CampaignId,
    int Version,
    Guid? SourceDraftId,
    string Title,
    CampaignDraftContent Content,
    DateTimeOffset ActivatedAtUtc)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static CampaignBibleVersionResponse From(CampaignBibleVersion version) => new(
        version.Id,
        version.CampaignId,
        version.Version,
        version.SourceDraftId,
        version.Title,
        JsonSerializer.Deserialize<CampaignDraftContent>(version.ContentJson, JsonOptions)
            ?? throw new JsonException("Stored campaign bible version content was empty."),
        version.ActivatedAtUtc);
}
