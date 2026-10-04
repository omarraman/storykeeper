using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record CampaignBriefRequest(
    string? Title,
    string? Genre,
    string? Tone,
    int CampaignLengthSessions,
    int SessionLengthMinutes,
    IReadOnlyList<string>? Inclusions,
    IReadOnlyList<string>? Exclusions,
    string? StoryIdea)
{
    public CampaignBrief ToDomain() => new()
    {
        Title = Title!.Trim(),
        Genre = Genre!.Trim(),
        Tone = Tone!.Trim(),
        CampaignLengthSessions = CampaignLengthSessions,
        SessionLengthMinutes = SessionLengthMinutes,
        Inclusions = Normalize(Inclusions),
        Exclusions = Normalize(Exclusions),
        StoryIdea = string.IsNullOrWhiteSpace(StoryIdea) ? null : StoryIdea.Trim()
    };

    private static List<string> Normalize(IReadOnlyList<string>? values) =>
        values?.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
}

public static class CampaignBriefRequestValidator
{
    public static Dictionary<string, string[]> Validate(CampaignBriefRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["brief"] = ["A campaign brief is required."];
            return errors;
        }

        ValidateRequiredText(errors, "title", request.Title, 120, "A story world title is required.", "Titles cannot exceed 120 characters.");
        ValidateRequiredText(errors, "genre", request.Genre, 100, "A genre is required.", "Genres cannot exceed 100 characters.");
        ValidateRequiredText(errors, "tone", request.Tone, 300, "A tone is required.", "Tones cannot exceed 300 characters.");
        if (request.CampaignLengthSessions is < 1 or > 30)
        {
            errors["campaignLengthSessions"] = ["Campaign length must be between 1 and 30 sessions."];
        }

        if (request.SessionLengthMinutes is < 15 or > 180)
        {
            errors["sessionLengthMinutes"] = ["Session length must be between 15 and 180 minutes."];
        }

        ValidateList(errors, "inclusions", request.Inclusions);
        ValidateList(errors, "exclusions", request.Exclusions);
        if (request.StoryIdea?.Trim().Length > 2000)
        {
            errors["storyIdea"] = ["The story idea cannot exceed 2000 characters."];
        }

        return errors;
    }

    private static void ValidateRequiredText(
        IDictionary<string, string[]> errors,
        string field,
        string? value,
        int maxLength,
        string requiredMessage,
        string lengthMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [requiredMessage];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [lengthMessage];
        }
    }

    private static void ValidateList(IDictionary<string, string[]> errors, string field, IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return;
        }

        if (values.Count > 20 || values.Any(value => value is null || value.Trim().Length > 100))
        {
            errors[field] = ["Choose no more than 20 items, each up to 100 characters."];
        }
    }
}
