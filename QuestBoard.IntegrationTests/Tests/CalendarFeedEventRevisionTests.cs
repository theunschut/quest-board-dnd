using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;
using System.Globalization;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves, through the real controllers a Dungeon Master and a member use and the real anonymous
/// feed a calendar client polls, that every write which changes a board event reaches a
/// subscriber as a newer revision of the same entry: the edit form, the series this-and-future
/// sweep, and cancel followed by restore. It also proves the other half of the rule, that a save
/// which changes nothing the feed shows leaves the reader's document byte-identical, and that a
/// reader's own availability answer moves that reader's stamp and nothing else.
///
/// Facts compare whole entry blocks only across two fetches and otherwise read single lines, so
/// they hold however many properties the writer adds to an entry.
/// </summary>
public class CalendarFeedEventRevisionTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    // Every seeded row carries this fixed past instant. A save made within the same wall-clock
    // second as a seed would otherwise produce the same second-granularity stamp and make
    // "later" depend on timing.
    private static readonly DateTime SeedInstant = new(2026, 1, 5, 9, 30, 0, DateTimeKind.Utc);

    private const string StampFormat = "yyyyMMdd'T'HHmmss'Z'";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    // ---- seeding ----

    private async Task<int> SeedEventAsync(
        string title, DateOnly date, TimeOnly? startTime, int? seriesId = null, int? slotIndex = null,
        string? description = null)
    {
        await using var ctx = factory.Database.CreateContext();
        var entity = new EventEntity
        {
            Title = title,
            Description = description,
            GroupId = 1,
            Date = date,
            StartTime = startTime,
            SeriesId = seriesId,
            SeriesSlotIndex = slotIndex,
            CreatedAt = SeedInstant
        };
        ctx.Events.Add(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity.Id;
    }

    private async Task<int> SeedSeriesAsync(string title, string description, DateOnly anchor, TimeOnly startTime)
    {
        await using var ctx = factory.Database.CreateContext();
        var series = new EventSeriesEntity
        {
            Title = title,
            Description = description,
            StartTime = startTime,
            AnchorDate = anchor,
            IntervalWeeks = 1,
            WeekDay = (int)anchor.DayOfWeek,
            CycleMask = "1",
            EndDate = null,
            GroupId = 1,
            CreatedAt = SeedInstant
        };
        ctx.EventSeries.Add(series);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return series.Id;
    }

    private async Task SeedSignupAsync(int eventId, int userId, VoteType availability)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = userId,
            Availability = (int)availability,
            CreatedAt = SeedInstant,
            UpdatedAt = SeedInstant
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A board member with a signed-in client of their own and a minted subscription address.
    private async Task<Reader> CreateReaderAsync(string name)
    {
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, name, $"{name}@example.com", roles: ["Player"]);

        using var scope = factory.Services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        var subscription = await subscriptionService.MintForUserAsync(user.Id, TestContext.Current.CancellationToken);

        return new Reader(client, user.Id, subscription.Token);
    }

    private async Task<HttpClient> CreateDungeonMasterClientAsync(string name)
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, name, $"{name}@example.com", roles: ["DungeonMaster"]);
        return client;
    }

    private sealed record Reader(HttpClient Client, int UserId, string Token);

    // ---- requests ----

    // The feed is fetched the way a calendar client does: no cookie, no signed-in user, and no
    // active board.
    private async Task<Fetch> FetchAsync(string token, string? ifNoneMatch = null)
    {
        var previous = factory.TestGroupContext.ActiveGroupId;
        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/feeds/calendar/{token}.ics");
            if (ifNoneMatch != null)
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
            }

            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return new Fetch(response.StatusCode, body, response.Headers.ETag?.Tag);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = previous;
        }
    }

    private sealed record Fetch(HttpStatusCode Status, string Body, string? ETag);

    private async Task PostEditAsync(
        HttpClient dungeonMaster, int eventId, string title, DateOnly date, TimeOnly? startTime,
        string? description, string? editScope = null)
    {
        var form = new Dictionary<string, string>
        {
            ["Id"] = eventId.ToString(CultureInfo.InvariantCulture),
            ["Title"] = title,
            ["Date"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Description"] = description ?? string.Empty
        };
        if (startTime is { } time)
        {
            form["StartTime"] = time.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
        if (editScope != null)
        {
            form["EditScope"] = editScope;
        }

        factory.TestGroupContext.ActiveGroupId = 1;
        var response = await dungeonMaster.PostAsync(
            $"/Events/Edit/{eventId}", new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    private async Task PostToEventAsync(HttpClient client, string action, int eventId)
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        var response = await client.PostAsync(
            $"/Events/{action}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = eventId.ToString(CultureInfo.InvariantCulture) }),
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    // ---- reading the document ----

    // Whole seconds, because the feed carries whole seconds: a stamp can be compared to "just
    // before the write" only at that granularity.
    private static DateTime UtcNowInWholeSeconds()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc);
    }

    // The lines of one entry, found by its identifier line and read out to the end of the entry,
    // or null when the document holds no such entry.
    private static string? EntryBlock(string body, int eventId)
    {
        var lines = body.Split("\r\n");
        var uidIndex = Array.IndexOf(lines, $"UID:questboard-event-{eventId}");
        if (uidIndex < 0)
        {
            return null;
        }

        var start = Array.LastIndexOf(lines, "BEGIN:VEVENT", uidIndex);
        var end = Array.IndexOf(lines, "END:VEVENT", uidIndex);
        return string.Join("\r\n", lines[start..(end + 1)]);
    }

    private static string RequireEntry(string body, int eventId)
    {
        var block = EntryBlock(body, eventId);
        block.Should().NotBeNull($"the feed should hold an entry for event {eventId}");
        return block!;
    }

    private static string LineStartingWith(string block, string prefix)
    {
        var line = block.Split("\r\n").FirstOrDefault(l => l.StartsWith(prefix, StringComparison.Ordinal));
        line.Should().NotBeNull($"the entry should carry a line starting with {prefix}");
        return line!;
    }

    private static int SequenceOf(string block) =>
        int.Parse(LineStartingWith(block, "SEQUENCE:")["SEQUENCE:".Length..], CultureInfo.InvariantCulture);

    private static DateTime StampOf(string block) =>
        DateTime.ParseExact(
            LineStartingWith(block, "DTSTAMP:")["DTSTAMP:".Length..],
            StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static DateOnly BaseDate() => DateOnly.FromDateTime(DateTime.Today).AddDays(21);

    // ---- edit form ----

    [Theory]
    [InlineData("Title")]
    [InlineData("Date")]
    [InlineData("StartTime")]
    public async Task Feed_EventEditedThroughTheEditForm_GoesOutWithAHigherSequenceAndALaterStamp(string changedField)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var dm = await CreateDungeonMasterClientAsync("rev_edit_dm");
        var reader = await CreateReaderAsync("rev_edit_reader");

        var baseDate = BaseDate();
        var baseTitle = "Revision Edit Session";
        var baseTime = new TimeOnly(19, 0);

        var editedId = await SeedEventAsync(baseTitle, baseDate, baseTime);
        var neighbourId = await SeedEventAsync("Revision Neighbour Session", baseDate.AddDays(3), new TimeOnly(18, 0));
        await SeedSignupAsync(editedId, reader.UserId, VoteType.Yes);
        await SeedSignupAsync(neighbourId, reader.UserId, VoteType.Yes);

        // The value each edit step writes for the field under test; the other fields hold still.
        (string Title, DateOnly Date, TimeOnly Time) ValuesAtStep(int step) => changedField switch
        {
            "Title" => ($"Renamed Revision Session {step}", baseDate, baseTime),
            "Date" => (baseTitle, baseDate.AddDays(step), baseTime),
            _ => (baseTitle, baseDate, baseTime.AddHours(step)),
        };

        var first = await FetchAsync(reader.Token);
        var firstEntry = RequireEntry(first.Body, editedId);
        var firstNeighbour = RequireEntry(first.Body, neighbourId);
        SequenceOf(firstEntry).Should().Be(1);
        StampOf(firstEntry).Should().Be(SeedInstant);

        var beforeFirstEdit = UtcNowInWholeSeconds();
        var (title1, date1, time1) = ValuesAtStep(1);
        await PostEditAsync(dm, editedId, title1, date1, time1, description: null);

        var second = await FetchAsync(reader.Token);
        var secondEntry = RequireEntry(second.Body, editedId);
        LineStartingWith(secondEntry, "UID:").Should().Be(LineStartingWith(firstEntry, "UID:"));
        SequenceOf(secondEntry).Should().Be(2);
        StampOf(secondEntry).Should().BeOnOrAfter(beforeFirstEdit);
        StampOf(secondEntry).Should().BeAfter(StampOf(firstEntry));
        LineStartingWith(secondEntry, "SUMMARY:").Should().Contain(title1);
        LineStartingWith(secondEntry, "DTSTART").Should().EndWith($"{date1:yyyyMMdd}T{time1:HHmm}00");

        // The neighbour was not touched, so its whole block is byte-identical to the last fetch.
        RequireEntry(second.Body, neighbourId).Should().Be(firstNeighbour);

        var beforeSecondEdit = UtcNowInWholeSeconds();
        var (title2, date2, time2) = ValuesAtStep(2);
        await PostEditAsync(dm, editedId, title2, date2, time2, description: null);

        var third = await FetchAsync(reader.Token);
        var thirdEntry = RequireEntry(third.Body, editedId);
        LineStartingWith(thirdEntry, "UID:").Should().Be(LineStartingWith(firstEntry, "UID:"));
        SequenceOf(thirdEntry).Should().Be(3);
        StampOf(thirdEntry).Should().BeOnOrAfter(beforeSecondEdit);
        StampOf(thirdEntry).Should().BeOnOrAfter(StampOf(secondEntry));
        RequireEntry(third.Body, neighbourId).Should().Be(firstNeighbour);
    }

    // ---- series sweep ----

    [Fact]
    public async Task Feed_ThisAndFutureEdit_RaisesEverySweptSiblingAndLeavesASkippedOneByteIdentical()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var dm = await CreateDungeonMasterClientAsync("rev_sweep_dm");
        var reader = await CreateReaderAsync("rev_sweep_reader");

        const string seriesTitle = "Weekly Revision Council";
        const string seriesNotes = "Every week at the tavern.";
        var anchor = DateOnly.FromDateTime(DateTime.Today).AddDays(14);
        var oldTime = new TimeOnly(19, 0);
        var newTime = new TimeOnly(20, 30);

        var seriesId = await SeedSeriesAsync(seriesTitle, seriesNotes, anchor, oldTime);

        var occurrenceIds = new List<int>();
        var occurrenceDates = new List<DateOnly>();
        for (var slot = 0; slot < 4; slot++)
        {
            var date = anchor.AddDays(slot * 7);
            // Slot 2 was retitled on its own, so the sweep must not touch it.
            var title = slot == 2 ? "One-off Feast In The Series" : seriesTitle;
            var id = await SeedEventAsync(title, date, oldTime, seriesId, slot, seriesNotes);
            await SeedSignupAsync(id, reader.UserId, VoteType.Yes);
            occurrenceIds.Add(id);
            occurrenceDates.Add(date);
        }

        var before = await FetchAsync(reader.Token);
        var blocksBefore = occurrenceIds.Select(id => RequireEntry(before.Body, id)).ToList();
        blocksBefore.Should().OnlyContain(b => SequenceOf(b) == 1);

        var beforeEdit = UtcNowInWholeSeconds();
        await PostEditAsync(dm, occurrenceIds[0], seriesTitle, occurrenceDates[0], newTime, seriesNotes, "ThisAndFutureEvents");

        var after = await FetchAsync(reader.Token);

        foreach (var slot in new[] { 0, 1, 3 })
        {
            var block = RequireEntry(after.Body, occurrenceIds[slot]);
            LineStartingWith(block, "UID:").Should().Be(LineStartingWith(blocksBefore[slot], "UID:"));
            SequenceOf(block).Should().Be(2, $"occurrence {slot} was swept or edited");
            StampOf(block).Should().BeOnOrAfter(beforeEdit);
            LineStartingWith(block, "DTSTART").Should().EndWith($"{occurrenceDates[slot]:yyyyMMdd}T{newTime:HHmm}00");
        }

        RequireEntry(after.Body, occurrenceIds[2]).Should().Be(blocksBefore[2]);
    }

    // ---- cancel and restore ----

    [Fact]
    public async Task Feed_RestoredEvent_ComesBackUnderTheSameUidWithAHigherSequence()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var dm = await CreateDungeonMasterClientAsync("rev_cancel_dm");
        var reader = await CreateReaderAsync("rev_cancel_reader");

        var anchor = DateOnly.FromDateTime(DateTime.Today).AddDays(14);
        var seriesId = await SeedSeriesAsync("Cancelled Revision Series", "Notes.", anchor, new TimeOnly(19, 0));
        var eventId = await SeedEventAsync("Cancelled Revision Series", anchor, new TimeOnly(19, 0), seriesId, 0, "Notes.");
        await SeedSignupAsync(eventId, reader.UserId, VoteType.Yes);

        var first = await FetchAsync(reader.Token);
        var firstEntry = RequireEntry(first.Body, eventId);
        SequenceOf(firstEntry).Should().Be(1);

        await PostToEventAsync(dm, "Cancel", eventId);

        var whileCancelled = await FetchAsync(reader.Token);
        EntryBlock(whileCancelled.Body, eventId).Should().BeNull();

        var beforeRestore = UtcNowInWholeSeconds();
        await PostToEventAsync(dm, "Restore", eventId);

        var restored = await FetchAsync(reader.Token);
        var restoredEntry = RequireEntry(restored.Body, eventId);
        LineStartingWith(restoredEntry, "UID:").Should().Be(LineStartingWith(firstEntry, "UID:"));
        SequenceOf(restoredEntry).Should().Be(3);
        StampOf(restoredEntry).Should().BeOnOrAfter(beforeRestore);
        StampOf(restoredEntry).Should().BeAfter(StampOf(firstEntry));
    }

    [Fact]
    public async Task Feed_EventEditedThroughTheEditForm_InvalidatesTheEarlierEntityTag()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var dm = await CreateDungeonMasterClientAsync("rev_etag_dm");
        var reader = await CreateReaderAsync("rev_etag_reader");

        var date = BaseDate();
        var time = new TimeOnly(19, 0);
        var eventId = await SeedEventAsync("Entity Tag Revision Session", date, time);
        await SeedSignupAsync(eventId, reader.UserId, VoteType.Yes);

        var first = await FetchAsync(reader.Token);
        first.ETag.Should().NotBeNull();

        await PostEditAsync(dm, eventId, "Entity Tag Revision Session", date.AddDays(1), time, description: null);

        // The tag the client holds no longer matches, so it gets the new document in full.
        var conditional = await FetchAsync(reader.Token, ifNoneMatch: first.ETag);
        conditional.Status.Should().Be(HttpStatusCode.OK);
        conditional.ETag.Should().NotBe(first.ETag);
        SequenceOf(RequireEntry(conditional.Body, eventId)).Should().Be(2);
    }

    // ---- a save that changes nothing the feed shows ----

    [Fact]
    public async Task Feed_EventSaveThatChangesNothingTheFeedShows_LeavesTheDocumentByteIdenticalAndAnswers304()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var dm = await CreateDungeonMasterClientAsync("rev_nochange_dm");
        var reader = await CreateReaderAsync("rev_nochange_reader");

        var date = BaseDate();
        var time = new TimeOnly(19, 0);
        const string title = "Quiet Revision Session";
        var eventId = await SeedEventAsync(title, date, time, description: "Original notes.");
        await SeedSignupAsync(eventId, reader.UserId, VoteType.Yes);

        var first = await FetchAsync(reader.Token);
        first.Status.Should().Be(HttpStatusCode.OK);
        first.ETag.Should().NotBeNull();

        // Only the description changes, and the feed does not show it.
        await PostEditAsync(dm, eventId, title, date, time, "Completely rewritten notes.");

        await using (var ctx = factory.Database.CreateContext())
        {
            var stored = await ctx.Events.IgnoreQueryFilters()
                .SingleAsync(e => e.Id == eventId, TestContext.Current.CancellationToken);
            stored.Description.Should().Be("Completely rewritten notes.");
        }

        var second = await FetchAsync(reader.Token);
        second.Status.Should().Be(HttpStatusCode.OK);
        second.Body.Should().Be(first.Body);
        second.ETag.Should().Be(first.ETag);

        var conditional = await FetchAsync(reader.Token, ifNoneMatch: first.ETag);
        conditional.Status.Should().Be(HttpStatusCode.NotModified);
        conditional.Body.Should().BeEmpty();
    }
}
