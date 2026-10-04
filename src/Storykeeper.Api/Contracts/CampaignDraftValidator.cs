using System.Text.RegularExpressions;
using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public static class CampaignDraftValidator
{
    private static readonly Regex UnsafeContent = new(
        @"\b(gore|gory|bloody|blood|torture|tortured|murder|murdered|killer|killed|kill|death|dead|died|die|corpse|suicide|sexual|sex|drugs?|alcohol|abuse|cruelty|cruel|violence|violent|beheaded|dismembered|massacre|execution|strangled|rape|erotic|nude|naked|mutilated|slaughter|mature themes)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MandatoryCombat = new(
        @"\b(must|have to|need to|required to|only way is to)\s+(fight|battle|attack|combat|use (a )?weapon|defeat)\b|\bmandatory tactical combat\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(CampaignDraftContent? content)
    {
        var errors = new Dictionary<string, string[]>();
        if (content is null)
        {
            errors["draft"] = ["A campaign draft is required."];
            return errors;
        }

        ValidateText(errors, "title", content.Title, 120);
        ValidateText(errors, "premise", content.Premise, 2000);
        ValidateText(errors, "centralMystery", content.CentralMystery, 1000);
        ValidateStringList(errors, "worldRules", content.WorldRules, 3, 6, 240);

        if (content.Npcs is not { Count: >= 3 and <= 5 })
        {
            errors["npcs"] = ["A campaign draft must include 3 to 5 recurring NPCs."];
        }
        else
        {
            for (var index = 0; index < content.Npcs.Count; index++)
            {
                var npc = content.Npcs[index];
                ValidateText(errors, $"npcs[{index}].name", npc?.Name, 100);
                ValidateText(errors, $"npcs[{index}].description", npc?.Description, 500);
                ValidateText(errors, $"npcs[{index}].disposition", npc?.Disposition, 240);
                ValidateOptionalText(errors, $"npcs[{index}].locationName", npc?.LocationName, 100);
            }

            ValidateUnique(errors, "npcs", content.Npcs.Where(npc => !string.IsNullOrWhiteSpace(npc?.Name)).Select(npc => npc!.Name!));
            var locationNames = (content.Locations ?? [])
                .Where(location => !string.IsNullOrWhiteSpace(location?.Name))
                .Select(location => location!.Name!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (content.Npcs.Any(npc => !string.IsNullOrWhiteSpace(npc?.LocationName) &&
                                        !locationNames.Contains(npc.LocationName)))
            {
                errors["npcs.locationName"] = ["NPC locations must match one of the draft locations."];
            }
        }

        if (content.Locations is not { Count: >= 3 and <= 6 })
        {
            errors["locations"] = ["A campaign draft must include 3 to 6 locations."];
        }
        else
        {
            for (var index = 0; index < content.Locations.Count; index++)
            {
                var location = content.Locations[index];
                ValidateText(errors, $"locations[{index}].name", location?.Name, 100);
                ValidateText(errors, $"locations[{index}].description", location?.Description, 500);
            }

            ValidateUnique(errors, "locations", content.Locations.Where(location => !string.IsNullOrWhiteSpace(location?.Name)).Select(location => location!.Name!));
        }

        if (content.AdventureHooks is not { Count: >= 1 and <= 4 })
        {
            errors["adventureHooks"] = ["A campaign draft must include 1 to 4 adventure hooks."];
        }
        else
        {
            for (var index = 0; index < content.AdventureHooks.Count; index++)
            {
                var hook = content.AdventureHooks[index];
                ValidateText(errors, $"adventureHooks[{index}].title", hook?.Title, 120);
                ValidateText(errors, $"adventureHooks[{index}].description", hook?.Description, 600);
            }

            ValidateUnique(errors, "adventureHooks",
                content.AdventureHooks.Where(hook => !string.IsNullOrWhiteSpace(hook?.Title)).Select(hook => hook!.Title!));
        }

        if (content.Safety is not
            {
                LowFright: true,
                NoGoreOrCruelty: true,
                NoMatureThemes: true,
                NoPermanentCharacterDeath: true,
                NoMandatoryTacticalCombat: true
            })
        {
            errors["safety"] = ["The draft must meet every required child-safety boundary."];
        }

        var text = GetText(content);
        if (!IsSafeGeneratedText(text))
        {
            errors["safety"] = ["The draft includes content outside Storykeeper's child-safety boundaries."];
        }

        if (text.Length > 18000)
        {
            errors["draft"] = ["The complete campaign draft is too long."];
        }

        return errors;
    }

    public static CampaignDraftContent Normalize(CampaignDraftContent content) => new(
        content.Title!.Trim(),
        content.Premise!.Trim(),
        content.CentralMystery!.Trim(),
        content.WorldRules!.Select(rule => rule!.Trim()).ToArray(),
        content.Npcs!.Select(npc => new CampaignDraftNpc(
            npc!.Name!.Trim(),
            npc.Description!.Trim(),
            npc.Disposition!.Trim(),
            string.IsNullOrWhiteSpace(npc.LocationName) ? null : npc.LocationName.Trim())).ToArray(),
        content.Locations!.Select(location => new CampaignDraftLocation(
            location!.Name!.Trim(),
            location.Description!.Trim())).ToArray(),
        content.AdventureHooks!.Select(hook => new CampaignDraftHook(
            hook!.Title!.Trim(),
            hook.Description!.Trim())).ToArray(),
        content.Safety);

    public static bool IsSafeGeneratedText(string text) =>
        !UnsafeContent.IsMatch(text) && !MandatoryCombat.IsMatch(text);

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

    public static string GetText(CampaignDraftContent content)
    {
        var values = new List<string?> { content.Title, content.Premise, content.CentralMystery };
        values.AddRange(content.WorldRules ?? []);
        foreach (var npc in content.Npcs ?? [])
        {
            values.Add(npc?.Name);
            values.Add(npc?.Description);
            values.Add(npc?.Disposition);
            values.Add(npc?.LocationName);
        }

        foreach (var location in content.Locations ?? [])
        {
            values.Add(location?.Name);
            values.Add(location?.Description);
        }

        foreach (var hook in content.AdventureHooks ?? [])
        {
            values.Add(hook?.Title);
            values.Add(hook?.Description);
        }

        return string.Join('\n', values);
    }
}
