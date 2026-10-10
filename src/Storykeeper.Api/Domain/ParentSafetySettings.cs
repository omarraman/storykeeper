namespace Storykeeper.Api.Domain;

public enum FearLevel
{
    None = 0,
    Low = 1
}

public enum CombatMode
{
    Avoid = 0,
    Silly = 1,
    StoryOnly = 2
}

public enum NarrationPlaybackPreference
{
    Off,
    OnDemand,
    AutoplayAfterNewStoryBeat
}

public enum TextToSpeechProviderKind
{
    Disabled,
    Piper,
    ElevenLabs
}

public sealed record ParentSafetySettings
{
    public FearLevel FearLevel { get; init; } = FearLevel.Low;
    public CombatMode CombatMode { get; init; } = CombatMode.Avoid;
    public bool VoiceEnabled { get; init; }
    public bool TextToSpeechEnabled { get; init; }
    public TextToSpeechProviderKind NarrationProvider { get; init; } = TextToSpeechProviderKind.Disabled;
    public NarrationPlaybackPreference NarrationPlayback { get; init; } = NarrationPlaybackPreference.Off;
    public List<string> ExcludedContent { get; init; } = [];
    public int MaxNarrationWords { get; init; } = 120;
    public int SessionLengthMinutes { get; init; } = 45;

    public static ParentSafetySettings Defaults => new();
}
