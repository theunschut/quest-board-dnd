using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;
using System.Globalization;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves, through the real quest controller and the anonymous calendar feed, that every write
/// path which changes a one-shot quest reaches a subscriber as a newer revision: reopening a
/// finalized quest and finalizing it again, and retitling it through the edit form. It also proves
/// the other side of the rule -- a save that changes nothing the feed shows transfers nothing new.
///
/// The facts post to the real Open, Finalize and Edit actions as the quest's Dungeon Master and
/// read the result back from the feed the way a calendar app would. The quest is seeded with a
/// fixed past creation time through the seeding context, never through a controller post, so the
/// first stamp is a known value that any later revision has to move past.
///
/// Assertions read single lines out of the entry's own block and compare whole documents only
/// across fetches, so they hold whatever other properties the writer adds to an entry.
/// </summary>
public class CalendarFeedQuestRevisionTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    // The default seeded board: a one-shot board, and the one the authenticated Dungeon Master
    // helper enrols its user on.
    private const int BoardId = 1;

    private static readonly DateTime SeededCreatedAt = new(2026, 1, 5, 9, 30, 0, DateTimeKind.Utc);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private sealed record Scenario(
        HttpClient DungeonMasterClient,
        string Token,
        int QuestId,
        string QuestTitle,
        int FirstDateId,
        DateTime FirstDate,
        int SecondDateId,
        DateTime SecondDate);

    // Seeds a finalized one-shot quest the reader runs as Dungeon Master, with two proposed
    // dates, and mints the reader's subscription. The reader is the quest's Dungeon Master and
    // holds no signup row, which is the route by which a Dungeon Master's own quests reach their
    // feed.
    private async Task<Scenario> ArrangeAsync(string userName, string questTitle)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardId;
        factory.TestGroupContext.BoardType = BoardType.OneShot;

        var (client, dungeonMaster) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, userName, $"{userName}@example.com", name: "Revision Dungeon Master", roles: ["DungeonMaster"]);

        var firstDate = DateTime.Today.AddDays(3).AddHours(19);
        var secondDate = DateTime.Today.AddDays(5).AddHours(20);

        int questId;
        int firstDateId;
        int secondDateId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var quest = new QuestEntity
            {
                Title = questTitle,
                Description = "Original description",
                ChallengeRating = 5,
                DungeonMasterId = dungeonMaster.Id,
                GroupId = BoardId,
                IsFinalized = true,
                FinalizedDate = firstDate,
                TotalPlayerCount = 4,
                DungeonMasterSession = false,
                CreatedAt = SeededCreatedAt
            };
            ctx.Quests.Add(quest);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

            var first = new ProposedDateEntity { QuestId = quest.Id, Date = firstDate };
            var second = new ProposedDateEntity { QuestId = quest.Id, Date = secondDate };
            ctx.Set<ProposedDateEntity>().AddRange(first, second);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

            questId = quest.Id;
            firstDateId = first.Id;
            secondDateId = second.Id;
        }

        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
            var subscription = await subscriptionService.MintForUserAsync(dungeonMaster.Id, TestContext.Current.CancellationToken);
            token = subscription.Token;
        }

        return new Scenario(client, token, questId, questTitle, firstDateId, firstDate, secondDateId, secondDate);
    }

    // A fresh, plain client with no authorization header -- the anonymous path a calendar app
    // takes -- and no active board, since a calendar app carries no session.
    private async Task<HttpResponseMessage> FetchFeedAsync(string token, string? ifNoneMatch = null)
    {
        factory.TestGroupContext.ActiveGroupId = null;
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/feeds/calendar/{token}.ics");
        if (ifNoneMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> FetchBodyAsync(string token)
    {
        var response = await FetchFeedAsync(token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> PostOpenAsync(Scenario scenario)
    {
        factory.TestGroupContext.ActiveGroupId = BoardId;
        return await scenario.DungeonMasterClient.PostAsync(
            $"/Quest/Open/{scenario.QuestId}", content: null, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> PostFinalizeAsync(Scenario scenario, int proposedDateId)
    {
        factory.TestGroupContext.ActiveGroupId = BoardId;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SelectedDateId"] = proposedDateId.ToString(CultureInfo.InvariantCulture)
        });
        return await scenario.DungeonMasterClient.PostAsync(
            $"/Quest/Finalize/{scenario.QuestId}", form, TestContext.Current.CancellationToken);
    }

    // Follows the edit form's own round trip: the form page first, for its antiforgery token and
    // cookie, then the post with the fields the page carries.
    private async Task<HttpResponseMessage> PostEditAsync(Scenario scenario, string title, string description)
    {
        factory.TestGroupContext.ActiveGroupId = BoardId;
        var client = scenario.DungeonMasterClient;

        var getResponse = await client.GetAsync($"/Quest/Edit/{scenario.QuestId}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);

        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var form = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string>
            {
                ["Id"] = scenario.QuestId.ToString(CultureInfo.InvariantCulture),
                ["Quest.Id"] = scenario.QuestId.ToString(CultureInfo.InvariantCulture),
                ["Quest.Title"] = title,
                ["Quest.Description"] = description,
                ["Quest.ChallengeRating"] = "5",
                ["Quest.DungeonMasterSession"] = "false",
                ["Quest.TotalPlayerCount"] = "4",
            },
            token);

        return await client.PostAsync($"/Quest/Edit/{scenario.QuestId}", form, TestContext.Current.CancellationToken);
    }

    private static void ShouldHaveRedirected(HttpResponseMessage response) =>
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

    // Returns the lines of the quest's own entry, or null when the document has no entry for it.
    // Matched on the whole identifier so quest 1 is never confused with quest 10.
    private static string[]? FindEntry(string body, int questId)
    {
        var uidPrefix = $"UID:questboard-quest-{questId}";
        foreach (var chunk in body.Split("BEGIN:VEVENT\r\n").Skip(1))
        {
            var lines = chunk.Split("\r\n");
            var hasUid = lines.Any(line =>
                line.StartsWith(uidPrefix, StringComparison.Ordinal)
                && (line.Length == uidPrefix.Length || !char.IsDigit(line[uidPrefix.Length])));
            if (hasUid)
            {
                return lines;
            }
        }

        return null;
    }

    private static string Line(string[] entry, string prefix) =>
        entry.Single(line => line.StartsWith(prefix, StringComparison.Ordinal));

    private static DateTime Stamp(string[] entry) =>
        DateTime.ParseExact(
            Line(entry, "DTSTAMP:")["DTSTAMP:".Length..],
            "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static string StartLine(DateTime start) =>
        $"DTSTART;TZID=Europe/Amsterdam:{start:yyyyMMdd}T{start:HHmmss}";

    private static int CountVEvents(string body) => body.Split("BEGIN:VEVENT").Length - 1;

    private static DateTime NowToWholeSeconds()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
    }

    [Fact]
    public async Task Feed_QuestReopenedAndFinalizedAtAnotherDate_ComesBackUnderTheSameUidWithAHigherSequence()
    {
        var scenario = await ArrangeAsync("quest_revision_other_date", "Revision Other Date Session");

        var firstBody = await FetchBodyAsync(scenario.Token);
        var firstEntry = FindEntry(firstBody, scenario.QuestId);
        firstEntry.Should().NotBeNull();
        var firstUidLine = Line(firstEntry!, "UID:");
        Line(firstEntry!, "SEQUENCE:").Should().Be("SEQUENCE:1");
        Line(firstEntry!, "DTSTART").Should().Be(StartLine(scenario.FirstDate));
        var firstStamp = Stamp(firstEntry!);

        ShouldHaveRedirected(await PostOpenAsync(scenario));

        // Reopened, the quest is back to voting: it is not finalized, so it has no place in the
        // calendar and its entry is gone from the very next fetch.
        var reopenedBody = await FetchBodyAsync(scenario.Token);
        FindEntry(reopenedBody, scenario.QuestId).Should().BeNull();
        CountVEvents(reopenedBody).Should().Be(0);

        var finalizedAfter = NowToWholeSeconds();
        ShouldHaveRedirected(await PostFinalizeAsync(scenario, scenario.SecondDateId));

        var finalBody = await FetchBodyAsync(scenario.Token);
        var finalEntry = FindEntry(finalBody, scenario.QuestId);
        finalEntry.Should().NotBeNull();
        CountVEvents(finalBody).Should().Be(1);

        // The identifier is the same byte for byte, so a client updates the copy it holds instead
        // of adding a second one.
        Line(finalEntry!, "UID:").Should().Be(firstUidLine);

        // One revision per save: the reopen and the finalize each raised it once, from 1 to 3. A
        // client that compares revisions only replaces its copy when the number is higher.
        Line(finalEntry!, "SEQUENCE:").Should().Be("SEQUENCE:3");
        Line(finalEntry!, "DTSTART").Should().Be(StartLine(scenario.SecondDate));

        var finalStamp = Stamp(finalEntry!);
        finalStamp.Should().BeOnOrAfter(finalizedAfter);
        finalStamp.Should().BeAfter(firstStamp);
    }

    [Fact]
    public async Task Feed_QuestReopenedAndFinalizedAtTheSameDate_StillComesBackWithAHigherSequence()
    {
        var scenario = await ArrangeAsync("quest_revision_same_date", "Revision Same Date Session");

        var firstBody = await FetchBodyAsync(scenario.Token);
        var firstEntry = FindEntry(firstBody, scenario.QuestId);
        firstEntry.Should().NotBeNull();
        var firstUidLine = Line(firstEntry!, "UID:");
        var firstStartLine = Line(firstEntry!, "DTSTART");
        Line(firstEntry!, "SEQUENCE:").Should().Be("SEQUENCE:1");
        var firstStamp = Stamp(firstEntry!);

        ShouldHaveRedirected(await PostOpenAsync(scenario));
        FindEntry(await FetchBodyAsync(scenario.Token), scenario.QuestId).Should().BeNull();

        var finalizedAfter = NowToWholeSeconds();
        ShouldHaveRedirected(await PostFinalizeAsync(scenario, scenario.FirstDateId));

        var finalBody = await FetchBodyAsync(scenario.Token);
        var finalEntry = FindEntry(finalBody, scenario.QuestId);
        finalEntry.Should().NotBeNull();
        CountVEvents(finalBody).Should().Be(1);

        // The start did not move, yet the entry went away and came back, and a client that kept
        // its old copy has to be told the returned one is newer.
        Line(finalEntry!, "UID:").Should().Be(firstUidLine);
        Line(finalEntry!, "DTSTART").Should().Be(firstStartLine);
        Line(finalEntry!, "SEQUENCE:").Should().Be("SEQUENCE:3");

        var finalStamp = Stamp(finalEntry!);
        finalStamp.Should().BeOnOrAfter(finalizedAfter);
        finalStamp.Should().BeAfter(firstStamp);
    }

    [Fact]
    public async Task Feed_QuestRetitledThroughTheEditForm_GoesOutWithAHigherSequenceAndALaterStamp()
    {
        var scenario = await ArrangeAsync("quest_revision_retitle", "Revision Retitle Session");

        var firstBody = await FetchBodyAsync(scenario.Token);
        var firstEntry = FindEntry(firstBody, scenario.QuestId);
        firstEntry.Should().NotBeNull();
        var firstUidLine = Line(firstEntry!, "UID:");
        Line(firstEntry!, "SEQUENCE:").Should().Be("SEQUENCE:1");
        var firstStamp = Stamp(firstEntry!);

        var editedAfter = NowToWholeSeconds();
        var response = await PostEditAsync(scenario, "Revision Renamed Session", "Original description");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        var secondBody = await FetchBodyAsync(scenario.Token);
        var secondEntry = FindEntry(secondBody, scenario.QuestId);
        secondEntry.Should().NotBeNull();
        CountVEvents(secondBody).Should().Be(1);

        Line(secondEntry!, "UID:").Should().Be(firstUidLine);
        Line(secondEntry!, "SUMMARY:").Should().EndWith("Revision Renamed Session");
        secondEntry!.Any(line => line.Contains("Revision Retitle Session", StringComparison.Ordinal)).Should().BeFalse();
        Line(secondEntry!, "SEQUENCE:").Should().Be("SEQUENCE:2");

        var secondStamp = Stamp(secondEntry!);
        secondStamp.Should().BeOnOrAfter(editedAfter);
        secondStamp.Should().BeAfter(firstStamp);
    }

    [Fact]
    public async Task Feed_QuestSaveThatChangesNothingTheFeedShows_LeavesTheDocumentByteIdenticalAndAnswers304()
    {
        var scenario = await ArrangeAsync("quest_revision_no_change", "Revision Unchanged Session");

        var firstResponse = await FetchFeedAsync(scenario.Token);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var firstTag = firstResponse.Headers.ETag?.Tag;
        firstTag.Should().NotBeNullOrEmpty();
        FindEntry(firstBody, scenario.QuestId).Should().NotBeNull();

        // Only the description changes; the title, the finalized date and the finalized flag are
        // exactly what they were, so nothing the feed shows has moved.
        var response = await PostEditAsync(scenario, scenario.QuestTitle, "A rewritten description that the feed never shows");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        await using (var ctx = factory.Database.CreateContext())
        {
            // The seeding context carries no active board, so its tenant filter is bypassed for
            // this read-back of the row the controller just saved.
            var stored = await ctx.Quests.IgnoreQueryFilters().SingleAsync(q => q.Id == scenario.QuestId, TestContext.Current.CancellationToken);
            stored.Description.Should().Be("A rewritten description that the feed never shows");
            stored.Title.Should().Be(scenario.QuestTitle);
        }

        var secondResponse = await FetchFeedAsync(scenario.Token);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        secondBody.Should().Be(firstBody);
        secondResponse.Headers.ETag?.Tag.Should().Be(firstTag);

        // A device that presents the tag it already holds is told to keep its copy.
        var conditionalResponse = await FetchFeedAsync(scenario.Token, ifNoneMatch: firstTag);
        conditionalResponse.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await conditionalResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }
}
