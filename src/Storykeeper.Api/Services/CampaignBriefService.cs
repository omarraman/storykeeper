using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class CampaignBriefService(StorykeeperDbContext dbContext) : ICampaignBriefService
{
    public async Task<CampaignBrief> CreateAsync(CampaignBrief brief, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        brief.CreatedAtUtc = now;
        brief.UpdatedAtUtc = now;
        brief.SafetyBoundaries = CampaignBriefDefaults.SafetyBoundaries.ToList();
        dbContext.CampaignBriefs.Add(brief);
        await dbContext.SaveChangesAsync(cancellationToken);
        return brief;
    }

    public async Task<IReadOnlyList<CampaignBrief>> ListAsync(CancellationToken cancellationToken = default)
    {
        var briefs = await dbContext.CampaignBriefs
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        return briefs.OrderByDescending(brief => brief.UpdatedAtUtc).ToArray();
    }

    public Task<CampaignBrief?> GetAsync(Guid briefId, CancellationToken cancellationToken = default) =>
        dbContext.CampaignBriefs.AsNoTracking()
            .SingleOrDefaultAsync(brief => brief.Id == briefId, cancellationToken);

    public async Task<CampaignBrief?> UpdateAsync(
        Guid briefId,
        CampaignBrief brief,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.CampaignBriefs
            .SingleOrDefaultAsync(item => item.Id == briefId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.Title = brief.Title;
        existing.Genre = brief.Genre;
        existing.Tone = brief.Tone;
        existing.CampaignLengthSessions = brief.CampaignLengthSessions;
        existing.SessionLengthMinutes = brief.SessionLengthMinutes;
        existing.Inclusions = brief.Inclusions;
        existing.Exclusions = brief.Exclusions;
        existing.StoryIdea = brief.StoryIdea;
        existing.SafetyBoundaries = CampaignBriefDefaults.SafetyBoundaries.ToList();
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid briefId, CancellationToken cancellationToken = default)
    {
        var brief = await dbContext.CampaignBriefs
            .SingleOrDefaultAsync(item => item.Id == briefId, cancellationToken);
        if (brief is null)
        {
            return false;
        }

        dbContext.CampaignBriefs.Remove(brief);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
