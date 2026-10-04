using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Xunit;

namespace Storykeeper.Api.Tests;

public sealed class ParentControlsTests
{
    [Fact]
    public void ParentSettingsRejectOutOfRangeValuesAndOversizedExclusionLists()
    {
        var settings = ParentSafetySettings.Defaults with
        {
            MaxNarrationWords = 151,
            SessionLengthMinutes = 181,
            ExcludedContent = Enumerable.Range(1, 21).Select(index => $"Topic {index}").ToList()
        };

        var errors = ParentControlsRequestValidator.Validate(settings);

        Assert.Contains("maxNarrationWords", errors.Keys);
        Assert.Contains("sessionLengthMinutes", errors.Keys);
        Assert.Contains("excludedContent", errors.Keys);
    }

    [Fact]
    public void ParentPinIsRequiredAndComparedWithoutExposingItsValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storykeeper:ParentPin"] = "246810" })
            .Build();
        var request = new DefaultHttpContext().Request;

        Assert.True(ParentPinAuthorization.IsConfigured(configuration));
        Assert.False(ParentPinAuthorization.IsAuthorized(request, configuration));
        request.Headers[ParentPinAuthorization.HeaderName] = "111111";
        Assert.False(ParentPinAuthorization.IsAuthorized(request, configuration));
        request.Headers[ParentPinAuthorization.HeaderName] = "246810";
        Assert.True(ParentPinAuthorization.IsAuthorized(request, configuration));
    }

    [Fact]
    public void LiveNarrationEnforcesParentWordLimitAndExcludedContent()
    {
        var text = string.Join(' ', Enumerable.Repeat("gentle", 41)) + " map.";
        var turn = new StoryTurnContent(
            text,
            null,
            [],
            null,
            [new StoryTurnChoice("continue", "Continue the adventure.")],
            []);
        var settings = ParentSafetySettings.Defaults with
        {
            MaxNarrationWords = 40,
            ExcludedContent = ["map"]
        };

        var errors = StoryTurnContentValidator.Validate(turn, [], null, settings);

        Assert.Contains("narration", errors.Keys);
        Assert.Contains("safetySettings.excludedContent", errors.Keys);
    }

    [Fact]
    public void ExcludedTopicMatchingUsesWordBoundariesAndSupportsPhrases()
    {
        Assert.True(SafetyContentFilter.ContainsExcludedContent("An ice cave appeared.", ["ice cave"]));
        Assert.False(SafetyContentFilter.ContainsExcludedContent("A nice day began.", ["ice"]));
    }
}
