using System.Net;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Xunit;

namespace Storykeeper.Api.Tests;

public sealed class CampaignDraftTests
{
    [Fact]
    public async Task ExistingCampaignBiblesReceiveVersionOneWhenTheNewMigrationRuns()
    {
        await using var database = await TestDatabase.CreateAtMigrationAsync("20261004074034_CampaignBriefs");
        var campaignId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Campaigns (Id, Name, Description, Status, CreatedAtUtc, UpdatedAtUtc, ArchivedAtUtc) VALUES ({campaignId}, {"Older World"}, {"Created before bible versioning."}, {0}, {now}, {now}, {null})");
        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CampaignSettings (Id, Theme, Tone, LowFright, CampaignId) VALUES ({Guid.NewGuid()}, {"Cozy Fantasy"}, {"Warm and funny"}, {true}, {campaignId})");
        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CampaignBibles (Id, WorldDescription, CurrentSituation, CampaignId) VALUES ({Guid.NewGuid()}, {""}, {null}, {campaignId})");
        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Parties (Id, Name, CampaignId) VALUES ({Guid.NewGuid()}, {"Adventurers"}, {campaignId})");

        await database.Context.Database.MigrateAsync();

        var loaded = await new CampaignRepository(database.Context).GetAsync(campaignId);
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.Bible!.Version);
    }

    [Fact]
    public async Task DraftMustBeReviewedAndApprovedBeforeIsolatedCampaignActivation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var briefService = new CampaignBriefService(database.Context);
        var unrelatedCampaign = await new CampaignService(new CampaignRepository(database.Context))
            .CreateAsync("An unrelated world", "This save must stay separate.");
        var brief = await briefService.CreateAsync(new CampaignBrief
        {
            Title = "The Starlight Garden",
            Genre = "Cozy fantasy",
            Tone = "Warm, funny, and adventurous"
        });
        var generator = new TestGenerator(ValidContent());
        var service = new CampaignDraftService(database.Context, generator);

        var draft = await service.GenerateAsync(brief.Id);

        Assert.NotNull(draft);
        Assert.Equal(CampaignDraftStatus.PendingReview, draft.Status);
        Assert.Equal(1, generator.Calls);
        Assert.Equal(CampaignDraftActivationStatus.ApprovalRequired,
            (await service.ActivateAsync(draft.Id)).Status);
        Assert.Single(await database.Context.Campaigns.ToArrayAsync());

        var editedContent = ValidContent() with { Title = "The Starry Garden" };
        var edited = await service.UpdateAsync(draft.Id, editedContent);
        Assert.Equal(CampaignDraftOperationStatus.Succeeded, edited.Status);
        Assert.Equal(CampaignDraftStatus.PendingReview, edited.Draft!.Status);

        var approved = await service.ApproveAsync(draft.Id);
        Assert.Equal(CampaignDraftStatus.Approved, approved.Draft!.Status);
        var activation = await service.ActivateAsync(draft.Id);

        Assert.Equal(CampaignDraftActivationStatus.Activated, activation.Status);
        Assert.NotNull(activation.Campaign);
        Assert.Equal("The Starry Garden", activation.Campaign.Name);
        Assert.Equal("Cozy fantasy", activation.Campaign.Settings!.Theme);
        Assert.True(activation.Campaign.Settings.LowFright);
        Assert.Equal(1, activation.Campaign.Bible!.Version);
        Assert.Equal("The garden's stars have gone missing.", activation.Campaign.Bible.CurrentSituation);
        Assert.Equal(3, await database.Context.Locations.CountAsync(item => item.CampaignId == activation.Campaign.Id));
        Assert.Equal(3, await database.Context.Npcs.CountAsync(item => item.CampaignId == activation.Campaign.Id));
        Assert.Equal(2, await database.Context.Quests.CountAsync(item => item.CampaignId == activation.Campaign.Id));
        Assert.Empty(await database.Context.Locations.Where(item => item.CampaignId == unrelatedCampaign.Id).ToArrayAsync());
        Assert.Empty(await database.Context.Npcs.Where(item => item.CampaignId == unrelatedCampaign.Id).ToArrayAsync());
        Assert.Empty(await database.Context.Quests.Where(item => item.CampaignId == unrelatedCampaign.Id).ToArrayAsync());
        Assert.Equal(2, await database.Context.Campaigns.CountAsync());

        var versions = await service.ListBibleVersionsAsync(activation.Campaign.Id);
        var version = Assert.Single(versions!);
        Assert.Equal(1, version.Version);
        Assert.Equal(draft.Id, version.SourceDraftId);
        Assert.Equal("The Starry Garden", version.Title);
        Assert.Equal(CampaignDraftActivationStatus.AlreadyActivated, (await service.ActivateAsync(draft.Id)).Status);
        Assert.Equal(2, await database.Context.Campaigns.CountAsync());
    }

    [Fact]
    public async Task InvalidRegenerationDoesNotReplacePreviouslyValidatedDraft()
    {
        await using var database = await TestDatabase.CreateAsync();
        var brief = await new CampaignBriefService(database.Context).CreateAsync(new CampaignBrief
        {
            Title = "The Starlight Garden",
            Genre = "Cozy fantasy",
            Tone = "Warm and funny"
        });
        var generator = new TestGenerator(ValidContent() with
        {
            Safety = new CampaignDraftSafety(true, false, true, true, true)
        });
        var service = new CampaignDraftService(database.Context, generator);
        await Assert.ThrowsAsync<CampaignDraftRejectedException>(() => service.GenerateAsync(brief.Id));
        Assert.Empty(await service.ListAsync());

        generator.Content = ValidContent();
        var draft = (await service.GenerateAsync(brief.Id))!;
        var originalContent = draft.ContentJson;
        generator.Content = ValidContent() with
        {
            Safety = new CampaignDraftSafety(false, true, true, true, true)
        };

        await Assert.ThrowsAsync<CampaignDraftRejectedException>(() => service.RegenerateAsync(draft.Id));

        var stored = await service.GetAsync(draft.Id);
        Assert.NotNull(stored);
        Assert.Equal(originalContent, stored.ContentJson);
        Assert.Equal(1, stored.GenerationNumber);
        Assert.Equal(CampaignDraftStatus.PendingReview, stored.Status);
    }

    [Fact]
    public void DraftValidatorRejectsUnsafeAndOutOfScopeContent()
    {
        var errors = CampaignDraftValidator.Validate(ValidContent() with
        {
            AdventureHooks = [],
            Safety = new CampaignDraftSafety(true, true, true, true, false)
        });

        Assert.Contains("safety", errors.Keys);
        Assert.Contains("adventureHooks", errors.Keys);

        var combatErrors = CampaignDraftValidator.Validate(ValidContent() with
        {
            Premise = "The only way is to fight in a tournament."
        });
        Assert.Contains("safety", combatErrors.Keys);
    }

    [Fact]
    public async Task OpenAiCompatibleGeneratorParsesValidatedStructuredResponse()
    {
        var expected = ValidContent();
        var responseJson = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-example",
            @object = "chat.completion",
            created = 1_728_000_000,
            model = "family-safe-model",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    },
                    finish_reason = "stop"
                }
            }
        });
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson)
        });
        using var client = new HttpClient(handler);
        var generator = new OpenAiCompatibleCampaignDraftGenerator(
            client,
            Options.Create(new StorykeeperAiOptions
            {
                BaseUrl = "https://example.test/v1",
                Model = "family-safe-model",
                ApiKey = "server-only-test-key"
            }),
            NullLogger<OpenAiCompatibleCampaignDraftGenerator>.Instance);

        var content = await generator.GenerateAsync(new CampaignBrief { Title = "My world" });

        Assert.Equal(expected.Title, content.Title);
        Assert.Equal(3, content.Npcs!.Count);
        Assert.Equal("https://example.test/v1/chat/completions", handler.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer server-only-test-key", handler.Authorization);
    }

    [Fact]
    public async Task OpenAiCompatibleGeneratorRejectsUnknownDraftProperties()
    {
        var content = JsonSerializer.Serialize(ValidContent(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var contentWithUnknownProperty = $"{content[..^1]},\"unexpected\":true}}";
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = contentWithUnknownProperty } }
            }
        });
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson)
        });
        using var client = new HttpClient(handler);
        var generator = new OpenAiCompatibleCampaignDraftGenerator(
            client,
            Options.Create(new StorykeeperAiOptions
            {
                BaseUrl = "https://example.test/v1",
                Model = "family-safe-model",
                ApiKey = "server-only-test-key"
            }),
            NullLogger<OpenAiCompatibleCampaignDraftGenerator>.Instance);

        var exception = await Assert.ThrowsAsync<CampaignDraftGenerationException>(
            () => generator.GenerateAsync(new CampaignBrief { Title = "My world" }));

        Assert.Equal(502, exception.StatusCode);
    }

    private static CampaignDraftContent ValidContent() => new(
        "The Starlight Garden",
        "A bright garden above the clouds is preparing for its annual lantern festival.",
        "The garden's stars have gone missing.",
        ["Kindness opens new paths.", "Everyone can ask for help.", "A setback always reveals another clue."],
        [
            new CampaignDraftNpc("Moss", "A small gardener with a leaf-shaped hat.", "Patient and encouraging.", "Lantern Grove"),
            new CampaignDraftNpc("Pip", "A curious cloud-shepherd who collects buttons.", "Cheerful and easily distracted.", "Cloud Market"),
            new CampaignDraftNpc("Juniper", "A clever moth who keeps the garden map.", "Thoughtful and a little shy.", "Lantern Grove")
        ],
        [
            new CampaignDraftLocation("Lantern Grove", "A peaceful circle of glowing trees."),
            new CampaignDraftLocation("Cloud Market", "A friendly market floating between soft clouds."),
            new CampaignDraftLocation("The Dewdrop Bridge", "A sparkling bridge that appears at sunrise.")
        ],
        [
            new CampaignDraftHook("Find the first lantern", "Follow a trail of tiny lights to discover who needs help."),
            new CampaignDraftHook("A map of the breeze", "Help Juniper piece together a map from songs and clues.")
        ],
        new CampaignDraftSafety(true, true, true, true, true));

    private sealed class TestGenerator(CampaignDraftContent content) : ICampaignDraftGenerator
    {
        public CampaignDraftContent Content { get; set; } = content;
        public int Calls { get; private set; }

        public Task<CampaignDraftContent> GenerateAsync(CampaignBrief brief, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Content);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(respond(request));
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

        public static async Task<TestDatabase> CreateAsync()
        {
            return await CreateAtMigrationAsync(null);
        }

        public static async Task<TestDatabase> CreateAtMigrationAsync(string? targetMigration)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<StorykeeperDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new StorykeeperDbContext(options);
            await context.Database.MigrateAsync(targetMigration);
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
