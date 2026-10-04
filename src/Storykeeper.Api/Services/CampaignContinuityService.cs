using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Data;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class CampaignContinuityService(StorykeeperDbContext dbContext) : ICampaignContinuityService
{
    public async Task<CampaignContinuityResponse?> GetAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Campaigns.AnyAsync(item => item.Id == campaignId, cancellationToken))
        {
            return null;
        }

        var facts = await dbContext.CampaignFacts.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderBy(item => item.Status)
            .ThenByDescending(item => item.Importance)
            .ThenBy(item => item.Statement)
            .ToArrayAsync(cancellationToken);
        var sessions = await dbContext.Sessions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId && item.Summary != null)
            .OrderByDescending(item => item.SessionNumber)
            .Take(30)
            .ToArrayAsync(cancellationToken);
        var revisions = await dbContext.CampaignContinuityRevisions.AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);
        var editedAt = revisions
            .GroupBy(revision => (revision.RecordType, revision.RecordId))
            .ToDictionary(group => group.Key, group => (DateTimeOffset?)group.Max(item => item.ChangedAtUtc));

        return new CampaignContinuityResponse(
            facts.Select(fact => CampaignFactResponse.From(
                fact,
                editedAt.GetValueOrDefault((ContinuityRecordType.Fact, fact.Id)))).ToArray(),
            sessions.Select(session => new SessionSummaryResponse(
                session.Id,
                session.SessionNumber,
                session.Summary!,
                session.EndedAtUtc,
                editedAt.GetValueOrDefault((ContinuityRecordType.Summary, session.Id)))).ToArray(),
            revisions.OrderByDescending(revision => revision.ChangedAtUtc)
                .Take(200)
                .Select(ContinuityRevisionResponse.From)
                .ToArray());
    }

    public async Task<CampaignFactResponse?> GetFactAsync(
        Guid campaignId,
        Guid factId,
        CancellationToken cancellationToken = default)
    {
        var fact = await dbContext.CampaignFacts.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CampaignId == campaignId && item.Id == factId,
                cancellationToken);
        return fact is null ? null : CampaignFactResponse.From(fact);
    }

    public async Task<CampaignFactResponse?> UpdateFactAsync(
        Guid campaignId,
        Guid factId,
        UpdateCampaignFactRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = CampaignContinuityRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new RuleValidationException(string.Join(" ", errors.Values.SelectMany(value => value)));
        }

        var fact = await dbContext.CampaignFacts
            .SingleOrDefaultAsync(item => item.CampaignId == campaignId && item.Id == factId, cancellationToken);
        if (fact is null)
        {
            return null;
        }

        var campaignIsArchived = await dbContext.Campaigns
            .AnyAsync(item => item.Id == campaignId && item.Status == CampaignStatus.Archived, cancellationToken);
        if (campaignIsArchived)
        {
            throw new RuleConflictException("Archived campaign continuity is read-only.");
        }

        var status = Enum.Parse<CampaignFactStatus>(request.Status!.Trim(), true);
        var updatedCategory = request.Category!.Trim().ToLowerInvariant();
        var updatedStatement = request.Statement!.Trim();
        if (fact.Category == updatedCategory &&
            fact.Statement == updatedStatement &&
            fact.Status == status &&
            fact.Importance == request.Importance)
        {
            return CampaignFactResponse.From(fact);
        }

        var previous = SerializeFact(fact);
        fact.Category = updatedCategory;
        fact.Statement = updatedStatement;
        fact.Status = status;
        fact.Importance = request.Importance;
        var revision = new CampaignContinuityRevision
        {
            CampaignId = campaignId,
            RecordType = ContinuityRecordType.Fact,
            RecordId = fact.Id,
            SourceSessionId = fact.SourceSessionId,
            PreviousContent = previous,
            NewContent = SerializeFact(fact)
        };
        dbContext.CampaignContinuityRevisions.Add(revision);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CampaignFactResponse.From(fact, revision.ChangedAtUtc);
    }

    public async Task<CampaignFactResponse?> CreateFactAsync(
        Guid campaignId,
        CreateCampaignFactRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = CampaignContinuityRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new RuleValidationException(string.Join(" ", errors.Values.SelectMany(value => value)));
        }

        var campaign = await dbContext.Campaigns
            .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            return null;
        }
        if (campaign.Status == CampaignStatus.Archived)
        {
            throw new RuleConflictException("Archived campaign continuity is read-only.");
        }
        if (request.SourceSessionId is { } sourceSessionId &&
            !await dbContext.Sessions.AnyAsync(
                item => item.CampaignId == campaignId && item.Id == sourceSessionId,
                cancellationToken))
        {
            throw new RuleValidationException("The source session must belong to this campaign.");
        }

        var fact = new CampaignFact
        {
            CampaignId = campaignId,
            SourceSessionId = request.SourceSessionId,
            Category = request.Category!.Trim().ToLowerInvariant(),
            Statement = request.Statement!.Trim(),
            Status = Enum.Parse<CampaignFactStatus>(request.Status!.Trim(), true),
            Importance = request.Importance
        };
        var revision = new CampaignContinuityRevision
        {
            CampaignId = campaignId,
            RecordType = ContinuityRecordType.Fact,
            RecordId = fact.Id,
            SourceSessionId = fact.SourceSessionId,
            NewContent = SerializeFact(fact)
        };
        dbContext.CampaignFacts.Add(fact);
        dbContext.CampaignContinuityRevisions.Add(revision);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CampaignFactResponse.From(fact, revision.ChangedAtUtc);
    }

    public async Task<SessionSummaryResponse?> SaveSummaryAsync(
        Guid campaignId,
        Guid sessionId,
        SaveSessionSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = CampaignContinuityRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new RuleValidationException(string.Join(" ", errors.Values.SelectMany(value => value)));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var session = await dbContext.Sessions
            .SingleOrDefaultAsync(item => item.CampaignId == campaignId && item.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var campaign = await dbContext.Campaigns
            .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            return null;
        }
        if (campaign.Status == CampaignStatus.Archived)
        {
            throw new RuleConflictException("Archived campaign continuity is read-only.");
        }

        var summary = request.Summary!.Trim();
        var wasActive = session.EndedAtUtc is null;
        if (wasActive)
        {
            var latestSessionId = await dbContext.Sessions
                .Where(item => item.CampaignId == campaignId)
                .OrderByDescending(item => item.SessionNumber)
                .Select(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (campaign.Status != CampaignStatus.Active || latestSessionId != session.Id)
            {
                throw new RuleConflictException("Only the current active session can be ended.");
            }
        }

        var previousSummary = session.Summary;
        var summaryChanged = !string.Equals(previousSummary, summary, StringComparison.Ordinal);
        if (wasActive)
        {
            session.EndedAtUtc = DateTimeOffset.UtcNow;
        }
        session.Summary = summary;

        CampaignContinuityRevision? revision = null;
        if (summaryChanged || wasActive)
        {
            revision = new CampaignContinuityRevision
            {
                CampaignId = campaignId,
                RecordType = ContinuityRecordType.Summary,
                RecordId = session.Id,
                SourceSessionId = session.Id,
                PreviousContent = previousSummary,
                NewContent = summary
            };
            dbContext.CampaignContinuityRevisions.Add(revision);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SessionSummaryResponse(
            session.Id,
            session.SessionNumber,
            session.Summary,
            session.EndedAtUtc,
            revision?.ChangedAtUtc);
    }

    private static string SerializeFact(CampaignFact fact) =>
        $"Category: {fact.Category}\nFact: {fact.Statement}\nStatus: {fact.Status}\n" +
        $"Importance: {fact.Importance}\nSource session: {fact.SourceSessionId?.ToString() ?? "none"}";
}
