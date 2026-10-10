using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Storykeeper.Api.Services;

internal static partial class OpenAiCompatibleProviderDiagnostics
{
    private const int MaximumErrorBodyBytes = 8_192;
    private const int MaximumMessageLength = 400;
    private const int MaximumRequestIdLength = 100;

    public static bool ShouldIncludeTemperature(Uri endpoint, double? temperature) =>
        temperature.HasValue && !IsAnthropicEndpoint(endpoint);

    public static bool ShouldIncludeResponseFormat(Uri endpoint) =>
        !IsAnthropicEndpoint(endpoint);

    public static bool IsLoopbackEndpoint(Uri endpoint) => endpoint.IsLoopback;

    public static async Task<(string? Message, string? RequestId)> ReadAsync(
        HttpResponseMessage response,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var body = await ReadBoundedBodyAsync(response.Content, cancellationToken);
        var message = ReadJsonString(body, "error", "message") ??
            ReadJsonString(body, "error") ??
            ReadJsonString(body, "message");
        var requestId = ReadHeader(response, "request-id") ??
            ReadHeader(response, "anthropic-request-id") ??
            ReadHeader(response, "x-request-id") ??
            ReadJsonString(body, "request_id") ??
            ReadJsonString(body, "requestId");

        return (SanitizeMessage(message, apiKey), SanitizeRequestId(requestId, apiKey));
    }

    private static bool IsAnthropicEndpoint(Uri endpoint) =>
        endpoint.Host.Equals("api.anthropic.com", StringComparison.OrdinalIgnoreCase) ||
        endpoint.Host.EndsWith(".anthropic.com", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> ReadBoundedBodyAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaximumErrorBodyBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    private static string? ReadHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static string? ReadJsonString(string body, params string[] path)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 8 });
            var value = document.RootElement;
            foreach (var property in path)
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty(property, out value))
                {
                    return null;
                }
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? SanitizeMessage(string? message, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            message = message.Replace(apiKey.Trim(), "[redacted]", StringComparison.Ordinal);
        }

        message = CredentialAssignmentRegex().Replace(message, "$1=[redacted]");
        message = BearerCredentialRegex().Replace(message, "Bearer [redacted]");
        message = ProviderKeyRegex().Replace(message, "[redacted]");
        var sanitized = WhitespaceRegex().Replace(
            new string(message.Select(character => char.IsControl(character) ? ' ' : character).ToArray()),
            " ").Trim();

        return sanitized.Length <= MaximumMessageLength
            ? sanitized
            : sanitized[..MaximumMessageLength] + "...";
    }

    private static string? SanitizeRequestId(string? requestId, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            requestId = requestId.Replace(apiKey.Trim(), "[redacted]", StringComparison.Ordinal);
        }

        var safeId = new string(requestId
            .Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            .Take(MaximumRequestIdLength)
            .ToArray());
        return safeId.Length == 0 ? null : safeId;
    }

    [GeneratedRegex("""(?i)\b(api[_ -]?key|access[_ -]?token|authorization)\b\s*[:=]\s*(?:bearer\s+)?["']?[^\s,"'}]+""")]
    private static partial Regex CredentialAssignmentRegex();

    [GeneratedRegex("""(?i)\bbearer\s+[A-Za-z0-9._~+/=-]+""")]
    private static partial Regex BearerCredentialRegex();

    [GeneratedRegex("""(?i)\bsk-(?:ant-)?[A-Za-z0-9_-]{8,}\b""")]
    private static partial Regex ProviderKeyRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
