using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Xunit;
using System.Text.Json.Serialization;

namespace Storykeeper.Api.Tests;

public sealed class CampaignArchiveTests
{
    [Fact]
    public async Task CampaignArchiveRestoresContinuityIntoAFreshDatabase()
    {
        await using var source = await TestDatabase.CreateAsync();
        var campaigns = new CampaignService(new CampaignRepository(source.Context));
        var entities = new CampaignEntityRepository(source.Context);
        var campaign = await campaigns.CreateAsync("Moon Garden", "A gentle lantern mystery.");
        var hero = await entities.AddAsync(campaign.Id, new Hero
        {
            CampaignId = campaign.Id,
            PartyId = campaign.Party!.Id,
            Name = "Pip",
            Description = "A careful sky explorer.",
            Role = "Ranger",
            Strengths = ["Noticing details"]
        });
        await entities.AddAsync(campaign.Id, new InventoryItem
        {
            CampaignId = campaign.Id,
            HeroId = hero.Id,
            Name = "Silver feather",
            Description = "A clue from the garden.",
            Quantity = 1
        });
        var location = await entities.AddAsync(campaign.Id, new Location
        {
            CampaignId = campaign.Id,
            Name = "Moon Garden",
            Description = "A quiet place under the stars."
        });
        var npc = await entities.AddAsync(campaign.Id, new Npc
        {
            CampaignId = campaign.Id,
            LocationId = location.Id,
            Name = "Mira",
            Description = "A gentle gardener.",
            Disposition = "Helpful"
        });
        var quest = await entities.AddAsync(campaign.Id, new Quest
        {
            CampaignId = campaign.Id,
            Title = "The Lantern Song",
            Description = "Find the missing garden song.",
            Status = QuestStatus.InProgress,
            SessionLengthMinutes = 45,
            AdventurePlanJson = "{\"title\":\"The Lantern Song\"}"
        });
        await entities.AddAsync(campaign.Id, new Quest
        {
            CampaignId = campaign.Id,
            Title = "The Garden Picnic",
            Description = "Celebrate the repaired lantern song.",
            Status = QuestStatus.Completed,
            AdventurePlanJson = "{\"title\":\"The Garden Picnic\",\"celebrationReward\":\"A moonberry picnic\"}"
        });
        var session = await entities.AddAsync(campaign.Id, new Session
        {
            CampaignId = campaign.Id,
            SessionNumber = 1,
            Title = "The Lantern Song",
            StartedAtUtc = DateTimeOffset.Parse("2026-10-04T10:00:00Z"),
            EndedAtUtc = DateTimeOffset.Parse("2026-10-04T11:00:00Z"),
            Summary = "Pip found the moon garden and helped Mira.",
            IsPaused = false
        });
        await entities.AddAsync(campaign.Id, new CheckResolution
        {
            CampaignId = campaign.Id,
            SessionId = session.Id,
            HeroId = hero.Id,
            Roll = 19,
            RollSource = CheckRollSource.Physical,
            Difficulty = CheckDifficulty.Tricky,
            Target = 12,
            Total = 19,
            Outcome = CheckOutcome.StrongSuccess,
            ConsequenceCategory = ConsequenceCategory.ExtraBenefit,
            ForwardProgressRequired = false,
            HeartsBefore = 3,
            HeartsAfter = 3,
            SparkleTokensBefore = 1,
            SparkleTokensAfter = 1
        });
        hero.SparkleTokens = 1;
        var fact = await entities.AddAsync(campaign.Id, new CampaignFact
        {
            CampaignId = campaign.Id,
            SourceSessionId = session.Id,
            Category = "clue",
            Statement = "The silver feather points toward the old bell.",
            Status = CampaignFactStatus.Active,
            Importance = 5
        });
        await entities.AddAsync(campaign.Id, new CampaignFact
        {
            CampaignId = campaign.Id,
            SourceSessionId = session.Id,
            Category = "clue",
            Statement = "An unreviewed clue is not yet part of the storybook.",
            Status = CampaignFactStatus.Proposed,
            Importance = 5
        });
        await entities.AddAsync(campaign.Id, new CampaignContinuityRevision
        {
            CampaignId = campaign.Id,
            RecordType = ContinuityRecordType.Fact,
            RecordId = fact.Id,
            SourceSessionId = session.Id,
            NewContent = "A carefully reviewed campaign clue."
        });
        await entities.AddAsync(campaign.Id, new Relationship
        {
            CampaignId = campaign.Id,
            SubjectType = RelationshipParticipantType.Hero,
            SubjectId = hero.Id,
            TargetType = RelationshipParticipantType.Npc,
            TargetId = npc.Id,
            Description = "They are friends."
        });
        await entities.AddAsync(campaign.Id, new Reward
        {
            CampaignId = campaign.Id,
            HeroId = hero.Id,
            Name = "Star compass",
            Description = "It points toward hidden paths."
        });
        source.Context.CampaignBibleVersions.Add(new CampaignBibleVersion
        {
            CampaignId = campaign.Id,
            Version = 1,
            Title = campaign.Name,
            ContentJson = "{\"premise\":\"A moon garden\"}"
        });
        source.Context.AdventureDrafts.Add(new AdventureDraft
        {
            CampaignId = campaign.Id,
            SessionLengthMinutes = 45,
            Status = AdventureDraftStatus.Activated,
            GenerationNumber = 1,
            ContentJson = "{\"title\":\"The Lantern Song\"}",
            ActivatedQuestId = quest.Id
        });
        await source.Context.SaveChangesAsync();

        var archiveService = new CampaignArchiveService(source.Context);
        var archive = await archiveService.ExportAsync(campaign.Id);
        Assert.NotNull(archive);
        var archiveOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        archiveOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        var json = JsonSerializer.Serialize(archive, archiveOptions);
        Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionStrings", json, StringComparison.OrdinalIgnoreCase);
        var roundTrippedArchive = JsonSerializer.Deserialize<CampaignArchiveDocument>(json, archiveOptions);
        Assert.NotNull(roundTrippedArchive);

        await using var target = await TestDatabase.CreateAsync();
        var imported = await new CampaignArchiveService(target.Context).ImportAsync(roundTrippedArchive);

        Assert.True(imported.CampaignId == campaign.Id,
            string.Join("; ", imported.Errors.SelectMany(error => error.Value)));
        Assert.Empty(imported.Errors);
        var restored = await new CampaignService(new CampaignRepository(target.Context)).GetAsync(campaign.Id);
        Assert.NotNull(restored);
        Assert.Equal("Pip found the moon garden and helped Mira.", Assert.Single(restored.Sessions).Summary);
        Assert.Equal("The Lantern Song", Assert.Single(restored.Sessions).Title);
        Assert.Equal("Silver feather", Assert.Single(restored.Party!.Heroes).Inventory.Single().Name);
        Assert.Equal(1, await target.Context.CheckResolutions.CountAsync());
        Assert.Equal(1, await target.Context.CampaignContinuityRevisions.CountAsync());
        Assert.Equal(1, await target.Context.Relationships.CountAsync());
        Assert.Equal(1, await target.Context.Rewards.CountAsync());
        Assert.Equal(1, await target.Context.CampaignBibleVersions.CountAsync());
        Assert.Equal(1, await target.Context.AdventureDrafts.CountAsync());
        var duplicateImport = await new CampaignArchiveService(target.Context).ImportAsync(roundTrippedArchive);
        Assert.True(duplicateImport.Conflict);
        Assert.Equal(1, await target.Context.Campaigns.CountAsync());

        var storybook = await new CampaignArchiveService(target.Context).GetStorybookAsync(campaign.Id);
        var entry = Assert.Single(storybook!.Sessions);
        Assert.Equal("The Lantern Song", entry.Title);
        Assert.Equal("The silver feather points toward the old bell.", Assert.Single(entry.Discoveries).Statement);
        Assert.Contains("Pip", Assert.Single(entry.HeroAchievements).HeroName);
        Assert.Contains(storybook.Rewards, reward => reward.Name == "Star compass");
        Assert.Contains(storybook.Rewards, reward => reward.Name == "A moonberry picnic");

        await using var invalidTarget = await TestDatabase.CreateAsync();
        var changedCheck = roundTrippedArchive.Checks.Single() with { Total = 20 };
        var tamperedArchive = roundTrippedArchive with
        {
            Checks = [changedCheck]
        };
        var rejected = await new CampaignArchiveService(invalidTarget.Context).ImportAsync(tamperedArchive);
        Assert.Null(rejected.CampaignId);
        Assert.Contains("continuity", rejected.Errors.Keys);
        Assert.Equal(0, await invalidTarget.Context.Campaigns.CountAsync());
    }

    [Fact]
    public async Task UnsupportedArchiveVersionFailsWithoutWritingCampaignData()
    {
        await using var source = await TestDatabase.CreateAsync();
        var campaign = await new CampaignService(new CampaignRepository(source.Context))
            .CreateAsync("Exported World", null);
        var archive = await new CampaignArchiveService(source.Context).ExportAsync(campaign.Id);
        Assert.NotNull(archive);

        await using var target = await TestDatabase.CreateAsync();
        var result = await new CampaignArchiveService(target.Context)
            .ImportAsync(archive with { SchemaVersion = CampaignArchiveService.ArchiveVersion + 1 });

        Assert.Null(result.CampaignId);
        Assert.Contains("schemaVersion", result.Errors.Keys);
        Assert.Equal(0, await target.Context.Campaigns.CountAsync());
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

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<StorykeeperDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new StorykeeperDbContext(options);
            await context.Database.MigrateAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
