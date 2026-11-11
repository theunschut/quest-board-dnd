# Phase 102: Board deletion - Pattern Map

**Mapped:** 2026-11-11
**Files analyzed:** 27 new/modified
**Analogs found:** 25 / 27

Paths are relative to `/mnt/Data/repos/quest-board-dnd`. Line numbers are from files read this session. The final bullet list under "Shared Patterns" holds the guard-test constraints every plan must respect.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `QuestBoard.Repository/GroupRepository.cs` (modify: `GetDeletionPreviewAsync`, `DeleteBoardAsync`, `GetBoardNamesByUserAsync`, scoped helpers) | repository | CRUD / batch delete | same file `RemoveMemberAsync` + `QuestBoard.Repository/QuestMoveRepository.cs` (save/catch) | exact |
| `QuestBoard.Domain/Interfaces/IGroupRepository.cs`, `IGroupService.cs` (modify, +3 members) | interface | request-response | `IGroupService.cs` existing members | exact |
| `QuestBoard.Domain/Services/GroupService.cs` (modify: `DeleteBoardAsync(id, confirmName)`) | service | request-response | same file (`HasMembersAsync` passthrough) + `QuestMoveService` status mapping | role-match |
| `QuestBoard.Domain/Models/BoardDeletion/*` (`BoardDeletionPreview`, `BoardDeletionItem`, `BoardUnlinkItem`, `BoardDeleteStatus`) | model | transform | `QuestBoard.Domain/Models/BoardMove/QuestMovePreview.cs`, `QuestBoard.Domain/Enums/MoveStatus.cs` | role-match |
| `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (modify `Delete`, `DeleteConfirmed`) | controller | request-response | same file lines 185-217; `MoveController` status mapping | exact |
| `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs` (modify, add `IGroupService`) | controller | request-response | same file | exact |
| `QuestBoard.Service/ViewModels/PlatformViewModels/BoardDeletePageViewModel.cs` (new) | view model | transform | `PlatformUserViewModel.cs`; `ViewModels/MoveViewModels/MovePageViewModel` | role-match |
| `.../PlatformViewModels/PlatformUserViewModel.cs` (modify, +`BoardNames`) | view model | transform | same file | exact |
| `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (modify: SuperAdmin existence check) | middleware | request-response | same file lines 131-164 | exact |
| `QuestBoard.Service/Services/ActiveBoardSwitcherService.cs` or new `ISession` extension (session key clear helper) | utility | request-response | `ActiveBoardSwitcherService.SwitchAsync` | exact |
| `Areas/Platform/Views/Group/Delete.cshtml`, `Delete.Mobile.cshtml` (rewrite) | view | request-response | `Views/Move/Index.cshtml` (preview then confirm); `Views/QuestLog/Details.cshtml` (converted sections) | role-match |
| `Areas/Platform/Views/Group/_BoardDeleteBody.cshtml` (new shared partial) | view partial | request-response | `Views/Move/Index.cshtml` lines 111-213 | role-match |
| `Areas/Platform/Views/Users/Index.cshtml`, `Index.Mobile.cshtml` (modify) | view | request-response | same files | exact |
| `wwwroot/css/platform-group-delete.css` (new) | stylesheet | n/a | `wwwroot/css/quest-vote.css` (`@layer pages`, tokens) + `move-entry.css` structure | role-match |
| `wwwroot/css/platform-group-delete.mobile.css`, `platform-users-boards.mobile.css` (new) | stylesheet | n/a | `wwwroot/css/dm-create.mobile.css` (`@layer pages`, mobile) | role-match |
| `wwwroot/js/platform-board-delete.js` (new) | script | event-driven | `wwwroot/js/avatar-fallback.js` loading; RESEARCH typed-name snippet | partial |
| `QuestBoard.UnitTests/Repository/GroupRepositoryBoardDeletionTests.cs` (new) | test | CRUD | `QuestBoard.UnitTests/Repository/BoardMoveQuestRepositoryTests.cs` | exact |
| `QuestBoard.UnitTests/Repository/BoardOwnedForeignKeysTests.cs` (new) | test | transform (model metadata) | none (see No Analog) | none |
| `QuestBoard.UnitTests/Services/GroupServiceBoardDeletionTests.cs` (new) | test | request-response | existing `QuestBoard.UnitTests/Services/*` tests | role-match |
| `QuestBoard.UnitTests/Controllers/GroupControllerBoardDeletionTests.cs` (new) | test | request-response | `GroupControllerEditConcurrencyTests.cs` | exact |
| `QuestBoard.UnitTests/.../GroupSessionMiddlewareRevalidationTests.cs` (modify) | test | request-response | same file lines 28-70, 144-150 | exact |
| `QuestBoard.IntegrationTests/Controllers/PlatformBoardDeleteTests.cs` (new) | test | request-response | `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` | exact |
| `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` (modify delete tests), `UsersControllerIntegrationTests.cs` (extend) | test | request-response | same files | exact |
| `QuestBoard.IntegrationTests/Tests/BoardDeletionSqlServerTests.cs` (new, gated) | test | CRUD | `QuestBoard.IntegrationTests/Migrator/MigratorSqlServer.cs` (`ScratchDatabase`) | role-match |
| `QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs` area: new static guard fact for the two Delete views + partial | test | static scan | existing `UiFoundation*` / `BoardPageStaticGuardTests` | role-match |

