using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Xunit;

namespace Storykeeper.Api.Tests;

public sealed class TextToSpeechTests
{
    [Fact]
    public async Task DisabledProviderReturnsTypedDisabledResult()
    {
        var provider = new DisabledTextToSpeechProvider();

        var result = await provider.SynthesizeAsync("A cheerful lantern glows.", CancellationToken.None);

        Assert.Equal(TextToSpeechResultStatus.Disabled, result.Status);
        Assert.Equal("disabled", result.ErrorCode);
    }

    [Fact]
    public async Task DisabledServiceDoesNotInvokeConfiguredProvider()
    {
        var provider = new FakeProvider(TextToSpeechProviderKind.Piper, (_, _) =>
            Task.FromResult(TextToSpeechResult.Success([1], "audio/wav")));
        var service = new TextToSpeechService(
            Options.Create(new TextToSpeechOptions { Provider = TextToSpeechProviderKind.Piper }),
            provider);

        var result = await service.SynthesizeAsync("A cheerful lantern glows.", CancellationToken.None);

        Assert.Equal(TextToSpeechResultStatus.Disabled, result.Status);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task SuccessfulAudioIsCachedUsingProviderAndNarrationSettings()
    {
        var cacheDirectory = NewCacheDirectory();
        try
        {
            var provider = new FakeProvider(TextToSpeechProviderKind.Piper, (_, _) =>
                Task.FromResult(TextToSpeechResult.Success([1, 2, 3], "audio/wav")));
            var service = new TextToSpeechService(Options.Create(new TextToSpeechOptions
            {
                Enabled = true,
                Provider = TextToSpeechProviderKind.Piper,
                Model = "model-a",
                NarratorVoice = "voice-a",
                OutputFormat = "wav",
                CacheDirectory = cacheDirectory
            }), provider);

            var first = await service.SynthesizeAsync("A cheerful lantern glows.", CancellationToken.None);
            var second = await service.SynthesizeAsync("A cheerful lantern glows.", CancellationToken.None);

            Assert.Equal(TextToSpeechResultStatus.Success, first.Status);
            Assert.Equal("audio/wav", second.ContentType);
            Assert.Equal(new byte[] { 1, 2, 3 }, second.AudioBytes);
            Assert.Equal(1, provider.Calls);

            var differentVoice = new TextToSpeechService(Options.Create(new TextToSpeechOptions
            {
                Enabled = true,
                Provider = TextToSpeechProviderKind.Piper,
                Model = "model-a",
                NarratorVoice = "voice-b",
                OutputFormat = "wav",
                CacheDirectory = cacheDirectory
            }), provider);
            await differentVoice.SynthesizeAsync("A cheerful lantern glows.", CancellationToken.None);
            Assert.Equal(2, provider.Calls);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ProviderTimeoutAndFailureAreRecoverableTypedResults()
    {
        var timeoutProvider = new FakeProvider(TextToSpeechProviderKind.Piper, async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return TextToSpeechResult.Failed();
        });
        var timeoutService = new TextToSpeechService(Options.Create(new TextToSpeechOptions
        {
            Enabled = true,
            Provider = TextToSpeechProviderKind.Piper,
            TimeoutSeconds = 1,
            CacheEnabled = false
        }), timeoutProvider);

        var timedOut = await timeoutService.SynthesizeAsync("The story continues.", CancellationToken.None);
        var failed = await new TextToSpeechService(Options.Create(new TextToSpeechOptions
        {
            Enabled = true,
            Provider = TextToSpeechProviderKind.Piper,
            CacheEnabled = false
        }), new FakeProvider(TextToSpeechProviderKind.Piper, (_, _) =>
            Task.FromResult(TextToSpeechResult.Unavailable("provider_unavailable"))))
            .SynthesizeAsync("The story continues.", CancellationToken.None);

        Assert.Equal(TextToSpeechResultStatus.TimedOut, timedOut.Status);
        Assert.Equal(TextToSpeechResultStatus.Unavailable, failed.Status);
        Assert.Equal("provider_unavailable", failed.ErrorCode);
    }

    [Fact]
    public async Task PiperPostsToConfiguredLocalEndpointAndReturnsAudio()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([4, 5, 6])
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav") }
            }
        });
        using var client = new HttpClient(handler);
        var provider = new PiperTextToSpeechProvider(client, Options.Create(new TextToSpeechOptions
        {
            BaseUrl = "http://127.0.0.1:8123/synthesize",
            Model = "local-model",
            NarratorVoice = "local-voice",
            OutputFormat = "wav"
        }));

        var result = await provider.SynthesizeAsync("A cheerful lantern glows.", CancellationToken.None);

        Assert.Equal("http://127.0.0.1:8123/synthesize", handler.RequestUri!.AbsoluteUri);
        Assert.Equal(HttpMethod.Post, handler.Method);
        using var requestBody = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("A cheerful lantern glows.", requestBody.RootElement.GetProperty("text").GetString());
        Assert.Equal("local-model", requestBody.RootElement.GetProperty("model").GetString());
        Assert.Equal("local-voice", requestBody.RootElement.GetProperty("voice").GetString());
        Assert.Equal("wav", requestBody.RootElement.GetProperty("output_format").GetString());
        Assert.Equal(TextToSpeechResultStatus.Success, result.Status);
        Assert.Equal("audio/wav", result.ContentType);
    }

    [Fact]
    public async Task ElevenLabsKeyStaysInServerSideHeaderAndProviderErrorsDoNotExposeIt()
    {
        const string secretKey = "server-only-key";
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("credential rejected")
        });
        using var client = new HttpClient(handler);
        var provider = new ElevenLabsTextToSpeechProvider(client, Options.Create(new TextToSpeechOptions
        {
            BaseUrl = "https://tts.example.test",
            ApiKey = secretKey,
            Model = "safe-model",
            NarratorVoice = "narrator-id",
            OutputFormat = "mp3_44100_128"
        }));

        var result = await provider.SynthesizeAsync("A cheerful lantern glows.", CancellationToken.None);

        Assert.Equal("https://tts.example.test/v1/text-to-speech/narrator-id?output_format=mp3_44100_128",
            handler.RequestUri!.AbsoluteUri);
        Assert.Equal(secretKey, handler.ApiKey);
        Assert.DoesNotContain(secretKey, handler.RequestBody);
        Assert.Equal(TextToSpeechResultStatus.Unavailable, result.Status);
        Assert.DoesNotContain(secretKey, result.ErrorCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElevenLabsSendsTextAndModelIdUsingTheProviderJsonContract()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("""{"detail":"request rejected"}""")
        });
        using var client = new HttpClient(handler);
        var provider = new ElevenLabsTextToSpeechProvider(client, Options.Create(new TextToSpeechOptions
        {
            BaseUrl = "https://tts.example.test",
            ApiKey = "server-only-test-key",
            Model = "test-model",
            NarratorVoice = "test-voice",
            OutputFormat = "mp3_44100_128"
        }));

        await provider.SynthesizeAsync("The lantern glows.", CancellationToken.None);

        using var requestBody = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("The lantern glows.", requestBody.RootElement.GetProperty("text").GetString());
        Assert.Equal("test-model", requestBody.RootElement.GetProperty("model_id").GetString());
    }

    private static string NewCacheDirectory() =>
        Path.Combine(Path.GetTempPath(), $"storykeeper-tts-test-{Guid.NewGuid():N}");

    private sealed class FakeProvider(
        TextToSpeechProviderKind kind,
        Func<string, CancellationToken, Task<TextToSpeechResult>> synthesize) : ITextToSpeechProvider
    {
        public int Calls { get; private set; }
        public TextToSpeechProviderKind Kind => kind;

        public Task<TextToSpeechResult> SynthesizeAsync(string text, CancellationToken cancellationToken)
        {
            Calls++;
            return synthesize(text, cancellationToken);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? ApiKey { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            ApiKey = request.Headers.TryGetValues("xi-api-key", out var values) ? values.Single() : null;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }
}
