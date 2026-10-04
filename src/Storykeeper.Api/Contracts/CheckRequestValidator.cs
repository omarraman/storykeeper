using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public static class CheckRequestValidator
{
    public static Dictionary<string, string[]> Validate(CheckRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["A check request is required."];
            return errors;
        }

        if (request.Roll is < 1 or > 20)
        {
            errors["roll"] = ["Enter a d20 result from 1 to 20."];
        }

        if (!Enum.IsDefined(request.Difficulty))
        {
            errors["difficulty"] = ["Choose easy, tricky, or heroic."];
        }

        if (request.Strength?.Trim().Length > 80)
        {
            errors["strength"] = ["A strength name cannot exceed 80 characters."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(TestCheckRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["A check request is required."];
            return errors;
        }

        if (!Enum.IsDefined(request.Difficulty))
        {
            errors["difficulty"] = ["Choose easy, tricky, or heroic."];
        }

        if (request.Strength?.Trim().Length > 80)
        {
            errors["strength"] = ["A strength name cannot exceed 80 characters."];
        }

        return errors;
    }
}
