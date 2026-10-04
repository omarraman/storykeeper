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

        if (request.SessionLengthMinutes is < 15 or > 180)
        {
            errors["sessionLengthMinutes"] = ["Session length must be between 15 and 180 minutes."];
        }

        if (request.ParentPreferences?.Trim().Length > 500)
        {
            errors["parentPreferences"] = ["Parent preferences cannot exceed 500 characters."];
        }

        return errors;
    }
}
