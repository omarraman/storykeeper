using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public interface ICampaignService
{
    Task<Campaign> CreateAsync(string name, string? description, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Campaign>> ListAsync(CancellationToken cancellationToken = default);
    Task<Campaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<Campaign?> UpdateAsync(
        Guid campaignId,
        string name,
        string? description,
        CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid campaignId, CancellationToken cancellationToken = default);
}
