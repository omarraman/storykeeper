namespace Storykeeper.Api.Services;

public sealed class CampaignDraftRejectedException(Dictionary<string, string[]> errors)
    : Exception("The campaign draft did not pass content validation.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
