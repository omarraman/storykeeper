namespace Storykeeper.Api.Contracts;

public sealed record TextToSpeechErrorResponse(string Type, string Code, string Message, bool Retryable);
