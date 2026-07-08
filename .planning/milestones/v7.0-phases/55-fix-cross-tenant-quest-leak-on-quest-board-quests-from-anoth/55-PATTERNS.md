# Phase 55: Fix cross-tenant quest leak on quest board - Pattern Map

**Mapped:** 2026-07-06
**Files analyzed:** 5 modified (no new files — this phase is entirely in-place edits)
**Analogs found:** 5 / 5 (all analogs are in-repo precedents, several in the same files being edited)

This phase has an unusually strong pattern-mapping situation: RESEARCH.md already did exhaustive
direct-source-inspection with exact line numbers for every change, and the closest "analog" for
almost every edit is another entity/test/block in the *same file*, following a pattern already
established by Phase 49. There are no brand-new files and no external analogs to search for —
this map exists to hand the planner ready-to-copy before/after code blocks per file.

## File Classification

| Modified File | Role | Data Flow | Closest Analog | Match Quality |
|----------------|------|-----------|-----------------|----------------|
| `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` | middleware | request-response | itself — reorder existing guard clauses (no external analog needed) | exact (self-precedent) |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` (7 `HasQueryFilter` calls) | model/config | CRUD (query filter) | `CharacterEntity`/`CharacterClassEntity`/`CharacterImageEntity` filters, same file, lines 299-316 (Phase 49 fail-closed shape) | exact |
| `QuestBoard.Service/Controllers/GroupPickerController.cs` (`SelectGroup`) | controller | request-response | `GroupPickerController.Index`, same file, lines 17-24 (existing `isSuperAdmin`/`userId` resolution pattern) | exact (self-precedent) |
| `QuestBoard.Service/Constants/SessionKeys.cs` | config | CRUD (session key constants) | itself — additive constant, existing two-constant shape | exact |
| `.planning/codebase/CONCERNS.md` (lines 288-292, 307-310) | docs | — | itself — text correction only | n/a (docs, not code) |

### Test files (classified separately — role: test)

| Test File | Data Flow | Closest Analog | Match Quality |
|-----------|-----------|-----------------|----------------|
| `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs` (rewrite `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware`, extend `[Theory]`) | request-response | `AuthenticatedUser_NoActiveGroup_ProtectedAreaRedirectsToGroupPick` (same file, lines 131-154) | exact |
| `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs` (new non-member `SelectGroup` test) | request-response | `SelectGroup_ShouldPersistActiveGroupInSession` (same file, lines 130-158) | exact |
| `QuestBoard.UnitTests/Repository/` new/extended filter regression tests | CRUD (query filter) | `PlayerSignupRepositoryTests.cs` `TestActiveGroupContext`/`MutableTestGroupContext` pattern, lines 13-29 | exact |
| New unit test for `GroupSessionMiddleware`'s D-06 re-validation block | request-response | No direct analog exists — `GroupSessionMiddleware` has never been unit-tested directly (only integration-tested); see "No Analog Found" | partial |

---

## Pattern Assignments

### `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (middleware, request-response)

**Analog:** itself — reorder two existing guard clauses; no external pattern needed.

**Current guard order** (`GroupSessionMiddleware.cs:57-96`):
```csharp
public async Task InvokeAsync(HttpContext context)
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        await next(context);
        return;
    }

    if (context.User.IsInRole("SuperAdmin"))
    {
        await next(context);   // BUG (D-01): bypasses the group-gate on EVERY route
        return;
    }

    if (ExemptPathPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
    {
        await next(context);
        return;
    }

    var groupContext = context.RequestServices.GetRequiredService<IActiveGroupContext>();
    if (groupContext.ActiveGroupId == null)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }

        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/groups/pick?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    await next(context);
}
```

