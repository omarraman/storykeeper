namespace Storykeeper.Api.Services;

public sealed class AdventureDraftGenerationException(
    int statusCode,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;
}
