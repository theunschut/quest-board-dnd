# Phase 103: Themed Error Pages - Pattern Map

**Mapped:** 2026-11-18
**Files analyzed:** 27 new/modified
**Analogs found:** 25 / 27 (2 partial: no existing status-code-pages / exception-handler endpoint, no existing "document navigation" header helper)

Line numbers are from the working tree on 2026-11-18. Phase 100 is executing concurrently, so anchor by content (quoted strings), not by line number, wherever a file is flagged **[P100]**.

## Phase 100 overlap (grep of `100-*-PLAN.md`, files_modified and read lists)

| File this phase touches or copies | Phase 100 plan(s) | Planner guidance |
|---|---|---|
| `QuestBoard.Service/Program.cs` | 100-07 (files_modified) | Insert by content: after `app.UseStaticFiles();`, and the `app.UseExceptionHandler("/Error");` line, and `options.OnRejected =`. Do not use line numbers |
| `QuestBoard.Service/wwwroot/css/components.css`, `shell.css`, `shell.mobile.css` | 100-13 (components.css), 100-20 (shell, shell.mobile) | Phase 103 edits none of them (UI-SPEC rule). Only link them from the new layouts |
| `Areas/Platform/Views/Shared/_Layout.Platform(.Mobile).cshtml` | 100-20 | Read only; never edited here |
| `QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs` | 100-13, 100-16, 100-21 | Phase 103 adds list entries (`Layouts`, `LayeredFiles`, `NoLiteralFiles`). Merge by adding lines, anchor on the array names. Expect conflicts: land after 100's closing plan or re-read before editing |
| `UiFoundationDesktopShellGuardTests.cs`, `UiFoundationMobileShellGuardTests.cs`, `BoardPageStaticGuardTests.cs`, `KeyboardScriptGuardTests.cs` | 100-16, 100-19, 100-20 | Read only unless a new layout must join a list |
| `QuestBoard.Repository/GroupRepository.cs`, `QuestBoard.Domain/Services/GroupService.cs`, `IGroupService.cs`, `IGroupRepository.cs` | 100-10 (reads/guards it; 100-10 asserts `git diff --quiet` on `GroupService.cs` and `GroupRepository.cs` over its own span) | The resolver fix needs a role/board-type projection. Prefer a NEW method on `ICrossBoardLinkRepository` (file Phase 100 does not touch) over editing `GroupRepository`, to avoid colliding with 100-10 |
| `AccountController.cs`, `AccountControllerIntegrationTests.cs`, `AccountConversionGuardTests.cs`, `GroupSessionMiddleware.cs`, `CrossBoard*`, `WebApplicationFactoryBase.cs`, `_Layout.cshtml`, `_Layout.Mobile.cshtml`, `_Layout.GroupPicker.cshtml`, `CapturingLoggerProvider.cs` | not in any 100 plan's file list | Safe to edit; still re-read before editing |

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `QuestBoard.Service/Controllers/ErrorController.cs` (new) | controller | request-response | `AccountController.AccessDenied` + `GroupPickerController` (explicit `[Route]`) | role-match |
| `QuestBoard.Service/Middleware/ErrorPageGateMiddleware.cs` (new) | middleware | request-response | `Middleware/CrossBoardDeepLinkMiddleware.cs` | exact (shape) |
| `QuestBoard.Service/Helpers/BrowserNavigation.cs` (new) | utility | transform | `CrossBoardDeepLinkMiddleware.IsRealTopLevelNavigation` (private) | partial |
| `QuestBoard.Service/ViewModels/ErrorPageViewModel.cs` (new) | model | transform | existing `ViewModels/*` (e.g. `GroupPickerViewModel`) | role-match |
| `Views/Shared/_Layout.Error.cshtml` / `.Mobile.cshtml` (new) | layout | request-response | `_Layout.GroupPicker.cshtml` (desktop), `_Layout.Mobile.cshtml` (phone) | exact |
| `Views/Shared/ErrorPage.cshtml` / `.Mobile.cshtml` (new) | view | request-response | `Views/Account/ForgotPassword.cshtml`, `Profile.Mobile.cshtml`, `AccessDenied.cshtml` | exact |
| `wwwroot/css/error.css` / `error.mobile.css` (new) | stylesheet | n/a | `account.css`, `platform-group-delete.mobile.css` | exact |
| `wwwroot/js/error-actions.js` (new) | script | event-driven | `wwwroot/js/confirm-submit.js` | exact |
| `QuestBoard.Service/Program.cs` (mod: status pages, exception handler, `OnRejected`, logging) | config | request-response | existing pipeline block and `options.OnRejected` **[P100 100-07]** | exact |
| `AccountController.cs` (mod: `Logout(toLogin)`, `AccessDenied`) | controller | request-response | itself | exact |
| `Views/Shared/AccessDenied.cshtml` (delete) | view | n/a | itself | n/a |
| `QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs` + `Services/CrossBoardLinkResolverService.cs` (mod) | service | request-response | itself | exact |
| `QuestBoard.Domain/Interfaces/ICrossBoardLinkRepository.cs` + `QuestBoard.Repository/CrossBoardLinkRepository.cs` (mod) | repository | CRUD (read) | itself; `ShopRepository.OnSale` | exact |
| new Domain enum `CrossBoardAccess` | model | n/a | `Enums/CrossBoardLookupKind.cs` | exact |
| `QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs`, `CrossBoardLinkRegistry.cs` (mod: carry access level) | utility | transform | itself | exact |
| `Middleware/CrossBoardDeepLinkMiddleware.cs`, `Controllers/GroupPickerController.cs` (mod: both resolver callers) | middleware / controller | request-response | itself | exact |
| `Middleware/GroupSessionMiddleware.cs` | middleware | request-response | itself | no change needed (`"/Error"` already exempt) |
| `Security/CrossBoardOracleParityTests.cs`, `CrossBoardAuthorizationBoundaryTests.cs`, `CrossBoardPickerSkipTests.cs` (rewrite pinned facts) | test | request-response | themselves | exact |
| `Tests/ErrorPagesGuardTests.cs` (new) | test (static guard) | file-I/O | `Tests/ShopConversionGuardTests.cs`, `UiFoundationLayerGuardTests.cs` | exact |
| `Tests/UiFoundationLayerGuardTests.cs`, `AccountConversionGuardTests.cs` (mod lists) | test | file-I/O | themselves **[P100]** for the first | exact |
| `Controllers/ErrorPagesIntegrationTests.cs` (new; navigation gate, 500, 429, 403, twin) | test | request-response | `CrossBoardOracleParityTests` + `WebApplicationFactoryBase` | role-match |
| Log-scope fact (RESEARCH F2) | test | event-driven | `CapturingLoggerProvider` (needs `ISupportExternalScope`) | partial |

