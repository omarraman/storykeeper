using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public static class AdventureDraftRequestValidator
{
    public static Dictionary<string, string[]> Validate(AdventureDraftRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["Adventure generation preferences are required."];
            return errors;
        }

        if (request.SessionLengthMinutes is not (30 or 45 or 60))
        {
            errors["sessionLengthMinutes"] = ["Choose an adventure length of 30, 45, or 60 minutes."];
        }

        if (request.ParentPreferences?.Trim().Length > 500)
        {
            errors["parentPreferences"] = ["Parent preferences cannot exceed 500 characters."];
        }

        return errors;
    }
}
