namespace Storykeeper.Api.Services;

internal static class TextToSpeechEndpoint
{
    public static bool TryCreate(string value, bool allowHttp, out Uri endpoint)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) &&
            string.IsNullOrEmpty(parsed.UserInfo) &&
            (parsed.Scheme == Uri.UriSchemeHttps ||
             (allowHttp && parsed.Scheme == Uri.UriSchemeHttp)))
        {
            endpoint = parsed;
            return true;
        }

        endpoint = null!;
        return false;
    }
}
