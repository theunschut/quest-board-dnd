using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http.Headers;

namespace QuestBoard.IntegrationTests.Controllers;

// This file proves two negatives that are easy to lose silently as later plans in this feature
// land: that the quest detail pages never render event markup even when an event exists on the
// same board and same day, and that quest creation is completely untouched by the presence of
// events. Both facts POST to /Events/Create as a plain route literal, so this file has no
// compile-time dependency on the controller behind it and is expected to fail (404) until that
// controller exists — that is the deliberate starting state for this suite, not a bug in the
// tests.
public class EventCalendarPartialTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // Single source of truth for the calendar event chip's markup identity, so a rename of
    // either CSS class only needs to be updated in one place for these assertions to keep
    // meaning what they say.
    private const string EventBlockClass = "calendar-events";
    private const string EventChipClass = "calendar-event";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(
        string url, string userAgent, AuthenticationHeaderValue? authorization)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (authorization != null)
        {
            request.Headers.Authorization = authorization;
        }
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    [Fact]
    public async Task QuestDetails_WithSameDayEventOnSameBoard_RendersNoEventMarkup()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventcal_dm_desktop", "eventcal_dm_desktop@example.com", roles: ["DungeonMaster"]);

        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dmUser.Id, "Same Day Test Quest");

        // A proposed date, not a finalized one, is what makes Quest Details actually render its
        // calendar partial for the DM viewing their own quest — a finalized quest's Details page
        // shows a "Quest Finalized!" summary instead and never calls the calendar partial at
        // all, which would make this assertion pass vacuously regardless of whether event
        // markup leaks through.
        var eventDate = DateTime.Today.AddDays(10);
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, eventDate);

        var eventFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Same Day Council Meeting",
            ["Date"] = eventDate.ToString("yyyy-MM-dd")
        });
        var eventCreateResponse = await dmClient.PostAsync("/Events/Create", eventFormContent, TestContext.Current.CancellationToken);
        // Fails loudly instead of silently passing the assertions below on a same-day event
        // that never actually got created.
        eventCreateResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var response = await dmClient.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        content.Should().NotContain("Same Day Council Meeting");
        content.Should().NotContain(EventBlockClass);
        content.Should().NotContain(EventChipClass);
        // Proves the page actually rendered its own content rather than an error page the
        // NotContain assertions above would otherwise pass against for free.
        content.Should().Contain("Same Day Test Quest");
    }

    [Fact]
    public async Task QuestDetailsMobile_WithSameDayEventOnSameBoard_RendersNoEventMarkup()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventcal_dm_mobile", "eventcal_dm_mobile@example.com", roles: ["DungeonMaster"]);

        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dmUser.Id, "Same Day Test Quest Mobile");

        var eventDate = DateTime.Today.AddDays(11);
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, eventDate);

        var eventFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Same Day Council Meeting Mobile",
            ["Date"] = eventDate.ToString("yyyy-MM-dd")
        });
        var eventCreateResponse = await dmClient.PostAsync("/Events/Create", eventFormContent, TestContext.Current.CancellationToken);
        eventCreateResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var (response, content) = await GetWithUserAgentAsync(
            $"/Quest/Details/{quest.Id}", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        content.Should().NotContain("Same Day Council Meeting Mobile");
        content.Should().NotContain(EventBlockClass);
        content.Should().NotContain(EventChipClass);
        content.Should().Contain("Same Day Test Quest Mobile");
    }

    [Fact]
    public async Task QuestCreate_WithExistingEventOnChosenDate_SucceedsUnchanged()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventcal_dm_questcreate", "eventcal_dm_questcreate@example.com", roles: ["DungeonMaster"]);

        var sharedDate = DateTime.Today.AddDays(21);

        var eventFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Existing Board Event",
            ["Date"] = sharedDate.ToString("yyyy-MM-dd")
        });
        var eventCreateResponse = await dmClient.PostAsync("/Events/Create", eventFormContent, TestContext.Current.CancellationToken);
        // Fails loudly instead of silently passing the "unaffected" assertion below against an
        // event that never actually got created.
        eventCreateResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var questFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Quest Sharing An Event Date",
            ["Description"] = "A quest proposed on a day that already has an event",
            ["ChallengeRating"] = "3",
            ["ProposedDates[0]"] = sharedDate.ToString("yyyy-MM-ddTHH:mm")
        });

        var response = await dmClient.PostAsync("/Quest/Create", questFormContent, TestContext.Current.CancellationToken);

        // Quest creation must be completely unaware of events — no validation, no warning, no
        // blocking. A Redirect (not a redisplayed 200 form, and not a 400) is the only outcome
        // that proves the creation actually succeeded rather than being silently rejected.
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = context.Quests.FirstOrDefault(q => q.Title == "Quest Sharing An Event Date");

        // This proves an absence that must keep holding for the life of the feature: nothing in
        // the quest creation path may inspect events, so this row's existence is the whole test.
        persisted.Should().NotBeNull();
    }
}
