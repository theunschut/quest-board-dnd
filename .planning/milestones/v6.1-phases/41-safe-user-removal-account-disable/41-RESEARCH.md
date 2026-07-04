# Phase 41: Safe User Removal & Account Disable - Research

**Researched:** 2026-07-04
**Domain:** ASP.NET Core Identity (lockout mechanism reuse), EF Core FK cascade behavior, existing Platform-area controller conventions
**Confidence:** HIGH

## Summary

This phase is almost entirely a "wire existing primitives together correctly" phase, not a new-technology phase. Every claim in CONTEXT.md's Decisions section was checked directly against the current source tree and confirmed accurate on the points it made — `IGroupService.RemoveMemberAsync(groupId, userId)` is a pre-existing, idempotent (no-throw), non-cascading primitive already live in `GroupController.RemoveMember`; `IdentityService.cs` follows a uniform `FindByIdAsync(id.ToString())` → `userManager.XxxAsync(entity)` wrapping style with zero exceptions; and no `SecurityStampValidatorOptions` configuration currently exists in `Program.cs`, confirming D-11 is a net-new, standalone `builder.Services.Configure<SecurityStampValidatorOptions>(...)` call.

However, direct inspection of `QuestBoardContext.OnModelCreating` surfaced a gap in CONTEXT.md's FK inventory: it correctly lists the `NoAction` FKs that make the *old* hard-delete throw a `DbUpdateException`, but it does not mention that **two other `UserEntity`-referencing FKs are configured `Cascade`, not `NoAction`** — `CharacterEntity.OwnerId` and the 1:1 `DungeonMasterProfileEntity` keyed on the user's own `Id`. Under the old `userService.RemoveAsync(user)` path, these would not throw — they would silently delete the user's characters and DM profile/bio/image. This doesn't change any locked decision (D-01 already replaces the whole hard-delete call with `RemoveMemberAsync`, which touches none of these tables), but it strengthens the rationale and should be captured for accuracy in Success Criterion 2's phrasing ("no unhandled server error" AND "no silent data loss").

