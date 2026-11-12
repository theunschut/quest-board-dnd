using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Constants;
using QuestBoard.Service.Extensions;
using QuestBoard.Service.Middleware;

namespace QuestBoard.UnitTests.Middleware;

/// <summary>
/// Unit tests for GroupSessionMiddleware's interval-gated membership re-validation block.
/// Tested directly against InvokeAsync (not via the integration WebApplicationFactory) because
/// the integration TestAuthHandler does not round-trip session cookies and the factory's
/// MutableGroupContext test double bypasses real elapsed-time behavior — neither can reliably
/// simulate wall-clock staleness on ActiveGroupValidatedAtUtc.
/// </summary>
public class GroupSessionMiddlewareRevalidationTests
{
    private const int TestUserId = 42;
    private const int TestGroupId = 7;

    private static DefaultHttpContext CreateContext(
        DateTime? validatedAtUtc,
        bool isSuperAdmin,
        IUserService userService,
        int? activeGroupId = TestGroupId,
        string method = "GET",
        IGroupService? groupService = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, TestUserId.ToString()) };
        if (isSuperAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "SuperAdmin"));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var services = new ServiceCollection();
        services.AddSingleton(userService);
        if (groupService == null)
        {
            groupService = Substitute.For<IGroupService>();
            groupService.GetByIdAsync(TestGroupId, Arg.Any<CancellationToken>())
                .Returns(new Group { Id = TestGroupId, Name = "Board" });
        }
        services.AddSingleton(groupService);
        services.AddSingleton<IActiveGroupContext>(new FakeActiveGroupContext(activeGroupId));
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            User = principal
        };
        context.Request.Method = method;
        context.Request.Path = "/quests";
        context.Session = new FakeSession(validatedAtUtc);

        return context;
    }

    private static (RequestDelegate next, Func<bool> wasCalled) CreateNext()
    {
        var called = false;
        RequestDelegate next = _ =>
        {
            called = true;
            return Task.CompletedTask;
        };
        return (next, () => called);
    }

    [Fact]
    public async Task RemovedMember_StaleTimestamp_Get_RedirectsToGroupPickAndClearsSession()
    {
        var userService = Substitute.For<IUserService>();
        userService.GetGroupRoleByIdAsync(TestUserId, TestGroupId).Returns((GroupRole?)null);

        var staleTimestamp = DateTime.UtcNow.AddMinutes(-10);
        var context = CreateContext(staleTimestamp, isSuperAdmin: false, userService, method: "GET");
        var (next, wasCalled) = CreateNext();

        var middleware = new GroupSessionMiddleware(next);
        await middleware.InvokeAsync(context);

        var location = context.Response.Headers.Location.ToString();
        location.Should().Contain("/groups/pick");
        ((FakeSession)context.Session).TryGetValue(SessionKeys.ActiveGroupId, out _).Should().BeFalse();
        ((FakeSession)context.Session).TryGetValue(SessionKeys.ActiveGroupName, out _).Should().BeFalse();
        ((FakeSession)context.Session).TryGetValue(SessionKeys.ActiveGroupValidatedAtUtc, out _).Should().BeFalse();
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public async Task RemovedMember_StaleTimestamp_Post_ReturnsConflict()
    {
        var userService = Substitute.For<IUserService>();
        userService.GetGroupRoleByIdAsync(TestUserId, TestGroupId).Returns((GroupRole?)null);

        var staleTimestamp = DateTime.UtcNow.AddMinutes(-10);
        var context = CreateContext(staleTimestamp, isSuperAdmin: false, userService, method: "POST");
        var (next, wasCalled) = CreateNext();

        var middleware = new GroupSessionMiddleware(next);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public async Task StillMember_StaleTimestamp_InvokesNextAndRestampsValidatedAt()
    {
        var userService = Substitute.For<IUserService>();
        userService.GetGroupRoleByIdAsync(TestUserId, TestGroupId).Returns(GroupRole.Player);

        var staleTimestamp = DateTime.UtcNow.AddMinutes(-10);
        var context = CreateContext(staleTimestamp, isSuperAdmin: false, userService, method: "GET");
        var (next, wasCalled) = CreateNext();

        var middleware = new GroupSessionMiddleware(next);
        await middleware.InvokeAsync(context);

        wasCalled().Should().BeTrue();
        var fakeSession = (FakeSession)context.Session;
        fakeSession.TryGetValue(SessionKeys.ActiveGroupValidatedAtUtc, out var raw).Should().BeTrue();
        var restamped = DateTime.Parse(Encoding.UTF8.GetString(raw!), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        restamped.Should().BeAfter(staleTimestamp);
    }

    [Fact]
    public async Task FreshTimestamp_WithinInterval_InvokesNextWithoutMembershipCheck()
    {
        var userService = Substitute.For<IUserService>();

        var freshTimestamp = DateTime.UtcNow.AddMinutes(-1);
        var context = CreateContext(freshTimestamp, isSuperAdmin: false, userService, method: "GET");
        var (next, wasCalled) = CreateNext();

        var middleware = new GroupSessionMiddleware(next);
        await middleware.InvokeAsync(context);

        wasCalled().Should().BeTrue();
        await userService.DidNotReceive().GetGroupRoleByIdAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task SuperAdmin_StaleTimestamp_InvokesNextWithoutMembershipCheck()
    {
        var userService = Substitute.For<IUserService>();

        var staleTimestamp = DateTime.UtcNow.AddMinutes(-10);
        var context = CreateContext(staleTimestamp, isSuperAdmin: true, userService, method: "GET");
        var (next, wasCalled) = CreateNext();

        var middleware = new GroupSessionMiddleware(next);
        await middleware.InvokeAsync(context);

        wasCalled().Should().BeTrue();
        await userService.DidNotReceive().GetGroupRoleByIdAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task SuperAdmin_StaleTimestamp_DeletedBoard_Get_RedirectsToGroupPickAndClearsSession()
    {
        var userService = Substitute.For<IUserService>();
        var groupService = Substitute.For<IGroupService>();
        groupService.GetByIdAsync(TestGroupId, Arg.Any<CancellationToken>()).Returns((Group?)null);

        var context = CreateContext(DateTime.UtcNow.AddMinutes(-10), isSuperAdmin: true, userService,
            method: "GET", groupService: groupService);
        context.Request.QueryString = new QueryString("?page=2");
        var (next, wasCalled) = CreateNext();

        await new GroupSessionMiddleware(next).InvokeAsync(context);

        var location = context.Response.Headers.Location.ToString();
        location.Should().StartWith("/groups/pick?returnUrl=");
        location.Should().Contain(Uri.EscapeDataString("/quests?page=2"));
        AssertActiveBoardCleared(context);
        wasCalled().Should().BeFalse();
        await userService.DidNotReceive().GetGroupRoleByIdAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task SuperAdmin_StaleTimestamp_DeletedBoard_Post_Returns409AndClearsSession()
    {
        var userService = Substitute.For<IUserService>();
        var groupService = Substitute.For<IGroupService>();
        groupService.GetByIdAsync(TestGroupId, Arg.Any<CancellationToken>()).Returns((Group?)null);

        var context = CreateContext(DateTime.UtcNow.AddMinutes(-10), isSuperAdmin: true, userService,
            method: "POST", groupService: groupService);
        var (next, wasCalled) = CreateNext();

        await new GroupSessionMiddleware(next).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        AssertActiveBoardCleared(context);
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public async Task SuperAdmin_StaleTimestamp_ExistingBoard_InvokesNextAndRestamps()
    {
        var userService = Substitute.For<IUserService>();

        var staleTimestamp = DateTime.UtcNow.AddMinutes(-10);
        var context = CreateContext(staleTimestamp, isSuperAdmin: true, userService, method: "GET");
        var (next, wasCalled) = CreateNext();

        await new GroupSessionMiddleware(next).InvokeAsync(context);

        wasCalled().Should().BeTrue();
        await userService.DidNotReceive().GetGroupRoleByIdAsync(Arg.Any<int>(), Arg.Any<int>());
        ((FakeSession)context.Session).TryGetValue(SessionKeys.ActiveGroupValidatedAtUtc, out var raw).Should().BeTrue();
        var restamped = DateTime.Parse(Encoding.UTF8.GetString(raw!), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        restamped.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task SuperAdmin_FreshTimestamp_DoesNotLookUpTheBoard()
    {
        var userService = Substitute.For<IUserService>();
        var groupService = Substitute.For<IGroupService>();

        var context = CreateContext(DateTime.UtcNow.AddMinutes(-1), isSuperAdmin: true, userService,
            method: "GET", groupService: groupService);
        var (next, wasCalled) = CreateNext();

        await new GroupSessionMiddleware(next).InvokeAsync(context);

        wasCalled().Should().BeTrue();
        await groupService.DidNotReceive().GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task SuperAdmin_FreshTimestamp_DeletedBoard_Write_Returns409AndClearsSession(string method)
    {
        // A board deleted a moment ago must stop a write at once rather than after the interval, or the
        // write would stamp a board that no longer exists and fail on the foreign key.
        var userService = Substitute.For<IUserService>();
        var groupService = Substitute.For<IGroupService>();
        groupService.GetByIdAsync(TestGroupId, Arg.Any<CancellationToken>()).Returns((Group?)null);

        var context = CreateContext(DateTime.UtcNow.AddMinutes(-1), isSuperAdmin: true, userService,
            method: method, groupService: groupService);
        var (next, wasCalled) = CreateNext();

        await new GroupSessionMiddleware(next).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        AssertActiveBoardCleared(context);
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public async Task RemovedMember_FreshTimestamp_Post_Returns409AndClearsSession()
    {
        var userService = Substitute.For<IUserService>();
        userService.GetGroupRoleByIdAsync(TestUserId, TestGroupId).Returns((GroupRole?)null);

        var context = CreateContext(DateTime.UtcNow.AddMinutes(-1), isSuperAdmin: false, userService, method: "POST");
        var (next, wasCalled) = CreateNext();

        await new GroupSessionMiddleware(next).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        AssertActiveBoardCleared(context);
        wasCalled().Should().BeFalse();
    }

    [Fact]
    public async Task StillMember_FreshTimestamp_Post_ChecksMembershipAndInvokesNext()
    {
        var userService = Substitute.For<IUserService>();
        userService.GetGroupRoleByIdAsync(TestUserId, TestGroupId).Returns(GroupRole.Player);

        var context = CreateContext(DateTime.UtcNow.AddMinutes(-1), isSuperAdmin: false, userService, method: "POST");
        var (next, wasCalled) = CreateNext();

        await new GroupSessionMiddleware(next).InvokeAsync(context);

        wasCalled().Should().BeTrue();
        await userService.Received(1).GetGroupRoleByIdAsync(TestUserId, TestGroupId);
    }

    [Fact]
    public async Task SuperAdmin_FreshTimestamp_Head_DoesNotLookUpTheBoard()
    {
        var userService = Substitute.For<IUserService>();
        var groupService = Substitute.For<IGroupService>();

        var context = CreateContext(DateTime.UtcNow.AddMinutes(-1), isSuperAdmin: true, userService,
            method: "HEAD", groupService: groupService);
        var (next, wasCalled) = CreateNext();

        await new GroupSessionMiddleware(next).InvokeAsync(context);

        wasCalled().Should().BeTrue();
        await groupService.DidNotReceive().GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearActiveBoardIfAsync_WhenTheSessionNamesTheBoard_ClearsAllThreeKeys()
    {
        var session = new FakeSession(DateTime.UtcNow);
        session.SetInt32(SessionKeys.ActiveGroupId, TestGroupId);
        session.SetString(SessionKeys.ActiveGroupName, "Board");

        var cleared = await session.ClearActiveBoardIfAsync(TestGroupId, TestContext.Current.CancellationToken);

        cleared.Should().BeTrue();
        session.TryGetValue(SessionKeys.ActiveGroupId, out _).Should().BeFalse();
        session.TryGetValue(SessionKeys.ActiveGroupName, out _).Should().BeFalse();
        session.TryGetValue(SessionKeys.ActiveGroupValidatedAtUtc, out _).Should().BeFalse();
    }

    [Fact]
    public async Task ClearActiveBoardIfAsync_WhenTheSessionNamesAnotherBoard_LeavesItAlone()
    {
        var session = new FakeSession(DateTime.UtcNow);
        session.SetInt32(SessionKeys.ActiveGroupId, TestGroupId + 1);
        session.SetString(SessionKeys.ActiveGroupName, "Other");

        var cleared = await session.ClearActiveBoardIfAsync(TestGroupId, TestContext.Current.CancellationToken);

        cleared.Should().BeFalse();
        session.GetInt32(SessionKeys.ActiveGroupId).Should().Be(TestGroupId + 1);
        session.GetString(SessionKeys.ActiveGroupName).Should().Be("Other");
        session.TryGetValue(SessionKeys.ActiveGroupValidatedAtUtc, out _).Should().BeTrue();
    }

    private static void AssertActiveBoardCleared(HttpContext context)
    {
        var session = (FakeSession)context.Session;
        session.TryGetValue(SessionKeys.ActiveGroupId, out _).Should().BeFalse();
        session.TryGetValue(SessionKeys.ActiveGroupName, out _).Should().BeFalse();
        session.TryGetValue(SessionKeys.ActiveGroupValidatedAtUtc, out _).Should().BeFalse();
    }

    private sealed class FakeActiveGroupContext(int? activeGroupId) : IActiveGroupContext
    {
        public int? ActiveGroupId { get; } = activeGroupId;
    }

    /// <summary>
    /// Minimal in-memory ISession test double backing a dictionary, so the middleware's
    /// GetString/SetString/Remove calls can be exercised directly without a real distributed
    /// cache or cookie round-trip.
    /// </summary>
    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public FakeSession(DateTime? validatedAtUtc)
        {
            if (validatedAtUtc != null)
            {
                _store[SessionKeys.ActiveGroupValidatedAtUtc] =
                    Encoding.UTF8.GetBytes(validatedAtUtc.Value.ToString("O"));
            }
        }

        public bool IsAvailable => true;
        public string Id => "test-session";
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
