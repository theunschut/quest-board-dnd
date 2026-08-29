using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The cross-board agenda is the first user-facing read in the application that deliberately
/// steps outside the ambient board filter, so the ordinary integration harness cannot notice a
/// leak on this page the way it can everywhere else -- it never looks at a second board at all.
/// A suite built only from a non-member-board-is-absent fact would stay entirely green even if
/// this page silently collapsed to a single board, because absence alone cannot distinguish
/// "correctly scoped to two boards" from "accidentally scoped to one." These facts therefore
/// seed a genuine third board and hold the two-joined-boards case as its own fact, alongside a
/// board the viewer leaves mid-suite and a filter selection that can never widen what it started
/// with.
/// </summary>
public class AgendaTenantIsolationTests(WebApplicationFactoryBase factory)
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

    // Seeds one event on the named board and returns its id.
    private async Task<int> SeedEventAsync(int groupId, string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    // Adds a membership row for an already-created user on the named board.
    private async Task SeedMembershipAsync(int userId, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)GroupRole.Player });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Creates a user, a membership row on the named board, and returns the user -- this is what
    // makes a roster member on a board the viewer does not belong to something a leak can
    // actually surface, rather than an unfamiliar name that happens to never appear.
    private async Task<UserEntity> SeedMemberAsync(int groupId, string userName, string email, string displayName)
    {
        var user = await AuthenticationHelper.CreateTestUserAsync(factory.Services, userName, email, name: displayName);
        await SeedMembershipAsync(user.Id, groupId);
        return user;
    }

    // Seeds a signup row for the given user on the given event.
    private async Task SeedSignupAsync(int eventId, int userId, VoteType availability, bool answered = true)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = userId,
            Availability = (int)availability,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = answered ? DateTime.UtcNow : null
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Agenda_ForMemberOfOneBoard_ShowsNothingFromANonMemberBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        await SeedBoardAsync(2, "Isolation Board Two");

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isoagenda_one_board", "isoagenda_one_board@example.com", roles: []);
        await SeedMembershipAsync(viewer.Id, 1);

        var ownEventId = await SeedEventAsync(1, "Board One Isolation Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        var ownMember = await SeedMemberAsync(1, "isoagenda_own_member", "isoagenda_own_member@example.com", "Board One Isolation Member");
        await SeedSignupAsync(ownEventId, ownMember.Id, VoteType.Yes);

        var otherEventId = await SeedEventAsync(2, "Board Two Isolation Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        var otherMember = await SeedMemberAsync(2, "isoagenda_other_member", "isoagenda_other_member@example.com", "Board Two Isolation Member");
        await SeedSignupAsync(otherEventId, otherMember.Id, VoteType.Yes);

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Board One Isolation Session");
        body.Should().Contain("Board One Isolation Member");
        body.Should().NotContain("Board Two Isolation Session");
        body.Should().NotContain("Board Two Isolation Member");
        body.Should().NotContain("Isolation Board Two");
    }

    [Fact]
    public async Task Agenda_ForMemberOfTwoOfThreeBoards_ShowsBothJoinedBoardsAndNotTheThird()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        await SeedBoardAsync(2, "Three Board Suite Board Two");
        await SeedBoardAsync(groupId: 3, name: "Three Board Suite Board Three");

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isoagenda_two_of_three", "isoagenda_two_of_three@example.com", roles: []);
        await SeedMembershipAsync(viewer.Id, 1);
        await SeedMembershipAsync(viewer.Id, 2);

        var boardOneEventId = await SeedEventAsync(1, "Three Board Suite Board One Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        var boardOneMember = await SeedMemberAsync(1, "isoagenda_board_one_member", "isoagenda_board_one_member@example.com", "Board One Suite Member");
        await SeedSignupAsync(boardOneEventId, boardOneMember.Id, VoteType.Yes);

        var boardTwoEventId = await SeedEventAsync(2, "Three Board Suite Board Two Session", DateOnly.FromDateTime(DateTime.Today).AddDays(2));
        var boardTwoMember = await SeedMemberAsync(2, "isoagenda_board_two_member", "isoagenda_board_two_member@example.com", "Board Two Suite Member");
        await SeedSignupAsync(boardTwoEventId, boardTwoMember.Id, VoteType.Yes);

        var boardThreeEventId = await SeedEventAsync(groupId: 3, title: "Three Board Suite Board Three Session", date: DateOnly.FromDateTime(DateTime.Today).AddDays(3));
        var boardThreeMember = await SeedMemberAsync(3, "isoagenda_board_three_member", "isoagenda_board_three_member@example.com", "Board Three Suite Member");
        await SeedSignupAsync(boardThreeEventId, boardThreeMember.Id, VoteType.Yes);

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // This is the fact that proves aggregation rather than mere absence: a page that
        // silently collapsed to a single joined board would still pass every other fact in this
        // file while failing only the two positive assertions below, so this is the one place a
        // collapse-to-one-board bug would actually be caught. Do not fold this into the
        // non-member-absence fact above, and do not weaken it to a single positive assertion.
        body.Should().Contain("Three Board Suite Board One Session");
        body.Should().Contain("Board One Suite Member");
        body.Should().Contain("Three Board Suite Board Two Session");
        body.Should().Contain("Three Board Suite Board Two");
        body.Should().Contain("Board Two Suite Member");

        body.Should().NotContain("Three Board Suite Board Three Session");
        body.Should().NotContain("Three Board Suite Board Three");
        body.Should().NotContain("Board Three Suite Member");
    }

    [Fact]
    public async Task Agenda_ForMemberOfTwoBoards_InterleavesRowsByDate()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        await SeedBoardAsync(2, "Interleave Suite Board Two");

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isoagenda_interleave", "isoagenda_interleave@example.com", roles: []);
        await SeedMembershipAsync(viewer.Id, 1);
        await SeedMembershipAsync(viewer.Id, 2);

        // Board two's event is dated earlier than board one's, so a per-board grouping (rather
        // than a genuine date-ordered merge across boards) would render them in the wrong
        // relative order.
        await SeedEventAsync(2, "Interleave Suite Board Two Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedEventAsync(1, "Interleave Suite Board One Session", DateOnly.FromDateTime(DateTime.Today).AddDays(2));

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var boardTwoIndex = body.IndexOf("Interleave Suite Board Two Session", StringComparison.Ordinal);
        var boardOneIndex = body.IndexOf("Interleave Suite Board One Session", StringComparison.Ordinal);
        boardTwoIndex.Should().BeGreaterThan(-1);
        boardOneIndex.Should().BeGreaterThan(-1);
        boardTwoIndex.Should().BeLessThan(boardOneIndex);
    }
}
