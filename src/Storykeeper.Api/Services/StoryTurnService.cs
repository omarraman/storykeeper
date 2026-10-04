using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class StoryTurnService(
    StorykeeperDbContext dbContext,
    IStoryTurnGenerator generator) : IStoryTurnService
{
    private static readonly JsonSerializerOptions ContextJsonOptions = AdventureDraftJson.Options;

    public async Task<StoryTurnResponse?> SubmitActionAsync(
        Guid campaignId,
        StoryTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns.AsNoTracking()
            .Include(item => item.Settings)
            .Include(item => item.Bible)
            .Include(item => item.Party)
                .ThenInclude(party => party!.Heroes)
                    .ThenInclude(hero => hero.Inventory)
            .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        if (campaign.Status != CampaignStatus.Active)
        {
            throw new RuleConflictException("Story actions can only be sent to an active campaign.");
        }

        var session = await dbContext.Sessions.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.CampaignId == campaignId && item.Id == request.SessionId, cancellationToken);
        var latestSession = await dbContext.Sessions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderByDescending(item => item.SessionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        if (session.EndedAtUtc is not null || latestSession?.Id != session.Id)
        {
            throw new RuleConflictException("Story actions can only be sent to the current active session.");
        }

        if (session.IsPaused)
        {
            throw new RuleConflictException("The story is paused by a parent. Resume it from parent controls to continue.");
        }

        var heroes = campaign.Party?.Heroes.OrderBy(hero => hero.Name).ToArray() ?? [];
        Hero? selectedHero = null;
        if (request.HeroId is { } heroId)
        {
            selectedHero = heroes.SingleOrDefault(hero => hero.Id == heroId);
            if (selectedHero is null)
            {
                throw new RuleValidationException("Choose a hero from this campaign.");
            }
        }

        var quests = await dbContext.Quests.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var currentQuest = quests
            .Where(quest => quest.Status == QuestStatus.InProgress)
            .OrderBy(quest => quest.Title)
            .FirstOrDefault();
        var facts = await dbContext.CampaignFacts.AsNoTracking()
            .Where(item => item.CampaignId == campaignId && item.Status == CampaignFactStatus.Active)
            .OrderByDescending(item => item.Importance)
            .ThenBy(item => item.Statement)
            .Take(20)
            .ToArrayAsync(cancellationToken);
        var unresolvedFacts = await dbContext.CampaignFacts.AsNoTracking()
            .Where(item => item.CampaignId == campaignId &&
                           item.Status == CampaignFactStatus.Active &&
                           (item.Category == "promise" || item.Category == "thread"))
            .OrderByDescending(item => item.Importance)
            .ThenBy(item => item.Statement)
            .Take(10)
            .ToArrayAsync(cancellationToken);
        var existingFactStatements = await dbContext.CampaignFacts.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .Select(item => item.Statement)
            .ToArrayAsync(cancellationToken);
        var summaries = await dbContext.Sessions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId &&
                           item.EndedAtUtc != null &&
                           item.Summary != null &&
                           item.Summary != "")
            .OrderByDescending(item => item.SessionNumber)
            .Take(3)
            .Select(item => item.Summary)
            .ToArrayAsync(cancellationToken);
        var completedQuests = quests.Where(quest => quest.Status == QuestStatus.Completed)
            .OrderBy(quest => quest.Title)
            .Take(5)
            .ToArray();
        var relationships = await dbContext.Relationships.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderBy(item => item.Id)
            .Take(20)
            .ToArrayAsync(cancellationToken);
        var rewards = await dbContext.Rewards.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderBy(item => item.Name)
            .Take(20)
            .ToArrayAsync(cancellationToken);
        var npcs = await dbContext.Npcs.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderBy(item => item.Name)
            .Take(20)
            .ToArrayAsync(cancellationToken);
        var locations = await dbContext.Locations.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderBy(item => item.Name)
            .Take(20)
            .ToArrayAsync(cancellationToken);

        var contextHeroes = selectedHero is null ? heroes : [selectedHero];
        var context = new
        {
            campaign = new
            {
                campaign.Name,
                description = Limit(campaign.Description, 1000),
                theme = campaign.Settings?.Theme,
                tone = campaign.Settings?.Tone,
                lowFright = campaign.Settings?.LowFright ?? true,
                parentSafetySettings = campaign.Settings?.SafetySettings ?? ParentSafetySettings.Defaults,
                world = Limit(campaign.Bible?.WorldDescription, 1800),
                currentSituation = Limit(campaign.Bible?.CurrentSituation, 1000)
            },
            currentSession = new
            {
                session.SessionNumber,
                summary = Limit(session.Summary, 1200),
                parentInstruction = session.ParentInstruction
            },
            party = contextHeroes.Select(hero => new
            {
                hero.Name,
                hero.Role,
                description = Limit(hero.Description, 600),
                hero.Strengths,
                inventory = hero.Inventory.Take(20).Select(item => new
                {
                    item.Name,
                    description = Limit(item.Description, 300),
                    item.Quantity
                })
            }),
            currentQuest = currentQuest is null ? null : new
            {
                currentQuest.Title,
                description = Limit(currentQuest.Description, 800),
                sessionLengthMinutes = currentQuest.SessionLengthMinutes,
                adventurePlan = currentQuest.AdventurePlanJson is null
                    ? null
                    : JsonSerializer.Deserialize<AdventureDraftContent>(
                        currentQuest.AdventurePlanJson, ContextJsonOptions)
            },
            completedQuests = completedQuests.Select(quest => new
            {
                quest.Title,
                description = Limit(quest.Description, 500)
            }),
            activeFacts = facts.Select(fact => new
            {
                fact.Category,
                statement = Limit(fact.Statement, 600),
                fact.Importance
            }),
            unresolvedThreads = unresolvedFacts.Select(fact => new
                {
                    fact.Category,
                    statement = Limit(fact.Statement, 600),
                    fact.Importance
                }),
            latestSessionSummaries = summaries.Select(summary => Limit(summary, 1200)),
            relationships = relationships.Select(relationship => new
            {
                subjectType = relationship.SubjectType.ToString(),
                subjectName = ParticipantName(relationship.SubjectType, relationship.SubjectId, heroes, npcs),
                relationship.SubjectId,
                targetType = relationship.TargetType.ToString(),
                targetName = ParticipantName(relationship.TargetType, relationship.TargetId, heroes, npcs),
                relationship.TargetId,
                description = Limit(relationship.Description, 500)
            }),
            rewards = rewards.Select(reward => new
            {
                reward.Name,
                description = Limit(reward.Description, 500),
                reward.IsClaimed
            }),
            npcs = npcs.Take(10).Select(npc => new
            {
                npc.Name,
                description = Limit(npc.Description, 500),
                disposition = Limit(npc.Disposition, 200)
            }),
            locations = locations.Take(10).Select(location => new
            {
                location.Name,
                description = Limit(location.Description, 500)
            }),
            resolvedCheck = await LoadResolvedCheckAsync(
                campaignId, session.Id, selectedHero, request.CheckResolutionId, cancellationToken)
        };

        var generated = await generator.GenerateAsync(new StoryTurnGenerationInput(
            request.Action!.Trim(),
            JsonSerializer.Serialize(context, ContextJsonOptions)), cancellationToken);
        if (session.ParentInstruction == "Make the next challenge easier and ensure the heroes make useful progress." &&
            generated.RollRequest is { } easierRoll)
        {
            generated = generated with
            {
                RollRequest = easierRoll with
                {
                    Difficulty = CheckDifficulty.Easy.ToString(),
                    Risky = false
                }
            };
        }

        var safetySettings = campaign.Settings?.SafetySettings ?? ParentSafetySettings.Defaults;
        var errors = StoryTurnContentValidator.Validate(generated, npcs, selectedHero, safetySettings);
        if (errors.Count > 0)
        {
            throw new StoryTurnGenerationException(502,
                "The Storykeeper could not safely shape that response. Please try the action again.");
        }

        var content = StoryTurnContentValidator.Normalize(generated);
        if (request.CheckResolutionId is not null && content.RollRequest is not null)
        {
            throw new StoryTurnGenerationException(502,
                "The Storykeeper could not safely shape that response. Please try the action again.");
        }

        if (content.RollRequest is not null)
        {
            if (session.ParentInstruction is not null)
            {
                await dbContext.Sessions
                    .Where(item => item.CampaignId == campaignId && item.Id == session.Id)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.ParentInstruction, (string?)null),
                        cancellationToken);
            }

            return StoryTurnResponse.ForRoll(content.RollRequest);
        }

        if (content.ProposedFacts!.Count > 0)
        {
            var existingStatements = existingFactStatements.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newFacts = content.ProposedFacts
                .Where(proposal => existingStatements.Add(proposal!.Statement!))
                .Select(proposal => new CampaignFact
                {
                    CampaignId = campaignId,
                    SourceSessionId = session.Id,
                    Category = proposal!.Category!,
                    Statement = proposal.Statement!,
                    Importance = proposal.Importance,
                    Status = CampaignFactStatus.Proposed
                }).ToArray();
            dbContext.CampaignFacts.AddRange(newFacts);
            await dbContext.SaveChangesAsync(cancellationToken);
            facts = facts.Concat(newFacts).OrderByDescending(fact => fact.Importance).Take(20).ToArray();
        }

        if (session.ParentInstruction is not null)
        {
            await dbContext.Sessions
                .Where(item => item.CampaignId == campaignId && item.Id == session.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.ParentInstruction, (string?)null),
                    cancellationToken);
        }

        return StoryTurnResponse.ForStory(content, facts, currentQuest, heroes);
    }

    private async Task<object?> LoadResolvedCheckAsync(
        Guid campaignId,
        Guid sessionId,
        Hero? selectedHero,
        Guid? checkResolutionId,
        CancellationToken cancellationToken)
    {
        if (checkResolutionId is null)
        {
            return null;
        }

        if (selectedHero is null)
        {
            throw new RuleValidationException("A server-resolved check must belong to a selected campaign hero.");
        }

        var check = await dbContext.CheckResolutions.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.CampaignId == campaignId &&
                item.SessionId == sessionId &&
                item.HeroId == selectedHero.Id &&
                item.Id == checkResolutionId,
                cancellationToken);
        if (check is null)
        {
            throw new RuleValidationException("That check does not belong to this hero and session.");
        }

        var recentChecks = await dbContext.CheckResolutions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId &&
                           item.SessionId == sessionId &&
                           item.HeroId == selectedHero.Id)
            .ToArrayAsync(cancellationToken);
        var latestCheckId = recentChecks
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => item.Id)
            .FirstOrDefault();
        if (latestCheckId != check.Id)
        {
            throw new RuleConflictException("Only the latest resolved check can continue the story.");
        }

        return new
        {
            roll = check.Roll,
            difficulty = check.Difficulty.ToString(),
            total = check.Total,
            target = check.Target,
            outcome = check.Outcome.ToString(),
            forwardProgressRequired = check.ForwardProgressRequired,
            consequence = check.ConsequenceCategory.ToString(),
            heartsAfter = check.HeartsAfter,
            sparkleTokensAfter = check.SparkleTokensAfter
        };
    }

    private static string? Limit(string? value, int maximum) =>
        value is null || value.Length <= maximum ? value : value[..maximum];

    private static string? ParticipantName(
        RelationshipParticipantType type,
        Guid id,
        IReadOnlyList<Hero> heroes,
        IReadOnlyList<Npc> npcs) =>
        type == RelationshipParticipantType.Hero
            ? heroes.FirstOrDefault(hero => hero.Id == id)?.Name
            : npcs.FirstOrDefault(npc => npc.Id == id)?.Name;
}