## Pattern Assignments

### `Middleware/ErrorPageGateMiddleware.cs` (middleware, request-response)

**Analog:** `QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs` (primary-constructor `RequestDelegate next`, early-return guard ladder, doc comment lists guard order).

**Shape to copy** (class declaration and guard style, file lines ~44-60):
```csharp
namespace QuestBoard.Service.Middleware;

public class CrossBoardDeepLinkMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await next(context);
            return;
        }
        ...
```
Register with `app.UseMiddleware<ErrorPageGateMiddleware>();` as `Program.cs` does for `MobileDetectionMiddleware` and `GroupSessionMiddleware`.

**Header test to generalise** (`CrossBoardDeepLinkMiddleware.cs`, `IsRealTopLevelNavigation` and `ContainsPrefetchHint`):
```csharp
return request.Headers["Sec-Fetch-Dest"] == "document"
    && request.Headers["Sec-Fetch-Mode"] == "navigate";
...
return headerValues.Any(value => value != null && value.Contains("prefetch", StringComparison.OrdinalIgnoreCase));
```
`BrowserNavigation.IsDocumentNavigation` is deliberately different (RESEARCH Pattern 2): requires `Accept` containing `text/html`, rejects `X-Requested-With: XMLHttpRequest`, and accepts `Sec-Fetch-Dest` absent/`document`/`iframe`/`frame`; no `Sec-Fetch-Mode` and no prefetch rejection. Keep it a separate static helper in `Helpers/`, do not widen the existing private method (the switch must stay strict: it mutates the session).

**Gate body** (from RESEARCH Pattern 2; no codebase analog for `IStatusCodePagesFeature`):
```csharp
var excluded = ctx.Request.Path.StartsWithSegments("/hangfire") || ctx.Request.Path.StartsWithSegments("/health");
if (excluded || !BrowserNavigation.IsDocumentNavigation(ctx.Request))
    ctx.Features.Get<IStatusCodePagesFeature>()!.Enabled = false;
await next(ctx);
```

---

### `Program.cs` pipeline edits (config) **[P100 100-07: anchor by content]**

