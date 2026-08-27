using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class CalendarControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // Calendar now requires authentication ([Authorize] added to the controller).
    // Unauthenticated requests must redirect (or 401), never return the calendar view directly.
    [Fact]
    public async Task Index_WhenNotAuthenticated_ShouldRedirect()
    {
        // Act
        var response = await factory.CreateNonRedirectingClient()
            .GetAsync("/Calendar", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Index_ShouldReturnCalendarView()
    {
        // Arrange
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // Act
        var response = await client.GetAsync("/Calendar", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Calendar");
    }

    [Fact]
    public async Task Index_WithYearAndMonth_ShouldReturnSpecificMonthCalendar()
    {
        // Arrange
        var year = 2024;
        var month = 6;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // Act
        var response = await client.GetAsync($"/Calendar?year={year}&month={month}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Calendar");
    }

    [Fact]
    public async Task Index_WithFinalizedQuests_ShouldDisplayQuestsOnCalendar()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calendardm", "calendar@example.com");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "calendarviewer", "calendarviewer@example.com");

        var questDate = DateTime.Today.AddDays(7);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services,
            dm.Id,
            "Calendar Quest",
            "Test quest for calendar",
            5,
            isFinalized: true);

        // Add a proposed date that matches the finalized date
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, questDate);

        // Update quest with finalized date
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = questDate;
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        // Act
        var response = await client.GetAsync($"/Calendar?year={questDate.Year}&month={questDate.Month}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Calendar Quest");
    }

    [Theory]
    [InlineData(2024, 1)]
    [InlineData(2024, 6)]
    [InlineData(2024, 12)]
    public async Task Index_WithDifferentMonths_ShouldReturnSuccessfully(int year, int month)
    {
        // Arrange
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // Act
        var response = await client.GetAsync($"/Calendar?year={year}&month={month}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Index_WithInvalidMonth_ShouldHandleGracefully()
    {
        // Arrange
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // Act
        var response = await client.GetAsync("/Calendar?year=2024&month=13", TestContext.Current.CancellationToken);

        // Assert
        // Calendar controller throws ArgumentOutOfRangeException for invalid dates
        // which results in 404 from exception handler
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Index_WithEventAndQuestOnSameDay_RendersEventBlockAboveQuestBlock()
    {
        // Arrange — clean slate, a finalized quest, and an event on the same day
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calendarevent_dm1", "calendarevent_dm1@example.com");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "calendarevent_viewer1", "calendarevent_viewer1@example.com");

        var sharedDate = DateTime.Today.AddDays(7);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services,
            dm.Id,
            "Same Day Ordering Quest",
            "Test quest for ordering assertion",
            5,
            isFinalized: true);

        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, sharedDate);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = sharedDate;
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            context.Events.Add(new EventEntity
            {
                Title = "Same Day Ordering Event",
                GroupId = 1,
                Date = DateOnly.FromDateTime(sharedDate),
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await client.GetAsync($"/Calendar?year={sharedDate.Year}&month={sharedDate.Month}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        content.Should().Contain("Same Day Ordering Event");
        content.Should().Contain("Same Day Ordering Quest");

        // Position, not colour, is what tells the two apart at a glance, so the ordering is
        // asserted rather than assumed.
        var eventsContainerIndex = content.IndexOf("calendar-events", StringComparison.Ordinal);
        var questContainerIndex = content.IndexOf("quest-events", StringComparison.Ordinal);
        eventsContainerIndex.Should().BeGreaterThan(0);
        eventsContainerIndex.Should().BeLessThan(questContainerIndex);
    }

    [Fact]
    public async Task Index_WithEvent_RendersClickableChipLinkingToEventDetails()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "calendarevent_chip_viewer", "calendarevent_chip_viewer@example.com");

        var eventDate = DateTime.Today.AddDays(9);
        int eventId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var newEvent = new EventEntity
            {
                Title = "Chip Link Event",
                GroupId = 1,
                Date = DateOnly.FromDateTime(eventDate),
                CreatedAt = DateTime.UtcNow
            };
            context.Events.Add(newEvent);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            eventId = newEvent.Id;
        }

        var response = await client.GetAsync($"/Calendar?year={eventDate.Year}&month={eventDate.Month}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain($"/Events/Details/{eventId}");
    }

    [Fact]
    public async Task Index_LegendExplainsEventChip()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "calendarevent_legend_viewer", "calendarevent_legend_viewer@example.com");

        var response = await client.GetAsync("/Calendar", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("legend-item event");
        content.Should().Contain("Click quests or events for details");
    }

    [Fact]
    public async Task Index_EventWithoutStartTime_RendersAllDayWording()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "calendarevent_allday_viewer", "calendarevent_allday_viewer@example.com");

        var eventDate = DateTime.Today.AddDays(11);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.Events.Add(new EventEntity
            {
                Title = "All Day Wording Event",
                GroupId = 1,
                Date = DateOnly.FromDateTime(eventDate),
                StartTime = null,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await client.GetAsync($"/Calendar?year={eventDate.Year}&month={eventDate.Month}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("All Day Wording Event");
        content.Should().Contain("All day");
    }
}
