using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the answers given for the pages of the characters, contacts and players areas that keep a separate phone
/// view: the headings the phone views gained, the markers they show and the dates they word. The layout is chosen
/// from the request's User-Agent header, so every fact states which one it sends.
/// </summary>
public class CharactersTwinAnswersTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string PhoneUserAgent =
        "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    public async ValueTask InitializeAsync() => await TestDataHelper.ClearDatabaseAsync(factory.Services);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<string> GetPhoneAsync(HttpClient client, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", PhoneUserAgent);
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PhoneManage_HasThePageHeading()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "twinanswers_manage_dm", "twinanswers_manage_dm@example.com", roles: ["DungeonMaster"]);

        var html = await GetPhoneAsync(client, "/ContactCategoryManagement");

        html.Should().Contain("mobile-layout");
        Regex.Matches(html, @"<h1\b").Should().HaveCount(1, "the phone page has one page title");
        Regex.IsMatch(html, @"<h1\b[^>]*>\s*Manage Categories\s*</h1>").Should().BeTrue(
            "the phone page carries the same title words as the desktop page");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("edit")]
    public async Task PhoneCharacterForms_MarkClassesRequired(string form)
    {
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"twinanswers_classes_{form}", $"twinanswers_classes_{form}@example.com");

        var url = "/Characters/Create";
        if (form == "edit")
        {
            var character = await TestDataHelper.CreateTestCharacterAsync(factory.Services, user.Id, "Required Marker Hero");
            url = $"/Characters/Edit/{character.Id}";
        }

        var html = await GetPhoneAsync(client, url);

        html.Should().Contain("mobile-layout");
        Regex.IsMatch(
            html,
            @"<label[^>]*>\s*Classes <span class=""text-danger"" aria-hidden=""true"">\*</span><span class=""visually-hidden""> \(required\)</span></label>")
            .Should().BeTrue("the phone form marks Classes as required, as the desktop form does");
    }
}