**Current** (`Program.cs` lines ~341-353):
```csharp
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseMiddleware<MobileDetectionMiddleware>();

app.UseRouting();
```
**Edit:** after `app.UseStaticFiles();` insert `UseStatusCodePagesWithReExecute("/Error/{0}", createScopeForStatusCodePages: true)` then `UseMiddleware<ErrorPageGateMiddleware>()` (all environments); change `UseExceptionHandler("/Error")` to the options overload with `CreateScopeForErrors = true`. Keep the `!IsDevelopment()` guard (D-11).

**`OnRejected` today** (`Program.cs` ~165-170):
```csharp
options.OnRejected = async (context, cancellationToken) =>
{
    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    await context.HttpContext.Response.WriteAsync(
        "Too many requests. Please try again later.", cancellationToken);
};
```
**Edit:** keep the status; write the sentence only when `!BrowserNavigation.IsDocumentNavigation(context.HttpContext.Request)`; for navigations write nothing so the status-code re-execute (which wraps the limiter) renders the page. Controller-level 429s in `AdminController` (`Response.StatusCode = StatusCodes.Status429TooManyRequests;` plus a body) keep their text (RESEARCH F4).

**Access denied path** (`Program.cs` ~100): `options.AccessDeniedPath = "/Account/AccessDenied";` stays.

---

### `Controllers/ErrorController.cs` (controller, request-response)

**Analog:** `QuestBoard.Service/Controllers/Admin/AccountController.cs` `AccessDenied` (lines ~42-47) for the anonymous-allowed result-returning shape, and `GroupPickerController` for explicit `[Route]` attributes.
```csharp
[HttpGet]
[AllowAnonymous]
public IActionResult AccessDenied()
{
    return View();
}
```
**Deviations (RESEARCH Pattern 3):** no `[Http*]` attribute (re-executed POST/HEAD must match), add `[IgnoreAntiforgeryToken]`, `[Route("Error/{code:int?}")]`, `View("ErrorPage", model)` by NAME (never `~/Views/...`: the explicit path bypasses `ViewExpanders/MobileViewLocationExpander.cs`, so the phone twin would not be chosen). Non-navigation callers get bare `StatusCode(n)`. Reference = `HttpContext.TraceIdentifier` on 500 only. Switch-board flag from `IGroupService.GetGroupsForUserAsync(userId)` in try/catch (fail closed). Never read `IStatusCodeReExecuteFeature.OriginalPath` into the model for a 404.

**GroupSessionMiddleware:** `ExemptPathPrefixes` already contains `"/Error"` (line ~63-70) and `StartsWithSegments` covers `/Error/404`. No change; the controller is named `ErrorController`, so `$"/{ControllerNameOf<...>()}"` style is optional.

---

### `AccountController.cs` (controller, request-response): `Logout(toLogin)` and `AccessDenied`

**Current** (`Controllers/Admin/AccountController.cs`):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Logout()
{
    await userService.SignOutAsync();
    return RedirectToAction(nameof(HomeController.Index), "Home");
}
```
**Edit:** add `bool toLogin = false`; when true `RedirectToAction(nameof(Login), "Account")`; default unchanged (pinned by `AccountControllerIntegrationTests.Logout_Post_ShouldRedirectToHome`). A bool cannot carry a URL, so no open redirect.
`AccessDenied` becomes `View("ErrorPage", model)` with status 403 and keeps heading "You Don't Have Permission" and both sentences (pinned by `AccountControllerIntegrationTests` lines ~27-36: asserts `OK` today, `Contain("You Don't Have Permission")`, `NotContain("Dungeon Master")`; update to `Forbidden`).

**Logout form markup to reuse** (`Views/Shared/_Layout.GroupPicker.cshtml`, the picker bar; same as `_Layout.cshtml` around line 256):
```html
<form asp-controller="Account" asp-action="Logout" asp-area="" method="post" class="d-inline">
    <button type="submit" class="btn btn-outline-light btn-sm">
        <i class="fas fa-sign-out-alt me-1"></i>Logout
    </button>
