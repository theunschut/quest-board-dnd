using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Proves that "has this game night passed" is answered against the board's own today rather
/// than a UTC instant, on every surface that asks the question.
/// </summary>
/// <remarks>
/// The whole suite is pinned to one instant chosen so the two answers differ: at
/// 2026-09-05T23:30Z the board (Europe/Amsterdam, +02:00 in September) has already turned over
/// to the 6th, while UTC is still on the 5th. A quest whose game night was the evening of the
/// 5th is therefore over as far as the board is concerned, and every fact below says so. Each
/// one fails against a UTC comparison, which would still call that quest "Finalized" and keep
/// it out of the quest log for another half hour.
/// </remarks>
public class QuestStatusBoardClockTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string BoardZoneId = "Europe/Amsterdam";
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    // 23:30 UTC on the 5th is 01:30 board time on the 6th -- the window the UTC comparison got
    // wrong, and the reason this fixture pins the clock rather than reading the real one.
    private static readonly DateTimeOffset PinnedNow = new(2026, 9, 5, 23, 30, 0, TimeSpan.Zero);

    // The game night that has already happened in board-local terms but not in UTC terms.
    private static readonly DateTime LastNightsGameNight = new(2026, 9, 5, 18, 0, 0);

    // Tonight's session, which must still read as upcoming on the same page load.
    private static readonly DateTime TonightsGameNight = new(2026, 9, 6, 18, 0, 0);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Mirrors WallClockUnmovedTests' variant-factory shape, with the clock pinned as well as the
    // zone so the board-local date under test is the same on every run.
    private WebApplicationFactory<Program> CreatePinnedFactory()
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TimeZone:BoardTimeZoneId"] = BoardZoneId
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(PinnedNow));
            });
        });
    }

    private static readonly Regex BadgeSpanPattern =
        new("<span class=\"badge[^\"]*\">(.*?)</span>", RegexOptions.Singleline);

    private static readonly Regex TagPattern = new("<[^>]*>");

    // The status badge renders its icon and its text on separate lines, so the text is read by
    // stripping the span's inner tags rather than matched as a literal substring of the page.
    private static List<string> StatusBadgeTexts(string html) =>
        BadgeSpanPattern.Matches(html)
            .Select(match => TagPattern.Replace(match.Groups[1].Value, string.Empty).Trim())
            .Where(text => text.Length > 0)
            .ToList();

    [Fact]
    public async Task AdminQuests_Desktop_ShowsLastNightsGameNightAsDone()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var pinnedFactory = CreatePinnedFactory();

        var (client, admin) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            pinnedFactory, "qsbc_admin1", "qsbc_admin1@example.com");
        await TestDataHelper.CreateTestQuestAsync(
            pinnedFactory.Services, admin.Id, title: "Board Clock Done Quest",
            isFinalized: true, finalizedDate: LastNightsGameNight);

        var response = await client.GetAsync("/Admin/Quests", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        StatusBadgeTexts(html).Should().Contain("Done",
            because: "the board has already turned over to the 6th, so the 5th's game night is over " +
                     "even though UTC still reads the 5th");
    }

    [Fact]
    public async Task AdminQuests_Mobile_ShowsLastNightsGameNightAsDone()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var pinnedFactory = CreatePinnedFactory();

        var (authClient, admin) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            pinnedFactory, "qsbc_admin2", "qsbc_admin2@example.com");
        await TestDataHelper.CreateTestQuestAsync(
            pinnedFactory.Services, admin.Id, title: "Board Clock Done Quest Mobile",
            isFinalized: true, finalizedDate: LastNightsGameNight);

        var request = new HttpRequestMessage(HttpMethod.Get, "/Admin/Quests");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await authClient.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("admin-quests-card-mobile", because: "the mobile layout must be the one rendered");
        StatusBadgeTexts(html).Should().Contain("Done");
    }

    [Fact]
    public async Task AdminQuests_StillShowsTonightsGameNightAsFinalized()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var pinnedFactory = CreatePinnedFactory();

        var (client, admin) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            pinnedFactory, "qsbc_admin3", "qsbc_admin3@example.com");
        await TestDataHelper.CreateTestQuestAsync(
            pinnedFactory.Services, admin.Id, title: "Board Clock Upcoming Quest",
            isFinalized: true, finalizedDate: TonightsGameNight);

        var response = await client.GetAsync("/Admin/Quests", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var badges = StatusBadgeTexts(html);
        badges.Should().Contain("Finalized",
            because: "a session dated for tonight has not happened yet, whichever clock is asked");
        badges.Should().NotContain("Done");
    }

    [Fact]
    public async Task QuestDetails_ShowsLastNightsGameNightAsDone()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var pinnedFactory = CreatePinnedFactory();

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            pinnedFactory.Services, "qsbc_dm1", "qsbc_dm1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            pinnedFactory.Services, dm.Id, title: "Board Clock Details Quest",
            isFinalized: true, finalizedDate: LastNightsGameNight);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            pinnedFactory, "qsbc_viewer1", "qsbc_viewer1@example.com");

        var response = await client.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        StatusBadgeTexts(html).Should().Contain("Done");
    }

    [Fact]
    public async Task QuestManage_ShowsLastNightsGameNightAsDone()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var pinnedFactory = CreatePinnedFactory();

        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            pinnedFactory, "qsbc_dm2", "qsbc_dm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            pinnedFactory.Services, dm.Id, title: "Board Clock Manage Quest",
            isFinalized: true, finalizedDate: LastNightsGameNight);

        var response = await client.GetAsync($"/Quest/Manage/{quest.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        StatusBadgeTexts(html).Should().Contain("Done");
    }

    [Fact]
    public async Task QuestLogIndex_AdmitsLastNightsGameNight()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var pinnedFactory = CreatePinnedFactory();

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            pinnedFactory.Services, "qsbc_dm3", "qsbc_dm3@example.com");
        await TestDataHelper.CreateTestQuestAsync(
            pinnedFactory.Services, dm.Id, title: "Board Clock Logged Quest",
            isFinalized: true, finalizedDate: LastNightsGameNight);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            pinnedFactory, "qsbc_viewer2", "qsbc_viewer2@example.com");

        var response = await client.GetAsync("/QuestLog", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("Board Clock Logged Quest",
            because: "the quest log admits a quest as soon as its game night is over in board-local terms");
    }

    [Fact]
    public async Task QuestLogDetails_AdmitsLastNightsGameNightRatherThanReturningNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var pinnedFactory = CreatePinnedFactory();

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            pinnedFactory.Services, "qsbc_dm4", "qsbc_dm4@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            pinnedFactory.Services, dm.Id, title: "Board Clock Log Details Quest",
            isFinalized: true, finalizedDate: LastNightsGameNight);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            pinnedFactory, "qsbc_viewer3", "qsbc_viewer3@example.com");

        var response = await client.GetAsync($"/QuestLog/Details/{quest.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the quest log's own membership check reads the same board-local today the index does");
    }

    [Fact]
    public async Task QuestLogDetails_StillRefusesTonightsGameNight()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var pinnedFactory = CreatePinnedFactory();

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            pinnedFactory.Services, "qsbc_dm5", "qsbc_dm5@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            pinnedFactory.Services, dm.Id, title: "Board Clock Upcoming Log Quest",
            isFinalized: true, finalizedDate: TonightsGameNight);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            pinnedFactory, "qsbc_viewer4", "qsbc_viewer4@example.com");

        var response = await client.GetAsync($"/QuestLog/Details/{quest.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "a session that has not run yet has no place in the quest log");
    }
}
