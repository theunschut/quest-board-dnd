using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using QuestBoard.Service.Authorization;
using System.Security.Claims;

namespace QuestBoard.UnitTests.Authorization;

/// <summary>
/// The raw Hangfire dashboard middleware and UseHangfireDashboard registration are both
/// skipped entirely when the ASP.NET Core environment is "Testing" (see Program.cs), so
/// /hangfire is unreachable via HTTP in the integration test factory. This asserts
/// AdminDashboardAuthFilter.Authorize directly instead: a group Admin (non-SuperAdmin) must
/// be denied, and a SuperAdmin must be allowed.
/// </summary>
public class AdminDashboardAuthFilterTests
{
    private static DashboardContext CreateContext(bool authenticated, params string[] roles)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };

        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
        // Passing null as the authenticationType yields IsAuthenticated == false, matching an
        // anonymous request; a non-null type (e.g. "Test") yields IsAuthenticated == true.
        var identity = new ClaimsIdentity(claims, authenticated ? "Test" : null);
        httpContext.User = new ClaimsPrincipal(identity);

        var storage = Substitute.For<JobStorage>();
        return new AspNetCoreDashboardContext(storage, new DashboardOptions(), httpContext);
    }

    [Fact]
    public void Authorize_WhenSuperAdmin_ReturnsTrue()
    {
        var context = CreateContext(authenticated: true, "SuperAdmin");

        var result = new AdminDashboardAuthFilter().Authorize(context);

        result.Should().BeTrue();
    }

    [Fact]
    public void Authorize_WhenGroupAdmin_ReturnsFalse()
    {
        // A group-scoped Admin role claim (not SuperAdmin) must be denied — Hangfire
        // dashboard access is SuperAdmin-only, per Phase 29's intent (preserved by 34.3-05).
        var context = CreateContext(authenticated: true, "Admin");

        var result = new AdminDashboardAuthFilter().Authorize(context);

        result.Should().BeFalse();
    }

    [Fact]
    public void Authorize_WhenUnauthenticated_ReturnsFalse()
    {
        var context = CreateContext(authenticated: false);

        var result = new AdminDashboardAuthFilter().Authorize(context);

        result.Should().BeFalse();
    }
}
