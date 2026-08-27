using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

// This is an intentionally-failing scaffold: it targets the Events routes as plain string
// literals so the test project keeps compiling before the controller behind those routes
// exists. Every fact below is expected to return 404 (route not found) until that controller
// lands — that is the deliberate starting state for this suite, not a bug in the tests.
public class EventsControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task Create_Get_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_player_create", "event_player_create@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync("/Events/Create", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Get_DungeonMasterAccess_ShouldSucceed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_create", "event_dm_create@example.com", roles: ["DungeonMaster"]);

        var response = await dmClient.GetAsync("/Events/Create", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_Post_ValidEvent_PersistsAndRedirectsToEventMonth()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_validcreate", "event_dm_validcreate@example.com", roles: ["DungeonMaster"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Wave Zero Feast",
            ["Date"] = "2026-01-17",
            ["StartTime"] = "19:30",
            ["Description"] = "**Bring dice**"
        });

        var response = await dmClient.PostAsync("/Events/Create", formContent, TestContext.Current.CancellationToken);

        // Redirect must land on the calendar at the event's own month (January 2026), not
        // whatever month happens to be current when the test runs.
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("year=2026");
        response.Headers.Location!.OriginalString.Should().Contain("month=1");
    }

    [Fact]
    public async Task Create_Post_WithoutTitle_ReturnsFormWithValidationError()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_notitle", "event_dm_notitle@example.com", roles: ["DungeonMaster"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Date"] = "2026-01-17"
        });

        var response = await dmClient.PostAsync("/Events/Create", formContent, TestContext.Current.CancellationToken);

        // Redisplays the form with a validation error rather than redirecting.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_Post_PastDate_IsAccepted()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_pastdate", "event_dm_pastdate@example.com", roles: ["DungeonMaster"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Backfilled Session",
            ["Date"] = DateTime.Today.AddDays(-45).ToString("yyyy-MM-dd")
        });

        var response = await dmClient.PostAsync("/Events/Create", formContent, TestContext.Current.CancellationToken);

        // An event is a record of something that happened, not a booking for something that
        // will — backfilling a past date must succeed like any other create.
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Create_Post_WithoutStartTime_IsAccepted()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_nostarttime", "event_dm_nostarttime@example.com", roles: ["DungeonMaster"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "All Day Moot",
            ["Date"] = "2026-02-05"
        });

        var response = await dmClient.PostAsync("/Events/Create", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Details_Get_BoardMember_CanRead()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_detailscreator", "event_dm_detailscreator@example.com", roles: ["DungeonMaster"]);

        var createFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Board Council Session",
            ["Date"] = "2026-03-12"
        });
        await dmClient.PostAsync("/Events/Create", createFormContent, TestContext.Current.CancellationToken);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_player_details", "event_player_details@example.com", roles: ["Player"]);

        // Every board member — not just DMs — can read an event's details.
        var response = await playerClient.GetAsync("/Events/Details/1", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Board Council Session");
    }

    [Fact]
    public async Task Edit_Post_ByAnyDmOnBoard_Succeeds()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (creatorDmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_editcreator", "event_dm_editcreator@example.com", roles: ["DungeonMaster"]);

        var createFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Original Meeting Title",
            ["Date"] = "2026-04-08"
        });
        await creatorDmClient.PostAsync("/Events/Create", createFormContent, TestContext.Current.CancellationToken);

        // A different DM on the same board — there is no author column on an event, so any
        // DM on the board may edit any event, not only the one who created it.
        var (otherDmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_editother", "event_dm_editother@example.com", roles: ["DungeonMaster"]);

        var editFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Renamed By Other Dm",
            ["Date"] = "2026-04-08"
        });
        var response = await otherDmClient.PostAsync("/Events/Edit/1", editFormContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Delete_Post_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_player_delete", "event_player_delete@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = "1" });
        var response = await playerClient.PostAsync("/Events/Delete", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Post_ByDm_RedirectsToEventMonth()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_delete", "event_dm_delete@example.com", roles: ["DungeonMaster"]);

        var createFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Event Bound For Deletion",
            ["Date"] = "2026-03-09"
        });
        await dmClient.PostAsync("/Events/Create", createFormContent, TestContext.Current.CancellationToken);

        var deleteFormContent = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = "1" });
        var response = await dmClient.PostAsync("/Events/Delete", deleteFormContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("year=2026");
        response.Headers.Location!.OriginalString.Should().Contain("month=3");
    }

    // A SuperAdmin has no active group by design. RequireActiveGroupId() previously threw
    // unguarded for a SuperAdmin reaching these actions with no board selected — these tests
    // pin down that every one of them now degrades gracefully (a redirect to the group picker
    // on GET, or GroupSessionMiddleware's 409 Conflict on a non-idempotent verb) instead of an
    // unhandled 500.

    [Fact]
    public async Task Create_Get_SuperAdminWithNoActiveGroup_DoesNotThrow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var response = await superAdminClient.GetAsync("/Events/Create", TestContext.Current.CancellationToken);

            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            location.Should().Contain("/groups/pick");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task Create_Post_SuperAdminWithNoActiveGroup_DoesNotThrow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Title"] = "No Board Event",
                ["Date"] = "2026-05-01"
            });

            var response = await superAdminClient.PostAsync("/Events/Create", formContent, TestContext.Current.CancellationToken);

            // A non-idempotent request with no active group never gets silently redirected
            // (that would drop the submitted body) — GroupSessionMiddleware returns 409 Conflict,
            // and if that gate is ever bypassed, EventsController's own write-stamp guard
            // redirects instead of throwing. Either way, it must never be a 500.
            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.Redirect, HttpStatusCode.Found);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task Edit_Post_SuperAdminWithNoActiveGroup_DoesNotThrow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_editnullgroup", "event_dm_editnullgroup@example.com", roles: ["DungeonMaster"]);

        var createFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Event For No-Board Edit",
            ["Date"] = "2026-05-02"
        });
        await dmClient.PostAsync("/Events/Create", createFormContent, TestContext.Current.CancellationToken);

        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var editFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Title"] = "Edited By SuperAdmin With No Board",
                ["Date"] = "2026-05-02"
            });

            var response = await superAdminClient.PostAsync("/Events/Edit/1", editFormContent, TestContext.Current.CancellationToken);

            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.Redirect, HttpStatusCode.Found);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task Details_Get_SuperAdminWithNoActiveGroup_DoesNotThrow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "event_dm_detailsnullgroup", "event_dm_detailsnullgroup@example.com", roles: ["DungeonMaster"]);

        var createFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Event For No-Board Details",
            ["Date"] = "2026-05-03"
        });
        await dmClient.PostAsync("/Events/Create", createFormContent, TestContext.Current.CancellationToken);

        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var response = await superAdminClient.GetAsync("/Events/Details/1", TestContext.Current.CancellationToken);

            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            location.Should().Contain("/groups/pick");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }
}
