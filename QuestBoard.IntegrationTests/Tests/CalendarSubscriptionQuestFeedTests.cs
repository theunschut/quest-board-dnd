using Microsoft.Extensions.Options;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the application's own scoping and response logic over real HTTP for the quest half of
/// the calendar feed: an anonymous, cookie-less GET against a minted address returns the
/// finalized one-shot quest sessions the subscription's owner holds a confirmed seat on, or is
/// the Dungeon Master of.
///
/// What these facts do NOT establish, and must not be read as establishing: end-to-end
/// isolation against the database the application actually runs on. The shared harness backs
/// every suite with the EF Core InMemory provider, which evaluates every predicate as ordinary
/// LINQ-to-Objects. Several properties the repository query depends on therefore never reach a
/// relational query compiler here: the board containment test (oneShotGroupIds.Contains(...))
/// over an *empty* id collection is exercised only as List.Contains, never as its SQL
/// translation; the filter bypass interacting with the signup entity's own board-navigating
/// filter generates no join at all in memory; and the seat-or-Dungeon-Master disjunction over a
/// navigation collection is likewise exercised only as in-memory enumeration, never as its
/// translated form. The facts below are about the application's own scoping logic; proving the
/// query translates and behaves the same way on a relational provider needs a separate,
/// relational test.
/// </summary>
public class CalendarSubscriptionQuestFeedTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
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

    // Adds a membership row for an already-created user on the named board.
    private async Task SeedMembershipAsync(int userId, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)GroupRole.Player });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Seeds a finalized quest on the named board with the given Dungeon Master, through the
    // unfiltered seeding context -- field shapes copied from TestDataHelper.CreateTestQuestAsync.
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

    // Seeds a confirmed (IsSelected == true by default) player signup for the given user on the
    // given quest -- field shapes copied from TestDataHelper.CreatePlayerSignupAsync.
    private async Task SeedPlayerSignupAsync(int questId, int playerId, bool isSelected = true)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.PlayerSignups.Add(new PlayerSignupEntity
        {
            QuestId = questId,
            PlayerId = playerId,
            SignupRole = 0,
            IsSelected = isSelected,
            SignupTime = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Mints through the real service from a scope, rather than a direct database insert, so
    // the fact exercises the same production write path the Profile page calls.
    private async Task<CalendarSubscription> MintSubscriptionAsync(int userId)
    {
        using var scope = factory.Services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        return await subscriptionService.MintForUserAsync(userId, TestContext.Current.CancellationToken);
    }

    // Reads the configured window and duration values from the running host's own DI
    // container, so a fact derives its expectations from configuration rather than a literal
    // that silently drifts out of sync with a future change to the defaults.
    private CalendarFeedOptions GetFeedOptions()
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptions<CalendarFeedOptions>>().Value;
    }

    // A fresh, plain client with no authorization header at all -- the genuinely anonymous path
    // a real calendar client takes.
    private async Task<HttpResponseMessage> FetchFeedAsync(string token)
    {
        var client = factory.CreateClient();
        return await client.GetAsync($"/feeds/calendar/{token}.ics", TestContext.Current.CancellationToken);
    }

    private static int CountVEvents(string body) => body.Split("BEGIN:VEVENT").Length - 1;

    [Fact]
    public async Task Feed_ServesASeatedReadersFinalizedOneShotQuest_ToAnonymousCaller()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_tracer_reader", "questfeed_tracer_reader@example.com", name: "Quest Feed Tracer Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_tracer_dm", "questfeed_tracer_dm@example.com", name: "Quest Feed Tracer DM");

        // Group 1 is the default seeded group -- a distinct id is used here so the board's own
        // name is under this fact's own control rather than borrowed.
        await SeedBoardAsync(2, "Quest Feed Tracer Board");
        await SeedMembershipAsync(reader.Id, 2);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);
        var questId = await SeedQuestAsync(2, dungeonMaster.Id, "Quest Feed Tracer Session", finalizedDate);
        await SeedPlayerSignupAsync(questId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        var options = GetFeedOptions();

        // Set after minting and before the request, so the fact proves the path does not lean
        // on an active board -- a real calendar client sends no cookie and has none.
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.ToString().Should().Be("text/calendar; charset=utf-8");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().StartWith("BEGIN:VCALENDAR");
        body.Should().EndWith("END:VCALENDAR\r\n");
        body.Should().Contain($"UID:questboard-quest-{questId}");

        // The title line, terminated immediately by the line break -- what proves no marker of
        // any kind (no quest marker, no Dungeon Master marker, no answer suffix) was appended.
        body.Should().Contain("SUMMARY:[Quest Feed Tracer Board] Quest Feed Tracer Session\r\n");

        body.Should().Contain($"DTSTART:{finalizedDate:yyyyMMdd}T{finalizedDate:HHmmss}");
        body.Should().NotContain($"DTSTART:{finalizedDate:yyyyMMdd}T{finalizedDate:HHmmss}Z");

        // The end is exactly the configured number of hours later, read from the running host's
        // own options rather than a literal four.
        var expectedEnd = finalizedDate.AddHours(options.QuestDurationHours);
        body.Should().Contain($"DTEND:{expectedEnd:yyyyMMdd}T{expectedEnd:HHmmss}");

        body.Should().Contain("TRANSP:TRANSPARENT");

        CountVEvents(body).Should().Be(1);

        // What proves the quest went through the timed branch and never the all-day one.
        body.Should().NotContain("DTSTART;VALUE=DATE:");
    }
}