## Pattern Assignments

### `QuestBoard.Repository/GroupRepository.cs` (repository, batch delete)

**Analog:** same file `RemoveMemberAsync` (lines 111-152) for narrowed `IgnoreQueryFilters`; `QuestBoard.Repository/QuestMoveRepository.cs` lines 117-133 for the save/catch shape.

**Class shape and imports** (lines 1-11): file is `internal class GroupRepository(QuestBoardContext dbContext, IMapper mapper, IBoardClock boardClock) : BaseRepository<Group, GroupEntity>(dbContext, mapper), IGroupRepository`. Usings: `AutoMapper`, `Microsoft.EntityFrameworkCore`, `QuestBoard.Domain.Enums/Interfaces/Models`, `QuestBoard.Repository.Entities`. Add `QuestBoard.Domain.Models.BoardDeletion`. Do not add a constructor parameter that needs a clock; the delete needs none.

**Narrowed bypass with the justification comment** (lines 130-152). Copy the comment style: say why the ambient filter answers the wrong question and that scope is re-imposed by the id argument.
```csharp
// The ambient board filter answers for the caller's currently selected board, which is the
// wrong question for an operation that targets a board named by an explicit groupId
// argument. Scope is re-imposed immediately below by that same argument, so this query is
// strictly narrower than an unscoped bypass rather than broader.
private async Task<List<int>> GetFutureEventIdsForGroupIgnoringActiveBoardAsync(int groupId, DateOnly today, CancellationToken token)
{
    return await DbContext.Events
        .IgnoreQueryFilters()
        .Where(e => e.GroupId == groupId && e.Date >= today)
        .Select(e => e.Id)
        .ToListAsync(token);
}
```
Nested navigation scope: `DbContext.EventSignups.IgnoreQueryFilters().Where(es => es.UserId == userId && es.Event.GroupId == groupId)` (line 148-151). Use the same style for `ps.Quest.GroupId`, `t.ShopItem.GroupId`, `pdv.ProposedDate.Quest.GroupId`, `es.Event.GroupId`.

**Single save + failure shape** (QuestMoveRepository.cs 117-133):
```csharp
try
{
    await dbContext.SaveChangesAsync(token);
}
catch (DbUpdateConcurrencyException)
{
    dbContext.ChangeTracker.Clear();
    return MoveStatus.Changed;
}
catch
{
    // No half-applied tracked graph may be saved later in the same request.
    dbContext.ChangeTracker.Clear();
    throw;
}
```
For delete: only the bare `catch { Clear(); throw; }` arm (concurrency retry is inside the context save override).

