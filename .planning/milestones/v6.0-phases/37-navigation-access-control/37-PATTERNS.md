# Phase 37: Navigation & Access Control - Pattern Map

**Mapped:** 2026-07-03
**Files analyzed:** 9 (2 new interface/service extensions, 2 layout edits, 2 controller edits, 1 view edit, 1 Program.cs edit, 2 test files)
**Analogs found:** 9 / 9 (all files have exact in-repo precedent — this phase is "generalize an existing pattern to a new call site," per RESEARCH.md)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|---------------|
| `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` | interface (Domain) | request-response | itself (existing file, extend in place) | exact |
| `QuestBoard.Service/Services/ActiveGroupContextService.cs` | service | request-response | `QuestController.GetActiveBoardTypeAsync` (`QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:1022-1031`) | exact |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` | component (Razor partial) | request-response | itself (existing file, edit in place) — nested `@if` gating pattern already used for Admin/DM dropdowns | exact |
| `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` | component (Razor partial) | request-response | itself (existing file, edit in place) — mirrors desktop layout's `@if` gating | exact |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` (`EmailStats` action) | controller | request-response | itself — method-level `[Authorize]` addition; pattern precedent is class-level policy attribute already on the controller | exact |
| `QuestBoard.Service/Controllers/Admin/AccountController.cs` (new `AccessDenied` action) | controller | request-response | `Login`/`ForgotPassword` GET actions in the same file (`AccountController.cs:18-29`) — simple `[HttpGet] [AllowAnonymous] IActionResult -> View()` shape | exact |
| `QuestBoard.Service/Views/Shared/AccessDenied.cshtml` | component (Razor view) | request-response | itself (existing, orphaned — generalize copy in place) | exact |
| `QuestBoard.Service/Program.cs` (`ConfigureApplicationCookie` addition) | config | request-response | adjacent `AddAuthorizationBuilder`/`AddPolicy` block (`Program.cs:76-82`) — same file, same configuration-builder region | exact |
| `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` | test infra (test double) | request-response | itself (existing file, add settable property) | exact |
| `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (new) | test | request-response | `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` (`GetWithUserAgentAsync` helper + `html.Should().Contain/NotContain` assertions) | exact |
| `AdminControllerIntegrationTests.cs` (new `EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected` test) | test | request-response | existing `EmailStats_WhenNotAdmin_ShouldReturnForbidden` (same file, immediately above) | exact |

## Pattern Assignments

### `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` (interface, request-response)

**Analog:** itself (current full contents, 11 lines — extend, don't replace)

**Current full contents:**
```csharp
namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Provides the active group ID for the current request or execution context.
/// Null means "see all records".
/// </summary>
public interface IActiveGroupContext
{
    int? ActiveGroupId { get; }
}
```

**Required change:** Add an **async method** (not a synchronous property — see Shared Patterns > Async BoardType pitfall below):
```csharp
Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default);
```
Needs `using QuestBoard.Domain.Enums;` added to the top of the file. `ActiveGroupId` stays a synchronous property, unchanged.

---

### `QuestBoard.Service/Services/ActiveGroupContextService.cs` (service, request-response)

**Analog:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:1022-1031` (`GetActiveBoardTypeAsync`) — this is the exact logic to lift into the service, per RESEARCH.md Pattern 1.

**Imports pattern** (current file, lines 1-5):
```csharp
using Microsoft.AspNetCore.Http;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Constants;

namespace QuestBoard.Service.Services;
```
Add `using QuestBoard.Domain.Enums;` (for `BoardType`) and inject `IGroupService groupService` via the primary constructor.

