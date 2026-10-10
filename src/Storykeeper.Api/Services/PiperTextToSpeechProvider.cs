using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class PiperTextToSpeechProvider(
    HttpClient httpClient,
    IOptions<TextToSpeechOptions> options) : ITextToSpeechProvider
{
    private readonly TextToSpeechOptions _options = options.Value;

    public TextToSpeechProviderKind Kind => TextToSpeechProviderKind.Piper;

    public async Task<TextToSpeechResult> SynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        if (!TextToSpeechEndpoint.TryCreate(_options.BaseUrl, allowHttp: true, out var endpoint) ||
            string.IsNullOrWhiteSpace(_options.NarratorVoice))
        {
            return TextToSpeechResult.Unavailable("provider_not_configured");
        }

        var requestBody = new PiperRequest(
            text,
            EmptyToNull(_options.Model),
            EmptyToNull(_options.NarratorVoice),
            EmptyToNull(_options.OutputFormat));

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(requestBody)
        };

        try
        {
            using var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return TextToSpeechResult.Unavailable("provider_unavailable");
            }

            return await TextToSpeechHttpResponse.ReadAudioAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return TextToSpeechResult.Unavailable("provider_unavailable");
        }
        catch (IOException)
        {
            return TextToSpeechResult.Unavailable("provider_unavailable");
        }
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record PiperRequest(
        string Text,
        string? Model,
        string? Voice,
        [property: JsonPropertyName("output_format")] string? OutputFormat);
}
