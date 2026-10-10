using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class ElevenLabsTextToSpeechProvider(
    HttpClient httpClient,
    IOptions<TextToSpeechOptions> options) : ITextToSpeechProvider
{
    private readonly TextToSpeechOptions _options = options.Value;

    public TextToSpeechProviderKind Kind => TextToSpeechProviderKind.ElevenLabs;

    public async Task<TextToSpeechResult> SynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        if (!TextToSpeechEndpoint.TryCreate(_options.BaseUrl, allowHttp: false, out var baseUri) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.NarratorVoice) ||
            string.IsNullOrWhiteSpace(_options.Model))
        {
            return TextToSpeechResult.Unavailable("provider_not_configured");
        }

        var endpoint = new Uri(
            $"{baseUri.AbsoluteUri.TrimEnd('/')}/v1/text-to-speech/{Uri.EscapeDataString(_options.NarratorVoice)}" +
            $"?output_format={Uri.EscapeDataString(_options.OutputFormat)}");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.TryAddWithoutValidation("xi-api-key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/*"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(new ElevenLabsRequest(text, _options.Model)),
            Encoding.UTF8,
            "application/json");

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

    private sealed record ElevenLabsRequest(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("model_id")] string ModelId);
}
