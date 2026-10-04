using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record ParentControlsRequest(ParentSafetySettings? SafetySettings);

public sealed record ParentActionRequest(string? Action);

public static class ParentControlsRequestValidator
{
    private static readonly HashSet<string> ParentActions = new(StringComparer.Ordinal)
    {
        "makeEasier", "addClue", "skipScene", "moveTowardEnding", "pause", "resume", "endSession"
    };

    public static Dictionary<string, string[]> Validate(ParentSafetySettings? settings)
    {
        var errors = new Dictionary<string, string[]>();
        if (settings is null)
        {
            errors["safetySettings"] = ["Parent safety settings are required."];
            return errors;
        }

        if (!Enum.IsDefined(settings.FearLevel))
        {
            errors["fearLevel"] = ["Choose no fright or low fright."];
        }

        if (!Enum.IsDefined(settings.CombatMode))
        {
            errors["combatMode"] = ["Choose a supported combat mode."];
        }

        if (settings.MaxNarrationWords is < 40 or > 150)
        {
            errors["maxNarrationWords"] = ["Narration length must be between 40 and 150 words."];
        }

        if (settings.SessionLengthMinutes is < 15 or > 180)
        {
            errors["sessionLengthMinutes"] = ["Session length must be between 15 and 180 minutes."];
        }

        if (settings.ExcludedContent is null ||
            settings.ExcludedContent.Count > 20 ||
            settings.ExcludedContent.Any(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100) ||
            settings.ExcludedContent.Distinct(StringComparer.OrdinalIgnoreCase).Count() != settings.ExcludedContent.Count)
        {
            errors["excludedContent"] = ["List up to 20 distinct topics or creatures, each up to 100 characters."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(ParentActionRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request?.Action is null || !ParentActions.Contains(request.Action))
        {
            errors["action"] = ["Choose an available parent control."];
        }

        return errors;
    }
}