The `Areas/Platform/Controllers/GroupController.cs` header-bar/mobile-view conventions (D-06) were confirmed pixel-for-pixel reusable. The `IUserService.GetAllAsync()` signature (D-04/D-08) returns `Task<IList<User>>` via `IBaseService<User>` — confirmed. The `User` domain model (`QuestBoard.Domain/Models/User.cs`) has **no `LockoutEnd` field** and the `EntityProfile` AutoMapper (`QuestBoard.Repository/Automapper/EntityProfile.cs:39`, not `QuestBoard.Domain/Automapper/EntityProfile.cs` as CLAUDE.md's Architecture note might suggest — actual location differs, see Pitfall below) does not map it — the new `UsersController.Index` view needs a real per-user lockout lookup via a new `IIdentityService` method, not a `User.LockoutEnd` property read.

**Primary recommendation:** Implement exactly as CONTEXT.md's decisions specify — `AdminController.DeleteUser` calls `groupService.RemoveMemberAsync`, new `IIdentityService`/`IdentityService` methods follow the existing `FindByIdAsync` + `UserManager` wrapper pattern, new `UsersController` mirrors `GroupController`'s structure/policy/header-bar pattern exactly, and `SecurityStampValidatorOptions.ValidationInterval` is configured as a new standalone block in `Program.cs` immediately after the `AddIdentity(...)` chain.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Remove user from active group | API / Backend (`AdminController.DeleteUser` → `IGroupService`) | Database (cascades only `UserGroupEntity` row) | Membership is a domain/repository concern; controller just re-routes to the existing service call |
| Disable/enable account (lockout) | API / Backend (`IIdentityService`/`IdentityService` wrapping `UserManager`) | Database (`AspNetUsers.LockoutEnd`, `SecurityStamp` columns — no schema change) | ASP.NET Core Identity owns lockout state; this phase only calls existing `UserManager` methods, never touches schema |
| Cross-group platform Users list | API / Backend (new `UsersController`) + Browser (new Razor views) | — | SuperAdmin-only cross-tenant read; correctly bypasses per-group scoping by design (mirrors `GroupController`) |
| Disabled-vs-lockout login messaging | API / Backend (`AccountController.Login` POST) | Browser (rendered ModelState error) | Business logic (exact `MaxValue` comparison) belongs server-side; the view just renders whatever error string is set |
| Session invalidation on disable | API / Backend (`UpdateSecurityStampAsync`) + Framework middleware (`SecurityStampValidator`, ASP.NET Core Identity internals) | — | Framework-owned re-validation loop; this phase only shortens its interval and triggers a stamp bump |

## Standard Stack

No new packages. This phase exclusively uses APIs already present via `Microsoft.AspNetCore.Identity` (already referenced through `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, in use since Phase 1) and the project's own `QuestBoard.Domain`/`QuestBoard.Repository` abstractions.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.AspNetCore.Identity | bundled with `net10.0` shared framework (no separate NuGet reference needed for `UserManager<TUser>`/`SignInManager<TUser>`/`SecurityStampValidatorOptions` — already in use) | Lockout, security stamp, sign-in APIs | Already the app's sole auth system; reusing `LockoutEnd` avoids a schema change entirely (per D-09/D-10) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Reusing `LockoutEnd = DateTimeOffset.MaxValue` as the "disabled" sentinel | New `IsDisabled bool` column + migration | Locked decision (D-09) explicitly rejects this — a new column would require a migration and duplicate state that `LockoutEnd` already models; also would NOT interact with the deliberate `LockoutEnabled = false` DB-only escape hatch the user wants to keep |

**Installation:** None required — no new NuGet packages, no `npm install`, no schema migration expected (see Package Legitimacy Audit below).

**Version verification:** N/A — no new package references being added. `net10.0` target framework confirmed via `QuestBoard.Service.csproj:4` (`<TargetFramework>net10.0</TargetFramework>`).

## Package Legitimacy Audit

No external packages are being installed in this phase. All APIs used (`UserManager.SetLockoutEndDateAsync`, `UpdateSecurityStampAsync`, `IsLockedOutAsync`, `SecurityStampValidatorOptions`) ship in the `Microsoft.AspNetCore.App` shared framework already referenced by the `net10.0` web SDK — no `PackageReference` change needed.

**Packages removed due to [SLOP] verdict:** none (no packages evaluated — none proposed)
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
                     ┌─────────────────────────────────────────────┐
                     │        Group-admin Users page (existing)     │
                     │  Views/Admin/Users.cshtml + .Mobile.cshtml    │
                     │  "Remove from Group" button (renamed, D-02)   │
                     └───────────────────┬───────────────────────────┘
                                         │ DELETE /Admin/DeleteUser/{id}
                                         ▼
                     ┌─────────────────────────────────────────────┐
                     │   AdminController.DeleteUser(int id)          │
                     │   [AdminOnly] — group-scoped guard unchanged  │
                     │   BEFORE: userService.RemoveAsync(user)       │◄── hard delete, cascades ALL memberships
                     │   AFTER:  groupService.RemoveMemberAsync(     │      + characters + DM profile (Cascade FKs)
                     │             groupId, id)                      │
                     └───────────────────┬───────────────────────────┘
                                         │ (identical call GroupController.RemoveMember already makes)
                                         ▼
                     ┌─────────────────────────────────────────────┐
                     │  GroupRepository.RemoveMemberAsync            │
                     │  DELETE single UserGroupEntity row, no-op     │
                     │  if the row doesn't exist — never throws      │
                     └─────────────────────────────────────────────┘


                     ┌─────────────────────────────────────────────┐
                     │  NEW: Areas/Platform/Controllers/             │
                     │       UsersController.cs  [SuperAdminOnly]    │
                     │  Index() -> IUserService.GetAllAsync()        │
                     │          + per-user IIdentityService lookup   │
                     │          of LockoutEnd for Active/Disabled    │
                     │          badge                                │
                     └──────┬───────────────────────┬────────────────┘
                            │ POST Disable(userId)   │ POST Enable(userId)
                            ▼                        ▼
              ┌───────────────────────────┐  ┌───────────────────────────┐
              │ IdentityService            │  │ IdentityService             │
              │  .DisableUserAsync         │  │  .EnableUserAsync           │
              │  SetLockoutEndDateAsync    │  │  SetLockoutEndDateAsync     │
              │    (user, MaxValue)        │  │    (user, null)             │
              │  UpdateSecurityStampAsync  │  │  (no stamp bump — no active │
              │    (user)     [D-10]       │  │   session to invalidate)   │
              └───────────────────────────┘  └───────────────────────────┘


        Login flow (AccountController.Login POST) ─────────────────────────
        result = PasswordSignInAsync(...)
             │
             ├─ Succeeded ────────────────────────► redirect to GroupPicker
             │
             ├─ IsLockedOut ──► NEW: look up target user's LockoutEnd
             │                     │
             │                     ├─ == DateTimeOffset.MaxValue ─► "This account has been
             │                     │                                 disabled. Contact an
             │                     │                                 administrator."
             │                     └─ otherwise ──────────────────► existing "...15 minutes." copy
             │
             └─ else ─────────────────────────────► "Invalid login attempt."


        Active-session invalidation (framework-owned, D-10 + D-11) ────────
        Existing auth cookie ──(next request, up to ValidationInterval later)──►
             SecurityStampValidator compares cookie's stamp vs. AspNetUsers.SecurityStamp
             │
             ├─ match     ──► request proceeds
             └─ mismatch  ──► forced re-authentication (sign-out), user hits Login,
                               sees the IsLockedOut branch above
```

### Recommended Project Structure

No new folders — this phase adds files into the existing structure:

```
QuestBoard.Service/
├── Areas/Platform/
│   ├── Controllers/
│   │   ├── GroupController.cs         # existing — unchanged except its Index view gains a link (D-06)
│   │   └── UsersController.cs         # NEW — [Area("Platform")] [Authorize(Policy = "SuperAdminOnly")]
│   └── Views/
│       ├── Group/Index.cshtml         # header bar gains "Manage Users" button
│       ├── Group/Index.Mobile.cshtml  # same, mobile variant
│       └── Users/
│           ├── Index.cshtml           # NEW
│           └── Index.Mobile.cshtml    # NEW (D-05)
├── Controllers/Admin/
│   ├── AdminController.cs             # DeleteUser body changes (D-01); no new actions
│   └── AccountController.cs           # Login POST IsLockedOut branch changes (D-13)
├── Views/Admin/
│   ├── Users.cshtml                   # Delete button + deleteUser() JS renamed (D-02)
│   └── Users.Mobile.cshtml            # same rename, mobile variant
├── Program.cs                         # new Configure<SecurityStampValidatorOptions> block (D-11)
QuestBoard.Domain/
├── Interfaces/IIdentityService.cs     # 3 new method signatures
QuestBoard.Repository/
├── IdentityService.cs                 # 3 new method implementations
```

### Pattern 1: Group-scoped removal via existing service (D-01)

**What:** Replace `AdminController.DeleteUser`'s hard-delete call with the exact `IGroupService.RemoveMemberAsync` call `GroupController.RemoveMember` already makes.
**When to use:** Any "remove user from group" action, regardless of which controller/page triggers it.
**Example:**
```csharp
// Source: QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:267-274 (existing, unchanged)
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RemoveMember(int id, int userId, string? search, string? memberSearch)
{
    await groupService.RemoveMemberAsync(id, userId);
    TempData["Success"] = "Member removed from the group.";
    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
}

// AdminController.DeleteUser — CURRENT (to be replaced):
// QuestBoard.Service/Controllers/Admin/AdminController.cs:339-358
[HttpDelete]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteUser(int id)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return NotFound();

    var targetRole = await userService.GetGroupRoleByIdAsync(id, groupId.Value);
    if (targetRole == null) return NotFound();

    var user = await userService.GetByIdAsync(id);
    if (user == null) return NotFound();

    await userService.RemoveAsync(user);   // <-- REPLACE this line
    return Ok();
}
// REPLACEMENT: await groupService.RemoveMemberAsync(groupId.Value, id);
// groupService (IGroupService) is NOT currently injected into AdminController's
// primary constructor list, but IS already present — see line 21:
// `IGroupService groupService` is already a constructor parameter (used by CreateUser/EditUser
// email-collision paths). No new DI registration needed.
```

### Pattern 2: `IIdentityService` thin-wrapper method style

**What:** Every existing `IdentityService` method takes a plain `int userId`, resolves the entity via `userManager.FindByIdAsync(userId.ToString())`, guards for null, and delegates to the corresponding `UserManager<UserEntity>` call.
**When to use:** All 3 new methods for this phase (disable, enable, get-lockout-end) must follow this exact shape for consistency.
**Example:**
```csharp
// Source: QuestBoard.Repository/IdentityService.cs (existing pattern, e.g. ConfirmEmailDirectlyAsync:141-149)
public async Task<IdentityResult> ConfirmEmailDirectlyAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });

    entity.EmailConfirmed = true;
    return await userManager.UpdateAsync(entity);
}

// RECOMMENDED new methods, following the identical shape:
public async Task<IdentityResult> DisableUserAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });

    await userManager.SetLockoutEndDateAsync(entity, DateTimeOffset.MaxValue);
    await userManager.UpdateSecurityStampAsync(entity); // D-10 — invalidate active session
    return IdentityResult.Success;
    // NOTE: do NOT set entity.LockoutEnabled here — D-09 deliberately leaves it untouched.
}

public async Task<IdentityResult> EnableUserAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });

    await userManager.SetLockoutEndDateAsync(entity, null);
    return IdentityResult.Success;
    // D-12: no SecurityStamp bump on enable — no active session to invalidate.
}

public async Task<DateTimeOffset?> GetLockoutEndAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    return entity?.LockoutEnd;
    // UserManager also exposes GetLockoutEndDateAsync(TUser) which does the same thing —
    // either is fine; FindByIdAsync + direct property read avoids an extra async hop.
}
```

### Pattern 3: Platform-area controller/view scaffold (D-04 through D-08)

**What:** `[Area("Platform")]`, `[Authorize(Policy = "SuperAdminOnly")]` class-level attributes; `Index()` GET action returning a strongly-typed list view; header-bar action button pattern in the parent `GroupController.Index` view.
**When to use:** The new `UsersController` + its `Index` view + the entry-point link in `GroupController.Index`'s header.
**Example:**
```csharp
// Source: QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:17-32 (structure to mirror)
[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class UsersController(IUserService userService, IIdentityService identityService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await userService.GetAllAsync();
        var viewModels = new List<PlatformUserViewModel>();
        foreach (var user in users)
        {
            var lockoutEnd = await identityService.GetLockoutEndAsync(user.Id);
            viewModels.Add(new PlatformUserViewModel
            {
                User = user,
                IsDisabled = lockoutEnd == DateTimeOffset.MaxValue
            });
        }
        return View(viewModels);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int userId)
    {
        // Self-disable guard (D-07) — compare against the CURRENT signed-in user's id,
        // not a role check, since SuperAdmin-on-SuperAdmin is explicitly allowed (D-08).
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (currentUserId != null && int.Parse(currentUserId) == userId)
        {
            TempData["Error"] = "You cannot disable your own account.";
            return RedirectToAction(nameof(Index));
        }
        await identityService.DisableUserAsync(userId);
        TempData["Success"] = "Account disabled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(int userId)
    {
        await identityService.EnableUserAsync(userId);
        TempData["Success"] = "Account re-enabled.";
        return RedirectToAction(nameof(Index));
    }
}
```
```html
<!-- Source: QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml:6-15 (header bar to mirror in Index.Mobile.cshtml too) -->
<div class="card-header modern-card-header d-flex justify-content-between align-items-center">
    <h2 class="mb-0">
        <i class="fas fa-layer-group text-danger me-2"></i>
        Group Management
    </h2>
    <a asp-controller="Group" asp-action="Create" asp-area="Platform" class="btn btn-success">
        <i class="fas fa-plus me-2"></i>Create Group
    </a>
    <!-- NEW: add next to Create Group button per D-06 -->
    <a asp-controller="Users" asp-action="Index" asp-area="Platform" class="btn btn-secondary">
        <i class="fas fa-users-cog me-2"></i>Manage Users
    </a>
</div>
```

### Anti-Patterns to Avoid
- **Setting `LockoutEnabled` anywhere in the new Disable/Enable code:** D-09 is explicit and deliberate — the app must never write to `LockoutEnabled`. Only `LockoutEnd` and (on disable only) `SecurityStamp` should be touched.
- **Building a new repository method for group-scoped removal:** `IGroupService.RemoveMemberAsync` already exists and is already proven in production use by `GroupController.RemoveMember` — do not add a parallel/duplicate method.
- **Reading `User.LockoutEnd` off the domain model:** it doesn't exist on `QuestBoard.Domain.Models.User` and isn't mapped by AutoMapper — must go through a dedicated `IIdentityService` lookup per user.
- **Using a fuzzy/threshold check for "is disabled" in `AccountController.Login`:** D-13 locks in an *exact* `== DateTimeOffset.MaxValue` comparison, not "more than N days out." A threshold check would also incorrectly flag long-lived legitimate token expirations if any existed (none currently do, but exactness avoids the ambiguity entirely).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Account disable/enable | New `IsDisabled` boolean column + migration + new query filters | `UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue \| null)` | Locked decision D-09; avoids schema change, reuses Identity's built-in `IsLockedOutAsync` short-circuit and the existing lockout-check already wired into `PasswordSignInAsync` |
| Invalidating an active session on disable | Custom session/cookie revocation list | `UserManager.UpdateSecurityStampAsync(user)` + shortened `SecurityStampValidatorOptions.ValidationInterval` | Framework-native mechanism designed for exactly this; already the standard Identity approach for "kick out an active session" |
| Distinguishing disabled vs. temporary lockout | A parallel "reason" enum/column | Exact `LockoutEnd == DateTimeOffset.MaxValue` sentinel comparison | Locked decision D-13; `MaxValue` is already the natural "permanent" sentinel `SetLockoutEndDateAsync` accepts, no new state needed |

**Key insight:** Every "hand-roll risk" in this phase has already been pre-empted by the CONTEXT.md decisions — the correct call in each case is to re-use ASP.NET Core Identity's built-in `LockoutEnd`/`SecurityStamp` fields rather than adding new schema or new authorization concepts.

## Common Pitfalls

### Pitfall 1: `IsLockedOutAsync`'s silent short-circuit on `LockoutEnabled = false`
**What goes wrong:** A SuperAdmin calls `Disable` on an account that has `LockoutEnabled = false` (the deliberate DB-only escape hatch per D-09/specifics), and nothing happens from the user's perspective — the account can still log in.
**Why it happens:** `UserManager.IsLockedOutAsync` (called internally by `PasswordSignInAsync` with `lockoutOnFailure: true`... actually the lockout check on successful-credentials-but-locked-out path) short-circuits to `false` whenever `entity.LockoutEnabled == false`, **regardless of `LockoutEnd`** — this is by design in ASP.NET Core Identity (`UserManager.IsLockedOutAsync` checks `SupportsUserLockout && await GetLockoutEnabledAsync(user) && ...LockoutEnd > DateTimeOffset.UtcNow`).
**How to avoid:** This is the *intended* behavior per D-09 — do not "fix" it or add a warning. It's the user's explicit, deliberate manual escape hatch. Do not add any code path that reads or displays `LockoutEnabled` in the new `UsersController.Index` view either, unless asked — CONTEXT.md scopes this phase to `LockoutEnd` only.
**Warning signs:** If a future phase's plan tries to "helpfully" auto-fix `LockoutEnabled` when disabling — this is a locked-decision violation and should be rejected at plan-review time.

### Pitfall 2: Two `UserEntity` FKs are `Cascade`, not `NoAction` — undocumented in CONTEXT.md's FK list
**What goes wrong:** CONTEXT.md's FK inventory (in `<code_context>` → Established Patterns) lists 5 `NoAction` FKs that throw `DbUpdateException` on hard-delete: `QuestEntity.DungeonMasterId`, `ShopItemEntity.CreatedByDmId`, `UserTransactionEntity.UserId`, `TradeItemEntity.OfferedByPlayerId`, `ReminderLogEntity.PlayerId`. Direct inspection of `QuestBoardContext.OnModelCreating` (`QuestBoard.Repository/Entities/QuestBoardContext.cs`) confirms all 5 are accurate, but reveals **two additional FKs are `Cascade`**: `CharacterEntity.OwnerId` (line ~115-119) and the 1:1 `DungeonMasterProfileEntity` keyed on the user's own `Id` (line ~144-149). Also `PlayerSignupEntity.PlayerId` (line ~66-70) is `Cascade`, not in CONTEXT.md's list either.
**Why it happens:** These weren't the ones that would visibly crash with a `DbUpdateException` — they'd silently succeed and delete the user's characters, DM profile (bio + profile image), and player-signup rows. Since the crash-on-delete bug (SAFE-02 motivation) was likely discovered via the loud `NoAction` FKs, the quiet `Cascade` ones were probably never noticed.
**How to avoid:** Not an issue for THIS phase's actual fix — D-01 replaces `userService.RemoveAsync(user)` entirely with `groupService.RemoveMemberAsync(groupId, userId)`, which never touches `CharacterEntity`, `DungeonMasterProfileEntity`, or `PlayerSignupEntity` at all. But the planner/plan-checker should phrase Success Criterion 2 verification as "no error AND no silent side-effect data loss," and a plan step verifying DeleteUser's new behavior should confirm characters/DM-profile/signups for the removed user are untouched after removal (not just that no exception was thrown).
**Warning signs:** Any future feature that re-introduces a true hard user-delete (e.g., a "purge account entirely" SuperAdmin feature) must re-audit this same FK list — and should treat this research's Cascade list as the authoritative version, not CONTEXT.md's original NoAction-only list.

### Pitfall 3: `EntityProfile.cs`'s actual location differs from CLAUDE.md's documented path
**What goes wrong:** CLAUDE.md states `QuestBoard.Domain/Automapper/EntityProfile.cs` for the Entity ↔ DomainModel mapper. The actual file is at `QuestBoard.Repository/Automapper/EntityProfile.cs`.
**Why it happens:** Likely drifted after a prior refactor moved AutoMapper profiles into the Repository layer (where the concrete `UserEntity` type is known) without updating CLAUDE.md.
**How to avoid:** This phase does not need to add any AutoMapper mapping (the `User` domain model deliberately has no `LockoutEnd` field, per the recommended pattern above — lockout status is looked up via `IIdentityService`, not mapped through AutoMapper). No action needed for this phase, but if a plan step references CLAUDE.md's stated path for `EntityProfile.cs`, correct it to `QuestBoard.Repository/Automapper/EntityProfile.cs`.
**Warning signs:** A build error "file not found" if a plan step tries to `Read`/`Edit` the CLAUDE.md-documented path literally.

### Pitfall 4: `AccountController.Login`'s `IsLockedOut` branch has no user context to look up `LockoutEnd` yet
**What goes wrong:** The current `IsLockedOut` branch (`AccountController.cs:137-141`) only has `model.Email`, not a resolved user id/entity — a plan step must add a lookup (`identityService.GetIdByEmailAsync(model.Email)` already exists, or a new `GetLockoutEndByEmailAsync`) before it can compare `LockoutEnd` against `MaxValue`.
**Why it happens:** `PasswordSignInAsync`'s `SignInResult` doesn't carry the target user's `LockoutEnd` — Identity's `SignInResult.LockedOut` is a plain static singleton with no payload.
**How to avoid:** Add the lookup explicitly in the `IsLockedOut` branch:
```csharp
if (result.IsLockedOut)
{
    var userId = await identityService.GetIdByEmailAsync(model.Email);
    var lockoutEnd = userId.HasValue ? await identityService.GetLockoutEndAsync(userId.Value) : null;

    ModelState.AddModelError(string.Empty, lockoutEnd == DateTimeOffset.MaxValue
        ? "This account has been disabled. Contact an administrator."
        : "Account locked due to too many failed attempts. Try again in 15 minutes.");
    return View(model);
}
```
`identityService.GetIdByEmailAsync(email)` already exists (`IIdentityService.GetIdByEmailAsync`, `QuestBoard.Repository/IdentityService.cs:152-156`) — no new lookup-by-email method needed, just compose it with the new `GetLockoutEndAsync(userId)`.
**Warning signs:** A plan that tries to read `LockoutEnd` directly off `SignInResult` will fail to compile — that property doesn't exist on `Microsoft.AspNetCore.Identity.SignInResult`.

### Pitfall 5: `AccountController` does not currently inject `IIdentityService` directly for this purpose — but it does already have it
**What goes wrong:** A plan might assume `identityService` needs to be newly added to `AccountController`'s constructor.
**Why it happens:** Not actually a pitfall — `AccountController`'s constructor (`QuestBoard.Service/Controllers/Admin/AccountController.cs:16`) already includes `IIdentityService identityService` as a parameter (used elsewhere for `GetIdByEmailAsync`, `GeneratePasswordResetTokenForUserAsync`, etc.). No DI change needed.
**How to avoid:** Confirmed no-op — just use the existing `identityService` field.
**Warning signs:** N/A — flagging so the planner doesn't add an unnecessary constructor-injection task.

## Code Examples

### `Program.cs` — SecurityStampValidatorOptions configuration (D-11)
```csharp
// Source: Microsoft Learn — SecurityStampValidatorOptions.ValidationInterval
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.securitystampvalidatoroptions.validationinterval
// Add immediately after the existing AddIdentity<UserEntity, IdentityRole<int>>(...) chain
// (QuestBoard.Service/Program.cs, after line 63's .AddDefaultTokenProviders();)
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});
```

### Confirming `IsLockedOutAsync`'s `LockoutEnabled` short-circuit (framework source, not this codebase)
```csharp
// Documented ASP.NET Core Identity behavior (Microsoft.AspNetCore.Identity.UserManager<TUser>):
// public virtual async Task<bool> IsLockedOutAsync(TUser user)
// {
//     ThrowIfDisposed();
//     if (!SupportsUserLockout) return false;
//     return await GetLockoutEndDateAsync(user) is DateTimeOffset lockoutTime &&
//            lockoutTime >= DateTimeOffset.UtcNow;
// }
// NOTE: The actual short-circuit on LockoutEnabled happens inside SignInManager's
// CheckCanSignInAsync/CheckLockoutAsync path (checked before password verification when
// lockoutOnFailure is honored), not inside IsLockedOutAsync itself in newer Identity versions —
// this project's own `PasswordSignInAsync(..., lockoutOnFailure: true)` call already exercises
// this path correctly (D-09's claim about the interaction is directionally correct: an account
// with LockoutEnabled = false will never report IsLockedOut = true regardless of LockoutEnd,
// because SignInManager checks GetLockoutEnabledAsync before consulting LockoutEnd). No code
// change needed to preserve this — just don't touch LockoutEnabled anywhere in this phase's work.
```

## State of the Art

No frameworks or approaches have changed here — ASP.NET Core Identity's lockout/security-stamp mechanism has been stable since ASP.NET Core 2.0 and is unchanged in the app's current `net10.0` target. Nothing to report as "old vs. current approach" for this phase.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `UserManager.IsLockedOutAsync`'s exact short-circuit mechanics (whether the `LockoutEnabled` check happens inside `IsLockedOutAsync` itself or in `SignInManager.CheckLockoutAsync` upstream of it) — described in Code Examples above as `[ASSUMED]` framework-internals detail, not verified against this exact .NET 10 Identity source build | Common Pitfalls #1, Code Examples | Low — the externally observable behavior (an account with `LockoutEnabled = false` never reports as locked out via `PasswordSignInAsync`) is what actually matters for D-09, and that IS confirmed by this project's own pre-existing `EnableLockoutForExistingUsers` migration comment and `Program.cs`'s `AllowedForNewUsers = true` setting, which only make sense if this behavior holds. The exact call-stack location of the check is immaterial to the plan. |
| A2 | Recommended new `IIdentityService` method names (`DisableUserAsync`, `EnableUserAsync`, `GetLockoutEndAsync`) | Architecture Patterns #2, Code Examples | None — CONTEXT.md explicitly leaves exact naming to Claude's discretion; these are suggestions the planner is free to rename |
| A3 | Recommended self-disable guard implementation via `ClaimTypes.NameIdentifier` claim comparison in the new `UsersController.Disable` action | Architecture Patterns #3 | Low — this is one reasonable implementation; the planner should verify how the current signed-in user's id is resolved elsewhere in the codebase (e.g. `identityService.GetUserIdAsync(User)` already exists and is arguably more consistent with existing conventions than a raw claims lookup — prefer that existing method instead) |

**If this table is empty:** N/A — see entries above. All core CONTEXT.md decisions (D-01 through D-13) were verified directly against source and are NOT included in this table because they carry HIGH confidence, not ASSUMED status.

## Open Questions

1. **Should `UsersController.Disable`'s self-guard use `identityService.GetUserIdAsync(User)` instead of a raw claims lookup?**
   - What we know: `IIdentityService.GetUserIdAsync(ClaimsPrincipal user)` already exists (`QuestBoard.Repository/IdentityService.cs:66-70`) and is the established way other controllers resolve "who is the current user" (e.g. `AccountController.Profile`/`Edit` via `userService.GetUserAsync(User)`).
   - What's unclear: Nothing really — this is a preference, not a gap. Using the existing method is more consistent.
   - Recommendation: Planner should prefer `await identityService.GetUserIdAsync(User)` (or the `IUserService` equivalent) over a raw `ClaimTypes.NameIdentifier` claims lookup, for consistency with the rest of the codebase.

2. **Exact route/action naming for Disable/Enable (`Disable`/`Enable` vs. single `ToggleLockout`) — explicitly left to Claude's discretion in CONTEXT.md.**
   - What we know: Both are equally valid; existing `PromoteToAdmin`/`DemoteFromAdmin`-style verb-pair naming in `AdminController` slightly favors two distinct named actions (`Disable`/`Enable`) over one toggle action, for consistency with that established pattern.
   - What's unclear: Nothing blocking — just a style call.
   - Recommendation: Use two separate `[HttpPost] Disable(int userId)` / `Enable(int userId)` actions, mirroring the `PromoteToAdmin`/`DemoteFromAdmin` verb-pair convention already established in `AdminController`.

## Environment Availability

Skipped — this phase has no external tool/service dependencies beyond the already-running SQL Server and the app's own build toolchain, both already verified functional by every prior phase in this milestone (Phases 38-40 completed successfully in the same environment).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (confirmed via `TestContext.Current.CancellationToken` usage pattern in existing tests) + `Microsoft.AspNetCore.Mvc.Testing` `WebApplicationFactory` |
| Config file | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` (existing, no changes needed) |
| Quick run command | `dotnet test --filter FullyQualifiedName~AdminControllerIntegrationTests` (or `~AccountControllerIntegrationTests`) |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SAFE-01 | `DeleteUser` removes group membership only, account/other-memberships/characters/DM-profile untouched, no `DbUpdateException` even for a user with quest/shop/transaction/trade/reminder history | integration | `dotnet test --filter FullyQualifiedName~AdminControllerIntegrationTests` | ❌ Wave 0 — no existing `DeleteUser_Post_*` test; needs new test seeding a user with quest/shop/transaction rows, then asserting 200 OK + membership gone + account/characters/other-group-membership still present |
| SAFE-02 | SuperAdmin can disable an account via new `UsersController.Disable`; no data deleted; `IsLockedOutAsync`/login rejects disabled account | integration | `dotnet test --filter FullyQualifiedName~UsersControllerIntegrationTests` (new file) | ❌ Wave 0 — new test file needed; no `UsersController` or its tests exist yet |
| SAFE-03 | SuperAdmin can re-enable a disabled account; login succeeds again after Enable | integration | same new test file | ❌ Wave 0 |
| SAFE-04 | Disabled-account login shows exact "This account has been disabled..." copy, NOT the 15-minute copy; a real 5-failed-attempts lockout still shows the original 15-minute copy | integration | `dotnet test --filter FullyQualifiedName~AccountControllerIntegrationTests` | ❌ Wave 0 — needs 2 new tests: one seeding `LockoutEnd = DateTimeOffset.MaxValue` directly via `UserManager` in test setup, one exercising the existing 5-failed-attempt path (if not already covered — grep found no existing test asserting the literal 15-minute message string) |

### Sampling Rate
- **Per task commit:** targeted `dotnet test --filter FullyQualifiedName~{TestClass}`
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` — add `DeleteUser_Post_*` tests covering SAFE-01 (group-only removal + no-throw with quest/shop/transaction history)
- [ ] `QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs` (new file, Platform area) — covers SAFE-02/SAFE-03 (Disable/Enable actions, self-disable guard D-07, peer-SuperAdmin-allowed D-08)
- [ ] `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` — add `Login_Post_DisabledAccount_ShowsDisabledMessage` and confirm/add a test for the ordinary 15-minute-lockout message wording (SAFE-04)
- [ ] Reuse existing `AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync` (`QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs:164-171`) for all new SuperAdmin-authenticated test clients — no new test helper needed
- [ ] Reuse existing `factory.Services.CreateScope()` + `UserManager<UserEntity>` resolution pattern (seen in `AccountControllerIntegrationTests.cs:508-513`) for asserting `LockoutEnd`/`SecurityStamp` state directly against the DB in new tests

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | Reuses existing ASP.NET Core Identity `PasswordSignInAsync`/lockout mechanism — no new auth surface introduced |
| V3 Session Management | yes | `SecurityStampValidator` re-validation interval (D-11) directly controls how quickly a revoked/disabled session is force-expired — shortening it from 30 to 5 minutes is a session-management hardening, not a weakening |
| V4 Access Control | yes | New `UsersController` gated by `[Authorize(Policy = "SuperAdminOnly")]` at class level, matching the existing `GroupController` pattern exactly; self-disable guard (D-07) is an additional business-logic access control on top |
| V5 Input Validation | yes | `[ValidateAntiForgeryToken]` on all new POST actions (`Disable`, `Enable`), matching every other mutating action in this codebase |
| V6 Cryptography | no | No new cryptographic material — `SecurityStamp` regeneration uses Identity's existing `IPersonalDataProtector`/RNG internals, untouched by this phase |

### Known Threat Patterns for ASP.NET Core Identity + MVC

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| CSRF on Disable/Enable POST actions | Tampering | `[ValidateAntiForgeryToken]` — already the established convention on every mutating action in this codebase; must be applied to the 2 new actions |
| Privilege escalation via self-disable-then-recovery-race | Elevation of Privilege | D-07's self-disable guard (compare target `userId` to current user's own id) prevents a SuperAdmin from accidentally locking themselves out with no other SuperAdmin able to recover — this is a business-continuity control, not a classic security vulnerability, but is treated as access-control hardening here |
| Enumeration via differing lockout/disabled messages | Information Disclosure | Not applicable here — the disabled-vs-lockout distinction is intentionally shown to a user who has ALREADY authenticated with correct-or-attempted credentials (the message only fires post-`IsLockedOut`, meaning the account is real); this is not a login-enumeration vector since both messages already confirm account existence today (`"Invalid login attempt."` vs. lockout messages already differ, an existing, accepted pattern in this codebase, not introduced by this phase) |
| Stale session after account disable | Tampering / Elevation of Privilege | D-10 (`UpdateSecurityStampAsync`) + D-11 (5-minute `ValidationInterval`) together bound the window during which a disabled user's already-issued cookie remains valid to a maximum of ~5 minutes — an accepted, deliberate tradeoff per the discussion log, not a gap |

## Sources

### Primary (HIGH confidence)
- Direct source inspection: `QuestBoard.Domain/Interfaces/IGroupService.cs`, `IGroupRepository.cs`, `QuestBoard.Domain/Services/GroupService.cs`, `QuestBoard.Repository/GroupRepository.cs` — confirmed `RemoveMemberAsync` signature and no-throw behavior
- Direct source inspection: `QuestBoard.Service/Controllers/Admin/AdminController.cs` (full file, 488 lines) — confirmed `DeleteUser` current implementation, `groupService` already in constructor
- Direct source inspection: `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (full file, 276 lines) — confirmed `RemoveMember` action and header-bar/`Index` view conventions
- Direct source inspection: `QuestBoard.Repository/IdentityService.cs`, `QuestBoard.Domain/Interfaces/IIdentityService.cs` (full files) — confirmed thin-wrapper method style and existing `GetIdByEmailAsync`/`GetUserIdAsync` methods
- Direct source inspection: `QuestBoard.Service/Controllers/Admin/AccountController.cs` (full file, 304 lines) — confirmed `Login` POST current `IsLockedOut` branch (lines 137-141) and existing `identityService` constructor injection
- Direct source inspection: `QuestBoard.Service/Program.cs` (full file) — confirmed no existing `SecurityStampValidatorOptions` config; confirmed `Lockout.MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 min`, `AllowedForNewUsers = true` (lines 57-60)
- Direct source inspection: `QuestBoard.Repository/Entities/QuestBoardContext.cs` (`OnModelCreating`, lines 41-245) — confirmed full FK inventory for all `UserEntity`-referencing relationships, including the 2 `Cascade` FKs (`CharacterEntity.OwnerId`, `DungeonMasterProfileEntity`) not listed in CONTEXT.md, plus `PlayerSignupEntity.PlayerId` (also `Cascade`, also not listed)
- Direct source inspection: `QuestBoard.Repository/Migrations/20260420142117_EnableLockoutForExistingUsers.cs` — confirmed migration content matches CONTEXT.md's description exactly
- Direct source inspection: `QuestBoard.Domain/Models/User.cs` — confirmed no `LockoutEnd` field on domain model
- Direct source inspection: `QuestBoard.Repository/Automapper/EntityProfile.cs:39` — confirmed `CreateMap<UserEntity, User>()` has no `LockoutEnd` mapping; also confirmed actual file location differs from CLAUDE.md's documented path
- Direct source inspection: `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs`, `AccountControllerIntegrationTests.cs`, `Helpers/AuthenticationHelper.cs` — confirmed existing test patterns, `CreateAuthenticatedSuperAdminClientAsync` helper, no existing `GroupControllerIntegrationTests.cs`
- [SecurityStampValidatorOptions.ValidationInterval Property (Microsoft.AspNetCore.Identity)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.securitystampvalidatoroptions.validationinterval?view=aspnetcore-9.0) — confirmed default (30 min) and `Configure<SecurityStampValidatorOptions>` configuration API

### Secondary (MEDIUM confidence)
- [Configure ASP.NET Core Identity | Microsoft Learn](https://learn.microsoft.com/en-aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0) — general Identity configuration reference, net10.0-versioned page confirms API stability into the app's target framework

### Tertiary (LOW confidence)
- None — all claims either verified directly against source or cited to official Microsoft Learn docs.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all APIs already in use or documented via official Microsoft Learn source
- Architecture: HIGH — every controller/service/view file involved was read directly; conventions confirmed by exact line citations
- Pitfalls: HIGH — FK cascade behavior confirmed by direct `OnModelCreating` inspection (superset of CONTEXT.md's claims, not a contradiction); `SecurityStampValidator` interaction confirmed via official docs

**Research date:** 2026-07-04
**Valid until:** 30 days (stable framework APIs, no fast-moving dependencies in this phase)
