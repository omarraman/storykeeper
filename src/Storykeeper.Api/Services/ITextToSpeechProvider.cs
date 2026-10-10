using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public interface ITextToSpeechProvider
{
    TextToSpeechProviderKind Kind { get; }

    Task<TextToSpeechResult> SynthesizeAsync(string text, CancellationToken cancellationToken);
}

public interface ITextToSpeechService
{
    Task<TextToSpeechResult> SynthesizeAsync(string text, CancellationToken cancellationToken);
}

public enum TextToSpeechResultStatus
{
    Success,
    Disabled,
    Unavailable,
    TimedOut,
    Failed
}

public sealed record TextToSpeechResult(
    TextToSpeechResultStatus Status,
    byte[]? AudioBytes = null,
    string? ContentType = null,
    string? ErrorCode = null)
{
    public static TextToSpeechResult Success(byte[] bytes, string contentType) =>
        new(TextToSpeechResultStatus.Success, bytes, contentType);

    public static TextToSpeechResult Disabled(string code = "disabled") =>
        new(TextToSpeechResultStatus.Disabled, ErrorCode: code);

    public static TextToSpeechResult Unavailable(string code = "unavailable") =>
        new(TextToSpeechResultStatus.Unavailable, ErrorCode: code);

    public static TextToSpeechResult TimedOut() =>
        new(TextToSpeechResultStatus.TimedOut, ErrorCode: "timeout");

    public static TextToSpeechResult Failed(string code = "provider_failure") =>
        new(TextToSpeechResultStatus.Failed, ErrorCode: code);
}
