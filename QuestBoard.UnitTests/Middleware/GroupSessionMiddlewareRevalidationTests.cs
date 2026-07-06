using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Constants;
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
        string method = "GET")
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