</form>
```
On the error page use `btn btn-secondary`, `me-2`, `aria-hidden="true"` icon, add `<input type="hidden" name="toLogin" value="true" />`.

---

### `Views/Shared/_Layout.Error.cshtml` and `_Layout.Error.Mobile.cshtml` (layout)

**Desktop analog:** `Views/Shared/_Layout.GroupPicker.cshtml` (whole file, 45 lines). Copy: `<head>` preloads, the link order `app, tokens, base, components, shell` (drop `legacy-pages` per UI-SPEC), `@await RenderSectionAsync("Styles", required: false)`, `<body class="d-flex flex-column min-vh-100 shell-fixed-nav ...">`, skip link, `<nav class="navbar navbar-dark chrome fixed-top" aria-label="Main">`, `<main id="main-content" tabindex="-1">@RenderBody()</main>`, `site.js`, `Scripts` section.
```html
<a class="visually-hidden-focusable skip-link" href="#main-content">Skip to main content</a>
<header>
<nav class="navbar navbar-dark chrome fixed-top" aria-label="Main">
    <div class="container-fluid px-3">
        <div class="navbar-brand"><i class="fas fa-dice-d20"></i> D&amp;D Quest Board</div>
```
**Deviations (all deliberate):** brand is `<a href="/">`; no Logout in the bar; remove `<partial name="_Toasts" />` (must not consume a pending toast); remove the Bootstrap bundle script; no `@inject`/`BoardPages` reads; add `_Footer` partial (`_Footer.cshtml` reads only `AppVersion.Current`).

**Phone analog:** `Views/Shared/_Layout.Mobile.cshtml`. Copy the head (adds `shell.mobile.css` after `shell.css`; do NOT link `mobile.css` per UI-SPEC planner assumption 5; `legacy-pages` absent), `<body class="d-flex flex-column min-vh-100 mobile-layout @ViewData["BodyClass"]">`, and the non-authenticated brand link. Drop everything from `var pages = await BoardPages.GetAsync();` (DB read), the offcanvas nav and the toggler. Note `_Layout.Mobile.cshtml` honours `ViewData["Theme"]`; the error layouts set none (default oak).

**Selection by name:** view sets `Layout = "_Layout.Error";`, as `Areas/Platform/Views/_ViewStart.cshtml` does with `Layout = "_Layout.Platform";`, so `MobileViewLocationExpander` finds `.Mobile`.

---

### `Views/Shared/ErrorPage.cshtml` and `ErrorPage.Mobile.cshtml` (view + phone twin)

**Analog (card + head + styles section):** `Views/Account/ForgotPassword.cshtml` lines 1-21:
```cshtml
@using QuestBoard.Domain.Interfaces
@using QuestBoard.Service.ViewModels.AccountViewModels
@model ForgotPasswordViewModel
@{
    ViewData["Title"] = "Forgot Password";
}

@section Styles {
    <link rel="stylesheet" href="~/css/account.css" asp-append-version="true" />
}

<div class="row justify-content-center">
    <div class="col-md-6 col-lg-4">
        <div class="card modern-card">
            <div class="card-header modern-card-header">
                <h1 class="mb-0">
```
Use `col-md-10 col-lg-8 col-xl-6` (UI-SPEC) and no icon in the `h1`; add `<span class="header-subtitle">Error 404</span>`. Add `@section Scripts { <script src="~/js/error-actions.js" asp-append-version="true"></script> }`.

**Phone twin analog:** `Views/Account/Profile.Mobile.cshtml` lines 1-22: same card directly in `main`, no root `.row`, same `@section Styles` (link `error.css` AND `error.mobile.css` per UI-SPEC: mobile sheet only from the twin; confirm whether the twin should also link `error.css` as `Profile.Mobile` links the one shared `account.css`).
```cshtml
<div class="card modern-card mb-3">
    <div class="card-header modern-card-header">
        <h1 class="mb-0"><i class="fas fa-user-cog me-2" aria-hidden="true"></i>Profile</h1>
    </div>
    <div class="card-body modern-card-body">
```
Both cases need `<hr>` plus the action row inside `.modern-card-body` (UI-SPEC deviation). Phone action container: `d-grid gap-2`.

**Existing 403 content to preserve verbatim** (`Views/Shared/AccessDenied.cshtml`, to be deleted): heading `You Don't Have Permission`, authenticated sentence "If you believe you should have access to this page, please contact an administrator.", anonymous "Please log in to access this page." Anonymous Log in today is `<a asp-controller="Account" asp-action="Login" class="alert-link account-link-inline">`. Also remove the legacy markers it carries (`fa-3x text-warning`, `alert alert-*`, `fa-ban`).

