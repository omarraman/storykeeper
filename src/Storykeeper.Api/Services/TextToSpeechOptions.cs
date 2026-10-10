namespace Storykeeper.Api.Services;

using Storykeeper.Api.Domain;

public sealed class TextToSpeechOptions
{
    public bool Enabled { get; set; }
    public TextToSpeechProviderKind Provider { get; set; } = TextToSpeechProviderKind.Disabled;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string NarratorVoice { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = "mp3_44100_128";
    public int TimeoutSeconds { get; set; } = 30;
    public bool CacheEnabled { get; set; } = true;
    public string CacheDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "tts-cache");
}
