using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Data;

public sealed class CampaignEntityRepository(StorykeeperDbContext dbContext) : ICampaignEntityRepository
{
    public Task<TEntity?> GetAsync<TEntity>(
        Guid campaignId,
        Guid entityId,
        CancellationToken cancellationToken = default)
        where TEntity : CampaignEntity =>
        dbContext.Set<TEntity>()
            .SingleOrDefaultAsync(
                entity => entity.CampaignId == campaignId && entity.Id == entityId,
                cancellationToken);

    public async Task<TEntity> AddAsync<TEntity>(
        Guid campaignId,
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : CampaignEntity
    {
        if (entity.CampaignId != campaignId)
        {
            throw new ArgumentException("The entity must belong to the specified campaign.", nameof(entity));
        }

        if (entity is Relationship relationship &&
            (!await ParticipantExistsAsync(campaignId, relationship.SubjectType, relationship.SubjectId, cancellationToken) ||
             !await ParticipantExistsAsync(campaignId, relationship.TargetType, relationship.TargetId, cancellationToken)))
        {
            throw new ArgumentException(
                "Both relationship participants must belong to the specified campaign.",
                nameof(entity));
        }

        dbContext.Set<TEntity>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private Task<bool> ParticipantExistsAsync(
        Guid campaignId,
        RelationshipParticipantType participantType,
        Guid participantId,
        CancellationToken cancellationToken) =>
        participantType switch
        {
            RelationshipParticipantType.Hero => dbContext.Heroes.AnyAsync(
                hero => hero.CampaignId == campaignId && hero.Id == participantId,
                cancellationToken),
            RelationshipParticipantType.Npc => dbContext.Npcs.AnyAsync(
                npc => npc.CampaignId == campaignId && npc.Id == participantId,
                cancellationToken),
            _ => Task.FromResult(false)
        };
}
