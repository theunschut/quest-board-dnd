using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// How two themed error responses are compared. The page carries a Log out form, so its body holds a
/// per-session request-verification token and the first response to a client also sets the antiforgery
/// cookie. Neither says anything about which record was asked for, so a comparison first primes each
/// client's cookie with a throwaway navigation and masks every token value before comparing.
/// </summary>
public static class ErrorPageComparison
{
    private const string MaskedToken = "[masked-token]";

    // Headers that legitimately differ between any two responses and say nothing about existence.
    private static readonly HashSet<string> VolatileHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Date",
        "Request-Id",
        "traceparent"
    };

    private static readonly Regex TokenValue = new(
        "(name=\"__RequestVerificationToken\"[^>]*?value=\")[^\"]*(\")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// One throwaway navigation so the client holds the antiforgery cookie before either response being
    /// compared is made; without it only the first response would carry a Set-Cookie header.
    /// </summary>
    public static async Task PrimeAsync(params HttpClient[] clients)
    {
        foreach (var client in clients)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/Error/404");
            request.Headers.TryAddWithoutValidation("Accept", "text/html");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>The body with every request-verification token value replaced by a fixed marker.</summary>
    public static string MaskToken(string body) => TokenValue.Replace(body, "$1" + MaskedToken + "$2");

    public static Dictionary<string, string> ComparableHeaders(HttpResponseMessage response)
    {
        return response.Headers
            .Concat(response.Content.Headers)
            .Where(h => !VolatileHeaderNames.Contains(h.Key))
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Status, redirect target, masked body and every header that is not time or tracing plumbing.</summary>
    public static async Task AssertIndistinguishableAsync(HttpResponseMessage first, HttpResponseMessage second)
    {
        first.StatusCode.Should().Be(second.StatusCode);
        first.Headers.Location.Should().Be(second.Headers.Location);

        var firstBody = MaskToken(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var secondBody = MaskToken(await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        firstBody.Should().Be(secondBody);

        ComparableHeaders(first).Should().BeEquivalentTo(ComparableHeaders(second));
    }

    /// <summary>The same comparison for bodies already read as strings.</summary>
    public static void AssertSameBody(string first, string second) =>
        MaskToken(first).Should().Be(MaskToken(second));
}
