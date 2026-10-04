using System.Text.RegularExpressions;

namespace Storykeeper.Api.Services;

public static class SafetyContentFilter
{
    public static bool ContainsExcludedContent(string text, IEnumerable<string>? excludedContent)
    {
        if (excludedContent is null)
        {
            return false;
        }

        return excludedContent
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Any(item =>
            {
                var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(item).Replace(@"\ ", @"\s+")}(?![\p{{L}}\p{{N}}])";
                return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            });
    }
}
