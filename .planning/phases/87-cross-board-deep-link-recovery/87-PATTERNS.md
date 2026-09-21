# Phase 87: Cross-Board Deep Link Recovery - Pattern Map

**Mapped:** 2026-09-21
**Files analyzed:** 9 new, 4 modified
**Analogs found:** 9 / 9

All analog paths below were verified tracked with `git ls-files` (present in `git status` clean
working tree; none are under `.gsd/` or any gitignored mirror).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/CrossBoardLinkRepository.cs` (new) | repository | CRUD (single membership-pinned projection) | `QuestBoard.Repository/EventSignupRepository.cs` (`GetFeedRowsForUserAsync`, lines 76-101) and `GroupRepository.cs` (lines 128-148) | exact (same `IgnoreQueryFilters()` + membership-pin shape) |
| `QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs` (new) | service (interface) | request-response | `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` (existing sibling interface — small, single-purpose) | role-match |
| `QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs` (new) | service | request-response | `QuestBoard.Service/Controllers/AgendaController.cs` (fresh-membership-read pattern) | role-match |
| `QuestBoard.Domain/Interfaces/IActiveBoardSwitcher.cs` (new) | service (interface) | event-driven (session write) | same as above | role-match |
| `QuestBoard.Domain/Services/ActiveBoardSwitcherService.cs` (new) | service | event-driven (session write) | `QuestBoard.Service/Controllers/GroupPickerController.cs` (`SelectGroup`, lines 44-61 — the three session writes being extracted) | exact (literal source of the extraction) |
| `QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs` (new) | middleware | request-response | `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (full file) | exact |
| `QuestBoard.Service/Controllers/GroupPickerController.cs` (modified — `Index`) | controller | request-response | itself, lines 14-40 (existing `groups.Count == 1` auto-select branch is the literal precedent) | exact |
| `QuestBoard.Service/Views/Shared/_Toasts.cshtml` (modified) | component (Razor partial) | request-response (SSR/TempData) | itself, `GoldReceived` block, lines 57-76 | exact |
| `QuestBoard.Service/Program.cs` (modified — pipeline registration) | config | request-response | itself, lines 315-325 (existing middleware registration block) | exact |
| `QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs` (new) | test (architecture allowlist) | batch (static file scan) | `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` (full file) | exact |
| `QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs` (new) | test (integration) | request-response | `QuestBoard.IntegrationTests/Mobile/MobileDetectionMiddlewareTests.cs` (structure only — note route-value caveat below) | role-match, with caveat |
| `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs` (modified, if exists) | test (integration) | request-response | itself (existing file — verify exact name during planning; `TESTING.md` confirms group-picker coverage exists) | exact if file exists |

## Pattern Assignments

### `QuestBoard.Repository/CrossBoardLinkRepository.cs` (repository, CRUD)

**Analog:** `QuestBoard.Repository/EventSignupRepository.cs` and `QuestBoard.Repository/GroupRepository.cs`

**Core membership-pinned projection pattern** (`EventSignupRepository.cs:76-101`):
```csharp
// Rooted at EventSignups rather than Events -- an event the caller holds no signup row
// on can never appear here... Scope is re-imposed immediately by memberGroupIds,
// supplied by the caller from a fresh membership read taken this same request.
var entities = await DbContext.EventSignups
    .IgnoreQueryFilters()
    .Where(es => es.UserId == userId
        && memberGroupIds.Contains(es.Event.GroupId)
        && es.Event.CancelledAt == null
        && es.Event.Date >= windowStart && es.Event.Date <= windowEnd)
    ...
```

