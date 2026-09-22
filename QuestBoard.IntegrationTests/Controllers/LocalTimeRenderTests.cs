using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

// Proves the whole vertical path -- IBoardClock, Html.LocalTime, and the two Profile views --
// over real rendered HTML for the exact value the operator reported wrong: the Calendar
// Subscription "Last fetched" timestamp on Account/Profile.
public class LocalTimeRenderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private async Task<CalendarSubscription> MintAndFetchSubscriptionAsync(int userId, HttpClient client)
    {
        using var scope = factory.Services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        var subscription = await subscriptionService.MintForUserAsync(userId, TestContext.Current.CancellationToken);

        // Fetching the feed is the only write path that sets LastFetchedAt -- mirrors
        // ProfileCalendarSubscriptionTests' own way of getting a known fetch state.
        await client.GetAsync($"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        return subscription;
    }

    [Fact]
    public async Task Profile_RendersLastFetchedAt_AsLocalTimeMarkup()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "localtime_lastfetched", "localtime_lastfetched@example.com");
        await MintAndFetchSubscriptionAsync(user.Id, client);

        var response = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        html.Should().Contain("class=\"local-time\"");
        html.Should().Contain("data-style=\"date-time\"");
        html.Should().MatchRegex(
            "datetime=\"\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}Z\"",
            because: "the datetime attribute must carry the untouched UTC instant, machine-parseable by JS Date");
        html.Should().MatchRegex(
            "title=\"\\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2} UTC\"",
            because: "title is the one place the raw UTC instant is disclosed, at full date-and-time precision");
    }

    [Fact]
    public async Task Profile_RendersCreatedAt_AsLocalTimeMarkup_OnBothLayouts()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "localtime_created", "localtime_created@example.com");
        using (var scope = factory.Services.CreateScope())
        {
            var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
            await subscriptionService.MintForUserAsync(user.Id, TestContext.Current.CancellationToken);
        }

        var desktopHtml = await (await client.GetAsync(
            "/Account/Profile", TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var mobileRequest = new HttpRequestMessage(HttpMethod.Get, "/Account/Profile");
        mobileRequest.Headers.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");
        var mobileHtml = await (await client.SendAsync(mobileRequest, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        desktopHtml.Should().Contain("data-style=\"date\"");
        mobileHtml.Should().Contain("data-style=\"date\"");
    }
}
