namespace Storykeeper.Api.Contracts;

public sealed record CreateHeroRequest(
    string? Name,
    string? Description,
    string? Role,
    IReadOnlyList<string?>? Strengths);

public static class CreateHeroRequestValidator
{
    public static Dictionary<string, string[]> Validate(CreateHeroRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["hero"] = ["Hero details are required."];
            return errors;
        }

        ValidateRequiredText(errors, "name", request.Name, 120);
        ValidateRequiredText(errors, "description", request.Description, 500);
        ValidateRequiredText(errors, "role", request.Role, 80);

        if (request.Strengths is { Count: > 6 })
        {
            errors["strengths"] = ["Choose up to 6 strengths."];
        }
        else if (request.Strengths is not null)
        {
            for (var index = 0; index < request.Strengths.Count; index++)
            {
                var strength = request.Strengths[index];
                if (string.IsNullOrWhiteSpace(strength) || strength.Trim().Length > 100)
                {
                    errors[$"strengths[{index}]"] = ["Each strength must be non-empty and no longer than 100 characters."];
                }
            }

            var strengths = request.Strengths.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToArray();
            if (strengths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != strengths.Length)
            {
                errors["strengths"] = ["Strengths must be unique."];
            }
        }

        var text = string.Join('\n', new[] { request.Name, request.Description, request.Role }
            .Concat(request.Strengths ?? []));
        if (!CampaignDraftValidator.IsSafeGeneratedText(text))
        {
            errors["safety"] = ["Hero details must follow Storykeeper's child-safety boundaries."];
        }

        return errors;
    }

    private static void ValidateRequiredText(
        IDictionary<string, string[]> errors,
        string field,
        string? value,
        int maxLength)
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
}
