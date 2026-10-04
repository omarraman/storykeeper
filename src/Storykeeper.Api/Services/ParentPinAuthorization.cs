using System.Security.Cryptography;
using System.Text;

namespace Storykeeper.Api.Services;

public static class ParentPinAuthorization
{
    public const string HeaderName = "X-Parent-Pin";

    public static bool IsConfigured(IConfiguration configuration)
    {
        var pin = configuration["Storykeeper:ParentPin"];
        return pin is { Length: >= 6 and <= 64 } && !string.IsNullOrWhiteSpace(pin);
    }

    public static bool IsAuthorized(HttpRequest request, IConfiguration configuration)
    {
        var configuredPin = configuration["Storykeeper:ParentPin"];
        var suppliedPin = request.Headers[HeaderName].ToString();
        if (!IsConfigured(configuration) || configuredPin is null || string.IsNullOrEmpty(suppliedPin))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(configuredPin)),
            SHA256.HashData(Encoding.UTF8.GetBytes(suppliedPin)));
    }
}
