namespace QuestBoard.Service.Helpers;

// Builds the two address forms a calendar subscription is offered in. Both are built from the
// configured application address, never from the current request's scheme or host: this
// application's reverse-proxy configuration forwards only the client address, not the scheme or
// host, so a request-derived base would publish a broken address on this deployment today. The
// configured address is already proven correct in production by every existing email link that
// uses it the same way.
//
// The setting lives under the email-settings section (IOptions<EmailSettings>.AppUrl), which
// reads oddly for a helper that has nothing to do with email -- that naming smell is a known,
// deliberately deferred cleanup elsewhere, not a mistake made here. It is reused as-is because it
// is the one base address already proven correct for this deployment.
public static class CalendarSubscriptionAddress
{
    public static string BuildHttps(string appUrl, string feedToken)
    {
        var trimmedAppUrl = appUrl.TrimEnd('/');
        return $"{trimmedAppUrl}/feeds/calendar/{feedToken}.ics";
    }

    public static string BuildWebcal(string appUrl, string feedToken)
    {
        var httpsAddress = BuildHttps(appUrl, feedToken);

        if (httpsAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "webcal://" + httpsAddress["https://".Length..];
        }

        if (httpsAddress.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return "webcal://" + httpsAddress["http://".Length..];
        }

        // The configured address carries neither scheme -- prefix rather than produce a
        // scheme-less address that no calendar client could resolve.
        return "webcal://" + httpsAddress;
    }
}
