using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class AdventureDraftService(
    StorykeeperDbContext dbContext,
    IAdventureDraftGenerator generator) : IAdventureDraftService
{
    public async Task<AdventureDraftOperationResult> GenerateAsync(
        Guid campaignId,
        AdventureDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestErrors = AdventureDraftRequestValidator.Validate(request);
        if (requestErrors.Count > 0)
        {
            throw new RuleValidationException(string.Join(" ", requestErrors.Values.SelectMany(value => value)));
        }

        var campaign = await LoadCampaignAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.NotFound);
        }

        if (!await CanGenerateAsync(campaignId, campaign, cancellationToken))
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.Conflict);
        }

        var preferences = string.IsNullOrWhiteSpace(request.ParentPreferences)
            ? null
            : request.ParentPreferences.Trim();
        var content = await GenerateValidatedContentAsync(
            campaign, request.SessionLengthMinutes, preferences, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var draft = new AdventureDraft
        {
            CampaignId = campaignId,
            SessionLengthMinutes = request.SessionLengthMinutes,
            ParentPreferences = preferences,
            ContentJson = Serialize(content),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.AdventureDrafts.Add(draft);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdventureDraftOperationResult(AdventureDraftOperationStatus.Succeeded, draft);
    }

    public async Task<IReadOnlyList<AdventureDraft>?> ListAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Campaigns.AnyAsync(campaign => campaign.Id == campaignId, cancellationToken))
        {
            return null;
        }

        var drafts = await dbContext.AdventureDrafts.AsNoTracking()
            .Where(draft => draft.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        return drafts.OrderByDescending(draft => draft.UpdatedAtUtc).ToArray();
    }

    public Task<AdventureDraft?> GetAsync(Guid draftId, CancellationToken cancellationToken = default) =>
        dbContext.AdventureDrafts.AsNoTracking()
            .SingleOrDefaultAsync(draft => draft.Id == draftId, cancellationToken);

    public async Task<AdventureDraftOperationResult> RegenerateAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.AdventureDrafts
            .SingleOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.NotFound);
        }

        if (draft.Status == AdventureDraftStatus.Activated)
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.AlreadyActivated, draft);
        }

        var campaign = await LoadCampaignAsync(draft.CampaignId, cancellationToken);
        if (campaign is null)
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.NotFound);
        }

        if (!await CanGenerateAsync(draft.CampaignId, campaign, cancellationToken))
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.Conflict, draft);
        }

        var content = await GenerateValidatedContentAsync(
            campaign, draft.SessionLengthMinutes, draft.ParentPreferences, cancellationToken);
        draft.ContentJson = Serialize(content);
        draft.GenerationNumber++;
        draft.Status = AdventureDraftStatus.PendingReview;
        draft.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdventureDraftOperationResult(AdventureDraftOperationStatus.Succeeded, draft);
    }

    public async Task<AdventureDraftOperationResult> UpdateAsync(
        Guid draftId,
        AdventureDraftContent content,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.AdventureDrafts
            .SingleOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.NotFound);
        }

        if (draft.Status == AdventureDraftStatus.Activated)
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.AlreadyActivated, draft);
        }

        var npcNames = await LoadNpcNamesAsync(draft.CampaignId, cancellationToken);
        var normalized = ValidateAndNormalize(content, npcNames);
        draft.ContentJson = Serialize(normalized);
        draft.Status = AdventureDraftStatus.PendingReview;
        draft.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdventureDraftOperationResult(AdventureDraftOperationStatus.Succeeded, draft);
    }

    public async Task<AdventureDraftOperationResult> ApproveAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.AdventureDrafts
            .SingleOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.NotFound);
        }

        if (draft.Status == AdventureDraftStatus.Activated)
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.AlreadyActivated, draft);
        }

        var npcNames = await LoadNpcNamesAsync(draft.CampaignId, cancellationToken);
        _ = ValidateAndNormalize(Deserialize(draft.ContentJson), npcNames);
        draft.Status = AdventureDraftStatus.Approved;
        draft.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdventureDraftOperationResult(AdventureDraftOperationStatus.Succeeded, draft);
    }

    public async Task<AdventureDraftActivationResult> ActivateAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.AdventureDrafts
            .SingleOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return new AdventureDraftActivationResult(AdventureDraftOperationStatus.NotFound);
        }

        if (draft.Status == AdventureDraftStatus.Activated)
        {
            return new AdventureDraftActivationResult(AdventureDraftOperationStatus.AlreadyActivated, draft);
        }

        if (draft.Status != AdventureDraftStatus.Approved)
        {
            throw new RuleConflictException("A parent must approve the adventure before activation.");
        }

        var campaign = await LoadCampaignAsync(draft.CampaignId, cancellationToken);
        if (campaign is null)
        {
            return new AdventureDraftActivationResult(AdventureDraftOperationStatus.NotFound);
        }

        if (!await CanGenerateAsync(draft.CampaignId, campaign, cancellationToken))
        {
            return new AdventureDraftActivationResult(AdventureDraftOperationStatus.Conflict, draft);
        }

        var content = ValidateAndNormalize(
            Deserialize(draft.ContentJson),
            campaign.Npcs.Select(npc => npc.Name).ToArray());
        var now = DateTimeOffset.UtcNow;
        var quest = new Quest
        {
            CampaignId = draft.CampaignId,
            Title = content.Title!,
            Description = content.Premise!,
            Status = QuestStatus.InProgress,
            SessionLengthMinutes = draft.SessionLengthMinutes,
            AdventurePlanJson = Serialize(content)
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var campaignUpdated = await dbContext.Campaigns
            .Where(item => item.Id == draft.CampaignId && item.Status == CampaignStatus.Active)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
        if (campaignUpdated == 0 || await dbContext.Sessions.AnyAsync(
                item => item.CampaignId == draft.CampaignId && item.EndedAtUtc == null,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AdventureDraftActivationResult(AdventureDraftOperationStatus.Conflict, draft);
        }

        var claimed = await dbContext.AdventureDrafts
            .Where(item => item.Id == draftId &&
                           item.Status == AdventureDraftStatus.Approved &&
                           item.ActivatedQuestId == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, AdventureDraftStatus.Activated)
                .SetProperty(item => item.ActivatedAtUtc, now)
                .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AdventureDraftActivationResult(AdventureDraftOperationStatus.AlreadyActivated);
        }

        await dbContext.Quests
            .Where(item => item.CampaignId == draft.CampaignId && item.Status == QuestStatus.InProgress)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, QuestStatus.Completed), cancellationToken);

        var npcAlreadyExists = campaign.Npcs.Any(npc =>
            string.Equals(npc.Name, content.FeaturedNpc!.Name, StringComparison.OrdinalIgnoreCase));
        if (!npcAlreadyExists)
        {
            dbContext.Npcs.Add(new Npc
            {
                CampaignId = draft.CampaignId,
                Name = content.FeaturedNpc!.Name!,
                Description = content.FeaturedNpc.Description!,
                Disposition = content.FeaturedNpc.Disposition!
            });
        }

        draft.Status = AdventureDraftStatus.Activated;
        draft.ActivatedAtUtc = now;
        draft.ActivatedQuestId = quest.Id;
        draft.UpdatedAtUtc = now;
        dbContext.Quests.Add(quest);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AdventureDraftActivationResult(AdventureDraftOperationStatus.Succeeded, draft);
    }

    public async Task<AdventureDraftOperationResult> DeleteAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.AdventureDrafts
            .SingleOrDefaultAsync(item => item.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.NotFound);
        }

        if (draft.Status == AdventureDraftStatus.Activated)
        {
            return new AdventureDraftOperationResult(AdventureDraftOperationStatus.AlreadyActivated, draft);
        }

        dbContext.AdventureDrafts.Remove(draft);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdventureDraftOperationResult(AdventureDraftOperationStatus.Succeeded);
    }

    private async Task<Campaign?> LoadCampaignAsync(Guid campaignId, CancellationToken cancellationToken) =>
        await dbContext.Campaigns.AsNoTracking()
            .Include(campaign => campaign.Settings)
            .Include(campaign => campaign.Bible)
            .Include(campaign => campaign.Quests)
            .Include(campaign => campaign.Npcs)
            .Include(campaign => campaign.Locations)
            .SingleOrDefaultAsync(campaign => campaign.Id == campaignId, cancellationToken);

    private async Task<bool> CanGenerateAsync(
        Guid campaignId,
        Campaign campaign,
        CancellationToken cancellationToken)
    {
        if (campaign.Status != CampaignStatus.Active)
        {
            return false;
        }

        return !await dbContext.Sessions.AnyAsync(
            session => session.CampaignId == campaignId && session.EndedAtUtc == null,
            cancellationToken);
    }

    private async Task<AdventureDraftContent> GenerateValidatedContentAsync(
        Campaign campaign,
        int sessionLengthMinutes,
        string? parentPreferences,
        CancellationToken cancellationToken)
    {
        var activeFacts = await dbContext.CampaignFacts.AsNoTracking()
            .Where(fact => fact.CampaignId == campaign.Id && fact.Status == CampaignFactStatus.Active)
            .OrderByDescending(fact => fact.Importance)
            .ThenBy(fact => fact.Statement)
            .Take(30)
            .Select(fact => $"{fact.Category} (importance {fact.Importance}): {Limit(fact.Statement, 600)}")
            .ToArrayAsync(cancellationToken);
        var summaries = await dbContext.Sessions.AsNoTracking()
            .Where(session => session.CampaignId == campaign.Id &&
                              session.EndedAtUtc != null &&
                              session.Summary != null &&
                              session.Summary != "")
            .OrderByDescending(session => session.SessionNumber)
            .Take(3)
            .Select(session => Limit(session.Summary!, 1200))
            .ToArrayAsync(cancellationToken);
        var content = await generator.GenerateAsync(new AdventureGenerationInput(
            campaign.Name,
            campaign.Settings?.Theme ?? string.Empty,
            campaign.Settings?.Tone ?? string.Empty,
            campaign.Bible?.WorldDescription ?? string.Empty,
            campaign.Bible?.CurrentSituation,
            sessionLengthMinutes,
            parentPreferences,
            activeFacts,
            summaries,
            campaign.Quests.Select(quest => quest.Title).Order().ToArray(),
            campaign.Npcs.Select(npc => npc.Name).Order().ToArray(),
            campaign.Locations.Select(location => location.Name).Order().ToArray()), cancellationToken);
        return ValidateAndNormalize(content, campaign.Npcs.Select(npc => npc.Name).ToArray());
    }

    private async Task<string[]> LoadNpcNamesAsync(Guid campaignId, CancellationToken cancellationToken) =>
        await dbContext.Npcs.AsNoTracking()
            .Where(npc => npc.CampaignId == campaignId)
            .Select(npc => npc.Name)
            .ToArrayAsync(cancellationToken);

    private static AdventureDraftContent ValidateAndNormalize(
        AdventureDraftContent? content,
        IReadOnlyCollection<string> npcNames)
    {
        var errors = AdventureDraftValidator.Validate(content, npcNames);
        if (errors.Count > 0)
        {
            throw new AdventureDraftRejectedException(errors);
        }

        return AdventureDraftValidator.Normalize(content!);
    }

    private static string Serialize(AdventureDraftContent content) =>
        JsonSerializer.Serialize(content, AdventureDraftJson.Options);

    private static AdventureDraftContent Deserialize(string contentJson) =>
        JsonSerializer.Deserialize<AdventureDraftContent>(contentJson, AdventureDraftJson.Options)
        ?? throw new JsonException("Stored adventure draft content was empty.");

    private static string Limit(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];
}

public sealed class AdventureDraftRejectedException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The generated adventure did not meet the story requirements.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
