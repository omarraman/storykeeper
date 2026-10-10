using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public sealed class TextToSpeechService(
    IOptions<TextToSpeechOptions> options,
    ITextToSpeechProvider provider) : ITextToSpeechService
{
    private readonly TextToSpeechOptions _options = options.Value;

    public async Task<TextToSpeechResult> SynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        if (!_options.Enabled || _options.Provider == TextToSpeechProviderKind.Disabled)
        {
            return TextToSpeechResult.Disabled();
        }

        if (provider.Kind != _options.Provider)
        {
            return TextToSpeechResult.Unavailable("provider_not_configured");
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length > 1200)
        {
            return TextToSpeechResult.Failed("invalid_narration");
        }

        if (_options.CacheEnabled && string.IsNullOrWhiteSpace(_options.CacheDirectory))
        {
            return TextToSpeechResult.Unavailable("cache_unavailable");
        }

        var cachePath = _options.CacheEnabled ? GetCachePath(text) : null;
        if (cachePath is not null)
        {
            try
            {
                if (File.Exists(cachePath))
                {
                    var cachedBytes = await File.ReadAllBytesAsync(cachePath, cancellationToken);
                    var contentTypeSeparator = Array.IndexOf(cachedBytes, (byte)'\n');
                    if (contentTypeSeparator > 0 && contentTypeSeparator < cachedBytes.Length - 1)
                    {
                        var contentType = Encoding.ASCII.GetString(cachedBytes, 0, contentTypeSeparator);
                        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                        {
                            return TextToSpeechResult.Success(
                                cachedBytes[(contentTypeSeparator + 1)..], contentType);
                        }
                    }
                }
            }
            catch (IOException)
            {
                return TextToSpeechResult.Unavailable("cache_unavailable");
            }
            catch (UnauthorizedAccessException)
            {
                return TextToSpeechResult.Unavailable("cache_unavailable");
            }
            catch (ArgumentException)
            {
                return TextToSpeechResult.Unavailable("cache_unavailable");
            }
            catch (NotSupportedException)
            {
                return TextToSpeechResult.Unavailable("cache_unavailable");
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds is >= 1 and <= 180
            ? _options.TimeoutSeconds
            : 30));
        TextToSpeechResult result;
        try
        {
            result = await provider.SynthesizeAsync(text.Trim(), timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TextToSpeechResult.TimedOut();
        }

        if (result.Status == TextToSpeechResultStatus.Success &&
            (result.AudioBytes is not { Length: > 0 } ||
             result.ContentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) != true))
        {
            return TextToSpeechResult.Failed("invalid_audio_response");
        }

        if (result.Status != TextToSpeechResultStatus.Success || cachePath is null)
        {
            return result;
        }

        try
        {
            Directory.CreateDirectory(_options.CacheDirectory);
            var temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
            var contentTypeBytes = Encoding.ASCII.GetBytes(result.ContentType! + "\n");
            var cachedBytes = new byte[contentTypeBytes.Length + result.AudioBytes!.Length];
            contentTypeBytes.CopyTo(cachedBytes, 0);
            result.AudioBytes.CopyTo(cachedBytes, contentTypeBytes.Length);
            await File.WriteAllBytesAsync(temporaryPath, cachedBytes, cancellationToken);
            File.Move(temporaryPath, cachePath, overwrite: true);
        }
        catch (IOException)
        {
            return TextToSpeechResult.Unavailable("cache_unavailable");
        }
        catch (UnauthorizedAccessException)
        {
            return TextToSpeechResult.Unavailable("cache_unavailable");
        }
        catch (ArgumentException)
        {
            return TextToSpeechResult.Unavailable("cache_unavailable");
        }
        catch (NotSupportedException)
        {
            return TextToSpeechResult.Unavailable("cache_unavailable");
        }

        return result;
    }

    private string GetCachePath(string text)
    {
        var narrationHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim())));
        var cacheInput = string.Join('\n',
            narrationHash,
            _options.Provider.ToString(),
            _options.Model,
            _options.NarratorVoice,
            _options.OutputFormat);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheInput)));
        return Path.Combine(_options.CacheDirectory, $"{digest}.audio");
    }
}
