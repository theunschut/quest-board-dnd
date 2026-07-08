using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using QuestBoard.Domain.Enums;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace QuestBoard.IntegrationTests.Helpers;

public static class AuthenticationHelper
{
    public static async Task<UserEntity> CreateTestUserAsync(
        IServiceProvider services,
        string userName = "testuser",
        string email = "test@example.com",
        string password = "Test123!",
        string name = "Test User")
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();

        // Make usernames and emails unique to avoid conflicts across tests
        var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var uniqueUserName = $"{userName}_{uniqueSuffix}";
        var uniqueEmail = email.Replace("@", $"_{uniqueSuffix}@");

        var user = new UserEntity
        {
            UserName = uniqueUserName,
            Email = uniqueEmail,
            EmailConfirmed = true,
            Name = name,
            HasKey = false
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new Exception($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactory<Program> factory,
        string userName = "testuser",
        string email = "test@example.com",
        string password = "Test123!",
        string name = "Test User",
        string[]? roles = null)
    {
        var user = await CreateTestUserAsync(factory.Services, userName, email, password, name);

        // Get user roles from database if not specified
        if (roles == null)
        {
            using var scope = factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
            var userFromDb = await userManager.FindByIdAsync(user.Id.ToString());
            if (userFromDb != null)
            {
                roles = (await userManager.GetRolesAsync(userFromDb)).ToArray();
            }
            roles ??= ["Player"]; // Default to Player role
        }

        // Create client using the same factory instance (don't create a new one)
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Encode user info with roles in the authorization header
        var userInfo = $"{user.Id}:{user.UserName}:{user.Email}:{string.Join(",", roles)}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", userInfo);

        return client;
    }

    public static async Task<(HttpClient client, UserEntity user)> CreateAuthenticatedClientWithUserAsync(
        WebApplicationFactory<Program> factory,
        string userName = "testuser",
        string email = "test@example.com",
        string password = "Test123!",
        string name = "Test User",
        string[]? roles = null)
    {
        var user = await CreateTestUserAsync(factory.Services, userName, email, password, name);

        // Add ASP.NET Core Identity roles to the user in the database if specified.
        // Only SuperAdmin is a real Identity role today — Admin/DungeonMaster/Player are
        // per-group roles stored on UserGroups.GroupRole (seeded below), so seeding them here
        // too would recreate the stale AspNetUserRoles rows the production fix removes.
        if (roles != null && roles.Contains("SuperAdmin"))
        {
            using var scope = factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var userFromDb = await userManager.FindByIdAsync(user.Id.ToString());
            if (userFromDb != null)
            {
                if (!await roleManager.RoleExistsAsync("SuperAdmin"))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>("SuperAdmin"));
                }
                await userManager.AddToRoleAsync(userFromDb, "SuperAdmin");
            }
        }

        // Seed UserGroups membership for group 1 whenever roles is left at its default (null),
        // not just when specific roles are requested, so an authenticated test client with no
        // roles argument is still an actual member of the active group the test factory's
        // MutableGroupContext defaults to. Without this, GroupSessionMiddleware's periodic
        // membership re-validation would gate out a "no roles given" caller even though the
        // test's ActiveGroupId=1 stub made it look like a valid, resolvable group session.
        // Passing an explicit empty array (roles: []) opts OUT of this seeding entirely — the
        // one existing caller that does so relies on ending up with zero group memberships.
        // Identity roles map to GroupRole enum values; default to Player when no roles were given.
        if (roles == null || roles.Length > 0)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

            GroupRole groupRole = GroupRole.Player;
            if (roles != null && roles.Contains("Admin"))
                groupRole = GroupRole.Admin;
            else if (roles != null && roles.Contains("DungeonMaster"))
                groupRole = GroupRole.DungeonMaster;

            var existingMembership = context.UserGroups
                .FirstOrDefault(ug => ug.UserId == user.Id && ug.GroupId == 1);
            if (existingMembership == null)
            {
                context.UserGroups.Add(new UserGroupEntity
                {
                    UserId = user.Id,
                    GroupId = 1,
                    GroupRole = (int)groupRole
                });
                await context.SaveChangesAsync();
            }
        }

        // Get all roles for the user from database
        string[] userRoles;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
            var userFromDb = await userManager.FindByIdAsync(user.Id.ToString());
            userRoles = userFromDb != null ? (await userManager.GetRolesAsync(userFromDb)).ToArray() : ["Player"];
        }

        // Create client using the same factory instance
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Encode user info with all roles in the authorization header
        var userInfo = $"{user.Id}:{user.UserName}:{user.Email}:{string.Join(",", userRoles)}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", userInfo);

        return (client, user);
    }

    public static async Task<(HttpClient client, UserEntity user)> CreateAuthenticatedSuperAdminClientAsync(
        WebApplicationFactory<Program> factory,
        string userName = "superadmin",
        string email = "superadmin@example.com",
        string name = "Super Admin User")
    {
        return await CreateAuthenticatedClientWithUserAsync(
            factory, userName, email, "Test1234!", name, ["SuperAdmin"]);
    }

    public static async Task<(HttpClient client, UserEntity user)> CreateAuthenticatedAdminClientAsync(
        WebApplicationFactory<Program> factory,
        string userName = "adminuser",
        string email = "admin@example.com",
        string password = "Admin123!",
        string name = "Admin User")
    {
        return await CreateAuthenticatedClientWithUserAsync(factory, userName, email, password, name, ["Admin"]);
    }

    public static async Task<(HttpClient client, UserEntity user)> CreateAuthenticatedDMClientAsync(
        WebApplicationFactory<Program> factory,
        string userName = "dmuser",
        string email = "dm@example.com",
        string password = "DMpass123!",
        string name = "DM User")
    {
        return await CreateAuthenticatedClientWithUserAsync(factory, userName, email, password, name, ["DungeonMaster"]);
    }
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers["Authorization"].ToString();

        // If there's a Test authorization header, use it
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Test "))
        {
            var userInfo = authHeader.Substring("Test ".Length);
            var parts = userInfo.Split(':');

            if (parts.Length < 4)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid auth header format"));
            }

            var userId = parts[0];
            var userName = parts[1];
            var email = parts[2];
            var rolesStr = parts[3];

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.Email, email)
            };

            // Add role claims
            if (!string.IsNullOrEmpty(rolesStr))
            {
                var roles = rolesStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        // No Test header - check if we have Identity cookies
        // We need to manually read the cookie and validate it
        if (Context.Request.Cookies.ContainsKey(".AspNetCore.Identity.Application"))
        {
            // There's an Identity cookie - let the middleware handle authentication naturally
            // by skipping (returning NoResult) and letting the request pipeline continue
            // This will allow the Identity.Application scheme to be tried
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        return Task.FromResult(AuthenticateResult.NoResult());
    }
}
