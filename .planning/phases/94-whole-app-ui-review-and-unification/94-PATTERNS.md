# Phase 94: Whole-App UI Review and Unification - Pattern Map

**Mapped:** 2026-10-30
**Files analyzed:** 24 new/modified groups
**Analogs found:** 21 / 24 (CSS layer files, vendor assets and the contrast test have no direct analog)

Paths are relative to repo root. `QS` = `QuestBoard.Service`, `IT` = `QuestBoard.IntegrationTests`.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `QS/Views/Shared/_Layout.cshtml` | layout | request-response | itself (rebuild); `_Layout.Mobile.cshtml` for `Styles` section | exact |
| `QS/Views/Shared/_Layout.Mobile.cshtml` | layout | request-response | itself; already renders `Styles` section (line 17) | exact |
| `QS/Views/Shared/_Layout.GroupPicker.cshtml` | layout | request-response | itself (lines 7-12, 34-36) | exact |
| `QS/Areas/Platform/Views/Shared/_Layout.Platform.cshtml` | layout | request-response | `_Layout.GroupPicker.cshtml` (same head shape); needs `Styles` section added | exact |
| `QS/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` | layout | request-response | `_Layout.Mobile.cshtml` | role-match |
| `QS/Views/Shared/_Footer.cshtml`, `_Footer.Mobile.cshtml` | partial | request-response | themselves | exact |
| `QS/Views/Shared/_Toasts.cshtml` | partial | event-driven (TempData to toast) | itself | exact |
| `QS/wwwroot/css/app.css` (layer order + `@import ... layer(vendor)`) | config (CSS) | static | none | no analog |
| `QS/wwwroot/css/tokens.css`, `base.css`, `components.css`, `shell.css`, `shell.mobile.css` | stylesheet | static | `modern-card.css`, `site.css`, `mobile.css` (source material to extract from, not structure to copy) | partial |
| `QS/wwwroot/css/site.css` to `legacy-pages.css` (rename) | stylesheet | static | itself | exact |
| `QS/wwwroot/lib/**` (bootstrap, fontawesome, cinzel) | static asset | static | none (only `wwwroot/images`, `js`, `css` exist today) | no analog |
| `build/tests/package-release-smoke-test.sh` | test (shell) | batch | itself, "zip contains" loop lines 66-74 | exact |
| `IT/Tests/UiFoundationStaticGuardTests.cs` (new) | test | file-I/O | `IT/Tests/BoardMoveStaticGuardTests.cs` (repo-root + scan); `IT/Tests/CalendarSubscriptionStaticGuardTests.cs` (`ResolveRepoFile` + `ExtractCssRule`) | role-match |
| `IT/Mobile/MobileCssTests.cs` (rewrite) | test | file-I/O + request-response | itself | exact |
| `IT/Controllers/PlatformAreaLayoutTests.cs` (rewrite) | test | request-response | itself | exact |
| `IT/Tests/ContactCategoryContrastGuardTests.cs` (rewrite) | test | file-I/O + request-response | itself | exact |
| `IT/Tests/LinkedBoardStaticGuardTests.cs`, `LinkedBoardCalendarStyleTests.cs` | test | file-I/O | themselves | exact |
| `IT/Tests/CalendarSubscriptionStaticGuardTests.cs` | test | file-I/O | itself (line 234 reads `site.css`) | exact |
| `IT/Tests/BoardMoveStaticGuardTests.cs` | test | file-I/O | itself (reads `_Layout.cshtml`; lists `mobile.css` at line 98) | exact |
| `.claude/ui-guidelines.md` | docs | n/a | itself | exact |

## Pattern Assignments

### Layouts: all five (layout, request-response)

**Head today** (`_Layout.cshtml` lines 8-31, same shape in the other four):
```html
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
<link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" rel="stylesheet">
<link href=".../v4-shims.min.css" rel="stylesheet" integrity="sha384-..." crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Cinzel:..." rel="stylesheet">
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/modern-card.css" asp-append-version="true" />
... 12 more page sheets (desktop only) ...
```
Replace the four CDN lines with the local `app.css` (first), then tokens, base, components, shell (plus shell.mobile on mobile), then legacy sheets. Keep the `asp-append-version="true"` convention on every local link.

