# Phase 38: Group-Scoped User List - Pattern Map

**Mapped:** 2026-07-03
**Files analyzed:** 5 (1 repository, 1 domain interface pair, 1 service, 1 controller, 1 integration test file)
**Analogs found:** 5 / 5

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Repository/UserRepository.cs` (new method, e.g. `GetAllGroupMembers`) | repository | CRUD (read, manual join) | `GetAllDungeonMasters`/`GetAllPlayers` in same file | exact |
| `QuestBoard.Domain/Interfaces/IUserRepository.cs` (new method signature) | model/interface | CRUD | `GetAllDungeonMasters`/`GetAllPlayers` signatures, same file | exact |
| `QuestBoard.Domain/Interfaces/IUserService.cs` (new method signature) | model/interface | CRUD | `GetAllDungeonMastersAsync`/`GetAllPlayersAsync`, same file | exact |
| `QuestBoard.Domain/Services/UserService.cs` (new pass-through method) | service | CRUD | `GetAllDungeonMastersAsync`/`GetAllPlayersAsync`, same file | exact |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `Users()` (modified) | controller | request-response (read) | same file's existing `groupId == null` guard + loop | exact (self-modify) |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `PromoteToAdmin`/`DemoteFromAdmin`/`PromoteToDM`/`DemoteToPlayer` (modified) | controller | request-response (write/CRUD) | same file's existing `groupId == null` guard pattern | exact (self-modify) |
| New helper for D-05 membership check (private controller method or `IUserRepository.IsMemberOfGroupAsync`) | repository or utility | CRUD (read, existence check) | `UserRepository.GetGroupRoleAsync` (same file) — returns null when no `UserGroups` row exists | role-match |
| `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` (new regression test, D-03) | test | request-response (integration/HTTP) | `CreateUser_Post_WhenAdmin_CreatesUserInActiveGroup` and `ManageUsers_WhenNotAuthenticated_ShouldRedirectToLogin` in same file | exact |

## Pattern Assignments

### `QuestBoard.Repository/UserRepository.cs` — new group-scoped read method (repository, CRUD)

**Analog:** `GetAllDungeonMasters` / `GetAllPlayers`, same file, lines 19-48

**Full manual-join pattern to mirror** (lines 19-48):
```csharp
/// <inheritdoc/>
public async Task<IList<User>> GetAllDungeonMasters(CancellationToken token = default)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return [];

    var entities = await DbSet
        .Where(u => DbContext.UserGroups
            .Any(ug => ug.UserId == u.Id
                    && ug.GroupId == groupId.Value
                    && (ug.GroupRole == (int)GroupRole.DungeonMaster
                        || ug.GroupRole == (int)GroupRole.Admin)))
        .ToListAsync(cancellationToken: token);
    return Mapper.Map<IList<User>>(entities);
}

/// <inheritdoc/>
public async Task<IList<User>> GetAllPlayers(CancellationToken token = default)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return [];

    var entities = await DbSet
        .Where(u => DbContext.UserGroups
            .Any(ug => ug.UserId == u.Id
                    && ug.GroupId == groupId.Value
                    && ug.GroupRole == (int)GroupRole.Player))
        .ToListAsync(cancellationToken: token);
    return Mapper.Map<IList<User>>(entities);
}
```

For a new `GetAllGroupMembers`-style method (per Claude's Discretion in CONTEXT.md), drop the `GroupRole` filter entirely — membership is any `UserGroups` row for the group:
```csharp
var entities = await DbSet
    .Where(u => DbContext.UserGroups
        .Any(ug => ug.UserId == u.Id && ug.GroupId == groupId.Value))
    .ToListAsync(cancellationToken: token);
