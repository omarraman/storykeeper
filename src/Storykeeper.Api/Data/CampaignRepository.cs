using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Data;

public sealed class CampaignRepository(StorykeeperDbContext dbContext) : ICampaignRepository
{
    public async Task<Campaign> CreateAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    public async Task<IReadOnlyList<Campaign>> ListAsync(CancellationToken cancellationToken = default)
    {
        var campaigns = await dbContext.Campaigns
            .Include(campaign => campaign.Party)
                .ThenInclude(party => party!.Heroes)
                    .ThenInclude(hero => hero.Inventory)
            .Include(campaign => campaign.Quests)
            .Include(campaign => campaign.Sessions)
            .ToListAsync(cancellationToken);
        return campaigns.OrderByDescending(campaign => campaign.UpdatedAtUtc).ToArray();
    }

    public Task<Campaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        dbContext.Campaigns
            .Include(campaign => campaign.Settings)
            .Include(campaign => campaign.Bible)
            .Include(campaign => campaign.Party)
                .ThenInclude(party => party!.Heroes)
                    .ThenInclude(hero => hero.Inventory)
            .Include(campaign => campaign.Quests)
            .Include(campaign => campaign.Sessions)
            .SingleOrDefaultAsync(campaign => campaign.Id == campaignId, cancellationToken);

    public async Task<Campaign?> UpdateAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Campaigns
            .SingleOrDefaultAsync(item => item.Id == campaign.Id, cancellationToken);

        if (existing is null || existing.Status != CampaignStatus.Active)
        {
            return null;
        }

        existing.Name = campaign.Name;
        existing.Description = campaign.Description;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> CompleteAsync(
        Guid campaignId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns
            .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);

        if (campaign is null || campaign.Status != CampaignStatus.Active)
        {
            return false;
        }

        campaign.Status = CampaignStatus.Completed;
        campaign.UpdatedAtUtc = completedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ArchiveAsync(
        Guid campaignId,
        DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns
            .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);

        if (campaign is null || campaign.Status == CampaignStatus.Archived)
        {
            return false;
        }

        campaign.Status = CampaignStatus.Archived;
        campaign.ArchivedAtUtc = archivedAtUtc;
        campaign.UpdatedAtUtc = archivedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns
            .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);

        if (campaign is null)
        {
            return false;
        }

        dbContext.Campaigns.Remove(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
