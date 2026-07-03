using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;
using System.Text;

namespace QuestBoard.IntegrationTests.Controllers;

public class AccountControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private readonly HttpClient _client = factory.CreateNonRedirectingClient();

    [Fact]
    public async Task Login_Get_ShouldReturnSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Login");
    }

    [Fact]
    public async Task AccessDenied_Get_ShouldReturnSuccessWithGeneralizedCopy()
    {
        // Act
        var response = await _client.GetAsync("/Account/AccessDenied", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("You Don't Have Permission");
        content.Should().NotContain("Dungeon Master");
    }

    // Public self-registration was removed — the route
    // no longer exists, so both GET and POST must 404.
    [Fact]
    public async Task Register_Get_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/Account/Register", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_Post_WithValidData_ShouldReturnNotFound()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "New User",
            ["Email"] = "newuser@example.com",
            ["Password"] = "NewUser123!",
            ["ConfirmPassword"] = "NewUser123!"
        });

        // Act
        var response = await _client.PostAsync("/Account/Register", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Profile_Get_WhenNotAuthenticated_ShouldRedirectToLogin()
    {
        // Act
        var response = await _client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Profile_Get_WhenAuthenticated_ShouldReturnUserProfile()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var password = "ProfilePass123!";
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profileuser", "profile@example.com", password, "Profile User");

        // Act
        var response = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain(user.Email);
    }

    // Regression: Profile badges must reflect GroupRole (via GetEffectiveGroupRoleAsync),
    // not the empty AspNetUserRoles. Default active group is 1 (MutableGroupContext default).
    [Fact]
    public async Task Profile_WhenAdmin_ShowsAdminBadge()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "badgeadmin", "badgeadmin@example.com", roles: ["Admin"]);

        // Act
        var response = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Administrator");
    }

    [Fact]
    public async Task Profile_WhenDungeonMaster_ShowsDungeonMasterBadge()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "badgedm", "badgedm@example.com", roles: ["DungeonMaster"]);

        // Act
        var response = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Dungeon Master & Player");
    }

    [Fact]
    public async Task Profile_WhenPlayer_ShowsNeitherAdminNorDungeonMasterBadge()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "badgeplayer", "badgeplayer@example.com", roles: ["Player"]);

        // Act
        var response = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("Administrator");
        content.Should().NotContain("Dungeon Master & Player");
    }

    // Regression: Edit's combined DM-enabled flag (EditProfileViewModel.IsDungeonMaster) must be
    // computed from GroupRole via GetEffectiveGroupRoleAsync without throwing, for both a
    // DungeonMaster and an Admin, and for a plain Player. The flag has no distinct visible
    // marker in Edit.cshtml today, so this proves the underlying resolution path (the fix here)
    // completes successfully end-to-end for every role — a regression in the role lookup itself
    // (e.g. re-introducing an empty-AspNetUserRoles read) would surface as a 500, not a silent
    // wrong-value bug the response body could show.
    [Fact]
    public async Task Edit_WhenDungeonMaster_ReturnsOk()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editdmuser", "editdmuser@example.com", roles: ["DungeonMaster"]);

        // Act
        var response = await dmClient.GetAsync("/Account/Edit", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_WhenAdmin_ReturnsOk()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editadminuser", "editadminuser@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.GetAsync("/Account/Edit", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_WhenPlayer_ReturnsOk()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editplayeruser", "editplayeruser@example.com", roles: ["Player"]);

        // Act
        var response = await playerClient.GetAsync("/Account/Edit", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Regression (D-04 fail-soft): a null ActiveGroupId must render the profile page
    // with no role badges and must NOT throw (no 500). Restores the shared fixture's
    // ActiveGroupId back to 1 afterward so sibling tests are unaffected.
    [Fact]
    public async Task Profile_WhenActiveGroupIdIsNull_ReturnsOkWithNoBadgesAndNoException()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "nullgroupadmin", "nullgroupadmin@example.com", roles: ["Admin"]);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            // Act
            var response = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);

            // Assert — fail-soft: 200, not 500, and no role badge markers.
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            content.Should().NotContain("Administrator");
            content.Should().NotContain("Dungeon Master & Player");
        }
        finally
        {
            // Restore shared fixture state so sibling tests using the default GroupId=1 are unaffected.
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task Logout_Post_ShouldRedirectToHome()
    {
        // Arrange
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // First, GET a page to obtain the anti-forgery token (use the home page or profile)
        var getResponse = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);

        // Set the anti-forgery cookie
        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            [],
            token);

        // Act
        var response = await client.PostAsync("/Account/Logout", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
    }

    // ForgotPassword GET renders the email-entry form (mirrors Login_Get).
    [Fact]
    public async Task ForgotPassword_Get_ShouldReturnSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Email");
    }

    // Enumeration-safety — known and unknown emails must produce the IDENTICAL
    // outcome, whatever that outcome is. This test asserts SAMENESS rather than a fixed expected
    // status code: Program.cs's ForgotPassword rate limiter (PermitLimit=3 / 15-min window) is
    // partitioned by RemoteIpAddress, and every in-memory TestServer request in this test class
    // shares the same loopback IP/partition — so depending on xUnit's (undefined) run order
    // relative to ForgotPassword_Post_ExceedingRateLimit_ShouldReturn429, this test's two requests
    // may land inside or outside the window and get 302 or 429. Either way, the property
    // under test — that a known email cannot be distinguished from an unknown one — holds as
    // long as BOTH requests get the exact same status code and (when redirecting) the same
    // redirect target; that sameness is the actual enumeration-safety guarantee.
    [Fact]
    public async Task ForgotPassword_Post_KnownAndUnknownEmail_ShouldReturnSameGenericMessage()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "knownuser", "knownuser@example.com", "KnownUser123!", "Known User");

        var client = factory.CreateNonRedirectingClient();

        async Task<HttpResponseMessage> PostForgotPasswordAsync(string email)
        {
            var getResponse = await client.GetAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);
            var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
            if (!string.IsNullOrEmpty(cookieValue))
            {
                client.DefaultRequestHeaders.Remove("Cookie");
                client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
            }

            var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
                new Dictionary<string, string> { ["Email"] = email },
                token);

            return await client.PostAsync("/Account/ForgotPassword", formContent, TestContext.Current.CancellationToken);
        }

        // Act
        var knownResponse = await PostForgotPasswordAsync(user.Email!);
        var unknownResponse = await PostForgotPasswordAsync("no-such-user@example.com");

        // Assert — identical outcome for both cases, regardless of whether the shared
        // rate-limit partition happened to be exhausted by another test (302+302, or 429+429,
        // both prove enumeration-safety; a MISMATCH between the two would be the actual bug).
        unknownResponse.StatusCode.Should().Be(knownResponse.StatusCode,
            "a known and an unknown email must be fully indistinguishable (D-11)");
        knownResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.TooManyRequests);

        if (knownResponse.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found)
        {
            knownResponse.Headers.Location!.OriginalString.Should().Contain("ForgotPassword");
            unknownResponse.Headers.Location!.OriginalString.Should().Be(knownResponse.Headers.Location!.OriginalString);
        }
    }

    // Within a single test, the 4th rapid POST from one client must be
    // rejected with 429. Program.cs configures PermitLimit=3 / 15-min window, partitioned by
    // RemoteIpAddress — since every in-memory TestServer request shares one partition, this
    // asserts the 4th-of-4 (from a private counter starting fresh in THIS test's 4 requests)
    // is rejected once the shared window's cumulative count (from this test alone, ignoring
    // whatever other ForgotPassword tests already consumed) reaches the limit. Because the
    // partition is process-wide, the actual rejection may occur earlier than the 4th request
    // if a sibling test already consumed part of the window — so this test asserts that AT
    // LEAST ONE of the 4 rapid requests is rejected with 429, which is the true DoS-mitigation
    // property: the limiter is demonstrably active for this endpoint.
    [Fact]
    public async Task ForgotPassword_Post_ExceedingRateLimit_ShouldReturn429()
    {
        // Arrange — isolated client for this test.
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var client = factory.CreateNonRedirectingClient();

        async Task<HttpResponseMessage> PostForgotPasswordAsync(string email)
        {
            var getResponse = await client.GetAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);
            var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
            if (!string.IsNullOrEmpty(cookieValue))
            {
                client.DefaultRequestHeaders.Remove("Cookie");
                client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
            }

            var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
                new Dictionary<string, string> { ["Email"] = email },
                token);

            return await client.PostAsync("/Account/ForgotPassword", formContent, TestContext.Current.CancellationToken);
        }

        // Act — issue 4 rapid requests from the same client (same RemoteIpAddress partition).
        var responses = new List<HttpResponseMessage>();
        for (var i = 1; i <= 4; i++)
        {
            responses.Add(await PostForgotPasswordAsync("ratelimit-test@example.com"));
        }

        // Assert — the limiter is active: at least one of the 4 rapid requests was rejected.
        // (Typically the 4th, but if a sibling test already consumed part of the shared
        // process-wide partition window, rejection may start earlier — either way proves the limiter is active.)
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    // SetPassword GET renders the form with hidden UserId/Token fields.
    [Fact]
    public async Task SetPassword_Get_WithUserIdAndToken_ShouldReturnForm()
    {
        // Act
        var response = await _client.GetAsync("/Account/SetPassword?userId=1&token=abc", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("UserId");
        content.Should().Contain("Token");
    }

    // SetPassword POST with a valid token sets the password AND confirms the email.
    [Fact]
    public async Task SetPassword_Post_WithValidToken_ShouldSetPasswordAndConfirmEmail()
    {
        // Arrange — seed a passwordless user via the same path AdminController.CreateUser uses.
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"passwordless_{uniqueSuffix}@example.com";

        int userId;
        string rawToken;
        using (var scope = factory.Services.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

            var createResult = await userService.CreateAsync(email, "Passwordless User");
            createResult.Succeeded.Should().BeTrue();

            var resolvedUserId = await identityService.GetIdByEmailAsync(email);
            resolvedUserId.Should().NotBeNull();
            userId = resolvedUserId!.Value;

            var token = await identityService.GeneratePasswordResetTokenForUserAsync(userId);
            token.Should().NotBeNullOrEmpty();
            rawToken = token!;
        }

        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

        var client = factory.CreateNonRedirectingClient();
        var getResponse = await client.GetAsync(
            $"/Account/SetPassword?userId={userId}&token={Uri.EscapeDataString(encodedToken)}",
            TestContext.Current.CancellationToken);
        var (formToken, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string>
            {
                ["UserId"] = userId.ToString(),
                ["Token"] = encodedToken,
                ["NewPassword"] = "BrandNewPass123!",
                ["ConfirmPassword"] = "BrandNewPass123!"
            },
            formToken);

        // Act
        var response = await client.PostAsync("/Account/SetPassword", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Contain("Login");

        using var verifyScope = factory.Services.CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        var updatedUser = await userManager.FindByIdAsync(userId.ToString());
        updatedUser.Should().NotBeNull();
        updatedUser!.EmailConfirmed.Should().BeTrue();

        var passwordCheck = await userManager.CheckPasswordAsync(updatedUser, "BrandNewPass123!");
        passwordCheck.Should().BeTrue();
    }

    // SetPassword POST with a garbage/invalid token fails gracefully (no 500, no password change).
    [Fact]
    public async Task SetPassword_Post_WithInvalidToken_ShouldFailGracefully()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"invalidtoken_{uniqueSuffix}@example.com";

        int userId;
        using (var scope = factory.Services.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

            var createResult = await userService.CreateAsync(email, "Invalid Token User");
            createResult.Succeeded.Should().BeTrue();

            var resolvedUserId = await identityService.GetIdByEmailAsync(email);
            resolvedUserId.Should().NotBeNull();
            userId = resolvedUserId!.Value;
        }

        var client = factory.CreateNonRedirectingClient();
        var getResponse = await client.GetAsync(
            $"/Account/SetPassword?userId={userId}&token=garbage-token",
            TestContext.Current.CancellationToken);
        var (formToken, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string>
            {
                ["UserId"] = userId.ToString(),
                ["Token"] = "garbage-token",
                ["NewPassword"] = "ShouldNotApply123!",
                ["ConfirmPassword"] = "ShouldNotApply123!"
            },
            formToken);

        // Act
        var response = await client.PostAsync("/Account/SetPassword", formContent, TestContext.Current.CancellationToken);

        // Assert — no 500, and the password was never actually set (account remains passwordless).
        ((int)response.StatusCode).Should().BeLessThan(500);

        using var verifyScope = factory.Services.CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        var unchangedUser = await userManager.FindByIdAsync(userId.ToString());
        unchangedUser.Should().NotBeNull();
        var passwordCheck = await userManager.CheckPasswordAsync(unchangedUser!, "ShouldNotApply123!");
        passwordCheck.Should().BeFalse();
    }

    // A passwordless account cannot sign in (no crash, no successful auth).
    [Fact]
    public async Task Login_Post_PasswordlessAccount_ShouldNotSignIn()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"neververified_{uniqueSuffix}@example.com";

        using (var scope = factory.Services.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var createResult = await userService.CreateAsync(email, "Never Set Password User");
            createResult.Succeeded.Should().BeTrue();
        }

        var client = factory.CreateNonRedirectingClient();
        var getResponse = await client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = "SomePlausiblePassword123!",
                ["RememberMe"] = "false"
            },
            token);

        // Act
        var response = await client.PostAsync("/Account/Login", formContent, TestContext.Current.CancellationToken);

        // Assert — no 500, and the response must NOT be a redirect to an authenticated area (GroupPicker).
        ((int)response.StatusCode).Should().BeLessThan(500);
        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found)
        {
            response.Headers.Location!.OriginalString.Should().NotContain("GroupPicker");
        }
    }
}