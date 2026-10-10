using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Xunit;

namespace Storykeeper.Api.Tests;

public sealed class CampaignNarratorGuideTests
{
    private const string Sentinel =
        "FINAL GUIDE SENTINEL: REUNITE WITH THE CREW AND SET COURSE FOR EARTH";

    [Fact]
    public async Task LongMultilineGuidePersistsThroughDraftRegenerationApprovalAndActivation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var guideText = string.Join("\r\n", Enumerable.Repeat(
            "PATCH and PIP awaken aboard Skylark after 214 days. MABEL is helpful, not an evil computer.",
            40)) + "\r\nFlicker is lonely and unintentionally disrupts electronics when afraid." +
            "\r\nThe main engines are locked because an orange power conduit is damaged." +
            "\r\nSafe repair, backup propulsion and rescue are possible routes." +
            "\r\n" + Sentinel;
        Assert.True(guideText.Length > 2_000);
        var briefs = new CampaignBriefService(database.Context);
        var brief = await briefs.CreateAsync(new CampaignBrief
        {
            Title = "The Last Light of the Skylark",
            Genre = "Space adventure",
            Tone = "Warm, funny, and adventurous",
            StoryIdea = "A small ship needs help.",
            NarratorGuide = guideText
        });

        var loadedBrief = await briefs.GetAsync(brief.Id);
        Assert.Equal(guideText, loadedBrief!.NarratorGuide);
        Assert.EndsWith(Sentinel, loadedBrief.NarratorGuide);

        var generator = new TestGenerator(ValidContent());
        var drafts = new CampaignDraftService(database.Context, generator);
        var draft = (await drafts.GenerateAsync(brief.Id))!;
        Assert.Equal(guideText, draft.NarratorGuide);
        Assert.Equal(guideText, generator.Sources.Single());

        await drafts.ApproveAsync(draft.Id);
        var editedGuide = guideText + "\r\nA parent correction.";
        var updated = await drafts.UpdateNarratorGuideAsync(draft.Id, editedGuide);
        Assert.Equal(CampaignDraftStatus.PendingReview, updated.Draft!.Status);
        Assert.Null(updated.Draft.ApprovedAtUtc);

        await drafts.RegenerateAsync(draft.Id);
        var regenerated = await drafts.GetAsync(draft.Id);
        Assert.Equal(editedGuide, regenerated!.NarratorGuide);
        Assert.Equal(editedGuide, generator.Sources.Last());