**Simpler scalar-projection precedent** (`GroupRepository.cs:127-140`):
```csharp
// The ambient board filter answers for the caller's currently selected board, which is the
// wrong question for an operation that targets a board named by an explicit groupId
// argument. Scope is re-imposed immediately below by that same argument.
private async Task<List<int>> GetFutureEventIdsForGroupIgnoringActiveBoardAsync(int groupId, DateOnly today, CancellationToken token)
{
    return await DbContext.Events
        .IgnoreQueryFilters()
        .Where(e => e.GroupId == groupId && e.Date >= today)
        .Select(e => e.Id)
        .ToListAsync(token);
}
```

**D-10's exact required shape** (from RESEARCH.md, matching the two patterns above):
```csharp
public async Task<int?> ResolveQuestBoardIdAsync(int questId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token)
{
    return await DbContext.Quests
        .IgnoreQueryFilters()
        .Where(q => q.Id == questId && memberGroupIds.Contains(q.GroupId))
        .Select(q => (int?)q.GroupId)
        .FirstOrDefaultAsync(token);
}
```
One such method per lookup-kind (Quest, Event, Character, Contact, Shop, EventSeries, DungeonMaster-via-UserGroups). `QuestLog/Details` and `QuestLog/EditRecap` reuse the Quest projection (no separate entity — verified via `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs`).

**Comment style to copy:** every `IgnoreQueryFilters()` call site in this codebase carries a comment explaining *why* the bypass is safe and how scope is re-imposed — follow that convention exactly (see both excerpts above).

---

### `QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs` (middleware, request-response)

**Analog:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (read in full, 168 lines)

**Constructor / DI-resolution pattern** (lines 48-49, 99-105, 113):
```csharp
public class GroupSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }
        ...
        var groupContext = context.RequestServices.GetRequiredService<IActiveGroupContext>();
```
Services are resolved from `context.RequestServices` inside `InvokeAsync`, not injected into the constructor — this is because `IMiddleware`-style DI in this codebase resolves scoped services per-request. Copy this exact resolution pattern for `IActiveGroupContext`, `ICrossBoardLinkResolver`, `IActiveBoardSwitcher`, and `ITempDataDictionaryFactory`.