**Target shape (D-01/D-02 — swap order: exempt-path check first, SuperAdmin bypass removed):**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        await next(context);
        return;
    }

    // Exempt paths (picker itself, auth, platform, error routes) pass through for EVERY role,
    // including SuperAdmin — these are the genuinely group-agnostic areas (D-02).
    if (ExemptPathPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
    {
        await next(context);
        return;
    }

    // SuperAdmin is no longer exempt from the group-required gate on group-scoped routes (D-01).
    var groupContext = context.RequestServices.GetRequiredService<IActiveGroupContext>();
    if (groupContext.ActiveGroupId == null)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }

        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/groups/pick?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    // D-06 — periodic re-validation block inserted here, before `await next(context)`. See below.

    await next(context);
}
```

**Doc-comment correction required (not just code):** The class XML doc (`lines 9-29`) and the
inline comment above `ExemptPathPrefixes` describe the *old* rationale ("SuperAdmin passes
through — a null ActiveGroupId is correct by design and must be checked BEFORE the group check
to avoid a redirect loop"). This must be rewritten to describe the new ordering rationale
(exempt-paths-first, no role-based bypass) — copy the tone/style of the existing doc comment
block, just correct the described behavior.

**D-06 — new re-validation block to insert (concrete sketch, exact insertion point marked above):**
```csharp
// New session key alongside the existing two in SessionKeys.cs:
// public const string ActiveGroupValidatedAtUtc = "ActiveGroupValidatedAtUtc";

private static readonly TimeSpan MembershipRevalidationInterval = TimeSpan.FromMinutes(5);
// Matches SecurityStampValidatorOptions.ValidationInterval (Phase 41) for a consistent
// staleness bound — not because they share a code path (Session state, not a claim).

// ... inside InvokeAsync, immediately after the existing null-ActiveGroupId block, before `await next(context)`:
var session = context.Session;
var validatedAtRaw = session.GetString(SessionKeys.ActiveGroupValidatedAtUtc);
var needsRevalidation = validatedAtRaw == null
    || !DateTime.TryParse(validatedAtRaw, System.Globalization.CultureInfo.InvariantCulture,
           System.Globalization.DateTimeStyles.RoundtripKind, out var validatedAt)
    || DateTime.UtcNow - validatedAt > MembershipRevalidationInterval;

if (needsRevalidation && !context.User.IsInRole("SuperAdmin"))
{
    var userService = context.RequestServices.GetRequiredService<IUserService>();
    var userId = int.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    var role = await userService.GetGroupRoleByIdAsync(userId, groupContext.ActiveGroupId!.Value);

    if (role == null)
    {
        session.Remove(SessionKeys.ActiveGroupId);
        session.Remove(SessionKeys.ActiveGroupName);
        session.Remove(SessionKeys.ActiveGroupValidatedAtUtc);

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }

        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/groups/pick?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
}
```

**Imports needed:** `System.Security.Claims` (for `ClaimTypes.NameIdentifier`) and
`QuestBoard.Domain.Interfaces.IUserService` — check whether `IUserService` needs a `using`
(namespace is `QuestBoard.Domain.Interfaces`, already imported at line 3 for `IActiveGroupContext`,
so no new `using` line needed).

**Note on SuperAdmin exclusion from D-06:** SuperAdmin is deliberately excluded from the
re-validation check (`!context.User.IsInRole("SuperAdmin")`) because SuperAdmin's group
selection is never membership-gated in the first place (D-04's bypass in `SelectGroup`) — there
is no membership row to go stale for that role.

---

### `QuestBoard.Repository/Entities/QuestBoardContext.cs` (model/config, CRUD query-filter)

**Analog:** `CharacterEntity`/`CharacterClassEntity`/`CharacterImageEntity` filters, same file,
lines 299-316 — the exact fail-closed shape established by Phase 49, to be replicated across
7 entities (5 named in CONTEXT.md + 2 discovered by RESEARCH.md).

**Fail-closed template to copy** (`QuestBoardContext.cs:299-302`, already in the file):
```csharp
modelBuilder.Entity<CharacterEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```

**All 7 current (fail-open) → target (fail-closed) transformations:**

```csharp
// QuestEntity — lines 251-254
// CURRENT:
modelBuilder.Entity<QuestEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId == null ||
        e.GroupId == activeGroupContext.ActiveGroupId);
// TARGET:
modelBuilder.Entity<QuestEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

// ShopItemEntity — lines 256-259 — identical transformation, e.GroupId direct FK.

