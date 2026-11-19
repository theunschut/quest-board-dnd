using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Service.Jobs;
using QuestBoard.Service.ViewModels.AdminViewModels;

namespace QuestBoard.IntegrationTests.Controllers;

// Isolated in its own class (own WebApplicationFactoryBase fixture, own in-memory database,
// own DI container) so these tests never share Program.cs's emailResendLimiter singleton
// with AdminControllerIntegrationTests' other tests. That class has 8 tests calling
// TestDataHelper.ClearDatabaseAsync (EnsureDeleted+EnsureCreated), which resets the EF
// InMemory database's auto-increment id sequence — colliding a fresh target user's id with
// an earlier test's already-exhausted rate-limit budget for that same id, since the limiter
// itself never resets. Isolating these tests into their own class/fixture removes the
// possibility of that collision entirely, rather than relying on test-method execution order.
public class AdminControllerRateLimitIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;

    public AdminControllerRateLimitIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    // Creates an unconfirmed target user (via IUserService.CreateAsync, which mirrors
    // AdminController.CreateUser's passwordless/unconfirmed account creation) and returns its id.
    // Also assigns the target to group 1 (Player role), mirroring the group-membership row
    // AdminController.CreateUser creates via SetGroupRoleAsync — the tests using this helper
    // exercise admin actions against group 1, which now require the target to be a group 1
    // member per the membership guard added to those actions.
    private async Task<int> CreateUnconfirmedTargetUserAsync(string email, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        var createResult = await userService.CreateAsync(email, name);
        createResult.Succeeded.Should().BeTrue();

        var resolvedUserId = await identityService.GetIdByEmailAsync(email);
        resolvedUserId.Should().NotBeNull();

        await userService.SetGroupRoleAsync(resolvedUserId!.Value, 1, GroupRole.Player);

        return resolvedUserId!.Value;
    }

    private async Task<HttpResponseMessage> PostSendConfirmationEmailAsync(HttpClient adminClient, int targetUserId)
    {
        var getResponse = await adminClient.GetAsync("/Admin/Users", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
        if (!string.IsNullOrEmpty(cookieValue))
        {
            adminClient.DefaultRequestHeaders.Remove("Cookie");
            adminClient.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string> { ["userId"] = targetUserId.ToString() },
            token);

        return await adminClient.PostAsync("/Admin/SendConfirmationEmail", formContent, TestContext.Current.CancellationToken);
    }

    // A refused send is a redirect to the Users page carrying the approved Error toast. The toast travels in temp data
    // and is shown once, so it is read from the page the redirect lands on.
    private static async Task AssertRefusedWithTheRateLimitToastAsync(HttpClient client, HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "a refused send is an answer, not a bare status");
        response.Headers.Location!.OriginalString.Should().Contain("/Admin/Users");

        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        ToastKinds(html).Should().Contain("Error");
        html.Should().Contain(AdminActionMessages.EmailRateLimited);
    }

    private static async Task AssertRefusedWithTheCreateLimitToastAsync(HttpClient client, HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "a refused create is an answer, not a bare status");
        response.Headers.Location!.OriginalString.Should().Contain("/Admin/Users");

        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        ToastKinds(html).Should().Contain("Error");
        html.Should().Contain(AdminActionMessages.CreateLimited);
    }

    // The heading of every toast the page shows (Success, Error, Warning, Info).
    private static List<string> ToastKinds(string html) =>
        Regex.Matches(html, "<strong class=\"me-auto\">(\\w+)</strong>").Select(m => m.Groups[1].Value).ToList();

    private int WelcomeMailsSentTo(string email) =>
        _factory.JobClient.EnqueuedJobs
            .Count(j => j.Type == typeof(WelcomeEmailJob) && j.Args.Count > 0 && (j.Args[0] as string) == email);

    // The 4th resend to the SAME target user within the 1-hour window
    // must be refused with the rate-limit toast and send nothing. Program.cs's emailResendLimiter is a singleton
    // PartitionedRateLimiter<int> keyed "email-resend:{userId}", PermitLimit=3/hour — this test
    // uses a fresh target userId, and this class's dedicated fixture guarantees no sibling
    // test's exhausted budget bleeds in.
    [Fact]
    public async Task SendConfirmationEmail_ExceedingRateLimit_IsRefusedWithAToast_AndSendsNothingMore()
    {
        // Arrange
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var targetEmail = $"emailrate01_{uniqueSuffix}@example.com";
        var targetUserId = await CreateUnconfirmedTargetUserAsync(targetEmail, "Email Rate 01 Target");

        // Act — 4 rapid resends to the same target user.
        var responses = new List<HttpResponseMessage>();
        for (var i = 1; i <= 4; i++)
        {
            responses.Add(await PostSendConfirmationEmailAsync(adminClient, targetUserId));
        }

        // Assert — the fourth is the refusal; only the first three queued a mail.
        await AssertRefusedWithTheRateLimitToastAsync(adminClient, responses[3]);
        WelcomeMailsSentTo(targetEmail).Should().Be(3, "the refused fourth request must not send anything");
    }

    // Two DISTINCT target users must have INDEPENDENT resend budgets —
    // proving the partition key is the target userId (not e.g. the requesting admin or a
    // shared or global bucket).
    [Fact]
    public async Task SendConfirmationEmail_DifferentTargetUsers_HaveIndependentBudgets()
    {
        // Arrange
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var emailA = $"emailrate02a_{uniqueSuffix}@example.com";
        var emailB = $"emailrate02b_{uniqueSuffix}@example.com";
        var targetUserIdA = await CreateUnconfirmedTargetUserAsync(emailA, "Email Rate 02 Target A");
        var targetUserIdB = await CreateUnconfirmedTargetUserAsync(emailB, "Email Rate 02 Target B");

        // Act — 3 resends for A (within budget), then 3 for B (within budget), then a 4th for A.
        for (var i = 1; i <= 3; i++)
        {
            await PostSendConfirmationEmailAsync(adminClient, targetUserIdA);
        }

        for (var i = 1; i <= 3; i++)
        {
            await PostSendConfirmationEmailAsync(adminClient, targetUserIdB);
        }

        var fourthForA = await PostSendConfirmationEmailAsync(adminClient, targetUserIdA);

        // Assert — each target user's first 3 resends are within their own budgets (three mails each), while A's 4th
        // exceeds its own budget and is refused, proving B's separate budget was never touched by A's requests.
        WelcomeMailsSentTo(emailA).Should().Be(3, "target user A's first 3 resends should be within its own budget");
        WelcomeMailsSentTo(emailB).Should().Be(3, "target user B's first 3 resends should be within its own independent budget");
        await AssertRefusedWithTheRateLimitToastAsync(adminClient, fourthForA);
    }

    // EditUser's email-change path shares the same per-target-user
    // rate limit as SendConfirmationEmail (only email-changing saves are counted); a refusal answers with the same toast.
    [Fact]
    public async Task EditUser_EmailChange_ExceedingRateLimit_IsRefusedWithAToast()
    {
        // Arrange
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var targetUserId = await CreateUnconfirmedTargetUserAsync(
            $"emailrate03_{uniqueSuffix}@example.com", "Email Rate 03 Target");

        async Task<HttpResponseMessage> PostEditUserWithEmailChangeAsync(string newEmail)
        {
            // Note: EditUser's GET action parameter is named "userId" (not "id"), so the
            // default {controller}/{action}/{id?} route pattern cannot bind it positionally —
            // it must be passed as a query string parameter.
            var getResponse = await adminClient.GetAsync($"/Admin/EditUser?userId={targetUserId}", TestContext.Current.CancellationToken);
            var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
            if (!string.IsNullOrEmpty(cookieValue))
            {
                adminClient.DefaultRequestHeaders.Remove("Cookie");
                adminClient.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
            }

            var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
                new Dictionary<string, string>
                {
                    ["Id"] = targetUserId.ToString(),
                    ["Name"] = "Email Rate 03 Target",
                    ["Email"] = newEmail,
                    ["HasKey"] = "false"
                },
                token);

            return await adminClient.PostAsync("/Admin/EditUser", formContent, TestContext.Current.CancellationToken);
        }

        // Act — 4 rapid email-changing saves for the same target user, each with a distinct
        // new email so ModelState stays valid and the emailChanged branch is entered every time.
        var responses = new List<HttpResponseMessage>();
        for (var i = 1; i <= 4; i++)
        {
            responses.Add(await PostEditUserWithEmailChangeAsync($"emailrate03_changed{i}_{uniqueSuffix}@example.com"));
        }

        // Assert — the fourth email-changing save is the refusal.
        await AssertRefusedWithTheRateLimitToastAsync(adminClient, responses[3]);
    }

    // CreateUser's one-shot automated welcome email is explicitly EXEMPT
    // from the resend rate limit — 4 distinct new-account creations must never 429.
    [Fact]
    public async Task CreateUser_RapidRequests_ShouldNotBeRateLimited()
    {
        // Arrange
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

        async Task<HttpResponseMessage> PostCreateUserAsync(int index)
        {
            var getResponse = await adminClient.GetAsync("/Admin/CreateUser", TestContext.Current.CancellationToken);
            var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
            if (!string.IsNullOrEmpty(cookieValue))
            {
                adminClient.DefaultRequestHeaders.Remove("Cookie");
                adminClient.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
            }

            var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
                new Dictionary<string, string>
                {
                    ["Email"] = $"emailrate04_{index}_{uniqueSuffix}@example.com",
                    ["Name"] = $"Email Rate 04 User {index}",
                    ["GroupRole"] = ((int)GroupRole.Player).ToString()
                },
                token);

            return await adminClient.PostAsync("/Admin/CreateUser", formContent, TestContext.Current.CancellationToken);
        }

        // Act — 4 distinct new-user creations in rapid succession.
        var responses = new List<HttpResponseMessage>();
        for (var i = 1; i <= 4; i++)
        {
            responses.Add(await PostCreateUserAsync(i));
        }

        // Assert — none of the 4 distinct CreateUser POSTs were rate-limited (exempted).
        responses.Should().NotContain(r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    // The resend budget belongs to the person receiving the mail. A resend that is refused because the person is not on
    // the Admin's board must not spend any of it, or an Admin of one board could lock the person's own board Admin out
    // for an hour. The person is on board 2 only; an Admin of board 1 asks three times, then the Admin of board 2 asks.
    [Fact]
    public async Task SendConfirmationEmail_ResendsRefusedAsOutOfBoard_DoNotSpendTheTargetsBudget()
    {
        // Arrange
        await TestDataHelper.SeedCampaignGroupAsync(_factory.Services, 2);
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var targetEmail = $"emailrate05_{uniqueSuffix}@example.com";
        int targetUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            (await userService.CreateAsync(targetEmail, "Email Rate 05 Target")).Succeeded.Should().BeTrue();
            targetUserId = (await identityService.GetIdByEmailAsync(targetEmail))!.Value;
            await userService.SetGroupRoleAsync(targetUserId, 2, GroupRole.Player);
        }

        var (otherBoardAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, $"emailrate05_a_{uniqueSuffix}", $"emailrate05_a_{uniqueSuffix}@example.com");
        var (ownBoardAdminClient, ownBoardAdmin) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, $"emailrate05_b_{uniqueSuffix}", $"emailrate05_b_{uniqueSuffix}@example.com");
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.UserGroups.Add(new UserGroupEntity { UserId = ownBoardAdmin.Id, GroupId = 2, GroupRole = (int)GroupRole.Admin });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act — three resends from an Admin of board 1 (the person is not on it), then the person's own board Admin.
        for (var i = 1; i <= 3; i++)
        {
            await PostSendConfirmationEmailAsync(otherBoardAdminClient, targetUserId);
        }

        HttpResponseMessage ownBoardResponse;
        _factory.TestGroupContext.ActiveGroupId = 2;
        try
        {
            ownBoardResponse = await PostSendConfirmationEmailAsync(ownBoardAdminClient, targetUserId);
        }
        finally
        {
            _factory.TestGroupContext.ActiveGroupId = 1;
        }

        // Assert
        ownBoardResponse.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
            "requests refused as out of board must not have used the person's hourly budget");
        _factory.JobClient.EnqueuedJobs
            .Count(j => j.Type == typeof(WelcomeEmailJob) && j.Args.Count > 0 && (j.Args[0] as string) == targetEmail)
            .Should().Be(1, "the person's own board Admin must be able to send the welcome mail");
    }

    // A refused email change must change nothing: the name and key flag were being saved before the hourly limit was
    // consulted, so a refusal still left half of the edit behind.
    [Fact]
    public async Task EditUser_RefusedEmailChange_LeavesTheNameUnchanged()
    {
        // Arrange
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var targetUserId = await CreateUnconfirmedTargetUserAsync(
            $"emailrate06_{uniqueSuffix}@example.com", "Email Rate 06 Target");

        async Task<HttpResponseMessage> PostEditAsync(string name, string newEmail) =>
            await adminClient.PostAsync("/Admin/EditUser", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = targetUserId.ToString(),
                ["Name"] = name,
                ["Email"] = newEmail,
                ["HasKey"] = "false"
            }), TestContext.Current.CancellationToken);

        for (var i = 1; i <= 3; i++)
        {
            await PostEditAsync("Email Rate 06 Target", $"emailrate06_changed{i}_{uniqueSuffix}@example.com");
        }

        // Act — the fourth email-changing save in the hour is over the limit and is refused.
        var refused = await PostEditAsync("Changed By A Refused Save", $"emailrate06_changed4_{uniqueSuffix}@example.com");

        // Assert
        using var scope = _factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var reloaded = await userService.GetByIdAsync(targetUserId, TestContext.Current.CancellationToken);
        reloaded!.Name.Should().Be("Email Rate 06 Target", "a save that is refused must not write any of its fields");
        refused.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    // ---- The per-administrator limit on creating accounts ---------------------------------------

    private static Task<HttpResponseMessage> PostCreateUserAsync(HttpClient client, string email, string name) =>
        client.PostAsync("/Admin/CreateUser", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Name"] = name,
            ["GroupRole"] = ((int)GroupRole.Player).ToString()
        }), TestContext.Current.CancellationToken);

    private async Task<int?> AccountIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IIdentityService>().GetIdByEmailAsync(email);
    }

    // One administrator may create twenty accounts an hour. The twenty-first is answered with the Error toast and
    // creates nothing; a different administrator has a budget of their own and is not affected.
    [Fact]
    public async Task CreateUser_BeyondTwentyAnHourByOneAdministrator_IsRefusedWithAToast_AndOtherAdministratorsAreUnaffected()
    {
        // Arrange
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var (busyAdmin, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, $"emailrate07_a_{uniqueSuffix}", $"emailrate07_a_{uniqueSuffix}@example.com");
        var (otherAdmin, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, $"emailrate07_b_{uniqueSuffix}", $"emailrate07_b_{uniqueSuffix}@example.com");

        // Act — twenty accounts are created, then a twenty-first.
        for (var i = 1; i <= 20; i++)
        {
            (await PostCreateUserAsync(busyAdmin, $"emailrate07_{i}_{uniqueSuffix}@example.com", $"Email Rate 07 User {i}"))
                .StatusCode.Should().Be(HttpStatusCode.Redirect);
        }

        var twentyFirstEmail = $"emailrate07_21_{uniqueSuffix}@example.com";
        var refused = await PostCreateUserAsync(busyAdmin, twentyFirstEmail, "Email Rate 07 User 21");
        var otherAdminEmail = $"emailrate07_other_{uniqueSuffix}@example.com";
        var unaffected = await PostCreateUserAsync(otherAdmin, otherAdminEmail, "Email Rate 07 Other");

        // Assert
        (await AccountIdAsync($"emailrate07_20_{uniqueSuffix}@example.com")).Should().NotBeNull("the twentieth create is still within the budget");
        (await AccountIdAsync(twentyFirstEmail)).Should().BeNull("a refused create must not leave an account behind");
        await AssertRefusedWithTheCreateLimitToastAsync(busyAdmin, refused);

        unaffected.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await AccountIdAsync(otherAdminEmail)).Should().NotBeNull("another administrator's budget is separate");
    }

    // A request that fails validation never reaches the limiter, so mistyped forms do not use up the budget.
    [Fact]
    public async Task CreateUser_InvalidForm_DoesNotSpendTheCreateBudget()
    {
        // Arrange
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, $"emailrate08_{uniqueSuffix}", $"emailrate08_{uniqueSuffix}@example.com");

        // Act — twenty invalid posts (no email), then a valid one.
        for (var i = 1; i <= 20; i++)
        {
            (await PostCreateUserAsync(client, string.Empty, "Email Rate 08 Invalid")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var validEmail = $"emailrate08_valid_{uniqueSuffix}@example.com";
        var valid = await PostCreateUserAsync(client, validEmail, "Email Rate 08 Valid");

        // Assert
        valid.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await AccountIdAsync(validEmail)).Should().NotBeNull();
    }
}
