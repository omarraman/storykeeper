using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public enum AdventureDraftOperationStatus
{
    Succeeded,
    NotFound,
    Conflict,
    AlreadyActivated
}

public sealed record AdventureDraftOperationResult(AdventureDraftOperationStatus Status, AdventureDraft? Draft = null);

public sealed record AdventureDraftActivationResult(AdventureDraftOperationStatus Status, AdventureDraft? Draft = null);

public interface IAdventureDraftService
{
    Task<AdventureDraftOperationResult> GenerateAsync(
        Guid campaignId,
        AdventureDraftRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdventureDraft>?> ListAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<AdventureDraft?> GetAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<AdventureDraftOperationResult> RegenerateAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<AdventureDraftOperationResult> UpdateAsync(
        Guid draftId,
        AdventureDraftContent content,
        CancellationToken cancellationToken = default);
    Task<AdventureDraftOperationResult> ApproveAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<AdventureDraftActivationResult> ActivateAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<AdventureDraftOperationResult> DeleteAsync(Guid draftId, CancellationToken cancellationToken = default);
}