        await drafts.UpdateNarratorGuideAsync(draft.Id, guideText);
        await drafts.ApproveAsync(draft.Id);
        var activation = await drafts.ActivateAsync(draft.Id);
        Assert.Equal(CampaignDraftActivationStatus.Activated, activation.Status);
        var activeGuide = await database.Context.CampaignNarratorGuides
            .SingleAsync(item => item.CampaignId == activation.Campaign!.Id);
        Assert.Equal(guideText, activeGuide.ActiveText);
        Assert.Equal(1, activeGuide.ActiveRevision);
        Assert.NotNull(activeGuide.ActiveApprovedAtUtc);
        Assert.EndsWith(Sentinel, activeGuide.ActiveText);
    }

    [Fact]
    public async Task GuideLimitAcceptsThirtyThousandAndRejectsOversizeWithoutReplacingSavedText()
    {
        await using var database = await TestDatabase.CreateAsync();
        var guideText = new string('x', NarratorGuideLimits.MaximumCharacters);
        var request = BriefRequest(guideText);
        Assert.Empty(CampaignBriefRequestValidator.Validate(request));
        Assert.Contains("narratorGuide",
            CampaignBriefRequestValidator.Validate(BriefRequest(guideText + "x")).Keys);

        var service = new CampaignBriefService(database.Context);
        var brief = await service.CreateAsync(request.ToDomain());
        Assert.Equal(guideText, (await service.GetAsync(brief.Id))!.NarratorGuide);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.UpdateNarratorGuideAsync(brief.Id, guideText + "x"));
        Assert.Equal(guideText, (await service.GetAsync(brief.Id))!.NarratorGuide);
    }

    [Fact]
    public async Task PendingActiveGuideCannotAffectPlayAndSessionRestrictionsAreServerEnforced()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaign = await new CampaignService(new CampaignRepository(database.Context))
            .CreateAsync("One guide", "A separate world.");
        const string activeText = "Approved authored truth.";
        var active = new CampaignNarratorGuide
        {
            CampaignId = campaign.Id,
            ActiveText = activeText,
            PendingText = "Unapproved change.",
            HasPendingRevision = true,
            ActiveRevision = 1,
            PendingRevision = 2,
            ActiveApprovedAtUtc = DateTimeOffset.UtcNow
        };
        database.Context.CampaignNarratorGuides.Add(active);
        await database.Context.SaveChangesAsync();

        var service = new CampaignNarratorGuideService(database.Context);
        var readBack = await service.GetAsync(campaign.Id);
        Assert.Equal(activeText, readBack!.ActiveText);
        Assert.Equal("Unapproved change.", readBack.PendingText);
        Assert.True(readBack.HasPendingRevision);

        var session = new Session { CampaignId = campaign.Id, SessionNumber = 1 };
        database.Context.Sessions.Add(session);
        await database.Context.SaveChangesAsync();

        Assert.Equal(NarratorGuideOperationStatus.SessionRunning,
            (await service.SavePendingAsync(campaign.Id, "A different revision.")).Status);
        Assert.Equal(NarratorGuideOperationStatus.SessionRunning,
            (await service.ApproveAsync(campaign.Id)).Status);
        Assert.Equal(NarratorGuideOperationStatus.SessionRunning,
            (await service.RemoveAsync(campaign.Id)).Status);
        Assert.Equal(activeText, (await service.GetAsync(campaign.Id))!.ActiveText);

        session.EndedAtUtc = DateTimeOffset.UtcNow;
        await database.Context.SaveChangesAsync();
        var saved = await service.SavePendingAsync(campaign.Id, "A reviewed new version.");
        Assert.Equal(NarratorGuideOperationStatus.Succeeded, saved.Status);
        Assert.Equal(activeText, saved.Guide!.ActiveText);
        var approved = await service.ApproveAsync(campaign.Id);
        Assert.Equal("A reviewed new version.", approved.Guide!.ActiveText);
        Assert.Equal(3, approved.Guide.ActiveRevision);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SavePendingAsync(
            campaign.Id,
            new string('z', NarratorGuideLimits.MaximumCharacters + 1)));
        Assert.Equal("A reviewed new version.", (await service.GetAsync(campaign.Id))!.ActiveText);

        var otherCampaign = await new CampaignService(new CampaignRepository(database.Context))
            .CreateAsync("Another guide boundary", null);
        Assert.Null(await service.GetAsync(otherCampaign.Id));
    }

    private static CampaignBriefRequest BriefRequest(string guide) => new(
        "The Skylark",
        "Space adventure",
        "Warm",
        6,
        45,
        [],
        [],
        "A short idea.",
        NarratorGuide: guide);

    private static CampaignDraftContent ValidContent() => new(
        "The Last Light of the Skylark",
        "PATCH and PIP wake aboard a friendly little starship.",
        "The crew is waiting at Beacon Station.",
        ["Ask for help.", "A setback reveals another route.", "Every hero can contribute."],
        [
            new CampaignDraftNpc("MABEL", "A helpful ship computer.", "Patient and kind.", "Skylark Bridge"),
            new CampaignDraftNpc("PATCH", "A careful repair robot.", "Thoughtful.", null),
            new CampaignDraftNpc("PIP", "A curious exploration robot.", "Cheerful.", null)
        ],
        [
            new CampaignDraftLocation("Skylark Bridge", "A bright control room."),
            new CampaignDraftLocation("Beacon Station", "A welcoming rescue station."),
            new CampaignDraftLocation("Engine Bay", "A safe place to repair the power conduit.")
        ],
        [
            new CampaignDraftHook("Find the crew", "Follow the signal toward Beacon Station."),
            new CampaignDraftHook("Restore propulsion", "Find a safe way to repair or replace the conduit.")
        ],
        new CampaignDraftSafety(true, true, true, true, true));

    private sealed class TestGenerator(CampaignDraftContent content) : ICampaignDraftGenerator
    {
        public List<string?> Sources { get; } = [];

        public Task<CampaignDraftContent> GenerateAsync(
            CampaignBrief brief,
            CancellationToken cancellationToken = default)
        {
            Sources.Add(brief.NarratorGuide);
            return Task.FromResult(content);
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
