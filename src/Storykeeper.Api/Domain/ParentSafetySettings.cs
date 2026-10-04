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

public sealed record ParentSafetySettings
{
    public FearLevel FearLevel { get; init; } = FearLevel.Low;
    public CombatMode CombatMode { get; init; } = CombatMode.Avoid;
    public List<string> ExcludedContent { get; init; } = [];
    public int MaxNarrationWords { get; init; } = 120;
    public int SessionLengthMinutes { get; init; } = 45;

    public static ParentSafetySettings Defaults => new();
}
