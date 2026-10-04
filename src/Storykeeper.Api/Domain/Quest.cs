namespace Storykeeper.Api.Domain;

public enum QuestStatus
{
    Available,
    InProgress,
    Completed,
    Paused
}

public sealed class Quest : CampaignEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public QuestStatus Status { get; set; } = QuestStatus.Available;
}