return Mapper.Map<IList<User>>(entities);
```
Note: `activeGroupContext` is already injected into `UserRepository` via primary constructor (line 10) — no new DI wiring needed if scoping by the *active* group. If instead the method takes an explicit `groupId` parameter (recommended so it can be reused/tested without relying on ambient `IActiveGroupContext`, and so `AdminController` can pass its own resolved `groupId`), follow the signature style of `GetGroupRoleAsync(int userId, int groupId)` (line 51) instead — no `activeGroupContext` read inside the method, caller passes `groupId` directly.

**Class declaration / DI pattern** (lines 1-11):
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class UserRepository(QuestBoardContext dbContext, IMapper mapper, IActiveGroupContext activeGroupContext)
    : BaseRepository<User, UserEntity>(dbContext, mapper), IUserRepository
```

**Existence-check pattern for D-05** — `GetGroupRoleAsync` (lines 50-57) already does exactly the null-check shape needed:
```csharp
/// <inheritdoc/>
public async Task<GroupRole?> GetGroupRoleAsync(int userId, int groupId)
{
    var ug = await DbContext.UserGroups
        .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
    if (ug == null) return null;
    return (GroupRole)ug.GroupRole;
}
```
This can be reused as-is for the D-05 membership check (`GetGroupRoleAsync(userId, groupId.Value) == null` means "not a member") — no new repository method is strictly required for D-05; it's a Claude's Discretion choice between reusing this existing method vs. adding a dedicated `IsMemberOfGroupAsync`.

**Write pattern already in file** (lines 59-75, `SetGroupRoleAsync`) — this is the method D-04/D-05 gates:
```csharp
/// <inheritdoc/>
public async Task<int?> SetGroupRoleAsync(int userId, int groupId, GroupRole role)
{
    var ug = await DbContext.UserGroups
        .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
    if (ug == null)
    {
        ug = new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)role };
        DbContext.UserGroups.Add(ug);
    }
    else
    {
        ug.GroupRole = (int)role;
    }
    await DbContext.SaveChangesAsync();
    return ug.Id;
}
```

---

### `QuestBoard.Domain/Interfaces/IUserRepository.cs` + `IUserService.cs` — new method signatures (interface)

**Analog:** existing paired declarations, `IUserRepository.cs` lines 13-23 and matching `IUserService.cs` `GetAllDungeonMastersAsync`/`GetAllPlayersAsync`

```csharp
/// <summary>
/// Returns all users holding the DungeonMaster or Admin group role in the active group.
/// Returns an empty list when there is no active group.
/// </summary>
Task<IList<User>> GetAllDungeonMasters(CancellationToken token = default);

/// <summary>
/// Returns all users holding the Player group role in the active group.
/// Returns an empty list when there is no active group.
/// </summary>
Task<IList<User>> GetAllPlayers(CancellationToken token = default);
```
Mirror this XML-doc + signature style exactly for the new group-scoped members method. Layering convention: repository method declared in `IUserRepository`, thin pass-through declared in `IUserService` and implemented in `UserService` (see below) — `AdminController` must call the service, never the repository, per the existing `Domain → Repository` layering noted in CONTEXT.md.

---

### `QuestBoard.Domain/Services/UserService.cs` — pass-through method (service, CRUD)

**Analog:** `GetAllDungeonMastersAsync` / `GetAllPlayersAsync`, same file, lines 42-52

```csharp
/// <inheritdoc/>
public async Task<IList<User>> GetAllDungeonMastersAsync(CancellationToken token = default)
{
    return await repository.GetAllDungeonMasters(token);
}

/// <inheritdoc/>
public async Task<IList<User>> GetAllPlayersAsync(CancellationToken token = default)
{
    return await repository.GetAllPlayers(token);
}
```
The new group-members service method should be a one-line pass-through in the same style, placed alongside these two (both in the interface and here).

**Class declaration** (line 10):
```csharp
internal class UserService(IIdentityService identityService, IUserRepository repository, IMapper mapper) : BaseService<User>(repository, mapper), IUserService
```

---

### `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `Users()` (controller, request-response)

**Analog:** self (same method being modified) — `Users()`, lines 23-53

