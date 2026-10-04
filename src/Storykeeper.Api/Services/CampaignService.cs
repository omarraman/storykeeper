using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class CampaignService(ICampaignRepository campaignRepository) : ICampaignService
{
    public Task<Campaign> CreateAsync(
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateDetails(name, description);

        var now = DateTimeOffset.UtcNow;
        return campaignRepository.CreateAsync(new Campaign
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Settings = new CampaignSettings(),
            Bible = new CampaignBible(),
            Party = new Party()
        }, cancellationToken);
    }

    public Task<Campaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        campaignRepository.GetAsync(campaignId, cancellationToken);

    public Task<IReadOnlyList<Campaign>> ListAsync(CancellationToken cancellationToken = default) =>
        campaignRepository.ListAsync(cancellationToken);

    public Task<Campaign?> UpdateAsync(
        Guid campaignId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateDetails(name, description);

        return campaignRepository.UpdateAsync(new Campaign
        {
            Id = campaignId,
            Name = name.Trim(),
            Description = description?.Trim()
        }, cancellationToken);
    }

    public Task<bool> ArchiveAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        campaignRepository.ArchiveAsync(campaignId, DateTimeOffset.UtcNow, cancellationToken);

    public Task<bool> CompleteAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        campaignRepository.CompleteAsync(campaignId, DateTimeOffset.UtcNow, cancellationToken);

    public Task<bool> DeleteAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        campaignRepository.DeleteAsync(campaignId, cancellationToken);

    private static void ValidateDetails(string name, string? description)
    {
        if (name.Trim().Length > 120)
        {
            throw new ArgumentException("Campaign names cannot exceed 120 characters.", nameof(name));
        }

        if (description?.Trim().Length > 2000)
        {
            throw new ArgumentException("Campaign descriptions cannot exceed 2000 characters.", nameof(description));
        }
    }
}
