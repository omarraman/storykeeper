namespace Storykeeper.Api.Services;

public sealed class StoryTurnGenerationException(int statusCode, string safeMessage, Exception? innerException = null)
    : Exception(safeMessage, innerException)
{
    public int StatusCode { get; } = statusCode;
    public string SafeMessage { get; } = safeMessage;
}
