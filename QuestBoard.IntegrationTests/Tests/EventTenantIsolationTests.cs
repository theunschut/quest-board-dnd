using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http.Headers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Two-group tenant isolation and write-side scoping tests for the Calendar Events feature.
/// The standard integration harness shares a single mutable group context defaulting to group 1,
/// so an ordinary test is structurally blind to a multi-group leak. These facts genuinely seed a
/// second board and prove one board's events are invisible to the other on both render platforms
/// and through a direct event identifier, that a posted board identifier cannot override the
/// server-side stamp on create, and that a cross-board schedule reference is rejected on edit.
/// </summary>
public class EventTenantIsolationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    // IAsyncLifetime — reset singleton group context after each test class run so that
    // test state does not bleed into subsequently-executed test classes.
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

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

    // Seeds a second board (creating it if needed) and one event on it, returning the seeded
    // event's identifier. This context runs with no active board selected (ActiveGroupId = null
    // on the seeding context), which is exactly what lets it write rows for a board the request
    // pipeline itself could never read or select.
    private async Task<int> SeedOtherBoardEventAsync(string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == 2))
        {
            ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherEventBoard", CreatedAt = DateTime.UtcNow });
        }

        var otherEvent = new EventEntity
        {
            Title = title,
            GroupId = 2,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(otherEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return otherEvent.Id;
    }

    [Fact]
    public async Task GroupFilter_HidesEventFromOtherGroupOnDesktopCalendar()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var eventDate = DateOnly.FromDateTime(DateTime.Today);
        await SeedOtherBoardEventAsync("GroupTwoCouncil", eventDate);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.Events.Add(new EventEntity
            {
                Title = "GroupOneCouncil",
                GroupId = 1,
                Date = eventDate,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventiso_desktop_viewer", "eventiso_desktop_viewer@example.com");

        var response = await client.GetAsync(
            $"/Calendar?year={eventDate.Year}&month={eventDate.Month}", TestContext.Current.CancellationToken);

        // The positive assertion below is what stops this fact passing on a blank or errored page.
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("GroupTwoCouncil");
        body.Should().Contain("GroupOneCouncil");
    }

    [Fact]
    public async Task GroupFilter_ShowsEventFromSameGroup()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var eventDate = DateOnly.FromDateTime(DateTime.Today);
        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.Events.Add(new EventEntity
            {
                Title = "GroupOneOnlyCouncil",
                GroupId = 1,
                Date = eventDate,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventiso_sameboard_viewer", "eventiso_sameboard_viewer@example.com");

        var response = await client.GetAsync(
            $"/Calendar?year={eventDate.Year}&month={eventDate.Month}", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("GroupOneOnlyCouncil");
    }

    [Fact]
    public async Task GroupFilter_HidesEventFromOtherGroupOnMobileAgenda()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var eventDate = DateOnly.FromDateTime(DateTime.Today);
        await SeedOtherBoardEventAsync("GroupTwoMobileCouncil", eventDate);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.Events.Add(new EventEntity
            {
                Title = "GroupOneMobileCouncil",
                GroupId = 1,
                Date = eventDate,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventiso_mobile_viewer", "eventiso_mobile_viewer@example.com");

        var (response, body) = await GetWithUserAgentAsync(
            $"/Calendar?year={eventDate.Year}&month={eventDate.Month}",
            MobileUserAgent,
            client.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain("GroupTwoMobileCouncil");
        body.Should().Contain("GroupOneMobileCouncil");
    }

    [Fact]
    public async Task Details_EventFromOtherGroup_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var seededId = await SeedOtherBoardEventAsync(
            "HiddenDetailsEvent", DateOnly.FromDateTime(DateTime.Today));

        // A not-found response here is deliberate: it is indistinguishable from a non-existent
        // event and therefore reveals nothing about the other board.
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventiso_details_viewer", "eventiso_details_viewer@example.com");

        var response = await client.GetAsync($"/Events/Details/{seededId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_Post_EventFromOtherGroup_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var seededId = await SeedOtherBoardEventAsync(
            "HiddenEditEvent", DateOnly.FromDateTime(DateTime.Today));

        // The Dungeon Master policy passes here, so this fact isolates tenant scoping from role
        // authorization: a DM on the wrong board still gets a not-found response.
        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventiso_edit_dm", "eventiso_edit_dm@example.com", roles: ["DungeonMaster"]);

        var editFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = seededId.ToString(),
            ["Title"] = "Renamed From Board One",
            ["Date"] = DateTime.Today.ToString("yyyy-MM-dd")
        });
        var response = await dmClient.PostAsync(
            $"/Events/Edit/{seededId}", editFormContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Post_EventFromOtherGroup_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var seededId = await SeedOtherBoardEventAsync(
            "HiddenDeleteEvent", DateOnly.FromDateTime(DateTime.Today));

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventiso_delete_dm", "eventiso_delete_dm@example.com", roles: ["DungeonMaster"]);

        var deleteFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = seededId.ToString()
        });
        var response = await dmClient.PostAsync("/Events/Delete", deleteFormContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The row must still exist on the other board — a rejected write, not a silently
        // successful one that merely failed to redirect correctly. The query filter is
        // fail-closed (a null ActiveGroupId sees nothing, not everything), so board 2 must be
        // made active to observe its own row.
        factory.TestGroupContext.ActiveGroupId = 2;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var stillExists = context.Events.Any(e => e.Id == seededId);
            stillExists.Should().BeTrue();
        }
        factory.TestGroupContext.ActiveGroupId = 1;
    }

    [Fact]
    public async Task Create_Post_PostedGroupIdIsIgnored_ServerStampsActiveBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventiso_create_dm", "eventiso_create_dm@example.com", roles: ["DungeonMaster"]);

        // The read filter offers no protection at all on an insert, so this posted GroupId is
        // the only thing standing between a form field and a cross-board write.
        var createFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Spoofed Board Event",
            ["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["GroupId"] = "2"
        });
        var response = await dmClient.PostAsync("/Events/Create", createFormContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = context.Events.Where(e => e.Title == "Spoofed Board Event").ToList();

        persisted.Should().ContainSingle();
        persisted[0].GroupId.Should().Be(1);
    }

    [Fact]
    public async Task Edit_Post_EventPointingAtAnotherBoardSchedule_ReturnsBadRequest()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        int eventIdWithForeignSchedule;
        await using (var ctx = factory.Database.CreateContext())
        {
            if (!ctx.Groups.Any(g => g.Id == 2))
            {
                ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherEventBoard", CreatedAt = DateTime.UtcNow });
            }

            var otherBoardSeries = new EventSeriesEntity
            {
                GroupId = 2,
                AnchorDate = DateOnly.FromDateTime(DateTime.Today),
                IntervalWeeks = 1,
                WeekDay = 0,
                CreatedAt = DateTime.UtcNow
            };
            ctx.EventSeries.Add(otherBoardSeries);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

            var eventOnOwnBoard = new EventEntity
            {
                Title = "Event Pointing At Foreign Schedule",
                GroupId = 1,
                Date = DateOnly.FromDateTime(DateTime.Today),
                SeriesId = otherBoardSeries.Id,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Events.Add(eventOnOwnBoard);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

            eventIdWithForeignSchedule = eventOnOwnBoard.Id;
        }

        // The read filter already hides the other board's schedule row, and the controller's
        // explicit comparison is a second, independent layer; this fact fails if either is
        // removed.
        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "eventiso_schedule_dm", "eventiso_schedule_dm@example.com", roles: ["DungeonMaster"]);

        var editFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = eventIdWithForeignSchedule.ToString(),
            ["Title"] = "Attempted Rename",
            ["Date"] = DateTime.Today.ToString("yyyy-MM-dd")
        });
        var response = await dmClient.PostAsync(
            $"/Events/Edit/{eventIdWithForeignSchedule}", editFormContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
