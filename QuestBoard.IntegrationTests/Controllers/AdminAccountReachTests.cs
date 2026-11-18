using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Service.Jobs;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Which accounts a board Admin may change from a board's Users page. An Admin of one board may reset the password of,
/// edit and re-send the welcome mail to the Players, Dungeon Masters and fellow Admins of that board, but not an
/// account that carries more reach than that: a platform administrator, or an Admin of some other board. Each
/// refusal must read as an Error toast on the Users page and must change nothing. The first group of facts states the
/// refusals; the second pins what stays allowed: members of the same board, a peer Admin, the Admin's own account, an
/// ordinary account that also belongs to another board, and everything for a platform administrator.
/// </summary>
public class AdminAccountReachTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string NewPassword = "BrandNewAdminSet123!";
    private const string OriginalPassword = "Test123!";

    private readonly WebApplicationFactoryBase _factory;

    public AdminAccountReachTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    // ---- Seeding helpers --------------------------------------------------------------------------

    // Clears the database and seeds a second board, so an account can administer a board other than the active one.
    // Clearing restarts the id sequence, and the hourly mail budget is kept per account id by the running app, so a
    // test that sends mail passes a distinct id offset: its accounts then get ids no other test's accounts have.
    private async Task ResetWithSecondBoardAsync(int idOffset = 0)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(_factory.Services, 2);
        _factory.JobClient.Clear();

        if (idOffset > 0)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            for (var i = 0; i < idOffset; i++)
            {
                context.Users.Add(new UserEntity { UserName = $"idfiller_{i}", Email = $"idfiller_{i}@example.com", Name = "Id filler" });
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<UserEntity> SeedAccountAsync(string tag, params (int GroupId, GroupRole Role)[] memberships)
    {
        var account = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, $"reach_{tag}", $"reach_{tag}@example.com", OriginalPassword, $"Reach {tag}");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        foreach (var (groupId, role) in memberships)
        {
            context.UserGroups.Add(new UserGroupEntity { UserId = account.Id, GroupId = groupId, GroupRole = (int)role });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return account;
    }

    private async Task MakePlatformAdministratorAsync(UserEntity account)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        var tracked = await userManager.FindByIdAsync(account.Id.ToString());
        (await userManager.AddToRoleAsync(tracked!, "SuperAdmin")).Succeeded.Should().BeTrue();
    }

    // An account that never confirmed its address, as the welcome-mail resend expects.
    private async Task<int> SeedUnconfirmedAccountAsync(string tag, params (int GroupId, GroupRole Role)[] memberships)
    {
        using var scope = _factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var email = $"reach_{tag}_{Guid.NewGuid().ToString("N")[..8]}@example.com";
        (await userService.CreateAsync(email, $"Reach {tag}")).Succeeded.Should().BeTrue();
        var id = (await identityService.GetIdByEmailAsync(email))!.Value;
        foreach (var (groupId, role) in memberships)
        {
            await userService.SetGroupRoleAsync(id, groupId, role);
        }

        return id;
    }

    private async Task<string?> PasswordHashAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        return (await userManager.FindByIdAsync(userId.ToString()))?.PasswordHash;
    }

    private async Task<UserEntity> ReloadAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        return (await userManager.FindByIdAsync(userId.ToString()))!;
    }

    private static Task<HttpResponseMessage> PostResetPasswordAsync(HttpClient client, int targetId) =>
        client.PostAsync("/Admin/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = targetId.ToString(),
            ["UserName"] = "Target",
            ["NewPassword"] = NewPassword,
            ["ConfirmPassword"] = NewPassword
        }), TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> PostEditUserAsync(HttpClient client, int targetId, string name, string email) =>
        client.PostAsync("/Admin/EditUser", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = targetId.ToString(),
            ["Name"] = name,
            ["Email"] = email,
            ["HasKey"] = "false"
        }), TestContext.Current.CancellationToken);

    // A refusal is a redirect to the Users page; the Error toast is read from the page the redirect lands on, because
    // the toast travels in temp data and is shown once.
    private static async Task AssertRefusedWithErrorToastAsync(HttpClient client, HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "a refused account change is an answer, not a page");
        response.Headers.Location!.OriginalString.Should().Contain("/Admin/Users");

        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        ToastKinds(html).Should().Contain("Error", "the refusal must be visible as an Error toast");
    }

    // The heading of every toast the page shows (Success, Error, Warning, Info), kept short so a failing run prints
    // a readable list instead of a whole page.
    private static List<string> ToastKinds(string html) =>
        Regex.Matches(html, "<strong class=\"me-auto\">(\\w+)</strong>").Select(m => m.Groups[1].Value).ToList();

    // ---- Refusals: the rule an Admin of one board must meet ---------------------------------------

    [Fact]
    public async Task ResetPassword_Post_ForAnAdminOfAnotherBoard_IsRefusedWithAnErrorToast_AndThePasswordIsUnchanged()
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var target = await SeedAccountAsync("otheradmin", (1, GroupRole.Player), (2, GroupRole.Admin));
        var hashBefore = await PasswordHashAsync(target.Id);

        var response = await PostResetPasswordAsync(adminClient, target.Id);

        var passwordChanged = await PasswordHashAsync(target.Id) != hashBefore;
        passwordChanged.Should().BeFalse("the account of another board's Admin must not be taken over");
        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    [Fact]
    public async Task ResetPassword_Post_ForAPlatformAdministrator_IsRefusedWithAnErrorToast_AndThePasswordIsUnchanged()
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var target = await SeedAccountAsync("platformadmin", (1, GroupRole.Player));
        await MakePlatformAdministratorAsync(target);
        var hashBefore = await PasswordHashAsync(target.Id);

        var response = await PostResetPasswordAsync(adminClient, target.Id);

        var passwordChanged = await PasswordHashAsync(target.Id) != hashBefore;
        passwordChanged.Should().BeFalse("a platform administrator's account must not be taken over by a board Admin");
        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResetPassword_Get_ForAnAccountWithMoreReach_RedirectsWithAnErrorToast(bool platformAdministrator)
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var target = platformAdministrator
            ? await SeedAccountAsync("getplatform", (1, GroupRole.Player))
            : await SeedAccountAsync("getotheradmin", (1, GroupRole.Player), (2, GroupRole.Admin));
        if (platformAdministrator)
            await MakePlatformAdministratorAsync(target);

        var response = await adminClient.GetAsync($"/Admin/ResetPassword?userId={target.Id}", TestContext.Current.CancellationToken);

        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EditUser_Post_ForAnAccountWithMoreReach_IsRefused_AndNameAndEmailAreUnchanged(bool platformAdministrator)
    {
        await ResetWithSecondBoardAsync(platformAdministrator ? 10 : 20);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var target = platformAdministrator
            ? await SeedAccountAsync("editplatform", (1, GroupRole.Player))
            : await SeedAccountAsync("editotheradmin", (1, GroupRole.Player), (2, GroupRole.Admin));
        if (platformAdministrator)
            await MakePlatformAdministratorAsync(target);

        var response = await PostEditUserAsync(adminClient, target.Id, "Renamed By Board Admin", $"moved_{target.Email}");

        var after = await ReloadAsync(target.Id);
        after.Name.Should().Be(target.Name, "the account's name belongs to its owner, not to a board Admin");
        after.Email.Should().Be(target.Email, "no address change may be started for an account with more reach");
        _factory.JobClient.EnqueuedJobs.Count(j => j.Type == typeof(ChangeEmailConfirmationJob)).Should().Be(0,
            "no confirmation mail may be sent to an address chosen for an account with more reach");
        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EditUser_Get_ForAnAccountWithMoreReach_RedirectsWithAnErrorToast(bool platformAdministrator)
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var target = platformAdministrator
            ? await SeedAccountAsync("geteditplatform", (1, GroupRole.Player))
            : await SeedAccountAsync("geteditotheradmin", (1, GroupRole.Player), (2, GroupRole.Admin));
        if (platformAdministrator)
            await MakePlatformAdministratorAsync(target);

        var response = await adminClient.GetAsync($"/Admin/EditUser?userId={target.Id}", TestContext.Current.CancellationToken);

        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendConfirmationEmail_ForAnAccountWithMoreReach_IsRefused_AndNoMailIsQueued(bool platformAdministrator)
    {
        await ResetWithSecondBoardAsync(platformAdministrator ? 30 : 40);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var targetId = platformAdministrator
            ? await SeedUnconfirmedAccountAsync("resendplatform", (1, GroupRole.Player))
            : await SeedUnconfirmedAccountAsync("resendotheradmin", (1, GroupRole.Player), (2, GroupRole.Admin));
        if (platformAdministrator)
            await MakePlatformAdministratorAsync(await ReloadAsync(targetId));

        var response = await adminClient.PostAsync("/Admin/SendConfirmationEmail",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["userId"] = targetId.ToString() }),
            TestContext.Current.CancellationToken);

        _factory.JobClient.EnqueuedJobs.Count(j => j.Type == typeof(WelcomeEmailJob)).Should().Be(0,
            "a welcome mail must not be sent for an account the board Admin has no reach over");
        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    // Adding an existing account to the board by its address is what makes it a "member of this board", so the reach
    // rule has to hold for the account added that way as well.
    [Fact]
    public async Task CreateUser_WithAPlatformAdministratorsAddress_ThenResetPassword_IsRefused()
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var platformAdministrator = await SeedAccountAsync("chainplatform");
        await MakePlatformAdministratorAsync(platformAdministrator);
        var hashBefore = await PasswordHashAsync(platformAdministrator.Id);

        await adminClient.PostAsync("/Admin/CreateUser", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = platformAdministrator.Email!,
            ["Name"] = "Added By Address",
            ["GroupRole"] = ((int)GroupRole.Player).ToString()
        }), TestContext.Current.CancellationToken);
        var response = await PostResetPasswordAsync(adminClient, platformAdministrator.Id);

        var passwordChanged = await PasswordHashAsync(platformAdministrator.Id) != hashBefore;
        passwordChanged.Should().BeFalse("adding the account by its address must not open it to a password reset");
        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    // ---- Allowed: what a board Admin and a platform administrator can still do --------------------

    [Theory]
    [InlineData(GroupRole.Player)]
    [InlineData(GroupRole.DungeonMaster)]
    [InlineData(GroupRole.Admin)]
    public async Task ResetPassword_Post_ForAMemberOfTheSameBoard_StillSucceeds(GroupRole role)
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var target = await SeedAccountAsync($"samereset{(int)role}", (1, role));

        var response = await PostResetPasswordAsync(adminClient, target.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("/Admin/Users");
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        var reloaded = await userManager.FindByIdAsync(target.Id.ToString());
        (await userManager.CheckPasswordAsync(reloaded!, NewPassword)).Should().BeTrue();
    }

    [Theory]
    [InlineData(GroupRole.Player)]
    [InlineData(GroupRole.DungeonMaster)]
    [InlineData(GroupRole.Admin)]
    public async Task EditUser_Post_ForAMemberOfTheSameBoard_StillSavesTheName(GroupRole role)
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var target = await SeedAccountAsync($"sameedit{(int)role}", (1, role));

        var response = await PostEditUserAsync(adminClient, target.Id, "Renamed Member", target.Email!);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await ReloadAsync(target.Id)).Name.Should().Be("Renamed Member");
    }

    [Fact]
    public async Task ResetPassword_Post_BySuperAdmin_StillSucceedsForAnAdminOfAnotherBoard()
    {
        await ResetWithSecondBoardAsync();
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var target = await SeedAccountAsync("superreset", (1, GroupRole.Player), (2, GroupRole.Admin));

        var response = await PostResetPasswordAsync(superAdminClient, target.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        var reloaded = await userManager.FindByIdAsync(target.Id.ToString());
        (await userManager.CheckPasswordAsync(reloaded!, NewPassword)).Should().BeTrue();
    }

    [Fact]
    public async Task EditUser_Post_BySuperAdmin_StillSavesTheNameOfAnotherPlatformAdministrator()
    {
        await ResetWithSecondBoardAsync();
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var target = await SeedAccountAsync("superedit", (1, GroupRole.Player));
        await MakePlatformAdministratorAsync(target);

        var response = await PostEditUserAsync(superAdminClient, target.Id, "Renamed Platform Administrator", target.Email!);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await ReloadAsync(target.Id)).Name.Should().Be("Renamed Platform Administrator");
    }

    [Fact]
    public async Task ResetPassword_Post_ForAnOrdinaryAccountThatAlsoBelongsToAnotherBoard_StillSucceeds()
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var target = await SeedAccountAsync("sharedplayer", (1, GroupRole.Player), (2, GroupRole.Player));

        var response = await PostResetPasswordAsync(adminClient, target.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        var reloaded = await userManager.FindByIdAsync(target.Id.ToString());
        (await userManager.CheckPasswordAsync(reloaded!, NewPassword)).Should().BeTrue(
            "belonging to another board as an ordinary member does not put an account out of reach");
    }

    [Fact]
    public async Task ResetPassword_Post_ForAnAdminOfTheSameBoardWhoAlsoAdministersAnotherBoard_IsRefused()
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var target = await SeedAccountAsync("sharedadmin", (1, GroupRole.Admin), (2, GroupRole.Admin));
        var hashBefore = await PasswordHashAsync(target.Id);

        var response = await PostResetPasswordAsync(adminClient, target.Id);

        (await PasswordHashAsync(target.Id)).Should().Be(hashBefore);
        await AssertRefusedWithErrorToastAsync(adminClient, response);
    }

    [Fact]
    public async Task ResetPassword_Post_ForTheSignedInAdminsOwnAccount_StillSucceeds_EvenWhenTheyAdministerAnotherBoard()
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, admin) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.UserGroups.Add(new UserGroupEntity { UserId = admin.Id, GroupId = 2, GroupRole = (int)GroupRole.Admin });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await PostResetPasswordAsync(adminClient, admin.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("/Admin/Users");
        var landed = await adminClient.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        ToastKinds(WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)))
            .Should().Contain("Success");
    }

    [Fact]
    public async Task EditUser_Post_ForTheSignedInAdminsOwnAccount_StillSavesTheName_EvenWhenTheyAdministerAnotherBoard()
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, admin) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.UserGroups.Add(new UserGroupEntity { UserId = admin.Id, GroupId = 2, GroupRole = (int)GroupRole.Admin });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var own = await ReloadAsync(admin.Id);

        var response = await PostEditUserAsync(adminClient, admin.Id, "Own New Name", own.Email!);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await ReloadAsync(admin.Id)).Name.Should().Be("Own New Name");
    }

    [Fact]
    public async Task ResetPassword_Post_Success_ShowsTheNamedSuccessToastOnTheUsersPage()
    {
        await ResetWithSecondBoardAsync();
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var target = await SeedAccountAsync("toasttarget", (1, GroupRole.Player));

        var response = await PostResetPasswordAsync(adminClient, target.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var landed = await adminClient.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        ToastKinds(html).Should().Contain("Success");
        html.Should().Contain($"Password reset successfully for {target.Name}!");
    }
}
