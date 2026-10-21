using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// A linked board's calendar offers a link to another board's event or quest only when the viewer
/// is a member there and that board still offers the page the link opens. Otherwise the entry
/// stays on the calendar as plain text, the same markup a non-member gets. Each fact is run on the
/// desktop month grid, the phone agenda and both Quest Details layouts, because the phone and the
/// desktop build their linked entries from different markup.
///
/// Seeded: board 1 (the active board), board 2 linked to it. Both carry an entry on one day of a
/// fixed far-away month, plus a quest on board 1 that proposes that day so Quest Details shows them.
///
/// What these facts do NOT establish: how the linked reads translate to SQL. The shared harness
/// uses the EF Core InMemory provider.
/// </summary>
public class BoardPageLinkedCalendarTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const int BoardA = 1;
    private const int BoardB = 2;
    private const string BoardBName = "Linked Board Bravo";
    private const string BravoEventTitle = "Bravo Lantern Night";
    private const string BravoQuestTitle = "Bravo Crypt Run";

    private const int Year = 2030;
    private const int Month = 3;
    private const int Day = 12;
    private const string CalendarUrl = "/Calendar?year=2030&month=3";

    public static TheoryData<string> UserAgents => new() { DesktopUserAgent, MobileUserAgent };

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = BoardA;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        await BoardPageSeed.ResetAsync(factory, BoardA);
        await BoardPageSeed.ResetAsync(factory, BoardB);
    }

    private record Scenario(int OwnQuestId, int EventId, int QuestId, HttpClient MemberClient, HttpClient NonMemberClient);

    // The member belongs to both boards; the non-member belongs to the active board only.
    private async Task<Scenario> SeedScenarioAsync()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardA;
        await BoardMoveSeed.SeedBoardAsync(factory, BoardB, BoardBName, BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, BoardA, BoardB);

        var dm = await BoardMoveSeed.CreateUserAsync(factory, "pagelinked_dm", "Dungeon Master Dana");
        var (memberClient, member) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "pagelinked_member", "pagelinked_member@example.com", roles: []);
        await BoardMoveSeed.SeedMembershipAsync(factory, member.Id, BoardA, GroupRole.Player);
        await BoardMoveSeed.SeedMembershipAsync(factory, member.Id, BoardB, GroupRole.Player);
        var (nonMemberClient, nonMember) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "pagelinked_outsider", "pagelinked_outsider@example.com", roles: []);
        await BoardMoveSeed.SeedMembershipAsync(factory, nonMember.Id, BoardA, GroupRole.Player);

        var proposed = new DateTime(Year, Month, Day, 19, 0, 0);
        var ownQuestId = await BoardMoveSeed.SeedQuestAsync(factory, BoardA, "Alpha Own Quest", dm.Id, [proposed]);
        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardB, BravoQuestTitle, dm.Id, [proposed], finalizedDate: proposed);
        var eventId = await SeedEventAsync(BoardB, BravoEventTitle);

        return new Scenario(ownQuestId, eventId, questId, memberClient, nonMemberClient);
    }

    private async Task<int> SeedEventAsync(int groupId, string title)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = new DateOnly(Year, Month, Day),
            StartTime = new TimeOnly(19, 0),
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    private static async Task<string> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // A link or click target to the details page, not a longer id that merely starts with it.
    private static bool Links(string html, string url) =>
        Regex.IsMatch(html, Regex.Escape(url) + @"(?!d)");

    private static string EventUrl(int id) => $"/Events/Details/{id}";

    private static string QuestUrl(int id) => $"/Quest/Details/{id}";

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Calendar_EventsOffOnTheOwningBoard_EventIsPlainTextForAMember(string userAgent)
    {
        var scenario = await SeedScenarioAsync();
        await BoardPageSeed.SetAsync(factory, BoardB, BoardPageSet.Empty.With(BoardPage.Events), BoardPage.Quests);

        var html = await GetAsync(scenario.MemberClient, CalendarUrl, userAgent);

        html.Should().Contain(BravoEventTitle);
        Links(html, EventUrl(scenario.EventId)).Should().BeFalse();
        html.Should().Contain(BravoQuestTitle);
        Links(html, QuestUrl(scenario.QuestId)).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Calendar_QuestsOffOnTheOwningBoard_QuestIsPlainTextForAMember(string userAgent)
    {
        var scenario = await SeedScenarioAsync();
        await BoardPageSeed.SetAsync(factory, BoardB, BoardPageSet.Empty.With(BoardPage.Quests), BoardPage.Calendar);

        var html = await GetAsync(scenario.MemberClient, CalendarUrl, userAgent);

        html.Should().Contain(BravoQuestTitle);
        Links(html, QuestUrl(scenario.QuestId)).Should().BeFalse();
        html.Should().Contain(BravoEventTitle);
        Links(html, EventUrl(scenario.EventId)).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Calendar_DefaultSettings_MemberStillGetsBothLinks(string userAgent)
    {
        var scenario = await SeedScenarioAsync();

        var html = await GetAsync(scenario.MemberClient, CalendarUrl, userAgent);

        html.Should().Contain(BravoEventTitle);
        Links(html, EventUrl(scenario.EventId)).Should().BeTrue();
        html.Should().Contain(BravoQuestTitle);
        Links(html, QuestUrl(scenario.QuestId)).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Calendar_DefaultSettings_NonMemberGetsPlainText(string userAgent)
    {
        var scenario = await SeedScenarioAsync();

        var html = await GetAsync(scenario.NonMemberClient, CalendarUrl, userAgent);

        html.Should().Contain(BravoEventTitle);
        html.Should().Contain(BravoQuestTitle);
        Links(html, EventUrl(scenario.EventId)).Should().BeFalse();
        Links(html, QuestUrl(scenario.QuestId)).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestDetails_OwningBoardPagesOff_LinkedEntriesAreTextButStillListed(string userAgent)
    {
        var scenario = await SeedScenarioAsync();
        await BoardPageSeed.SetAsync(
            factory, BoardB, BoardPageSet.Empty.With(BoardPage.Events).With(BoardPage.Quests), BoardPage.Calendar);

        var html = await GetAsync(scenario.MemberClient, QuestUrl(scenario.OwnQuestId), userAgent);

        html.Should().Contain(BravoEventTitle);
        html.Should().Contain(BravoQuestTitle);
        Links(html, EventUrl(scenario.EventId)).Should().BeFalse();
        Links(html, QuestUrl(scenario.QuestId)).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestDetails_DefaultSettings_MemberStillGetsBothLinks(string userAgent)
    {
        var scenario = await SeedScenarioAsync();

        var html = await GetAsync(scenario.MemberClient, QuestUrl(scenario.OwnQuestId), userAgent);

        Links(html, EventUrl(scenario.EventId)).Should().BeTrue();
        Links(html, QuestUrl(scenario.QuestId)).Should().BeTrue();
    }
}
