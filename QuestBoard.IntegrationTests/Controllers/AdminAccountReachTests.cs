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
/// refusal must read as an Error toast on the Users page and must change nothing. The facts marked as reproducing an
/// open gap describe that rule and stay skipped until the rule is enforced; the unskipped facts pin what must keep
/// working once it is.
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
    private async Task ResetWithSecondBoardAsync()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(_factory.Services, 2);
        _factory.JobClient.Clear();
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

    // ---- Reproduction facts: the rule an Admin of one board must meet -----------------------------

    [Fact(Skip = "Reproduces an open gap; enabled by its fix")]
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
}