// ProposedDateEntity — lines 266-269 (via pd.Quest.GroupId navigation)
// CURRENT:
modelBuilder.Entity<ProposedDateEntity>()
    .HasQueryFilter(pd =>
        activeGroupContext.ActiveGroupId == null ||
        pd.Quest.GroupId == activeGroupContext.ActiveGroupId);
// TARGET:
modelBuilder.Entity<ProposedDateEntity>()
    .HasQueryFilter(pd =>
        activeGroupContext.ActiveGroupId != null &&
        pd.Quest.GroupId == activeGroupContext.ActiveGroupId);

// PlayerDateVoteEntity — lines 274-277 (via pdv.ProposedDate.Quest.GroupId) — identical transformation, keep navigation chain.

// PlayerSignupEntity — lines 283-286 (via ps.Quest.GroupId) — identical transformation, keep navigation.

// ReminderLogEntity — lines 290-293 (via r.Quest.GroupId) — DISCOVERED by RESEARCH.md, not in CONTEXT.md's list of 5, same shape/fix.
// CURRENT:
modelBuilder.Entity<ReminderLogEntity>()
    .HasQueryFilter(r =>
        activeGroupContext.ActiveGroupId == null ||
        r.Quest.GroupId == activeGroupContext.ActiveGroupId);
// TARGET:
modelBuilder.Entity<ReminderLogEntity>()
    .HasQueryFilter(r =>
        activeGroupContext.ActiveGroupId != null &&
        r.Quest.GroupId == activeGroupContext.ActiveGroupId);

// UserTransactionEntity — lines 323-326 (via t.ShopItem.GroupId) — DISCOVERED by RESEARCH.md, same shape/fix.
// CURRENT:
modelBuilder.Entity<UserTransactionEntity>()
    .HasQueryFilter(t =>
        activeGroupContext.ActiveGroupId == null ||
        t.ShopItem.GroupId == activeGroupContext.ActiveGroupId);
// TARGET:
modelBuilder.Entity<UserTransactionEntity>()
    .HasQueryFilter(t =>
        activeGroupContext.ActiveGroupId != null &&
        t.ShopItem.GroupId == activeGroupContext.ActiveGroupId);
