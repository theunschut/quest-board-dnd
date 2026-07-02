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
}
