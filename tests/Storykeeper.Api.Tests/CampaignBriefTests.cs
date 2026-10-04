using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Xunit;

namespace Storykeeper.Api.Tests;

public sealed class CampaignBriefTests
{
    [Fact]
    public async Task CampaignBriefCanBeCreatedLoadedUpdatedListedAndDiscarded()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new CampaignBriefService(database.Context);
        var created = await service.CreateAsync(new CampaignBrief
        {
            Title = "The Moonlit Woods",
            Genre = "Cozy fantasy",
            Tone = "Warm, funny, and adventurous",
            CampaignLengthSessions = 6,
            SessionLengthMinutes = 45,
            Inclusions = ["Friendly dragons"],
            Exclusions = ["Storms"],
            StoryIdea = "A dragon is looking for a home."
        });

        var loaded = await service.GetAsync(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal("The Moonlit Woods", loaded.Title);
        Assert.Equal(["Friendly dragons"], loaded.Inclusions);
        Assert.Equal(["Storms"], loaded.Exclusions);
        Assert.Equal("A dragon is looking for a home.", loaded.StoryIdea);
        Assert.Equal(CampaignBriefDefaults.SafetyBoundaries, loaded.SafetyBoundaries);

        var updated = await service.UpdateAsync(created.Id, new CampaignBrief
        {
            Title = "The Starry Woods",
            Genre = "Gentle mystery",
            Tone = "Curious and kind",
            CampaignLengthSessions = 3,
            SessionLengthMinutes = 30,
            Inclusions = ["Helpful foxes"],
            Exclusions = [],
            SafetyBoundaries = ["User-provided boundary"]
        });

        Assert.NotNull(updated);
        Assert.Equal("The Starry Woods", updated.Title);
        Assert.Equal(CampaignBriefDefaults.SafetyBoundaries, updated.SafetyBoundaries);
        Assert.Single(await service.ListAsync());
        Assert.True(await service.DeleteAsync(created.Id));
        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public void CampaignBriefRequestValidatorRejectsInvalidBoundsAndNullListEntries()
    {
        var errors = CampaignBriefRequestValidator.Validate(new CampaignBriefRequest(
            "A title",
            "Cozy fantasy",
            "Warm",
            31,
            10,
            [null!],
            [],
            new string('x', 2001)));

        Assert.Contains("campaignLengthSessions", errors.Keys);
        Assert.Contains("sessionLengthMinutes", errors.Keys);
        Assert.Contains("inclusions", errors.Keys);
        Assert.Contains("storyIdea", errors.Keys);
        Assert.Empty(CampaignBriefRequestValidator.Validate(new CampaignBriefRequest(
            "A title",
            "Cozy fantasy",
            "Warm",
            6,
            45,
            null,
            null,
            null)));
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
