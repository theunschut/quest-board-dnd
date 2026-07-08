using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

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

    // The 4th resend to the SAME target user within the 1-hour window
    // must be rejected with 429. Program.cs's emailResendLimiter is a singleton
    // PartitionedRateLimiter<int> keyed "email-resend:{userId}", PermitLimit=3/hour — this test
    // uses a fresh target userId, and this class's dedicated fixture guarantees no sibling
    // test's exhausted budget bleeds in.
    [Fact]
    public async Task SendConfirmationEmail_ExceedingRateLimit_ShouldReturn429()
    {
        // Arrange
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
        // Arrange
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

        // Assert — at least one of the 4 rapid email-changing saves was rejected with 429.
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests);
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
}
