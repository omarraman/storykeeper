namespace Storykeeper.Api.Contracts;

public static class StoryTurnRequestValidator
{
    public static Dictionary<string, string[]> Validate(StoryTurnRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["A story action is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Action))
        {
            errors["action"] = ["Tell the Storykeeper what the heroes would like to do."];
        }
        else if (request.Action.Trim().Length > 500)
        {
            errors["action"] = ["Actions cannot exceed 500 characters."];
        }

        if (request.SessionId is null || request.SessionId == Guid.Empty)
        {
            errors["sessionId"] = ["A current session is required."];
        }

        if (request.HeroId == Guid.Empty)
        {
            errors["heroId"] = ["Choose a hero from this campaign."];
        }

        if (request.CheckResolutionId == Guid.Empty)
        {
            errors["checkResolutionId"] = ["Choose a valid server-resolved check."];
        }

        return errors;
    }
}
