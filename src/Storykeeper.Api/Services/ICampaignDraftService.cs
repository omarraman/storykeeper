using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public enum CampaignDraftOperationStatus
{
    Succeeded,
    NotFound,
    AlreadyActivated
}

public enum CampaignDraftActivationStatus
{
    Activated,
    NotFound,
    ApprovalRequired,
    AlreadyActivated
}

public sealed record CampaignDraftOperationResult(CampaignDraftOperationStatus Status, CampaignDraft? Draft = null);

public sealed record CampaignDraftActivationResult(
    CampaignDraftActivationStatus Status,
    Campaign? Campaign = null);

public interface ICampaignDraftService
{
    Task<CampaignDraft?> GenerateAsync(Guid briefId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampaignDraft>> ListAsync(CancellationToken cancellationToken = default);
    Task<CampaignDraft?> GetAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<CampaignDraftOperationResult> UpdateNarratorGuideAsync(
        Guid draftId,
        string? narratorGuide,
        CancellationToken cancellationToken = default);
    Task<CampaignDraftOperationResult> RegenerateAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<CampaignDraftOperationResult> UpdateAsync(
        Guid draftId,
        CampaignDraftContent content,
        CancellationToken cancellationToken = default);
    Task<CampaignDraftOperationResult> ApproveAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<CampaignDraftActivationResult> ActivateAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<CampaignDraftOperationResult> DeleteAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampaignBibleVersion>?> ListBibleVersionsAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);
}
