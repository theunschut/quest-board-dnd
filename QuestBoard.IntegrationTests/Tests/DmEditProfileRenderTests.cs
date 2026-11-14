using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the Dungeon Master profile form is one view for both layouts: the phone and the desktop User-Agent get
/// the same title card, picture block, bio editor, crop dialog and action row, the current photo is a class rather
/// than an inline size, and a refused submission lists every error (field errors as well as model-level ones) in the
/// validation summary. The layout is chosen from the request's User-Agent, so every fact states which one it sends.
/// </summary>
public class DmEditProfileRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    private static string AgentFor(bool mobile) => mobile ? MobileUserAgent : DesktopUserAgent;

    private static string Tag(bool mobile) => mobile ? "phone" : "desktop";

    private async Task<(HttpClient Client, int Id)> SeedDungeonMasterAsync(bool mobile, bool withPhoto)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"dmeditprofile{Tag(mobile)}", $"dmeditprofile{Tag(mobile)}@example.com", roles: ["DungeonMaster"]);

        if (withPhoto)
        {
            using var scope = factory.Services.CreateScope();
            var profiles = scope.ServiceProvider.GetRequiredService<IDungeonMasterProfileService>();
            await profiles.UpsertProfileAsync(
                user.Id, bio: "I run grim but fair tables.", imageBytes: [1, 2, 3, 4], newCroppedImageData: [9, 9, 9, 9, 9],
                token: TestContext.Current.CancellationToken);
        }

        return (client, user.Id);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, bool mobile)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", AgentFor(mobile));
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static TheoryData<bool> BothLayouts => new() { false, true };

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task EditProfile_RendersTheSameFormUnderBothUserAgents(bool mobile)
    {
        var (client, id) = await SeedDungeonMasterAsync(mobile, withPhoto: false);

        var response = await GetAsync(client, $"/DungeonMaster/EditProfile/{id}", mobile);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        if (mobile)
            html.Should().Contain("mobile-layout");
        else
            html.Should().NotContain("mobile-layout");

        Regex.Matches(html, @"<h1\b").Should().HaveCount(1, "a page has exactly one title");
        html.Should().Contain("Edit DM Profile");
        html.Should().Contain("form-page");
        html.Should().NotContain("dm-editprofile-card-mobile", "the phone card wrapper is gone with the phone view");
        html.Should().NotContain("dm-editprofile.mobile.css");
        html.Should().MatchRegex(@"<link[^>]*href=""/css/dm-profile-form\.css");
        html.Should().MatchRegex(@"<link[^>]*href=""/css/image-crop\.css");

        // The picture block, the bio editor and the shared crop dialog are all on the page.
        html.Should().Contain("id=\"dmProfilePictureInput\"");
        html.Should().Contain("data-crop-input");
        html.Should().MatchRegex(@"<input type=""file"" name=""CroppedPictureFile"" hidden");
        html.Should().MatchRegex(@"<textarea[^>]*name=""Bio""[^>]*markdown-editor-textarea");
        html.Should().Contain("id=\"cropPhotoModal\"");

        // The exit comes first in the markup and the one commit follows as btn-primary; the label is long enough
        // to need the stacked fallback on a phone.
        html.Should().MatchRegex(
            @"<div class=""form-actions form-actions--stack"">\s*<a [^>]*class=""btn btn-secondary""[^>]*>\s*<i[^>]*></i>Back to Profile\s*</a>\s*<button type=""submit"" class=""btn btn-primary"">\s*<i[^>]*></i>Save Profile\s*</button>");
        html.Should().NotContain("btn btn-warning\">", "the commit is the primary action, not the caution colour");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task EditProfile_WithAPhoto_ShowsItAsAClassWithAReCropButton(bool mobile)
    {
        var (client, id) = await SeedDungeonMasterAsync(mobile, withPhoto: true);

        var response = await GetAsync(client, $"/DungeonMaster/EditProfile/{id}", mobile);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var image = Regex.Match(html, @"<img[^>]*alt=""Current profile photo""[^>]*>");
        image.Success.Should().BeTrue("the current photo is shown");
        image.Value.Should().Contain("class=\"rounded-circle form-photo-round\"");
        image.Value.Should().NotContain("style=");
        html.Should().NotContain("dm-profile-photo", "the form no longer shares a class the profile sheet styles");

        html.Should().MatchRegex(@"<button type=""button"" class=""btn btn-sm btn-warning[^""]*"" id=""recropPhotoBtn"" data-crop-original-url=""/DungeonMaster/GetOriginalDMProfilePicture/");
        html.Should().Contain("Re-crop Photo");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task EditProfile_WithoutAPhoto_OffersNoReCrop(bool mobile)
    {
        var (client, id) = await SeedDungeonMasterAsync(mobile, withPhoto: false);

        var response = await GetAsync(client, $"/DungeonMaster/EditProfile/{id}", mobile);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().NotContain("recropPhotoBtn");
        html.Should().NotContain("alt=\"Current profile photo\"");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task EditProfile_RefusedSubmission_SummaryListsTheFieldErrors(bool mobile)
    {
        var (client, id) = await SeedDungeonMasterAsync(mobile, withPhoto: false);

        var getResponse = await GetAsync(client, $"/DungeonMaster/EditProfile/{id}", mobile);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var form = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(new Dictionary<string, string>
        {
            ["DungeonMasterId"] = id.ToString(),
            ["Bio"] = new string('x', 2001)
        }, token);
        var post = new HttpRequestMessage(HttpMethod.Post, $"/DungeonMaster/EditProfile/{id}") { Content = form };
        post.Headers.TryAddWithoutValidation("User-Agent", AgentFor(mobile));
        var response = await client.SendAsync(post, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "a refused submission shows the form again");
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var summary = Regex.Match(html, @"<div[^>]*class=""[^""]*\bvalidation-summary-errors\b[^""]*""[^>]*>(?<body>.*?)</div>", RegexOptions.Singleline);
        summary.Success.Should().BeTrue("the summary is shown when any field is refused, not only for model-level errors");
        summary.Groups["body"].Value.Should().Contain("Bio cannot exceed 2000 characters");
        html.Should().NotContain("validation-summary-valid", "the summary is not the empty valid placeholder");
    }
}
