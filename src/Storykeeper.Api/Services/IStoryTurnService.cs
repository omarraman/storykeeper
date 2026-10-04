using Storykeeper.Api.Contracts;

namespace Storykeeper.Api.Services;

public interface IStoryTurnService
{
    Task<StoryTurnResponse?> SubmitActionAsync(
        Guid campaignId,
        StoryTurnRequest request,
        CancellationToken cancellationToken = default);
}
