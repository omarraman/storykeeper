using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public enum NarratorGuideOperationStatus
{
    Succeeded,
    NotFound,
    SessionRunning,
    NoPendingRevision
}

public sealed record NarratorGuideOperationResult(
    NarratorGuideOperationStatus Status,
    CampaignNarratorGuide? Guide = null);

public sealed class CampaignNarratorGuideService(StorykeeperDbContext dbContext)
{
    public async Task<CampaignNarratorGuide?> GetAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Campaigns.AnyAsync(campaign => campaign.Id == campaignId, cancellationToken))
        {
            return null;
        }

        return await dbContext.CampaignNarratorGuides.AsNoTracking()
            .SingleOrDefaultAsync(guide => guide.CampaignId == campaignId, cancellationToken);
    }

    public Task<bool> CampaignExistsAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        dbContext.Campaigns.AnyAsync(campaign => campaign.Id == campaignId, cancellationToken);

    public async Task<NarratorGuideOperationResult> SavePendingAsync(
        Guid campaignId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns.SingleOrDefaultAsync(
            item => item.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            return new NarratorGuideOperationResult(NarratorGuideOperationStatus.NotFound);
        }

        if (text.Length > NarratorGuideLimits.MaximumCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(text),
                $"The full narrator guide cannot exceed {NarratorGuideLimits.MaximumCharacters} characters.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Enter guide text. Use the explicit removal control to remove an active guide.", nameof(text));
        }

        if (await HasRunningSessionAsync(campaignId, cancellationToken))
        {
            return new NarratorGuideOperationResult(NarratorGuideOperationStatus.SessionRunning);
        }

        var guide = await dbContext.CampaignNarratorGuides
            .SingleOrDefaultAsync(item => item.CampaignId == campaignId, cancellationToken);
        if (guide is null)
        {
            guide = new CampaignNarratorGuide
            {
                CampaignId = campaignId,
                PendingText = text,
                PendingRevision = 1,
                HasPendingRevision = true,
                PendingUpdatedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.CampaignNarratorGuides.Add(guide);
        }
        else
        {
            guide.PendingText = text;
            guide.PendingRevision = Math.Max(guide.ActiveRevision, guide.PendingRevision) + 1;
            guide.HasPendingRevision = true;
            guide.PendingUpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new NarratorGuideOperationResult(NarratorGuideOperationStatus.Succeeded, guide);
    }

    public async Task<NarratorGuideOperationResult> ApproveAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        var guide = await dbContext.CampaignNarratorGuides
            .SingleOrDefaultAsync(item => item.CampaignId == campaignId, cancellationToken);
        if (guide is null)
        {
            return new NarratorGuideOperationResult(
                await dbContext.Campaigns.AnyAsync(item => item.Id == campaignId, cancellationToken)
                    ? NarratorGuideOperationStatus.NoPendingRevision
                    : NarratorGuideOperationStatus.NotFound);
        }

        if (await HasRunningSessionAsync(campaignId, cancellationToken))
        {
            return new NarratorGuideOperationResult(NarratorGuideOperationStatus.SessionRunning, guide);
        }

        if (!guide.HasPendingRevision)
        {
            return new NarratorGuideOperationResult(NarratorGuideOperationStatus.NoPendingRevision, guide);
        }

        guide.ActiveText = string.IsNullOrWhiteSpace(guide.PendingText) ? null : guide.PendingText;
        guide.ActiveRevision = guide.PendingRevision;
        guide.ActiveApprovedAtUtc = DateTimeOffset.UtcNow;
        guide.PendingText = string.Empty;
        guide.HasPendingRevision = false;
        guide.PendingUpdatedAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new NarratorGuideOperationResult(NarratorGuideOperationStatus.Succeeded, guide);
    }

    public async Task<NarratorGuideOperationResult> RemoveAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Campaigns.AnyAsync(campaign => campaign.Id == campaignId, cancellationToken))
        {
            return new NarratorGuideOperationResult(NarratorGuideOperationStatus.NotFound);
        }

        if (await HasRunningSessionAsync(campaignId, cancellationToken))
        {
            return new NarratorGuideOperationResult(NarratorGuideOperationStatus.SessionRunning);
        }

        var guide = await dbContext.CampaignNarratorGuides
            .SingleOrDefaultAsync(item => item.CampaignId == campaignId, cancellationToken);
        if (guide is not null)
        {
            dbContext.CampaignNarratorGuides.Remove(guide);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new NarratorGuideOperationResult(NarratorGuideOperationStatus.Succeeded);
    }

    private Task<bool> HasRunningSessionAsync(Guid campaignId, CancellationToken cancellationToken) =>
        dbContext.Sessions.AnyAsync(
            session => session.CampaignId == campaignId && session.EndedAtUtc == null,
            cancellationToken);
}