```

**Comment block correction required:** The explanatory comment at lines 244-250 currently reads
*"Null = see all (SuperAdmin/seeding contexts intentionally bypass group scoping)"* — this
rationale is being reversed by D-03 and must be rewritten to match `CharacterEntity`'s existing
comment style (lines 295-298: *"deliberately does NOT offer a SuperAdmin cross-group view...
Do not 'fix' this... an empty roster is the intended behavior"*). Each of the 7 entities' own
inline comments (lines 244-250, 261-265, 271-273, 279-282, 288-289, 318-322) that describe the
old "SuperAdmin escape hatch" framing should be updated to describe the new fail-closed intent,
consistent in tone with the `CharacterEntity`/`CharacterClassEntity`/`CharacterImageEntity`
comments immediately below them in the same file (which already describe the fail-closed shape
correctly and need NO changes).

**No structural changes elsewhere in this file** — `CharacterEntity`, `CharacterClassEntity`,
`CharacterImageEntity` (lines 299-316) are already fail-closed (Phase 49) and require zero
changes. `UserEntity` (line 318 comment) intentionally has no filter — leave as-is.

---

### `QuestBoard.Service/Controllers/GroupPickerController.cs` (`SelectGroup`) (controller, request-response)

**Analog:** `GroupPickerController.Index`, same file, lines 17-24 — the controller's own existing
`isSuperAdmin`/`userId`-resolution style, to be mirrored in `SelectGroup` rather than introducing
a new pattern.

**Current `Index` pattern to mirror** (`GroupPickerController.cs:17-24`):
```csharp
public async Task<IActionResult> Index(string? returnUrl = null)
{
    var isSuperAdmin = User.IsInRole("SuperAdmin");
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    IList<GroupWithMemberCount> groups = isSuperAdmin
        ? await groupService.GetAllWithMemberCountAsync()
        : await groupService.GetGroupsForUserAsync(userId);
```

**Current `SelectGroup`** (`GroupPickerController.cs:41-51`):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SelectGroup(int groupId, string? returnUrl = null)
{
    var group = await groupService.GetByIdAsync(groupId);
    if (group == null) return NotFound();

    HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
    return RedirectToLocal(returnUrl);
}
```

**Target (D-04/D-05 — membership check added, mirroring `Index`'s own `isSuperAdmin`/`userId`
resolution style exactly):**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SelectGroup(int groupId, string? returnUrl = null)
{
    var group = await groupService.GetByIdAsync(groupId);
    if (group == null) return NotFound();

    var isSuperAdmin = User.IsInRole("SuperAdmin");
    if (!isSuperAdmin)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = await userService.GetGroupRoleByIdAsync(userId, groupId);
        if (role == null) return NotFound();
    }

    HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
    // D-06: stamp the re-validation timestamp whenever ActiveGroupId is (re)written.
    HttpContext.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
    return RedirectToLocal(returnUrl);
}
```

**Constructor change required (Pitfall 5):** Current constructor is
`GroupPickerController(IGroupService groupService) : Controller` — only `IGroupService`. Add
`IUserService userService` as a second primary-constructor parameter:
```csharp
public class GroupPickerController(IGroupService groupService, IUserService userService) : Controller
```
`IUserService` is already DI-registered app-wide (used in `AdminController`, `AccountController`,
etc.) — no `Program.cs` change needed, just the new constructor parameter. Add
`using QuestBoard.Domain.Interfaces;` if not already present (it is — line 3 already imports this
namespace for `IGroupService`).

**D-06 stamping also needed in `Index`'s single-group auto-select branch** (`GroupPickerController.cs:31-36`,
currently sets `ActiveGroupId`/`ActiveGroupName` but not a validated-at timestamp):
```csharp
if (!isSuperAdmin && groups.Count == 1)
{
    HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, groups[0].Id);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupName, groups[0].Name);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O")); // NEW
    return RedirectToLocal(returnUrl);
}
```

**Confirmed no change needed:** `Index` (GET) already correctly scopes non-SuperAdmin callers to
their own memberships (`groupService.GetGroupsForUserAsync(userId)`, line 24) — verified per
CONTEXT.md's "Claude's Discretion" item and RESEARCH.md's confirmation. D-04 only touches
`SelectGroup`.

---

### `QuestBoard.Service/Constants/SessionKeys.cs` (config, additive)

**Analog:** itself — existing two-constant shape.

**Current** (`SessionKeys.cs:6-10`):
```csharp
public static class SessionKeys
{
    public const string ActiveGroupId = "ActiveGroupId";
    public const string ActiveGroupName = "ActiveGroupName";
}
```

**Target — add one constant, same style:**
```csharp
public static class SessionKeys
{
    public const string ActiveGroupId = "ActiveGroupId";
    public const string ActiveGroupName = "ActiveGroupName";
    public const string ActiveGroupValidatedAtUtc = "ActiveGroupValidatedAtUtc";
}
```

---

### `.planning/codebase/CONCERNS.md` (docs — lines 288-292, 307-310)

**Analog:** none needed — plain text correction. Not code; flagged here only so the planner
schedules the edit alongside D-01/D-03. Update the stated expectation ("SuperAdmin should see
all groups' quests via `IgnoreQueryFilters`, not zero quests from a null group") to reflect that
D-01/D-03 now make SuperAdmin gated exactly like every other role, and that the fail-open filter
shape was the confirmed root cause of the Phase 55 incident.

---

## Test Patterns

### `GroupSessionMiddlewareIntegrationTests.cs` — rewrite required (Pitfall 2)

**Analog:** `AuthenticatedUser_NoActiveGroup_ProtectedAreaRedirectsToGroupPick` (same file,
lines 131-154) — this is the target shape the rewritten SuperAdmin test should match (assert
redirect to `/groups/pick`, not absence of redirect).

**Test to REWRITE** (`GroupSessionMiddlewareIntegrationTests.cs:45-66`, current — asserts the
OLD/buggy behavior and will contradict D-01 once shipped):
```csharp
[Fact]
public async Task SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware()
{
    await TestDataHelper.ClearDatabaseAsync(factory.Services);
    var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

    factory.TestGroupContext.ActiveGroupId = null;
    try
    {
        var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);

        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().NotContain("/groups/pick");
    }
    finally
    {
        factory.TestGroupContext.ActiveGroupId = 1;
    }
}
```

**Target — rename and invert the assertion** (mirror `AuthenticatedUser_NoActiveGroup_RedirectsToGroupPick`,
lines 20-40, using `CreateAuthenticatedSuperAdminClientAsync` instead of a regular user):
```csharp
[Fact]
public async Task SuperAdmin_NoActiveGroup_RedirectsToGroupPick()
{
    await TestDataHelper.ClearDatabaseAsync(factory.Services);
    var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

    factory.TestGroupContext.ActiveGroupId = null;
    try
    {
        var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().Contain("/groups/pick");
    }
    finally
    {
        factory.TestGroupContext.ActiveGroupId = 1;
    }
}
```

**`[Theory]` extension** — add a SuperAdmin-authenticated variant of the existing protected-area
theory (lines 131-154), using `CreateAuthenticatedSuperAdminClientAsync` in place of
`CreateAuthenticatedClientWithUserAsync`, same `[InlineData]` route list (`/Calendar`,
`/DungeonMaster/EditProfile`, `/QuestLog`).

### `GroupPickerControllerIntegrationTests.cs` — new non-member test

**Analog:** `SelectGroup_ShouldPersistActiveGroupInSession` (same file, lines 130-158) for the
POST-form pattern; `Index_WhenMultiGroupUser_ShouldReturnPickerPage` (lines 76-108) for the
"seed a second group the user is NOT a member of" pattern.

**Pattern to copy for the new test:**
```csharp
[Fact]
public async Task SelectGroup_WhenNotAMember_ShouldReturnNotFound()
{
    // Arrange
    await TestDataHelper.ClearDatabaseAsync(_factory.Services);
    var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
        _factory, "nonmemberuser", "nonmember@example.com", roles: ["Player"]);

    // Seed a second group the authenticated user is NOT a member of (mirrors
    // Index_WhenMultiGroupUser_ShouldReturnPickerPage's second-group seeding, lines 84-98,
    // but deliberately WITHOUT adding a UserGroupEntity row for this user).
    int otherGroupId;
    using (var scope = _factory.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var otherGroup = new GroupEntity { Name = "OtherGroup_" + Guid.NewGuid().ToString("N")[..8], CreatedAt = DateTime.UtcNow };
        context.Groups.Add(otherGroup);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        otherGroupId = otherGroup.Id;
    }

    // Act
    var formData = new Dictionary<string, string> { ["groupId"] = otherGroupId.ToString() };
    var response = await client.PostAsync("/GroupPicker/SelectGroup",
        new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

    // Assert — 404, matching this codebase's cross-tenant "hide existence" convention (D-05)
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

### `QuestBoard.UnitTests/Repository/` — new/extended filter regression tests

**Analog:** `PlayerSignupRepositoryTests.cs` lines 13-29 — the `CreateContext(databaseName,
IActiveGroupContext)` overload plus `TestActiveGroupContext`/`MutableTestGroupContext` pattern.

**Pattern to copy (per hardened entity, or parameterized across all 7):**
```csharp
private static QuestBoardContext CreateContext(string databaseName, IActiveGroupContext activeGroupContext)
{
    var options = new DbContextOptionsBuilder<QuestBoardContext>()
        .UseInMemoryDatabase(databaseName)
        .Options;

    return new QuestBoardContext(options, activeGroupContext);
}

private sealed class TestActiveGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId => null;   // simulates the SuperAdmin/null-group case
}
```

Seed two groups' worth of rows for the target entity (`QuestEntity`, `ShopItemEntity`,
`ProposedDateEntity`, `PlayerDateVoteEntity`, `PlayerSignupEntity`, `ReminderLogEntity`,
`UserTransactionEntity`), query through a context built with `TestActiveGroupContext`
(`ActiveGroupId == null`), and assert **zero rows returned** (fail-closed) — the inverse of what
the pre-fix filter would have returned (every row, fail-open).