**Current full contents (11-36), to extend not replace:**
```csharp
public class ActiveGroupContextService(IHttpContextAccessor httpContextAccessor) : IActiveGroupContext
{
    private int? _overriddenGroupId;
    private bool _groupIdOverridden;

    public int? ActiveGroupId =>
        _groupIdOverridden
            ? _overriddenGroupId
            : httpContextAccessor.HttpContext?.Session?.GetInt32(SessionKeys.ActiveGroupId);

    public void SetGroupId(int? groupId)
    {
        _groupIdOverridden = true;
        _overriddenGroupId = groupId;
    }
}
```
Constructor becomes `ActiveGroupContextService(IHttpContextAccessor httpContextAccessor, IGroupService groupService) : IActiveGroupContext`.

**Core pattern to port verbatim** (from `QuestController.cs:1022-1031`, the method being generalized):
```csharp
private async Task<BoardType> GetActiveBoardTypeAsync(CancellationToken token = default)
{
    if (activeGroupContext.ActiveGroupId is not { } groupId)
    {
        return BoardType.OneShot;
    }

    var group = await groupService.GetByIdAsync(groupId, token);
    return group?.BoardType ?? BoardType.OneShot;
}
```
**Important divergence:** the new interface member returns `BoardType?` (nullable), not `BoardType` defaulting to `OneShot` — because D-03 requires the "no active group" case to be indistinguishable from Campaign for nav-hiding purposes (`null` → hidden), whereas `QuestController`'s existing per-action usage defaults to `OneShot` for a different reason (form defaults). Do not copy the `?? BoardType.OneShot` fallback into the new service method — return `null` when `ActiveGroupId` is null or the group lookup returns null:
```csharp
public async Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default)
{
    if (ActiveGroupId is not { } groupId) return null;
    var group = await groupService.GetByIdAsync(groupId, token);
    return group?.BoardType;
}
```
(`IGroupService.GetByIdAsync(int, CancellationToken)` is inherited from `IBaseService<Group>` — `QuestBoard.Domain/Interfaces/IBaseService.cs:23` — no new repository method needed.)

**DI registration note:** `Program.cs:200-214` has a hand-written comment warning that `IActiveGroupContext` registration must stay in sync across two call sites (Scoped registration + a factory lambda) — read that block before editing; do not blindly `AddScoped<IActiveGroupContext, ActiveGroupContextService>()` if the existing registration uses a factory pattern for Hangfire-job compatibility.

---

### `QuestBoard.Service/Views/Shared/_Layout.cshtml` (component, request-response)

**Analog:** itself — current file already has the exact `@if`-nesting convention to replicate for the new BoardType gate.

**Imports/inject pattern** (lines 1-3, current):
```csharp
@using QuestBoard.Domain.Interfaces
@using QuestBoard.Service.Constants
@inject Microsoft.AspNetCore.Http.IHttpContextAccessor HttpContextAccessor
```
Add `@using QuestBoard.Domain.Enums` and `@inject IActiveGroupContext ActiveGroupContext`.

