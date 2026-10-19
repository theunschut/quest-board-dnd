using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The move control on the Details pages and the toast after a move. Seeded: board 1 "Board
/// Alpha" (active) and board 2 "Board Bravo" linked to it (or left unlinked, or linked to
/// nothing, where a fact needs that), an open quest "Goblin Market" and an upcoming event
/// "Harvest Fair" on board 1. The control shows for a super admin on a linked board only,
/// enabled or disabled with a reason; the toast links to the entry only for a member of the
/// target.
///
/// What these facts do NOT establish: how the pages look. They check markup and wording, not
/// rendering, and the harness uses the EF Core InMemory provider.
/// </summary>
public class BoardMoveControlTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const int BoardAlpha = 1;
    private const int BoardBravo = 2;
    private const string ControlLabel = "Move to Linked Board";

    private static readonly DateTime[] UpcomingDates = [new(2031, 5, 10, 19, 0, 0), new(2031, 5, 11, 19, 0, 0)];
    private static readonly DateTime PlayedDate = new(2020, 5, 10, 19, 0, 0);
    private static readonly DateOnly UpcomingEventDate = new(2031, 5, 10);
    private static readonly DateOnly PastEventDate = new(2020, 5, 10);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    public enum Linking { LinkedPair, NoSet, SetOfOne }

    private record Scenario(HttpClient SuperAdminClient, int SuperAdminId, int DmId);

    private async Task<Scenario> SeedScenarioAsync(Linking linking = Linking.LinkedPair)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;

        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", BoardType.OneShot);
        switch (linking)
        {
            case Linking.LinkedPair:
                await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, BoardAlpha, BoardBravo);
                break;
            case Linking.SetOfOne:
                await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, BoardAlpha);
                break;
        }

        var dm = await BoardMoveSeed.CreateUserAsync(factory, "move_control_dm", "Dana the DM");
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardAlpha, GroupRole.DungeonMaster);
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardBravo, GroupRole.DungeonMaster);

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "move_control_super", "move_control_super@example.com");
        return new Scenario(client, user.Id, dm.Id);
    }

    private static async Task<(HttpStatusCode Status, string Html)> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, html);
    }

    private static string Decoded(string html) => WebUtility.HtmlDecode(html);

    private static Task<(HttpStatusCode Status, string Html)> QuestDetailsAsync(HttpClient client, int questId, string userAgent) =>
        GetAsync(client, $"/Quest/Details/{questId}", userAgent);

    private static Task<(HttpStatusCode Status, string Html)> EventDetailsAsync(HttpClient client, int eventId) =>
        GetAsync(client, $"/Events/Details/{eventId}", DesktopUserAgent);

    private Task<int> SeedOpenQuestAsync(int dmId, string title = "Goblin Market") =>
        BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, title, dmId, UpcomingDates);

    private static void ShouldShowDisabledControlWithReason(string html, string reason)
    {
        html.Should().Contain(ControlLabel);
        html.Should().MatchRegex("<button type=\"button\" class=\"btn btn-info w-100[^\"]*disabled\" aria-disabled=\"true\" aria-describedby=\"move-reason\"");
        html.Should().MatchRegex($"<p class=\"move-control-reason[^\"]*\" id=\"move-reason\">\\s*{Regex.Escape(reason)}\\s*</p>");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task SuperAdminOnALinkedBoard_SeesTheEnabledQuestControlLinkingTheMovePage(string userAgent)
    {
        var scenario = await SeedScenarioAsync();
        var questId = await SeedOpenQuestAsync(scenario.DmId);

        var (status, html) = await QuestDetailsAsync(scenario.SuperAdminClient, questId, userAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(ControlLabel);
        html.Should().Contain($"href=\"/Move/Quest/{questId}\"");
        html.Should().Contain("fa-right-left");
        html.Should().NotContain("move-control-reason");
    }

    [Fact]
    public async Task SuperAdminOnALinkedBoard_SeesTheEventControlLinkingTheMovePage()
    {
        var scenario = await SeedScenarioAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, "Harvest Fair", UpcomingEventDate);

        var (status, html) = await EventDetailsAsync(scenario.SuperAdminClient, eventId);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(ControlLabel);
        html.Should().Contain($"href=\"/Move/Event/{eventId}\"");
        html.Should().NotContain("move-control-reason");
    }

    [Theory]
    [InlineData("closed", "Closed in the Quest Log.", DesktopUserAgent)]
    [InlineData("closed", "Closed in the Quest Log.", MobileUserAgent)]
    [InlineData("played", "Already played.", DesktopUserAgent)]
    [InlineData("played", "Already played.", MobileUserAgent)]
    public async Task AQuestThatCannotMove_ShowsADisabledControlWithItsReason(string state, string reason, string userAgent)
    {
        var scenario = await SeedScenarioAsync();
        var questId = state == "closed"
            ? await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", scenario.DmId, UpcomingDates, isClosed: true)
            : await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", scenario.DmId, [PlayedDate], finalizedDate: PlayedDate);

        var (status, html) = await QuestDetailsAsync(scenario.SuperAdminClient, questId, userAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain($"href=\"/Move/Quest/{questId}\"");
        ShouldShowDisabledControlWithReason(html, reason);
    }

    [Theory]
    [InlineData("series", "Part of a recurring series. Detach it first.")]
    [InlineData("past", "Already in the past.")]
    public async Task AnEventThatCannotMove_ShowsADisabledControlWithItsReason(string state, string reason)
    {
        var scenario = await SeedScenarioAsync();
        int eventId;
        if (state == "series")
        {
            var seriesId = await BoardMoveSeed.SeedSeriesAsync(factory, BoardAlpha, "Weekly Fair", UpcomingEventDate);
            eventId = await BoardMoveSeed.SeedEventAsync(
                factory, BoardAlpha, "Harvest Fair", UpcomingEventDate, seriesId: seriesId, seriesSlotIndex: 0);
        }
        else
        {
            eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, "Harvest Fair", PastEventDate);
        }

        var (status, html) = await EventDetailsAsync(scenario.SuperAdminClient, eventId);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain($"href=\"/Move/Event/{eventId}\"");
        ShouldShowDisabledControlWithReason(html, reason);
    }

    [Theory]
    [InlineData(Linking.NoSet, DesktopUserAgent)]
    [InlineData(Linking.NoSet, MobileUserAgent)]
    [InlineData(Linking.SetOfOne, DesktopUserAgent)]
    [InlineData(Linking.SetOfOne, MobileUserAgent)]
    public async Task SuperAdminOnABoardOutsideAnySetOrInASetOfOne_SeesNoControlOnAnyDetailsPage(Linking linking, string userAgent)
    {
        var scenario = await SeedScenarioAsync(linking);
        var questId = await SeedOpenQuestAsync(scenario.DmId);
        var closedQuestId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Old Quest", scenario.DmId, UpcomingDates, isClosed: true);
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, "Harvest Fair", UpcomingEventDate);
        var pastEventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, "Last Year", PastEventDate);

        foreach (var id in new[] { questId, closedQuestId })
        {
            var (status, html) = await QuestDetailsAsync(scenario.SuperAdminClient, id, userAgent);
            status.Should().Be(HttpStatusCode.OK);
            html.Should().NotContain(ControlLabel);
            html.Should().NotContain("move-control-reason");
            html.Should().NotContain("/Move/Quest");
        }

        foreach (var id in new[] { eventId, pastEventId })
        {
            var (status, html) = await EventDetailsAsync(scenario.SuperAdminClient, id);
            status.Should().Be(HttpStatusCode.OK);
            html.Should().NotContain(ControlLabel);
            html.Should().NotContain("move-control-reason");
            html.Should().NotContain("/Move/Event");
        }
    }

    [Theory]
    [InlineData("Admin", DesktopUserAgent)]
    [InlineData("Admin", MobileUserAgent)]
    [InlineData("DungeonMaster", DesktopUserAgent)]
    [InlineData("DungeonMaster", MobileUserAgent)]
    public async Task BoardAdminOrDmOfBothBoards_NeverSeesTheControlEvenOnALinkedBoard(string role, string userAgent)
    {
        var scenario = await SeedScenarioAsync();
        var questId = await SeedOpenQuestAsync(scenario.DmId);
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, "Harvest Fair", UpcomingEventDate);
        var key = $"move_control_{role.ToLowerInvariant()}";
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, key, $"{key}@example.com", roles: [role]);
        var groupRole = role == "Admin" ? GroupRole.Admin : GroupRole.DungeonMaster;
        await BoardMoveSeed.SeedMembershipAsync(factory, user.Id, BoardAlpha, groupRole);
        await BoardMoveSeed.SeedMembershipAsync(factory, user.Id, BoardBravo, groupRole);

        var (questStatus, questHtml) = await QuestDetailsAsync(client, questId, userAgent);
        var (eventStatus, eventHtml) = await EventDetailsAsync(client, eventId);

        questStatus.Should().Be(HttpStatusCode.OK);
        if (role == "Admin")
        {
            // The page really is the management view for this viewer, so the absence below means something.
            questHtml.Should().Contain("Manage Quest");
        }

        questHtml.Should().NotContain(ControlLabel);
        questHtml.Should().NotContain("move-control-reason");
        eventStatus.Should().Be(HttpStatusCode.OK);
        eventHtml.Should().NotContain(ControlLabel);
        eventHtml.Should().NotContain("move-control-reason");
    }

    [Fact]
    public async Task ADirectGetOfAnIneligibleQuest_RedirectsToDetailsWithTheErrorToastAndTheReason()
    {
        var scenario = await SeedScenarioAsync();
        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Goblin Market", scenario.DmId, UpcomingDates, isClosed: true);

        var (_, html) = await QuestDetailsAsync(scenario.SuperAdminClient, questId, DesktopUserAgent);
        html.Should().NotContain("<form action=\"/Move");

        var direct = await scenario.SuperAdminClient.GetAsync($"/Move/Quest/{questId}", TestContext.Current.CancellationToken);
        direct.StatusCode.Should().Be(HttpStatusCode.Redirect);
        direct.Headers.Location!.OriginalString.Should().Contain($"/Quest/Details/{questId}");
        var follow = await scenario.SuperAdminClient.GetAsync(direct.Headers.Location.ToString(), TestContext.Current.CancellationToken);
        Decoded(await follow.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Contain("This quest cannot be moved.");
    }

    private async Task<string> PreviewQuestStampAsync(int questId)
    {
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IQuestMoveService>();
        var actor = await BoardMoveSeed.CreateUserAsync(factory, $"move_control_stamp_{Guid.NewGuid():N}", "Stamp Actor");
        var preview = await service.PreviewAsync(questId, BoardBravo, actor.Id, TestContext.Current.CancellationToken);
        return preview.Stamp;
    }

    private async Task<string> PreviewEventStampAsync(int eventId)
    {
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventMoveService>();
        var actor = await BoardMoveSeed.CreateUserAsync(factory, $"move_control_stamp_{Guid.NewGuid():N}", "Stamp Actor");
        var preview = await service.PreviewAsync(eventId, BoardBravo, actor.Id, TestContext.Current.CancellationToken);
        return preview.Stamp;
    }

    private static Task<HttpResponseMessage> PostMoveAsync(HttpClient client, string path, int id, string stamp) =>
        client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = id.ToString(),
            ["targetBoardId"] = BoardBravo.ToString(),
            ["stamp"] = stamp
        }), TestContext.Current.CancellationToken);

    private static async Task<string> FollowRawAsync(HttpClient client, HttpResponseMessage redirect)
    {
        var response = await client.GetAsync(redirect.Headers.Location!.ToString(), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AQuestMoveByAMemberOfTheTarget_ShowsTheMovedToastWithTheOpenItLinkOnTheQuestBoard()
    {
        var scenario = await SeedScenarioAsync();
        await BoardMoveSeed.SeedMembershipAsync(factory, scenario.SuperAdminId, BoardBravo, GroupRole.Admin);
        var questId = await SeedOpenQuestAsync(scenario.DmId);
        var stamp = await PreviewQuestStampAsync(questId);

        var response = await PostMoveAsync(scenario.SuperAdminClient, "/Move/Quest", questId, stamp);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var html = Decoded(await FollowRawAsync(scenario.SuperAdminClient, response));
        html.Should().Contain("<strong class=\"me-auto\">Moved</strong>");
        html.Should().Contain("Moved \"Goblin Market\" to \"Board Bravo\".");
        html.Should().MatchRegex($"<a href=\"/Quest/Details/{questId}\" class=\"btn btn-sm btn-dark[^\"]*\">\\s*<i class=\"fas fa-arrow-right me-2\"[^>]*></i>Open it on Board Bravo");
        html.Should().MatchRegex("class=\"toast show[^\"]*\" role=\"alert\" data-bs-autohide=\"false\"[^>]*>\\s*<div class=\"toast-header bg-success");
        html.Should().Contain("fa-check-circle");
    }

    [Fact]
    public async Task AnEventMoveByAMemberOfTheTarget_ShowsTheMovedToastWithTheOpenItLinkOnTheEventsList()
    {
        var scenario = await SeedScenarioAsync();
        await BoardMoveSeed.SeedMembershipAsync(factory, scenario.SuperAdminId, BoardBravo, GroupRole.Admin);
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, "Harvest Fair", UpcomingEventDate);
        var stamp = await PreviewEventStampAsync(eventId);

        var response = await PostMoveAsync(scenario.SuperAdminClient, "/Move/Event", eventId, stamp);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var html = Decoded(await FollowRawAsync(scenario.SuperAdminClient, response));
        html.Should().Contain("<strong class=\"me-auto\">Moved</strong>");
        html.Should().Contain("Moved \"Harvest Fair\" to \"Board Bravo\".");
        html.Should().MatchRegex($"<a href=\"/Events/Details/{eventId}\" class=\"btn btn-sm btn-dark[^\"]*\">");
        html.Should().Contain("Open it on Board Bravo");
    }

    [Fact]
    public async Task AMoveByANonMemberOfTheTarget_ShowsTheStandardSuccessToastWithNoLink()
    {
        var scenario = await SeedScenarioAsync();
        var questId = await SeedOpenQuestAsync(scenario.DmId);
        var stamp = await PreviewQuestStampAsync(questId);

        var response = await PostMoveAsync(scenario.SuperAdminClient, "/Move/Quest", questId, stamp);

        var html = Decoded(await FollowRawAsync(scenario.SuperAdminClient, response));
        html.Should().Contain("<strong class=\"me-auto\">Success</strong>");
        html.Should().Contain("Moved \"Goblin Market\" to \"Board Bravo\".");
        html.Should().NotContain("Open it on");
        html.Should().NotContain("<strong class=\"me-auto\">Moved</strong>");
    }

    [Fact]
    public async Task AQuestTitleWithMarkup_RendersEncodedInTheMovedToast()
    {
        var scenario = await SeedScenarioAsync();
        await BoardMoveSeed.SeedMembershipAsync(factory, scenario.SuperAdminId, BoardBravo, GroupRole.Admin);
        var questId = await SeedOpenQuestAsync(scenario.DmId, "<b>Goblin</b> Market");
        var stamp = await PreviewQuestStampAsync(questId);

        var response = await PostMoveAsync(scenario.SuperAdminClient, "/Move/Quest", questId, stamp);

        var raw = await FollowRawAsync(scenario.SuperAdminClient, response);
        raw.Should().Contain("Open it on Board Bravo");
        raw.Should().NotContain("<b>Goblin</b>");
        raw.Should().Contain("&lt;b&gt;Goblin&lt;/b&gt; Market");
    }
}
