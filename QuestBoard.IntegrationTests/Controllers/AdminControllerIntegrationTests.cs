using Microsoft.AspNetCore.Identity;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class AdminControllerIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;
    private readonly HttpClient _client;

    public AdminControllerIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        // Use non-redirecting client to properly test authorization redirects
        _client = factory.CreateNonRedirectingClient();
    }

    [Fact]
    public async Task Index_WhenNotAuthenticated_ShouldRedirectToLogin()
    {
        // Act - Changed from /Admin to /Admin/Users (actual route name)
        var response = await _client.GetAsync("/Admin/Users", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Index_WhenNotAdmin_ShouldReturnForbidden()
    {
        // Arrange - Create user with Player role (not Admin)
        var (regularClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "regularuser", "regular@example.com", roles: ["Player"]);

        // Act - Changed from /Admin to /Admin/Users (actual route name)
        var response = await regularClient.GetAsync("/Admin/Users", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ManageUsers_WhenNotAuthenticated_ShouldRedirectToLogin()
    {
        // Act - Changed from /Admin/ManageUsers to /Admin/Users (actual route name)
        var response = await _client.GetAsync("/Admin/Users", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/Admin/EditUser/1")]
    [InlineData("/Admin/DeleteUser/1")]
    [InlineData("/Admin/ResetPassword/1")]
    public async Task AdminActions_WhenNotAuthenticated_ShouldRedirectToLogin(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint, TestContext.Current.CancellationToken);

        // Assert
        // Note: DeleteUser returns 405 Method Not Allowed since it requires DELETE not GET
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    // Regression: a real Admin completing AdminResetPassword end-to-end must succeed —
    // IdentityService.AdminResetPasswordAsync's redundant inner IsInRoleAsync(admin, "Admin")
    // check (always false post-Phase-27) previously blocked every reset unconditionally.
    [Fact]
    public async Task ResetPassword_Post_WhenAdmin_SucceedsForTargetUser()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var targetUserId = await CreateUnconfirmedTargetUserAsync(
            $"resetpwtarget_{uniqueSuffix}@example.com", "Reset Password Target");

        var formData = new Dictionary<string, string>
        {
            ["UserId"] = targetUserId.ToString(),
            ["UserName"] = "Reset Password Target",
            ["NewPassword"] = "BrandNewAdminSet123!",
            ["ConfirmPassword"] = "BrandNewAdminSet123!"
        };

        // Act
        var response = await adminClient.PostAsync("/Admin/ResetPassword",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        // Assert — a successful reset redirects to Users with a success message,
        // NOT the "Admin user not found or not authorized" ModelState failure that
        // would re-render the ResetPassword form (200) instead of redirecting.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Contain("Users");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        var updatedUser = await userManager.FindByIdAsync(targetUserId.ToString());
        updatedUser.Should().NotBeNull();
        var passwordCheck = await userManager.CheckPasswordAsync(updatedUser!, "BrandNewAdminSet123!");
        passwordCheck.Should().BeTrue();
    }

    [Fact]
    public async Task EmailStats_WhenNotAuthenticated_ShouldRedirectToLogin()
    {
        // Act
        var response = await _client.GetAsync("/Admin/EmailStats", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmailStats_WhenNotAdmin_ShouldReturnForbidden()
    {
        // Arrange - Create user with Player role (not Admin)
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "emailstatsuser", "emailstats@example.com", roles: ["Player"]);

        // Act
        var response = await playerClient.GetAsync("/Admin/EmailStats", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected()
    {
        // Arrange - Create user with Admin role (not SuperAdmin)
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "emailstatsadmin", "emailstatsadmin@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.GetAsync("/Admin/EmailStats", TestContext.Current.CancellationToken);

        // Assert - an Admin who is not also a SuperAdmin must be rejected server-side
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmailStats_WhenSuperAdmin_ShouldSucceed()
    {
        // Arrange - Create user with SuperAdmin role
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        // Act
        var response = await superAdminClient.GetAsync("/Admin/EmailStats", TestContext.Current.CancellationToken);

        // Assert - SuperAdmin passes both AdminOnly and SuperAdminOnly
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CreateUser auth gating — a non-admin must not reach the form
    [Fact]
    public async Task CreateUser_WhenNotAdmin_ShouldBeForbidden()
    {
        // Arrange
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "createuserplayer", "createuserplayer@example.com", roles: ["Player"]);

        // Act
        var response = await playerClient.GetAsync("/Admin/CreateUser", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    // An admin can reach the CreateUser form
    [Fact]
    public async Task CreateUser_Get_WhenAdmin_ShouldReturnForm()
    {
        // Arrange
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        // Act
        var response = await adminClient.GetAsync("/Admin/CreateUser", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("CreateUser");
        content.Should().Contain("GroupRole");
    }

    // Admin-created users are assigned to the admin's
    // active group with the chosen GroupRole, created passwordless, and the Welcome email
    // job fires (targeting the SetPassword callback) instead of an admin-set password.
    [Fact]
    public async Task CreateUser_Post_WhenAdmin_CreatesUserInActiveGroup()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var newUserEmail = $"createduser_{uniqueSuffix}@example.com";
        // No Password field is submitted — CreateUser is passwordless.
        var formData = new Dictionary<string, string>
        {
            ["Email"] = newUserEmail,
            ["Name"] = "Created User",
            ["GroupRole"] = ((int)GroupRole.DungeonMaster).ToString()
        };

        // Act — _factory.TestGroupContext.ActiveGroupId defaults to 1 (the seeded EuphoriaInn group)
        var response = await adminClient.PostAsync("/Admin/CreateUser",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        var createdUser = await userManager.FindByEmailAsync(newUserEmail);
        createdUser.Should().NotBeNull();
        createdUser!.Name.Should().Be("Created User");
        createdUser.PasswordHash.Should().BeNull("accounts are created passwordless (PWFLOW-01/D-01)");
        createdUser.EmailConfirmed.Should().BeFalse("email is only confirmed once the user completes SetPassword (D-09)");

        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var membership = context.UserGroups.FirstOrDefault(ug => ug.UserId == createdUser.Id && ug.GroupId == 1);
        membership.Should().NotBeNull("the created user should be assigned to the admin's active group");
        membership!.GroupRole.Should().Be((int)GroupRole.DungeonMaster);
    }

    // SendConfirmationEmail (the "Resend Welcome Email" action) succeeds for an
    // unconfirmed user and redirects to Users with a success outcome. Job-enqueue internals
    // are not observable via HTTP (covered by Plan 02's WelcomeEmailJobTests unit tests).
    [Fact]
    public async Task SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var targetEmail = $"resendwelcome_{uniqueSuffix}@example.com";

        int targetUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

            var createResult = await userService.CreateAsync(targetEmail, "Resend Welcome User");
            createResult.Succeeded.Should().BeTrue();

            var resolvedUserId = await identityService.GetIdByEmailAsync(targetEmail);
            resolvedUserId.Should().NotBeNull();
            targetUserId = resolvedUserId!.Value;
        }

        var getResponse = await adminClient.GetAsync("/Admin/Users", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
        if (!string.IsNullOrEmpty(cookieValue))
        {
            adminClient.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string> { ["userId"] = targetUserId.ToString() },
            token);

        // Act
        var response = await adminClient.PostAsync("/Admin/SendConfirmationEmail", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Contain("Users");
    }

    // A group admin whose active group is group 1 must never see a user who belongs
    // only to a different group on the Users management page.
    [Fact]
    public async Task Users_WhenAdmin_DoesNotShowUsersFromOtherGroups()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(_factory.Services, 2);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        var inGroupMarker = $"InGroupOne {Guid.NewGuid():N}";
        var outOfGroupMarker = $"InGroupTwoOnly {Guid.NewGuid():N}";

        var inGroupUser = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "ingroupuser", "ingroupuser@example.com", name: inGroupMarker);
        var outOfGroupUser = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "outofgroupuser", "outofgroupuser@example.com", name: outOfGroupMarker);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.UserGroups.Add(new UserGroupEntity { UserId = inGroupUser.Id, GroupId = 1, GroupRole = (int)GroupRole.Player });
            context.UserGroups.Add(new UserGroupEntity { UserId = outOfGroupUser.Id, GroupId = 2, GroupRole = (int)GroupRole.Player });
            await context.SaveChangesAsync();
        }

        // Act
        var response = await adminClient.GetAsync("/Admin/Users", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain(inGroupMarker, "in-group members must still render");
        content.Should().NotContain(outOfGroupMarker, "a group admin must never see a user who belongs only to a different group");
    }

    // Creates an unconfirmed target user (via IUserService.CreateAsync, which mirrors
    // AdminController.CreateUser's passwordless/unconfirmed account creation) and returns its id.
    private async Task<int> CreateUnconfirmedTargetUserAsync(string email, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        var createResult = await userService.CreateAsync(email, name);
        createResult.Succeeded.Should().BeTrue();

        var resolvedUserId = await identityService.GetIdByEmailAsync(email);
        resolvedUserId.Should().NotBeNull();
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

    // The 4th resend to the SAME target user within the 1-hour window
    // must be rejected with 429. Program.cs's emailResendLimiter is a process-wide singleton
    // PartitionedRateLimiter<int> keyed "email-resend:{userId}", PermitLimit=3/hour — this test
    // uses a fresh, unique target userId so no sibling test's exhausted budget bleeds in.
    [Fact]
    public async Task SendConfirmationEmail_ExceedingRateLimit_ShouldReturn429()
    {
        // Arrange — deliberately NOT calling ClearDatabaseAsync: it does EnsureDeleted+EnsureCreated,
        // which resets auto-increment IDs, while the emailResendLimiter singleton is process-wide and
        // NEVER resets. Wiping the DB risks a freshly-created target user reusing an integer id that
        // a prior test already exhausted the budget for. A unique userId (guaranteed by not resetting
        // the id sequence) is the correct isolation mechanism, per this plan's isolation note.
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var targetUserId = await CreateUnconfirmedTargetUserAsync(
            $"emailrate01_{uniqueSuffix}@example.com", "Email Rate 01 Target");

        // Act — 4 rapid resends to the same target user.
        var responses = new List<HttpResponseMessage>();
        for (var i = 1; i <= 4; i++)
        {
            responses.Add(await PostSendConfirmationEmailAsync(adminClient, targetUserId));
        }

        // Assert — at least one of the 4 rapid requests was rejected with 429 (mirrors the
        // ForgotPassword rate-limit analog's "at least one" assertion for robustness).
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    // Two DISTINCT target users must have INDEPENDENT resend budgets —
    // proving the partition key is the target userId (not e.g. the requesting admin or a
    // shared/global bucket, which would be the RESEARCH Pitfall 1 "unknown partition" collapse).
    [Fact]
    public async Task SendConfirmationEmail_DifferentTargetUsers_ShouldHaveIndependentBudgets()
    {
        // Arrange — no ClearDatabaseAsync (see isolation note above); unique target userIds are
        // guaranteed by the untouched auto-increment sequence, not by wiping the database.
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var targetUserIdA = await CreateUnconfirmedTargetUserAsync(
            $"emailrate02a_{uniqueSuffix}@example.com", "Email Rate 02 Target A");
        var targetUserIdB = await CreateUnconfirmedTargetUserAsync(
            $"emailrate02b_{uniqueSuffix}@example.com", "Email Rate 02 Target B");

        // Act — 3 resends for A (within budget), then 3 for B (within budget), then a 4th for A.
        var responsesA = new List<HttpResponseMessage>();
        for (var i = 1; i <= 3; i++)
        {
            responsesA.Add(await PostSendConfirmationEmailAsync(adminClient, targetUserIdA));
        }

        var responsesB = new List<HttpResponseMessage>();
        for (var i = 1; i <= 3; i++)
        {
            responsesB.Add(await PostSendConfirmationEmailAsync(adminClient, targetUserIdB));
        }

        var fourthForA = await PostSendConfirmationEmailAsync(adminClient, targetUserIdA);

        // Assert — each target user's first-3 sequence is 429-free (independent budgets),
        // while A's 4th request over its own budget is rejected, proving B's separate budget
        // was never touched by A's requests.
        responsesA.Should().NotContain(r => r.StatusCode == HttpStatusCode.TooManyRequests,
            "target user A's first 3 resends should be within its own budget");
        responsesB.Should().NotContain(r => r.StatusCode == HttpStatusCode.TooManyRequests,
            "target user B's first 3 resends should be within its own independent budget");
        fourthForA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "target user A's 4th resend should exceed its own budget, proving the partition key is per-target-user");
    }

    // EditUser's email-change path shares the same per-target-user
    // rate limit as SendConfirmationEmail (only email-changing saves are counted).
    [Fact]
    public async Task EditUser_EmailChange_ExceedingRateLimit_ShouldReturn429()
    {
        // Arrange — no ClearDatabaseAsync (see isolation note above); unique target userId is
        // guaranteed by the untouched auto-increment sequence, not by wiping the database.
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

        // Assert — at least one of the 4 rapid email-changing saves was rejected with 429.
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    // CreateUser's one-shot automated welcome email is explicitly EXEMPT
    // from the resend rate limit — 4 distinct new-account creations must never 429.
    [Fact]
    public async Task CreateUser_RapidRequests_ShouldNotBeRateLimited()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
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
}