**Existing nested `@if` convention to mirror** (Admin dropdown, lines 40-72 — same shape as the SuperAdmin-only Email Stats gate you're adding):
```csharp
@if ((await AuthorizationService.AuthorizeAsync(User, "AdminOnly")).Succeeded)
{
    <li class="nav-item dropdown">
        ...
        <ul class="dropdown-menu">
            ...
            @if (User.IsInRole("SuperAdmin"))
            {
            <li>
                <a class="dropdown-item" href="/hangfire">
                    <i class="fas fa-tasks me-2"></i>Background Jobs
                </a>
            </li>
            }
            <li>
                <a class="dropdown-item" asp-controller="Admin" asp-action="EmailStats">
                    <i class="fas fa-envelope-open-text me-2"></i>Email Stats
                </a>
            </li>
        </ul>
    </li>
}
```
**Change required:** wrap the existing Email Stats `<li>` (lines 65-69) in `@if (User.IsInRole("SuperAdmin"))` — same guard already used one block above for "Background Jobs" (line 57). Do not change the Admin dropdown's own outer gate.

**DM dropdown items needing the OneShot allowlist gate** (lines 74-100 — "Manage Shop" line 87-91, "Edit My Profile" line 92-96, nested inside existing `DungeonMasterOnly` `@if`):
```csharp
@if ((await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded)
{
    <li class="nav-item dropdown">
        ...
        <ul class="dropdown-menu">
            <li>
                <a class="dropdown-item" asp-controller="Quest" asp-action="Create">
                    <i class="fas fa-scroll me-2"></i>Create Quest
                </a>
            </li>
            <li>
                <a class="dropdown-item" asp-controller="ShopManagement" asp-action="Index">
                    <i class="fas fa-coins me-2"></i>Manage Shop
                </a>
            </li>
            <li>
                <a class="dropdown-item" asp-controller="DungeonMaster" asp-action="EditProfile">
                    <i class="fas fa-user-edit me-2"></i>Edit My Profile
                </a>
            </li>
        </ul>
    </li>
}
```
Wrap "Manage Shop" and "Edit My Profile" `<li>` blocks each in `@if (activeBoardType == BoardType.OneShot) { ... }` — "Create Quest" stays ungated (out of NAV-01..06 scope).

**Nav items outside dropdowns needing the gate** (lines 102-123 — Shop line 103-107, Players line 118-122; Guild Members line 113-117 and Quest Log line 108-112 are D-05 — leave unchanged):
```csharp
@* Available to all authenticated users *@
<li class="nav-item">
    <a class="nav-link" asp-controller="Shop" asp-action="Index">
        <i class="fas fa-store me-1"></i>Shop
    </a>
</li>
<li class="nav-item">
    <a class="nav-link" asp-controller="QuestLog" asp-action="Index">
        <i class="fas fa-book-open me-1"></i>Quest Log
    </a>
</li>
<li class="nav-item">
    <a class="nav-link" asp-controller="GuildMembers" asp-action="Index">
        <i class="fas fa-people-group me-1"></i>Guild Members
    </a>
</li>
<li class="nav-item">
    <a class="nav-link" asp-controller="Players" asp-action="Index">
        <i class="fas fa-users me-1"></i>Players
    </a>
</li>
```
Wrap Shop and Players individually (not QuestLog/GuildMembers) in the OneShot allowlist gate.

**Calendar — the D-04 + NAV-01 combined fix** (currently lines 126-131, rendered UNCONDITIONALLY in the `ms-auto` group, outside the `IsAuthenticated` block that wraps the `me-auto` group):
```csharp
<ul class="navbar-nav ms-auto">
    <li class="nav-item">
        <a class="nav-link" asp-controller="Calendar" asp-action="Index">
            <i class="fas fa-calendar-alt me-1"></i>Calendar
        </a>
    </li>
    @if (User.Identity?.IsAuthenticated == true)
    {
        ...
```
**Required fix:** nest Calendar's `<li>` inside `@if (User.Identity?.IsAuthenticated == true)` (D-04) AND inside `@if (activeBoardType == BoardType.OneShot)` (NAV-01) — both conditions, order doesn't matter functionally but nest IsAuthenticated outer to match the surrounding block's existing structure. This moves Calendar's markup into the same `@if` block as the profile dropdown, immediately preceding it.

**Where to compute `activeBoardType` once per render** (place near the top of the nav render, e.g. right after the opening `<nav>` or right before first use — RESEARCH.md's Code Examples section shape):
```csharp
@{
    var activeBoardType = await ActiveGroupContext.GetBoardTypeAsync();
}
@if (activeBoardType == BoardType.OneShot)
{
    ...
}
```
Compute it once, reuse the local variable for all 5 gates — do not call `GetBoardTypeAsync()` five separate times in the same render.

---

### `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` (component, request-response)

**Analog:** itself — structurally near-identical to `_Layout.cshtml` but as a flat offcanvas list (no dropdowns), same `@if` gating convention.

**Imports/inject pattern** (lines 1-3, current — identical to desktop):
```csharp
@using QuestBoard.Domain.Interfaces
@using QuestBoard.Service.Constants
@inject Microsoft.AspNetCore.Http.IHttpContextAccessor HttpContextAccessor
```
Same additions as desktop: `@using QuestBoard.Domain.Enums` + `@inject IActiveGroupContext ActiveGroupContext`.

**DM-only items needing the gate** (lines 67-85, flat list — no nested dropdown, so gating is a direct `@if` wrap around each `<li>`):
```csharp
@if ((await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded)
{
    <li class="nav-item">
        <a class="nav-link" asp-controller="Quest" asp-action="Create">
            <i class="fas fa-scroll me-2"></i>Create Quest
        </a>
    </li>
    <li class="nav-item">
        <a class="nav-link" asp-controller="ShopManagement" asp-action="Index">
            <i class="fas fa-coins me-2"></i>Manage Shop
        </a>
    </li>
    <li class="nav-item">
        <a class="nav-link" asp-controller="DungeonMaster" asp-action="EditProfile">
            <i class="fas fa-user-edit me-2"></i>Edit My Profile
        </a>
    </li>
}
```
Gate "Manage Shop" and "Edit My Profile" individually — "Create Quest" stays ungated.

**Items outside the DM block** (lines 87-107 — Shop line 88-92, Players line 103-107; Quest Log line 93-97 and Guild Members line 98-102 are D-05, unchanged):
```csharp
@* Available to all authenticated users *@
<li class="nav-item">
    <a class="nav-link" asp-controller="Shop" asp-action="Index">
        <i class="fas fa-store me-2"></i>Shop
    </a>
</li>
```
Same allowlist wrap as desktop.

**Calendar — currently unconditional, structurally separate** (lines 110-115, with an explicit comment flagging the pre-existing "available to all" intent):
```csharp
@* Calendar — available to all *@
<li class="nav-item">
    <a class="nav-link" asp-controller="Calendar" asp-action="Index">
        <i class="fas fa-calendar-alt me-2"></i>Calendar
    </a>
</li>
```
This sits OUTSIDE both the earlier `@if (User.Identity?.IsAuthenticated == true) { ... }` block (which closes at line 108) and the later one that starts at line 118 for the profile section. **Required fix:** move/wrap this `<li>` so it's gated by `User.Identity?.IsAuthenticated == true && activeBoardType == BoardType.OneShot` — matching D-04 + NAV-01 exactly as in the desktop layout. Update or remove the now-inaccurate `@* Calendar — available to all *@` comment since it will no longer be true.

---

### `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `EmailStats` (controller, request-response)

**Analog:** itself — class declaration + existing `EmailStats` action signature.

**Class-level policy** (line 20-21, unchanged):
```csharp
[Authorize(Policy = "AdminOnly")]
public class AdminController(IUserService userService, IQuestService questService, IIdentityService identityService, IBackgroundJobClient jobClient, IOptions<EmailSettings> emailOptions, IMemoryCache cache, IActiveGroupContext activeGroupContext, ILogger<AdminController> logger, PartitionedRateLimiter<int> emailResendLimiter, ResendStatsClient resendStatsClient) : Controller
```

**Existing action signature** (line 357-358, to receive the new attribute directly above it):
```csharp
[HttpGet]
public async Task<IActionResult> EmailStats(bool force = false, CancellationToken token = default)
```

**Required change:**
```csharp
[HttpGet]
[Authorize(Policy = "SuperAdminOnly")]
public async Task<IActionResult> EmailStats(bool force = false, CancellationToken token = default)
```
**CRITICAL — do not describe this as an "override."** Per RESEARCH.md Pitfall 2 (verified against `learn.microsoft.com/aspnet/core/security/authorization/policies`), ASP.NET Core ANDs method-level and class-level `[Authorize]` policies — both `AdminOnly` and `SuperAdminOnly` must pass. This produces the correct behavior here only because every SuperAdmin already passes `AdminOnly` via `AdminHandler`'s role-claim bypass — it is not a replacement/narrowing mechanism in general.

`SuperAdminOnly` policy is already defined at `Program.cs:81-82` — no new policy code needed:
```csharp
.AddPolicy("SuperAdminOnly", policy =>
    policy.RequireRole("SuperAdmin"));
```

---

### `QuestBoard.Service/Controllers/Admin/AccountController.cs` — new `AccessDenied` action (controller, request-response)

**Analog:** `Login`/`ForgotPassword` GET actions in the same file (lines 18-29) — simplest existing shape for an unauthenticated-accessible GET-only action returning a bare view.

**Imports pattern** (lines 1-14, current file — no change needed, `Microsoft.AspNetCore.Authorization` for `[AllowAnonymous]` is already imported):
```csharp
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Controllers.QuestBoard;
using QuestBoard.Service.Jobs;
using QuestBoard.Service.ViewModels.AccountViewModels;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Text;

namespace QuestBoard.Service.Controllers.Admin;

public class AccountController(IUserService userService, IIdentityService identityService, IBackgroundJobClient jobClient, ILogger<AccountController> logger, IActiveGroupContext activeGroupContext) : Controller
```
**Note:** class has no `[Authorize]` at class level (confirmed) — actions opt in individually via `[Authorize]` attributes on `Profile`/`Edit`/`ChangePassword`. `AccessDenied` needs no `[AllowAnonymous]` override since there's no class-level policy to override, but RESEARCH.md's Pattern 2 code example includes it explicitly for clarity/documentation of intent — follow that.

**Core pattern to copy verbatim** (lines 18-23, `Login` GET — the exact shape needed):
```csharp
[HttpGet]
public IActionResult Login(string? returnUrl = null)
{
    ViewData["ReturnUrl"] = returnUrl;
    return View();
}
```

**New action (per RESEARCH.md Pattern 2/Code Examples):**
```csharp
[HttpGet]
[AllowAnonymous]
public IActionResult AccessDenied()
{
    return View();
}
```
Place it anywhere among the other bare GET actions (e.g., near `Login`/`ForgotPassword`).

**View resolution:** Since no `Views/Account/AccessDenied.cshtml` exists, MVC's fallback view search resolves to `Views/Shared/AccessDenied.cshtml` automatically — verify this in dev; if ambiguous, `return View("~/Views/Shared/AccessDenied.cshtml");` is the safe explicit fallback (RESEARCH.md Assumption A1).

---

### `QuestBoard.Service/Views/Shared/AccessDenied.cshtml` (component, request-response)

**Analog:** itself — already modern-card-compliant, only the copy needs generalizing (per RESEARCH.md Pattern 3 / CLAUDE.md UI convention — do not rewrite structurally).

**Current full contents (50 lines) — structure to KEEP as-is:**
```html
@using QuestBoard.Domain.Interfaces
@{
    ViewData["Title"] = "Access Denied";
}

<div class="row justify-content-center">
    <div class="col-md-6">
        <div class="card modern-card">
            <div class="card-header modern-card-header">
                <h2 class="mb-0">
                    <i class="fas fa-shield-alt me-2"></i>
                    Access Denied
                </h2>
            </div>
            <div class="card-body modern-card-body text-center">
                <div class="mb-4">
                    <i class="fas fa-crown fa-3x text-warning mb-3"></i>
                    <h4>Dungeon Master Access Required</h4>
                    <p class="text-muted">
                        This page is restricted to Dungeon Masters only.
                        You need to have DM privileges to access this content.
                    </p>
                </div>

                @if (User.Identity?.IsAuthenticated == true)
                {
                    <div class="alert alert-info">
                        <i class="fas fa-info-circle me-2"></i>
                        If you believe you should have access to this page, please contact an administrator.
                    </div>
                }
                else
                {
                    <div class="alert alert-warning">
                        <i class="fas fa-sign-in-alt me-2"></i>
                        Please <a asp-controller="Account" asp-action="Login" class="alert-link">log in</a>
                        with a Dungeon Master account to access this page.
                    </div>
                }

                <div class="d-grid gap-2 d-md-block">
                    <a asp-controller="Home" asp-action="Index" class="btn btn-warning">
                        <i class="fas fa-home me-2"></i>
                        Back to Quest Board
                    </a>
                </div>
            </div>
        </div>
    </div>
</div>
```
**Required copy changes (surgical, not structural):**
- `<i class="fas fa-crown ...">` and `<h4>Dungeon Master Access Required</h4>` → generalize to a policy-agnostic icon/heading (e.g., `fa-shield-alt` or `fa-ban`, "Access Denied" / "You don't have permission to view this page")
- The paragraph "This page is restricted to Dungeon Masters only..." → generalize similarly (no policy-specific wording)
- The unauthenticated `alert-warning` branch's "...with a Dungeon Master account..." → generalize to remove the DM-specific phrase
- Keep `modern-card`/`modern-card-header`/`modern-card-body`, the `IsAuthenticated` branch structure, and the "Back to Quest Board" button unchanged — this already matches CLAUDE.md's UI convention and D-07's discretion note.

---

### `QuestBoard.Service/Program.cs` — `ConfigureApplicationCookie` addition (config, request-response)

**Analog:** adjacent `AddAuthorizationBuilder`/`.AddPolicy(...)` block (lines 76-82) — same file, same configuration region, good insertion point nearby.

**Current adjacent block** (lines 73-82, for placement context):
```csharp
// Add Authorization policies
builder.Services.AddScoped<IAuthorizationHandler, DungeonMasterHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AdminHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("DungeonMasterOnly", policy =>
        policy.Requirements.Add(new DungeonMasterRequirement()))
    .AddPolicy("AdminOnly", policy =>
        policy.Requirements.Add(new AdminRequirement()))
    .AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole("SuperAdmin"));
```

**New code to add** (per RESEARCH.md Pattern 2 — place near this block or near the `AddIdentity<...>()` call at lines 44-63, before `builder.Build()`):
```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Account/AccessDenied";
});
```
**No existing `AddCookie`/`ConfigureApplicationCookie` call exists anywhere in `Program.cs` today** (verified) — this is net-new configuration, not an override of anything. This is intentionally **app-wide** per D-07 — every `AdminOnly`/`DungeonMasterOnly`/`SuperAdminOnly` failure across the whole app now redirects here instead of 404ing (approved wider blast radius, not scope creep).

---

### `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` (test infra, request-response)

**Analog:** itself — existing test double, extend with a settable property mirroring `ActiveGroupId`'s existing shape.

**Current full contents (14 lines):**
```csharp
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// Settable implementation of IActiveGroupContext for integration tests.
/// Defaults to GroupId = 1 (EuphoriaInn seed group). Tests override as needed.
/// Registered as Singleton in WebApplicationFactoryBase so test code can mutate it directly.
/// </summary>
public class MutableGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId { get; set; } = 1;
}
```

**Required change** (per RESEARCH.md Pattern 1's exact recommended code):
```csharp
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.IntegrationTests.Helpers;

public class MutableGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId { get; set; } = 1;
    public BoardType? BoardType { get; set; } = QuestBoard.Domain.Enums.BoardType.OneShot;

    public Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default) => Task.FromResult(BoardType);
}
```
**Important:** the interface member is `GetBoardTypeAsync()` (async method per the sync/async mismatch pitfall), but the test double should still expose a plain settable `BoardType` property for test code ergonomics (`context.BoardType = BoardType.Campaign;`), with `GetBoardTypeAsync()` just wrapping it in `Task.FromResult`. Default `OneShot` keeps all existing nav-visible tests green without modification (matches RESEARCH.md's explicit rationale).

---

### `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (new test, request-response)

**Analog:** `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` — reuse its `GetWithUserAgentAsync` helper and `IClassFixture<WebApplicationFactoryBase>` shape directly; this is the closest existing precedent for "assert on rendered nav HTML across desktop/mobile user agents."

**Class shape + fixture pattern** (`MobileViewsTests.cs:13-28`):
```csharp
public class MobileViewsTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly WebApplicationFactoryBase _factory;
    private readonly HttpClient _client;

    public MobileViewsTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }
```

**HTTP helper with User-Agent + optional auth header** (`MobileViewsTests.cs:30-55`):
```csharp
private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(string url, string userAgent)
{
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
    var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
    var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    return (response, html);
}

private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(
    string url, string userAgent, AuthenticationHeaderValue? authorization)
{
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
    if (authorization != null)
    {
        request.Headers.Authorization = authorization;
    }
    var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
    var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    return (response, html);
}
```

**Assertion style to follow** (e.g. `MobileViewsTests.cs:70-74`):
```csharp
response.StatusCode.Should().Be(HttpStatusCode.OK);
html.Should().Contain("quest-card-mobile");
html.Should().NotContain("fantasy-quest-card");
```

**How to vary BoardType per test:** since `MutableGroupContext` is registered as a Singleton (per `WebApplicationFactoryBase`), tests set `BoardType` directly via DI resolution before making the request — resolve it from `_factory.Services` (the same pattern other tests use `_factory.Services.GetRequiredService<...>()` for `QuestBoardContext`, e.g. `MobileViewsTests.cs:514`). Reset/set the value at the start of each test since the singleton persists across tests in the same class fixture (watch for test-order coupling — consider resetting to `OneShot` in each test's Arrange step rather than relying on a previous test's cleanup).

**Coverage needed** (from RESEARCH.md's Phase Requirements → Test Map): NAV-01 (Calendar hidden in Campaign, both UAs), NAV-02 (Shop hidden in Campaign), NAV-03 (Guild Members visible regardless — regression check), NAV-04 (Manage Shop hidden in Campaign, DM role), NAV-05 (Edit My Profile hidden in Campaign, DM role), NAV-06 (Players hidden in Campaign), D-04 (Calendar hidden for anonymous, unauthenticated client — no `authorization` param), ACCESS-01 nav-link (Email Stats link absent for Admin-not-SuperAdmin).

---

### `AdminControllerIntegrationTests.cs` — new `EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected` (test, request-response)

**Analog:** existing `EmailStats_WhenNotAdmin_ShouldReturnForbidden` (same file, immediately above the insertion point).

**Existing test to model from directly:**
```csharp
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
```
**New test — swap role to `Admin` (not `SuperAdmin`)** to specifically exercise the new AND-combined policy gap:
```csharp
[Fact]
public async Task EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected()
{
    var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
        _factory, "emailstatsadmin", "emailstatsadmin@example.com", roles: ["Admin"]);

    var response = await adminClient.GetAsync("/Admin/EmailStats", TestContext.Current.CancellationToken);

    response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
}
```
Per RESEARCH.md Pitfall 3: once `ConfigureApplicationCookie` is wired, this will manifest as a `302 Found` redirect to `/Account/AccessDenied` — already covered by the existing `BeOneOf(Forbidden, Redirect, Unauthorized)` assertion set without modification. Confirm this holds rather than assuming.

## Shared Patterns

### Authorization policy gating (existing, reused verbatim)
**Source:** `QuestBoard.Service/Program.cs:76-82`
**Apply to:** `AdminController.EmailStats` (method-level `SuperAdminOnly` addition), `_Layout.cshtml`/`_Layout.Mobile.cshtml` (Email Stats nav link gate uses `User.IsInRole("SuperAdmin")`, same check already used for "Background Jobs")
```csharp
.AddPolicy("SuperAdminOnly", policy =>
    policy.RequireRole("SuperAdmin"));
```
No new authorization code needed anywhere in this phase.

### Async BoardType accessor — sync/async mismatch pitfall (new, must be followed exactly)
**Source:** RESEARCH.md Pattern 1 / Anti-Patterns section
**Apply to:** `IActiveGroupContext`, `ActiveGroupContextService`, `MutableGroupContext`, both `_Layout*.cshtml` files
`ActiveGroupId` is a synchronous property (Session reads are synchronous, no I/O). `BoardType` requires a DB lookup via `IGroupService.GetByIdAsync`, which is inherently async. **Do not** expose it as a synchronous property backed by a blocking `.Result`/`.GetAwaiter().GetResult()` call — deadlock risk on Kestrel's synchronization context. Expose as:
```csharp
Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default);
```
and call with `@await ActiveGroupContext.GetBoardTypeAsync()` once per Razor render, cached in a local variable, not once per gated nav item.

### Nav allowlist gating shape — D-01 (new pattern, all 5 nav items + Calendar)
**Source:** RESEARCH.md Pattern 4 / Code Examples
**Apply to:** Both `_Layout.cshtml` and `_Layout.Mobile.cshtml`, for Calendar (NAV-01 + D-04), Shop (NAV-02), Manage Shop (NAV-04), Edit My Profile (NAV-05), Players (NAV-06)
```csharp
@if (activeBoardType == BoardType.OneShot)
{
    <li class="nav-item">
        <a class="nav-link" asp-controller="Players" asp-action="Index">
            <i class="fas fa-users me-1"></i>Players
        </a>
    </li>
}
```
**This is deliberately the OPPOSITE polarity** from the existing `boardType != BoardType.Campaign` checks used inside `QuestController`/`QuestLogController` views — those are blocklist-shaped and safe there because those views never render for an anonymous/no-group visitor. `_Layout.cshtml` renders unconditionally on every page including pre-login and pre-group-selection, which is exactly why D-01/D-03 mandate `== OneShot` (naturally evaluates false/hidden for `null`) rather than `!= Campaign` (would wrongly evaluate true/shown for `null`). Do not invert this.

### Error/access-denied redirect wiring (new, app-wide side effect approved by D-07)
**Source:** RESEARCH.md Pattern 2, Pitfall 3
**Apply to:** `Program.cs` only (single call site)
```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Account/AccessDenied";
});
```
This is the only shared cross-cutting change with an app-wide blast radius — every existing `AdminOnly`/`DungeonMasterOnly`/`SuperAdminOnly` failure elsewhere in the app (not just EmailStats) starts redirecting here instead of 404ing. This is expected and explicitly approved (D-07), not a regression to fix narrowly.

## No Analog Found

None — every file in this phase has a direct, concrete in-repo precedent (per RESEARCH.md's explicit framing: "The work is almost entirely 'generalize an existing pattern to a new call site,' not 'invent a new pattern.'").

## Metadata

**Analog search scope:** `QuestBoard.Domain/Interfaces/`, `QuestBoard.Service/Services/`, `QuestBoard.Service/Views/Shared/`, `QuestBoard.Service/Controllers/Admin/`, `QuestBoard.Service/Controllers/`, `QuestBoard.Service/Program.cs`, `QuestBoard.Service/Controllers/QuestBoard/`, `QuestBoard.IntegrationTests/Helpers/`, `QuestBoard.IntegrationTests/Mobile/`, `QuestBoard.IntegrationTests/Controllers/`
**Files scanned:** `IActiveGroupContext.cs`, `ActiveGroupContextService.cs`, `_Layout.cshtml`, `_Layout.Mobile.cshtml`, `AdminController.cs`, `AccountController.cs`, `AccessDenied.cshtml`, `Program.cs`, `GroupPickerController.cs`, `QuestController.cs`, `MutableGroupContext.cs`, `MobileViewsTests.cs`, `AdminControllerIntegrationTests.cs`
**Pattern extraction date:** 2026-07-03
