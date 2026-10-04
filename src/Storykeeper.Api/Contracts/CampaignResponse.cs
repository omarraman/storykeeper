using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record CampaignResponse(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    string Theme,
    string Tone,
    bool LowFright,
    int BibleVersion,
    string WorldDescription,
    string? CurrentSituation,
    string PartyName,
    IReadOnlyList<HeroResponse> Heroes,
    IReadOnlyList<QuestResponse> Quests,
    IReadOnlyList<SessionResponse> Sessions,
    QuestResponse? CurrentQuest,
    SessionResponse? LatestSession)
{
    public static CampaignResponse From(Campaign campaign)
    {
        var quests = campaign.Quests
            .OrderBy(quest => quest.Title)
            .Select(QuestResponse.From)
            .ToArray();
        var sessions = campaign.Sessions
            .OrderBy(session => session.SessionNumber)
            .Select(SessionResponse.From)
            .ToArray();
        var currentQuest = campaign.Quests.FirstOrDefault(quest => quest.Status == QuestStatus.InProgress);
        var latestSession = campaign.Sessions.MaxBy(session => session.SessionNumber);

        return new CampaignResponse(
            campaign.Id,
            campaign.Name,
            campaign.Description,
            campaign.Status.ToString(),
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc,
            campaign.ArchivedAtUtc,
            campaign.Settings?.Theme ?? string.Empty,
            campaign.Settings?.Tone ?? string.Empty,
            campaign.Settings?.LowFright ?? true,
            campaign.Bible?.Version ?? 1,
            campaign.Bible?.WorldDescription ?? string.Empty,
            campaign.Bible?.CurrentSituation,
            campaign.Party?.Name ?? string.Empty,
            campaign.Party?.Heroes
                .OrderBy(hero => hero.Name)
                .Select(HeroResponse.From)
                .ToArray() ?? [],
            quests,
            sessions,
            currentQuest is null ? null : QuestResponse.From(currentQuest),
            latestSession is null ? null : SessionResponse.From(latestSession));
    }
}

public sealed record HeroResponse(
    Guid Id,
    string Name,
    string Description,
    string Role,
    IReadOnlyList<string> Strengths,
    int Hearts,
    int SparkleTokens,
    IReadOnlyList<InventoryItemResponse> Inventory)
{
    public static HeroResponse From(Hero hero) => new(
        hero.Id,
        hero.Name,
        hero.Description,
        hero.Role,
        hero.Strengths.ToArray(),
        hero.Hearts,
        hero.SparkleTokens,
        hero.Inventory
            .OrderBy(item => item.Name)
            .Select(item => new InventoryItemResponse(item.Name, item.Description, item.Quantity))
            .ToArray());
}

public sealed record InventoryItemResponse(string Name, string Description, int Quantity);

public sealed record QuestResponse(Guid Id, string Title, string Description, string Status)
{
    public static QuestResponse From(Quest quest) =>
        new(quest.Id, quest.Title, quest.Description, quest.Status.ToString());
}

public sealed record SessionResponse(
    Guid Id,
    int SessionNumber,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? Summary)
{
    public static SessionResponse From(Session session) =>
        new(session.Id, session.SessionNumber, session.StartedAtUtc, session.EndedAtUtc, session.Summary);
}
