using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// What the four role actions and Remove from Group may do to the person using them and to the board's last Admin. An
/// Admin may not demote or remove themselves, and nobody may leave a board without an Admin from the Users page; each
/// refusal reads as an Error toast and changes nothing. A role action or removal for someone who is not on the board
/// answers an Error toast instead of silence. The remaining facts pin the role changes and removals that must keep
/// working.
/// </summary>
public class AdminRoleGuardTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;

    public AdminRoleGuardTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    // ---- Seeding and reading helpers --------------------------------------------------------------

    private async Task<UserEntity> SeedMemberAsync(string tag, GroupRole role)
    {
        var account = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, $"guard_{tag}", $"guard_{tag}@example.com", "Test123!", $"Guard {tag}");
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        context.UserGroups.Add(new UserGroupEntity { UserId = account.Id, GroupId = 1, GroupRole = (int)role });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return account;
    }

    private async Task<GroupRole?> RoleOnBoardAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var row = context.UserGroups.FirstOrDefault(ug => ug.UserId == userId && ug.GroupId == 1);
        return row == null ? null : (GroupRole)row.GroupRole;
    }

    private async Task<int> MembershipCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        return context.UserGroups.Count(ug => ug.GroupId == 1);
    }

    private static Task<HttpResponseMessage> PostRoleActionAsync(HttpClient client, string action, int userId) =>
        client.PostAsync($"/Admin/{action}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["userId"] = userId.ToString() }),
            TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> PostDeleteUserAsync(HttpClient client, int userId) =>
        client.PostAsync($"/Admin/DeleteUser/{userId}", new FormUrlEncodedContent(new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

    // A refusal is a redirect to the Users page; the Error toast is read from the page the redirect lands on, because
    // the toast travels in temp data and is shown once.
    private static async Task AssertRefusedWithErrorToastAsync(HttpClient client, HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "a refused role change is an answer, not a page");
        response.Headers.Location!.OriginalString.Should().Contain("/Admin/Users");

        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Regex.Matches(html, "<strong class=\"me-auto\">(\\w+)</strong>").Select(m => m.Groups[1].Value)
            .Should().Contain("Error", "the refusal must be visible as an Error toast");
    }

    // ---- Reproduction facts: self and last-Admin rules ----------------------------------------------

    // The actor has a peer Admin, so the only thing wrong with the request is that it is aimed at themselves.
    private async Task AssertSelfDemotionRefusedAsync(string action)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, actor) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        await SeedMemberAsync("peeradmin", GroupRole.Admin);

        var response = await PostRoleActionAsync(adminClient, action, actor.Id);

        (await RoleOnBoardAsync(actor.Id)).Should().Be(GroupRole.Admin, "an Admin may not demote themselves");
        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    [Fact]
    public async Task DemoteFromAdmin_OnTheActorThemselves_IsRefused_AndTheirRoleIsUnchanged() =>
        await AssertSelfDemotionRefusedAsync("DemoteFromAdmin");

    [Fact]
    public async Task DemoteToPlayer_OnTheActorThemselves_IsRefused_AndTheirRoleIsUnchanged() =>
        await AssertSelfDemotionRefusedAsync("DemoteToPlayer");

    // PromoteToDM on an Admin is the crafted case: its name says "up" but it demotes.
    [Fact]
    public async Task PromoteToDM_OnTheActorThemselves_IsRefused_AndTheirRoleIsUnchanged() =>
        await AssertSelfDemotionRefusedAsync("PromoteToDM");

    // Remove from Group is a posted form like the role actions: a refusal is an Error toast and the membership stays.
    [Fact]
    public async Task DeleteUser_OnTheActorThemselves_IsRefused_AndTheirMembershipStays()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, actor) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        await SeedMemberAsync("peeradmin", GroupRole.Admin);

        var response = await PostDeleteUserAsync(adminClient, actor.Id);

        (await RoleOnBoardAsync(actor.Id)).Should().Be(GroupRole.Admin, "an Admin may not remove themselves from the board");
        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    // The actor is a platform administrator who is only a Player here, so the target is the sole Admin of the board and
    // nothing is aimed at the actor. PromoteToDM on an Admin is the crafted case: its name says "up" but it demotes.
    [Theory]
    [InlineData("DemoteFromAdmin")]
    [InlineData("DemoteToPlayer")]
    [InlineData("PromoteToDM")]
    public async Task RoleAction_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheRoleIsUnchanged(string action)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var onlyAdmin = await SeedMemberAsync("onlyadmin", GroupRole.Admin);

        var response = await PostRoleActionAsync(superAdminClient, action, onlyAdmin.Id);

        (await RoleOnBoardAsync(onlyAdmin.Id)).Should().Be(GroupRole.Admin, "a board must not be left without an Admin");
        await AssertRefusedWithErrorToastAsync(superAdminClient, response);
    }

    [Fact]
    public async Task DeleteUser_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheMembershipStays()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var onlyAdmin = await SeedMemberAsync("onlyadmin", GroupRole.Admin);

        var response = await PostDeleteUserAsync(superAdminClient, onlyAdmin.Id);

        (await RoleOnBoardAsync(onlyAdmin.Id)).Should().Be(GroupRole.Admin, "a board must not be left without an Admin");
        await AssertRefusedWithErrorToastAsync(superAdminClient, response);
    }

    // A page that was open while the person left the board still posts their id; the answer must say so.
    [Theory]
    [InlineData("PromoteToAdmin")]
    [InlineData("DemoteFromAdmin")]
    [InlineData("PromoteToDM")]
    [InlineData("DemoteToPlayer")]
    public async Task RoleAction_ForSomeoneWhoIsNotOnTheBoard_AnswersAnErrorToast_AndAddsNoMembership(string action)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var outsider = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "guard_outsider", "guard_outsider@example.com", "Test123!", "Guard outsider");
        var membersBefore = await MembershipCountAsync();

        var response = await PostRoleActionAsync(adminClient, action, outsider.Id);

        (await MembershipCountAsync()).Should().Be(membersBefore);
        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    [Fact]
    public async Task DeleteUser_ForSomeoneWhoIsNotOnTheBoard_AnswersTheStaleToast_AndRemovesNobody()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var outsider = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "guard_outsider", "guard_outsider@example.com", "Test123!", "Guard outsider");
        var membersBefore = await MembershipCountAsync();

        var response = await PostDeleteUserAsync(adminClient, outsider.Id);

        (await MembershipCountAsync()).Should().Be(membersBefore);
        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    // The refusal sentences are the ones the person reads, so they are pinned by text and not only by the toast kind.
    [Fact]
    public async Task DeleteUser_OnTheActorThemselves_AnswersTheSelfSentence()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, actor) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        await SeedMemberAsync("peeradmin", GroupRole.Admin);

        var response = await PostDeleteUserAsync(adminClient, actor.Id);
        var landed = await adminClient.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        html.Should().Contain("You cannot change your own role or remove yourself from the board.");
    }

    [Fact]
    public async Task DeleteUser_OnTheOnlyAdmin_AnswersTheKeepAManagerSentence()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var onlyAdmin = await SeedMemberAsync("onlyadmin", GroupRole.Admin);

        var response = await PostDeleteUserAsync(superAdminClient, onlyAdmin.Id);
        var landed = await superAdminClient.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        html.Should().Contain("A board must keep at least one member who can manage it. Give someone else that role first.");
    }

    [Fact]
    public async Task DeleteUser_ForAnAdminWhoIsNotTheLastOne_StillRemovesThem()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var peer = await SeedMemberAsync("peeradmin", GroupRole.Admin);

        var response = await PostDeleteUserAsync(adminClient, peer.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await RoleOnBoardAsync(peer.Id)).Should().BeNull("another Admin remains, so removing this one is allowed");
    }

    // The stale-page toast names the board and not a role, and the posted role actions answer it too.
    [Fact]
    public async Task RoleAction_ForSomeoneWhoIsNotOnTheBoard_AnswersTheStaleSentence()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var outsider = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "guard_outsider", "guard_outsider@example.com", "Test123!", "Guard outsider");

        var response = await PostRoleActionAsync(adminClient, "PromoteToAdmin", outsider.Id);
        var landed = await adminClient.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        html.Should().Contain("That person is not a member of this board any more.");
    }

    // ---- Pins: role changes that must keep working -------------------------------------------------

    // Another Admin is on the board, so demoting this one leaves an Admin behind.
    [Fact]
    public async Task DemoteFromAdmin_ByAnotherAdmin_StillDemotesAnAdminWhoIsNotTheLastOne()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, actor) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var peer = await SeedMemberAsync("peeradmin", GroupRole.Admin);

        var response = await PostRoleActionAsync(adminClient, "DemoteFromAdmin", peer.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await RoleOnBoardAsync(peer.Id)).Should().Be(GroupRole.DungeonMaster, "the button is labelled Demote to DM");
        (await RoleOnBoardAsync(actor.Id)).Should().Be(GroupRole.Admin);
    }

    [Fact]
    public async Task DemoteToPlayer_ByAnotherAdmin_StillDemotesAnAdminWhoIsNotTheLastOne()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, actor) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var peer = await SeedMemberAsync("peeradmin", GroupRole.Admin);

        var response = await PostRoleActionAsync(adminClient, "DemoteToPlayer", peer.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await RoleOnBoardAsync(peer.Id)).Should().Be(GroupRole.Player);
        (await RoleOnBoardAsync(actor.Id)).Should().Be(GroupRole.Admin);
    }

    // The target is a Dungeon Master, not the last Admin, so a platform administrator acting from outside the board
    // is never mistaken for the person being changed.
    [Fact]
    public async Task DemoteToPlayer_BySuperAdmin_StillDemotesADungeonMaster()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var dungeonMaster = await SeedMemberAsync("dm", GroupRole.DungeonMaster);

        var response = await PostRoleActionAsync(superAdminClient, "DemoteToPlayer", dungeonMaster.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await RoleOnBoardAsync(dungeonMaster.Id)).Should().Be(GroupRole.Player);
    }

    [Fact]
    public async Task PromoteToDM_ByAnAdmin_StillPromotesAPlayer()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var player = await SeedMemberAsync("player", GroupRole.Player);

        var response = await PostRoleActionAsync(adminClient, "PromoteToDM", player.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await RoleOnBoardAsync(player.Id)).Should().Be(GroupRole.DungeonMaster);
    }

    [Fact]
    public async Task PromoteToAdmin_ByAnAdmin_StillPromotesADungeonMaster()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var dungeonMaster = await SeedMemberAsync("dm", GroupRole.DungeonMaster);

        var response = await PostRoleActionAsync(adminClient, "PromoteToAdmin", dungeonMaster.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await RoleOnBoardAsync(dungeonMaster.Id)).Should().Be(GroupRole.Admin);
    }

    [Fact]
    public async Task DeleteUser_ByAnAdmin_StillRemovesAPlayerFromTheBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var player = await SeedMemberAsync("player", GroupRole.Player);

        var response = await PostDeleteUserAsync(adminClient, player.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await RoleOnBoardAsync(player.Id)).Should().BeNull();
    }
}
