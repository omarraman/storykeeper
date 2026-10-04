using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Xunit;

namespace Storykeeper.Api.Tests;

public sealed class CampaignPersistenceTests
{
    [Fact]
    public async Task CampaignCanBeCreatedLoadedUpdatedAndArchived()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new CampaignRepository(database.Context);
        var service = new CampaignService(repository);

        var created = await service.CreateAsync("  The Moonlit Woods  ", "A gentle mystery");
        var loaded = await service.GetAsync(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal("The Moonlit Woods", loaded.Name);
        Assert.Equal("A gentle mystery", loaded.Description);
        Assert.NotNull(loaded.Settings);
        Assert.True(loaded.Settings.LowFright);
        Assert.NotNull(loaded.Bible);
        Assert.NotNull(loaded.Party);

        var updated = await service.UpdateAsync(created.Id, "Moonlit Woods", "A kind mystery");

        Assert.NotNull(updated);
        Assert.Equal("Moonlit Woods", updated.Name);
        Assert.Equal("A kind mystery", updated.Description);
        Assert.True(await service.ArchiveAsync(created.Id));

        var archived = await service.GetAsync(created.Id);
        Assert.NotNull(archived);
        Assert.Equal(CampaignStatus.Archived, archived.Status);
        Assert.NotNull(archived.ArchivedAtUtc);
        Assert.Null(await service.UpdateAsync(created.Id, "Renamed", null));
    }

    [Fact]
    public async Task CampaignEntitiesAreReadOnlyWithinTheirOwningCampaign()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaigns = new CampaignRepository(database.Context);
        var entities = new CampaignEntityRepository(database.Context);
        var service = new CampaignService(campaigns);
        var first = await service.CreateAsync("First World", null);
        var second = await service.CreateAsync("Second World", null);
        var hero = new Hero
        {
            CampaignId = first.Id,
            PartyId = first.Party!.Id,
            Name = "Pip",
            Description = "A cheerful explorer",
            Role = "Ranger"
        };

        await entities.AddAsync(first.Id, hero);

        Assert.NotNull(await entities.GetAsync<Hero>(first.Id, hero.Id));
        Assert.Null(await entities.GetAsync<Hero>(second.Id, hero.Id));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            entities.AddAsync(second.Id, new Hero
            {
                CampaignId = first.Id,
                PartyId = first.Party.Id,
                Name = "Intruder",
                Description = "Not in this world",
                Role = "Scout"
            }));
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            entities.AddAsync(second.Id, new Hero
            {
                CampaignId = second.Id,
                PartyId = first.Party.Id,
                Name = "Crossed Boundary",
                Description = "Belongs to another party",
                Role = "Scout"
            }));
    }

    [Fact]
    public async Task CampaignFactsPersistTheirMetadataAndSourceSession()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaigns = new CampaignRepository(database.Context);
        var entities = new CampaignEntityRepository(database.Context);
        var campaign = await new CampaignService(campaigns).CreateAsync("Fact World", null);
        var session = await entities.AddAsync(campaign.Id, new Session
        {
            CampaignId = campaign.Id,
            SessionNumber = 1,
            Summary = "The party found a silver feather."
        });
        var fact = await entities.AddAsync(campaign.Id, new CampaignFact
        {
            CampaignId = campaign.Id,
            SourceSessionId = session.Id,
            Category = "Clue",
            Statement = "The silver feather belongs to the sky messenger.",
            Status = CampaignFactStatus.Active,
            Importance = 4
        });

        var stored = await entities.GetAsync<CampaignFact>(campaign.Id, fact.Id);

        Assert.NotNull(stored);
        Assert.Equal("Clue", stored.Category);
        Assert.Equal(CampaignFactStatus.Active, stored.Status);
        Assert.Equal(4, stored.Importance);
        Assert.Equal(session.Id, stored.SourceSessionId);
    }

    [Fact]
    public async Task CampaignsCanBeListedCompletedResumedAndDeletedIndependently()
    {
        Assert.Equal(1, (int)CampaignStatus.Archived);
        await using var database = await TestDatabase.CreateAsync();
        var campaignRepository = new CampaignRepository(database.Context);
        var entityRepository = new CampaignEntityRepository(database.Context);
        var service = new CampaignService(campaignRepository);
        var first = await service.CreateAsync("First World", "A moonlit forest");
        var second = await service.CreateAsync("Second World", "A friendly space station");
        var hero = await entityRepository.AddAsync(first.Id, new Hero
        {
            CampaignId = first.Id,
            PartyId = first.Party!.Id,
            Name = "Pip",
            Description = "A cheerful explorer",
            Role = "Ranger"
        });
        await entityRepository.AddAsync(first.Id, new Quest
        {
            CampaignId = first.Id,
            Title = "Find the lost star",
            Description = "Follow the silver trail.",
            Status = QuestStatus.InProgress
        });
        var session = await entityRepository.AddAsync(first.Id, new Session
        {
            CampaignId = first.Id,
            SessionNumber = 1,
            Summary = "Pip found a clue beneath the old oak."
        });
        await entityRepository.AddAsync(first.Id, new CampaignFact
        {
            CampaignId = first.Id,
            SourceSessionId = session.Id,
            Category = "Clue",
            Statement = "The trail leads north.",
            Status = CampaignFactStatus.Active,
            Importance = 3
        });

        var listed = await service.ListAsync();
        var firstListed = Assert.Single(listed, campaign => campaign.Id == first.Id);
        Assert.Single(firstListed.Party!.Heroes);
        Assert.Single(firstListed.Quests);
        Assert.Equal("Pip found a clue beneath the old oak.", Assert.Single(firstListed.Sessions).Summary);
        Assert.Empty(Assert.Single(listed, campaign => campaign.Id == second.Id).Party!.Heroes);

        Assert.True(await service.CompleteAsync(first.Id));
        Assert.Null(await service.UpdateAsync(first.Id, "Changed", null));
        var completed = await service.GetAsync(first.Id);
        Assert.NotNull(completed);
        Assert.Equal(CampaignStatus.Completed, completed.Status);
        Assert.Equal("Pip", Assert.Single(completed.Party!.Heroes).Name);
        Assert.True(await service.ArchiveAsync(first.Id));
        Assert.Equal(CampaignStatus.Archived, (await service.GetAsync(first.Id))!.Status);

        Assert.True(await service.DeleteAsync(first.Id));
        Assert.Null(await service.GetAsync(first.Id));
        Assert.Null(await entityRepository.GetAsync<Hero>(second.Id, hero.Id));
        Assert.Equal("Second World", (await service.GetAsync(second.Id))!.Name);
    }

    [Fact]
    public async Task RelationshipParticipantsCannotCrossCampaignBoundaries()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaignRepository = new CampaignRepository(database.Context);
        var entityRepository = new CampaignEntityRepository(database.Context);
        var service = new CampaignService(campaignRepository);
        var first = await service.CreateAsync("First World", null);
        var second = await service.CreateAsync("Second World", null);
        var hero = await entityRepository.AddAsync(first.Id, new Hero
        {
            CampaignId = first.Id,
            PartyId = first.Party!.Id,
            Name = "Pip",
            Description = "A cheerful explorer",
            Role = "Ranger"
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            entityRepository.AddAsync(second.Id, new Relationship
            {
                CampaignId = second.Id,
                SubjectType = RelationshipParticipantType.Hero,
                SubjectId = hero.Id,
                TargetType = RelationshipParticipantType.Hero,
                TargetId = hero.Id,
                Description = "A relationship must stay in one campaign."
            }));
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