### New unit test for `GroupSessionMiddleware`'s D-06 block — no existing analog (see below)

---

## Shared Patterns

### Fail-closed group-scoped query filter
**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs:299-316` (`CharacterEntity`,
`CharacterClassEntity`, `CharacterImageEntity` — Phase 49)
**Apply to:** All 7 entities named in D-03 (`QuestEntity`, `ShopItemEntity`, `ProposedDateEntity`,
`PlayerDateVoteEntity`, `PlayerSignupEntity`, `ReminderLogEntity`, `UserTransactionEntity`)
```csharp
.HasQueryFilter(e =>
    activeGroupContext.ActiveGroupId != null &&
    e.GroupId == activeGroupContext.ActiveGroupId);
```

### Cross-tenant "hide existence" response convention
**Source:** Phase 49 (D-04/D-09/D-13), this phase's D-05
**Apply to:** `GroupPickerController.SelectGroup` when the caller isn't a member of the posted
`groupId` — return `NotFound()` (404), never `Forbid()` (403).

### Membership-check primitive
**Source:** `QuestBoard.Domain/Interfaces/IUserService.cs:79` —
`Task<GroupRole?> GetGroupRoleByIdAsync(int userId, int groupId)` (returns null if not a member)
**Apply to:** `GroupPickerController.SelectGroup` (D-04) and `GroupSessionMiddleware`'s D-06
re-validation block — both call this exact primitive, no new repository method needed.

### `isSuperAdmin`/`userId` resolution style
**Source:** `GroupPickerController.Index`, lines 19-20:
```csharp
var isSuperAdmin = User.IsInRole("SuperAdmin");
var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
```
**Apply to:** `GroupPickerController.SelectGroup` (D-04) — same controller, same style, no new
pattern introduced. Note: in `GroupSessionMiddleware` (a non-MVC-context class), the equivalent
is `context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value` since
`User.FindFirstValue` is an MVC `ControllerBase` extension method not available on `ClaimsPrincipal`
directly in middleware.

### Session-cookie round-trip test-harness limitation (Pitfall 6)
**Source:** `GroupPickerControllerIntegrationTests.cs` lines 148-153, 163-167 (comments)
**Apply to:** D-06's test coverage — prefer a direct unit test against
`GroupSessionMiddleware.InvokeAsync` with a constructed fake `HttpContext`/session over an
integration test that would need to simulate wall-clock time elapsing on a real session cookie,
which this test harness's `TestAuthHandler` (header-based, not cookie-based) cannot reliably do.

---

## No Analog Found

| File/Test | Role | Data Flow | Reason |
|-----------|------|-----------|--------|
| New unit test for `GroupSessionMiddleware`'s D-06 re-validation block | test | request-response | No existing test constructs a fake `HttpContext`/`ISession` directly against `GroupSessionMiddleware.InvokeAsync` — all existing coverage for this middleware is integration-level (`WebApplicationFactory`-based, via `GroupSessionMiddlewareIntegrationTests.cs`). Per Pitfall 6, this new test should construct a minimal fake `HttpContext` with a mocked/in-memory `ISession` (e.g. via `Microsoft.AspNetCore.Http.DefaultHttpContext` + a test double implementing `ISession`, or a `TestServer`-free unit-level approach) and call `InvokeAsync` directly, asserting the 409/redirect behavior when `ActiveGroupValidatedAtUtc` is stale and `GetGroupRoleByIdAsync` returns null. The planner should treat this as new test infrastructure, not a copy of an existing file. |

## Metadata

**Analog search scope:** `QuestBoard.Service/Middleware/`, `QuestBoard.Service/Controllers/`,
`QuestBoard.Service/Constants/`, `QuestBoard.Repository/Entities/`,
`QuestBoard.IntegrationTests/Controllers/`, `QuestBoard.UnitTests/Repository/`
**Files scanned:** 6 source files read directly (all full-file, all ≤ 105 lines);
RESEARCH.md's own exhaustive direct-source-inspection (12 controllers, full middleware/context/
controller files) reused rather than re-read, per the no-re-read constraint.
**Pattern extraction date:** 2026-07-06
