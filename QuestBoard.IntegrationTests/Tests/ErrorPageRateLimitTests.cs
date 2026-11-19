using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// A refused request keeps its 429 for every caller: a browser navigation sees the themed page with the same
/// sentence the plain response always carried, and any other caller keeps the sentence as its whole body. Each
/// class owns its host, so the forgot-password budget of three posts per window is never shared with a sibling.
/// </summary>
internal static class ForgotPasswordPoster
{
    internal const string Sentence = "Too many requests. Please try again later.";

    internal static async Task<List<(HttpStatusCode Status, string Body)>> PostFourTimesAsync(
        HttpClient client, Action<HttpRequestMessage> shape)
    {
        var results = new List<(HttpStatusCode, string)>();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var form = await client.GetAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);
            var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(form);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/ForgotPassword")
            {
                Content = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
                    new Dictionary<string, string> { ["Email"] = "nobody-here@example.com" }, token)
            };
            if (!string.IsNullOrEmpty(cookieValue))
                request.Headers.TryAddWithoutValidation("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
            shape(request);

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            results.Add((response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        }

        return results;
    }
}

public class ErrorPageRateLimitNavigationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task FourthForgotPasswordPost_AsANavigation_AnswersTheThemedTooManyRequestsPage()
    {
        var client = factory.CreateNonRedirectingClient();

        var results = await ForgotPasswordPoster.PostFourTimesAsync(client, request =>
        {
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        });

        results[3].Status.Should().Be(HttpStatusCode.TooManyRequests);
        results[3].Body.Should().Contain("Please wait a moment");
        results[3].Body.Should().Contain("Error 429");
        results[3].Body.Should().Contain(ForgotPasswordPoster.Sentence);
        results.Take(3).Should().OnlyContain(r => r.Status != HttpStatusCode.TooManyRequests);
    }
}

public class ErrorPageRateLimitScriptTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task FourthForgotPasswordPost_AsAScriptCaller_AnswersThePlainSentenceAsItsWholeBody()
    {
        var client = factory.CreateNonRedirectingClient();

        var results = await ForgotPasswordPoster.PostFourTimesAsync(client, request =>
        {
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        });

        results[3].Status.Should().Be(HttpStatusCode.TooManyRequests);
        results[3].Body.Should().Be(ForgotPasswordPoster.Sentence);
    }
}