**GET/HEAD-only verb-check pattern** (lines 116-124, mirrors D-19's requirement):
```csharp
if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
{
    // Don't silently redirect a non-idempotent request...
    context.Response.StatusCode = StatusCodes.Status409Conflict;
    return;
}
```
The new middleware needs the same `HttpMethods.IsGet`/`IsHead` check but the "else" branch is "pass through unchanged" (D-19), not a redirect/409 — different consequence, same verb-check idiom.

**Session-write pattern being extracted into `ActiveBoardSwitcher`** (`GroupSessionMiddleware.cs:148-150`, mirrors `SelectGroup`):
```csharp
context.Session.Remove(SessionKeys.ActiveGroupId);
context.Session.Remove(SessionKeys.ActiveGroupName);
context.Session.Remove(SessionKeys.ActiveGroupValidatedAtUtc);
```
(the new switcher's `SwitchAsync` is the *positive* mirror — `SetInt32`/`SetString`/`SetString` for the same three keys, see `GroupPickerController.SelectGroup` below.)

**Docstring convention:** `GroupSessionMiddleware` carries a long structured `<summary>` numbering its guard order (lines 10-47) — follow this convention for `CrossBoardDeepLinkMiddleware`, numbering: exempt/anonymous → verb check (D-19) → Fetch Metadata gate (D-02) → registry lookup (D-09) → resolve (D-10) → switch-or-passthrough (D-06/D-07).

**No captured `ActiveGroupId`:** `QuestBoard.Repository/Entities/QuestBoardContext.cs:391-393` —
```csharp
// CRITICAL: Do NOT capture activeGroupContext.ActiveGroupId into a local var here.
//           That captures the value once (null at model-build time). Always reference the service.
```
Applies directly to the new middleware: read `groupContext.ActiveGroupId` live at each check point (before the switch, to capture "previous board", and never cached across the `SwitchAsync` call).

---

### `QuestBoard.Service/Controllers/GroupPickerController.cs` (controller, modified `Index`)

**Analog:** itself — the file already contains both patterns this change generalizes.

**Existing single-group auto-select precedent to generalize** (lines 31-37):
```csharp
if (!isSuperAdmin && groups.Count == 1)
{
    HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, groups[0].Id);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupName, groups[0].Name);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
    return RedirectToLocal(returnUrl);
}
```

**`SelectGroup`'s identical three-key write** (lines 57-59) — the literal source `ActiveBoardSwitcherService.SwitchAsync` must replace at all three call sites (`SelectGroup`, this `Index` branch, and the new middleware):
```csharp
HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
HttpContext.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
return RedirectToLocal(returnUrl);
```

**`RedirectToLocal`'s open-redirect guard** (lines 63-73), unchanged, reused by D-05's new branch:
```csharp
private IActionResult RedirectToLocal(string? returnUrl)
{
    if (Url.IsLocalUrl(returnUrl))
    {
        return Redirect(returnUrl);
    }
    else
    {
        return RedirectToAction("Index", "Quest");
    }
}
```

**Error handling / not-found pattern** (`SelectGroup`, lines 46-55) — the `NotFound()`-on-non-member shape the resolver's failure path should mirror conceptually (though the resolver itself never throws — it returns `null`):
```csharp
var group = await groupService.GetByIdAsync(groupId);
if (group == null) return NotFound();

var isSuperAdmin = User.IsInRole("SuperAdmin");
if (!isSuperAdmin)
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var role = await userService.GetGroupRoleByIdAsync(userId, groupId);
    if (role == null) return NotFound();
}
```

---

### `QuestBoard.Service/Views/Shared/_Toasts.cshtml` (component, modified)

**Analog:** itself — `GoldReceived` block (lines 57-76), the existing precedent for a toast carrying more than a plain string.

```cshtml
@if (TempData["GoldReceived"] != null)
{
    <div class="toast show gold-toast" role="alert" data-bs-autohide="true" data-bs-delay="6000">
        <div class="toast-header bg-warning text-dark">
            <i class="fas fa-coins me-2"></i>
            <strong class="me-auto">Gold Received!</strong>
            <button type="button" class="btn-close" data-bs-dismiss="toast"></button>
        </div>
        <div class="toast-body">
            <div class="d-flex align-items-center">
                <i class="fas fa-coins fa-2x text-warning me-3"></i>
                <div>
                    <strong class="fs-5">+@TempData["GoldReceived"] gp</strong>
                    <br>
                    <small class="text-muted">@TempData["Success"]</small>
                </div>
            </div>
        </div>
    </div>
}
```
The new `BoardSwitchNotice` block follows this exact shape (toast-header + toast-body + dismiss button), reading 2-3 TempData keys (target board name, previous group id, previous group name) instead of one, and — new for this codebase — embeds a `<form>` for the switch-back POST (see RESEARCH.md Pattern 4 for the drafted markup, which already follows this file's conventions and antiforgery-token usage; there is no other in-repo precedent for a form-carrying toast, so RESEARCH.md's example is the one to start from).

**No `.Mobile.cshtml` twin needed for this file** — confirmed both `_Layout.cshtml:250` and `_Layout.Mobile.cshtml:211` already render `<partial name="_Toasts" />` identically, so a single shared partial change covers both. This is the one file in this phase where the twin rule (`.claude/ui-guidelines.md`) is already satisfied by the existing structure, not something to newly apply.

---

### `QuestBoard.Service/Program.cs` (config, modified — middleware registration)

**Analog:** itself, lines 315-325 (current pipeline)
```csharp
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseMiddleware<MobileDetectionMiddleware>();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseMiddleware<GroupSessionMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();
```
**New line to insert**, directly after `GroupSessionMiddleware` and before `UseRateLimiter`/`UseAuthorization` (D-06/D-18 — placement before `UseAuthorization` is load-bearing, not stylistic):
```csharp
app.UseMiddleware<GroupSessionMiddleware>();
app.UseMiddleware<CrossBoardDeepLinkMiddleware>(); // NEW
app.UseRateLimiter();
app.UseAuthorization();
```

---

### `QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs` (test, architecture allowlist)

**Analog:** `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` (full file, 306 lines) — read completely, this is a direct structural mirror per D-11.

**Reusable machinery to copy verbatim (adapted):**
- `ResolveRepoRelativePath` (lines 103-123) — upward-walk file resolver from `AppContext.BaseDirectory`, no changes needed beyond copy.
- `StripComments` (lines 130-156) — strips `//`, `/* */`, and `@* *@` before scanning; copy verbatim.
- The closed-list + `[Theory]`/`MemberData` shape (lines 15-16, 171-174, 190-205) — swap `GuardedRelativePaths` (files allowed to contain the shape) for the new allowlist (files allowed to contain `IgnoreQueryFilters()`), and swap `AmbientReadShapes = ["DateTime.Today", "DateTime.Now", "DateTime.UtcNow"]` for `["IgnoreQueryFilters()"]`.

**Key difference to flag in the plan:** `AmbientClockSeamTests` asserts guarded files contain *none* of the banned shape (files are guarded against having it) — D-11's test inverts this: it must assert the shape *only ever* appears in the allowed files repo-wide (a full-repo scan for `IgnoreQueryFilters()`, then assert every hit's file is in the allowlist), not merely check each listed file individually. The listed 7 existing call sites plus the new `CrossBoardLinkRepository.cs` become the allowlist:
```csharp
private static readonly string[] AllowedCallSites =
[
    "QuestBoard.Repository/EventSignupRepository.cs",
    "QuestBoard.Repository/EventRepository.cs",
    "QuestBoard.Repository/GroupRepository.cs",
    "QuestBoard.Repository/QuestRepository.cs",
    "QuestBoard.Repository/CrossBoardLinkRepository.cs", // NEW for this phase
];
```

---

### `QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs` (test, integration)

**Analog:** `QuestBoard.IntegrationTests/Mobile/MobileDetectionMiddlewareTests.cs` — structural style only (bare `DefaultHttpContext` construction), **with a documented caveat**: that file's bare-context style does **not** populate route values, and this new middleware's behavior is entirely route-value-dependent (`context.GetRouteValue("controller"/"action"/"id")`). Per RESEARCH.md Pitfall 2, this test class needs `WebApplicationFactory`-based integration coverage (full HTTP round trip through real routing), not the bare-`DefaultHttpContext` unit-test shape `MobileDetectionMiddlewareTests` uses. Use whatever existing `WebApplicationFactory`-based base class other integration tests in the suite use (check `QuestBoard.IntegrationTests` base fixture during planning — not read this pass, but referenced throughout RESEARCH.md's Validation Architecture section, e.g. `AuthenticationHelper.CreateAuthenticatedDMClientAsync`).

**Header-gate test shape** (from RESEARCH.md, D-15 paired-parity pattern to copy for the identical-404 assertions):
```csharp
[Fact]
public async Task Details_NonMemberOfExistingBoard_AndNonexistentId_ProduceIdenticalResponses()
{
    var nonMemberResponse = await _client.GetAsync($"/Quest/Details/{existingQuestOnBoardAId}");
    var nonexistentResponse = await _client.GetAsync($"/Quest/Details/{int.MaxValue}");

    nonMemberResponse.StatusCode.Should().Be(nonexistentResponse.StatusCode);
    (await nonMemberResponse.Content.ReadAsStringAsync()).Should().Be(
        await nonexistentResponse.Content.ReadAsStringAsync());
    nonMemberResponse.Headers.Should().BeEquivalentTo(nonexistentResponse.Headers);
}
```

## Shared Patterns

### Session repointing — the three-key write
**Source:** `QuestBoard.Service/Controllers/GroupPickerController.cs:57-59` (also duplicated today at `:33-35`)
**Apply to:** `ActiveBoardSwitcherService.SwitchAsync` (the only place this write should exist after this phase), and every one of its three callers (`SelectGroup`, `GroupPickerController.Index`, `CrossBoardDeepLinkMiddleware`) must be refactored to call it instead of writing Session directly.
```csharp
HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
HttpContext.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
```
**Pitfall called out in RESEARCH.md:** omitting `ActiveGroupValidatedAtUtc` doesn't fail today's request — it silently forces `GroupSessionMiddleware`'s revalidation branch (`GroupSessionMiddleware.cs:131-135`) to fire on the very next request. Any code review finding this write outside `ActiveBoardSwitcherService`/`SelectGroup`/`GroupPickerController.Index` is a defect per RESEARCH.md's own stated warning sign.

### Live (never-captured) `IActiveGroupContext` reads
**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs:391-393`
**Apply to:** every new file reading `ActiveGroupId` — `CrossBoardDeepLinkMiddleware`, `CrossBoardLinkResolverService`. Never assign `groupContext.ActiveGroupId` to a local variable that outlives a call to the switcher.
```csharp
// CRITICAL: Do NOT capture activeGroupContext.ActiveGroupId into a local var here.
//           That captures the value once (null at model-build time). Always reference the service.
```

### `IgnoreQueryFilters()` comment convention
**Source:** `QuestBoard.Repository/EventSignupRepository.cs:76-90`, `GroupRepository.cs:127-140`
**Apply to:** every method in the new `CrossBoardLinkRepository.cs`. Every existing call site explains, in a comment directly above the query, (a) why the bypass is safe and (b) exactly how scope is re-imposed immediately after. This is a hard house convention, not merely style — `CrossBoardIgnoreQueryFiltersSeamTests` only checks *where* the call sites are, not that they're commented, so this is a code-review-level convention to carry forward regardless.

### GET/HEAD verb gating
**Source:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:116, 152`
**Apply to:** `CrossBoardDeepLinkMiddleware` (D-19) — same `HttpMethods.IsGet(...) && HttpMethods.IsHead(...)` idiom, different consequence (pass-through, not redirect/409).

### DI resolution inside middleware via `context.RequestServices`
**Source:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:113, 139-140`
**Apply to:** `CrossBoardDeepLinkMiddleware` for `IActiveGroupContext`, `ICrossBoardLinkResolver`, `IActiveBoardSwitcher`, and (new for this phase, per RESEARCH.md Anti-Patterns) `ITempDataDictionaryFactory` — resolved and then `.GetTempData(context)` called directly, since middleware has no `ControllerContext`/`TempData` property.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| Switch-back `<form>` embedded inside a toast (`_Toasts.cshtml` `BoardSwitchNotice` block) | component fragment | request-response | No existing toast in this codebase embeds a POST form with an antiforgery token — `GoldReceived` is the closest but is read-only markup. Use RESEARCH.md's drafted markup (Pattern 4) as the starting shape; it already follows this file's header/body/dismiss-button conventions and the app's standard `@Html.AntiForgeryToken()` usage. |

## Metadata

**Analog search scope:** `QuestBoard.Repository/`, `QuestBoard.Service/Middleware/`, `QuestBoard.Service/Controllers/`, `QuestBoard.Service/Views/Shared/`, `QuestBoard.UnitTests/Architecture/`, `QuestBoard.IntegrationTests/`
**Files scanned/read in full:** `GroupSessionMiddleware.cs`, `GroupPickerController.cs`, `EventSignupRepository.cs` (excerpt), `GroupRepository.cs` (excerpt), `AmbientClockSeamTests.cs`, `_Toasts.cshtml`, `Program.cs` (excerpt), `QuestBoardContext.cs` (excerpt)
**Pattern extraction date:** 2026-09-21
