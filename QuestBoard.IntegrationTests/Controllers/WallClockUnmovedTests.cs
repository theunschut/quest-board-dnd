using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

// Proves the structural marker this phase relies on: a converted real instant sits inside a
// <time class="local-time"> wrapper, and an untouched wall-clock value never does. If
// reconfiguring the board zone ever moved a game night by even a minute, one of the facts
// below would show a different rendered digit.
public class WallClockUnmovedTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private static readonly Regex LocalTimeElementPattern =
        new("<time class=\"local-time\"[^>]*>.*?</time>", RegexOptions.Singleline);

    // Mirrors BoardTimeZoneHealthCheckTests' own WithWebHostBuilder variant-factory shape.
    // Both zones used across this file are deliberately non-default (not Europe/Amsterdam)
    // and non-UTC, so a fact that only happened to pass against the configured default cannot
    // hide a real regression.
    private WebApplicationFactory<Program> CreateZoneVariantFactory(string zoneId)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TimeZone:BoardTimeZoneId"] = zoneId
                });
            });
        });
    }

    private async Task<string> RenderFinalizedQuestDetailsUnderZoneAsync(string zoneId, DateTime finalizedDate, string title)
    {
        var variantFactory = CreateZoneVariantFactory(zoneId);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            variantFactory.Services, "wcu_dm", "wcu_dm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            variantFactory.Services, dm.Id, title: title, isFinalized: true, finalizedDate: finalizedDate);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            variantFactory, "wcu_viewer", "wcu_viewer@example.com");

        var response = await client.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private async Task<string> RenderUnfinalizedQuestManageUnderZoneAsync(string zoneId, DateTime proposedDate)
    {
        var variantFactory = CreateZoneVariantFactory(zoneId);

        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(variantFactory);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            variantFactory.Services, dm.Id, title: "Wall Clock Manage Quest", isFinalized: false);
        await TestDataHelper.CreateProposedDateAsync(variantFactory.Services, quest.Id, proposedDate);

        var response = await client.GetAsync($"/Quest/Manage/{quest.Id}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FinalizedQuestGameNight_RendersIdenticalWallClockText_AcrossTwoDifferentBoardZones()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var finalizedDate = new DateTime(2026, 9, 20, 19, 0, 0);
        var expectedGameNightText = finalizedDate.ToString("dddd, MMMM dd, yyyy 'at' h:mm tt");

        // Two structurally distinct quests, one seeded and rendered under each zone's own
        // variant host, so each client resolves IBoardClock independently. The actual proof
        // that nothing moved is that both renders carry byte-identical wall-clock text, not
        // merely that each individually matched an expected literal.
        var firstZoneHtml = await RenderFinalizedQuestDetailsUnderZoneAsync(
            "America/New_York", finalizedDate, "Wall Clock Unmoved Quest One");
        var secondZoneHtml = await RenderFinalizedQuestDetailsUnderZoneAsync(
            "Asia/Tokyo", finalizedDate, "Wall Clock Unmoved Quest Two");

        firstZoneHtml.Should().Contain(expectedGameNightText);
        secondZoneHtml.Should().Contain(expectedGameNightText);
    }

    [Fact]
    public async Task FinalizedQuestGameNight_IsNeverInsideALocalTimeElement_WhileARealInstantOnTheSamePageIs()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var finalizedDate = new DateTime(2026, 9, 20, 19, 0, 0);
        var expectedGameNightText = finalizedDate.ToString("dddd, MMMM dd, yyyy 'at' h:mm tt");

        var html = await RenderFinalizedQuestDetailsUnderZoneAsync(
            "Pacific/Auckland", finalizedDate, "Wall Clock Structural Marker Quest");

        // The wall-clock game night text appears on the page...
        html.Should().Contain(expectedGameNightText);

        // ...but never inside a <time class="local-time"> wrapper. The wrapper's presence or
        // absence is the structural marker the whole phase relies on -- not the text alone,
        // which a coincidental format match could satisfy even if conversion had been applied.
        foreach (Match match in LocalTimeElementPattern.Matches(html))
        {
            match.Value.Should().NotContain(expectedGameNightText);
        }

        // A real instant on the same page -- the quest's CreatedAt -- IS wrapped, so this fact
        // would fail if conversion had simply been switched off wholesale rather than applied
        // selectively.
        LocalTimeElementPattern.Matches(html).Count.Should().BeGreaterThan(0);
        html.Should().Contain("data-style=\"date\"");
    }

    [Fact]
    public async Task ProposedDateOnManagePage_RendersIdenticalWallClockText_AcrossTwoDifferentBoardZones()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var proposedDate = new DateTime(2026, 10, 3, 18, 30, 0);
        var expectedProposedDateText = proposedDate.ToString("dddd, MMMM dd, yyyy 'at' h:mm tt");

        var firstZoneHtml = await RenderUnfinalizedQuestManageUnderZoneAsync("America/New_York", proposedDate);
        var secondZoneHtml = await RenderUnfinalizedQuestManageUnderZoneAsync("Asia/Tokyo", proposedDate);

        firstZoneHtml.Should().Contain(expectedProposedDateText);
        secondZoneHtml.Should().Contain(expectedProposedDateText);
    }

    private static readonly Regex WallClockElementPattern =
        new("<time class=\"wall-clock\"[^>]*>.*?</time>", RegexOptions.Singleline);

    // Proves the QuestLog Details "Completed On:" finalized branch (86-07's gap closure) renders
    // through Html.WallClock with no timezone conversion applied anywhere in the pipeline. A
    // deliberately non-UTC, non-default board zone (Asia/Tokyo, +09:00) is configured so that if
    // WallClock ever grew a zone dependency by accident, this fact would show the wrong digit.
    [Fact]
    public async Task FinalizedQuestGameNight_OnQuestLogDetails_RendersTheStoredHourWithNoZAndNoTitle()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var finalizedDate = new DateTime(2026, 9, 4, 18, 0, 0);
        var variantFactory = CreateZoneVariantFactory("Asia/Tokyo");

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            variantFactory.Services, "wcu_qlog_dm", "wcu_qlog_dm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            variantFactory.Services, dm.Id, title: "Wall Clock QuestLog Details Quest",
            isFinalized: true, finalizedDate: finalizedDate);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            variantFactory, "wcu_qlog_viewer", "wcu_qlog_viewer@example.com");

        var response = await client.GetAsync($"/QuestLog/Details/{quest.Id}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var wallClockMatch = WallClockElementPattern.Match(html);
        wallClockMatch.Success.Should().BeTrue(
            because: "the finalized game night must render inside a <time class=\"wall-clock\"> element");

        // The exact stored components, with no Z and no offset -- this value has no UTC instant.
        wallClockMatch.Value.Should().Contain("datetime=\"2026-09-04T18:00\"");
        wallClockMatch.Value.Should().NotContain("Z");

        // No title attribute: a UTC tooltip would claim a UTC instant this value does not have.
        wallClockMatch.Value.Should().NotContain("title=");

        // The regression this whole gap-closure plan exists to prevent: a UTC+2 (or any other)
        // conversion applied to this value would shift 18:00 to a different hour. The stored
        // hour must appear verbatim.
        html.Should().ContainAny("18:00", "6:00 PM");
    }

    // The invariant stated directly rather than implied by the fixture above: for this exact
    // finalized date, the +02:00-shifted digits a board-zone conversion would have produced must
    // never appear on the page at all.
    [Fact]
    public async Task FinalizedQuestGameNight_NeverShowsTheUtcPlusTwoShiftedHour()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var finalizedDate = new DateTime(2026, 9, 4, 18, 0, 0);
        var variantFactory = CreateZoneVariantFactory("Europe/Amsterdam");

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            variantFactory.Services, "wcu_qlog_dm2", "wcu_qlog_dm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            variantFactory.Services, dm.Id, title: "Wall Clock No Shift Quest",
            isFinalized: true, finalizedDate: finalizedDate);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            variantFactory, "wcu_qlog_viewer2", "wcu_qlog_viewer2@example.com");

        var response = await client.GetAsync($"/QuestLog/Details/{quest.Id}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().NotContain("20:00");
        html.Should().NotContain("8:00 PM");
    }
}
