using Storykeeper.Api.Contracts;

namespace Storykeeper.Api.Services;

public interface IStoryTurnGenerator
{
    Task<StoryTurnContent> GenerateAsync(StoryTurnGenerationInput input, CancellationToken cancellationToken = default);
}

public sealed record StoryTurnGenerationInput(string Action, string ContextJson);
