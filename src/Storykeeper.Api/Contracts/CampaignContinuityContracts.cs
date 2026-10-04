using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record CampaignContinuityResponse(
    IReadOnlyList<CampaignFactResponse> Facts,
    IReadOnlyList<SessionSummaryResponse> Summaries,
    IReadOnlyList<ContinuityRevisionResponse> Revisions);

public sealed record CampaignFactResponse(
    Guid Id,
    Guid? SourceSessionId,
    string Category,
    string Statement,
    string Status,
    int Importance,
    DateTimeOffset? LastEditedAtUtc)
{
    public static CampaignFactResponse From(
        CampaignFact fact,
        DateTimeOffset? lastEditedAtUtc = null) =>
        new(fact.Id, fact.SourceSessionId, fact.Category, fact.Statement,
            fact.Status.ToString(), fact.Importance, lastEditedAtUtc);
}

public sealed record SessionSummaryResponse(
    Guid SessionId,
    int SessionNumber,
    string Summary,
    DateTimeOffset? EndedAtUtc,
    DateTimeOffset? LastEditedAtUtc);

public sealed record ContinuityRevisionResponse(
    Guid Id,
    string RecordType,
    Guid RecordId,
    Guid? SourceSessionId,
    string? PreviousContent,
    string NewContent,
    string ChangedBy,
    DateTimeOffset ChangedAtUtc)
{
    public static ContinuityRevisionResponse From(CampaignContinuityRevision revision) =>
        new(revision.Id, revision.RecordType.ToString(), revision.RecordId,
            revision.SourceSessionId, revision.PreviousContent, revision.NewContent,
            revision.ChangedBy, revision.ChangedAtUtc);
}

public sealed record UpdateCampaignFactRequest(
    string? Category,
    string? Statement,
    string? Status,
    int Importance);

public sealed record CreateCampaignFactRequest(
    string? Category,
    string? Statement,
    string? Status,
    int Importance,
    Guid? SourceSessionId);

public sealed record SaveSessionSummaryRequest(string? Summary);

public static class CampaignContinuityRequestValidator
{
    private static readonly HashSet<string> FactCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "clue", "world", "npc", "quest", "party", "location", "object",
        "promise", "thread", "relationship", "reward", "other"
    };

    public static Dictionary<string, string[]> Validate(UpdateCampaignFactRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["fact"] = ["A fact correction is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Category) ||
            !FactCategories.Contains(request.Category.Trim()))
        {
            errors["category"] = ["Choose a supported fact category."];
        }
        if (string.IsNullOrWhiteSpace(request.Statement) || request.Statement.Trim().Length > 2000)
        {
            errors["statement"] = ["A fact statement is required and cannot exceed 2000 characters."];
        }
        else if (!CampaignDraftValidator.IsSafeGeneratedText(request.Statement))
        {
            errors["statement"] = ["The fact includes content outside Storykeeper's child-safety boundaries."];
        }
        if (!Enum.TryParse<CampaignFactStatus>(request.Status, true, out var status) ||
            !Enum.IsDefined(status))
        {
            errors["status"] = ["Choose a valid fact status."];
        }
        if (request.Importance is < 1 or > 5)
        {
            errors["importance"] = ["Importance must be between 1 and 5."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(CreateCampaignFactRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["fact"] = ["A campaign fact is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Category) ||
            !FactCategories.Contains(request.Category.Trim()))
        {
            errors["category"] = ["Choose a supported fact category."];
        }
        if (string.IsNullOrWhiteSpace(request.Statement) || request.Statement.Trim().Length > 2000)
        {
            errors["statement"] = ["A fact statement is required and cannot exceed 2000 characters."];
        }
        else if (!CampaignDraftValidator.IsSafeGeneratedText(request.Statement))
        {
            errors["statement"] = ["The fact includes content outside Storykeeper's child-safety boundaries."];
        }
        if (!Enum.TryParse<CampaignFactStatus>(request.Status, true, out var status) ||
            !Enum.IsDefined(status) || status == CampaignFactStatus.Proposed)
        {
            errors["status"] = ["Parent-created facts must be active, resolved, superseded, or discarded."];
        }
        if (request.Importance is < 1 or > 5)
        {
            errors["importance"] = ["Importance must be between 1 and 5."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(SaveSessionSummaryRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request?.Summary) || request.Summary.Trim().Length > 1200)
        {
            errors["summary"] = ["A factual session summary is required and cannot exceed 1200 characters."];
        }
        else if (!CampaignDraftValidator.IsSafeGeneratedText(request.Summary))
        {
            errors["summary"] = ["The summary includes content outside Storykeeper's child-safety boundaries."];
        }
        return errors;
    }
}
