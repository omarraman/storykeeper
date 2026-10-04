using Storykeeper.Api.Contracts;

namespace Storykeeper.Api.Services;

public interface ICampaignContinuityService
{
    Task<CampaignContinuityResponse?> GetAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<CampaignFactResponse?> GetFactAsync(
        Guid campaignId,
        Guid factId,
        CancellationToken cancellationToken = default);

    Task<CampaignFactResponse?> UpdateFactAsync(
        Guid campaignId,
        Guid factId,
        UpdateCampaignFactRequest request,
        CancellationToken cancellationToken = default);

    Task<CampaignFactResponse?> CreateFactAsync(
        Guid campaignId,
        CreateCampaignFactRequest request,
        CancellationToken cancellationToken = default);

    Task<SessionSummaryResponse?> SaveSummaryAsync(
        Guid campaignId,
        Guid sessionId,
        SaveSessionSummaryRequest request,
        CancellationToken cancellationToken = default);
}
