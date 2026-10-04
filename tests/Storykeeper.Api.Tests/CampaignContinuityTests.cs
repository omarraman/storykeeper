using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Xunit;

namespace Storykeeper.Api.Tests;

public sealed class CampaignContinuityTests
{
    private const string Narration =
        "The friendly mapmaker smiles as the lantern settles beside the path. A little tune drifts over the hill, and the map reveals a silver star. There are several safe ways to follow the clue together.";

    [Fact]
    public async Task ParentFactCorrectionsChangeStatusAndKeepSourceAndRevisionHistory()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, session) = await CreateCampaignAsync(database.Context, "Lantern Garden");
        var service = new CampaignContinuityService(database.Context);
        var fact = await service.CreateFactAsync(campaign.Id, new CreateCampaignFactRequest(
            "promise", "Mira promised to show Pip the hidden map room.", "Active", 5, session.Id));

        var corrected = await service.UpdateFactAsync(campaign.Id, fact!.Id, new UpdateCampaignFactRequest(
            "promise", "Mira promised to help Pip find the map room.", "Resolved", 4));
        var continuity = await service.GetAsync(campaign.Id);

        Assert.Equal("Resolved", corrected!.Status);
        Assert.Equal(session.Id, corrected.SourceSessionId);
        Assert.Equal(2, continuity!.Revisions.Count);
        Assert.Contains(continuity.Revisions, revision =>
            revision.PreviousContent is null && revision.SourceSessionId == session.Id);
        Assert.Contains(continuity.Revisions, revision =>
            revision.PreviousContent!.Contains("hidden map room", StringComparison.Ordinal) &&
            revision.NewContent.Contains("find the map room", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SummaryEndsSessionAndParentCorrectionsAreAudited()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, session) = await CreateCampaignAsync(database.Context, "Lantern Garden");
        var service = new CampaignContinuityService(database.Context);

        var ended = await service.SaveSummaryAsync(campaign.Id, session.Id,
            new SaveSessionSummaryRequest("Pip found a silver star map. Mira promised to help open the map room."));
        var corrected = await service.SaveSummaryAsync(campaign.Id, session.Id,
            new SaveSessionSummaryRequest("Pip earned the silver star map. Mira promised to open the map room."));
        var savedSession = await database.Context.Sessions.SingleAsync(item => item.Id == session.Id);
        var continuity = await service.GetAsync(campaign.Id);

        Assert.NotNull(ended!.EndedAtUtc);
        Assert.Equal(ended.EndedAtUtc, corrected!.EndedAtUtc);
        Assert.Equal("Pip earned the silver star map. Mira promised to open the map room.", savedSession.Summary);
        Assert.Equal(2, continuity!.Revisions.Count(revision => revision.RecordType == "Summary"));
        Assert.Contains(continuity.Revisions, revision =>
            revision.RecordType == "Summary" &&
            revision.PreviousContent!.Contains("found a silver star", StringComparison.Ordinal) &&
            revision.NewContent.Contains("earned the silver star", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StoryPromptUsesApprovedFactsSummariesRelationshipsAndRewardsOnlyFromItsCampaign()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, hero, session) = await CreatePlayableCampaignAsync(database.Context, "Lantern Garden");
        var (otherCampaign, _, _) = await CreatePlayableCampaignAsync(database.Context, "Other World");
        database.Context.CampaignFacts.AddRange(
            new CampaignFact
            {
                CampaignId = campaign.Id,
                SourceSessionId = session.Id,
                Category = "promise",
                Statement = "Mira still owes Pip a map-room tour.",
                Status = CampaignFactStatus.Active,
                Importance = 1
            },
            new CampaignFact
            {
                CampaignId = campaign.Id,
                SourceSessionId = session.Id,
                Category = "clue",
                Statement = "An unapproved clue.",
                Status = CampaignFactStatus.Proposed,
                Importance = 5
            },
            new CampaignFact
            {
                CampaignId = campaign.Id,
                SourceSessionId = session.Id,
                Category = "thread",
                Statement = "A resolved promise.",
                Status = CampaignFactStatus.Resolved,
                Importance = 5
            },
            new CampaignFact
            {
                CampaignId = otherCampaign.Id,
                Category = "clue",
                Statement = "A fact from another story.",
                Status = CampaignFactStatus.Active,
                Importance = 5
            });
        for (var index = 0; index < 20; index++)
        {
            database.Context.CampaignFacts.Add(new CampaignFact
            {
                CampaignId = campaign.Id,
                Category = "world",
                Statement = $"Important world fact {index}.",
                Status = CampaignFactStatus.Active,
                Importance = 5
            });
        }
        var previousSession = new Session
        {
            CampaignId = campaign.Id,
            SessionNumber = 1,
            EndedAtUtc = DateTimeOffset.UtcNow,
            Summary = "Pip earned the silver star map."
        };
        session.SessionNumber = 2;
        database.Context.Sessions.Add(previousSession);
        database.Context.Relationships.Add(new Relationship
        {
            CampaignId = campaign.Id,
            SubjectType = RelationshipParticipantType.Hero,
            SubjectId = hero.Id,
            TargetType = RelationshipParticipantType.Npc,
            TargetId = Guid.NewGuid(),
            Description = "Mira trusts Pip with important maps."
        });
        database.Context.Rewards.Add(new Reward
        {
            CampaignId = campaign.Id,
            HeroId = hero.Id,
            Name = "Silver star map",
            Description = "A map earned from Mira.",
            IsClaimed = true
        });
        await database.Context.SaveChangesAsync();
        var generator = new TestGenerator();
        var service = new StoryTurnService(database.Context, generator);

        await service.SubmitActionAsync(campaign.Id, new StoryTurnRequest(
            "Ask Mira about the map room.", session.Id, hero.Id));

        using var context = JsonDocument.Parse(generator.Input!.ContextJson);
        var contextText = context.RootElement.GetRawText();
        Assert.Contains("Mira still owes Pip a map-room tour.", contextText);
        Assert.Contains("Pip earned the silver star map.", contextText);
        Assert.Contains("Mira trusts Pip with important maps.", contextText);
        Assert.Contains("Silver star map", contextText);
        Assert.DoesNotContain("Mira still owes Pip a map-room tour.", string.Join(
            " ",
            context.RootElement.GetProperty("activeFacts").EnumerateArray()
                .Select(item => item.GetProperty("statement").GetString())));
        Assert.Contains("Mira still owes Pip a map-room tour.", string.Join(
            " ",
            context.RootElement.GetProperty("unresolvedThreads").EnumerateArray()
                .Select(item => item.GetProperty("statement").GetString())));
        Assert.DoesNotContain("An unapproved clue.", contextText);
        Assert.DoesNotContain("A resolved promise.", contextText);
        Assert.DoesNotContain("A fact from another story.", contextText);
    }

    [Fact]
    public async Task ParentCannotAttachFactToASessionFromAnotherCampaign()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, _) = await CreateCampaignAsync(database.Context, "First");
        var (otherCampaign, otherSession) = await CreateCampaignAsync(database.Context, "Second");
        var service = new CampaignContinuityService(database.Context);

        await Assert.ThrowsAsync<RuleValidationException>(() => service.CreateFactAsync(
            campaign.Id,
            new CreateCampaignFactRequest("clue", "A clue.", "Active", 3, otherSession.Id)));

        Assert.Empty(await database.Context.CampaignFacts.ToArrayAsync());
    }

    [Fact]
    public async Task MigrationPreservesLegacySupersededAndDiscardedFactStatuses()
    {
        await using var database = await TestDatabase.CreateAsync("20261004093617_ChildFriendlyRules");
        var campaign = await new CampaignService(new CampaignRepository(database.Context))
            .CreateAsync("Legacy World", null);
        database.Context.CampaignFacts.AddRange(
            new CampaignFact
            {
                CampaignId = campaign.Id,
                Category = "clue",
                Statement = "This clue was superseded.",
                Status = (CampaignFactStatus)2
            },
            new CampaignFact
            {
                CampaignId = campaign.Id,
                Category = "clue",
                Statement = "This clue was discarded.",
                Status = (CampaignFactStatus)3
            });
        await database.Context.SaveChangesAsync();

        await database.Context.Database.MigrateAsync();
        database.Context.ChangeTracker.Clear();
        var statuses = await database.Context.CampaignFacts
            .OrderBy(item => item.Statement)
            .Select(item => item.Status)
            .ToArrayAsync();

        Assert.Equal([CampaignFactStatus.Discarded, CampaignFactStatus.Superseded], statuses);
    }

    private static async Task<(Campaign Campaign, Session Session)> CreateCampaignAsync(
        StorykeeperDbContext context,
        string name)
    {
        var campaign = await new CampaignService(new CampaignRepository(context)).CreateAsync(name, null);
        var session = new Session { CampaignId = campaign.Id, SessionNumber = 1 };
        context.Sessions.Add(session);
        await context.SaveChangesAsync();
        return (campaign, session);
    }

    private static async Task<(Campaign Campaign, Hero Hero, Session Session)> CreatePlayableCampaignAsync(
        StorykeeperDbContext context,
        string name)
    {
        var campaign = await new CampaignService(new CampaignRepository(context)).CreateAsync(name, null);
        var hero = new Hero
        {
            CampaignId = campaign.Id,
            PartyId = campaign.Party!.Id,
            Name = "Pip",
            Description = "A curious scout.",
            Role = "Scout",
            Strengths = ["Noticing tiny details"]
        };
        var session = new Session { CampaignId = campaign.Id, SessionNumber = 1 };
        var npc = new Npc
        {
            CampaignId = campaign.Id,
            Name = "Mira",
            Description = "A helpful mapmaker.",
            Disposition = "Friendly"
        };
        context.Heroes.Add(hero);
        context.Sessions.Add(session);
        context.Npcs.Add(npc);
        context.Quests.Add(new Quest
        {
            CampaignId = campaign.Id,
            Title = "Find the lantern map",
            Description = "Follow the map.",
            Status = QuestStatus.InProgress
        });
        await context.SaveChangesAsync();
        return (campaign, hero, session);
    }

    private sealed class TestGenerator : IStoryTurnGenerator
    {
        public StoryTurnGenerationInput? Input { get; private set; }

        public Task<StoryTurnContent> GenerateAsync(
            StoryTurnGenerationInput input,
            CancellationToken cancellationToken = default)
        {
            Input = input;
            return Task.FromResult(new StoryTurnContent(
                Narration,
                null,
                [],
                null,
                [new StoryTurnChoice("ask-mira", "Ask Mira about the map.")],
                []));
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection, StorykeeperDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public StorykeeperDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync(string? migration = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<StorykeeperDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new StorykeeperDbContext(options);
            await context.Database.MigrateAsync(migration);
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
