using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the Dungeon Master profile renders its contract on both layouts: one page title card with the photo or its
/// placeholder and the owner's edit action, About and Quest History as section cards with an h2, a finalized quest date
/// written by the wall-clock helper, Date TBD for an undated quest, and the empty states in the shared shape. The
/// desktop holds the quests in a table inside a responsive wrapper; the phone holds them as slips with one stretched
/// link each. The layout is chosen from the request's User-Agent, so every fact states which one it sends.
/// </summary>
public class DmProfileRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string PhoneUserAgent =
        "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    private const string LongQuestTitle =
        "The Unreasonably Long-Named Expedition Beneath the Seven Hollow Crossings and the Mist-Wrapped Weir";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    public static TheoryData<bool> BothLayouts => new() { false, true };

    private static string Tag(bool phone) => phone ? "phone" : "desktop";

    private static async Task<string> GetHtmlAsync(HttpClient client, string url, bool phone)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (phone)
            request.Headers.TryAddWithoutValidation("User-Agent", PhoneUserAgent);
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static string MainOf(string html)
    {
        var start = html.IndexOf("<main", StringComparison.Ordinal);
        var end = html.IndexOf("</main>", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        end.Should().BeGreaterThan(start);
        return html[start..end];
    }

    private sealed record Seeded(HttpClient Owner, int DmId);

    // A Dungeon Master with a bio and a photo, a finalized quest on 5 November 2026, one with a long name finalized on
    // 30 September 2026 and one that has no date, seen by that Dungeon Master.
    private async Task<Seeded> SeedFullProfileAsync(bool phone)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"dmprofile{Tag(phone)}", $"dmprofile{Tag(phone)}@example.com", roles: ["DungeonMaster"]);

        using (var scope = factory.Services.CreateScope())
        {
            var profiles = scope.ServiceProvider.GetRequiredService<IDungeonMasterProfileService>();
            await profiles.UpsertProfileAsync(
                dm.Id, bio: "I run **grim but fair** tables.", imageBytes: [1, 2, 3, 4], newCroppedImageData: [9, 9, 9, 9, 9],
                token: TestContext.Current.CancellationToken);
        }

        await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "The Lantern Gate Vigil", challengeRating: 4, isFinalized: true,
            finalizedDate: new DateTime(2026, 11, 5, 19, 0, 0));
        await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "The Unscheduled Embermarch", challengeRating: 4);
        await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, LongQuestTitle, challengeRating: 7, isFinalized: true,
            finalizedDate: new DateTime(2026, 9, 30, 19, 0, 0));
        return new Seeded(client, dm.Id);
    }

    private async Task<Seeded> SeedBareProfileAsync(bool phone)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"dmbare{Tag(phone)}", $"dmbare{Tag(phone)}@example.com", roles: ["DungeonMaster"]);
        return new Seeded(client, dm.Id);
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Profile_RendersTheCardsAndTheirSheets(bool phone)
    {
        var seeded = await SeedFullProfileAsync(phone);

        var html = await GetHtmlAsync(seeded.Owner, $"/DungeonMaster/Profile/{seeded.DmId}", phone);
        var main = MainOf(html);

        if (phone)
            html.Should().Contain("mobile-layout");
        else
            html.Should().NotContain("mobile-layout");

        Regex.Matches(main, @"<h1\b").Should().HaveCount(1, "the page has exactly one title");
        main.Should().Contain("dm-profile-page");
        main.Should().NotContain("container mt-4", "the layout already supplies the container");
        Regex.Matches(main, @"<h2\b[^>]*>[^<]*(?:<i\b[^>]*></i>)?\s*About").Should().HaveCount(1);
        Regex.Matches(main, @"<h2\b[^>]*>\s*(?:<i\b[^>]*></i>)?\s*Quest History").Should().HaveCount(1);
        Regex.Matches(main, @"modern-card--aside").Count.Should().BeGreaterThanOrEqualTo(2, "About and Quest History are section cards");

        // Each layout links its own layered sheet from the Styles section and never the other's.
        if (phone)
        {
            html.Should().MatchRegex(@"<link[^>]*href=""/css/dm-profile\.mobile\.css");
            html.Should().NotMatchRegex(@"<link[^>]*href=""/css/dm-profile\.css");
        }
        else
        {
            html.Should().MatchRegex(@"<link[^>]*href=""/css/dm-profile\.css");
            html.Should().NotContain("dm-profile.mobile.css");
        }

        main.Should().NotMatchRegex(@"\sstyle\s*=", "a profile carries no inline style");
        main.Should().NotMatchRegex(@"\son[a-z]+\s*=", "a profile carries no inline event handler");
        main.Should().NotContain("text-muted fst-italic", "the empty bio line is not styled per page");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Profile_ShowsThePhotoAndTheMarkdownBio(bool phone)
    {
        var seeded = await SeedFullProfileAsync(phone);

        var main = MainOf(await GetHtmlAsync(seeded.Owner, $"/DungeonMaster/Profile/{seeded.DmId}", phone));

        main.Should().MatchRegex(@"<img[^>]*class=""[^""]*\bdm-profile-photo\b[^""]*""[^>]*/DungeonMaster/GetDMProfilePicture/" + seeded.DmId);
        main.Should().NotContain("dm-profile-placeholder");
        main.Should().Contain("<strong>grim but fair</strong>");
        main.Should().NotContain("No bio provided yet.");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Profile_TheOwnerSeesEditProfileAsTheCautionButton(bool phone)
    {
        var seeded = await SeedBareProfileAsync(phone);

        var main = MainOf(await GetHtmlAsync(seeded.Owner, $"/DungeonMaster/Profile/{seeded.DmId}", phone));

        main.Should().MatchRegex(
            @"<a [^>]*href=""/DungeonMaster/EditProfile/" + seeded.DmId + @"""[^>]*class=""btn btn-warning[^""]*""[^>]*>\s*<i[^>]*></i>Edit Profile\s*</a>|<a [^>]*class=""btn btn-warning[^""]*""[^>]*href=""/DungeonMaster/EditProfile/" + seeded.DmId + @"""[^>]*>\s*<i[^>]*></i>Edit Profile\s*</a>");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Profile_AnotherMemberSeesNoEditAction(bool phone)
    {
        var seeded = await SeedBareProfileAsync(phone);
        var (other, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"dmprofileother{Tag(phone)}", $"dmprofileother{Tag(phone)}@example.com");

        var main = MainOf(await GetHtmlAsync(other, $"/DungeonMaster/Profile/{seeded.DmId}", phone));

        main.Should().NotContain("Edit Profile");
        main.Should().NotContain("btn-warning");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Profile_WritesAFinalizedDateThroughTheWallClockHelper(bool phone)
    {
        var seeded = await SeedFullProfileAsync(phone);

        var main = MainOf(await GetHtmlAsync(seeded.Owner, $"/DungeonMaster/Profile/{seeded.DmId}", phone));

        main.Should().Contain(
            "<time class=\"wall-clock\" data-style=\"date\" datetime=\"2026-11-05T19:00\">Nov 5, 2026</time>",
            "a finalized date is a floating wall-clock value written by the helper, never converted");
        main.Should().Contain(">Sep 30, 2026</time>", "the month abbreviation is the helper's own, not a hand-written one");
        main.Should().NotContain("Nov 05, 2026", "the leading zero of the old format is gone");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Profile_ShowsDateTbdForAnUndatedQuest(bool phone)
    {
        var seeded = await SeedFullProfileAsync(phone);

        var main = MainOf(await GetHtmlAsync(seeded.Owner, $"/DungeonMaster/Profile/{seeded.DmId}", phone));

        Regex.Matches(main, "Date TBD").Should().HaveCount(1, "only the undated quest has no date");
    }

    [Fact]
    public async Task DesktopProfile_HoldsTheQuestsInAResponsiveTableWithReadableLinks()
    {
        var seeded = await SeedFullProfileAsync(phone: false);

        var main = MainOf(await GetHtmlAsync(seeded.Owner, $"/DungeonMaster/Profile/{seeded.DmId}", phone: false));

        main.Should().MatchRegex(@"<div class=""table-responsive"">\s*<table class=""table table-striped table-hover"">");
        Regex.Matches(main, @"<th scope=""col"">(?<name>[^<]+)</th>").Select(m => m.Groups["name"].Value)
            .Should().Equal("Title", "Date", "Difficulty");
        Regex.Matches(main, @"<tbody>[\s\S]*?</tbody>").Single().Value
            .Should().Contain("<span class=\"badge bg-secondary\">CR 4</span>");
        main.Should().Contain(">The Lantern Gate Vigil</a>", "the quest title is a link in the table");
        main.Should().Contain(LongQuestTitle, "a long quest name is shown whole, never truncated");
        main.Should().NotMatchRegex(@"text-truncate|text-overflow", "nothing truncates a name");
    }

    [Fact]
    public async Task PhoneProfile_HoldsEachQuestAsASlipWithOneStretchedLink()
    {
        var seeded = await SeedFullProfileAsync(phone: true);

        var main = MainOf(await GetHtmlAsync(seeded.Owner, $"/DungeonMaster/Profile/{seeded.DmId}", phone: true));

        var slips = Regex.Split(main, @"(?=<div class=""dm-quest-row[ ""])").Skip(1).ToList();
        slips.Should().HaveCount(3, "each quest is one slip");
        foreach (var slip in slips)
        {
            Regex.Matches(slip, @"<a class=""row-nav-link""").Should().HaveCount(1, "a slip is one stretched link");
            slip.Should().Contain("href=\"/Quest/Details/");
            slip.Should().Contain("badge bg-secondary");
        }

        main.Should().Contain(LongQuestTitle, "a long quest name is shown whole, never truncated");
        main.Should().NotContain("<table", "the phone holds slips, not a table");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Profile_WithoutAPhotoBioOrQuests_RendersThePlaceholderAndTheEmptyStates(bool phone)
    {
        var seeded = await SeedBareProfileAsync(phone);

        var main = MainOf(await GetHtmlAsync(seeded.Owner, $"/DungeonMaster/Profile/{seeded.DmId}", phone));

        main.Should().Contain("dm-profile-placeholder");
        main.Should().NotContain("<img");
        main.Should().Contain("No bio provided yet.");
        main.Should().MatchRegex(
            @"<div class=""empty-state"">\s*<i class=""fas fa-dragon empty-state-icon"" aria-hidden=""true""></i>\s*<h2 class=""empty-state-heading"">No Quests Yet</h2>\s*<p class=""empty-state-text"">This Dungeon Master hasn(?:'|&#x27;)t run any quests yet\.</p>");
        main.Should().NotContain("<table");
        main.Should().NotContain("Date TBD");
    }
}
