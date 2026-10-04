using System.Text.RegularExpressions;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public static class AdventureDraftValidator
{
    private static readonly Regex UnsafeContent = new(
        @"\b(gore|gory|bloody|blood|torture|tortured|murder|murdered|killer|killed|kill|death|dead|died|die|corpse|suicide|sexual|sex|drugs?|alcohol|abuse|cruelty|cruel|violence|violent|beheaded|dismembered|massacre|execution|strangled|rape|erotic|nude|naked|mutilated|slaughter|mature themes)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MandatoryCombat = new(
        @"\b(must|have to|need to|required to|only way is to)\s+(fight|battle|attack|combat|use (a )?weapon|defeat)\b|\bmandatory tactical combat\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(
        AdventureDraftContent? content,
        IReadOnlyCollection<string>? campaignNpcNames = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (content is null)
        {
            errors["adventure"] = ["An adventure plan is required."];
            return errors;
        }

        ValidateText(errors, "title", content.Title, 120);
        ValidateText(errors, "premise", content.Premise, 800);
        if (!Enum.IsDefined(content.ArcType))
        {
            errors["arcType"] = ["Choose whether this adventure stands alone or advances a season arc."];
        }

        ValidateOptionalText(errors, "arcConnection", content.ArcConnection, 600);
        if (content.ArcType == AdventureArcType.SeasonArc && string.IsNullOrWhiteSpace(content.ArcConnection))
        {
            errors["arcConnection"] = ["A season-arc adventure must explain how it advances the larger story."];
        }

        ValidateText(errors, "opening", content.Opening, 800);
        if (content.Scenes is not { Count: >= 2 and <= 4 })
        {
            errors["scenes"] = ["An adventure must include 2 to 4 scenes."];
        }
        else
        {
            for (var index = 0; index < content.Scenes.Count; index++)
            {
                var scene = content.Scenes[index];
                ValidateText(errors, $"scenes[{index}].title", scene?.Title, 120);
                ValidateText(errors, $"scenes[{index}].description", scene?.Description, 800);
                ValidateOptionalText(errors, $"scenes[{index}].npcName", scene?.NpcName, 100);
            }

            ValidateUnique(errors, "scenes", content.Scenes
                .Where(scene => !string.IsNullOrWhiteSpace(scene?.Title))
                .Select(scene => scene!.Title!));
        }

        ValidateStringList(errors, "solutionPaths", content.SolutionPaths, 2, 4, 400);
        if (content.SolutionPaths is not null)
        {
            ValidateUnique(errors, "solutionPaths", content.SolutionPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!));
        }
        if (content.Clues is not { Count: >= 2 and <= 5 })
        {
            errors["clues"] = ["An adventure must include 2 to 5 clues."];
        }
        else
        {
            for (var index = 0; index < content.Clues.Count; index++)
            {
                ValidateText(errors, $"clues[{index}].title", content.Clues[index]?.Title, 120);
                ValidateText(errors, $"clues[{index}].description", content.Clues[index]?.Description, 400);
            }

            ValidateUnique(errors, "clues", content.Clues
                .Where(clue => !string.IsNullOrWhiteSpace(clue?.Title))
                .Select(clue => clue!.Title!));
        }

        var npc = content.FeaturedNpc;
        ValidateText(errors, "featuredNpc.name", npc?.Name, 100);
        ValidateText(errors, "featuredNpc.description", npc?.Description, 500);
        ValidateText(errors, "featuredNpc.disposition", npc?.Disposition, 240);
        if (npc?.Name is not null && campaignNpcNames is not null &&
            campaignNpcNames.Contains(npc.Name.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors["featuredNpc.name"] = ["The featured character must be new to this campaign."];
        }

        if (content.Scenes is not null && npc?.Name is not null)
        {
            var sceneNpcNames = content.Scenes
                .Where(scene => !string.IsNullOrWhiteSpace(scene?.NpcName))
                .Select(scene => scene!.NpcName!.Trim())
                .ToArray();
            if (!sceneNpcNames.Contains(npc.Name.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                errors["featuredNpc.name"] = ["At least one scene must use the featured character."];
            }

            var knownNames = new HashSet<string>(
                campaignNpcNames ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase) { npc.Name.Trim() };
            if (sceneNpcNames.Any(name => !knownNames.Contains(name)))
            {
                errors["scenes.npcName"] = ["Scenes may only use characters from this campaign or the featured character."];
            }
        }

        ValidateText(errors, "finale", content.Finale, 800);
        ValidateText(errors, "celebrationReward", content.CelebrationReward, 400);

        var text = GetText(content);
        if (UnsafeContent.IsMatch(text) || MandatoryCombat.IsMatch(text))
        {
            errors["safety"] = ["The adventure includes content outside Storykeeper's child-safety boundaries."];
        }

        if (text.Length > 12000)
        {
            errors["adventure"] = ["The complete adventure plan is too long."];
        }

        return errors;
    }

    public static AdventureDraftContent Normalize(AdventureDraftContent content) => content with
    {
        Title = content.Title!.Trim(),
        Premise = content.Premise!.Trim(),
        ArcConnection = string.IsNullOrWhiteSpace(content.ArcConnection) ? null : content.ArcConnection.Trim(),
        Opening = content.Opening!.Trim(),
        Scenes = content.Scenes!.Select(scene => new AdventureScene(
            scene!.Title!.Trim(),
            scene.Description!.Trim(),
            string.IsNullOrWhiteSpace(scene.NpcName) ? null : scene.NpcName.Trim())).ToArray(),
        SolutionPaths = content.SolutionPaths!.Select(path => path!.Trim()).ToArray(),
        Clues = content.Clues!.Select(clue => new AdventureClue(
            clue!.Title!.Trim(),
            clue.Description!.Trim())).ToArray(),
        FeaturedNpc = new AdventureDraftNpc(
            content.FeaturedNpc!.Name!.Trim(),
            content.FeaturedNpc.Description!.Trim(),
            content.FeaturedNpc.Disposition!.Trim()),
        Finale = content.Finale!.Trim(),
        CelebrationReward = content.CelebrationReward!.Trim()
    };

    private static void ValidateText(IDictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["This field is required."];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"This field cannot exceed {maxLength} characters."];
        }
    }

    private static void ValidateOptionalText(IDictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"This field cannot exceed {maxLength} characters."];
        }
    }

    private static void ValidateStringList(
        IDictionary<string, string[]> errors,
        string field,
        IReadOnlyList<string?>? values,
        int minimum,
        int maximum,
        int itemLength)
    {
        if (values is null || values.Count < minimum || values.Count > maximum ||
            values.Any(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > itemLength))
        {
            errors[field] = [$"Include {minimum} to {maximum} non-empty items, each up to {itemLength} characters."];
        }
    }

    private static void ValidateUnique(IDictionary<string, string[]> errors, string field, IEnumerable<string> values)
    {
        if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count())
        {
            errors[field] = ["Names must be unique."];
        }
    }

    public static string GetText(AdventureDraftContent content)
    {
        var values = new List<string?>
        {
            content.Title, content.Premise, content.ArcConnection, content.Opening,
            content.Finale, content.CelebrationReward,
            content.FeaturedNpc?.Name, content.FeaturedNpc?.Description, content.FeaturedNpc?.Disposition
        };
        values.AddRange(content.SolutionPaths ?? []);
        foreach (var scene in content.Scenes ?? [])
        {
            values.Add(scene?.Title);
            values.Add(scene?.Description);
            values.Add(scene?.NpcName);
        }

        foreach (var clue in content.Clues ?? [])
        {
            values.Add(clue?.Title);
            values.Add(clue?.Description);
        }

        return string.Join('\n', values);
    }
}
