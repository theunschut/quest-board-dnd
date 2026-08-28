using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

// Exercises EventsController's CRUD routes, its role-gated access checks, and the SuperAdmin
// no-active-group edge case, all via plain string route literals rather than direct controller
// references.
public class EventsControllerIntegrationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    // IAsyncLifetime -- reset the shared singleton group context after every fact so a
    // campaign-board flag or a null active group set by one fact can never leak into whichever
    // fact runs next, in this class or any other.
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

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

    // Availability lifecycle, ownership, board-type and past-date facts for SetAvailability
    // and Withdraw. One-shot facts prove a signup row exists only once a player has clicked;
    // campaign facts prove every member holds an automatic row the instant an event exists and
    // that opting out flips that row rather than deleting it; ownership facts prove a write
    // changes only the acting user's row even when a request carries a field naming someone
    // else; and the past-date facts prove an event stays answerable long after it happened.

    // Seeds an event directly on group 1 through the unfiltered seeding context, bypassing the
    // request pipeline and its query filter entirely, and returns the new event's id.
    private async Task<int> SeedGroupOneEventAsync(string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = 1,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    // Looks an already-seeded event back up by title through the unfiltered seeding context --
    // used after a Create post, whose response never hands the caller the new event's id.
    private async Task<int> GetEventIdByTitleAsync(string title)
    {
        await using var ctx = factory.Database.CreateContext();
        return ctx.Events.IgnoreQueryFilters().Single(e => e.Title == title).Id;
    }

    // Reads a single member's signup row for an event back through the unfiltered seeding
    // context, so an assertion never depends on the request pipeline's own query filter -- a
    // filtered read cannot distinguish "no row was written" from "a row exists but is hidden".
    private async Task<EventSignupEntity?> GetSignupAsync(int eventId, int userId)
    {
        await using var ctx = factory.Database.CreateContext();
        return await ctx.EventSignups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(es => es.EventId == eventId && es.UserId == userId, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SetAvailability_OneShot_NoExistingRow_CreatesRowForActingUser()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedGroupOneEventAsync("One Shot Opt In", DateOnly.FromDateTime(DateTime.Today));
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_oneshot_optin", "avail_oneshot_optin@example.com");

        (await GetSignupAsync(eventId, user.Id)).Should().BeNull();

        var response = await client.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Yes" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await GetSignupAsync(eventId, user.Id);
        after.Should().NotBeNull();
        after!.Availability.Should().Be((int)VoteType.Yes);
        after.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SetAvailability_OneShot_MaybeThenNo_UpdatesSameRowRatherThanCreatingMore()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedGroupOneEventAsync("One Shot Changed Mind", DateOnly.FromDateTime(DateTime.Today));
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_oneshot_change", "avail_oneshot_change@example.com");

        await client.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Maybe" }),
            TestContext.Current.CancellationToken);
        (await GetSignupAsync(eventId, user.Id))!.Availability.Should().Be((int)VoteType.Maybe);

        var response = await client.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "No" }),
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.Database.CreateContext();
        var rows = await ctx.EventSignups.IgnoreQueryFilters()
            .Where(es => es.EventId == eventId && es.UserId == user.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().ContainSingle();
        rows[0].Availability.Should().Be((int)VoteType.No);
    }

    [Fact]
    public async Task Withdraw_OneShot_RemovesRow_ReturnsPlayerToNotAnswered()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedGroupOneEventAsync("One Shot Withdraw", DateOnly.FromDateTime(DateTime.Today));
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_oneshot_withdraw", "avail_oneshot_withdraw@example.com");

        await client.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Yes" }),
            TestContext.Current.CancellationToken);
        (await GetSignupAsync(eventId, user.Id)).Should().NotBeNull();

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/Events/Withdraw/{eventId}");
        var response = await client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetSignupAsync(eventId, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Create_CampaignBoard_AutoSignsUpEveryMember_WithNullAnsweredTimestamp()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        // The board type here resolves through the request pipeline's own IBoardTypeResolver
        // stub, which is exactly what the create-time fan-out reads -- no real GroupEntity row
        // needs to say Campaign for this fact.
        factory.TestGroupContext.BoardType = BoardType.Campaign;

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_campaign_dm", "avail_campaign_dm@example.com", roles: ["DungeonMaster"]);
        var member2 = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "avail_campaign_member2", "avail_campaign_member2@example.com");
        var member3 = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "avail_campaign_member3", "avail_campaign_member3@example.com");

        await using (var seedCtx = factory.Database.CreateContext())
        {
            seedCtx.UserGroups.Add(new UserGroupEntity { UserId = member2.Id, GroupId = 1, GroupRole = (int)GroupRole.Player });
            seedCtx.UserGroups.Add(new UserGroupEntity { UserId = member3.Id, GroupId = 1, GroupRole = (int)GroupRole.Player });
            await seedCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var createFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Campaign Fan Out Session",
            ["Date"] = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd")
        });
        var response = await dmClient.PostAsync("/Events/Create", createFormContent, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var eventId = await GetEventIdByTitleAsync("Campaign Fan Out Session");
        await using var assertCtx = factory.Database.CreateContext();
        var signups = assertCtx.EventSignups.IgnoreQueryFilters()
            .Where(s => s.EventId == eventId)
            .ToList();

        signups.Should().HaveCount(3);
        signups.Should().OnlyContain(s => s.Availability == (int)VoteType.Yes);
        // An automatic row must never look like an answer someone actually gave.
        signups.Should().OnlyContain(s => s.UpdatedAt == null);
    }

    [Fact]
    public async Task SetAvailability_CampaignBoard_OptOut_FlipsAutoRowToNo_WithoutDeletingIt()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.BoardType = BoardType.Campaign;

        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_campaign_optout_dm", "avail_campaign_optout_dm@example.com", roles: ["DungeonMaster"]);
        var (memberClient, memberUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_campaign_optout_member", "avail_campaign_optout_member@example.com");

        var createFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Campaign Opt Out Session",
            ["Date"] = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd")
        });
        await dmClient.PostAsync("/Events/Create", createFormContent, TestContext.Current.CancellationToken);
        var eventId = await GetEventIdByTitleAsync("Campaign Opt Out Session");

        var autoRow = await GetSignupAsync(eventId, memberUser.Id);
        autoRow.Should().NotBeNull();
        autoRow!.UpdatedAt.Should().BeNull();

        var response = await memberClient.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "No" }),
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.Database.CreateContext();
        var rowCountAfter = ctx.EventSignups.IgnoreQueryFilters().Count(s => s.EventId == eventId);
        rowCountAfter.Should().Be(2); // the DM's auto row plus the member's flipped row -- nothing added, nothing removed

        var updatedRow = await GetSignupAsync(eventId, memberUser.Id);
        updatedRow!.Availability.Should().Be((int)VoteType.No);
        updatedRow.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Withdraw_CampaignBoard_IsRefused_RowSurvives()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.BoardType = BoardType.Campaign;

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_campaign_withdraw_dm", "avail_campaign_withdraw_dm@example.com", roles: ["DungeonMaster"]);

        var createFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Campaign Withdraw Refusal Session",
            ["Date"] = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd")
        });
        await dmClient.PostAsync("/Events/Create", createFormContent, TestContext.Current.CancellationToken);
        var eventId = await GetEventIdByTitleAsync("Campaign Withdraw Refusal Session");

        (await GetSignupAsync(eventId, dmUser.Id)).Should().NotBeNull();

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/Events/Withdraw/{eventId}");
        var response = await dmClient.SendAsync(deleteRequest, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetSignupAsync(eventId, dmUser.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task SetAvailability_Ownership_EachMemberChangesOnlyTheirOwnRow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedGroupOneEventAsync("Ownership Shared Event", DateOnly.FromDateTime(DateTime.Today));

        var (clientA, userA) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_ownership_a", "avail_ownership_a@example.com");
        var (clientB, userB) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_ownership_b", "avail_ownership_b@example.com");

        await clientA.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Yes" }),
            TestContext.Current.CancellationToken);
        await clientB.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "No" }),
            TestContext.Current.CancellationToken);

        var response = await clientA.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Maybe" }),
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rowA = await GetSignupAsync(eventId, userA.Id);
        var rowB = await GetSignupAsync(eventId, userB.Id);
        rowA!.Availability.Should().Be((int)VoteType.Maybe);
        rowB!.Availability.Should().Be((int)VoteType.No);
    }

    [Fact]
    public async Task SetAvailability_Ownership_SpoofedUserIdField_OnlyChangesActingUsersRow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedGroupOneEventAsync("Ownership Spoofing Target", DateOnly.FromDateTime(DateTime.Today));

        var (actingClient, actingUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_spoof_actor", "avail_spoof_actor@example.com");
        var (_, otherUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_spoof_target", "avail_spoof_target@example.com");

        // SetAvailability has no user, member or signup identifier parameter at all -- these
        // extra fields are inert. This fact fails loudly if a parameter matching one of them is
        // ever added to the action signature.
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["availability"] = "Yes",
            ["userId"] = otherUser.Id.ToString(),
            ["UserId"] = otherUser.Id.ToString()
        });
        var response = await actingClient.PostAsync($"/Events/SetAvailability/{eventId}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetSignupAsync(eventId, actingUser.Id)).Should().NotBeNull();
        (await GetSignupAsync(eventId, otherUser.Id)).Should().BeNull();
    }

    [Fact]
    public async Task SetAvailability_InvalidValue_ReturnsBadRequest_WritesNoRow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedGroupOneEventAsync("Invalid Value Event", DateOnly.FromDateTime(DateTime.Today));
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_invalid_value", "avail_invalid_value@example.com");

        var response = await client.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "99" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetSignupAsync(eventId, user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task SetAvailability_UnknownEvent_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_unknown_event", "avail_unknown_event@example.com");

        var response = await client.PostAsync("/Events/SetAvailability/999999",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Yes" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetAvailability_PastDatedEvent_AcceptsChangedAnswer()
    {
        // Past-dated events are allowed to exist -- an event here is a record of a session
        // rather than a booking, so correcting who was actually available after the fact is a
        // legitimate action and is deliberately permitted.
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedGroupOneEventAsync(
            "Past Dated Correction", DateOnly.FromDateTime(DateTime.Today.AddDays(-30)));
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_pastdate_answer", "avail_pastdate_answer@example.com");

        var response = await client.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Yes" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetSignupAsync(eventId, user.Id))!.Availability.Should().Be((int)VoteType.Yes);
    }

    [Fact]
    public async Task Withdraw_PastDatedOneShotEvent_Succeeds()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedGroupOneEventAsync(
            "Past Dated Withdraw", DateOnly.FromDateTime(DateTime.Today.AddDays(-30)));
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "avail_pastdate_withdraw", "avail_pastdate_withdraw@example.com");

        await client.PostAsync($"/Events/SetAvailability/{eventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Yes" }),
            TestContext.Current.CancellationToken);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/Events/Withdraw/{eventId}");
        var response = await client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetSignupAsync(eventId, user.Id)).Should().BeNull();
    }
}
