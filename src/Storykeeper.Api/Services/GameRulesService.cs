using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class GameRulesService(StorykeeperDbContext dbContext) : IGameRulesService
{
    private static readonly IReadOnlyDictionary<CheckDifficulty, int> Targets =
        new Dictionary<CheckDifficulty, int>
        {
            [CheckDifficulty.Easy] = 8,
            [CheckDifficulty.Tricky] = 12,
            [CheckDifficulty.Heroic] = 16
        };

    public async Task<SessionStartResult> StartSessionAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var campaign = await dbContext.Campaigns
            .Include(item => item.Party)
                .ThenInclude(party => party!.Heroes)
            .Include(item => item.Sessions)
            .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            return new SessionStartResult(null, []);
        }

        if (campaign.Status != CampaignStatus.Active)
        {
            throw new RuleConflictException("A new session can only start in an active campaign.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var activeSession in campaign.Sessions.Where(session => session.EndedAtUtc is null))
        {
            activeSession.EndedAtUtc = now;
        }

        var heroes = campaign.Party?.Heroes.OrderBy(hero => hero.Name).ToArray() ?? [];
        foreach (var hero in heroes)
        {
            hero.Hearts = 3;
            hero.SparkleTokens = 1;
        }

        var session = new Session
        {
            CampaignId = campaignId,
            SessionNumber = campaign.Sessions.Select(item => item.SessionNumber).DefaultIfEmpty(0).Max() + 1,
            StartedAtUtc = now
        };
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SessionStartResult(session, heroes);
    }

    public Task<CheckResolution?> ResolveCheckAsync(
        Guid campaignId,
        Guid sessionId,
        Guid heroId,
        int roll,
        CheckDifficulty difficulty,
        string? strength,
        bool spendSparkleToken,
        bool risky,
        CancellationToken cancellationToken = default) =>
        ResolveCheckAsync(
            campaignId,
            sessionId,
            heroId,
            roll,
            difficulty,
            strength,
            spendSparkleToken,
            risky,
            CheckRollSource.Physical,
            cancellationToken);

    public Task<CheckResolution?> ResolveTestCheckAsync(
        Guid campaignId,
        Guid sessionId,
        Guid heroId,
        CheckDifficulty difficulty,
        string? strength,
        bool spendSparkleToken,
        bool risky,
        CancellationToken cancellationToken = default) =>
        ResolveCheckAsync(
            campaignId,
            sessionId,
            heroId,
            RandomNumberGenerator.GetInt32(1, 21),
            difficulty,
            strength,
            spendSparkleToken,
            risky,
            CheckRollSource.ServerTest,
            cancellationToken);

    private async Task<CheckResolution?> ResolveCheckAsync(
        Guid campaignId,
        Guid sessionId,
        Guid heroId,
        int roll,
        CheckDifficulty difficulty,
        string? strength,
        bool spendSparkleToken,
        bool risky,
        CheckRollSource rollSource,
        CancellationToken cancellationToken)
    {
        if (roll is < 1 or > 20)
        {
            throw new RuleValidationException("A d20 roll must be between 1 and 20.");
        }

        if (!Targets.TryGetValue(difficulty, out var target))
        {
            throw new RuleValidationException("Choose an easy, tricky, or heroic difficulty.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var campaign = await dbContext.Campaigns
            .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
        var session = await dbContext.Sessions
            .SingleOrDefaultAsync(
                item => item.CampaignId == campaignId && item.Id == sessionId,
                cancellationToken);
        var hero = await dbContext.Heroes
            .SingleOrDefaultAsync(
                item => item.CampaignId == campaignId && item.Id == heroId,
                cancellationToken);
        if (campaign is null || session is null || hero is null)
        {
            return null;
        }

        var latestSessionNumber = await dbContext.Sessions
            .Where(item => item.CampaignId == campaignId)
            .MaxAsync(item => (int?)item.SessionNumber, cancellationToken);
        if (campaign.Status != CampaignStatus.Active ||
            session.EndedAtUtc is not null ||
            session.SessionNumber != latestSessionNumber)
        {
            throw new RuleConflictException("Checks can only be resolved in the current active session.");
        }

        var normalizedStrength = strength?.Trim();
        var strengthBonus = 0;
        if (!string.IsNullOrEmpty(normalizedStrength))
        {
            if (!hero.Strengths.Any(available =>
                    string.Equals(available.Trim(), normalizedStrength, StringComparison.OrdinalIgnoreCase)))
            {
                throw new RuleValidationException("Choose one of this hero's listed strengths.");
            }

            strengthBonus = 2;
        }

        var totalBeforeSparkle = roll + strengthBonus;
        if (spendSparkleToken && totalBeforeSparkle >= target)
        {
            throw new RuleValidationException("A sparkle token can only be spent when the roll is below the target.");
        }

        if (spendSparkleToken && hero.SparkleTokens < 1)
        {
            throw new RuleConflictException("This hero has no sparkle token left this session.");
        }

        var sparkleBonus = spendSparkleToken ? 3 : 0;
        var total = totalBeforeSparkle + sparkleBonus;
        var outcome = ResolveOutcome(total, target);
        var heartsBefore = hero.Hearts;
        var sparkleTokensBefore = hero.SparkleTokens;
        if (spendSparkleToken)
        {
            hero.SparkleTokens--;
        }

        var losesHeart = outcome == CheckOutcome.SetbackWithProgress && risky && hero.Hearts > 0;
        if (losesHeart)
        {
            hero.Hearts--;
        }

        var resolution = new CheckResolution
        {
            CampaignId = campaignId,
            SessionId = sessionId,
            HeroId = heroId,
            Roll = roll,
            RollSource = rollSource,
            Difficulty = difficulty,
            Target = target,
            Strength = normalizedStrength,
            StrengthBonus = strengthBonus,
            SparkleBonus = sparkleBonus,
            Total = total,
            Outcome = outcome,
            ForwardProgressRequired = outcome is CheckOutcome.SuccessWithComplication or CheckOutcome.SetbackWithProgress,
            ConsequenceCategory = outcome switch
            {
                CheckOutcome.StrongSuccess => ConsequenceCategory.ExtraBenefit,
                CheckOutcome.Success => ConsequenceCategory.None,
                CheckOutcome.SuccessWithComplication => ConsequenceCategory.GentleComplication,
                CheckOutcome.SetbackWithProgress when losesHeart => ConsequenceCategory.GentleSetbackWithHeartLoss,
                CheckOutcome.SetbackWithProgress => ConsequenceCategory.GentleSetback,
                _ => throw new ArgumentOutOfRangeException(nameof(outcome))
            },
            Risky = risky,
            SparkleTokenSpent = spendSparkleToken,
            HeartsBefore = heartsBefore,
            HeartsAfter = hero.Hearts,
            SparkleTokensBefore = sparkleTokensBefore,
            SparkleTokensAfter = hero.SparkleTokens,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.CheckResolutions.Add(resolution);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return resolution;
    }

    private static CheckOutcome ResolveOutcome(int total, int target)
    {
        if (total >= target + 5)
        {
            return CheckOutcome.StrongSuccess;
        }

        if (total >= target)
        {
            return CheckOutcome.Success;
        }

        return total >= target - 2
            ? CheckOutcome.SuccessWithComplication
            : CheckOutcome.SetbackWithProgress;
    }
}