**`Styles` section pattern** (already in `_Layout.Mobile.cshtml` line 17 and `_Layout.GroupPicker.cshtml` line 12). Add the same line to `_Layout.cshtml` and `_Layout.Platform.cshtml`, which lack it:
```razor
@await RenderSectionAsync("Styles", required: false)
```

**Body and scripts** (`_Layout.GroupPicker.cshtml` 34-36, `_Layout.Platform.cshtml` 51-53). Today jQuery (cdnjs, no SRI), the Bootstrap bundle (jsDelivr) and `~/js/site.js` load in that order. Target is a local bootstrap bundle, then `site.js`. Drop jQuery only after the four proofs.
```html
<script src="https://cdnjs.cloudflare.com/ajax/libs/jquery/3.6.0/jquery.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>
<script src="~/js/site.js" asp-append-version="true"></script>
```

**Body class** (desktop line 33, mobile line 20): `<body class="d-flex flex-column min-vh-100 @ViewData["BodyClass"]">`. The mobile layout adds `mobile-layout`, which is what A-5 and the mobile guard scope to.

**Server-side preamble to keep** (`_Layout.cshtml` lines 1-5, 34-37): `@inject IHttpContextAccessor`, and `var pages = await BoardPages.GetAsync(); var homeRoute = BoardPageRoutes.HomeRoute(pages);`. Menu structure and `BoardPages` conditions are untouched; only classes and ARIA change.

**Nav markup today:** desktop `<nav class="navbar navbar-expand-lg navbar-dark bg-dark py-2">` (line 40); mobile `<nav class="navbar navbar-dark bg-dark py-2">` (line 29) with an offcanvas. Add `aria-label`, `aria-controls` and `aria-expanded` on togglers, and a skip link first in `<body>`.

### `_Footer.cshtml` / `_Footer.Mobile.cshtml` (partial)

**Analog:** `_Footer.cshtml` in full:
```razor
@using QuestBoard.Service.Helpers
<footer class="navbar navbar-dark bg-dark mt-auto">
    <div class="container">
        <span class="navbar-text">D&amp;D Quest Board v@(AppVersion.Current). Created by Theun Schut.</span>
        <a href="https://github.com/theunschut/dnd-quest-board" target="_blank" class="navbar-brand">
            <i class="fab fa-github"></i>
        </a>
    </div>
</footer>
```
Keep the text. Add `aria-label` and `rel="noopener noreferrer"` to the GitHub link.

### `_Toasts.cshtml` (partial, event-driven)

**Analog:** itself. Container (line 5): `<div class="toast-container position-fixed top-0 end-0 p-3" style="z-index: 1055;">`. Drop the inline style and use a class with `top: var(--qb-nav-h)`.

Success pattern (lines 7-18):
```razor
@if (TempData["Success"] != null)
{
    <div class="toast show" role="alert" data-bs-autohide="true" data-bs-delay="5000">
        <div class="toast-header bg-success text-white">
            <i class="fas fa-check-circle me-2"></i>
            <strong class="me-auto">Success</strong>
            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
        </div>
```
Target per the contract (A-9):
- Success and info become `role="status"`.
- Error and warning keep `role="alert"` and get `data-bs-autohide="false"`. Today they have no autohide attribute at all.
- The Info header `bg-info text-white` needs a passing fill and label pair.
- Add `aria-label` to the close buttons.

### New CSS files in `QS/wwwroot/css/`

No layered-CSS analog exists: 48 files, no `@layer`.

**Extraction sources:**
- `modern-card.css` and `site.css` globals: card, form and table re-themes.
- `mobile.css`: mobile buttons and targets. It already holds the `min-height: 44px` and `font-size: 16px` literals that `MobileCssTests` pins.

Files and layers:
- `app.css`: `@layer vendor, tokens, base, components, pages, utilities;` plus `@import url("../lib/bootstrap-<ver>/css/bootstrap.min.css") layer(vendor);` and the same for Font Awesome.
- `tokens.css`: the only file allowed to hold colour literals.
- `base.css` and `components.css`: no `!important` except inside `prefers-reduced-motion` and `.visually-hidden*`.

Rules to pin from the UI-SPEC: one-class selectors, no IDs, `:where()` for the mobile touch-target rule, and `color-mix()` for tints.

### `build/tests/package-release-smoke-test.sh`

