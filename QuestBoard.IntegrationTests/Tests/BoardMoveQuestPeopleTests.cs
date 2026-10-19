using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// What a quest move does to the people on the quest, over HTTP. Seeded: boards 1 "Board Alpha"
/// and 2 "Board Bravo" linked, a finalized quest on board 1 dated 2031-03-10 19:00 whose
/// Dungeon Master runs quests on both boards, two members of board 2 (one selected with a
/// character, one waitlisted voting Yes), two people who are not on board 2 (a selected player and
/// a spectator), an original quest the moved quest follows and a follow-up that follows it.
///
/// What these facts do NOT establish: how the writes translate to SQL. The shared harness uses
/// the EF Core InMemory provider.
/// </summary>
public class BoardMoveQuestPeopleTests(MoveWebApplicationFactory factory)
    : IClassFixture<MoveWebApplicationFactory>, IAsyncLifetime
{
    private const int BoardAlpha = 1;
    private const int BoardBravo = 2;

    internal static readonly DateTime SessionDate = new(2031, 3, 10, 19, 0, 0);
    internal static readonly DateTime SignedUpAt = new(2031, 1, 2, 8, 0, 0, DateTimeKind.Utc);
    internal static readonly DateTime VoteChangedAt = new(2031, 1, 3, 9, 0, 0, DateTimeKind.Utc);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    internal sealed record Scenario(
        int QuestId,
        int OriginalId,
        int FollowUpId,
        int ProposedDateId,
        int K1Signup,
        int K2Signup,
        int D1Signup,
        int D2Signup,
        UserEntity Dm,
        UserEntity K1,
        UserEntity K2,
        UserEntity D1,
        UserEntity D2,
        UserEntity Actor,
        HttpClient ActorClient);

    internal static async Task<Scenario> SeedScenarioAsync(
        MoveWebApplicationFactory factory, string key, GroupRole? dmRoleOnBravo = GroupRole.DungeonMaster, bool dmIsSuperAdmin = false)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;

        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, BoardAlpha, BoardBravo);

        UserEntity dm;
        if (dmIsSuperAdmin)
        {
            (_, dm) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
                factory, $"{key}_dmsuper", $"{key}_dmsuper@example.com", name: "Dana the DM");
        }
        else
        {
            dm = await BoardMoveSeed.CreateUserAsync(factory, $"{key}_dm", "Dana the DM");
            await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardAlpha, GroupRole.DungeonMaster);
        }

        if (dmRoleOnBravo is { } role)
        {
            await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardBravo, role);
        }

        var k1 = await BoardMoveSeed.CreateUserAsync(factory, $"{key}_k1", "Kept One");
        var k2 = await BoardMoveSeed.CreateUserAsync(factory, $"{key}_k2", "Kept Two");
        var d1 = await BoardMoveSeed.CreateUserAsync(factory, $"{key}_d1", "Dropped One");
        var d2 = await BoardMoveSeed.CreateUserAsync(factory, $"{key}_d2", "Dropped Two");
        await BoardMoveSeed.SeedMembershipAsync(factory, k1.Id, BoardBravo, GroupRole.Player);
        await BoardMoveSeed.SeedMembershipAsync(factory, k2.Id, BoardBravo, GroupRole.Player);

        var originalId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Part One", dm.Id, [new DateTime(2031, 1, 10, 19, 0, 0)]);
        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Goblin Market", dm.Id, [SessionDate],
            finalizedDate: SessionDate, originalQuestId: originalId);
        var followUpId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Part Three", dm.Id, [new DateTime(2031, 6, 10, 19, 0, 0)], originalQuestId: questId);
        var pdId = (await BoardMoveSeed.ProposedDateIdsAsync(factory, questId)).Single();

        var character = await BoardMoveSeed.SeedCharacterAsync(factory, k1.Id, BoardAlpha, "Thorin");
        var k1Signup = await BoardMoveSeed.SeedSignupAsync(
            factory, questId, k1.Id, SignupRole.Player, true, character,
            new Dictionary<int, VoteType> { [pdId] = VoteType.Yes }, SignedUpAt, VoteChangedAt);
        var k2Signup = await BoardMoveSeed.SeedSignupAsync(
            factory, questId, k2.Id, SignupRole.Player, false, null,
            new Dictionary<int, VoteType> { [pdId] = VoteType.Yes }, SignedUpAt);
        var d1Signup = await BoardMoveSeed.SeedSignupAsync(
            factory, questId, d1.Id, SignupRole.Player, true, null,
            new Dictionary<int, VoteType> { [pdId] = VoteType.Yes }, SignedUpAt);
        var d2Signup = await BoardMoveSeed.SeedSignupAsync(
            factory, questId, d2.Id, SignupRole.Spectator, false, null,
            new Dictionary<int, VoteType> { [pdId] = VoteType.Maybe }, SignedUpAt);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.ReminderLogs.Add(new ReminderLogEntity { QuestId = questId, PlayerId = k1.Id, SentAt = DateTime.UtcNow });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (client, actor) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, $"{key}_actor", $"{key}_actor@example.com", name: "Move Actor");

        return new Scenario(questId, originalId, followUpId, pdId, k1Signup, k2Signup, d1Signup, d2Signup,
            dm, k1, k2, d1, d2, actor, client);
    }

    internal static async Task<HttpResponseMessage> PostMoveAsync(MoveWebApplicationFactory factory, Scenario scenario, string? stamp = null)
    {
        if (stamp == null)
        {
            // The stamp the page would have carried, taken while the source board is active.
            factory.TestGroupContext.ActiveGroupId = BoardAlpha;
            await using var scope = factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IQuestMoveService>();
            var preview = await service.PreviewAsync(
                scenario.QuestId, BoardBravo, scenario.Actor.Id, TestContext.Current.CancellationToken);
            stamp = preview.Stamp;
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = scenario.QuestId.ToString(),
            ["targetBoardId"] = BoardBravo.ToString(),
            ["stamp"] = stamp
        });
        return await scenario.ActorClient.PostAsync("/Move/Quest", form, TestContext.Current.CancellationToken);
    }

    private async Task<List<PlayerSignupEntity>> ReadSignupsAsync(int questId)
    {
        await using var ctx = factory.Database.CreateContext();
        return await ctx.PlayerSignups.IgnoreQueryFilters().AsNoTracking()
            .Include(ps => ps.DateVotes).Where(ps => ps.QuestId == questId).ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Move_KeepsMembersWithTheirVotesAndClearsTheirCharacter()
    {
        var scenario = await SeedScenarioAsync(factory, "people_keep");

        var response = await PostMoveAsync(factory, scenario);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var signups = await ReadSignupsAsync(scenario.QuestId);
        var k1 = signups.Single(s => s.Id == scenario.K1Signup);
        k1.SignupRole.Should().Be((int)SignupRole.Player);
        k1.IsSelected.Should().BeTrue();
        k1.SignupTime.Should().Be(SignedUpAt);
        k1.LastVoteChangeTime.Should().Be(VoteChangedAt);
        k1.CharacterId.Should().BeNull();
        k1.DateVotes.Should().ContainSingle().Which.Vote.Should().Be((int)VoteType.Yes);
    }

    [Fact]
    public async Task Move_RemovesNonMembersOfEveryRoleTogetherWithTheirVotes()
    {
        var scenario = await SeedScenarioAsync(factory, "people_drop");

        await PostMoveAsync(factory, scenario);

        var signups = await ReadSignupsAsync(scenario.QuestId);
        signups.Select(s => s.Id).Should().BeEquivalentTo([scenario.K1Signup, scenario.K2Signup]);
        await using var ctx = factory.Database.CreateContext();
        var voterSignupIds = await ctx.Set<PlayerDateVoteEntity>().IgnoreQueryFilters()
            .Select(v => v.PlayerSignupId).ToListAsync(TestContext.Current.CancellationToken);
        voterSignupIds.Should().NotContain([scenario.D1Signup, scenario.D2Signup]);
    }

    [Fact]
    public async Task Move_PromotesTheTopWaitlistedMemberIntoTheFreedSeat()
    {
        var scenario = await SeedScenarioAsync(factory, "people_promote");

        await PostMoveAsync(factory, scenario);

        var signups = await ReadSignupsAsync(scenario.QuestId);
        signups.Single(s => s.Id == scenario.K2Signup).IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task Move_CutsBothChainLinksAndStoresThemAsNull()
    {
        var scenario = await SeedScenarioAsync(factory, "people_chain");

        await PostMoveAsync(factory, scenario);

        (await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId)).OriginalQuestId.Should().BeNull();
        (await BoardMoveSeed.ReadQuestAsync(factory, scenario.FollowUpId)).OriginalQuestId.Should().BeNull();
    }

    [Fact]
    public async Task Move_KeepsProposedDatesAndReminderLogsOnTheQuest()
    {
        var scenario = await SeedScenarioAsync(factory, "people_attached");

        await PostMoveAsync(factory, scenario);

        (await BoardMoveSeed.ProposedDateIdsAsync(factory, scenario.QuestId)).Should().ContainSingle();
        await using var ctx = factory.Database.CreateContext();
        (await ctx.ReminderLogs.IgnoreQueryFilters().CountAsync(r => r.QuestId == scenario.QuestId, TestContext.Current.CancellationToken))
            .Should().Be(1);
        (await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId)).GroupId.Should().Be(BoardBravo);
    }

    [Fact]
    public async Task Move_WithADmWhoIsOnlyAPlayerOnTheTarget_IsRefusedAndNothingChanges()
    {
        var scenario = await SeedScenarioAsync(factory, "people_dm_refused", dmRoleOnBravo: GroupRole.Player);

        var response = await PostMoveAsync(factory, scenario, stamp: "anything");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var redirect = await scenario.ActorClient.GetAsync(
            response.Headers.Location!.ToString(), TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(await redirect.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        html.Should().Contain("Dana the DM is not a Dungeon Master or Admin on Board Bravo, so the quest was not moved.");

        (await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId)).GroupId.Should().Be(BoardAlpha);
        (await ReadSignupsAsync(scenario.QuestId)).Should().HaveCount(4);
    }

    [Fact]
    public async Task Move_WithADmWithNoRowOnTheTarget_IsRefused()
    {
        var scenario = await SeedScenarioAsync(factory, "people_dm_norow", dmRoleOnBravo: null);

        var response = await PostMoveAsync(factory, scenario, stamp: "anything");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId)).GroupId.Should().Be(BoardAlpha);
    }

    [Fact]
    public async Task Move_WithASuperAdminDmWithNoRowOnTheTarget_Moves()
    {
        var scenario = await SeedScenarioAsync(factory, "people_dm_super", dmRoleOnBravo: null, dmIsSuperAdmin: true);

        var response = await PostMoveAsync(factory, scenario);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId)).GroupId.Should().Be(BoardBravo);
    }
}