**Guard impact:** `AccountConversionGuardTests` lists `AccessDenied.cshtml` at lines ~26, ~54 (`("...AccessDenied.cshtml", "account.css")`) and ~414 (`.Append(".../AccessDenied.cshtml")`): update the same intent (remove from lists in the commit that deletes the file; the guard fails if a listed file is missing).

---

### `wwwroot/css/error.css` and `error.mobile.css` (stylesheet)

**Analog:** `wwwroot/css/account.css` (desktop/shared page sheet) and `wwwroot/css/platform-group-delete.mobile.css` (phone twin: header comment states "carrying no media query").
```css
/* header comment: purpose, why no breakpoint, "Every value is a token from tokens.css" */
@layer pages {
  .board-delete-row--deleted {
    --board-delete-stripe: var(--qb-danger-text);
  }
```
Copy: opening comment, `@layer pages { ... }`, tokens only (`var(--qb-space-*)`, `--qb-text-*`, `--qb-surface-overlay`, `--qb-surface-glass-border`, `--qb-radius-pill`, `--qb-hit-min`), variant custom property pattern (`.error-page--500 { --error-seal-fg: var(--qb-danger-text); }` mirrors `--board-delete-stripe`). No `@media`, `!important`, colour literal, `@keyframes`, `infinite`, `transition: all`, `aspect-ratio`, `object-fit`.

**Guard registration** (`UiFoundationLayerGuardTests.cs` **[P100 100-13/16/21]**): add `("error.css", "pages")`, `("error.mobile.css", "pages")` to `LayeredFiles`; add both names to `NoLiteralFiles`; add the two new layouts to `Layouts` (`(path, IsMobile)` tuples) and extend its link-order assertion for the no-legacy-sheet allowance. Regex used to read links: `LocalStylesheetHref`.

---

### `wwwroot/js/error-actions.js` (script, event-driven)

**Analog:** `wwwroot/js/confirm-submit.js` (full file): top comment, `document.querySelectorAll('...[data-...]').forEach(...)`, `addEventListener`, no globals, no markup from strings.
```js
document.querySelectorAll('form[data-confirm-message]').forEach(function (form) {
    form.addEventListener('submit', function (event) {
        if (!confirm(form.dataset.confirmMessage)) {
            event.preventDefault();
        }
    });
});
```
Adapt: `document.querySelectorAll('[data-history-back]')`; if `history.length > 1` remove `hidden` and on click `preventDefault()` then `history.back()`; else leave the Referer `href` to work. No `innerHTML`.
**Guard:** `KeyboardScriptGuardTests.cs` and the `ModuleScanner`/`MarkupFromStrings` scanner in `ShopConversionGuardTests.cs` (line ~826 `ModuleScanner_FlagsEveryWayToHandAStringToTheParser`) show how script files are scanned; replicate for `error-actions.js` in `ErrorPagesGuardTests`. `KeyboardScriptGuardTests` is in **[P100 100-19]**: read, do not edit.

---

### `CrossBoardLinkResolverService` / `ICrossBoardLinkResolver` (service, request-response)

**Analog:** itself (`QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs`, `internal sealed class ... (IGroupService groupService, ICrossBoardLinkRepository repository)`).

**Current core** (lines ~20-47):
```csharp
public async Task<CrossBoardTarget?> ResolveAsync(CrossBoardLookupKind kind, int id, int userId, CancellationToken token = default)
{
    var memberships = await groupService.GetGroupsForUserAsync(userId, token);
    if (memberships.Count == 0) return null;
    var memberGroupIds = memberships.Select(g => g.Id).ToList();
    if (kind == CrossBoardLookupKind.BoardMember)
        return await ResolveBoardMemberAsync(id, memberships, memberGroupIds, token);
    var resolvedGroupId = await repository.ResolveBoardIdAsync(kind, id, memberGroupIds, token);
    if (resolvedGroupId is not { } targetGroupId) return null;
    var targetMembership = memberships.First(g => g.Id == targetGroupId);
    return new CrossBoardTarget(targetMembership.Id, targetMembership.Name);
}
```
**Extend (RESEARCH Pattern 5):** add `CrossBoardAccess access` and `bool viewerIsSuperAdmin` parameters; narrow `memberGroupIds` to boards where the page is available and the role satisfies `access` BEFORE the repository call, and pass `userId`, `boardNow`/`boardToday` (from `IBoardClock`) for record-level predicates. No `if (exists && !visible)` branch anywhere: hidden and nonexistent both end in `return null`. Comments saying "never consults roles" in this file, in `ICrossBoardLinkResolver` (summary block) and in `CrossBoardLinkRepository` header must be rewritten (RESEARCH F5). Do NOT put EF or `IgnoreQueryFilters` in Domain.