**Analog:** itself, the zip-entry loop (lines 66-74). Extend the entry list or add a second loop for `app/wwwroot/lib/...`.
```bash
ENTRIES="$(unzip -Z1 "$ZIP")"
for entry in release-manifest.json app/QuestBoard.Service.dll migrator/QuestBoard.Migrator.dll deploy/systemd/questboard.service.d/10-release-layout.conf; do
  # A here-string, not a pipe: grep -q exits on the first match ... (pipefail)
  if grep -qxF "$entry" <<<"$ENTRIES"; then
    check "zip contains $entry" "present" "present"
  else
    check "zip contains $entry" "present" "missing"
  fi
done
```
Use `check "desc" expected actual`, never a pipe into `grep -q`. The app is served from `current/app`, so add a curl of `/lib/...css` after the favicon check (lines 105-106, `check "GET /favicon.ico returns 200 ..."`). Confirm in `build/package-release.sh` that `wwwroot/lib` is published.

### `IT/Tests/UiFoundationStaticGuardTests.cs` (new; test, file-I/O)

**Primary analog:** `IT/Tests/BoardMoveStaticGuardTests.cs`. It has no fixture, so it is a plain class and needs no `WebApplicationFactoryBase`.

Repo-root finder (lines 14-30):
```csharp
private static string ResolveRepoFile(params string[] segments) =>
    Path.Combine(FindRepositoryRoot().FullName, Path.Combine(segments));

private static DirectoryInfo FindRepositoryRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (dir.EnumerateFiles("*.slnx").Any() || dir.EnumerateDirectories(".git").Any())
            return dir;
        dir = dir.Parent;
    }
    throw new DirectoryNotFoundException(...);
}
```
Copy this walk-up exactly. The comment explains why: `bin/` holds apphost binaries named like the project folders, so matching on a `QuestBoard.Service` directory name is fragile.

Scan-list pattern (lines 33-60): forbidden patterns as `(string Label, Regex Pattern)[]` built from fragments so the test file never contains the forbidden literal. A list of source files skips any file that does not exist. Reuse this for `!important`, colour-literal, CDN-host, `style="` and `<script>` scans.

**Secondary analog** (`CalendarSubscriptionStaticGuardTests.cs`):
- Lines 191 and 234: `File.ReadAllText(ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "site.css"))`.
- Messages follow `because: $"... at '{cssPath}' must contain ..."`.

**Rule-extraction helper** (`ContactCategoryContrastGuardTests.cs` lines 59-69):
```csharp
private static string ExtractCssRule(string css, string selectorWithOpenBrace)
{
    var start = css.IndexOf(selectorWithOpenBrace, StringComparison.Ordinal);
    if (start < 0) return string.Empty;
    var end = css.IndexOf('}', start);
    return end < 0 ? string.Empty : css[start..(end + 1)];
}
```
It returns empty on a missing rule so a deleted rule fails the assertion rather than throwing. Use it for the `base.css` focus-ring and reduced-motion assertions. It does not handle nested braces, so `@media` or `@layer` blocks need a brace-depth variant.

Use FluentAssertions (`.Should().Contain(..., because: ...)`) and xUnit v3 `[Fact]`; `TestContext.Current.CancellationToken` appears where async is used.

**No analog:** the A-1 contrast recompute from `tokens.css` (custom-property parse plus WCAG luminance). Nothing like it exists; write from the UI-SPEC.

### `IT/Mobile/MobileCssTests.cs` (rewrite)

**Analog:** itself.

- `ResolveMobileCssPath` (lines 41-65) walks up from `AppContext.BaseDirectory` and looks for `QuestBoard.Service/wwwroot/css/mobile.css`.
- File-content facts assert `css.Should().Contain("min-height: 44px")` and `"font-size: 16px"`. These move to the new files, probably `components.css` for the `.mobile-layout :where(...)` rule and `base.css` for 16px.
- Link-presence facts (lines 90-132) GET "/" with a mobile or desktop User-Agent and assert `html.Should().Contain("mobile.css")`. The desktop one asserts `NotContain("mobile.css")` and `Contain("site.css")`.

Rewrite the link assertions to new names. `shell.mobile.css` still contains "mobile.css" as a substring, and `shell.css` does not, so check that the desktop `NotContain("mobile.css")` assertion still holds. The `site.css` link becomes `legacy-pages.css` or `app.css`.

