using System.Text.Json;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Domain;
using Xunit;

namespace Storykeeper.Api.Tests;

public sealed class CampaignResponseTests
{
    [Fact]
    public void CurrentQuestResponseIncludesItsOpeningAndFeaturedNpcWithoutFuturePlanDetails()
    {
        var activeQuest = new Quest
        {
            Title = "Find the lantern map",
            Description = "Follow the paper lantern.",
            Status = QuestStatus.InProgress,
            AdventurePlanJson = JsonSerializer.Serialize(new AdventureDraftContent(
                "Find the lantern map",
                "A lantern carries a map.",
                AdventureArcType.Standalone,
                null,
                "A paper lantern floats into the meadow.",
                [new AdventureScene("The meadow", "Look around.", "Mira")],
                ["Follow the lantern."],
                [new AdventureClue("Paper boat", "A map is folded into a boat.")],
                new AdventureDraftNpc("Mira", "The friendly mapmaker.", "Eager to help."),
                "The map reveals a hidden garden.",
                "The party shares a picnic."),
                AdventureDraftJson.Options)
        };
        var campaign = new Campaign
        {
            Name = "Moonlit Woods",
            Quests = [activeQuest]
        };

        var response = CampaignResponse.From(campaign);
        var currentQuest = Assert.IsType<QuestResponse>(response.CurrentQuest);

        Assert.Equal("A paper lantern floats into the meadow.", currentQuest.AdventureOpening!.Opening);
        Assert.Equal("Mira", currentQuest.AdventureOpening.FeaturedNpc!.Name);
        Assert.Equal("The friendly mapmaker.", currentQuest.AdventureOpening.FeaturedNpc.Description);
        var json = JsonSerializer.Serialize(currentQuest);
        Assert.DoesNotContain("hidden garden", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("party shares a picnic", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompletedQuestDoesNotExposeItsAdventureOpening()
    {
        var quest = new Quest
        {
            Title = "Past adventure",
            Description = "Already completed.",
            Status = QuestStatus.Completed,
            AdventurePlanJson = JsonSerializer.Serialize(new AdventureDraftContent(
                "Past adventure",
                "A completed story.",
                AdventureArcType.Standalone,
                null,
                "An old opening.",
                [],
                [],
                [],
                new AdventureDraftNpc("Mira", "An old character.", "Friendly."),
                "An old finale.",
                "An old reward."),
                AdventureDraftJson.Options)
        };

        var response = QuestResponse.From(quest);

        Assert.Null(response.AdventureOpening);
    }
}
