using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves over real anonymous HTTP that the calendar feed declares exactly the zone the board
/// clock resolved -- under the default, a non-default, an unresolvable and a Windows-style
/// configured zone -- with the stored wall-clock digits unchanged, and that an unresolvable
/// configured id never reaches the document.
///
/// What these facts do NOT establish: behaviour against a relational database, since the shared
/// harness backs every host with the EF Core InMemory provider.
/// </summary>
public class CalendarFeedBoardZoneHttpTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    // A host whose board clock is configured with the given zone id. Every variant host shares
    // the fixture's one in-memory database, so rows seeded through the fixture are visible to it,
    // while its own board clock singleton resolves the overridden id independently.
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

    // Creates a board (if it does not already exist) through the unfiltered seeding context.
    private async Task SeedBoardAsync(int groupId, string name, BoardType boardType = BoardType.OneShot)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == groupId))
        {
            ctx.Groups.Add(new GroupEntity { Id = groupId, Name = name, CreatedAt = DateTime.UtcNow, BoardType = (int)boardType });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task SeedMembershipAsync(int userId, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)GroupRole.Player });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> SeedEventAsync(int groupId, string title, DateOnly date, TimeOnly? startTime = null)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = date,
            StartTime = startTime,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    private async Task SeedEventSignupAsync(int eventId, int userId, VoteType availability)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = userId,
            Availability = (int)availability,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> SeedQuestAsync(int groupId, int dungeonMasterId, string title, DateTime finalizedDate)
    {
        await using var ctx = factory.Database.CreateContext();
        var quest = new QuestEntity
        {
            Title = title,
            Description = "Test Description",
            ChallengeRating = 5,
            DungeonMasterId = dungeonMasterId,
            GroupId = groupId,
            IsFinalized = true,
            FinalizedDate = finalizedDate,
            TotalPlayerCount = 4,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Quests.Add(quest);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return quest.Id;
    }

    private async Task SeedPlayerSignupAsync(int questId, int playerId)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.PlayerSignups.Add(new PlayerSignupEntity
        {
            QuestId = questId,
            PlayerId = playerId,
            SignupRole = (int)SignupRole.Player,
            IsSelected = true,
            SignupTime = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Mints through the real service from a scope of the host under test, so the fact exercises
    // the same production write path the Profile page calls.
    private static async Task<CalendarSubscription> MintSubscriptionAsync(IServiceProvider services, int userId)
    {
        using var scope = services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        return await subscriptionService.MintForUserAsync(userId, TestContext.Current.CancellationToken);
    }

    // A fresh, plain client with no authorization header at all -- the genuinely anonymous path
    // a real calendar client takes -- against the host under test.
    private static async Task<string> FetchFeedBodyAsync(WebApplicationFactory<Program> host, string token)
    {
        var client = host.CreateClient();
        var response = await client.GetAsync($"/feeds/calendar/{token}.ics", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static int CountOccurrences(string haystack, string needle) => haystack.Split(needle).Length - 1;

    // Seeds one one-shot board holding a timed event at tomorrow 19:00, an all-day event the day
    // after, and a seated finalized quest at three days out 19:00, all visible to one reader,
    // then mints for that reader through the host under test and fetches the anonymous feed.
    private async Task<(string Body, DateOnly TimedDay, DateOnly AllDay, DateOnly QuestDay)> SeedAndFetchAsync(
        WebApplicationFactory<Program> host, string userKey, int boardId)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"{userKey}_reader", $"{userKey}_reader@example.com", name: $"{userKey} Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"{userKey}_dm", $"{userKey}_dm@example.com", name: $"{userKey} DM");

        await SeedBoardAsync(boardId, $"{userKey} Board");
        await SeedMembershipAsync(reader.Id, boardId);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var timedDay = today.AddDays(1);
        var allDay = today.AddDays(2);
        var questFinalized = DateTime.Today.AddDays(3).AddHours(19);

        var timedEventId = await SeedEventAsync(boardId, $"{userKey} Timed Event", timedDay, new TimeOnly(19, 0));
        await SeedEventSignupAsync(timedEventId, reader.Id, VoteType.Yes);

        var allDayEventId = await SeedEventAsync(boardId, $"{userKey} All Day Event", allDay);
        await SeedEventSignupAsync(allDayEventId, reader.Id, VoteType.Yes);

        var questId = await SeedQuestAsync(boardId, dungeonMaster.Id, $"{userKey} Quest", questFinalized);
        await SeedPlayerSignupAsync(questId, reader.Id);

        var subscription = await MintSubscriptionAsync(host.Services, reader.Id);

        // Set after minting and before the request: a real calendar client sends no cookie and
        // has no active board.
        factory.TestGroupContext.ActiveGroupId = null;

        var body = await FetchFeedBodyAsync(host, subscription.Token);
        return (body, timedDay, allDay, DateOnly.FromDateTime(questFinalized));
    }

    [Fact]
    public async Task Feed_DefaultBoardZone_DeclaresEuropeAmsterdamOnTimedEntriesOnly()
    {
        var (body, timedDay, allDay, questDay) = await SeedAndFetchAsync(factory, "zonefeed_default", 30);

        body.Should().Contain($"DTSTART;TZID=Europe/Amsterdam:{timedDay:yyyyMMdd}T190000\r\n");
        body.Should().Contain($"DTSTART;TZID=Europe/Amsterdam:{questDay:yyyyMMdd}T190000\r\n");

        // An all-day entry is a date, not a moment, so it never names a zone.
        body.Should().Contain($"DTSTART;VALUE=DATE:{allDay:yyyyMMdd}\r\n");
        CountOccurrences(body, "DTSTART;VALUE=DATE:").Should().Be(1);
        CountOccurrences(body, "DTSTART;TZID=").Should().Be(2);

        CountOccurrences(body, "BEGIN:VTIMEZONE").Should().Be(1);
        body.Should().Contain("TZID:Europe/Amsterdam\r\n");
        body.Should().Contain("X-WR-TIMEZONE:Europe/Amsterdam\r\n");
    }

    [Fact]
    public async Task Feed_NonDefaultBoardZone_DeclaresThatZoneWithTheStoredDigitsUnchanged()
    {
        var host = CreateZoneVariantFactory("Pacific/Auckland");

        var (body, timedDay, _, questDay) = await SeedAndFetchAsync(host, "zonefeed_auckland", 31);

        // Only the declared zone changes; the seeded 19:00 wall-clock digits come back as stored.
        body.Should().Contain($"DTSTART;TZID=Pacific/Auckland:{timedDay:yyyyMMdd}T190000\r\n");
        body.Should().Contain($"DTSTART;TZID=Pacific/Auckland:{questDay:yyyyMMdd}T190000\r\n");
        CountOccurrences(body, "BEGIN:VTIMEZONE").Should().Be(1);
        body.Should().Contain("TZID:Pacific/Auckland\r\n");
        body.Should().Contain("X-WR-TIMEZONE:Pacific/Auckland\r\n");
        body.Should().NotContain("Europe/Amsterdam");
    }

    [Fact]
    public async Task Feed_UnresolvableBoardZone_DeclaresUtcAndNeverTheConfiguredId()
    {
        var host = CreateZoneVariantFactory("Definitely/NotAZone");

        var (body, timedDay, _, questDay) = await SeedAndFetchAsync(host, "zonefeed_unresolvable", 32);

        // The clock fell back to UTC, so the document declares exactly that and nothing else.
        body.Should().Contain($"DTSTART;TZID=UTC:{timedDay:yyyyMMdd}T190000\r\n");
        body.Should().Contain($"DTSTART;TZID=UTC:{questDay:yyyyMMdd}T190000\r\n");
        CountOccurrences(body, "BEGIN:VTIMEZONE").Should().Be(1);
        body.Should().Contain("TZID:UTC\r\n");
        body.Should().Contain("X-WR-TIMEZONE:UTC\r\n");

        // UTC never changes offset, so exactly one standard observance and no daylight one.
        CountOccurrences(body, "BEGIN:STANDARD").Should().Be(1);
        body.Should().NotContain("BEGIN:DAYLIGHT");
        body.Should().Contain("TZOFFSETFROM:+0000\r\nTZOFFSETTO:+0000\r\n");

        body.Should().NotContain("Definitely/NotAZone");
    }

    [Fact]
    public async Task Feed_WindowsStyleBoardZone_DeclaresAnIanaName()
    {
        var host = CreateZoneVariantFactory("W. Europe Standard Time");

        var (body, timedDay, _, _) = await SeedAndFetchAsync(host, "zonefeed_windows", 33);

        // Calendar clients resolve zones by IANA name, so the Windows id is declared as its
        // region's canonical IANA name, and the Windows spelling never appears.
        body.Should().Contain($"DTSTART;TZID=Europe/Berlin:{timedDay:yyyyMMdd}T190000\r\n");
        body.Should().Contain("X-WR-TIMEZONE:Europe/Berlin\r\n");
        body.Should().NotContain("W. Europe");
    }
}