Test-client plumbing to reuse:
```csharp
var request = new HttpRequestMessage(HttpMethod.Get, "/");
request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
```
It uses the `MobileUserAgent` and `DesktopUserAgent` constants (lines 15-19).

### `IT/Controllers/PlatformAreaLayoutTests.cs` (rewrite)

**Analog:** itself, 29 lines. One fact authenticates as `SuperAdmin` and GETs `/Platform/Group`:
```csharp
var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
    factory, "platform_user", "platform_user@example.com", roles: ["SuperAdmin"]);
var response = await client.GetAsync("/Platform/Group", TestContext.Current.CancellationToken);
html.Should().Contain("modern-card.css");
```
Change the asserted string to `components.css`. Add facts that the Platform layout carries the foundation files and the `Styles` section.

### `IT/Tests/ContactCategoryContrastGuardTests.cs` (rewrite)

**Analog:** itself; the class carries `ResolveCssPath(fileName)`, `ExtractCssRule`, and `GetMobileAsync`. Several facts pin `modern-card.css` element rules:
- line 125: `.modern-card .form-text a`
- line 164: `.modern-card .text-danger`
- line 175: `.modern-card-header .header-subtitle`

Re-pin the Phase 80 intent against the surface-inheritance model: category headings must not be repainted unreadable. The mobile facts reading `contacts.mobile.css` and `contact-form.mobile.css` stay untouched, since those are area-phase files.

### Other guard tests

- `BoardMoveStaticGuardTests`: it reads `_Layout.cshtml`; its `FeatureSourceFiles` list (around line 98) names `mobile.css`. Keep them green after the layout rebuild.
- `CalendarSubscriptionStaticGuardTests` line 234: it asserts a QR SVG rule in `site.css`. If the rule moves with the `legacy-pages.css` rename, update the path at lines 234-244.
- `LinkedBoardStaticGuardTests` and `LinkedBoardCalendarStyleTests`: they reference `modern-card.css`. I did not read them. The planner should grep for the exact selectors before rewriting.

## Shared Patterns

### Static-guard file location
**Source:** `BoardMoveStaticGuardTests.cs` lines 14-30 (`FindRepositoryRoot`). `ContactCategoryContrastGuardTests` and `MobileCssTests` each hand-roll `ResolveCssPath` instead. Prefer the `.slnx` or `.git` walk for new tests, and consider copying it rather than adding more per-file resolvers.

### User-Agent split
**Source:** `MobileCssTests.cs` lines 15-19 and `ContactCategoryContrastGuardTests.cs` lines 15-17. A mobile UA picks the `.Mobile.cshtml` twin; GroupPicker has no twin and serves both.

### No planning references in source
**Source:** CLAUDE.md "Code Comments". No decision IDs, phase numbers or planning document names in CSS, cshtml or C# comments. `BoardMoveStaticGuardTests` enforces this with the regex array.

### Cache busting
**Source:** every local `<link>` and `<script>` uses `asp-append-version="true"`. Vendor `@import` targets are not fingerprinted, so the version goes in the path (`lib/bootstrap-5.3.x/`).

## No Analog Found

| File | Role | Reason |
|---|---|---|
| `QS/wwwroot/css/app.css` and the layered `tokens/base/components` files | stylesheet | No `@layer` or token file exists today; the UI-SPEC is the only source |
| `QS/wwwroot/lib/**` | vendor assets | Nothing is self-hosted today; all vendor files come from CDNs |
| Token contrast test (A-1) | test | No colour or contrast computation exists in the suite |
| Skip link, `<main id="main-content">`, `aria-label` on the shell | markup | Absent from every layout today |

## Metadata

**Analog search scope:** `QS/Views/Shared`, `QS/Areas/Platform/Views/Shared`, `QS/wwwroot/css`, `IT/Tests`, `IT/Mobile`, `IT/Controllers`, `build/tests`.
**Files read:** the UI-SPEC (lines 1-458 only, plus a grep of the remainder), 5 guard tests (partial), 3 layouts (head only), `_Toasts`, `_Footer`, the smoke script.
**Not read:** `LinkedBoardStaticGuardTests`, `LinkedBoardCalendarStyleTests`, `_Layout.Platform.Mobile.cshtml`, `_Footer.Mobile.cshtml`, `package-release.sh`.
**Pattern extraction date:** 2026-10-30
