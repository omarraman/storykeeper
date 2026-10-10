using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class CampaignArchiveService(StorykeeperDbContext dbContext)
{
    public const string ArchiveFormat = "storykeeper-campaign";
    public const int ArchiveVersion = 1;

    public async Task<CampaignArchiveDocument?> ExportAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var campaign = await dbContext.Campaigns.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        var settings = await dbContext.CampaignSettings.AsNoTracking()
            .SingleAsync(item => item.CampaignId == campaignId, cancellationToken);
        var bible = await dbContext.CampaignBibles.AsNoTracking()
            .SingleAsync(item => item.CampaignId == campaignId, cancellationToken);
        var party = await dbContext.Parties.AsNoTracking()
            .SingleAsync(item => item.CampaignId == campaignId, cancellationToken);
        var heroes = await dbContext.Heroes.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var inventory = await dbContext.InventoryItems.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var locations = await dbContext.Locations.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var npcs = await dbContext.Npcs.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var quests = await dbContext.Quests.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var sessions = await dbContext.Sessions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var checks = await dbContext.CheckResolutions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var facts = await dbContext.CampaignFacts.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var revisions = await dbContext.CampaignContinuityRevisions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var relationships = await dbContext.Relationships.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var rewards = await dbContext.Rewards.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var bibleVersions = await dbContext.CampaignBibleVersions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var adventureDrafts = await dbContext.AdventureDrafts.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);

        var archive = new CampaignArchiveDocument(
            ArchiveFormat,
            ArchiveVersion,
            DateTimeOffset.UtcNow,
            new CampaignArchiveCampaign(
                campaign.Id, campaign.Name, campaign.Description, campaign.Status,
                campaign.CreatedAtUtc, campaign.UpdatedAtUtc, campaign.ArchivedAtUtc),
            new CampaignArchiveSettings(
                settings.Id, settings.Theme, settings.Tone, settings.LowFright, settings.SafetySettings),
            new CampaignArchiveBible(
                bible.Id, bible.WorldDescription, bible.CurrentSituation, bible.Version),
            new CampaignArchiveParty(party.Id, party.Name),
            heroes.Select(item => new CampaignArchiveHero(
                item.Id, item.PartyId, item.Name, item.Description, item.Role,
                item.Strengths, item.Hearts, item.SparkleTokens)).ToArray(),
            inventory.Select(item => new CampaignArchiveInventoryItem(
                item.Id, item.HeroId, item.Name, item.Description, item.Quantity)).ToArray(),
            locations.Select(item => new CampaignArchiveLocation(
                item.Id, item.Name, item.Description)).ToArray(),
            npcs.Select(item => new CampaignArchiveNpc(
                item.Id, item.LocationId, item.Name, item.Description, item.Disposition)).ToArray(),
            quests.Select(item => new CampaignArchiveQuest(
                item.Id, item.Title, item.Description, item.Status,
                item.SessionLengthMinutes, item.AdventurePlanJson)).ToArray(),
            sessions.Select(item => new CampaignArchiveSession(
                item.Id, item.SessionNumber, item.Title, item.StartedAtUtc, item.EndedAtUtc,
                item.Summary, item.IsPaused, item.ParentInstruction)).ToArray(),
            checks.Select(item => new CampaignArchiveCheck(
                item.Id, item.SessionId, item.HeroId, item.Roll, item.RollSource, item.Difficulty,
                item.Target, item.Strength, item.StrengthBonus, item.SparkleBonus, item.Total,
                item.Outcome, item.ForwardProgressRequired, item.ConsequenceCategory, item.Risky,
                item.SparkleTokenSpent, item.HeartsBefore, item.HeartsAfter,
                item.SparkleTokensBefore, item.SparkleTokensAfter, item.CreatedAtUtc)).ToArray(),
            facts.Select(item => new CampaignArchiveFact(
                item.Id, item.SourceSessionId, item.Category, item.Statement,
                item.Status, item.Importance)).ToArray(),
            revisions.Select(item => new CampaignArchiveRevision(
                item.Id, item.RecordType, item.RecordId, item.SourceSessionId,
                item.PreviousContent, item.NewContent, item.ChangedBy, item.ChangedAtUtc)).ToArray(),
            relationships.Select(item => new CampaignArchiveRelationship(
                item.Id, item.SubjectType, item.SubjectId, item.TargetType,
                item.TargetId, item.Description)).ToArray(),
            rewards.Select(item => new CampaignArchiveReward(
                item.Id, item.HeroId, item.Name, item.Description, item.IsClaimed)).ToArray(),
            bibleVersions.Select(item => new CampaignArchiveBibleVersion(
                item.Id, item.Version, item.Title, item.ContentJson, item.ActivatedAtUtc)).ToArray(),
            adventureDrafts.Select(item => new CampaignArchiveAdventureDraft(
                item.Id, item.SessionLengthMinutes, item.ParentPreferences, item.Status,
                item.GenerationNumber, item.ContentJson, item.ActivatedQuestId,
                item.CreatedAtUtc, item.UpdatedAtUtc, item.ActivatedAtUtc)).ToArray());
        await transaction.CommitAsync(cancellationToken);
        return archive;
    }

    public async Task<CampaignStorybookResponse?> GetStorybookAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await dbContext.Campaigns.AnyAsync(item => item.Id == campaignId, cancellationToken))
        {
            return null;
        }

        var sessions = await dbContext.Sessions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderBy(item => item.SessionNumber)
            .ToArrayAsync(cancellationToken);
        var facts = await dbContext.CampaignFacts.AsNoTracking()
            .Where(item => item.CampaignId == campaignId &&
                           item.SourceSessionId != null &&
                           item.Importance >= 4 &&
                           (item.Status == CampaignFactStatus.Active ||
                            item.Status == CampaignFactStatus.Resolved ||
                            item.Status == CampaignFactStatus.Superseded))
            .ToArrayAsync(cancellationToken);
        var checks = await dbContext.CheckResolutions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId &&
                           item.Outcome == CheckOutcome.StrongSuccess)
            .ToArrayAsync(cancellationToken);
        var heroNames = await dbContext.Heroes.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var discoveriesBySession = facts.ToLookup(item => item.SourceSessionId!.Value);
        var achievementsBySession = checks
            .Where(item => heroNames.ContainsKey(item.HeroId))
            .GroupBy(item => item.SessionId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.CreatedAtUtc)
                    .Select(item => new CampaignStorybookAchievement(
                        heroNames[item.HeroId],
                        $"Strong success on a {item.Difficulty.ToString().ToLowerInvariant()} check"))
                    .ToArray());
        var rewards = await dbContext.Rewards.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
        var completedQuestPlans = await dbContext.Quests.AsNoTracking()
            .Where(item => item.CampaignId == campaignId &&
                           item.Status == QuestStatus.Completed &&
                           item.AdventurePlanJson != null)
            .Select(item => new { item.Title, item.AdventurePlanJson })
            .ToArrayAsync(cancellationToken);
        var storyRewards = rewards.Select(reward => new CampaignStorybookReward(
                reward.Name,
                reward.Description,
                reward.HeroId is { } heroId ? heroNames.GetValueOrDefault(heroId) : null,
                reward.IsClaimed))
            .ToList();
        foreach (var completedQuest in completedQuestPlans)
        {
            var plan = JsonSerializer.Deserialize<AdventureDraftContent>(
                completedQuest.AdventurePlanJson!, AdventureDraftJson.Options);
            if (!string.IsNullOrWhiteSpace(plan?.CelebrationReward) &&
                !storyRewards.Any(reward => string.Equals(
                    reward.Name, plan.CelebrationReward, StringComparison.OrdinalIgnoreCase)))
            {
                storyRewards.Add(new CampaignStorybookReward(
                    plan.CelebrationReward,
                    $"Celebration reward from {completedQuest.Title}.",
                    null,
                    true));
            }
        }

        var storybook = new CampaignStorybookResponse(
            sessions.Select(session => new CampaignStorybookSession(
                session.Id,
                session.SessionNumber,
                session.Title,
                session.StartedAtUtc,
                session.EndedAtUtc,
                session.Summary,
                discoveriesBySession[session.Id]
                    .OrderByDescending(fact => fact.Importance)
                    .ThenBy(fact => fact.Statement)
                    .Select(fact => new CampaignStorybookDiscovery(fact.Category, fact.Statement))
                    .ToArray(),
                achievementsBySession.GetValueOrDefault(session.Id, [])))
                .ToArray(),
            storyRewards.OrderBy(reward => reward.Name).ToArray());
        await transaction.CommitAsync(cancellationToken);
        return storybook;
    }

    public async Task<CampaignArchiveImportResult> ImportAsync(
        CampaignArchiveDocument? archive,
        CancellationToken cancellationToken = default)
    {
        var errors = CampaignArchiveValidator.Validate(archive);
        if (errors.Count > 0 || archive is null)
        {
            return new CampaignArchiveImportResult(null, errors, false);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await dbContext.Campaigns.AnyAsync(
                item => item.Id == archive.Campaign.Id, cancellationToken) ||
            await HasExistingArchiveIdsAsync(archive, cancellationToken))
        {
            return new CampaignArchiveImportResult(
                null,
                new Dictionary<string, string[]>
                {
                    ["campaign"] = ["This archive contains IDs already present in this database. Exported campaigns can only be imported once per database."]
                },
                true);
        }

        var campaignId = archive.Campaign.Id;
        dbContext.Campaigns.Add(new Campaign
        {
            Id = campaignId,
            Name = archive.Campaign.Name,
            Description = archive.Campaign.Description,
            Status = archive.Campaign.Status,
            CreatedAtUtc = archive.Campaign.CreatedAtUtc,
            UpdatedAtUtc = archive.Campaign.UpdatedAtUtc,
            ArchivedAtUtc = archive.Campaign.ArchivedAtUtc
        });
        dbContext.CampaignSettings.Add(new CampaignSettings
        {
            Id = archive.Settings.Id,
            CampaignId = campaignId,
            Theme = archive.Settings.Theme,
            Tone = archive.Settings.Tone,
            LowFright = archive.Settings.LowFright,
            SafetySettings = archive.Settings.SafetySettings
        });
        dbContext.CampaignBibles.Add(new CampaignBible
        {
            Id = archive.Bible.Id,
            CampaignId = campaignId,
            WorldDescription = archive.Bible.WorldDescription,
            CurrentSituation = archive.Bible.CurrentSituation,
            Version = archive.Bible.Version
        });
        dbContext.Parties.Add(new Party
        {
            Id = archive.Party.Id,
            CampaignId = campaignId,
            Name = archive.Party.Name
        });
        dbContext.Heroes.AddRange(archive.Heroes.Select(item => new Hero
        {
            Id = item.Id,
            CampaignId = campaignId,
            PartyId = item.PartyId,
            Name = item.Name,
            Description = item.Description,
            Role = item.Role,
            Strengths = item.Strengths.ToList(),
            Hearts = item.Hearts,
            SparkleTokens = item.SparkleTokens
        }));
        dbContext.InventoryItems.AddRange(archive.Inventory.Select(item => new InventoryItem
        {
            Id = item.Id,
            CampaignId = campaignId,
            HeroId = item.HeroId,
            Name = item.Name,
            Description = item.Description,
            Quantity = item.Quantity
        }));
        dbContext.Locations.AddRange(archive.Locations.Select(item => new Location
        {
            Id = item.Id,
            CampaignId = campaignId,
            Name = item.Name,
            Description = item.Description
        }));
        dbContext.Npcs.AddRange(archive.Npcs.Select(item => new Npc
        {
            Id = item.Id,
            CampaignId = campaignId,
            LocationId = item.LocationId,
            Name = item.Name,
            Description = item.Description,
            Disposition = item.Disposition
        }));
        dbContext.Quests.AddRange(archive.Quests.Select(item => new Quest
        {
            Id = item.Id,
            CampaignId = campaignId,
            Title = item.Title,
            Description = item.Description,
            Status = item.Status,
            SessionLengthMinutes = item.SessionLengthMinutes,
            AdventurePlanJson = item.AdventurePlanJson
        }));
        dbContext.Sessions.AddRange(archive.Sessions.Select(item => new Session
        {
            Id = item.Id,
            CampaignId = campaignId,
            SessionNumber = item.SessionNumber,
            Title = item.Title,
            StartedAtUtc = item.StartedAtUtc,
            EndedAtUtc = item.EndedAtUtc,
            Summary = item.Summary,
            IsPaused = item.IsPaused,
            ParentInstruction = item.ParentInstruction
        }));
        dbContext.CheckResolutions.AddRange(archive.Checks.Select(item => new CheckResolution
        {
            Id = item.Id,
            CampaignId = campaignId,
            SessionId = item.SessionId,
            HeroId = item.HeroId,
            Roll = item.Roll,
            RollSource = item.RollSource,
            Difficulty = item.Difficulty,
            Target = item.Target,
            Strength = item.Strength,
            StrengthBonus = item.StrengthBonus,
            SparkleBonus = item.SparkleBonus,
            Total = item.Total,
            Outcome = item.Outcome,
            ForwardProgressRequired = item.ForwardProgressRequired,
            ConsequenceCategory = item.ConsequenceCategory,
            Risky = item.Risky,
            SparkleTokenSpent = item.SparkleTokenSpent,
            HeartsBefore = item.HeartsBefore,
            HeartsAfter = item.HeartsAfter,
            SparkleTokensBefore = item.SparkleTokensBefore,
            SparkleTokensAfter = item.SparkleTokensAfter,
            CreatedAtUtc = item.CreatedAtUtc
        }));
        dbContext.CampaignFacts.AddRange(archive.Facts.Select(item => new CampaignFact
        {
            Id = item.Id,
            CampaignId = campaignId,
            SourceSessionId = item.SourceSessionId,
            Category = item.Category,
            Statement = item.Statement,
            Status = item.Status,
            Importance = item.Importance
        }));
        dbContext.CampaignContinuityRevisions.AddRange(archive.Revisions.Select(item => new CampaignContinuityRevision
        {
            Id = item.Id,
            CampaignId = campaignId,
            RecordType = item.RecordType,
            RecordId = item.RecordId,
            SourceSessionId = item.SourceSessionId,
            PreviousContent = item.PreviousContent,
            NewContent = item.NewContent,
            ChangedBy = item.ChangedBy,
            ChangedAtUtc = item.ChangedAtUtc
        }));
        dbContext.Relationships.AddRange(archive.Relationships.Select(item => new Relationship
        {
            Id = item.Id,
            CampaignId = campaignId,
            SubjectType = item.SubjectType,
            SubjectId = item.SubjectId,
            TargetType = item.TargetType,
            TargetId = item.TargetId,
            Description = item.Description
        }));
        dbContext.Rewards.AddRange(archive.Rewards.Select(item => new Reward
        {
            Id = item.Id,
            CampaignId = campaignId,
            HeroId = item.HeroId,
            Name = item.Name,
            Description = item.Description,
            IsClaimed = item.IsClaimed
        }));
        dbContext.CampaignBibleVersions.AddRange(archive.BibleVersions.Select(item => new CampaignBibleVersion
        {
            Id = item.Id,
            CampaignId = campaignId,
            Version = item.Version,
            Title = item.Title,
            ContentJson = item.ContentJson,
            ActivatedAtUtc = item.ActivatedAtUtc
        }));
        dbContext.AdventureDrafts.AddRange(archive.AdventureDrafts.Select(item => new AdventureDraft
        {
            Id = item.Id,
            CampaignId = campaignId,
            SessionLengthMinutes = item.SessionLengthMinutes,
            ParentPreferences = item.ParentPreferences,
            Status = item.Status,
            GenerationNumber = item.GenerationNumber,
            ContentJson = item.ContentJson,
            ActivatedQuestId = item.ActivatedQuestId,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
            ActivatedAtUtc = item.ActivatedAtUtc
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CampaignArchiveImportResult(campaignId, new Dictionary<string, string[]>(), false);
    }

    private async Task<bool> HasExistingArchiveIdsAsync(
        CampaignArchiveDocument archive,
        CancellationToken cancellationToken) =>
        await dbContext.CampaignSettings.AnyAsync(item => item.Id == archive.Settings.Id, cancellationToken) ||
        await dbContext.CampaignBibles.AnyAsync(item => item.Id == archive.Bible.Id, cancellationToken) ||
        await dbContext.Parties.AnyAsync(item => item.Id == archive.Party.Id, cancellationToken) ||
        await HasAnyAsync<Hero>(archive.Heroes.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<InventoryItem>(archive.Inventory.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<Location>(archive.Locations.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<Npc>(archive.Npcs.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<Quest>(archive.Quests.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<Session>(archive.Sessions.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<CheckResolution>(archive.Checks.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<CampaignFact>(archive.Facts.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<CampaignContinuityRevision>(archive.Revisions.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<Relationship>(archive.Relationships.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<Reward>(archive.Rewards.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<CampaignBibleVersion>(archive.BibleVersions.Select(item => item.Id), cancellationToken) ||
        await HasAnyAsync<AdventureDraft>(archive.AdventureDrafts.Select(item => item.Id), cancellationToken);

    private Task<bool> HasAnyAsync<TEntity>(IEnumerable<Guid> ids, CancellationToken cancellationToken)
        where TEntity : class =>
        dbContext.Set<TEntity>().AnyAsync(item => ids.Contains(EF.Property<Guid>(item, "Id")), cancellationToken);
}

public static class CampaignArchiveValidator
{
    public static Dictionary<string, string[]> Validate(CampaignArchiveDocument? archive)
    {
        var errors = new Dictionary<string, string[]>();
        if (archive is null)
        {
            errors["archive"] = ["A campaign archive is required."];
            return errors;
        }

        if (!string.Equals(archive.Format, CampaignArchiveService.ArchiveFormat, StringComparison.Ordinal))
        {
            errors["format"] = ["This file is not a Storykeeper campaign archive."];
        }
        if (archive.SchemaVersion != CampaignArchiveService.ArchiveVersion)
        {
            errors["schemaVersion"] = [$"Unsupported archive version {archive.SchemaVersion}. Expected version {CampaignArchiveService.ArchiveVersion}."];
        }
        if (archive.Campaign is null || archive.Settings is null || archive.Bible is null || archive.Party is null ||
            archive.Heroes is null || archive.Inventory is null || archive.Locations is null || archive.Npcs is null ||
            archive.Quests is null || archive.Sessions is null || archive.Checks is null || archive.Facts is null ||
            archive.Revisions is null || archive.Relationships is null || archive.Rewards is null ||
            archive.BibleVersions is null || archive.AdventureDrafts is null)
        {
            errors["content"] = ["The archive is missing required campaign data."];
            return errors;
        }
        if (archive.Heroes.Any(item => item is null) || archive.Inventory.Any(item => item is null) ||
            archive.Locations.Any(item => item is null) || archive.Npcs.Any(item => item is null) ||
            archive.Quests.Any(item => item is null) || archive.Sessions.Any(item => item is null) ||
            archive.Checks.Any(item => item is null) || archive.Facts.Any(item => item is null) ||
            archive.Revisions.Any(item => item is null) || archive.Relationships.Any(item => item is null) ||
            archive.Rewards.Any(item => item is null) || archive.BibleVersions.Any(item => item is null) ||
            archive.AdventureDrafts.Any(item => item is null))
        {
            errors["content"] = ["Archive collections cannot contain null records."];
            return errors;
        }

        if (archive.Campaign.Id == Guid.Empty || string.IsNullOrWhiteSpace(archive.Campaign.Name) ||
            archive.Campaign.Name.Trim().Length > 120 || archive.Campaign.Description?.Length > 2000 ||
            IsUnsafe(archive.Campaign.Name) || IsUnsafe(archive.Campaign.Description) ||
            !Enum.IsDefined(archive.Campaign.Status))
        {
            errors["campaign"] = ["Campaign identity, name, description, or status is invalid."];
        }

        if (archive.Settings.Id == Guid.Empty || string.IsNullOrWhiteSpace(archive.Settings.Theme) ||
            archive.Settings.Theme.Length > 100 || string.IsNullOrWhiteSpace(archive.Settings.Tone) ||
            archive.Settings.Tone.Length > 300 || archive.Settings.SafetySettings is null ||
            IsUnsafe(archive.Settings.Theme) || IsUnsafe(archive.Settings.Tone) ||
            !Enum.IsDefined(archive.Settings.SafetySettings.FearLevel) ||
            !Enum.IsDefined(archive.Settings.SafetySettings.CombatMode) ||
            !Enum.IsDefined(archive.Settings.SafetySettings.NarrationProvider) ||
            !Enum.IsDefined(archive.Settings.SafetySettings.NarrationPlayback) ||
            archive.Settings.SafetySettings.ExcludedContent is null ||
            archive.Settings.SafetySettings.ExcludedContent.Count > 50 ||
            archive.Settings.SafetySettings.ExcludedContent.Any(value =>
                string.IsNullOrWhiteSpace(value) || value.Length > 100) ||
            (archive.Settings.SafetySettings.TextToSpeechEnabled &&
             (archive.Settings.SafetySettings.NarrationProvider == TextToSpeechProviderKind.Disabled ||
              archive.Settings.SafetySettings.NarrationPlayback == NarrationPlaybackPreference.Off)) ||
            (!archive.Settings.SafetySettings.TextToSpeechEnabled &&
             archive.Settings.SafetySettings.NarrationPlayback != NarrationPlaybackPreference.Off) ||
            archive.Settings.SafetySettings.MaxNarrationWords is < 40 or > 150 ||
            archive.Settings.SafetySettings.SessionLengthMinutes is < 15 or > 180)
        {
            errors["settings"] = ["Campaign safety settings are invalid."];
        }
        if (archive.Bible.Id == Guid.Empty || archive.Bible.WorldDescription?.Length > 4000 ||
            archive.Bible.CurrentSituation?.Length > 2000 ||
            IsUnsafe(archive.Bible.WorldDescription) || IsUnsafe(archive.Bible.CurrentSituation) ||
            archive.Bible.WorldDescription is null ||
            archive.Bible.Version < 1 || archive.Party.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(archive.Party.Name) || archive.Party.Name.Length > 120)
        {
            errors["world"] = ["Campaign world, bible, or party details are invalid."];
        }

        var allSets = new (string Name, IEnumerable<Guid> Ids)[]
        {
            ("settings", [archive.Settings.Id]), ("bible", [archive.Bible.Id]), ("party", [archive.Party.Id]),
            ("heroes", archive.Heroes.Select(item => item.Id)),
            ("inventory", archive.Inventory.Select(item => item.Id)),
            ("locations", archive.Locations.Select(item => item.Id)),
            ("npcs", archive.Npcs.Select(item => item.Id)),
            ("quests", archive.Quests.Select(item => item.Id)),
            ("sessions", archive.Sessions.Select(item => item.Id)),
            ("checks", archive.Checks.Select(item => item.Id)),
            ("facts", archive.Facts.Select(item => item.Id)),
            ("revisions", archive.Revisions.Select(item => item.Id)),
            ("relationships", archive.Relationships.Select(item => item.Id)),
            ("rewards", archive.Rewards.Select(item => item.Id)),
            ("bibleVersions", archive.BibleVersions.Select(item => item.Id)),
            ("adventureDrafts", archive.AdventureDrafts.Select(item => item.Id))
        };
        foreach (var (name, ids) in allSets)
        {
            var values = ids.ToArray();
            if (values.Any(id => id == Guid.Empty) || values.Distinct().Count() != values.Length)
            {
                errors[name] = ["IDs must be present and unique within each campaign data collection."];
            }
        }

        var count = allSets.Sum(set => set.Ids.Count());
        if (count > 10000)
        {
            errors["content"] = ["The archive contains too many records (maximum 10,000)."];
        }

        var heroIds = archive.Heroes.Select(item => item.Id).ToHashSet();
        var heroesById = archive.Heroes.ToDictionary(item => item.Id);
        var locationIds = archive.Locations.Select(item => item.Id).ToHashSet();
        var questIds = archive.Quests.Select(item => item.Id).ToHashSet();
        var sessionIds = archive.Sessions.Select(item => item.Id).ToHashSet();
        var participantIds = (archive.Heroes.Select(item => (RelationshipParticipantType.Hero, item.Id))
            .Concat(archive.Npcs.Select(item => (RelationshipParticipantType.Npc, item.Id)))).ToHashSet();

        if (archive.Heroes.Any(item => item.PartyId != archive.Party.Id ||
                string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 120 ||
                string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 2000 ||
                string.IsNullOrWhiteSpace(item.Role) || item.Role.Length > 80 ||
                IsUnsafe(item.Name) || IsUnsafe(item.Description) || IsUnsafe(item.Role) ||
                item.Hearts is < 0 or > 3 || item.SparkleTokens is < 0 or > 1 ||
                item.Strengths is null || item.Strengths.Count > 20 ||
                item.Strengths.Any(value => value is null || value.Length > 80 || IsUnsafe(value))) ||
            archive.Inventory.Any(item => !heroIds.Contains(item.HeroId) ||
                string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 120 ||
                string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 1000 ||
                IsUnsafe(item.Name) || IsUnsafe(item.Description) || item.Quantity < 1) ||
            archive.Locations.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
                item.Name.Length > 120 || string.IsNullOrWhiteSpace(item.Description) ||
                item.Description.Length > 2000 || IsUnsafe(item.Name) || IsUnsafe(item.Description)) ||
            archive.Npcs.Any(item => item.LocationId is { } locationId && !locationIds.Contains(locationId) ||
                string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 120 ||
                string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 2000 ||
                string.IsNullOrWhiteSpace(item.Disposition) || item.Disposition.Length > 300 ||
                IsUnsafe(item.Name) || IsUnsafe(item.Description) || IsUnsafe(item.Disposition)))
        {
            errors["characters"] = ["A hero, inventory item, location, or character is invalid or references a missing campaign record."];
        }

        if (archive.Quests.Any(item => string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 160 ||
                string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 2000 ||
                !Enum.IsDefined(item.Status) ||
                item.SessionLengthMinutes is not null and (< 15 or > 180) ||
                item.AdventurePlanJson?.Length > 12000 || IsUnsafe(item.Title) ||
                IsUnsafe(item.Description) || IsUnsafe(item.AdventurePlanJson) ||
                !IsValidAdventurePlanJson(item.AdventurePlanJson)) ||
            archive.Sessions.Any(item => item.SessionNumber < 1 || string.IsNullOrWhiteSpace(item.Title) ||
                item.Title.Length > 160 || item.Summary?.Length > 4000 ||
                item.ParentInstruction?.Length > 300 || IsUnsafe(item.Title) ||
                IsUnsafe(item.Summary) || !AllowedParentInstruction(item.ParentInstruction) ||
                item.EndedAtUtc is not null && string.IsNullOrWhiteSpace(item.Summary) ||
                item.EndedAtUtc < item.StartedAtUtc) ||
            archive.Sessions.Select(item => item.SessionNumber).Distinct().Count() != archive.Sessions.Count ||
            archive.Sessions.Count(item => item.EndedAtUtc is null) > 1 ||
            archive.Sessions.Count(item => item.EndedAtUtc is null) == 1 &&
                archive.Sessions.Single(item => item.EndedAtUtc is null).SessionNumber !=
                archive.Sessions.Max(item => item.SessionNumber) ||
            archive.Quests.Count(item => item.Status == QuestStatus.InProgress) > 1)
        {
            errors["adventures"] = ["An adventure, session, or session title is invalid."];
        }

        if (archive.Checks.Any(item => !CheckIsValid(item, heroesById, sessionIds)) ||
            archive.Facts.Any(item => item.SourceSessionId is { } sessionId && !sessionIds.Contains(sessionId) ||
                string.IsNullOrWhiteSpace(item.Category) || item.Category.Length > 80 ||
                string.IsNullOrWhiteSpace(item.Statement) || item.Statement.Length > 2000 ||
                !Enum.IsDefined(item.Status) || item.Importance is < 1 or > 5 ||
                IsUnsafe(item.Category) || IsUnsafe(item.Statement)) ||
            archive.Revisions.Any(item => item.SourceSessionId is { } sourceId && !sessionIds.Contains(sourceId) ||
                item.PreviousContent?.Length > 4000 || string.IsNullOrWhiteSpace(item.NewContent) ||
                item.NewContent.Length > 4000 || string.IsNullOrWhiteSpace(item.ChangedBy) ||
                item.ChangedBy.Length > 80 || !Enum.IsDefined(item.RecordType) ||
                item.RecordType == ContinuityRecordType.Fact &&
                    !archive.Facts.Any(fact => fact.Id == item.RecordId) ||
                item.RecordType == ContinuityRecordType.Summary &&
                    !sessionIds.Contains(item.RecordId) ||
                IsUnsafe(item.PreviousContent) || IsUnsafe(item.NewContent)))
        {
            errors["continuity"] = ["A roll, campaign fact, or continuity revision is invalid or references a missing session or hero."];
        }
        if (archive.Sessions.Count > 0)
        {
            var latestSessionId = archive.Sessions.MaxBy(item => item.SessionNumber)!.Id;
            var latestSessionChecks = archive.Checks
                .Where(item => item.SessionId == latestSessionId)
                .GroupBy(item => item.HeroId)
                .ToDictionary(
                    group => group.Key,
                    group => group.MaxBy(item => item.CreatedAtUtc)!);
            if (archive.Heroes.Any(hero =>
                    latestSessionChecks.TryGetValue(hero.Id, out var latestCheck)
                        ? hero.Hearts != latestCheck.HeartsAfter ||
                          hero.SparkleTokens != latestCheck.SparkleTokensAfter
                        : hero.Hearts != 3 || hero.SparkleTokens != 1))
            {
                errors["heroResources"] = ["Current hero hearts and sparkle tokens must match the latest saved session checks."];
            }
        }

        if (archive.Relationships.Any(item => !Enum.IsDefined(item.SubjectType) ||
                !Enum.IsDefined(item.TargetType) ||
                !participantIds.Contains((item.SubjectType, item.SubjectId)) ||
                !participantIds.Contains((item.TargetType, item.TargetId)) ||
                string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 1000 ||
                IsUnsafe(item.Description)) ||
            archive.Rewards.Any(item => item.HeroId is { } heroId && !heroIds.Contains(heroId) ||
                string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 120 ||
                string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 1000 ||
                IsUnsafe(item.Name) || IsUnsafe(item.Description)) ||
            archive.BibleVersions.Any(item => item.Version < 1 ||
                string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 120 ||
                string.IsNullOrWhiteSpace(item.ContentJson) || item.ContentJson.Length > 30000 ||
            IsUnsafe(item.Title) || IsUnsafe(item.ContentJson) || !IsValidJsonObject(item.ContentJson)) ||
            archive.BibleVersions.Select(item => item.Version).Distinct().Count() != archive.BibleVersions.Count ||
            archive.AdventureDrafts.Any(item => item.SessionLengthMinutes is < 15 or > 180 ||
                item.ParentPreferences?.Length > 500 || !Enum.IsDefined(item.Status) ||
                item.GenerationNumber < 1 || string.IsNullOrWhiteSpace(item.ContentJson) ||
                item.ContentJson.Length > 16000 ||
                item.ActivatedQuestId is { } questId && !questIds.Contains(questId) ||
                IsUnsafe(item.ParentPreferences) || IsUnsafe(item.ContentJson) ||
                !IsValidAdventurePlanJson(item.ContentJson)) ||
            archive.AdventureDrafts.Where(item => item.ActivatedQuestId is not null)
                .Select(item => item.ActivatedQuestId)
                .Distinct().Count() != archive.AdventureDrafts.Count(item => item.ActivatedQuestId is not null))
        {
            errors["campaignData"] = ["A relationship, reward, bible version, or adventure draft is invalid or references a missing campaign record."];
        }

        return errors;
    }

    private static bool CheckIsValid(
        CampaignArchiveCheck check,
        IReadOnlyDictionary<Guid, CampaignArchiveHero> heroesById,
        IReadOnlySet<Guid> sessionIds)
    {
        if (!heroesById.TryGetValue(check.HeroId, out var hero) ||
            !sessionIds.Contains(check.SessionId) ||
            check.Roll is < 1 or > 20 ||
            !Enum.IsDefined(check.RollSource) ||
            !Enum.IsDefined(check.Difficulty) ||
            !Enum.IsDefined(check.Outcome) ||
            !Enum.IsDefined(check.ConsequenceCategory) ||
            check.Strength?.Length > 80 ||
            IsUnsafe(check.Strength))
        {
            return false;
        }

        var expectedTarget = check.Difficulty switch
        {
            CheckDifficulty.Easy => 8,
            CheckDifficulty.Tricky => 12,
            CheckDifficulty.Heroic => 16,
            _ => 0
        };
        var strength = check.Strength?.Trim();
        var hasStrength = !string.IsNullOrEmpty(strength);
        if (check.Target != expectedTarget ||
            check.StrengthBonus != (hasStrength ? 2 : 0) ||
            hasStrength && !hero.Strengths.Any(value =>
                string.Equals(value.Trim(), strength, StringComparison.OrdinalIgnoreCase)) ||
            check.SparkleBonus != (check.SparkleTokenSpent ? 3 : 0) ||
            check.HeartsBefore is < 0 or > 3 ||
            check.SparkleTokensBefore is < 0 or > 1 ||
            check.SparkleTokenSpent && check.SparkleTokensBefore < 1 ||
            check.SparkleTokenSpent && check.Roll + check.StrengthBonus >= check.Target ||
            check.Total != check.Roll + check.StrengthBonus + check.SparkleBonus)
        {
            return false;
        }

        var expectedOutcome = check.Total >= check.Target + 5
            ? CheckOutcome.StrongSuccess
            : check.Total >= check.Target
                ? CheckOutcome.Success
                : check.Total >= check.Target - 2
                    ? CheckOutcome.SuccessWithComplication
                    : CheckOutcome.SetbackWithProgress;
        var losesHeart = expectedOutcome == CheckOutcome.SetbackWithProgress &&
                         check.Risky && check.HeartsBefore > 0;
        var expectedConsequence = expectedOutcome switch
        {
            CheckOutcome.StrongSuccess => ConsequenceCategory.ExtraBenefit,
            CheckOutcome.Success => ConsequenceCategory.None,
            CheckOutcome.SuccessWithComplication => ConsequenceCategory.GentleComplication,
            CheckOutcome.SetbackWithProgress when losesHeart => ConsequenceCategory.GentleSetbackWithHeartLoss,
            CheckOutcome.SetbackWithProgress => ConsequenceCategory.GentleSetback,
            _ => throw new ArgumentOutOfRangeException(nameof(check))
        };

        return check.Outcome == expectedOutcome &&
               check.ForwardProgressRequired == (expectedOutcome is CheckOutcome.SuccessWithComplication or CheckOutcome.SetbackWithProgress) &&
               check.ConsequenceCategory == expectedConsequence &&
               check.HeartsAfter == check.HeartsBefore - (losesHeart ? 1 : 0) &&
               check.SparkleTokensAfter == check.SparkleTokensBefore - (check.SparkleTokenSpent ? 1 : 0);
    }

    private static bool AllowedParentInstruction(string? instruction) =>
        instruction is null or
            "Make the next challenge easier and ensure the heroes make useful progress." or
            "Offer a clear, useful clue now without requiring a roll." or
            "Skip the current scene and move directly to a fresh, player-led moment." or
            "Gently move the story toward a satisfying stopping point." or
            "Bring the story to a gentle stopping point now, with no new cliffhanger.";

    private static bool IsUnsafe(string? text) =>
        text is not null && !CampaignDraftValidator.IsSafeGeneratedText(text);

    private static bool IsValidAdventurePlanJson(string? json)
    {
        if (json is null)
        {
            return true;
        }
        try
        {
            return JsonSerializer.Deserialize<AdventureDraftContent>(json, AdventureDraftJson.Options) is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsValidJsonObject(string? json)
    {
        if (json is null)
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
