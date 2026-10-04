using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Xunit;

namespace Storykeeper.Api.Tests;

public sealed class GameRulesTests
{
    [Theory]
    [InlineData(13, CheckOutcome.StrongSuccess, false)]
    [InlineData(8, CheckOutcome.Success, false)]
    [InlineData(7, CheckOutcome.SuccessWithComplication, true)]
    [InlineData(6, CheckOutcome.SuccessWithComplication, true)]
    [InlineData(5, CheckOutcome.SetbackWithProgress, true)]
    [InlineData(1, CheckOutcome.SetbackWithProgress, true)]
    [InlineData(20, CheckOutcome.StrongSuccess, false)]
    public async Task CheckUsesConfiguredOutcomeBands(
        int roll,
        CheckOutcome expectedOutcome,
        bool forwardProgressRequired)
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, hero) = await CreateCampaignWithHeroAsync(database.Context);
        var rules = new GameRulesService(database.Context);
        var session = (await rules.StartSessionAsync(campaign.Id)).Session!;

        var resolution = await rules.ResolveCheckAsync(
            campaign.Id,
            session.Id,
            hero.Id,
            roll,
            CheckDifficulty.Easy,
            null,
            spendSparkleToken: false,
            risky: false);

        Assert.NotNull(resolution);
        Assert.Equal(expectedOutcome, resolution.Outcome);
        Assert.Equal(forwardProgressRequired, resolution.ForwardProgressRequired);
        Assert.Equal(roll, resolution.Total);
    }

    [Theory]
    [InlineData(CheckDifficulty.Easy, 8)]
    [InlineData(CheckDifficulty.Tricky, 12)]
    [InlineData(CheckDifficulty.Heroic, 16)]
    public async Task DifficultyTargetsAreStable(CheckDifficulty difficulty, int target)
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, hero) = await CreateCampaignWithHeroAsync(database.Context);
        var session = (await new GameRulesService(database.Context).StartSessionAsync(campaign.Id)).Session!;

        var resolution = await new GameRulesService(database.Context).ResolveCheckAsync(
            campaign.Id,
            session.Id,
            hero.Id,
            target,
            difficulty,
            null,
            spendSparkleToken: false,
            risky: false);

        Assert.NotNull(resolution);
        Assert.Equal(target, resolution.Target);
        Assert.Equal(CheckOutcome.Success, resolution.Outcome);
    }

    [Fact]
    public async Task ListedStrengthAddsTwoAndUnlistedStrengthIsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, hero) = await CreateCampaignWithHeroAsync(database.Context);
        var session = (await new GameRulesService(database.Context).StartSessionAsync(campaign.Id)).Session!;
        var rules = new GameRulesService(database.Context);

        var resolution = await rules.ResolveCheckAsync(
            campaign.Id,
            session.Id,
            hero.Id,
            6,
            CheckDifficulty.Easy,
            "climbing",
            spendSparkleToken: false,
            risky: false);

        Assert.NotNull(resolution);
        Assert.Equal(2, resolution.StrengthBonus);
        Assert.Equal(8, resolution.Total);
        await Assert.ThrowsAsync<RuleValidationException>(() => rules.ResolveCheckAsync(
            campaign.Id,
            session.Id,
            hero.Id,
            6,
            CheckDifficulty.Easy,
            "magic",
            spendSparkleToken: false,
            risky: false));
    }

    [Fact]
    public async Task SparkleSpendAddsThreeAndIsSavedWithTheCheck()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, hero) = await CreateCampaignWithHeroAsync(database.Context);
        var session = (await new GameRulesService(database.Context).StartSessionAsync(campaign.Id)).Session!;
        var rules = new GameRulesService(database.Context);

        var resolution = await rules.ResolveCheckAsync(
            campaign.Id,
            session.Id,
            hero.Id,
            5,
            CheckDifficulty.Easy,
            null,
            spendSparkleToken: true,
            risky: false);

        Assert.NotNull(resolution);
        Assert.Equal(3, resolution.SparkleBonus);
        Assert.Equal(8, resolution.Total);
        Assert.True(resolution.SparkleTokenSpent);
        Assert.Equal(1, resolution.SparkleTokensBefore);
        Assert.Equal(0, resolution.SparkleTokensAfter);
        Assert.Equal(0, hero.SparkleTokens);
        Assert.Equal(resolution.Id, await database.Context.CheckResolutions
            .Where(item => item.CampaignId == campaign.Id)
            .Select(item => item.Id)
            .SingleAsync());
    }

    [Fact]
    public async Task ServerTestRollsAreGeneratedAndMarkedAsNonPhysical()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, hero) = await CreateCampaignWithHeroAsync(database.Context);
        var session = (await new GameRulesService(database.Context).StartSessionAsync(campaign.Id)).Session!;

        var resolution = await new GameRulesService(database.Context).ResolveTestCheckAsync(
            campaign.Id,
            session.Id,
            hero.Id,
            CheckDifficulty.Easy,
            null,
            spendSparkleToken: false,
            risky: false);

        Assert.NotNull(resolution);
        Assert.InRange(resolution.Roll, 1, 20);
        Assert.Equal(CheckRollSource.ServerTest, resolution.RollSource);
        Assert.Equal(resolution.Roll, resolution.Total);
    }

    [Fact]
    public async Task SparkleCannotBeSpentAfterTheRollMeetsTheTargetOrWithoutAToken()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, hero) = await CreateCampaignWithHeroAsync(database.Context);
        var rules = new GameRulesService(database.Context);
        var session = (await rules.StartSessionAsync(campaign.Id)).Session!;

        await Assert.ThrowsAsync<RuleValidationException>(() => rules.ResolveCheckAsync(
            campaign.Id,
            session.Id,
            hero.Id,
            8,
            CheckDifficulty.Easy,
            null,
            spendSparkleToken: true,
            risky: false));

        await rules.ResolveCheckAsync(
            campaign.Id,
            session.Id,
            hero.Id,
            5,
            CheckDifficulty.Easy,
            null,
            spendSparkleToken: true,
            risky: false);

        await Assert.ThrowsAsync<RuleConflictException>(() => rules.ResolveCheckAsync(
            campaign.Id,
            session.Id,
            hero.Id,
            5,
            CheckDifficulty.Easy,
            null,
            spendSparkleToken: true,
            risky: false));
        Assert.Equal(0, hero.SparkleTokens);
    }

    [Fact]
    public async Task HeartLossRequiresRiskAndCannotReduceHeartsBelowZero()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, hero) = await CreateCampaignWithHeroAsync(database.Context);
        var rules = new GameRulesService(database.Context);
        var session = (await rules.StartSessionAsync(campaign.Id)).Session!;

        var safeSetback = await rules.ResolveCheckAsync(
            campaign.Id, session.Id, hero.Id, 1, CheckDifficulty.Easy, null, false, risky: false);
        var riskySetback = await rules.ResolveCheckAsync(
            campaign.Id, session.Id, hero.Id, 1, CheckDifficulty.Easy, null, false, risky: true);
        hero.Hearts = 0;
        await database.Context.SaveChangesAsync();
        var zeroHeartSetback = await rules.ResolveCheckAsync(
            campaign.Id, session.Id, hero.Id, 1, CheckDifficulty.Easy, null, false, risky: true);

        Assert.NotNull(safeSetback);
        Assert.Equal(3, safeSetback.HeartsAfter);
        Assert.NotNull(riskySetback);
        Assert.Equal(2, riskySetback.HeartsAfter);
        Assert.NotNull(zeroHeartSetback);
        Assert.Equal(0, zeroHeartSetback.HeartsAfter);
        Assert.Equal(CheckOutcome.SetbackWithProgress, zeroHeartSetback.Outcome);
        Assert.True(zeroHeartSetback.ForwardProgressRequired);
    }

    [Fact]
    public async Task StartingANewSessionResetsResourcesAndClosesThePreviousSession()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, hero) = await CreateCampaignWithHeroAsync(database.Context);
        var rules = new GameRulesService(database.Context);
        var firstSession = (await rules.StartSessionAsync(campaign.Id)).Session!;
        hero.Hearts = 1;
        hero.SparkleTokens = 0;
        await database.Context.SaveChangesAsync();

        var secondSession = (await rules.StartSessionAsync(campaign.Id)).Session!;

        Assert.Equal(firstSession.SessionNumber + 1, secondSession.SessionNumber);
        Assert.NotNull(firstSession.EndedAtUtc);
        Assert.Equal(3, hero.Hearts);
        Assert.Equal(1, hero.SparkleTokens);
        await Assert.ThrowsAsync<RuleConflictException>(() => rules.ResolveCheckAsync(
            campaign.Id,
            firstSession.Id,
            hero.Id,
            10,
            CheckDifficulty.Easy,
            null,
            spendSparkleToken: false,
            risky: false));
    }

    [Fact]
    public async Task CheckCannotUseAHeroOrSessionFromAnotherCampaign()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (firstCampaign, firstHero) = await CreateCampaignWithHeroAsync(database.Context, "First");
        var (secondCampaign, secondHero) = await CreateCampaignWithHeroAsync(database.Context, "Second");
        var rules = new GameRulesService(database.Context);
        var firstSession = (await rules.StartSessionAsync(firstCampaign.Id)).Session!;
        var secondSession = (await rules.StartSessionAsync(secondCampaign.Id)).Session!;

        var wrongHero = await rules.ResolveCheckAsync(
            secondCampaign.Id,
            secondSession.Id,
            firstHero.Id,
            10,
            CheckDifficulty.Easy,
            null,
            spendSparkleToken: false,
            risky: false);
        var wrongSession = await rules.ResolveCheckAsync(
            firstCampaign.Id,
            secondSession.Id,
            firstHero.Id,
            10,
            CheckDifficulty.Easy,
            null,
            spendSparkleToken: false,
            risky: false);

        Assert.Null(wrongHero);
        Assert.Null(wrongSession);
        Assert.Empty(await database.Context.CheckResolutions.ToListAsync());
        Assert.Equal(1, secondHero.SparkleTokens);
    }

    private static async Task<(Campaign Campaign, Hero Hero)> CreateCampaignWithHeroAsync(
        StorykeeperDbContext context,
        string name = "Rules World")
    {
        var campaigns = new CampaignService(new CampaignRepository(context));
        var campaign = await campaigns.CreateAsync(name, null);
        var hero = new Hero
        {
            CampaignId = campaign.Id,
            PartyId = campaign.Party!.Id,
            Name = "Pip",
            Description = "A cheerful explorer",
            Role = "Ranger",
            Strengths = ["climbing", "kindness"]
        };
        context.Heroes.Add(hero);
        await context.SaveChangesAsync();
        return (campaign, hero);
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
