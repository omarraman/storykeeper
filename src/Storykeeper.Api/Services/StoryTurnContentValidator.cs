using Storykeeper.Api.Contracts;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Services;

public static class StoryTurnContentValidator
{
    private static readonly HashSet<string> FactCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "clue", "world", "npc", "quest", "party", "location", "object",
        "promise", "thread", "relationship", "reward", "other"
    };

    public static Dictionary<string, string[]> Validate(
        StoryTurnContent? content,
        IReadOnlyCollection<Npc> campaignNpcs,
        Hero? selectedHero,
        ParentSafetySettings? safetySettings = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (content is null)
        {
            errors["turn"] = ["The Storykeeper returned an empty response."];
            return errors;
        }

        ValidateText(errors, "narration", content.Narration, 1200);
        var narrationLimit = safetySettings?.MaxNarrationWords ?? 120;
        if (content.Narration is not null &&
            content.Narration.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > narrationLimit)
        {
            errors["narration"] = [$"Narration must be no longer than {narrationLimit} words."];
        }

        ValidateOptionalText(errors, "speaker", content.Speaker, 100);
        if (content.NpcDialogue is not { Count: <= 3 } ||
            content.NpcDialogue.Any(dialogue => dialogue is null))
        {
            errors["npcDialogue"] = ["Include no more than three valid NPC dialogue lines."];
        }
        else
        {
            for (var index = 0; index < content.NpcDialogue.Count; index++)
            {
                var dialogue = content.NpcDialogue[index]!;
                ValidateText(errors, $"npcDialogue[{index}].npcName", dialogue.NpcName, 100);
                ValidateText(errors, $"npcDialogue[{index}].text", dialogue.Text, 300);
                if (!string.IsNullOrWhiteSpace(dialogue.NpcName) &&
                    !campaignNpcs.Any(npc => string.Equals(npc.Name, dialogue.NpcName.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    errors[$"npcDialogue[{index}].npcName"] = ["Dialogue can only be attributed to an NPC in this campaign."];
                }
            }
        }

        if (content.Choices is not { Count: >= 1 and <= 4 } ||
            content.Choices.Any(choice => choice is null))
        {
            errors["choices"] = ["Include one to four valid suggested choices; players may also enter their own action."];
        }
        else
        {
            for (var index = 0; index < content.Choices.Count; index++)
            {
                ValidateText(errors, $"choices[{index}].id", content.Choices[index]!.Id, 80);
                ValidateText(errors, $"choices[{index}].text", content.Choices[index]!.Text, 200);
            }

            var ids = content.Choices.Where(choice => !string.IsNullOrWhiteSpace(choice?.Id))
                .Select(choice => choice!.Id!.Trim());
            if (ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Count())
            {
                errors["choices"] = ["Suggested choice identifiers must be unique."];
            }
        }

        if (content.ProposedFacts is not { Count: <= 3 } ||
            content.ProposedFacts.Any(fact => fact is null))
        {
            errors["proposedFacts"] = ["Include no more than three valid proposed facts."];
        }
        else
        {
            for (var index = 0; index < content.ProposedFacts.Count; index++)
            {
                var fact = content.ProposedFacts[index]!;
                if (string.IsNullOrWhiteSpace(fact.Category) || !FactCategories.Contains(fact.Category.Trim()))
                {
                    errors[$"proposedFacts[{index}].category"] = ["Choose a supported fact category."];
                }

                ValidateText(errors, $"proposedFacts[{index}].statement", fact.Statement, 2000);
                if (fact.Importance is < 1 or > 5)
                {
                    errors[$"proposedFacts[{index}].importance"] = ["Importance must be between 1 and 5."];
                }
            }
        }

        if (content.RollRequest is { } roll)
        {
            ValidateText(errors, "rollRequest.prompt", roll.Prompt, 300);
            if (!Enum.TryParse<CheckDifficulty>(roll.Difficulty, true, out var difficulty) ||
                !Enum.IsDefined(difficulty))
            {
                errors["rollRequest.difficulty"] = ["Choose an easy, tricky, or heroic difficulty."];
            }

            if (roll.Strength is not null &&
                (selectedHero is null || !selectedHero.Strengths.Any(strength =>
                    string.Equals(strength, roll.Strength.Trim(), StringComparison.OrdinalIgnoreCase))))
            {
                errors["rollRequest.strength"] = ["A requested strength must be listed for a campaign hero."];
            }
        }

        var generatedText = string.Join('\n',
            new[] { content.Narration, content.Speaker }
                .Concat(content.NpcDialogue?.SelectMany(item => new[] { item?.NpcName, item?.Text }) ?? [])
                .Concat(content.Choices?.SelectMany(item => new[] { item?.Text }) ?? [])
                .Concat(content.ProposedFacts?.Select(item => item?.Statement) ?? [])
                .Concat(content.RollRequest is null ? [] : [content.RollRequest.Prompt]));
        if (!CampaignDraftValidator.IsSafeGeneratedText(generatedText))
        {
            errors["safety"] = ["The response includes content outside Storykeeper's child-safety boundaries."];
        }
        else if (SafetyContentFilter.ContainsExcludedContent(generatedText, safetySettings?.ExcludedContent))
        {
            errors["safetySettings.excludedContent"] = ["The response includes a topic or creature excluded by the parent."];
        }

        return errors;
    }

    public static StoryTurnContent Normalize(StoryTurnContent content) => content with
    {
        Narration = content.Narration!.Trim(),
        Speaker = string.IsNullOrWhiteSpace(content.Speaker) ? null : content.Speaker.Trim(),
        NpcDialogue = content.NpcDialogue!.Select(dialogue => new StoryTurnDialogue(
            dialogue!.NpcName!.Trim(), dialogue.Text!.Trim())).ToArray(),
        Choices = content.Choices!.Select(choice => new StoryTurnChoice(
            choice!.Id!.Trim(), choice.Text!.Trim())).ToArray(),
        ProposedFacts = content.ProposedFacts!.Select(fact => new StoryTurnFactProposal(
            fact!.Category!.Trim().ToLowerInvariant(),
            fact.Statement!.Trim(),
            fact.Importance)).ToArray(),
        RollRequest = content.RollRequest is null ? null : content.RollRequest with
        {
            Prompt = content.RollRequest.Prompt!.Trim(),
            Difficulty = Enum.Parse<CheckDifficulty>(content.RollRequest.Difficulty!, true).ToString(),
            Strength = string.IsNullOrWhiteSpace(content.RollRequest.Strength)
                ? null
                : content.RollRequest.Strength.Trim()
        }
    };

    private static void ValidateText(IDictionary<string, string[]> errors, string field, string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["This field is required."];
        }
        else if (value.Trim().Length > maximum)
        {
            errors[field] = [$"This field cannot exceed {maximum} characters."];
        }
    }

    private static void ValidateOptionalText(IDictionary<string, string[]> errors, string field, string? value, int maximum)
    {
        if (value?.Trim().Length > maximum)
        {
            errors[field] = [$"This field cannot exceed {maximum} characters."];
        }
    }
}