**Current implementation to replace** (lines 23-53):
```csharp
[HttpGet]
public async Task<IActionResult> Users()
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return RedirectToAction("Index", "GroupPicker");

    var allUsers = await userService.GetAllAsync();
    var userViewModels = new List<UserManagementViewModel>();

    foreach (var user in allUsers)
    {
        GroupRole? groupRole = await userService.GetGroupRoleByIdAsync(user.Id, groupId.Value);

        userViewModels.Add(new UserManagementViewModel
        {
            User = user,
            IsAdmin = groupRole == GroupRole.Admin,
            IsDungeonMaster = groupRole == GroupRole.DungeonMaster,
            IsPlayer = groupRole == GroupRole.Player,
            EmailConfirmed = user.EmailConfirmed
        });
    }

    // Sort by account type first (Admin, DM, Player), then alphabetically by name
    var sortedUsers = userViewModels
        .OrderBy(u => u.IsAdmin ? 0 : u.IsDungeonMaster ? 1 : 2)  // Admin=0, DM=1, Player=2
        .ThenBy(u => u.User.Name)
        .ToList();

    return View(sortedUsers);
}
```
D-01 replaces `var allUsers = await userService.GetAllAsync();` with the new group-scoped call (e.g. `await userService.GetAllGroupMembersAsync(groupId.Value)` or a union of `GetAllDungeonMastersAsync()`/`GetAllPlayersAsync()`). The `groupId == null` guard at the top (line 27) and the existing `RedirectToAction("Index", "GroupPicker")` redirect are unchanged — this is the exact "guard + redirect, no error message" style D-05 must copy for the four POST actions. The per-user `GetGroupRoleByIdAsync` loop (lines 32-44) and sort logic (lines 46-50) are unchanged unless Claude's Discretion opts to collapse them into a single query returning `(User, GroupRole)` pairs.

**Controller class declaration / DI** (lines 18-21):
```csharp
namespace QuestBoard.Service.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
public class AdminController(IUserService userService, IQuestService questService, IIdentityService identityService, IBackgroundJobClient jobClient, IOptions<EmailSettings> emailOptions, IMemoryCache cache, IActiveGroupContext activeGroupContext, ILogger<AdminController> logger, PartitionedRateLimiter<int> emailResendLimiter, ResendStatsClient resendStatsClient) : Controller
```

---

### `QuestBoard.Service/Controllers/Admin/AdminController.cs` — role-change POST actions (controller, request-response/CRUD write)

**Analog:** self — `PromoteToAdmin`/`DemoteFromAdmin`/`PromoteToDM`/`DemoteToPlayer`, lines 55-93

**Current implementation, all four follow this exact shape** (example, lines 55-63):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> PromoteToAdmin(int userId)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return RedirectToAction(nameof(Users));
    await userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.Admin);
    return RedirectToAction(nameof(Users));
}
```
This `if (groupId == null) return RedirectToAction(nameof(Users));` guard (identical in all four actions, lines 60/70/80/90) is the exact silent-redirect-no-error pattern D-05 cites. D-05's new membership check must be inserted the same way, right before the `SetGroupRoleAsync` call, e.g.:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> PromoteToAdmin(int userId)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return RedirectToAction(nameof(Users));
    if (!await IsMemberOfGroupAsync(userId, groupId.Value)) return RedirectToAction(nameof(Users));
    await userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.Admin);
    return RedirectToAction(nameof(Users));
}
```
Repeat identically (same guard clause line) in `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer` (lines 65-93). Both `[HttpPost]` + `[ValidateAntiForgeryToken]` attributes above each action are unchanged.

---

### `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` — D-03 regression test (test, request-response/integration)

**Analog:** `CreateUser_Post_WhenAdmin_CreatesUserInActiveGroup` (lines 194-230) for the arrange/act/assert-against-DB shape, and `ManageUsers_WhenNotAuthenticated_ShouldRedirectToLogin` (lines 45-53) for the minimal GET-and-assert shape.

**File-level imports** (lines 1-7):
```csharp
using Microsoft.AspNetCore.Identity;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;
```

**Class/fixture pattern** (lines 9-19):
```csharp
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
```