**Member recheck first** (line 47-48): `await DbContext.UserGroups.AnyAsync(ug => ug.GroupId == groupId, token)`; return `HasMembers` before staging anything.

**Users column query** (one grouped query; `UserGroups`/`Groups` have no query filter): project `new { ug.UserId, ug.Group.Name }`, group by user in memory, sort names case-insensitively. Same shape as `GetAllWithMemberCountAsync` (lines 14-27, a `Select` projection to a Domain model, no mapper).

**Preview:** project `Select(q => q.Title)` etc. with `OrderBy` inside the helpers; counts via `CountAsync`; never `Include` image navigations. Keep preview and delete on the same private `BoardX(groupId)` helpers (RESEARCH Pattern 1/2/3 give the full draft).

**Guards (all keep this work in this one file, not a partial):** it is on `AllowedCallSites` in `QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs:16-23` and on both lists in `AmbientClockSeamTests`. No `DateTime.Now/UtcNow/Today`, no `ExecuteDelete/ExecuteUpdate/ExecuteSql`, no `.Attach(`, no `.Entry(` (`FeedRevisionWriteSeamTests`).

---

### `QuestBoard.Domain/Interfaces/IGroupService.cs` + `IGroupRepository.cs` (interface)

**Analog:** `IGroupService.cs` lines 1-43. XML-doc every member (`/// <summary>`), `CancellationToken token = default` last, domain types only (no EF).
```csharp
/// <summary>
/// Returns whether the group has at least one member.
/// </summary>
Task<bool> HasMembersAsync(int groupId, CancellationToken token = default);
```
Add `GetDeletionPreviewAsync(int groupId, ...)`, `DeleteBoardAsync(int groupId, string confirmName, ...)` (service) / `DeleteBoardAsync(int groupId, ...)` (repository), and a user-id to board-names lookup.

---

### `QuestBoard.Domain/Services/GroupService.cs` (service, request-response)

**Analog:** same file. Primary-constructor `internal class GroupService(IGroupRepository repository, IMapper mapper) : BaseService<Group>(...)`; one-line passthroughs with `/// <inheritdoc/>`:
```csharp
/// <inheritdoc/>
public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
    => await repository.HasMembersAsync(groupId, token);
```
Orchestration for `DeleteBoardAsync`: load group (missing -> `NotFound`), `string.Equals(confirmName?.Trim(), group.Name, StringComparison.Ordinal)` else `NameMismatch`, then call repository (returns `HasMembers`/`Deleted`). Status enum mirrors `QuestBoard.Domain/Enums/MoveStatus.cs` (a plain enum file in `Enums/`), and the controller switch mirrors `MoveController`. Note `GroupService.AddAsync` uses `DateTime.UtcNow`; do not copy that into new code.

---

### `QuestBoard.Domain/Models/BoardDeletion/*` (model)

**Analog:** `QuestBoard.Domain/Models/BoardMove/` (`QuestMovePreview.cs`, `MovePerson.cs`, `MovePreview.cs`) as the per-feature subfolder of plain preview records; `Models/GroupWithMemberCount.cs` for a flat projection model. Put `BoardDeleteStatus` in the same folder or in `Enums/` next to `MoveStatus`. Shape from UI-SPEC "Data the Views Need": item `{ Label singular/plural, Icon, Count, Names or null }` in spec order; unlink side `{ Title, BoardName }` rows plus two counts. Keep Icon strings out of Domain if you prefer: have the view model map type to icon.

---

### `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (controller, request-response)

**Analog:** same file. Constructor (lines 21-27) already has `IGroupService`, `ILogger<GroupController>`; do NOT change it (`GroupControllerEditConcurrencyTests` constructs it with `new`).

**Existing actions to replace** (lines 185-217): `Delete` GET (null group -> `RedirectToAction(nameof(Index))`; members -> `TempData["Error"]` + redirect) and `[HttpPost, ActionName("Delete")] [ValidateAntiForgeryToken] DeleteConfirmed(int id)`. Keep the attributes and names; add `string? confirmName` to POST.

