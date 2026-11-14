using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the character Create and Edit forms are one view each for both layouts: the same title card, picture
/// block, required markers, class rows, crop dialog and action row under the phone and the desktop User-Agent, class
/// rows that come from one server-rendered template with a contiguous index, and a current picture that is requested
/// only when one is stored. The layout is chosen from the request's User-Agent, so every fact states which one it
/// sends.
/// </summary>
public class CharacterFormRenderTests(WebApplicationFactoryBase factory)
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

    public static TheoryData<bool> BothLayouts => new() { false, true };

    public static TheoryData<string, bool> EveryFormUnderBothLayouts() =>
        new()
        {
            { "create", false },
            { "create", true },
            { "edit", false },
            { "edit", true }
        };

    private sealed record Seeded(HttpClient Client, int CharacterId, string Url);

    private async Task<Seeded> SeedAsync(string form, bool mobile, int classCount = 1, bool withPicture = false)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var tag = $"{form}{Tag(mobile)}{classCount}{(withPicture ? "pic" : "nopic")}";
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"charform{tag}", $"charform{tag}@example.com");

        if (form == "create")
            return new Seeded(client, 0, "/Characters/Create");

        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, user.Id, "Form Hero", level: 6, groupId: 1);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

            // The helper adds one class; this brings the character to the requested number (none included).
            var existing = context.CharacterClasses.Where(c => c.CharacterId == character.Id).ToList();
            context.CharacterClasses.RemoveRange(existing);
            for (var i = 0; i < classCount; i++)
            {
                context.CharacterClasses.Add(new CharacterClassEntity
                {
                    CharacterId = character.Id,
                    Class = 5 + i,
                    ClassLevel = 2 + i
                });
            }

            if (withPicture)
            {
                context.Set<CharacterImageEntity>().Add(new CharacterImageEntity
                {
                    Id = character.Id,
                    OriginalImageData = [1, 2, 3, 4],
                    CroppedImageData = [9, 9, 9, 9, 9]
                });
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return new Seeded(client, character.Id, $"/Characters/Edit/{character.Id}");
    }

    private static async Task<string> GetHtmlAsync(Seeded seeded, bool mobile)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, seeded.Url);
        request.Headers.TryAddWithoutValidation("User-Agent", AgentFor(mobile));
        var response = await seeded.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{seeded.Url} must render");
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // The page with the class-row template removed, so facts about the rows on the page are not read from the
    // template that cloning copies.
    private static string WithoutTemplate(string html) =>
        Regex.Replace(html, @"<template\b[\s\S]*?</template>", " ");

    // The page content between the main landmark's tags, without the shell around it.
    private static string MainOf(string html)
    {
        var start = html.IndexOf("<main", StringComparison.Ordinal);
        var end = html.IndexOf("</main>", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        end.Should().BeGreaterThan(start);
        return html[start..end];
    }

    private static string TemplateOf(string html)
    {
        var match = Regex.Match(html, @"<template id=""class-row-template"">(?<body>[\s\S]*?)</template>");
        match.Success.Should().BeTrue("the page renders the class row template");
        return match.Groups["body"].Value;
    }

    [Theory]
    [MemberData(nameof(EveryFormUnderBothLayouts))]
    public async Task Form_RendersOneTemplateTheModuleAndNoInlineClassRowScript(string form, bool mobile)
    {
        var seeded = await SeedAsync(form, mobile);

        var html = await GetHtmlAsync(seeded, mobile);

        Regex.Matches(html, @"<template id=""class-row-template""").Should().HaveCount(1);
        var template = TemplateOf(html);
        template.Should().Contain("name=\"Classes[__index__].Class\"");
        template.Should().Contain("name=\"Classes[__index__].ClassLevel\"");
        template.Should().Contain("aria-label=\"Class\"");
        template.Should().Contain("aria-label=\"Class level\"");
        template.Should().Contain("btn btn-danger", "a row added later can be removed");

        html.Should().MatchRegex(@"<script type=""module"" src=""/js/character-form\.js");
        html.Should().NotContain("classIndex");
        html.Should().NotContain("newEntry");
        html.Should().NotContain("add-class').addEventListener");
    }

    [Theory]
    [MemberData(nameof(EveryFormUnderBothLayouts))]
    public async Task Form_RendersTheSameContractUnderBothUserAgents(string form, bool mobile)
    {
        var seeded = await SeedAsync(form, mobile);

        var html = await GetHtmlAsync(seeded, mobile);

        if (mobile)
            html.Should().Contain("mobile-layout");
        else
            html.Should().NotContain("mobile-layout");

        Regex.Matches(html, @"<h1\b").Should().HaveCount(1, "a page has exactly one title");
        html.Should().Contain("character-form-page form-page");
        html.Should().NotContain("character-form-card", "the phone card wrapper is gone with the phone view");
        html.Should().NotContain("character-form.mobile.css");
        html.Should().MatchRegex(@"<link[^>]*href=""/css/character-form\.css");
        html.Should().MatchRegex(@"<link[^>]*href=""/css/image-crop\.css");
        html.Should().Contain("id=\"cropPhotoModal\"", "the shared crop dialog is on the page");
        html.Should().Contain("data-crop-input");

        // Both editors, in the page's order, and the form's own fields.
        html.Should().MatchRegex(@"<textarea[^>]*name=""Description""[^>]*markdown-editor-textarea");
        html.Should().MatchRegex(@"<textarea[^>]*name=""Backstory""[^>]*markdown-editor-textarea");

        // Help text is a .form-text; nothing on the form is faded with the muted utility.
        MainOf(html).Should().NotContain("text-muted");

        // Both layouts get Create's placeholders.
        html.Should().Contain("placeholder=\"Enter character name\"");
        html.Should().Contain("placeholder=\"https://...\"");
    }

    [Theory]
    [MemberData(nameof(EveryFormUnderBothLayouts))]
    public async Task Form_MarksNameLevelAndClassesRequiredWithSpokenText(string form, bool mobile)
    {
        var seeded = await SeedAsync(form, mobile);

        var html = await GetHtmlAsync(seeded, mobile);

        foreach (var label in new[] { "Character Name", "Level", "Classes" })
        {
            html.Should().MatchRegex(
                @"<label[^>]*>\s*" + Regex.Escape(label) +
                @" <span class=""text-danger"" aria-hidden=""true"">\*</span><span class=""visually-hidden""> \(required\)</span></label>",
                $"{label} carries the required marker with a hidden spoken word");
        }
    }

    [Theory]
    [MemberData(nameof(EveryFormUnderBothLayouts))]
    public async Task Form_ActionRowHasCancelFirstAndTheCommitAsPrimary(string form, bool mobile)
    {
        var seeded = await SeedAsync(form, mobile);

        var html = await GetHtmlAsync(seeded, mobile);

        var commit = form == "create" ? "Create Character" : "Save Changes";
        var stack = form == "create" ? " form-actions--stack" : string.Empty;
        html.Should().MatchRegex(
            @"<div class=""form-actions" + stack + @""">\s*<a [^>]*class=""btn btn-secondary""[^>]*>\s*<i[^>]*></i>Cancel\s*</a>\s*<button type=""submit"" class=""btn btn-primary"">\s*<i[^>]*></i>" + commit + @"\s*</button>");
        html.Should().NotContain("btn btn-warning\">", "the commit is the primary action, not the caution colour");

        if (form == "create")
            html.Should().MatchRegex(@"<a href=""/Characters""[^>]*class=""btn btn-secondary""");
        else
            html.Should().MatchRegex(@"<a href=""/Characters/Details/" + seeded.CharacterId + @"""[^>]*class=""btn btn-secondary""");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Create_RendersOneClassRowWithNoRemoveAndNoPictureLeftovers(bool mobile)
    {
        var seeded = await SeedAsync("create", mobile);

        var html = await GetHtmlAsync(seeded, mobile);
        var page = WithoutTemplate(html);

        Regex.Matches(page, @"name=""Classes\[\d+\]\.Class""").Should().HaveCount(1);
        page.Should().Contain("name=\"Classes[0].Class\"");
        page.Should().NotContain("remove-class", "the first row cannot be removed");
        page.Should().Contain("Create New Character");

        // A new character has no picture: no thumbnail, no picture address, no re-crop button.
        var content = MainOf(html);
        content.Should().NotContain("GetCroppedPicture");
        content.Should().NotContain("GetProfilePicture");
        content.Should().NotContain("recropPhotoBtn");
        content.Should().NotContain("Re-crop Photo");
        content.Should().NotContain("onerror");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Edit_WithTwoClasses_RendersTwoNamedRowsAndRemoveOnlyOnTheSecond(bool mobile)
    {
        var seeded = await SeedAsync("edit", mobile, classCount: 2);

        var html = await GetHtmlAsync(seeded, mobile);
        var page = WithoutTemplate(html);

        page.Should().Contain("name=\"Classes[0].Class\"");
        page.Should().Contain("name=\"Classes[1].Class\"");
        page.Should().NotContain("name=\"Classes[2]");
        page.Should().Contain("name=\"Classes[1].ClassLevel\"");
        Regex.Matches(page, @"class=""btn btn-danger[^""]*remove-class").Should().HaveCount(1);
        page.Should().Contain("Edit Form Hero");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Edit_WithNoStoredClasses_RendersOneRowStartingAtIndexZero(bool mobile)
    {
        var seeded = await SeedAsync("edit", mobile, classCount: 0);

        var html = await GetHtmlAsync(seeded, mobile);
        var page = WithoutTemplate(html);

        Regex.Matches(page, @"name=""Classes\[\d+\]\.Class""").Select(m => m.Value).Should()
            .Equal("name=\"Classes[0].Class\"");
        page.Should().NotContain("remove-class");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Edit_WithAPicture_ShowsTheThumbnailAsAClassWithReCrop(bool mobile)
    {
        var seeded = await SeedAsync("edit", mobile, withPicture: true);

        var html = await GetHtmlAsync(seeded, mobile);

        var image = Regex.Match(html, @"<img[^>]*src=""/Characters/GetCroppedPicture/" + seeded.CharacterId + @"""[^>]*>");
        image.Success.Should().BeTrue("the stored picture is shown");
        image.Value.Should().Contain("form-picture-thumb");
        image.Value.Should().NotContain("style=");
        html.Should().MatchRegex(
            @"<button type=""button"" class=""btn btn-sm btn-warning[^""]*"" id=""recropPhotoBtn"" data-crop-original-url=""/Characters/GetProfilePicture/" + seeded.CharacterId + @"""");
        html.Should().Contain("Re-crop Photo");
        MainOf(html).Should().NotContain("onerror");

        // The thumbnail sits under the file input and its hint, never above the input.
        html.IndexOf("id=\"profilePictureInput\"", StringComparison.Ordinal).Should()
            .BeLessThan(html.IndexOf("GetCroppedPicture", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Edit_WithoutAPicture_RequestsNoPictureAndOffersNoReCrop(bool mobile)
    {
        var seeded = await SeedAsync("edit", mobile, withPicture: false);

        var html = await GetHtmlAsync(seeded, mobile);

        var content = MainOf(html);
        content.Should().NotContain("GetCroppedPicture/" + seeded.CharacterId);
        content.Should().NotContain("GetProfilePicture/" + seeded.CharacterId);
        content.Should().NotContain("recropPhotoBtn");
        content.Should().NotContain("Re-crop Photo");
        content.Should().NotContain("onerror");
        content.Should().NotContain("alt=\"Current\"");
    }
}
