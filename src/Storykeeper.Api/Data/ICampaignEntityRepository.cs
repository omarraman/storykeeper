using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Data;

public interface ICampaignEntityRepository
{
    Task<TEntity?> GetAsync<TEntity>(
        Guid campaignId,
        Guid entityId,
        CancellationToken cancellationToken = default)
        where TEntity : CampaignEntity;

    Task<TEntity> AddAsync<TEntity>(
        Guid campaignId,
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : CampaignEntity;
}
