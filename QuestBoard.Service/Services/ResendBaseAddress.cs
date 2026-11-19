using Microsoft.Extensions.Hosting;

namespace QuestBoard.Service.Services;

/// <summary>
/// Decides where the Resend statistics client sends its requests. The configured address is honoured only on a
/// Development host, so no other environment can be pointed away from the real service by a stray setting.
/// </summary>
public static class ResendBaseAddress
{
    public static Uri Resolve(IHostEnvironment environment, string? configuredBaseUrl, Uri fixedAddress)
    {
        if (!environment.IsDevelopment() || string.IsNullOrWhiteSpace(configuredBaseUrl))
            return fixedAddress;

        var parsed = Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var configured)
                     && (configured.Scheme == Uri.UriSchemeHttp || configured.Scheme == Uri.UriSchemeHttps);

        return parsed ? configured! : fixedAddress;
    }
}