**Toast vocabulary** (used across the file): `TempData["Success"|"Error"|"Warning"]`; add `TempData["Info"]` for board-gone (UI-SPEC). Copy texts from UI-SPEC Copywriting Contract.

**Re-render with refusal** pattern: lines 77-97 `Edit` POST re-renders `View(model)` after rebuilding the page state; use `ModelState`-free bool on `BoardDeletePageViewModel` for the mismatch (UI-SPEC says refusal line, empty input, `is-invalid`).

**Failure mapping:** try/catch around `groupService.DeleteBoardAsync`; `logger.LogError(ex, ...)`, `TempData["Error"] = "The board could not be deleted and nothing was changed. Try again."`, `RedirectToAction(nameof(Delete), new { id })`. Log one Information line on success (user id, board id and name, counts only).

**Session clear after success:** `HttpContext.Session.LoadAsync()` then remove the three keys if `ActiveGroupId == id` (see switcher/middleware excerpts below).

---

### `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs` (controller)

**Analog:** same file lines 10-27. Primary ctor `UsersController(IUserService userService, IIdentityService identityService)`; add `IGroupService groupService`. Fetch the user-id to names dictionary once before the loop, then set `BoardNames = names.GetValueOrDefault(user.Id) ?? []` inside the existing `viewModels.Add(new PlatformUserViewModel { User = user, IsDisabled = ... })`. Only integration tests construct it (DI), so the ctor change is safe.

`PlatformUserViewModel` (whole file, 8 lines): add `public IReadOnlyList<string> BoardNames { get; set; } = [];`.

---

### `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (middleware)

**Analog:** same file lines 131-164. The gap is the condition on line ~138:
```csharp
if (needsRevalidation && !context.User.IsInRole("SuperAdmin"))
{
    var userService = context.RequestServices.GetRequiredService<IUserService>();
    ...
    var role = await userService.GetGroupRoleByIdAsync(userId, groupContext.ActiveGroupId!.Value);
    if (role == null)
    {
        context.Session.Remove(SessionKeys.ActiveGroupId);
        context.Session.Remove(SessionKeys.ActiveGroupName);
        context.Session.Remove(SessionKeys.ActiveGroupValidatedAtUtc);
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }
        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/groups/pick?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }
    context.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
}
```
Add an `else if (needsRevalidation)` arm for SuperAdmin: resolve `IGroupService` via `context.RequestServices.GetRequiredService<>()`, `GetByIdAsync(activeId)`; null -> same clear + 409/redirect block; else restamp. Factor the three `Remove` calls into one shared helper used here and by the controller. Existing test `SuperAdmin_StaleTimestamp_InvokesNextWithoutMembershipCheck` (GroupSessionMiddlewareRevalidationTests.cs:28-70, 144-150) builds a `ServiceCollection` with only `IUserService` and `IActiveGroupContext`; add an `IGroupService` substitute returning an existing group.

---

### Session-key helper (utility)

**Analog:** `QuestBoard.Service/Services/ActiveBoardSwitcherService.cs` lines 6-23, whose doc names it "the one place permitted to write the three active-board session keys":
```csharp
await context.Session.LoadAsync(token);
context.Session.SetInt32(SessionKeys.ActiveGroupId, groupId);
context.Session.SetString(SessionKeys.ActiveGroupName, groupName);
context.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
```
Add the clear counterpart next to it (either `ClearAsync` on `IActiveBoardSwitcher` plus the interface, or an `ISession` extension in `QuestBoard.Service/Extensions`, which avoids a `GroupController` ctor change). Keys live in `QuestBoard.Service/Constants/SessionKeys.cs`. Check for a test that pins "only this file writes SessionKeys" before choosing the location.

---

### `Areas/Platform/Views/Group/Delete.cshtml` + `Delete.Mobile.cshtml` + `_BoardDeleteBody.cshtml` (views)

