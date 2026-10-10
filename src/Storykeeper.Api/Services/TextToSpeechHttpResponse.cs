using System.Net.Http.Headers;

namespace Storykeeper.Api.Services;

internal static class TextToSpeechHttpResponse
{
    private const int MaximumAudioBytes = 25 * 1024 * 1024;

    public static async Task<TextToSpeechResult> ReadAudioAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null ||
            !contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
            response.Content.Headers.ContentLength > MaximumAudioBytes)
        {
            return TextToSpeechResult.Failed("invalid_audio_response");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var audio = new MemoryStream(response.Content.Headers.ContentLength is > 0 and <= MaximumAudioBytes
            ? (int)response.Content.Headers.ContentLength.Value
            : 0);
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (audio.Length + bytesRead > MaximumAudioBytes)
            {
                return TextToSpeechResult.Failed("invalid_audio_response");
            }

            await audio.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return audio.Length > 0
            ? TextToSpeechResult.Success(audio.ToArray(), contentType)
            : TextToSpeechResult.Failed("invalid_audio_response");
    }
}
