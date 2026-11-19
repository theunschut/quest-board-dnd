# Phase 100: Admin and Platform UI conversion - Pattern Map

**Mapped:** 2026-11-18
**Files analyzed:** about 60 (25 views, 1 partial, 2 Platform layouts verified, about 4 new page sheets, 6 retired sheets, 1 new module, domain and repository additions, 8 test classes, tooling)
**Analogs found:** all but two (see No Analog Found)

Paths are repository-relative. Phase 99's map (`.planning/phases/99-shop-and-shop-management-ui-conversion/99-PATTERNS.md`) still applies for: `@section Styles` linking, `@layer pages` sheets, no-inline-code regexes, `UiFoundationLayerGuardTests` array appends, `RetiredFiles`, scratch tooling copy (`~/qb-ui-review/phase-99/tools` to `phase-100/tools`, never edit in place), confirm-submit usage and Gate package shape. Only what is new or different is below. RESEARCH (`100-RESEARCH.md`) is authoritative for the gap table G-01 to G-23 and for line numbers inside the Admin and Platform views. Note: `modern-card.css` and `site.css` no longer exist; `.modern-card`, `.badge`, `.table` live in `components.css`. RIP MCP was not used; reads and greps only.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `QuestBoard.Domain/Interfaces/IUserService.cs` + `Services/UserService.cs` (`GetAccountReachAsync`, `ResetPasswordAsync` gains group id and refusal) | service | request-response | `UserService.ResetPasswordAsync(ClaimsPrincipal, ...)` `:107-110` and `GetEffectiveGroupRoleAsync` `:67-73` | role-match |
| `QuestBoard.Domain/Interfaces/IGroupService.cs` + `Services/GroupService.cs` (`ChangeMemberRoleAsync`, `TryRemoveMemberAsync`) | service | CRUD | `GroupService.RemoveMemberAsync` `:34-41` (chokepoint it wraps) | role-match |
| `QuestBoard.Domain` outcome enums (`AccountReach`, `MemberChangeOutcome`) | model | transform | `QuestBoard.Domain/Enums/GroupRole.cs` | role-match |
| `QuestBoard.Repository/GroupRepository.cs` (`CountMembersWithRoleAsync`) + interface | repository | CRUD (query) | `GroupRepository.HasMembersAsync` `:49-50` | exact |
| `QuestBoard.Repository/UserRepository.cs` (ids that administer another board) + interface | repository | CRUD (query) | `UserRepository.GetBoardNamesByUserAsync` `:~80` (unfiltered `UserGroups` read) and `SetGroupRoleAsync` `:105-120` | role-match |
| `QuestBoard.Repository/IdentityService.cs` (batch SuperAdmin ids) | service | request-response | `IsSuperAdminAsync(int)` `:174-178` | exact |
| `QuestBoard.Domain/Services/QuestService.RemoveAsync` (reminder-log cleanup) | service | CRUD | itself `:110-125`; board delete tracked removal `GroupRepository.cs:173-174`, `:246-247` | exact |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` (refusals, DELETE to POST) | controller | request-response | itself; `RedirectWith*` in `Extensions/ControllerExtensions.cs` | exact |
| Platform `GroupController`/`UsersController` outcomes (G-13, G-15) | controller | request-response | `UsersController.Disable` self-refusal `:35-40` | role-match |
| `UserManagementViewModel` per-row `Reach`, `IsSelf`, `IsLastAdmin` | model | transform | `ShopManagementIndexViewModel` rows | role-match |
| Display mapping (board type, group role, quest status; D-16) | helper | transform | `QuestBoard.Service/ViewModels/ShopViewModels/ShopItemDisplay.cs` | exact |
| `Views/Admin/*` (11) and `Areas/Platform/Views/{Group,Users,LinkedBoards}/*` (15 incl. `_BoardDeleteBody`) | view | request-response | `Views/ShopManagement/Index.cshtml` (h1 in `.modern-card-header`), `Platform/Views/Group/Delete.cshtml` (Styles section) | role-match |
| `wwwroot/css/admin-pages.css`, `admin-pages.mobile.css`, `platform-pages.css`, `platform-pages.mobile.css` (new, layered) | stylesheet | n/a | `wwwroot/css/shop-pages.css`, `shop-pages.mobile.css`, `platform-group-delete.css` | exact |
| Six retired phone sheets (`admin-form`, `admin-quests`, `admin-users`, `platform-group`, `platform-users`, `platform-linked-boards` `.mobile.css`) | stylesheet | n/a | Phase 99 `RetiredFiles` | exact |
| Optional `wwwroot/js/platform-members.js` (reopen create-member modal on error, G-15) | script | event-driven | `wwwroot/js/confirm-submit.js` header tone; `platform-board-delete.js` | role-match |
| `Program.cs` optional `EmailSettings:ResendBaseUrl` (dev capture only) | config | request-response | `Program.cs:223-228` named client | exact |
| `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` Resend handler stub | test infra | request-response | its own `ConfigureTestServices` (IBackgroundJobClient spy) | exact |
| `Tests/AdminPlatformConversionGuardTests.cs` (new) | test | file scan | `Tests/ShopConversionGuardTests.cs` | exact |
| `Tests/AdminPlatformRenderTests.cs`, `EmailStatsRenderTests.cs` (new) | test | request-response render | `Tests/AgendaConversionTests.cs` | exact |
| `Controllers/AdminAccountReachTests.cs`, `AdminRoleGuardTests.cs`, `AdminQuestDeleteTests.cs` (new) | test | request-response | `Controllers/AdminControllerIntegrationTests.cs` (`:392-426`) | exact |
| `UnitTests/Services/UserServiceTests.cs`, `GroupServiceTests.cs` additions | test | unit | same files | exact |
| Rewritten guards (`PlatformAreaLayoutTests`, `LinkedBoardsPlatformIntegrationTests`, `BoardPageStaticGuardTests`, `MobileViewsTests` Admin entries, `QuestStatusBoardClockTests`, `UsersControllerIntegrationTests`, `AdminControllerIntegrationTests` DeleteAsync facts) | test | render / scan | themselves | exact |
| `UiFoundationLayerGuardTests.cs` arrays | test | file scan | itself (`LayeredFiles` `:54`, `NoLiteralFiles` `:106`) | exact |

## Pattern Assignments

### 1. Domain reach and guard service methods (D-02, D-03), no EF in Service

**Analog (service shape):** `QuestBoard.Domain/Services/UserService.cs:107-110`, the overload that has no target check today:
```csharp
public async Task<IdentityResult> ResetPasswordAsync(ClaimsPrincipal adminUser, User user, string newPassword)
{
    return await identityService.AdminResetPasswordAsync(adminUser, user.Id, newPassword);
}
```
Extend with the group id; refuse with `IdentityResult.Failed(...)` before the Identity call. The SuperAdmin short-circuit follows `GetEffectiveGroupRoleAsync` (`UserService.cs:67-73`, uses `IsInRole("SuperAdmin")`), so an acting SuperAdmin costs no query. Target SuperAdmin test reads Identity (`IsSuperAdminAsync`, `IdentityService.cs:174-178`), never a membership row.

**Analog (chokepoint to wrap, not change)** `QuestBoard.Domain/Services/GroupService.cs:30-41`:
```csharp
/// The single chokepoint every membership removal funnels through ... Anything that must happen
/// when a member leaves a board belongs behind it.
public async Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default)
    => await repository.RemoveMemberAsync(groupId, userId, token);
```
Add `TryRemoveMemberAsync(groupId, actingUserId, targetUserId)` that applies self and last-Admin checks and then calls this; Platform `RemoveMember` keeps the plain method (G-14). Guard is judged on the resulting state (RESEARCH Pattern 2, copy that block for `ChangeMemberRoleAsync`).

**Analog (repository count query):** `QuestBoard.Repository/GroupRepository.cs:49-50`
```csharp
public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
    => await DbContext.UserGroups.AnyAsync(ug => ug.GroupId == groupId, token);
```
New `CountMembersWithRoleAsync` is `CountAsync(ug => ug.GroupId == groupId && ug.GroupRole == (int)role, token)`. "Admin on any other board" query lives in `UserRepository` over `DbContext.UserGroups` (no global filter; `GetBoardNamesByUserAsync` reads it unfiltered): `ids.Contains(ug.UserId) && ug.GroupId != groupId && ug.GroupRole == (int)GroupRole.Admin`, `Select(UserId).Distinct()`. Writes stay on the change tracker (see `UserRepository.SetGroupRoleAsync :105-120`: tracked edit then `SaveChangesAsync`); no `ExecuteUpdate`/raw SQL.

**Quest delete cleanup (G-05):** `QuestService.RemoveAsync` (`:110-125`) already re-reads through the filtered `QuestRepository.GetQuestWithManageDetailsAsync` and removes signups then the quest; add the quest's `ReminderLogs` removal and null an other-board follow-up's `OriginalQuestId`, copying the tracked removals in `GroupRepository.cs:173-174` and `:246-247`. InMemory does not enforce FKs, so the test must assert the rows are gone, not just "no throw".

**Tests:** unit facts in `UnitTests/Services/UserServiceTests.cs` (constructs `new UserService(identityService, repository, mapper, groupService)` with NSubstitute) and `GroupServiceTests.cs`.

### 2. Controller refusal to RedirectWithError toast (G-01, G-03, G-04, G-23)

**Analog:** `QuestBoard.Service/Extensions/ControllerExtensions.cs` (all in `internal static class`):
```csharp
internal static IActionResult RedirectWithError(this Controller controller, string action, string message)
    => controller.RedirectWithMessage(action, "Error", message);   // TempData["Error"] then RedirectToAction(action)
```
Also `RedirectWithSuccess`, `RedirectWithWarning`. Platform controllers write `TempData` keys directly.

**Current shape to replace** (`AdminController.cs:58-68`, same in `:71-106`, `:225-239`):
```csharp
var groupId = activeGroupContext.ActiveGroupId;
if (groupId == null) return RedirectToAction(nameof(Users));
// Guard against a crafted POST for a user outside the active group
var targetRole = await userService.GetGroupRoleByIdAsync(userId, groupId.Value);
if (targetRole == null) return RedirectToAction(nameof(Users));
await userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.Admin);
return RedirectToAction(nameof(Users));
```
Target shape (keep the membership scope check, turn silence into a toast, call the guarded service, map each outcome to copy). Reach call from RESEARCH Code Examples:
```csharp
var reach = (await userService.GetAccountReachAsync(User, groupId.Value, [model.Id]))[model.Id];
if (reach != AccountReach.Reachable)
    return this.RedirectWithError(nameof(Users), AccountReachCopy.Refusal(reach));
```
Apply on GET and POST of `EditUser` and `ResetPassword` and POST `SendConfirmationEmail`. Acquire the rate-limit lease after the scope, reach and already-confirmed checks (G-06, G-08). Refusal copy goes in one static class (precedent: `ShopItemDisplay`), never inline strings scattered per action.

**View side:** per-row flags on `UserManagementViewModel`; disabled control plus a visible one-line reason text linked with `aria-describedby`, not `title` (RESEARCH Pattern 1; analog `.move-control-reason`, `Views/Quest/Manage.cshtml:~709`, class is move-specific by name so add a neutral class in the components layer).

### 3. HttpDelete to HttpPost with a confirm-submit form (D-12, D-13)

**Controller current** (`AdminController.cs:344-363`, `:439-449`): `[HttpDelete][ValidateAntiForgeryToken]`, returns `NotFound()` / `Ok()`. Change to `[HttpPost][ValidateAntiForgeryToken]`, return PRG: `this.RedirectWithSuccess(nameof(Users), ...)` / `RedirectWithError(...)`; `DeleteQuest` keeps `[BoardPage(BoardPage.Quests)]` and redirects to `nameof(Quests)`. Cross-board id stays a 404 (verdict in RESEARCH; add the missing regression fact).

**View pattern** (RESEARCH Pattern 3; analogs `Views/Shop/Index.cshtml:379`, `Views/ContactCategoryManagement/Manage.cshtml:111`, `Views/Events/Details.cshtml:78,87`):
```cshtml
<form asp-action="DeleteUser" asp-route-id="@userModel.User.Id" method="post"
      data-confirm-message="Are you sure you want to remove this user from the group? ...">
    <button type="submit" class="btn btn-danger btn-sm"><i class="fas fa-user-minus me-2" aria-hidden="true"></i>Remove from Group</button>
</form>
@section Scripts { <script type="module" src="~/js/confirm-submit.js" asp-append-version="true"></script> }
```
`asp-action` forms get the token from the tag helper; plain `method="post"` forms must contain `@Html.AntiForgeryToken()` (guard `PostFormsWithoutAToken`). The module (`wwwroot/js/confirm-submit.js`, reuse unchanged) binds forms present at load:
```js
document.querySelectorAll('form[data-confirm-message]').forEach(function (form) {
    form.addEventListener('submit', function (event) {
        if (!confirm(form.dataset.confirmMessage)) { event.preventDefault(); }
    });
});
```
Delete the four inline fetch scripts (Users, Quests, both layouts) and the LinkedBoards handler strings built in Razor (`onsubmit` becomes `data-confirm-message`, sentence unchanged byte for byte).

**Existing tests to rewrite to same intent:** `Controllers/AdminControllerIntegrationTests.cs:57` (`InlineData("/Admin/DeleteUser/1")`, comment at `:65` about 405), `:393-426` and `:432-475` use `adminClient.DeleteAsync(...)`. Convert to a POST with form content and assert 302 plus the toast; keep the "membership row only, account intact" and "no DbUpdateException" assertions. `AntiForgeryTokenCoverageTests` (reflects over `HttpPostAttribute`) then covers both actions without edit.

### 4. Resend named-client handler stub in the integration factory (D-10)

**Analog (place and style):** `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`, `builder.ConfigureTestServices(...)` which runs after the app's registrations and already swaps `IBackgroundJobClient` for a capturing spy:
```csharp
builder.ConfigureTestServices(services =>
{
    services.AddSingleton<IBackgroundJobClient>(JobClient);
    services.AddSingleton<IActiveGroupContext>(TestGroupContext);
    ...
});
```
Add (RESEARCH Pattern 4, probed):
```csharp
services.AddHttpClient("Resend").ConfigurePrimaryHttpMessageHandler(() => ResendHandler);   // later registration wins
// and in ConfigureWebHost: builder.UseSetting("EmailSettings:ResendApiKey", "<stub>");
```
Production registration it overrides (`QuestBoard.Service/Program.cs:223-229`): `AddHttpClient("Resend", c => { c.BaseAddress = new Uri("https://api.resend.com/"); ... })` and `AddScoped<ResendStatsClient>()`. Since `ResendStatsClient` caches for 5 minutes per host in `IMemoryCache`, use one factory (own fixture, like `AdminControllerRateLimitIntegrationTests`) per figure set or `?force=true`. Make the handler a public property on the factory with a settable canned JSON so the default factory still shows the "not configured" state (`appsettings.json` key is empty); only the dedicated EmailStats fixture sets the key.

**Handler shape to copy:** `QuestBoard.UnitTests/Services/ResendStatsClientTests.cs:89` (`private sealed class StubHttpMessageHandler : HttpMessageHandler`, `Enqueue(status, body)`) and its JSON (`:15-20`):
```json
{"data":[{"id":"e1","created_at":"2026-06-02 00:00:00.000000+00","last_event":"delivered"}]}
```
It is private to the unit project, so write a small separate handler class in the integration project's `Helpers/`. Optional dev seam `EmailSettings:ResendBaseUrl` (default unchanged, pinned by a test) is for the live capture only.

### 5. Conversion guard tests: clone of ShopConversionGuardTests

**Analog:** `QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs` (1242 lines). Clone as `AdminPlatformConversionGuardTests.cs`, replacing lists:

- `ConvertedViews` (`:94`): the 11 Admin views and the Platform Group, Users, LinkedBoards views, partial `_BoardDeleteBody.cshtml`. `ViewSheetLinks` (`:117`): e.g. `["QuestBoard.Service/Views/Admin/Users.cshtml"] = ["admin-pages.css"]`, phone twins add the `.mobile.css`. `RetiredFiles` (`:132`): the six retired phone sheets. Module list: `confirm-submit.js` plus any new module.
- Reuse helpers verbatim: `ResolveRepoFile`/`FindRepositoryRoot`/`ReadRepoFile` (`:18-37`), `InlineCodePatterns` (`:143-148`), `StripRazorAndHtmlComments` (`:242`), `PostFormsWithoutAToken` (`:251-272`), `StylesSection`/`LinksSheet` (`:495-521`), `MarkupFromStrings` (`:322`), `DateFormatCall`/`RawDateFormats` (`:482-488`), `PlanningReferencePatterns` (`:225`, fragments so the test file never holds forbidden strings), `ScopeBreaks`/`FilesIn` (`:580-600`) with `PhaseScope_IsFullyConverted`.
- Add facts: no `onsubmit`/`onclick`; no `method="delete"`/`fetch(` in the views; no `alert(`; Admin and Platform view folders fully listed; `Layouts` carry no area sheet; one `@section Styles` per view.

**Whole-picture scanner with self-test (copy exactly, `ShopConversionGuardTests.cs:274-281` and `:750-760`):**
```csharp
private static List<string> WholeImageBreaks(string css)
{
    var withoutComments = Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");
    return Regex.Matches(withoutComments, @"(?<![\w-])(aspect-ratio|object-fit)\s*:")
        .Select(m => m.Groups[1].Value).ToList();
}

[Fact]
public void WholeImageScanner_FlagsBothDeclarationsAndIgnoresCommentsAndOtherNames()
{
    WholeImageBreaks(".thumb { aspect-ratio: 4 / 3; object-fit: cover; }").Should().BeEquivalentTo("aspect-ratio", "object-fit");
    WholeImageBreaks(".thumb { width: 100%; height: auto; }").Should().BeEmpty();
    WholeImageBreaks("/* never use aspect-ratio: or object-fit: here */ .thumb { max-width: 100%; }").Should().BeEmpty();
    WholeImageBreaks(".thumb { --my-aspect-ratio: 1; }").Should().BeEmpty("a custom property that merely contains the name is not the declaration");
}
```
A real fact then runs it over the four new sheets and every converted view and script. Do not copy `WholeImageRuleGaps`, `Rarity*`, `Legendary*`, `ShopMotion*` (shop-specific; no admin page shows a picture).

**Self-tests to copy too:** `ViewSheetLinkScanner_ReadsOnlyTheStylesSection` (`:1039-1047`) and `ScopeScanner_FlagsAnUnlistedViewAForeignEntryAndADuplicate` (`:1062-1068`). Core facts to clone (`:1014-1060`): `ConvertedViews_CarryNoInlineStyleScriptOrHandler`, `ConvertedViews_LinkTheirSheets`, `RetiredFiles_AreGone`, `PhaseScope_IsFullyConverted`.

**Layer guard arrays:** `Tests/UiFoundationLayerGuardTests.cs` `LayeredFiles` (`:54`, tuples `(File, Layer)`) and `NoLiteralFiles` (`:106`). Append `("admin-pages.css","pages")`, `("admin-pages.mobile.css","pages")`, `("platform-pages.css","pages")`, `("platform-pages.mobile.css","pages")`; re-read both arrays before appending (Phases 96 to 99 grew them). Phase 102 sheets (`platform-group-delete*.css`, `platform-users-boards.mobile.css`) are guarded by `BoardDeletionStaticGuardTests.cs:50-58`; if consolidated, rewrite those lists, never drop.

### 6. Render tests for both User-Agents

**Analog:** `Tests/AgendaConversionTests.cs:15-45` (`IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime`; the two UA consts; `AgentFor`; `GetAsync` setting `User-Agent` via `TryAddWithoutValidation`; `DisposeAsync` resets `TestGroupContext.ActiveGroupId = 1` and `BoardType`):
```csharp
private static string AgentFor(bool mobile) => mobile ? MobileUserAgent : DesktopUserAgent;
private static async Task<string> GetAsync(HttpClient client, string url, string userAgent) { ... }
```
Use `[Theory][InlineData(false)][InlineData(true)]`. Seeding/auth: `AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory)` and `CreateAuthenticatedSuperAdminClientAsync` (`Helpers/AuthenticationHelper.cs:166,176`), `CreateTestUserAsync(...)`, `TestDataHelper.ClearDatabaseAsync` / `SeedCampaignGroupAsync(services, 2)`, direct `QuestBoardContext.UserGroups.Add(...)` as in `AdminControllerIntegrationTests.cs:402-408`. Facts to pin per both layouts: Q-85 Resend form for unconfirmed-with-address users, Q-87 Open/Closed status via the shared mapping, Q-88 "Current Members", Q-92 "Password Management", Q-93 name in ResetPassword heading, one `h1` in the card header (Q-124), empty-state copy unchanged (Q-94), disabled controls render their visible reason, no `title`-only reasons.

EmailStats statistics branch: own fixture with the Resend handler stub (section 4), assert the stat tiles render canned figures; default fixture asserts "not configured".

### 7. Page sheets in @layer pages via @section Styles

**Analog sheets:** `QuestBoard.Service/wwwroot/css/shop-pages.css` (header comment states "one pages layer block and nothing but design tokens"; `@layer pages { ... }`), `shop-pages.mobile.css`, `platform-group-delete.css` / `.mobile.css`. Phone rules scope under `.mobile-layout` (selected by User-Agent, not width).

**Link pattern** (`Areas/Platform/Views/Group/Delete.cshtml:6-8`, same as `Views/Events/Create.cshtml:9-11`):
```cshtml
@section Styles {
    <link rel="stylesheet" href="~/css/platform-group-delete.css" asp-append-version="true" />
}
```
Both Platform layouts render the `Styles` and `Scripts` sections (`_Layout.Platform.cshtml:16`, `_Layout.Platform.Mobile.cshtml:17`). Platform desktop links `app, tokens, base, components, shell, legacy-pages`; phone links `app, tokens, base, components, shell, shell.mobile, mobile`. Partials cannot render sections, so `_BoardDeleteBody.cshtml` links nothing; the pages rendering it do. Rules: tokens only, no hex/rgb, no `!important`, no `transition: all`; delete the old unlayered phone rules in the same slice (unlayered beats layered). Page title is an `h1` inside `.modern-card-header` on both layouts, sections `h2`, `.empty-state`, `.form-page` and `.form-actions` for forms (analog `Views/ShopManagement/Index.cshtml:17-22`). Align `Group/Delete.cshtml` desktop `h2` to `h1`.

### 8. Display mapping (D-16, Q-87, A-admin-044)

**Analog:** `QuestBoard.Service/ViewModels/ShopViewModels/ShopItemDisplay.cs`: a `public static class` of `switch` expressions with plain XML docs and an `_ =>` fallback:
```csharp
public static string StatusName(ItemStatus status) => status switch
{
    ItemStatus.Draft => "Pending Review",
    ItemStatus.Published => "Available",
    ...
};
```
New `AdminDisplay` (name proposal) with `BoardTypeName`, `GroupRoleName`, `QuestBoardStatus(boardType, isClosed)`; both layouts call it. Wording changes ("Dungeon Master", "Administrator") need the Gate 1 yes before they land. Guard: no view formats `GroupRole`/`BoardType` via raw `ToString()`. Quest status inputs already come from `ViewBag.BoardType` and `ViewBag.BoardToday` (`AdminController.cs:428-431`); keep the board-clock fact in `QuestStatusBoardClockTests.cs:120`.

## Shared Patterns

- **Anti-forgery:** every `method="post"` form carries a token (`asp-action` tag helper or `@Html.AntiForgeryToken()`); controller actions keep `[ValidateAntiForgeryToken]`; sweep `Security/AntiForgeryTokenCoverageTests.cs:29-33` covers `[HttpPost]` only, which is why D-12 matters.
- **PRG toasts:** `RedirectWithSuccess`/`RedirectWithError`/`RedirectWithWarning` (`Extensions/ControllerExtensions.cs`).
- **Confirmations:** `data-confirm-message` plus `confirm-submit.js` (`type="module"` in `@section Scripts`), wording byte-identical (change G-18 sentence only with Gate 1 approval).
- **No inline code:** regexes in `ShopConversionGuardTests.cs:143-148`; modules build no markup from strings (`MarkupFromStrings`).
- **Layering:** Service project holds no EF Core; reach and guard logic in Domain, queries in Repository; writes to `Events`/`Quests` stay on the change tracker (DeleteQuest keeps tracked `Remove`).
- **Dates:** `Html.LocalTime` for instants (Group/Index Created), `Html.WallClock` for wall-clock; no raw date `ToString`.
- **Comments:** plain-language why; no requirement, phase, plan or finding IDs or planning document names in C#, Razor, CSS or JS (G-/D-/Q-/A- IDs above are for planning only).
- **Test fixtures:** unique usernames per class, `TestDataHelper.ClearDatabaseAsync`, reset `TestGroupContext` in `DisposeAsync`; rate-limit tests use their own fixture.
- **Release:** v6.5.0 via PR after both gates; each plan's docs commit updates ROADMAP.md and STATE.md.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| Disabled-with-visible-reason control for D-02/D-03 on list rows | view component | request-response | Only the move control (`Quest/Manage.cshtml`, `.move-control-reason`) is similar and its class is move-named; add a neutral class to `components.css` |
| Reopen the Create New User modal after a failed POST (G-15), if chosen over redirect-with-toast | script | event-driven | No module reopens a Bootstrap modal from server state; redirect-with-toast like `AddMember` is the in-repo analog (operator question O-12) |

## Metadata

**Analog search scope:** `QuestBoard.Service/{Controllers,Extensions,Views,Areas/Platform,ViewModels,wwwroot/css,wwwroot/js,Program.cs}`, `QuestBoard.Domain/Services`, `QuestBoard.Repository`, `QuestBoard.IntegrationTests/{Tests,Controllers,Helpers,WebApplicationFactoryBase.cs}`, `QuestBoard.UnitTests/Services`, Phase 99 PATTERNS, Phase 100 CONTEXT and RESEARCH (first 600 lines plus slices).
**Files scanned:** about 25 read or grepped (the `.claude/worktrees/shop-create-fix-*` copy of the repo was ignored).
**Pattern extraction date:** 2026-11-18
