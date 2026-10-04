using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Data;

public interface ICampaignRepository
{
    Task<Campaign> CreateAsync(Campaign campaign, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Campaign>> ListAsync(CancellationToken cancellationToken = default);
    Task<Campaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<Campaign?> UpdateAsync(Campaign campaign, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(Guid campaignId, DateTimeOffset completedAtUtc, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid campaignId, DateTimeOffset archivedAtUtc, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid campaignId, CancellationToken cancellationToken = default);
}