**BoardMember branch** (`ResolveBoardMemberAsync`): filter `sharedGroupIds` by the access predicate first, then the existing `if (sharedGroupIds.Count != 1) return null;` rule.

**Repository analog:** `QuestBoard.Repository/CrossBoardLinkRepository.cs`; each private `Resolve*BoardIdAsync` has this shape:
```csharp
return await dbContext.ShopItems
    .IgnoreQueryFilters()
    .Where(s => s.Id == shopItemId && memberGroupIds.Contains(s.GroupId))
    .Select(s => (int?)s.GroupId)
    .FirstOrDefaultAsync(token);
```
Add the record predicate inside the same `Where`, keeping `Select(... (int?)GroupId)` (never select entity data). `CrossBoardIgnoreQueryFiltersSeamTests` confines `IgnoreQueryFilters` to the file list in `AllowedCallSites`: do not add a new file that calls it.

**Shop predicate to mirror** (single definition rule, `QuestBoard.Domain/Models/Shop/ShopItemAvailability.cs`):
```csharp
public static bool HasOpened(ShopItem item, DateTime boardNow) =>
    item.AvailableFrom == null || item.AvailableFrom <= boardNow;
```
and the query-form precedent for passing the board clock into EF (`QuestBoard.Repository/ShopRepository.cs`, `OnSale`):
```csharp
private static IQueryable<ShopItemEntity> OnSale(IQueryable<ShopItemEntity> query, DateTime boardNow) =>
    query
        .Where(si => si.Status == (int)ItemStatus.Published)
        .Where(si => si.AvailableFrom == null || si.AvailableFrom <= boardNow)
```
For `Shop/Details` the rule is NOT `OnSale` (it keeps Archived and ended windows open). Mirror the controller rule instead (`Controllers/Shop/ShopController.cs`, `Details`, lines ~71-99):
```csharp
if (item == null || (IsUnreleased(item, boardNow) && !await MayReviewUnreleasedItemsAsync()))
    return NotFound();
...
private static bool IsUnreleased(ShopItem item, DateTime boardNow) =>
    item.Status == ItemStatus.Draft || item.Status == ItemStatus.Denied || !ShopItemAvailability.HasOpened(item, boardNow);
```
Status is stored as `int` on the entity (`si.Status == (int)ItemStatus.Published`). RESEARCH recommends a Domain-side shared rule called by both controller and resolver plus an agreement-matrix drift guard test.

**Callers to update together (RESEARCH F3):** `Middleware/CrossBoardDeepLinkMiddleware.cs` (`resolver.ResolveAsync(routeTarget.Kind, routeTarget.Id, userId)`) and `Controllers/GroupPickerController.cs` line ~54 (`crossBoardLinkResolver.ResolveAsync(target.Kind, target.Id, userId)`; in this caller a `null` result must keep "falling straight through"). `CrossBoardRouteTarget` is `internal sealed record CrossBoardRouteTarget(CrossBoardLookupKind Kind, int Id)`: add `CrossBoardAccess Access` (positional), populated from the registry. The registry (`Helpers/CrossBoardLinkRegistry.cs`) is a `Dictionary<string, CrossBoardLookupKind> Routes` of 18 entries keyed by `DefaultRoute<TController>(nameof(...))`; change the value to a `(Kind, Access)` pair so the table stays the one closed list. The unit fact `RegisteredRoutes_HasExactly18Entries` in `QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs` pins the count.

**Enum analog:** `QuestBoard.Domain/Enums/CrossBoardLookupKind.cs` (doc comments on each member; mirror for `CrossBoardAccess`: Member, DungeonMasterTier, QuestOwnerOrAdmin, CharacterOwnerOrAdmin, CompletedQuest, ContactVisible, ShopItemVisible, AdminForOtherUser).

---

### `Tests/ErrorPagesGuardTests.cs` (static guard)

