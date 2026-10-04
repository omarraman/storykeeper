using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public interface ICampaignDraftGenerator
{
    Task<CampaignDraftContent> GenerateAsync(CampaignBrief brief, CancellationToken cancellationToken = default);
}
