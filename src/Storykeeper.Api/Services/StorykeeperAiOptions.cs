namespace Storykeeper.Api.Services;

public sealed class StorykeeperAiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public double? Temperature { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public int RecentTurnLimit { get; set; } = 8;
    public int RecentTurnCharacterBudget { get; set; } = 12_000;
}