**Analog:** `QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs` (most recent shipped guard). Copy verbatim: `ResolveRepoFile` / `FindRepositoryRoot` (walks up to `*.slnx` or `.git`), `ReadRepoFile` that fails if a listed file is missing, explicit path arrays, `PlanningReferencePatterns` built from string fragments so the test file does not contain what it forbids:
```csharp
private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
[
    ("a decision id", new Regex(@"\bD" + @"-(?:0[1-9]|[1-9][0-9])\b")),
    ...
    ("a planning document name", new Regex(
        "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md|AUD" + "IT\\.md")),
];
```
Update the phase-specific entries (`Pha...se 99`, `99-..`) to cover phase 103 and `10` `3-..` plan ids, still built from fragments. Pair every scanner with a self-test fact (`PlanningScanner_FlagsEveryShapeAndAllowsPlainComments`, `ModuleScanner_...`, `EndlessMotionScanner_...`): a seeded bad string must fire and a clean one must stay quiet.
**Layer-sheet scanning helpers** to reuse or copy: `UiFoundationLayerGuardTests` `StripComments`, `StripUrls`, `EnclosingPreludes`, `RequiredCssFiles` (missing listed sheet fails). Check for `@media` in `error.mobile.css` and `ErrorPage.Mobile.cshtml`. Inline-code scan for views/layouts: no `style=`, `<style>`, `<script>` (without `src`), `on*=`.
Also add the two new views to any rendered-markup region helpers if present (`Helpers/DesktopPageRegions.cs`, `PhonePageRegions.cs`), only if the guard needs them.

---

### Integration test shapes (`ErrorPagesIntegrationTests`, parity rewrites)

