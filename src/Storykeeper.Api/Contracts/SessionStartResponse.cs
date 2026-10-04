using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record SessionStartResponse(
    Guid Id,
    Guid CampaignId,
    int SessionNumber,
    DateTimeOffset StartedAtUtc,
    IReadOnlyList<SessionHeroStateResponse> Heroes)
{
    public static SessionStartResponse From(Session session, IReadOnlyList<Hero> heroes) =>
        new(
            session.Id,
            session.CampaignId,
            session.SessionNumber,
            session.StartedAtUtc,
            heroes.Select(hero => new SessionHeroStateResponse(
                hero.Id,
                hero.Name,
                hero.Hearts,
                hero.SparkleTokens)).ToArray());
}

public sealed record SessionHeroStateResponse(
    Guid HeroId,
    string HeroName,
    int Hearts,
    int SparkleTokens);
