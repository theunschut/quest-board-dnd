using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// End-to-end proof that the group picker itself -- not the middleware -- skips straight to the
/// page when the link that sent a board-less viewer there already names one board they belong
/// to, and otherwise renders today's picker exactly as it always has.
/// </summary>
public class CrossBoardPickerSkipTests(CrossBoardWebApplicationFactory factory)
    : IClassFixture<CrossBoardWebApplicationFactory>
{
    // Clean slate, two boards the viewer belongs to (Board A = group 1, Board B = a new group),
    // one quest seeded on each, and no board selected -- the starting state every fact in this
    // class needs.
    private async Task<(HttpClient client, int boardAQuestId, int boardBQuestId, int boardBId)> SeedTwoBoardViewerAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"pickerskipdm{suffix}", $"pickerskipdm{suffix}@example.com");

        int boardBId, boardAQuestId, boardBQuestId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"PickerSkipBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBId = boardB.Id;

            var boardAQuest = new QuestEntity
            {
                Title = $"PickerSkipBoardAQuest{suffix}",
                Description = "Lives on Board A.",
                GroupId = 1,
                DungeonMasterId = dm.Id,
                ChallengeRating = 1,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            };
            var boardBQuest = new QuestEntity
            {
                Title = $"PickerSkipBoardBQuest{suffix}",
                Description = "Lives on Board B.",
                GroupId = boardBId,
                DungeonMasterId = dm.Id,
                ChallengeRating = 1,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Quests.Add(boardAQuest);
            ctx.Quests.Add(boardBQuest);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardAQuestId = boardAQuest.Id;
            boardBQuestId = boardBQuest.Id;
        }

        // roles: ["Player"] seeds Board A (group 1) membership; Board B is added explicitly below.
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"pickerskipviewer{suffix}", $"pickerskipviewer{suffix}@example.com", roles: ["Player"]);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.UserGroups.Add(new UserGroupEntity { UserId = user.Id, GroupId = boardBId, GroupRole = (int)GroupRole.Player });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return (client, boardAQuestId, boardBQuestId, boardBId);
    }

    [Fact]
    public async Task TwoBoardViewer_ReturnUrlNamingOtherBoardsQuest_RedirectsStraightToQuest()
    {
        var (client, _, boardBQuestId, _) = await SeedTwoBoardViewerAsync("1");
        var returnUrl = $"/Quest/Details/{boardBQuestId}";

        // Act — a GET of the picker carrying the return URL, no board selected.
        var response = await client.GetAsync($"/GroupPicker/Index?returnUrl={Uri.EscapeDataString(returnUrl)}", TestContext.Current.CancellationToken);

        // Assert — never sees the picker; redirects straight to the return URL.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().Be(returnUrl);

        // Act — following that redirect lands on the quest page with the switch banner.
        var landed = await client.GetAsync(returnUrl, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("PickerSkipBoardBQuest1");
        body.Should().Contain("board-switch-toast");
        body.Should().Contain("PickerSkipBoardB1");
    }

    [Fact]
    public async Task TwoBoardViewer_LandingBannerHasNoSwitchBackForm()
    {
        var (client, _, boardBQuestId, _) = await SeedTwoBoardViewerAsync("2");
        var returnUrl = $"/Quest/Details/{boardBQuestId}";

        await client.GetAsync($"/GroupPicker/Index?returnUrl={Uri.EscapeDataString(returnUrl)}", TestContext.Current.CancellationToken);
        var landed = await client.GetAsync(returnUrl, TestContext.Current.CancellationToken);
        var body = await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert — the banner exists, but nothing from it onward in the page renders a form:
        // there is no previous board this path could offer a way back to. The toast partial is
        // the last thing the shared layout renders before scripts, so the remainder of the body
        // from this point on is exactly the banner's own markup.
        var toastStart = body.IndexOf("board-switch-toast", StringComparison.Ordinal);
        toastStart.Should().BeGreaterThanOrEqualTo(0);
        var toastOnward = body[toastStart..];
        toastOnward.Should().NotContain("<form");
        toastOnward.Should().NotContain("__RequestVerificationToken");
    }

    [Fact]
    public async Task TwoBoardViewer_NoReturnUrl_RendersTodaysPicker()
    {
        var (client, _, _, _) = await SeedTwoBoardViewerAsync("3");

        var response = await client.GetAsync("/GroupPicker/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Select Your Group");
    }

    [Fact]
    public async Task TwoBoardViewer_ReturnUrlNamingUnregisteredRoute_RendersTodaysPicker()
    {
        var (client, _, _, _) = await SeedTwoBoardViewerAsync("4");

        // The quest list route participates in no registry entry -- it is not a detail route.
        var response = await client.GetAsync($"/GroupPicker/Index?returnUrl={Uri.EscapeDataString("/quests")}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Select Your Group");
    }

    // One test, two requests, one assertion set: a non-member's entity and a nonexistent id must
    // be indistinguishable, or the picker becomes a membership/existence oracle. The viewer must
    // belong to at least two boards -- otherwise a viewer with exactly one membership would fall
    // through to the pre-existing single-board auto-select branch instead of rendering a picker
    // at all, and this fact would no longer be testing what it claims to.
    [Fact]
    public async Task NonMemberReturnUrl_AndNonexistentReturnUrl_ProduceIndistinguishablePickerResponses()
    {
        // Seeds Board A (group 1) and Board B, both memberships, plus one quest on each -- only
        // the third board below is deliberately left out of the viewer's memberships.
        var (client, _, _, _) = await SeedTwoBoardViewerAsync("oracle");

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "pickerskipdmoracle", "pickerskipdmoracle@example.com");

        int thirdBoardQuestId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var thirdBoard = new GroupEntity { Name = "PickerSkipThirdBoard", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(thirdBoard);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

            var thirdBoardQuest = new QuestEntity
            {
                Title = "PickerSkipThirdBoardQuest",
                Description = "Lives on a board the viewer is not a member of.",
                GroupId = thirdBoard.Id,
                DungeonMasterId = dm.Id,
                ChallengeRating = 1,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Quests.Add(thirdBoardQuest);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            thirdBoardQuestId = thirdBoardQuest.Id;
        }

        var nonMemberResponse = await client.GetAsync(
            $"/GroupPicker/Index?returnUrl={Uri.EscapeDataString($"/Quest/Details/{thirdBoardQuestId}")}", TestContext.Current.CancellationToken);
        var nonexistentResponse = await client.GetAsync(
            $"/GroupPicker/Index?returnUrl={Uri.EscapeDataString($"/Quest/Details/{int.MaxValue}")}", TestContext.Current.CancellationToken);

        nonMemberResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        nonMemberResponse.StatusCode.Should().Be(nonexistentResponse.StatusCode);
        nonMemberResponse.Content.Headers.ContentType.Should().BeEquivalentTo(nonexistentResponse.Content.Headers.ContentType);

        var nonMemberBody = await nonMemberResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var nonexistentBody = await nonexistentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Two volatile-but-harmless fields must be normalized before comparing: the antiforgery
        // token is freshly random on every render regardless of membership, and the picker's
        // SelectGroup forms echo the caller's own returnUrl verbatim -- both requests deliberately
        // carry a different id, so that echo differs no matter what the server does. Neither field
        // reveals anything about which board is real; what this fact actually checks is that
        // everything the server itself decides -- status, content type, and the rest of the
        // markup -- is identical between a non-member's real entity and one that never existed.
        nonMemberBody = NormalizeVolatileFields(nonMemberBody);
        nonexistentBody = NormalizeVolatileFields(nonexistentBody);
        nonMemberBody.Should().Be(nonexistentBody);
    }

    private static string NormalizeVolatileFields(string html)
    {
        html = Regex.Replace(html, "(name=\"__RequestVerificationToken\" type=\"hidden\" value=\")[^\"]*(\")", "$1$2");
        html = Regex.Replace(html, "(name=\"returnUrl\" value=\")[^\"]*(\")", "$1$2");
        return html;
    }

    [Fact]
    public async Task SuperAdmin_WithResolvableReturnUrl_StillReceivesPicker()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "pickerskipdmsuper", "pickerskipdmsuper@example.com");

        int questId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var quest = new QuestEntity
            {
                Title = "PickerSkipSuperAdminQuest",
                Description = "Any board's quest.",
                GroupId = 1,
                DungeonMasterId = dm.Id,
                ChallengeRating = 1,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Quests.Add(quest);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            questId = quest.Id;
        }

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

        var response = await client.GetAsync(
            $"/GroupPicker/Index?returnUrl={Uri.EscapeDataString($"/Quest/Details/{questId}")}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Go to Platform");
    }

    [Fact]
    public async Task SingleBoardViewer_ReturnUrlNamingOwnBoardsQuest_StillEndsUpOnQuest()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "pickerskipdmsingle", "pickerskipdmsingle@example.com");

        int questId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var quest = new QuestEntity
            {
                Title = "PickerSkipSingleBoardQuest",
                Description = "Lives on the viewer's only board.",
                GroupId = 1,
                DungeonMasterId = dm.Id,
                ChallengeRating = 1,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Quests.Add(quest);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            questId = quest.Id;
        }

        // roles: ["Player"] seeds membership on group 1 only -- a genuine single-board viewer.
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "pickerskipviewersingle", "pickerskipviewersingle@example.com", roles: ["Player"]);
        var returnUrl = $"/Quest/Details/{questId}";

        var response = await client.GetAsync($"/GroupPicker/Index?returnUrl={Uri.EscapeDataString(returnUrl)}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().Be(returnUrl);

        var landed = await client.GetAsync(returnUrl, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("PickerSkipSingleBoardQuest");
    }
}
