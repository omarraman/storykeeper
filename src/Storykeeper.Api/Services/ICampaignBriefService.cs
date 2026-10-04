using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public interface ICampaignBriefService
{
    Task<CampaignBrief> CreateAsync(CampaignBrief brief, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampaignBrief>> ListAsync(CancellationToken cancellationToken = default);
    Task<CampaignBrief?> GetAsync(Guid briefId, CancellationToken cancellationToken = default);
    Task<CampaignBrief?> UpdateAsync(Guid briefId, CampaignBrief brief, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid briefId, CancellationToken cancellationToken = default);
}
