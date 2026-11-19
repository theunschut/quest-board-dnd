using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// What the four role actions and Remove from Group may do to the person using them and to the board's last Admin. An
/// Admin may not demote or remove themselves, and nobody may leave a board without an Admin from the Users page; each
/// refusal reads as an Error toast and changes nothing. A role action, a removal, an edit, a password reset or a welcome
/// email resend for someone who is not on the board answers an Error toast instead of silence. The remaining facts pin
/// the role changes and removals that must keep working.
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

    // ---- Stale page: the account actions on a row whose person has left the board -------------------

    // The Edit, Reset Password and Resend Welcome Email buttons of a Users page that was open while the person left the
    // board, or while their account was deleted, still carry the old id. Each answers the same stale-page Error toast as
    // the role actions and changes nothing, whether the id belongs to someone on another board only or to nobody.
    private static Task<HttpResponseMessage> SendAccountActionAsync(HttpClient client, string action, int userId) =>
        action switch
        {
            "EditUser:GET" => client.GetAsync($"/Admin/EditUser?userId={userId}", TestContext.Current.CancellationToken),
            "EditUser:POST" => client.PostAsync("/Admin/EditUser", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = userId.ToString(),
                ["Name"] = "Renamed From A Stale Page",
                ["Email"] = "renamed_from_stale_page@example.com",
                ["HasKey"] = "true"
            }), TestContext.Current.CancellationToken),
            "ResetPassword:GET" => client.GetAsync($"/Admin/ResetPassword?userId={userId}", TestContext.Current.CancellationToken),
            "ResetPassword:POST" => client.PostAsync("/Admin/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UserId"] = userId.ToString(),
                ["NewPassword"] = "StalePage123!",
                ["ConfirmPassword"] = "StalePage123!"
            }), TestContext.Current.CancellationToken),
            "SendConfirmationEmail" => client.PostAsync("/Admin/SendConfirmationEmail",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["userId"] = userId.ToString() }),
                TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

    private async Task<(string Name, string? Email, bool HasKey, string? PasswordHash)> AccountStateAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var user = context.Users.IgnoreQueryFilters().Single(u => u.Id == userId);
        return (user.Name, user.Email, user.HasKey, user.PasswordHash);
    }

    // The answer is a redirect to the Users page whose single landing shows an Error toast carrying the stale sentence.
    private static async Task AssertStaleToastAsync(HttpClient client, HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "a stale page is answered, not shown");
        response.Headers.Location!.OriginalString.Should().Contain("/Admin/Users");

        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Regex.Matches(html, "<strong class=\"me-auto\">(\\w+)</strong>").Select(m => m.Groups[1].Value)
            .Should().Contain("Error", "the stale page must be told as an Error toast");
        html.Should().Contain("That person is not a member of this board any more.");
    }

    [Theory]
    [InlineData("EditUser:GET")]
    [InlineData("EditUser:POST")]
    [InlineData("ResetPassword:GET")]
    [InlineData("ResetPassword:POST")]
    [InlineData("SendConfirmationEmail")]
    public async Task AccountAction_ForSomeoneWhoIsNotOnTheBoard_AnswersTheStaleToast_AndChangesNothing(string action)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(_factory.Services, 2);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var outsider = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "guard_outsider", "guard_outsider@example.com", "Test123!", "Guard outsider");
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.UserGroups.Add(new UserGroupEntity { UserId = outsider.Id, GroupId = 2, GroupRole = (int)GroupRole.Player });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var membersBefore = await MembershipCountAsync();
        var accountBefore = await AccountStateAsync(outsider.Id);

        var response = await SendAccountActionAsync(adminClient, action, outsider.Id);

        (await MembershipCountAsync()).Should().Be(membersBefore);
        (await AccountStateAsync(outsider.Id)).Should().Be(accountBefore, "a stale page must not change the account");
        await AssertStaleToastAsync(adminClient, response);
    }

    [Theory]
    [InlineData("EditUser:GET")]
    [InlineData("EditUser:POST")]
    [InlineData("ResetPassword:GET")]
    [InlineData("ResetPassword:POST")]
    [InlineData("SendConfirmationEmail")]
    public async Task AccountAction_ForAnAccountThatNoLongerExists_AnswersTheStaleSentence(string action)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var membersBefore = await MembershipCountAsync();

        var response = await SendAccountActionAsync(adminClient, action, 987654);

        (await MembershipCountAsync()).Should().Be(membersBefore);
        await AssertStaleToastAsync(adminClient, response);
    }

    // ---- Race: a role change and a removal on the same row ------------------------------------------

    // One Admin's role change reads the membership, another Admin's removal deletes it, then the role change writes.
    // The write only updates an existing row, so it reports that nothing was changed and the person stays removed
    // instead of being put back on the board with the new role.
    [Fact]
    public async Task RoleChange_ThatLosesARaceWithARemoval_DoesNotPutThePersonBackOnTheBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var member = await SeedMemberAsync("raced", GroupRole.DungeonMaster);

        using var roleChangeScope = _factory.Services.CreateScope();
        var repository = roleChangeScope.ServiceProvider.GetRequiredService<QuestBoard.Domain.Interfaces.IUserRepository>();
        (await repository.GetGroupRoleAsync(member.Id, 1)).Should().Be(GroupRole.DungeonMaster);

        using (var removalScope = _factory.Services.CreateScope())
        {
            var groupService = removalScope.ServiceProvider.GetRequiredService<QuestBoard.Domain.Interfaces.IGroupService>();
            await groupService.RemoveMemberAsync(1, member.Id, TestContext.Current.CancellationToken);
        }

        var updated = await repository.UpdateGroupRoleAsync(member.Id, 1, GroupRole.Admin, TestContext.Current.CancellationToken);

        updated.Should().BeFalse("the membership the change read is gone");
        (await RoleOnBoardAsync(member.Id)).Should().BeNull("a role change must never re-create a removed membership");
    }

    [Fact]
    public async Task UpdateGroupRole_ForAnExistingMember_StillChangesTheRole()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var member = await SeedMemberAsync("updated", GroupRole.Player);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<QuestBoard.Domain.Interfaces.IUserRepository>();
        var updated = await repository.UpdateGroupRoleAsync(member.Id, 1, GroupRole.DungeonMaster, TestContext.Current.CancellationToken);

        updated.Should().BeTrue();
        (await RoleOnBoardAsync(member.Id)).Should().Be(GroupRole.DungeonMaster);
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
