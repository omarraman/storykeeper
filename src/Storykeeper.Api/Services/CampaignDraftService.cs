using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class CampaignDraftService(
    StorykeeperDbContext dbContext,
    ICampaignDraftGenerator generator) : ICampaignDraftService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CampaignDraft?> GenerateAsync(Guid briefId, CancellationToken cancellationToken = default)
    {
        var brief = await dbContext.CampaignBriefs.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == briefId, cancellationToken);
        if (brief is null)
        {
            return null;
        }

        var content = await GenerateValidatedContentAsync(brief, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var draft = new CampaignDraft
        {
            CampaignBriefId = brief.Id,
            ContentJson = Serialize(content),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.CampaignDrafts.Add(draft);
        await dbContext.SaveChangesAsync(cancellationToken);
        return draft;
    }

    public async Task<IReadOnlyList<CampaignDraft>> ListAsync(CancellationToken cancellationToken = default)
    {
        var drafts = await dbContext.CampaignDrafts.AsNoTracking().ToArrayAsync(cancellationToken);
        return drafts.OrderByDescending(draft => draft.UpdatedAtUtc).ToArray();
    }

    public Task<CampaignDraft?> GetAsync(Guid draftId, CancellationToken cancellationToken = default) =>
        dbContext.CampaignDrafts.AsNoTracking()
            .SingleOrDefaultAsync(draft => draft.Id == draftId, cancellationToken);

    public async Task<CampaignDraftOperationResult> RegenerateAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.CampaignDrafts
            .SingleOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return new CampaignDraftOperationResult(CampaignDraftOperationStatus.NotFound);
        }

        if (draft.Status == CampaignDraftStatus.Activated)
        {
            return new CampaignDraftOperationResult(CampaignDraftOperationStatus.AlreadyActivated, draft);
        }

        var brief = await dbContext.CampaignBriefs.AsNoTracking()
            .SingleAsync(item => item.Id == draft.CampaignBriefId, cancellationToken);
        var content = await GenerateValidatedContentAsync(brief, cancellationToken);
        draft.ContentJson = Serialize(content);
        draft.GenerationNumber++;
        draft.Status = CampaignDraftStatus.PendingReview;
        draft.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CampaignDraftOperationResult(CampaignDraftOperationStatus.Succeeded, draft);
    }

    public async Task<CampaignDraftOperationResult> UpdateAsync(
        Guid draftId,
        CampaignDraftContent content,
        CancellationToken cancellationToken = default)
    {
        var normalized = ValidateAndNormalize(content);
        var draft = await dbContext.CampaignDrafts
            .SingleOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return new CampaignDraftOperationResult(CampaignDraftOperationStatus.NotFound);
        }

        if (draft.Status == CampaignDraftStatus.Activated)
        {
            return new CampaignDraftOperationResult(CampaignDraftOperationStatus.AlreadyActivated, draft);
        }

        draft.ContentJson = Serialize(normalized);
        draft.Status = CampaignDraftStatus.PendingReview;
        draft.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CampaignDraftOperationResult(CampaignDraftOperationStatus.Succeeded, draft);
    }

    public async Task<CampaignDraftOperationResult> ApproveAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.CampaignDrafts
            .SingleOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return new CampaignDraftOperationResult(CampaignDraftOperationStatus.NotFound);
        }

        if (draft.Status == CampaignDraftStatus.Activated)
        {
            return new CampaignDraftOperationResult(CampaignDraftOperationStatus.AlreadyActivated, draft);
        }

        _ = ValidateAndNormalize(Deserialize(draft.ContentJson));
        draft.Status = CampaignDraftStatus.Approved;
        draft.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CampaignDraftOperationResult(CampaignDraftOperationStatus.Succeeded, draft);
    }

    public async Task<CampaignDraftActivationResult> ActivateAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.CampaignDrafts
            .SingleOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return new CampaignDraftActivationResult(CampaignDraftActivationStatus.NotFound);
        }

        if (draft.Status == CampaignDraftStatus.Activated)
        {
            return new CampaignDraftActivationResult(CampaignDraftActivationStatus.AlreadyActivated);
        }

        if (draft.Status != CampaignDraftStatus.Approved)
        {
            return new CampaignDraftActivationResult(CampaignDraftActivationStatus.ApprovalRequired);
        }

        var content = ValidateAndNormalize(Deserialize(draft.ContentJson));
        var brief = await dbContext.CampaignBriefs.AsNoTracking()
            .SingleAsync(item => item.Id == draft.CampaignBriefId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var campaign = new Campaign
        {
            Name = content.Title!,
            Description = content.Premise,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Settings = new CampaignSettings
            {
                Theme = brief.Genre,
                Tone = brief.Tone,
                LowFright = true
            },
            Bible = new CampaignBible
            {
                WorldDescription = content.Premise!,
                CurrentSituation = content.CentralMystery,
                Version = 1
            },
            Party = new Party { Name = "Adventurers" }
        };
        var locations = content.Locations!.Select(location => new Location
        {
            CampaignId = campaign.Id,
            Name = location!.Name!,
            Description = location.Description!
        }).ToArray();
        var locationsByName = locations.ToDictionary(location => location.Name, StringComparer.OrdinalIgnoreCase);
        var npcs = content.Npcs!.Select(npc => new Npc
        {
            CampaignId = campaign.Id,
            Name = npc!.Name!,
            Description = npc.Description!,
            Disposition = npc.Disposition!,
            LocationId = string.IsNullOrWhiteSpace(npc.LocationName)
                ? null
                : locationsByName[npc.LocationName].Id
        }).ToArray();
        var quests = content.AdventureHooks!.Select(hook => new Quest
        {
            CampaignId = campaign.Id,
            Title = hook!.Title!,
            Description = hook.Description!,
            Status = QuestStatus.Available
        }).ToArray();
        var version = new CampaignBibleVersion
        {
            CampaignId = campaign.Id,
            Version = 1,
            SourceDraftId = draft.Id,
            Title = content.Title!,
            ContentJson = Serialize(content),
            ActivatedAtUtc = now
        };

        var claimed = await dbContext.CampaignDrafts
            .Where(item => item.Id == draftId && item.Status == CampaignDraftStatus.Approved && item.CampaignId == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, CampaignDraftStatus.Activated)
                .SetProperty(item => item.ActivatedAtUtc, now)
                .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new CampaignDraftActivationResult(CampaignDraftActivationStatus.AlreadyActivated);
        }

        draft.Status = CampaignDraftStatus.Activated;
        draft.CampaignId = campaign.Id;
        draft.ActivatedAtUtc = now;
        draft.UpdatedAtUtc = now;
        dbContext.Campaigns.Add(campaign);
        dbContext.Locations.AddRange(locations);
        dbContext.Npcs.AddRange(npcs);
        dbContext.Quests.AddRange(quests);
        dbContext.CampaignBibleVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CampaignDraftActivationResult(CampaignDraftActivationStatus.Activated, campaign);
    }

    public async Task<CampaignDraftOperationResult> DeleteAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.CampaignDrafts
            .SingleOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return new CampaignDraftOperationResult(CampaignDraftOperationStatus.NotFound);
        }

        if (draft.Status == CampaignDraftStatus.Activated)
        {
            return new CampaignDraftOperationResult(CampaignDraftOperationStatus.AlreadyActivated, draft);
        }

        dbContext.CampaignDrafts.Remove(draft);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CampaignDraftOperationResult(CampaignDraftOperationStatus.Succeeded);
    }

    public async Task<IReadOnlyList<CampaignBibleVersion>?> ListBibleVersionsAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Campaigns.AnyAsync(campaign => campaign.Id == campaignId, cancellationToken))
        {
            return null;
        }

        var versions = await dbContext.CampaignBibleVersions.AsNoTracking()
            .Where(version => version.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        return versions.OrderByDescending(version => version.Version).ToArray();
    }

    private async Task<CampaignDraftContent> GenerateValidatedContentAsync(
        CampaignBrief brief,
        CancellationToken cancellationToken)
    {
        var generated = await generator.GenerateAsync(brief, cancellationToken);
        return ValidateAndNormalize(generated);
    }

    private static CampaignDraftContent ValidateAndNormalize(CampaignDraftContent? content)
    {
        var errors = CampaignDraftValidator.Validate(content);
        if (errors.Count > 0)
        {
            throw new CampaignDraftRejectedException(errors);
        }

        return CampaignDraftValidator.Normalize(content!);
    }

    private static string Serialize(CampaignDraftContent content) => JsonSerializer.Serialize(content, JsonOptions);

    private static CampaignDraftContent Deserialize(string contentJson) =>
        JsonSerializer.Deserialize<CampaignDraftContent>(contentJson, JsonOptions)
        ?? throw new JsonException("Stored campaign draft content was empty.");
}
