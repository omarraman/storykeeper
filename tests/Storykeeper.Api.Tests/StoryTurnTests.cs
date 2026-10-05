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

public sealed class StoryTurnTests
{
    private const string Narration =
        "The little lantern gives a cheerful wobble as you explain your idea. Mira listens carefully, then points toward a path lined with tiny blue flowers. A breeze carries a soft tune from beyond the hill, and the folded map flutters open just enough to reveal a silver star. The gardener nearby waves and offers to help you follow the melody. Nothing seems urgent, and there are several friendly ways to learn more.";

    [Fact]
    public async Task StoryTurnLoadsOnlyCampaignContextAndPersistsValidatedFactsAsProposals()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaign = await CreatePlayableCampaignAsync(database.Context, "Lantern Garden");
        var otherCampaign = await CreatePlayableCampaignAsync(database.Context, "Other World");
        var quest = await database.Context.Quests.SingleAsync(item => item.CampaignId == campaign.Campaign.Id);
        quest.Title = "The Lantern's Lost Tune";
        quest.Description = "Help Mira restore a missing garden song.";
        quest.SessionLengthMinutes = 45;
        quest.AdventurePlanJson = JsonSerializer.Serialize(new AdventureDraftContent(
                "The Lantern's Lost Tune",
                "Help Mira restore a missing garden song.",
                AdventureArcType.Standalone,
                null,
                "Mira hears a melody inside a lantern.",
                [new AdventureScene("Follow the notes", "Look for bright leaves.", "Mira"),
                 new AdventureScene("Ask the birds", "Listen to a friendly riddle.", "Mira")],
                ["Follow the leaves.", "Ask the birds."],
                [new AdventureClue("Silver leaf", "It matches the song."),
                 new AdventureClue("Bird rhyme", "It points to the bellflower.")],
                new AdventureDraftNpc("Mira", "A kind mapmaker.", "Helpful."),
                "The garden sings together.",
                "Everyone shares a picnic."),
                AdventureDraftJson.Options);
        await database.Context.SaveChangesAsync();
        var generator = new TestGenerator(StoryContent(
            facts: [new StoryTurnFactProposal("clue", "The map shows a silver star.", 4)]));
        var service = new StoryTurnService(database.Context, generator);

        var result = await service.SubmitActionAsync(campaign.Campaign.Id, new StoryTurnRequest(
            "Look closely at the folded map.", campaign.Session.Id, campaign.Hero.Id));

