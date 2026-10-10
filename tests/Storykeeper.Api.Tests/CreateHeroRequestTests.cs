using Storykeeper.Api.Contracts;
using Xunit;

namespace Storykeeper.Api.Tests;

public sealed class CreateHeroRequestTests
{
    [Fact]
    public void ValidatesAndAcceptsSafeHeroDetails()
    {
        var errors = CreateHeroRequestValidator.Validate(new CreateHeroRequest(
            "Pip",
            "A curious young explorer.",
            "Scout",
            ["Noticing tiny details", "Kindness"]));

        Assert.Empty(errors);
    }

    [Fact]
    public void RejectsMissingDuplicateOversizedAndUnsafeHeroDetails()
    {
        var errors = CreateHeroRequestValidator.Validate(new CreateHeroRequest(
            " ",
            "A brave explorer.",
            "Scout",
            ["Kindness", " kindness ", "A".PadRight(101, 'A')]));

        Assert.Contains("name", errors.Keys);
        Assert.Contains("strengths[2]", errors.Keys);
        Assert.Contains("strengths", errors.Keys);

        var unsafeErrors = CreateHeroRequestValidator.Validate(new CreateHeroRequest(
            "Pip",
            "A curious explorer.",
            "Killer",
            []));

        Assert.Contains("safety", unsafeErrors.Keys);
    }
}
