using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// The "DM Controls" sidebar card on the desktop Quest Manage page. Seeded: a quest and its own
/// dungeon master, as a finalized one-shot quest (with and without a follow-up), as a finalized
/// campaign quest on a campaign board, and as an open quest that is not finalized yet. The facts
/// pin what the card holds and in which order (View Public Page, Create Follow-Up Quest, Send
/// Reminder), that the action row in the main card no longer carries the follow-up and reminder
/// controls, and the conditions under which each control shows. They also pin that the phone
/// layout keeps its own arrangement on purpose.
///
/// What these facts do NOT establish: how the page looks. They check markup, not rendering, and
/// the harness uses the EF Core InMemory provider.
/// </summary>
public class QuestManageDmControlsTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string CardHeading = "<h5>DM Controls</h5>";
    private const string NextCardHeading = "<h5>Quest Summary</h5>";

    private static readonly DateTime[] UpcomingDates = [new(2031, 5, 10, 19, 0, 0), new(2031, 5, 11, 19, 0, 0)];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private static async Task<(HttpStatusCode Status, string Html)> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, html);
    }

    private static Task<(HttpStatusCode Status, string Html)> ManageAsync(HttpClient client, int questId, string userAgent) =>
        GetAsync(client, $"/Quest/Manage/{questId}", userAgent);

    private static string CardSegment(string html)
    {
        var start = html.IndexOf(CardHeading, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "the page should render the DM Controls card");
        var end = html.IndexOf(NextCardHeading, start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, "the Quest Summary card should follow the DM Controls card");
        return html[start..end];
    }

    private static string MainSegment(string html)
    {
        var start = html.IndexOf(CardHeading, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "the page should render the DM Controls card");
        return html[..start];
    }

    private async Task<(HttpClient Client, int DmId)> CreateDmAsync(string key)
    {
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, key, $"{key}@example.com", roles: ["DungeonMaster"]);
        return (client, dm.Id);
    }

    private async Task<(HttpClient Client, int QuestId)> SeedFinalizedOneShotAsync(string key, bool withFollowUp)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dmId) = await CreateDmAsync(key);
        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, 1, "Goblin Market", dmId, UpcomingDates, finalizedDate: UpcomingDates[0]);
        if (withFollowUp)
        {
            await BoardMoveSeed.SeedQuestAsync(
                factory, 1, "Goblin Market Part Two", dmId, UpcomingDates, originalQuestId: questId);
        }

        return (client, questId);
    }

    [Fact]
    public async Task FinalizedOneShotQuest_ShowsViewPublicPageCreateFollowUpAndSendReminderInTheDmControlsCard()
    {
        var (client, questId) = await SeedFinalizedOneShotAsync("dm_controls_finalized", withFollowUp: false);

        var (status, html) = await ManageAsync(client, questId, DesktopUserAgent);

        status.Should().Be(HttpStatusCode.OK);
        Regex.Matches(html, Regex.Escape(CardHeading)).Count.Should().Be(1);
        html.Should().NotContain("<h5>View Public Page</h5>");
        html.Should().Contain("View Public Page");

        var card = CardSegment(html);
        var viewPublicPage = card.IndexOf($"href=\"/Quest/Details/{questId}\"", StringComparison.Ordinal);
        var followUp = card.IndexOf($"href=\"/Quest/CreateFollowUp/{questId}\"", StringComparison.Ordinal);
        var reminder = card.IndexOf($"action=\"/Quest/SendReminder/{questId}\"", StringComparison.Ordinal);
        viewPublicPage.Should().BeGreaterThanOrEqualTo(0);
        followUp.Should().BeGreaterThan(viewPublicPage);
        reminder.Should().BeGreaterThan(followUp);
        card.Should().Contain("Create Follow-Up Quest");
        card.Should().Contain("Send Reminder");
        card.IndexOf("__RequestVerificationToken", reminder, StringComparison.Ordinal).Should().BeGreaterThan(reminder);

        card.Should().MatchRegex($"<a href=\"/Quest/Details/{questId}\" class=\"btn btn-secondary w-100\"");
        card.Should().MatchRegex($"<a href=\"/Quest/CreateFollowUp/{questId}\"\\s+class=\"btn btn-primary w-100 mt-2\"");
        card.Should().MatchRegex("<button type=\"submit\" class=\"btn btn-info w-100\"");
    }

    [Fact]
    public async Task FinalizedOneShotQuest_KeepsEditOpenAndRefreshInTheMainCardWithoutFollowUpOrReminder()
    {
        var (client, questId) = await SeedFinalizedOneShotAsync("dm_controls_main_card", withFollowUp: false);

        var (status, html) = await ManageAsync(client, questId, DesktopUserAgent);

        status.Should().Be(HttpStatusCode.OK);
        var main = MainSegment(html);
        main.Should().Contain("Edit Quest");
        main.Should().Contain("Open Quest");
        main.Should().Contain("Refresh Data");
        main.Should().NotContain("/Quest/CreateFollowUp/");
        main.Should().NotContain("/Quest/SendReminder/");
        main.Should().NotContain("Create Follow-Up Quest");
        main.Should().NotContain("Send Reminder");
    }

    [Fact]
    public async Task FinalizedOneShotQuestWithAFollowUp_HidesCreateFollowUpButStillShowsSendReminder()
    {
        var (client, questId) = await SeedFinalizedOneShotAsync("dm_controls_followup", withFollowUp: true);

        var (status, html) = await ManageAsync(client, questId, DesktopUserAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain($"/Quest/CreateFollowUp/{questId}");
        CardSegment(html).Should().Contain($"action=\"/Quest/SendReminder/{questId}\"");
    }

    [Fact]
    public async Task CampaignQuest_ShowsViewPublicPageInTheDmControlsCardButNeitherFollowUpNorReminder()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);
        var (client, dmId) = await CreateDmAsync("dm_controls_campaign");
        await BoardMoveSeed.SeedMembershipAsync(factory, dmId, 2, GroupRole.DungeonMaster);
        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, 2, "Campaign Arc", dmId, UpcomingDates, finalizedDate: UpcomingDates[0]);
        factory.TestGroupContext.ActiveGroupId = 2;
        factory.TestGroupContext.BoardType = BoardType.Campaign;

        var (status, html) = await ManageAsync(client, questId, DesktopUserAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Close Quest");
        CardSegment(html).Should().Contain($"href=\"/Quest/Details/{questId}\"");
        html.Should().NotContain("/Quest/CreateFollowUp/");
        html.Should().NotContain("/Quest/SendReminder/");
    }

    [Fact]
    public async Task UnfinalizedOneShotQuest_ShowsViewPublicPageInTheDmControlsCardButNeitherFollowUpNorReminder()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dmId) = await CreateDmAsync("dm_controls_open");
        var questId = await BoardMoveSeed.SeedQuestAsync(factory, 1, "Open Quest Board", dmId, UpcomingDates);

        var (status, html) = await ManageAsync(client, questId, DesktopUserAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Finalize Quest");
        CardSegment(html).Should().Contain($"href=\"/Quest/Details/{questId}\"");
        html.Should().NotContain("/Quest/CreateFollowUp/");
        html.Should().NotContain("/Quest/SendReminder/");
    }

    [Fact]
    public async Task PhoneLayout_KeepsItsOwnArrangementWithoutADmControlsCard()
    {
        var (client, questId) = await SeedFinalizedOneShotAsync("dm_controls_phone", withFollowUp: false);

        var (status, html) = await ManageAsync(client, questId, MobileUserAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().Contain($"href=\"/Quest/CreateFollowUp/{questId}\"");
        html.Should().NotContain(CardHeading);
    }
}
