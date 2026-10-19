using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// A super admin's confirm POST moves a one-off event to the other board of its linked set.
/// Seeded: board 1 "Board Alpha" (active) and board 2 "Board Bravo" linked, an upcoming event
/// "Harvest Fair" on board 1 and, where a fact needs them, members of board 2 with and without
/// availability answers.
///
/// What these facts do NOT establish: how the writes translate to SQL. The shared harness uses
/// the EF Core InMemory provider. The mail fact reads the jobs the capturing client recorded;
/// delivery itself is not exercised.
/// </summary>
public class BoardMoveEventTests(MoveWebApplicationFactory factory)
    : IClassFixture<MoveWebApplicationFactory>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const int BoardAlpha = 1;
    private const int BoardBravo = 2;
    private const string EventTitle = "Harvest Fair";

    private static readonly DateOnly Upcoming = new(2031, 5, 10);
    private static readonly DateTime AnsweredAt = new(2031, 1, 3, 9, 0, 0, DateTimeKind.Utc);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<HttpClient> SeedBoardsAsync(BoardType type = BoardType.OneShot)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = type;

        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", type);
        await BoardMoveSeed.LinkAsync(factory, type, BoardAlpha, BoardBravo);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "move_event_super", "move_event_super@example.com");
        return client;
    }

    private async Task<UserEntity> SeedMemberOfBravoAsync(string key, string name)
    {
        var user = await BoardMoveSeed.CreateUserAsync(factory, key, name);
        await BoardMoveSeed.SeedMembershipAsync(factory, user.Id, BoardBravo, GroupRole.Player);
        return user;
    }

    private static FormUrlEncodedContent MoveForm(int eventId, int targetBoardId, string stamp) =>
        new(new Dictionary<string, string>
        {
            ["id"] = eventId.ToString(),
            ["targetBoardId"] = targetBoardId.ToString(),
            ["stamp"] = stamp
        });

    // The stamp the page would have carried: the service's own preview for the same target,
    // taken while the source board is the active one.
    private async Task<string> PreviewStampAsync(int eventId, int targetBoardId)
    {
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventMoveService>();
        var actor = await BoardMoveSeed.CreateUserAsync(factory, $"move_event_stamp_{Guid.NewGuid():N}", "Stamp Actor");
        var preview = await service.PreviewAsync(eventId, targetBoardId, actor.Id, TestContext.Current.CancellationToken);
        return preview.Stamp;
    }

    private async Task<HttpResponseMessage> PostMoveAsync(HttpClient client, int eventId, int targetBoardId, string? stamp = null)
    {
        stamp ??= await PreviewStampAsync(eventId, targetBoardId);
        return await client.PostAsync("/Move/Event", MoveForm(eventId, targetBoardId, stamp), TestContext.Current.CancellationToken);
    }

    private static string PathOf(HttpResponseMessage redirect)
    {
        var location = redirect.Headers.Location!.OriginalString;
        var query = location.IndexOf('?');
        return query < 0 ? location : location[..query];
    }

    private static async Task<string> FollowAsync(HttpClient client, HttpResponseMessage redirect)
    {
        var response = await client.GetAsync(redirect.Headers.Location!.ToString(), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static void ShouldBeRefused(HttpResponseMessage response)
    {
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Forbidden);
        if (response.Headers.Location != null)
        {
            response.Headers.Location.OriginalString.Should().Contain("AccessDenied");
        }
    }

    private async Task<HttpStatusCode> GetDetailsStatusAsync(HttpClient client, int boardId, int eventId)
    {
        factory.TestGroupContext.ActiveGroupId = boardId;
        var response = await client.GetAsync($"/Events/Details/{eventId}", TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("DungeonMaster")]
    public async Task BoardAdminOrDmOfBothBoards_IsRefusedOnGetAndPostAndTheEventStaysPut(string role)
    {
        await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"move_event_{role.ToLowerInvariant()}", $"move_event_{role.ToLowerInvariant()}@example.com",
            roles: [role]);
        await BoardMoveSeed.SeedMembershipAsync(factory, user.Id, BoardBravo, Enum.Parse<GroupRole>(role));
        var stamp = await PreviewStampAsync(eventId, BoardBravo);

        var get = await client.GetAsync($"/Move/Event/{eventId}?targetBoardId={BoardBravo}", TestContext.Current.CancellationToken);
        ShouldBeRefused(get);

        var post = await client.PostAsync("/Move/Event", MoveForm(eventId, BoardBravo, stamp), TestContext.Current.CancellationToken);
        ShouldBeRefused(post);

        var stored = await BoardMoveSeed.ReadEventAsync(factory, eventId);
        stored.GroupId.Should().Be(BoardAlpha);
        stored.FeedRevision.Should().Be(1);
    }

    [Fact]
    public async Task ConfirmPost_MovesAOneOffEventAndShowsItOnlyOnTheTargetBoard()
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming, new TimeOnly(18, 30));

        var response = await PostMoveAsync(client, eventId, BoardBravo);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(response).Should().BeEquivalentTo("/Events");
        var html = await FollowAsync(client, response);
        html.Should().Contain("Moved \"Harvest Fair\" to \"Board Bravo\".");
        html.Should().NotContain("answer was removed");
        html.Should().NotContain("answers were removed");

        var after = await BoardMoveSeed.ReadEventAsync(factory, eventId);
        after.GroupId.Should().Be(BoardBravo);
        after.FeedRevision.Should().Be(2);

        (await GetDetailsStatusAsync(client, BoardAlpha, eventId)).Should().Be(HttpStatusCode.NotFound);
        (await GetDetailsStatusAsync(client, BoardBravo, eventId)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SeriesOccurrence_IsRefusedOnGetAndOnPostAndStaysOnItsBoard()
    {
        var client = await SeedBoardsAsync();
        var seriesId = await BoardMoveSeed.SeedSeriesAsync(factory, BoardAlpha, "Weekly Market", Upcoming);
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, "Market Day", Upcoming, seriesId: seriesId, seriesSlotIndex: 0);

        var get = await client.GetAsync($"/Move/Event/{eventId}?targetBoardId={BoardBravo}", TestContext.Current.CancellationToken);
        get.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(get).Should().BeEquivalentTo($"/Events/Details/{eventId}");
        var html = await FollowAsync(client, get);
        html.Should().Contain("This event cannot be moved. It is part of a recurring series. Use \"Detach sessions\" on the series page first.");

        var post = await client.PostAsync("/Move/Event", MoveForm(eventId, BoardBravo, "anything"), TestContext.Current.CancellationToken);
        post.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(post).Should().BeEquivalentTo($"/Events/Details/{eventId}");

        var stored = await BoardMoveSeed.ReadEventAsync(factory, eventId);
        stored.GroupId.Should().Be(BoardAlpha);
        stored.SeriesId.Should().Be(seriesId);
    }

    [Fact]
    public async Task DetachedOccurrence_Moves()
    {
        var client = await SeedBoardsAsync();
        var seriesId = await BoardMoveSeed.SeedSeriesAsync(factory, BoardAlpha, "Weekly Market", Upcoming);
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, "Market Day", Upcoming, seriesId: null, seriesSlotIndex: null);
        seriesId.Should().BeGreaterThan(0);

        var response = await PostMoveAsync(client, eventId, BoardBravo);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await BoardMoveSeed.ReadEventAsync(factory, eventId)).GroupId.Should().Be(BoardBravo);
    }

    [Fact]
    public async Task PastEvent_IsRefusedWithTheReasonAndStaysOnItsBoard()
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, "Old Fair", new DateOnly(2020, 1, 10));

        var get = await client.GetAsync($"/Move/Event/{eventId}?targetBoardId={BoardBravo}", TestContext.Current.CancellationToken);
        get.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await FollowAsync(client, get)).Should().Contain("This event cannot be moved. It is in the past.");

        var post = await client.PostAsync("/Move/Event", MoveForm(eventId, BoardBravo, "anything"), TestContext.Current.CancellationToken);
        post.StatusCode.Should().Be(HttpStatusCode.Redirect);

        (await BoardMoveSeed.ReadEventAsync(factory, eventId)).GroupId.Should().Be(BoardAlpha);
    }

    [Fact]
    public async Task UpcomingCancelledEvent_MovesAndKeepsItsCancellation()
    {
        var client = await SeedBoardsAsync();
        var cancelledAt = new DateTime(2031, 2, 1, 8, 0, 0, DateTimeKind.Utc);
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming, cancelledAt: cancelledAt);

        var response = await PostMoveAsync(client, eventId, BoardBravo);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var after = await BoardMoveSeed.ReadEventAsync(factory, eventId);
        after.GroupId.Should().Be(BoardBravo);
        after.CancelledAt.Should().Be(cancelledAt);
    }

    [Fact]
    public async Task CampaignTarget_KeepsMemberAnswersDropsStrangersAndAddsAutomaticYesRows()
    {
        var client = await SeedBoardsAsync(BoardType.Campaign);
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);

        var answered = await SeedMemberOfBravoAsync("move_event_answered", "Answered Member");
        var automatic = await SeedMemberOfBravoAsync("move_event_automatic", "Automatic Member");
        var silent = await SeedMemberOfBravoAsync("move_event_silent", "Silent Member");
        var stranger = await BoardMoveSeed.CreateUserAsync(factory, "move_event_stranger", "Stranger");

        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, answered.Id, VoteType.Maybe, updatedAt: AnsweredAt);
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, automatic.Id, VoteType.Yes);
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, stranger.Id, VoteType.No, updatedAt: AnsweredAt);

        var response = await PostMoveAsync(client, eventId, BoardBravo);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var html = await FollowAsync(client, response);
        html.Should().Contain("Moved \"Harvest Fair\" to \"Board Bravo\". 1 answer was removed.");

        var after = await BoardMoveSeed.ReadEventAsync(factory, eventId);
        after.GroupId.Should().Be(BoardBravo);
        after.Signups.Select(s => s.UserId).Should().BeEquivalentTo([answered.Id, automatic.Id, silent.Id]);

        var keptAnswer = after.Signups.Single(s => s.UserId == answered.Id);
        keptAnswer.Availability.Should().Be((int)VoteType.Maybe);
        keptAnswer.UpdatedAt.Should().Be(AnsweredAt);

        var keptAutomatic = after.Signups.Single(s => s.UserId == automatic.Id);
        keptAutomatic.Availability.Should().Be((int)VoteType.Yes);
        keptAutomatic.UpdatedAt.Should().BeNull();

        var added = after.Signups.Single(s => s.UserId == silent.Id);
        added.Availability.Should().Be((int)VoteType.Yes);
        added.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task OneShotTarget_AddsNoRowsAndPluralisesTheRemovedAnswers()
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);
        var member = await SeedMemberOfBravoAsync("move_event_oneshot_member", "Member");
        await SeedMemberOfBravoAsync("move_event_oneshot_silent", "Silent Member");
        var strangerOne = await BoardMoveSeed.CreateUserAsync(factory, "move_event_oneshot_s1", "Stranger One");
        var strangerTwo = await BoardMoveSeed.CreateUserAsync(factory, "move_event_oneshot_s2", "Stranger Two");
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, member.Id, VoteType.Yes, updatedAt: AnsweredAt);
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, strangerOne.Id, VoteType.Yes, updatedAt: AnsweredAt);
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, strangerTwo.Id, VoteType.No);

        var response = await PostMoveAsync(client, eventId, BoardBravo);

        var html = await FollowAsync(client, response);
        html.Should().Contain("2 answers were removed.");
        var after = await BoardMoveSeed.ReadEventAsync(factory, eventId);
        after.Signups.Select(s => s.UserId).Should().BeEquivalentTo([member.Id]);
    }

    [Fact]
    public async Task Move_EnqueuesNoEmailOfAnyKind()
    {
        var client = await SeedBoardsAsync(BoardType.Campaign);
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);
        var member = await SeedMemberOfBravoAsync("move_event_mail_member", "Mail Member");
        var stranger = await BoardMoveSeed.CreateUserAsync(factory, "move_event_mail_stranger", "Mail Stranger");
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, member.Id, VoteType.Yes, updatedAt: AnsweredAt);
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, stranger.Id, VoteType.Yes, updatedAt: AnsweredAt);
        var stamp = await PreviewStampAsync(eventId, BoardBravo);
        factory.JobClient.Clear();

        var response = await client.PostAsync("/Move/Event", MoveForm(eventId, BoardBravo, stamp), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await BoardMoveSeed.ReadEventAsync(factory, eventId)).GroupId.Should().Be(BoardBravo);
        factory.JobClient.EnqueuedJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task StaleStamp_ReShowsThePageAndMovesNothing()
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);

        var response = await PostMoveAsync(client, eventId, BoardBravo, stamp: "stale");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BoardMoveSeed.ReadEventAsync(factory, eventId)).GroupId.Should().Be(BoardAlpha);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(BoardAlpha)]
    public async Task TargetOutsideTheSetOrTheSourceItself_IsRefusedAndNothingMoves(int targetBoardId)
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);

        var response = await PostMoveAsync(client, eventId, targetBoardId, stamp: "anything");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await BoardMoveSeed.ReadEventAsync(factory, eventId)).GroupId.Should().Be(BoardAlpha);
    }

    [Fact]
    public async Task BoardInNoSet_IsRefusedWithTheNotLinkedCopy()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "move_event_super_alone", "move_event_super_alone@example.com");
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);

        var get = await client.GetAsync($"/Move/Event/{eventId}", TestContext.Current.CancellationToken);

        get.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await FollowAsync(client, get)).Should().Contain("This board is not linked to any other board, so there is nowhere to move this.");
    }

    [Fact]
    public async Task EventOnAnotherBoard_RedirectsToTheEventsListWithAnInfoToastOnGet()
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardBravo, EventTitle, Upcoming);

        var get = await client.GetAsync($"/Move/Event/{eventId}", TestContext.Current.CancellationToken);

        get.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(get).Should().BeEquivalentTo("/Events");
        (await FollowAsync(client, get)).Should().Contain(GoneToast);
    }

    private const string GoneToast = "That event is no longer on this board.";

    [Fact]
    public async Task ConfirmPost_RepeatedAfterASuccessfulMove_RedirectsToTheEventsListWithAnInfoToastAndMovesNothingAgain()
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);
        var stamp = await PreviewStampAsync(eventId, BoardBravo);
        var first = await PostMoveAsync(client, eventId, BoardBravo, stamp);
        first.StatusCode.Should().Be(HttpStatusCode.Redirect);
        await FollowAsync(client, first);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;

        var second = await PostMoveAsync(client, eventId, BoardBravo, stamp);

        second.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(second).Should().BeEquivalentTo("/Events");
        (await FollowAsync(client, second)).Should().Contain(GoneToast);

        var stored = await BoardMoveSeed.ReadEventAsync(factory, eventId);
        stored.GroupId.Should().Be(BoardBravo);
        stored.FeedRevision.Should().Be(2);
    }

    [Fact]
    public async Task Get_AfterASuccessfulMove_RedirectsToTheEventsListWithAnInfoToastInsteadOfA404()
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);
        var first = await PostMoveAsync(client, eventId, BoardBravo);
        await FollowAsync(client, first);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;

        var back = await client.GetAsync($"/Move/Event/{eventId}", TestContext.Current.CancellationToken);

        back.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(back).Should().BeEquivalentTo("/Events");
        (await FollowAsync(client, back)).Should().Contain(GoneToast);
    }

    [Fact]
    public async Task ConfirmPost_ForAnEventOnAnotherBoardOrNoneAtAll_GivesTheSameRedirectAndToast()
    {
        var client = await SeedBoardsAsync();
        var elsewhereId = await BoardMoveSeed.SeedEventAsync(factory, BoardBravo, EventTitle, Upcoming);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;

        foreach (var id in new[] { 99999, elsewhereId })
        {
            var post = await client.PostAsync(
                "/Move/Event", MoveForm(id, BoardBravo, "anything"), TestContext.Current.CancellationToken);

            post.StatusCode.Should().Be(HttpStatusCode.Redirect);
            PathOf(post).Should().BeEquivalentTo("/Events");
            (await FollowAsync(client, post)).Should().Contain(GoneToast);
        }

        (await BoardMoveSeed.ReadEventAsync(factory, elsewhereId)).GroupId.Should().Be(BoardBravo);
    }

    private async Task<(string Html, HttpResponseMessage Response)> GetPageAsync(
        HttpClient client, int eventId, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/Move/Event/{eventId}");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (html, response);
    }

    [Fact]
    public async Task Page_OnDesktop_ShowsTheEventCopy()
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);

        var (html, response) = await GetPageAsync(client, eventId, DesktopUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Move Event to Linked Board");
        html.Should().Contain("This Event");
        html.Should().Contain("No availability answers to keep.");
        html.Should().Contain("No emails are sent for an event move.");
        html.Should().NotContain("move-card-mobile");
    }

    [Fact]
    public async Task Page_OnMobile_ShowsTheEventCopyInTheMobileCards()
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);

        var (html, response) = await GetPageAsync(client, eventId, MobileUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("move-card-mobile");
        html.Should().Contain("Move Event to Linked Board");
        html.Should().Contain("This Event");
        html.Should().Contain("No emails are sent for an event move.");
    }

    [Fact]
    public async Task Page_CountsTheAnswersThatStay()
    {
        var client = await SeedBoardsAsync();
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, Upcoming);
        var member = await SeedMemberOfBravoAsync("move_event_page_member", "Page Member");
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, member.Id, VoteType.Yes, updatedAt: AnsweredAt);

        var (html, _) = await GetPageAsync(client, eventId, DesktopUserAgent);

        html.Should().Contain("1 availability answer stays exactly as given.");
    }
}