**Factory analogs:**
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`: `builder.UseEnvironment("Testing")` (so the non-Development exception handler runs), `builder.ConfigureLogging(logging => logging.AddProvider(LogCapture))`, `ConfigureTestServices` for stub replacement, `CreateNonRedirectingClient()`; `Test` auth scheme with `DefaultForbidScheme = "Identity.Application"` (Forbid yields the 302 to AccessDenied).
- `Helpers/CrossBoardWebApplicationFactory.cs`: restores the real session-backed `IActiveGroupContext` and exposes `CreateCookieClient()` (cookie container, `AllowAutoRedirect = false`). Use this for any fact that depends on a board switch (parity pairs).
- Throwing-stub 500: register a throwing service in `ConfigureTestServices` via a derived factory (own-fixture pattern; see `AdminControllerRateLimitIntegrationTests.cs` named in the 100-07 plan, and `ProductionResolverWebApplicationFactory.cs`, `EmailStatsWebApplicationFactory.cs` in `Helpers/` for factory-subclass precedent).

**Parity pair (copy and extend)** from `Security/CrossBoardOracleParityTests.cs`:
```csharp
private static HttpRequestMessage NavigationGet(string url)
{
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Add("Sec-Fetch-Dest", "document");
    request.Headers.Add("Sec-Fetch-Mode", "navigate");
    return request;
}
```
**Add `request.Headers.Add("Accept", "text/html")`** to every navigation request meant to see the page (otherwise the gate keeps it bare; RESEARCH Q10). Comparison helpers (`VolatileHeaderNames` = Date, Request-Id, traceparent; `CollectComparableHeaders`; `AssertIndistinguishableAsync` comparing status, body, headers). For the themed page extend the helper to (a) mask `__RequestVerificationToken` hidden-field values in authenticated bodies and (b) prime the antiforgery cookie with a throwaway request so the first response's `Set-Cookie` does not differ (RESEARCH F7/Q7). Theory pattern: `[MemberData(nameof(EntityFamilyRoutes))]` with `string.Format(urlTemplate, id)` versus `int.MaxValue`. New pair set: "exists on a member board but hidden from the viewer (Draft shop item as Player, hidden contact, open quest in QuestLog, Player on Quest/Edit)" versus nonexistent, still indistinguishable, and "board unchanged afterwards" (copy `CrossBoardOracleParity_UnresolvableRequest_LeavesActiveBoardUnchanged`: follow-up GET `/quests` contains the seeded board name).

**Pinned facts that must be rewritten to the same intent (RESEARCH F5):** `CrossBoardOracleParity_UnresolvableEntity_Returns404WithEmptyBody` (asserts `body.Should().BeEmpty()`; its comment says "The application registers no status-code page middleware"; becomes: 404 with a themed body that contains the fixed sentence and no id/path echo, and a non-navigation request still gets an empty body), `CrossBoardAuthorizationBoundaryTests.ResolvedDraftShopItem_SwitchesTheBoardAndLetsThePageDecideForItself`, `...DungeonMasterOnActiveBoard_PlayerOnTargetBoard_IsRefusedTheQuestEditPage`, plus `CrossBoardPickerSkipTests` status/body comparisons for the picker path.

**Log capture (D-12 reference check):** `Helpers/CapturingLoggerProvider.cs` has `BeginScope<TState>(TState state) => null;` and records message + structured state + exception only. To prove the page's Reference equals the logged `RequestId`, extend it with `ISupportExternalScope` (store the `IExternalScopeProvider`, capture scope state in `Log`). Records are read via `factory.LogCapture.Records`, cleared with `factory.LogCapture.Clear()` (usage precedent: `Tests/BoardMoveEndToEndTests.cs` lines ~244, ~264).

**Mobile twin selection fact:** `Mobile/MobileCssTests.cs` uses a `MobileUserAgent` constant (iPhone UA) and sends it with `request.Headers.TryAddWithoutValidation("User-Agent", userAgent)`; copy for "a phone UA on a 404 receives `ErrorPage.Mobile` and `mobile-layout`" (also Accept text/html).

---

## Shared Patterns

### Tenancy: one indistinguishable path
**Source:** `CrossBoardDeepLinkMiddleware.cs` ("Same path a nonexistent id takes -- no branch exists here on purpose.") and `CrossBoardLinkRepository.cs` header comment.
**Apply to:** resolver changes, `ErrorController`, ErrorPage view.
```csharp
if (target == null)
{
    // Same path a nonexistent id takes -- no branch exists here on purpose.
    await next(context);
    return;
}
```
Never render `OriginalPath`, id or route values on the page; the 404 body must be identical for a missing id and a hidden/foreign id.

### Layer rules
**Source:** `.claude/architecture.md`. Domain holds the access rules and enums; only `QuestBoard.Repository` touches EF; the Service project gains no EF package. Board time via `IBoardClock` (`boardClock.Now` as in `ShopController.Details`), never `DateTime.Now/UtcNow`; `AmbientClockSeamTests` holds a closed list.

### No planning IDs in source
**Source:** `CLAUDE.md` "Code Comments". Applies to every new view, sheet, script, test string and comment in this phase. Enforced by the `PlanningReferencePatterns` scan (copy from `ShopConversionGuardTests.cs`).

### Name-based view lookup for phone twins
**Source:** `ViewExpanders/MobileViewLocationExpander.cs`; `Areas/Platform/Views/_ViewStart.cshtml` (`Layout = "_Layout.Platform";`). Use `View("ErrorPage", model)` and `Layout = "_Layout.Error"`.

### Rendering conventions
**Source:** `.claude/ui-guidelines.md` (read by the planner/executor before any Razor edit): card pattern (`card modern-card` + `modern-card-header` + `modern-card-body`), filled buttons with `me-2` icons, `<hr>` before the action row, `.Mobile.cshtml` twin with its own sheet and no `@media`, 44px targets via `.mobile-layout` rule in `shell.mobile.css`.

## No Analog Found

| File / concern | Role | Data Flow | Reason |
|---|---|---|---|
| `UseStatusCodePagesWithReExecute` / `IStatusCodePagesFeature` gate | middleware | request-response | No status-code-pages middleware exists; use RESEARCH Pattern 1/2 (spiked) |
| `UseExceptionHandler` with `ExceptionHandlerOptions` | config | request-response | Only the broken `UseExceptionHandler("/Error")` string overload exists; use RESEARCH Q5 |
| `Logging` scopes on (`AddSimpleConsole(o => o.IncludeScopes = true)`) | config | n/a | No console logger configuration in repo; RESEARCH F2 lists the two options and flags the env-var alternative |
| Scope-capturing log provider | test helper | event-driven | `CapturingLoggerProvider.BeginScope` returns null today; needs a small extension |
| Static `BrowserNavigation` helper | utility | transform | Closest is the private `IsRealTopLevelNavigation`; the rule differs on purpose |

## Metadata

**Analog search scope:** `QuestBoard.Service/{Controllers,Middleware,Helpers,Views/Shared,Views/Account,wwwroot/css,wwwroot/js,Program.cs}`, `QuestBoard.Domain/{Services,Interfaces,Enums,Models/Shop}`, `QuestBoard.Repository/{CrossBoardLinkRepository.cs,ShopRepository.cs}`, `QuestBoard.IntegrationTests/{Security,Tests,Helpers,Mobile,WebApplicationFactoryBase.cs}`
**Files scanned:** ~40
**Not read in full:** RESEARCH.md lines 434-791 (Pitfalls, validation architecture, open questions, fixtures) were not re-read for this map; the planner must read them directly.
**Pattern extraction date:** 2026-11-18
