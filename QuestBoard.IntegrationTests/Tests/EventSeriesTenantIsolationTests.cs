using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http.Headers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Two-group tenant isolation and write-side scoping tests for the recurring event series
/// feature. The standard integration harness shares a single mutable group context defaulting
/// to group 1, so an ordinary test is structurally blind to a multi-group leak. These facts
/// genuinely seed a second board and prove a series and its occurrences are invisible to the
/// other board on every read surface this phase adds, that every mutating action this phase
/// adds rejects a cross-board identifier through the real pipeline, that a posted board
/// identifier cannot override the server-side stamp on a recurring create, and that the two
/// server-side refusals the phase depends on hold when posted directly rather than merely being
/// hidden in markup.
/// </summary>
public class EventSeriesTenantIsolationTests(WebApplicationFactoryBase factory)
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

    // Seeds a second board (creating it if needed), one series and a chosen number of live
    // occurrences on it, through a context with no active board selected. This context runs
    // with no active board selected (ActiveGroupId = null on the seeding context), which is
    // exactly what lets it write rows for a board the request pipeline itself could never read
    // or select. Every occurrence gets a distinct slot index so the unique idempotency index is
    // satisfied, and the series' template fields are filled so the rows are realistic.
    private async Task<(int SeriesId, IReadOnlyList<int> EventIds)> SeedOtherBoardSeriesAsync(
        string seriesTitle, DateOnly anchorDate, int occurrenceCount)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == 2))
        {
            ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherSeriesBoard", CreatedAt = DateTime.UtcNow });
        }

        var series = new EventSeriesEntity
        {
            Title = seriesTitle,
            Description = "Weekly session on the other board.",
            StartTime = new TimeOnly(19, 0),
            AnchorDate = anchorDate,
            IntervalWeeks = 1,
            WeekDay = (int)anchorDate.DayOfWeek,
            CycleMask = "1",
            EndDate = null,
            GroupId = 2,
            CreatedAt = DateTime.UtcNow
        };
        ctx.EventSeries.Add(series);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        // The seeding context has no active board selected, which means its own query filter
        // hides everything -- including the rows just inserted. Every id below is therefore
        // captured straight off the tracked entity (populated by the identity generator on
        // save) rather than through a query that the filter would silently empty out.
        var occurrences = new List<EventEntity>();
        for (var slot = 0; slot < occurrenceCount; slot++)
        {
            var occurrence = new EventEntity
            {
                Title = seriesTitle,
                Description = series.Description,
                GroupId = 2,
                Date = anchorDate.AddDays(slot * series.IntervalWeeks * 7),
                StartTime = series.StartTime,
                SeriesId = series.Id,
                SeriesSlotIndex = slot,
                CreatedAt = DateTime.UtcNow
            };
            occurrences.Add(occurrence);
            ctx.Events.Add(occurrence);
        }
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var seededIds = occurrences
            .OrderBy(e => e.SeriesSlotIndex)
            .Select(e => e.Id)
            .ToList();

        return (series.Id, seededIds);
    }

    [Fact]
    public async Task GroupFilter_HidesSeriesFromOtherGroupOnDesktopCalendar()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var anchorDate = DateOnly.FromDateTime(DateTime.Today);
        await SeedOtherBoardSeriesAsync("GroupTwoCouncilSeries", anchorDate, 3);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.Events.Add(new EventEntity
            {
                Title = "GroupOneOwnCouncil",
                GroupId = 1,
                Date = anchorDate,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_desktop_viewer", "seriesiso_desktop_viewer@example.com");

        var response = await client.GetAsync(
            $"/Calendar?year={anchorDate.Year}&month={anchorDate.Month}", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("GroupTwoCouncilSeries");
        body.Should().Contain("GroupOneOwnCouncil");
    }

    [Fact]
    public async Task GroupFilter_HidesSeriesFromOtherGroupOnMobileAgenda()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var anchorDate = DateOnly.FromDateTime(DateTime.Today);
        await SeedOtherBoardSeriesAsync("GroupTwoMobileSeries", anchorDate, 3);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.Events.Add(new EventEntity
            {
                Title = "GroupOneMobileCouncil",
                GroupId = 1,
                Date = anchorDate,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_mobile_viewer", "seriesiso_mobile_viewer@example.com");

        var (response, body) = await GetWithUserAgentAsync(
            $"/Calendar?year={anchorDate.Year}&month={anchorDate.Month}",
            MobileUserAgent,
            client.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain("GroupTwoMobileSeries");
        body.Should().Contain("GroupOneMobileCouncil");
    }

    [Fact]
    public async Task Details_OccurrenceFromOtherGroup_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (_, eventIds) = await SeedOtherBoardSeriesAsync(
            "HiddenOccurrenceDetailsSeries", DateOnly.FromDateTime(DateTime.Today), 1);

        // A not-found response here is deliberate: it is indistinguishable from a non-existent
        // occurrence and therefore reveals nothing about the other board.
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_occurrence_details_viewer", "seriesiso_occurrence_details_viewer@example.com");

        var response = await client.GetAsync($"/Events/Details/{eventIds[0]}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_SeriesFromOtherGroup_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (seriesId, _) = await SeedOtherBoardSeriesAsync(
            "HiddenSeriesDetailsSeries", DateOnly.FromDateTime(DateTime.Today), 1);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_series_details_viewer", "seriesiso_series_details_viewer@example.com");

        var response = await client.GetAsync($"/Series/Details/{seriesId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cancel_Post_OccurrenceFromOtherGroup_DoesNotSucceedAndLeavesRowUnchanged()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (_, eventIds) = await SeedOtherBoardSeriesAsync(
            "HiddenCancelSeries", DateOnly.FromDateTime(DateTime.Today), 1);
        var otherOccurrenceId = eventIds[0];

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_cancel_dm", "seriesiso_cancel_dm@example.com", roles: ["DungeonMaster"]);

        var cancelFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = otherOccurrenceId.ToString()
        });
        var response = await dmClient.PostAsync("/Events/Cancel", cancelFormContent, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeFalse();
        response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);

        // The query filter is fail-closed (a null ActiveGroupId sees nothing, not everything),
        // so board 2 must be made active to read its own row back.
        factory.TestGroupContext.ActiveGroupId = 2;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var stillUncancelled = context.Events.Single(e => e.Id == otherOccurrenceId);
            stillUncancelled.CancelledAt.Should().BeNull();
        }
        factory.TestGroupContext.ActiveGroupId = 1;
    }

    [Fact]
    public async Task End_Post_SeriesFromOtherGroup_DoesNotSucceedAndLeavesSeriesAndOccurrencesUnchanged()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (otherSeriesId, eventIds) = await SeedOtherBoardSeriesAsync(
            "HiddenEndSeries", DateOnly.FromDateTime(DateTime.Today), 3);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_end_dm", "seriesiso_end_dm@example.com", roles: ["DungeonMaster"]);

        var endFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = otherSeriesId.ToString()
        });
        var response = await dmClient.PostAsync("/Series/End", endFormContent, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeFalse();
        response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);

        factory.TestGroupContext.ActiveGroupId = 2;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var series = context.EventSeries.Single(s => s.Id == otherSeriesId);
            series.EndDate.Should().BeNull();
            context.Events.Count(e => e.SeriesId == otherSeriesId).Should().Be(eventIds.Count);
        }
        factory.TestGroupContext.ActiveGroupId = 1;
    }

    [Fact]
    public async Task Delete_Post_SeriesFromOtherGroup_DoesNotSucceedAndLeavesSeriesAndOccurrencesPresent()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (otherSeriesId, eventIds) = await SeedOtherBoardSeriesAsync(
            "HiddenDeleteSeries", DateOnly.FromDateTime(DateTime.Today), 3);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_delete_dm", "seriesiso_delete_dm@example.com", roles: ["DungeonMaster"]);

        var deleteFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = otherSeriesId.ToString()
        });
        var response = await dmClient.PostAsync("/Series/Delete", deleteFormContent, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeFalse();
        response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);

        factory.TestGroupContext.ActiveGroupId = 2;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.EventSeries.Any(s => s.Id == otherSeriesId).Should().BeTrue();
            context.Events.Count(e => e.SeriesId == otherSeriesId).Should().Be(eventIds.Count);
        }
        factory.TestGroupContext.ActiveGroupId = 1;
    }

    [Fact]
    public async Task Detach_Post_SeriesFromOtherGroup_DoesNotSucceedAndLeavesSeriesAndOccurrencesPresent()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (otherSeriesId, eventIds) = await SeedOtherBoardSeriesAsync(
            "HiddenDetachSeries", DateOnly.FromDateTime(DateTime.Today), 3);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_detach_dm", "seriesiso_detach_dm@example.com", roles: ["DungeonMaster"]);

        var detachFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = otherSeriesId.ToString()
        });
        var response = await dmClient.PostAsync("/Series/Detach", detachFormContent, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeFalse();
        response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);

        factory.TestGroupContext.ActiveGroupId = 2;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.EventSeries.Any(s => s.Id == otherSeriesId).Should().BeTrue();
            // A successful detach would clear SeriesId on every occurrence -- still series-linked
            // proves the write never landed.
            context.Events.Count(e => e.SeriesId == otherSeriesId).Should().Be(eventIds.Count);
        }
        factory.TestGroupContext.ActiveGroupId = 1;
    }

    [Fact]
    public async Task Edit_Post_FutureScopeForOccurrenceFromOtherGroup_DoesNotSucceedAndRewritesNothing()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (_, eventIds) = await SeedOtherBoardSeriesAsync(
            "HiddenEditScopeSeries", DateOnly.FromDateTime(DateTime.Today), 2);
        var otherOccurrenceId = eventIds[0];

        // Read the original title back before attempting the edit so the assertion can prove
        // nothing changed rather than merely that the response failed.
        string originalTitle;
        factory.TestGroupContext.ActiveGroupId = 2;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            originalTitle = context.Events.Single(e => e.Id == otherOccurrenceId).Title;
        }

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_editscope_dm", "seriesiso_editscope_dm@example.com", roles: ["DungeonMaster"]);

        var editFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = otherOccurrenceId.ToString(),
            ["Title"] = "Attempted Cross-Board Future Rename",
            ["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["EditScope"] = "ThisAndFutureEvents"
        });
        var response = await dmClient.PostAsync(
            $"/Events/Edit/{otherOccurrenceId}", editFormContent, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeFalse();
        response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);

        factory.TestGroupContext.ActiveGroupId = 2;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var stillOriginal = context.Events.Single(e => e.Id == otherOccurrenceId);
            stillOriginal.Title.Should().Be(originalTitle);
        }
        factory.TestGroupContext.ActiveGroupId = 1;
    }

    [Fact]
    public async Task Create_Post_RecurringWithPostedGroupIdIsIgnored_ServerStampsActiveBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_create_dm", "seriesiso_create_dm@example.com", roles: ["DungeonMaster"]);

        // The read filter offers no protection at all on an insert, so this posted GroupId is
        // the only thing standing between a form field and a cross-board write, exactly as the
        // one-off event's own spoofed-create fact already proves for the non-recurring path.
        var createFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Spoofed Board Series",
            ["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["GroupId"] = "2",
            ["IsRecurring"] = "true",
            ["IntervalWeeks"] = "1",
            ["CycleMask"] = "1"
        });
        var response = await dmClient.PostAsync("/Events/Create", createFormContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        factory.TestGroupContext.ActiveGroupId = 1;
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedSeries = context.EventSeries.Where(s => s.Title == "Spoofed Board Series").ToList();

        persistedSeries.Should().ContainSingle();
        persistedSeries[0].GroupId.Should().Be(1);

        var persistedOccurrences = context.Events.Where(e => e.SeriesId == persistedSeries[0].Id).ToList();
        persistedOccurrences.Should().NotBeEmpty();
        persistedOccurrences.Should().OnlyContain(e => e.GroupId == 1);
    }

    [Fact]
    public async Task Delete_Post_SeriesOccurrenceOnActiveBoard_ReturnsBadRequestAndOccurrenceStillExists()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        int occurrenceId;
        int seriesId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var series = new EventSeriesEntity
            {
                Title = "OwnBoardDeleteRefusalSeries",
                AnchorDate = DateOnly.FromDateTime(DateTime.Today),
                IntervalWeeks = 1,
                WeekDay = (int)DateTime.Today.DayOfWeek,
                CycleMask = "1",
                GroupId = 1,
                CreatedAt = DateTime.UtcNow
            };
            ctx.EventSeries.Add(series);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

            var occurrence = new EventEntity
            {
                Title = series.Title,
                GroupId = 1,
                Date = series.AnchorDate,
                SeriesId = series.Id,
                SeriesSlotIndex = 0,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Events.Add(occurrence);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

            occurrenceId = occurrence.Id;
            seriesId = series.Id;
        }

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_ownboard_delete_dm", "seriesiso_ownboard_delete_dm@example.com", roles: ["DungeonMaster"]);

        var deleteFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = occurrenceId.ToString()
        });
        var response = await dmClient.PostAsync("/Events/Delete", deleteFormContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        context.Events.Any(e => e.Id == occurrenceId && e.SeriesId == seriesId).Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_Post_OneOffEventOnActiveBoard_ReturnsBadRequestAndCancelledMarkerStillUnset()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        int eventId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var oneOffEvent = new EventEntity
            {
                Title = "OwnBoardCancelRefusalOneOff",
                GroupId = 1,
                Date = DateOnly.FromDateTime(DateTime.Today),
                CreatedAt = DateTime.UtcNow
            };
            ctx.Events.Add(oneOffEvent);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

            eventId = oneOffEvent.Id;
        }

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "seriesiso_ownboard_cancel_dm", "seriesiso_ownboard_cancel_dm@example.com", roles: ["DungeonMaster"]);

        var cancelFormContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = eventId.ToString()
        });
        var response = await dmClient.PostAsync("/Events/Cancel", cancelFormContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        context.Events.Single(e => e.Id == eventId).CancelledAt.Should().BeNull();
    }
}
