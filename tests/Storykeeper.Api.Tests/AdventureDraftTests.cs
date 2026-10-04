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

public sealed class AdventureDraftTests
{
    [Fact]
    public async Task GenerationUsesOnlyCampaignContinuityAndActivationAddsPlayableQuest()
    {
        var fixtureErrors = AdventureDraftValidator.Validate(ValidContent(), ["Mira"]);
        Assert.True(fixtureErrors.Count == 0, string.Join("; ", fixtureErrors.SelectMany(item =>
            item.Value.Select(message => $"{item.Key}: {message}"))));

        await using var database = await TestDatabase.CreateAsync();
        var campaign = await CreateCampaignAsync(database.Context, "Lantern Garden");
        var otherCampaign = await CreateCampaignAsync(database.Context, "Other World");
        database.Context.CampaignFacts.AddRange(
            new CampaignFact
            {
                CampaignId = campaign.Id,
                Category = "promise",
                Statement = "Mira promised to return the silver map.",
                Importance = 5,
                Status = CampaignFactStatus.Active
            },
            new CampaignFact
            {
                CampaignId = campaign.Id,
                Category = "clue",
                Statement = "An unapproved clue.",
                Importance = 5,
                Status = CampaignFactStatus.Proposed
            },
            new CampaignFact
            {
                CampaignId = otherCampaign.Id,
                Category = "world",
                Statement = "A fact from another campaign.",
                Importance = 5,
                Status = CampaignFactStatus.Active
            });
        database.Context.Sessions.Add(new Session
        {
            CampaignId = campaign.Id,
            SessionNumber = 1,
            Summary = "Mira found a folded map beneath the lantern tree.",
            StartedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            EndedAtUtc = DateTimeOffset.UtcNow
        });
        database.Context.Sessions.Add(new Session
        {
            CampaignId = otherCampaign.Id,
            SessionNumber = 1,
            StartedAtUtc = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var generator = new TestGenerator(ValidContent());
        var service = new AdventureDraftService(database.Context, generator);
        var created = await service.GenerateAsync(
            campaign.Id, new AdventureDraftRequest(45, "Add a gentle puzzle."));

        Assert.Equal(AdventureDraftOperationStatus.Succeeded, created.Status);
        Assert.Equal(45, created.Draft!.SessionLengthMinutes);
        Assert.Equal("Add a gentle puzzle.", created.Draft.ParentPreferences);
        var input = generator.Input!;
        Assert.Equal("Lantern Garden", input.CampaignName);
        Assert.Contains(input.ActiveFacts, fact => fact.Contains("Mira promised", StringComparison.Ordinal));
        Assert.DoesNotContain(input.ActiveFacts, fact => fact.Contains("unapproved", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(input.ActiveFacts, fact => fact.Contains("another campaign", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(input.RecentSessionSummaries, summary => summary.Contains("folded map", StringComparison.Ordinal));
        Assert.Contains("Mira", input.NpcNames);
        Assert.Equal(AdventureDraftOperationStatus.Conflict,
            (await service.GenerateAsync(otherCampaign.Id, new AdventureDraftRequest(30, null))).Status);
        await Assert.ThrowsAsync<RuleConflictException>(() => service.ActivateAsync(created.Draft.Id));

        var approved = await service.ApproveAsync(created.Draft.Id);
        Assert.Equal(AdventureDraftStatus.Approved, approved.Draft!.Status);
        var activated = await service.ActivateAsync(created.Draft.Id);

        Assert.Equal(AdventureDraftOperationStatus.Succeeded, activated.Status);
        Assert.Equal(AdventureDraftStatus.Activated, activated.Draft!.Status);
        Assert.NotNull(activated.Draft.ActivatedQuestId);
        var quest = await database.Context.Quests.SingleAsync(item => item.Id == activated.Draft.ActivatedQuestId);
        Assert.Equal(QuestStatus.InProgress, quest.Status);
        Assert.Equal(45, quest.SessionLengthMinutes);
        var plan = JsonSerializer.Deserialize<AdventureDraftContent>(quest.AdventurePlanJson!, AdventureDraftJson.Options);
        Assert.Equal("The Lantern's Lost Tune", plan!.Title);
        Assert.Equal(2, plan.Scenes!.Count);
        Assert.True(await database.Context.Npcs.AnyAsync(npc =>
            npc.CampaignId == campaign.Id && npc.Name == "Tumble"));

        generator.Content = ValidContent("Breezy") with { Title = "A Song for the Clouds" };
        var nextDraft = await service.GenerateAsync(campaign.Id, new AdventureDraftRequest(30, null));
        await service.ApproveAsync(nextDraft.Draft!.Id);
        var nextActivated = await service.ActivateAsync(nextDraft.Draft.Id);
        var campaignQuests = await database.Context.Quests
            .AsNoTracking()
            .Where(item => item.CampaignId == campaign.Id)
            .OrderBy(item => item.Title)
            .ToArrayAsync();
        Assert.Equal(QuestStatus.Completed, campaignQuests.Single(item => item.Title == "The Lantern's Lost Tune").Status);
        Assert.Equal(QuestStatus.InProgress, campaignQuests.Single(item => item.Title == "A Song for the Clouds").Status);
        Assert.NotEqual(activated.Draft.ActivatedQuestId, nextActivated.Draft!.ActivatedQuestId);
        Assert.Empty(await database.Context.Quests.Where(item => item.CampaignId == otherCampaign.Id).ToArrayAsync());
    }

    [Fact]
    public async Task InvalidRegenerationKeepsPreviousDraftAndEditingRequiresParentReviewAgain()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaign = await CreateCampaignAsync(database.Context, "Lantern Garden");
        var generator = new TestGenerator(ValidContent());
        var service = new AdventureDraftService(database.Context, generator);
        var draft = (await service.GenerateAsync(campaign.Id, new AdventureDraftRequest(60, null))).Draft!;
        var originalContent = draft.ContentJson;

        generator.Content = ValidContent() with { Finale = "A bloody contest decides the winner." };
        await Assert.ThrowsAsync<AdventureDraftRejectedException>(() => service.RegenerateAsync(draft.Id));
        var stored = await service.ListAsync(campaign.Id);
        var savedDraft = Assert.Single(stored!);
        Assert.Equal(originalContent, savedDraft.ContentJson);
        Assert.Equal(1, savedDraft.GenerationNumber);

        generator.Content = ValidContent();
        await service.ApproveAsync(draft.Id);
        var edited = await service.UpdateAsync(draft.Id, ValidContent() with { Title = "The Quiet Lantern" });
        Assert.Equal(AdventureDraftStatus.PendingReview, edited.Draft!.Status);
        await Assert.ThrowsAsync<RuleConflictException>(() => service.ActivateAsync(draft.Id));
        await service.ApproveAsync(draft.Id);
        var activated = await service.ActivateAsync(draft.Id);
        Assert.Equal(AdventureDraftOperationStatus.Succeeded, activated.Status);
        Assert.Equal(AdventureDraftOperationStatus.AlreadyActivated, (await service.DeleteAsync(draft.Id)).Status);
    }

    [Fact]
    public async Task ActivationIsBlockedIfANewSessionStartsAfterGeneration()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaign = await CreateCampaignAsync(database.Context, "Lantern Garden");
        var service = new AdventureDraftService(database.Context, new TestGenerator(ValidContent()));
        var draft = (await service.GenerateAsync(campaign.Id, new AdventureDraftRequest(45, null))).Draft!;
        await service.ApproveAsync(draft.Id);
        database.Context.Sessions.Add(new Session
        {
            CampaignId = campaign.Id,
            SessionNumber = 1,
            StartedAtUtc = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var result = await service.ActivateAsync(draft.Id);

        Assert.Equal(AdventureDraftOperationStatus.Conflict, result.Status);
        Assert.Equal(AdventureDraftStatus.Approved,
            (await database.Context.AdventureDrafts.AsNoTracking().SingleAsync(item => item.Id == draft.Id)).Status);
        Assert.Empty(await database.Context.Quests.Where(quest => quest.CampaignId == campaign.Id).ToArrayAsync());
    }

    [Fact]
    public void ValidatorsRequirePlayableSafeStructureAndBoundedPreferences()
    {
        Assert.Contains("sessionLengthMinutes", AdventureDraftRequestValidator.Validate(
            new AdventureDraftRequest(90, null)).Keys);
        Assert.Contains("parentPreferences", AdventureDraftRequestValidator.Validate(
            new AdventureDraftRequest(30, new string('x', 501))).Keys);
        Assert.Contains("scenes", AdventureDraftValidator.Validate(ValidContent() with { Scenes = [] }).Keys);
        Assert.Contains("safety", AdventureDraftValidator.Validate(ValidContent() with
        {
            Finale = "A bloody contest decides the winner."
        }).Keys);
        Assert.Contains("featuredNpc.name", AdventureDraftValidator.Validate(
            ValidContent(), ["Tumble"]).Keys);
        Assert.Contains("solutionPaths", AdventureDraftValidator.Validate(ValidContent() with
        {
            SolutionPaths = ["Use the map.", "use the map."]
        }).Keys);
    }

    [Fact]
    public async Task ProviderUsesServerKeyAndDeserializesStrictStructuredPlan()
    {
        var expected = ValidContent();
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(expected, AdventureDraftJson.Options)
                    }
                }
            }
        });
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson)
        });
        using var client = new HttpClient(handler);
        var generator = new OpenAiCompatibleAdventureDraftGenerator(
            client,
            Options.Create(new StorykeeperAiOptions
            {
                BaseUrl = "https://example.test/v1",
                Model = "family-safe-model",
                ApiKey = "server-only-test-key"
            }),
            NullLogger<OpenAiCompatibleAdventureDraftGenerator>.Instance);

        var generated = await generator.GenerateAsync(new AdventureGenerationInput(
            "Lantern Garden", "Cozy fantasy", "Warm and funny", "A garden under the clouds.",
            "Mira lost the silver map.", 45, null, [], [], [], ["Mira"], []));

        Assert.Equal(expected.Title, generated.Title);
        Assert.Equal(AdventureArcType.Standalone, generated.ArcType);
        Assert.Equal("https://example.test/v1/chat/completions", handler.RequestUri!.AbsoluteUri);
        Assert.StartsWith("Bearer ", handler.Authorization);
    }

    [Fact]
    public async Task ProviderRejectsUnknownAdventureProperties()
    {
        var content = JsonSerializer.Serialize(ValidContent(), AdventureDraftJson.Options);
        var invalidContent = $"{content[..^1]},\"unexpected\":true}}";
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content = invalidContent } } }
            }))
        });
        using var client = new HttpClient(handler);
        var generator = new OpenAiCompatibleAdventureDraftGenerator(
            client,
            Options.Create(new StorykeeperAiOptions
            {
                BaseUrl = "https://example.test/v1",
                Model = "family-safe-model",
                ApiKey = "server-only-test-key"
            }),
            NullLogger<OpenAiCompatibleAdventureDraftGenerator>.Instance);

        var exception = await Assert.ThrowsAsync<AdventureDraftGenerationException>(() =>
            generator.GenerateAsync(new AdventureGenerationInput(
                "Lantern Garden", "Cozy fantasy", "Warm and funny", "A garden under the clouds.",
                null, 45, null, [], [], [], [], [])));

        Assert.Equal(502, exception.StatusCode);
    }

    private static async Task<Campaign> CreateCampaignAsync(StorykeeperDbContext context, string name)
    {
        var campaign = await new CampaignService(new CampaignRepository(context))
            .CreateAsync(name, "A gentle mystery.");
        context.Npcs.Add(new Npc
        {
            CampaignId = campaign.Id,
            Name = "Mira",
            Description = "A friendly mapmaker.",
            Disposition = "Curious and helpful."
        });
        await context.SaveChangesAsync();
        return campaign;
    }

    private static AdventureDraftContent ValidContent(string featuredNpcName = "Tumble") => new(
        "The Lantern's Lost Tune",
        "Find the missing song that helps the lantern garden glow.",
        AdventureArcType.Standalone,
        null,
        "Mira hears a tiny melody inside the lantern and asks the heroes to help.",
        [
            new AdventureScene("Follow the notes", "The melody skips along a path of bright leaves.", "Mira"),
            new AdventureScene("Ask the cloud birds", "A friendly bird offers a riddle about the tune.", featuredNpcName)
        ],
        ["Piece together the leaf-pattern clue.", "Invite the birds to sing each note they remember."],
        [
            new AdventureClue("A silver leaf", "Its pattern matches the rhythm of the missing tune."),
            new AdventureClue("A bird's rhyme", "The tune ends near the old bellflower.")
        ],
        new AdventureDraftNpc(featuredNpcName, "A small cloud bird with a bell-shaped feather.", "Cheerful and thoughtful."),
        "The heroes help Mira and the birds reunite the lantern with its happy song.",
        "The garden celebrates with a paper-star badge and a shared picnic.");

    private sealed class TestGenerator(AdventureDraftContent content) : IAdventureDraftGenerator
    {
        public AdventureDraftContent Content { get; set; } = content;
        public AdventureGenerationInput? Input { get; private set; }

        public Task<AdventureDraftContent> GenerateAsync(
            AdventureGenerationInput input,
            CancellationToken cancellationToken = default)
        {
            Input = input;
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
