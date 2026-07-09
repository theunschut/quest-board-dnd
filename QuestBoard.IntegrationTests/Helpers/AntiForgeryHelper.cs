using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Helpers;

public static class AntiForgeryHelper
{
    public static async Task<(string Token, string CookieValue)> ExtractAntiForgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        // Extract the anti-forgery token from the form
        var tokenMatch = Regex.Match(content, @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        if (!tokenMatch.Success)
        {
            throw new Exception("Anti-forgery token not found in response");
        }

        var token = tokenMatch.Groups[1].Value;

        // Extract the cookie value
        var cookieValue = string.Empty;
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var antiForgeryTokenCookie = cookies.FirstOrDefault(c => c.Contains(".AspNetCore.Antiforgery"));
            if (antiForgeryTokenCookie != null)
            {
                var cookieMatch = Regex.Match(antiForgeryTokenCookie, @"\.AspNetCore\.Antiforgery\.[^=]+=([^;]+)");
                if (cookieMatch.Success)
                {
                    cookieValue = cookieMatch.Groups[1].Value;
                }
            }
        }

        return (token, cookieValue);
    }

    public static FormUrlEncodedContent CreateFormContentWithAntiForgeryToken(
        Dictionary<string, string> formData,
        string antiForgeryToken)
    {
        formData["__RequestVerificationToken"] = antiForgeryToken;
        return new FormUrlEncodedContent(formData);
    }

    /// <summary>
    /// Extracts a request-verification token from an authenticated GET response for use as the
    /// <c>RequestVerificationToken</c> HTTP header on a JSON POST -- this app's existing default
    /// header-based antiforgery convention (see <c>QuestController.RevokeSignup</c>/<c>RemovePlayerSignup</c>
    /// and <c>MarkdownController.Preview</c>). ASP.NET Core's <c>AntiforgeryTokenSet.RequestToken</c>
    /// value is the same regardless of whether it travels as a hidden form field or a header --
    /// only the transport differs, so this reuses <see cref="ExtractAntiForgeryTokenAsync"/>'s
    /// hidden-input extraction (every authenticated page in this app that renders a standard
    /// asp-action form already emits that hidden field) rather than a second parsing strategy.
    /// The returned cookie value is provided for callers using a fresh <see cref="HttpClient"/>
    /// with no shared cookie container; a client created with the default
    /// <c>WebApplicationFactoryClientOptions</c> (cookie handling enabled) already carries the
    /// antiforgery cookie automatically once it has issued the GET this method reads from.
    /// </summary>
    public static async Task<(string HeaderToken, string CookieValue)> ExtractHeaderAntiForgeryTokenAsync(HttpResponseMessage response)
    {
        var (token, cookieValue) = await ExtractAntiForgeryTokenAsync(response);
        return (token, cookieValue);
    }
}