        Assert.Equal("story_beat", result!.Type);
        Assert.Equal("Mira", result.StoryBeat!.NpcDialogue[0].NpcName);
        Assert.Equal("Pip", result.State!.Heroes[0].Name);
        Assert.Contains(result.State.Clues, clue => clue.Text == "The map shows a silver star.");
        Assert.Equal("Look closely at the folded map.", generator.Input!.Action);
        using var context = JsonDocument.Parse(generator.Input.ContextJson);
        var contextText = context.RootElement.GetRawText();
        Assert.Contains("Lantern Garden", contextText);
        Assert.DoesNotContain("Other World", contextText);
        var currentQuest = context.RootElement.GetProperty("currentQuest");
        Assert.Equal(45, currentQuest.GetProperty("sessionLengthMinutes").GetInt32());
        Assert.Equal(
            "The Lantern's Lost Tune",
            currentQuest.GetProperty("adventurePlan").GetProperty("title").GetString());
        Assert.Equal(CampaignFactStatus.Proposed,
            (await database.Context.CampaignFacts.SingleAsync()).Status);
        Assert.Equal(campaign.Session.Id,
            (await database.Context.CampaignFacts.SingleAsync()).SourceSessionId);
        Assert.Empty(await database.Context.CampaignFacts.Where(fact => fact.CampaignId == otherCampaign.Campaign.Id).ToArrayAsync());
    }

    [Fact]
    public async Task RollRequestDoesNotPersistProposedFacts()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaign = await CreatePlayableCampaignAsync(database.Context, "Lantern Garden");
        var generator = new TestGenerator(StoryContent(
            roll: new StoryTurnRollRequest("Can you balance along the wide bridge?", "Tricky", null, false),
            facts: [new StoryTurnFactProposal("clue", "The bridge is safe.", 2)]));
        var service = new StoryTurnService(database.Context, generator);

        var result = await service.SubmitActionAsync(campaign.Campaign.Id, new StoryTurnRequest(
            "Carefully cross the bridge.", campaign.Session.Id, campaign.Hero.Id));

        Assert.Equal("roll_required", result!.Type);
        Assert.Equal("Tricky", result.RollRequired!.Difficulty);
        Assert.Empty(await database.Context.CampaignFacts.ToArrayAsync());
    }

    [Fact]
    public async Task ResolvedCheckIsLoadedFromTheCurrentHeroAndSession()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaign = await CreatePlayableCampaignAsync(database.Context, "Lantern Garden");
        var check = new CheckResolution
        {
            CampaignId = campaign.Campaign.Id,
            SessionId = campaign.Session.Id,
            HeroId = campaign.Hero.Id,
            Roll = 9,
            Difficulty = CheckDifficulty.Tricky,
            Target = 12,
            Total = 11,
            Outcome = CheckOutcome.SuccessWithComplication,
            ForwardProgressRequired = true,
            ConsequenceCategory = ConsequenceCategory.GentleComplication,
            HeartsBefore = 3,
            HeartsAfter = 3,
            SparkleTokensBefore = 1,
            SparkleTokensAfter = 1
        };
        database.Context.CheckResolutions.Add(check);
        await database.Context.SaveChangesAsync();
        var generator = new TestGenerator(StoryContent());
        var service = new StoryTurnService(database.Context, generator);

        await service.SubmitActionAsync(campaign.Campaign.Id, new StoryTurnRequest(
            "Carefully cross the bridge.", campaign.Session.Id, campaign.Hero.Id, check.Id));

        using var context = JsonDocument.Parse(generator.Input!.ContextJson);
        var resolvedCheck = context.RootElement.GetProperty("resolvedCheck");
        Assert.Equal(11, resolvedCheck.GetProperty("total").GetInt32());
        Assert.Equal("SuccessWithComplication", resolvedCheck.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task UnsafeOrOutOfCampaignGeneratedContentIsRejectedBeforeSaving()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaign = await CreatePlayableCampaignAsync(database.Context, "Lantern Garden");
        var unsafeResponse = StoryContent(narration: "A cruel villain threatens to kill everyone.");
        var service = new StoryTurnService(database.Context, new TestGenerator(unsafeResponse));

        var exception = await Assert.ThrowsAsync<StoryTurnGenerationException>(() =>
            service.SubmitActionAsync(campaign.Campaign.Id, new StoryTurnRequest(
                "Ask what happened.", campaign.Session.Id, campaign.Hero.Id)));

        Assert.Equal(502, exception.StatusCode);
        Assert.DoesNotContain("villain", exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await database.Context.CampaignFacts.ToArrayAsync());
    }

    [Fact]
    public void RequestValidatorBoundsActionsAndRequiresCurrentSession()
    {
        var errors = StoryTurnRequestValidator.Validate(new StoryTurnRequest(
            new string('a', 501), null, Guid.Empty));

        Assert.Contains("action", errors.Keys);
        Assert.Contains("sessionId", errors.Keys);
        Assert.Contains("heroId", errors.Keys);
    }

    [Fact]
    public async Task ProviderUsesServerCredentialAndRejectsUnknownTurnFields()
    {
        var responseContent = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = """{"narration":"A safe response.","speaker":null,"npcDialogue":[],"rollRequest":null,"choices":[{"id":"look","text":"Look around"}],"proposedFacts":[],"unexpected":true}""" } }
            }
        });
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent)
        });
        using var client = new HttpClient(handler);
        var generator = new OpenAiCompatibleStoryTurnGenerator(
            client,
            Options.Create(new StorykeeperAiOptions
            {
                BaseUrl = "https://example.test/v1",
                Model = "family-safe-model",
                ApiKey = "server-only-test-key"
            }),
            NullLogger<OpenAiCompatibleStoryTurnGenerator>.Instance);

        var exception = await Assert.ThrowsAsync<StoryTurnGenerationException>(() =>
            generator.GenerateAsync(new StoryTurnGenerationInput("Look around", "{}")));

        Assert.Equal(502, exception.StatusCode);
        Assert.Equal("https://example.test/v1/chat/completions", handler.RequestUri!.AbsoluteUri);
        Assert.NotNull(handler.Authorization);
        Assert.DoesNotContain("server-only-test-key", handler.RequestBody);
        using var requestDocument = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("text",
            requestDocument.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Equal(2400, requestDocument.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task ProviderRejectsReasoningOnlyResponseWhenModelRunsOutOfTokens()
    {
        const string privateReasoning = "Private model reasoning must not become narration.";
        var responseContent = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new { content = "", reasoning_content = privateReasoning },
                    finish_reason = "length"
                }
            },
            usage = new
            {
                completion_tokens = 1800,
                completion_tokens_details = new { reasoning_tokens = 1799 }
            }
        });
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent)
        });
        using var client = new HttpClient(handler);
        var generator = new OpenAiCompatibleStoryTurnGenerator(
            client,
            Options.Create(new StorykeeperAiOptions
            {
                BaseUrl = "https://example.test/v1",
                Model = "family-safe-model",
                ApiKey = "server-only-test-key"
            }),
            NullLogger<OpenAiCompatibleStoryTurnGenerator>.Instance);

        var exception = await Assert.ThrowsAsync<StoryTurnGenerationException>(() =>
            generator.GenerateAsync(new StoryTurnGenerationInput("Look around for clues", "{}")));

        Assert.Equal(502, exception.StatusCode);
        Assert.DoesNotContain(privateReasoning, exception.SafeMessage, StringComparison.Ordinal);
    }

    private static StoryTurnContent StoryContent(
        string? narration = null,
        StoryTurnRollRequest? roll = null,
        IReadOnlyList<StoryTurnFactProposal?>? facts = null) =>
        new(
            narration ?? Narration,
            "Mira the mapmaker",
            [new StoryTurnDialogue("Mira", "I know a path that might help.")],
            roll,
            [new StoryTurnChoice("follow-path", "Follow the flower path.")],
            facts ?? []);

    private static async Task<PlayableCampaign> CreatePlayableCampaignAsync(
        StorykeeperDbContext context,
        string name)
    {
        var campaign = await new CampaignService(new CampaignRepository(context))
            .CreateAsync(name, "A friendly adventure.");
        var hero = new Hero
        {
            CampaignId = campaign.Id,
            PartyId = campaign.Party!.Id,
            Name = "Pip",
            Description = "A curious scout.",
            Role = "Scout",
            Strengths = ["Noticing tiny details"]
        };
        var session = new Session
        {
            CampaignId = campaign.Id,
            SessionNumber = 1
        };
        context.Heroes.Add(hero);
        context.Sessions.Add(session);
        context.Npcs.Add(new Npc
        {
            CampaignId = campaign.Id,
            Name = "Mira",
            Description = "A helpful mapmaker.",
            Disposition = "Friendly"
        });
        context.Quests.Add(new Quest
        {
            CampaignId = campaign.Id,
            Title = "Find the lantern map",
            Description = "Follow the map.",
            Status = QuestStatus.InProgress
        });
        await context.SaveChangesAsync();
        return new PlayableCampaign(campaign, hero, session);
    }

    private sealed record PlayableCampaign(Campaign Campaign, Hero Hero, Session Session);

    private sealed class TestGenerator(StoryTurnContent content) : IStoryTurnGenerator
    {
        public StoryTurnGenerationInput? Input { get; private set; }

        public Task<StoryTurnContent> GenerateAsync(
            StoryTurnGenerationInput input,
            CancellationToken cancellationToken = default)
        {
            Input = input;
            return Task.FromResult(content);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
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