**Full test example to mirror for D-03** (`CreateUser_Post_WhenAdmin_CreatesUserInActiveGroup`, lines 194-230) — shows the `ClearDatabaseAsync` + authenticated-admin-client + DB-scope-assertion pattern:
```csharp
[Fact]
public async Task CreateUser_Post_WhenAdmin_CreatesUserInActiveGroup()
{
    // Arrange
    await TestDataHelper.ClearDatabaseAsync(_factory.Services);
    var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
    // ... build formData ...

    // Act — _factory.TestGroupContext.ActiveGroupId defaults to 1 (the seeded EuphoriaInn group)
    var response = await adminClient.PostAsync("/Admin/CreateUser",
        new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

    // Assert
    response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

    using var scope = _factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
    var membership = context.UserGroups.FirstOrDefault(ug => ug.UserId == createdUser.Id && ug.GroupId == 1);
    membership.Should().NotBeNull("the created user should be assigned to the admin's active group");
}
```
For the D-03 cross-group-isolation regression test: create two groups, a user in group B only, authenticate as an Admin of group A (`_factory.TestGroupContext`/`AuthenticationHelper.CreateAuthenticatedAdminClientAsync` defaults to group 1 — check `AuthenticationHelper` for a variant that lets you target a specific group, or create a second group + membership row directly via `QuestBoardContext` as done for assertions above), then GET `/Admin/Users` and assert the response body does NOT contain the group-B-only user's identifying text (e.g. name/email) — mirroring the `content.Should().Contain(...)` assertion style seen in `CreateUser_Get_WhenAdmin_ShouldReturnForm` (line 187).

## Shared Patterns

### Silent-redirect-on-guard-failure (D-05)
**Source:** `QuestBoard.Service/Controllers/Admin/AdminController.cs`, lines 27, 60, 70, 80, 90 (five occurrences of the same `if (x == null) return RedirectToAction(...)` shape)
**Apply to:** `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer` — add the membership-check guard using the identical shape, no `TempData`/error-banner set, just `return RedirectToAction(nameof(Users));`

### Manual join for group-scoped user queries (D-01)
**Source:** `QuestBoard.Repository/UserRepository.cs`, lines 19-48 (`GetAllDungeonMasters`, `GetAllPlayers`)
**Apply to:** new group-scoped read method backing `AdminController.Users()`
```csharp
DbSet.Where(u => DbContext.UserGroups.Any(ug => ug.UserId == u.Id && ug.GroupId == groupId.Value))
```
No `GroupRole` filter needed for "any member" semantics (unlike the two existing methods, which filter by role).

### Repository -> Service pass-through layering
**Source:** `QuestBoard.Domain/Services/UserService.cs` lines 42-52, paired with `IUserRepository.cs`/`IUserService.cs` declarations
**Apply to:** any new repository method must get a matching one-line pass-through in `UserService` plus interface declarations in both `IUserRepository` and `IUserService` — `AdminController` calls only `IUserService`, never `IUserRepository` directly (layering already enforced throughout this controller: it depends on `IUserService`, not `IUserRepository`).

### Existence-check via `FirstOrDefaultAsync` returning null (D-05 helper)
**Source:** `QuestBoard.Repository/UserRepository.cs`, `GetGroupRoleAsync`, lines 50-57
**Apply to:** the membership-check helper backing D-05 — either reuse `GetGroupRoleAsync(userId, groupId) == null` directly, or add a same-shaped `IsMemberOfGroupAsync` that does `DbContext.UserGroups.AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId)`.

## No Analog Found

None — every file/change in scope has a direct or same-file analog to mirror.

## Metadata

**Analog search scope:** `QuestBoard.Repository/UserRepository.cs`, `QuestBoard.Domain/Interfaces/IUserRepository.cs`, `QuestBoard.Domain/Interfaces/IUserService.cs`, `QuestBoard.Domain/Services/UserService.cs`, `QuestBoard.Service/Controllers/Admin/AdminController.cs`, `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs`
**Files scanned:** 6 (all fully read; no file exceeded 2,000 lines)
**Pattern extraction date:** 2026-07-03