**Read `.claude/ui-guidelines.md` first (project rule).** Authoritative layout is `102-UI-SPEC.md`.

**Analog for structure:** `QuestBoard.Service/Views/Move/Index.cshtml`.

Consequence row (lines 150-162 pattern; copy structure, not the Move sheet's literals):
```cshtml
<ul class="move-consequences list-unstyled">
    <li class="move-consequence move-consequence--kept">
        <div class="move-consequence-icon"><i class="fas fa-fw fa-check" aria-hidden="true"></i></div>
        <div class="move-consequence-body"><p class="mb-0">@Model.KeptSentence</p></div>
    </li>
```
Rename to `board-delete-row board-delete-row--{deleted|unlinked|kept}`; icon cell, body `<p class="mb-0"><strong>{count} {label}</strong></p>`, optional `<p class="board-delete-hint mb-0">`, optional `<details>`.

Hint and `<strong>` use (line 211-ish): `<p class="move-hint mb-0">` and `<strong class="move-strong">@name</strong>`. In new code use a plain `<strong>` (token sheet carries weight).

Confirm footer (Move/Index.cshtml 252-274): `<hr>` then `<div class="d-flex justify-content-between">` with a `btn btn-secondary` back link first and the form second; `<form method="post" data-disable-on-submit asp-controller=... asp-action=...>`. New page uses `btn-danger` per UI-SPEC and puts the input inside the form.

Card header (Move 49-56): `card modern-card` > `card-header modern-card-header` > `<h2 class="mb-0"><i class="fas ... me-2" aria-hidden="true"></i>Title</h2>`; body `card-body modern-card-body`.

Alert (Move 59-62): `<div class="alert alert-warning" role="alert"><i class="fas fa-triangle-exclamation me-2" aria-hidden="true"></i>...</div>`; the Delete page uses `alert-danger` without `role` (UI-SPEC).

**Existing files to rewrite** (current content): desktop uses `row justify-content-center` > `col-md-6` > `card modern-card` with `<form asp-action="Delete" method="post" asp-route-id="@Model.Id">`; phone links `~/css/platform-group.mobile.css` and uses `platform-group-card-mobile`. The new phone twin stops linking that legacy sheet and uses `container-fluid px-2 mt-2` > `card modern-card` (converted phone card pattern per UI-SPEC; `Views/QuestLog/Index.Mobile.cshtml` is the cited precedent). Model changes from `@model Group` to `@model BoardDeletePageViewModel`.

**Section loading** (Views/QuestLog/Details.cshtml 11-17):
```cshtml
@section Styles {
    <link rel="stylesheet" href="~/css/quest-log.css" asp-append-version="true" />
}

@section Scripts {
    <script type="module" src="~/js/avatar-fallback.js" asp-append-version="true"></script>
}
```
Both Platform layouts render `Styles` and `Scripts` (`_Layout.Platform.cshtml` lines 16 and 66; Mobile twin likewise) and `_Toasts`.

**Dates:** `Html.LocalTime(CreatedAt, "date")` (real instant). Never `.ToString` on the date.

**Names:** Razor-encoded `<li>`; never `Html.Raw`. Blank name -> `(untitled)`. Avoid `<span>` for visible text inside the desktop `modern-card`.

**Users views:** `Users/Index.cshtml` header row lines 21-27 (`<th scope="col">Email</th>` then Confirmed): insert `<th scope="col">Boards</th>` between Email and Confirmed and a matching `<td title="...">`. Phone `Users/Index.Mobile.cshtml`: new block between the email `<div class="mb-2">` (line ~46) and the `d-flex flex-wrap gap-2` action row. Add `@section Styles` link for `platform-users-boards.mobile.css` after the existing `platform-users.mobile.css` link. Leave the existing `onclick="return confirm(...)"` untouched.

---

### CSS sheets (stylesheet)

**Analog:** `QuestBoard.Service/wwwroot/css/quest-vote.css` (header comment block explaining the page, then everything inside `@layer pages { ... }`, tokens only):
```css
@layer pages {
  .vote-choice {
    --vote-fill: var(--qb-neutral);
    min-width: 32px;
    border: 2px solid transparent;
    background-color: var(--vote-fill);
    font-size: var(--qb-text-sm);
    font-weight: var(--qb-weight-strong);
```
Also `dm-create.mobile.css` and `quest-manage.css` in the same layer. Row rhythm from `move-entry.css` lines 5-35 (`display:flex; gap; padding; border-left: 4px solid`; body `flex:1 1 auto; min-width:0; overflow-wrap:anywhere`) but replace `8px/16px/4px` with `var(--qb-space-sm/md/xs)` and the hex stripes with `--qb-danger-text`, `--qb-info-text`, `--qb-neutral-text`. Do NOT copy `move-entry.mobile.css` (it has `rgba()` literals and hex). No `@media`, no `!important`, no hex in the phone sheets. Phone `summary`: `min-height: var(--qb-hit-min); display:flex; align-items:center`.

---

### `wwwroot/js/platform-board-delete.js` (script, event-driven)

**Analog:** loading pattern from `Views/QuestLog/Details.cshtml:15-17` (`<script type="module" ... asp-append-version="true">` in `@section Scripts`); body from RESEARCH "Typed-name gate":
```javascript
const input = document.querySelector('[data-board-delete-name]');
const button = document.querySelector('[data-board-delete-submit]');
if (input && button) {
    const sync = () => { button.disabled = input.value.trim() !== input.dataset.boardDeleteName; };
    input.addEventListener('input', sync);
    sync();
}
```
Extend per UI-SPEC: also write the status region (`#board-delete-status`) only on state change, run `sync` on `pageshow` (after `site.js` re-enables submit buttons), and never re-enable after submit. `data-disable-on-submit` is the existing hook in `wwwroot/js/site.js`. Other modules to glance at for style: `wwwroot/js/avatar-fallback.js`, `quest-details.js`.

---

### `QuestBoard.UnitTests/Repository/GroupRepositoryBoardDeletionTests.cs` (test)

**Analog:** `QuestBoard.UnitTests/Repository/BoardMoveQuestRepositoryTests.cs` (declares its own private `MutableGroupContext : IActiveGroupContext` at line 24, InMemory `QuestBoardContext`, seeds then asserts). Also `BoardMoveWaitlistParityTests.cs` for parity style and `FeedRevisionStamperTests.cs`. Use `context.SavingChanges` to count saves / inject a throwing handler. Include the FK-integrity sweep helper (RESEARCH "How the FK-integrity sweep works") and cases with active board = another board and = null.

### `BoardOwnedForeignKeysTests.cs`
No analog in repo; see No Analog Found.

### `GroupControllerBoardDeletionTests.cs`
**Analog:** `QuestBoard.UnitTests/Controllers/GroupControllerEditConcurrencyTests.cs` (constructs `GroupController` with `new GroupController(...)` and NSubstitute services). Keeps ctor stable. Needs a `ControllerContext` with a session for the own-session-clear fact.

### `PlatformBoardDeleteTests.cs` and edits to `GroupManagementIntegrationTests.cs`
**Analog:** `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` lines 86-125:
```csharp
await TestDataHelper.ClearDatabaseAsync(_factory.Services);
var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
var uniqueName = "DelPost_" + Guid.NewGuid().ToString("N")[..8];
var createData = new Dictionary<string, string> { ["Name"] = uniqueName, ["BoardType"] = ((int)BoardType.OneShot).ToString() };
await client.PostAsync("/platform/Group/Create", new FormUrlEncodedContent(createData), TestContext.Current.CancellationToken);
using var scope = _factory.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
var group = dbContext.Groups.FirstOrDefault(g => g.Name == uniqueName);
```
`DeleteGroup_WhenEmpty_PostShouldRedirectToIndex` (line 110) posts no name and must be updated to post `confirmName`. Phone UA: copy the `Mobile Safari` header pattern from `AgendaMobileRenderTests`.

### `BoardDeletionSqlServerTests.cs` (gated)
**Analog:** `QuestBoard.IntegrationTests/Migrator/MigratorSqlServer.cs` (`public static class MigratorSqlServer` line 14 with `RequireServerConnectionString()`; `public sealed class ScratchDatabase : IDisposable` line 154) plus `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` for the active-board stub. Use `EnsureCreated()` on the scratch DB; never touch the app database.

---

## Shared Patterns

### Narrowed `IgnoreQueryFilters`
**Source:** `QuestBoard.Repository/GroupRepository.cs:130-152`. **Apply to:** every preview and delete query. Always `IgnoreQueryFilters().Where(<explicit id predicate>)` with a comment saying why. Never an unscoped bypass.

### Friendly toast and redirect
**Source:** `GroupController.cs` (`TempData["Success"|"Error"|"Warning"]` then `RedirectToAction(nameof(Index))`), rendered by the shared `_Toasts` partial in both Platform layouts. **Apply to:** all delete outcomes; write no toast markup.

### Single tracked save with tracker clear
**Source:** `QuestMoveRepository.cs:117-133`. **Apply to:** `DeleteBoardAsync`. Tracked `Remove/RemoveRange` plus property writes for unlinks, one `SaveChangesAsync`.

### Platform authorization and CSRF
**Source:** `GroupController.cs:19-20` (`[Area("Platform")] [Authorize(Policy = "SuperAdminOnly")]`) and `[HttpPost] [ValidateAntiForgeryToken]` on every POST. **Apply to:** Delete POST; keep the form on the tag helper so the token renders.

### Guard tests every plan must keep green
- `FeedRevisionWriteSeamTests`: no `ExecuteDelete/ExecuteUpdate/ExecuteSql`, `.Attach(`, `.Entry(` in Repository/Domain/Service source.
- `CrossBoardIgnoreQueryFiltersSeamTests`: `IgnoreQueryFilters` only in the five allowlisted files; keep the work in `GroupRepository.cs`.
- `AmbientClockSeamTests`: `GroupRepository.cs` is guarded; no ambient clock reads.
- `AntiForgeryTokenCoverageTests`, `UiFoundation*`, `MobileCssTests`, `BoardPageStaticGuardTests` (reads `platform-group.mobile.css`, so leave that file alone).
- Architecture rules: EF packages only in Repository; Service -> Domain -> Repository one-way; no GSD tracking IDs (D-xx, phase numbers) in source comments or strings (CLAUDE.md).

### Branching
Work goes on `milestone/v9-rolling-improvements` or a feature branch, never `main`. Per CLAUDE.md, no migration is needed in this phase.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `QuestBoard.UnitTests/Repository/BoardOwnedForeignKeysTests.cs` | test | model metadata | No existing test reads `Model.FindEntityType(...).GetReferencingForeignKeys()`; use RESEARCH "The model-pin test" |
| FK-integrity sweep helper (inside `GroupRepositoryBoardDeletionTests`) | test helper | reflection over `Model.GetEntityTypes()` | No existing relational-integrity check on InMemory; use RESEARCH "How the FK-integrity sweep works" |
| `platform-board-delete.js` state machine (status region, pageshow) | script | event-driven | Only the typed-name snippet and the module loading pattern exist; no existing "gated submit" module |

## Metadata

**Analog search scope:** `QuestBoard.Repository`, `QuestBoard.Domain/{Interfaces,Services,Models,Enums}`, `QuestBoard.Service/{Areas/Platform,Views/Move,Views/QuestLog,Middleware,Services,ViewModels,wwwroot/css,wwwroot/js}`, `QuestBoard.UnitTests`, `QuestBoard.IntegrationTests`
**Files scanned:** about 30 read or grepped
**Pattern extraction date:** 2026-11-11
