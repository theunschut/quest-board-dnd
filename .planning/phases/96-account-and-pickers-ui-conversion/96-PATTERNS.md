# Phase 96: Account and pickers UI conversion - Pattern Map

**Mapped:** 2026-11-10
**Files analyzed:** 27 new/modified files
**Analogs found:** 24 / 27 (all analog paths are git-tracked source under `QuestBoard.Service/` and `QuestBoard.IntegrationTests/`; no `.gsd` mirror paths)

## Important finding about "already converted" analogs

Phase 94 converted only the group picker into the `@layer pages` style. No Phase 95 view has landed on this branch yet (its plans are untracked), so:

- The only committed layered page sheet is `QuestBoard.Service/wwwroot/css/group-picker.css`. `move-entry.css`, `events-overview.css` and the rest are still unlayered legacy sheets and are NOT analogs for `account.css`.
- No view uses `<script type="module">` yet. The only extracted-script precedent is classic: `QuestBoard.Service/wwwroot/js/contact-tags.js` (loaded as `<script src="~/js/..." asp-append-version="true">`, e.g. `Views/Contacts/Create.cshtml:156-157`). Research recommends a module for `profile-subscriptions.js`; a classic `<script src>` also satisfies the "no inline script" regex guards. Phase 95's PATTERNS.md and plans cover the same ground, so keep the two choices consistent.
- The foundation sheets (`tokens.css`, `base.css`, `components.css`, `shell*.css`) are the style analogs for the layer header, token use and comments.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `wwwroot/css/account.css` (new) | stylesheet (pages layer) | n/a | `wwwroot/css/group-picker.css` | exact (layer/shape) |
| `wwwroot/css/group-picker.css` (rewrite rules) | stylesheet (pages layer) | n/a | itself | self |
| `wwwroot/css/shell.mobile.css` (one gutter rule) | stylesheet (components layer) | n/a | itself (existing `.mobile-layout` rules) | self |
| `wwwroot/css/legacy-pages.css` (remove Profile block), `account.mobile.css` (delete) | stylesheet | n/a | n/a (deletions; link-then-remove) | n/a |
| `wwwroot/js/profile-subscriptions.js` (new) | script | event-driven | inline `<script>` in `Views/Account/Profile.cshtml:243-337` (moved code); file shape `wwwroot/js/contact-tags.js` | exact (moved code) |
| `Views/Account/Login/ForgotPassword/SetPassword/ChangePassword.cshtml` (merged, `.Mobile` deleted) | view | request-response | current desktop twins + `Login.Mobile.cshtml` for `@section Styles` | exact |
| `Views/Account/Edit.cshtml`, `Edit.Mobile.cshtml` | view | request-response/CRUD | themselves | self |
| `Views/Account/Profile.cshtml`, `Profile.Mobile.cshtml` | view | CRUD + event-driven | themselves; Rename modal at `Profile.cshtml:183-210` for Delete modal | exact |
| `Views/GroupPicker/Index.cshtml` (merged, `.Mobile` deleted) | view | request-response | itself (current) | self |
| `Views/Shared/AccessDenied.cshtml` | view | request-response | itself | self |
| `Views/Home/Index.cshtml` (+ `.Mobile` per Gate 1) | view | request-response | itself | self |
| `Tests/AccountConversionGuardTests.cs` (new) | test | file scan | `Tests/BoardPageStaticGuardTests.cs`, `Tests/UiFoundationMobileShellGuardTests.cs:99-110` | exact |
| `Tests/UiFoundationLayerGuardTests.cs` (arrays) | test | file scan | itself lines 52-64 | self |
| `Mobile/MobileViewsTests.cs` (rewrite facts ~552-690) | test | request-response | itself lines 552-575 | self |
| `Tests/ProfileCalendarSubscriptionTests.cs` | test | request-response | itself | self |
| `Tests/CalendarSubscriptionStaticGuardTests.cs` | test | file scan | itself lines 54-62, 188-247 | self |
| `Controllers/HomeControllerIntegrationTests.cs` | test | request-response | itself lines 40-77 | self |

## Pattern Assignments

### `wwwroot/css/account.css` (stylesheet, pages layer)

**Analog:** `QuestBoard.Service/wwwroot/css/group-picker.css` (whole file, 28 lines)

**Header + layer pattern** (lines 1-6). The file must open with `@layer pages {` as its first non-comment statement (guard `LayeredFiles_DeclareTheirLayer`). Comments explain why in plain language, no planning IDs:
```css
/*
 * The group picker is served to phones as well as desktops, from the one layout and with the desktop stylesheets
 * (it has no phone twin), so its own small-screen rules live here and not in a phone sheet.
 */
@layer pages {
  @media (max-width: 767.98px) {
    .group-picker .btn {
      min-height: var(--qb-hit-min);
      min-width: var(--qb-hit-min);
    }
```
Rules: tokens only (`var(--qb-*)`), no hex/rgb/`!important`, no `*.mobile.css` name. Two-space indent. UA-driven differences in a merged view use `.mobile-layout .x` selectors; width reflow uses `@media`. Move (never duplicate) the Profile block of `legacy-pages.css` (`.calendar-subscription-*`, `.calendar-subscription-qr svg {` with `max-width: 240px`) and the phone calendar rules of `account.mobile.css` (`.calendar-subscription-icon-btn {` with 44px, `.calendar-subscription-row .calendar-subscription-name {` with `text-overflow: ellipsis`). Keep these selector strings with the exact `selector {` shape, because `ExtractCssRule` looks up `".calendar-subscription-icon-btn {"` etc. New class stems must not contain `calendar-subscription-row` or `calendar-subscription-address` (use `subscription-item`, `subscription-summary`, `profile-fields`, `section-heading`, `account-link`).

**Link from a view** (analog `Views/Account/Login.Mobile.cshtml:5-7`; change href to `account.css`):
```cshtml
@section Styles {
    <link href="~/css/account.css" asp-append-version="true" rel="stylesheet" />
}
```
All three layouts render `Styles` after the last global link (pinned by `UiFoundationLayerGuardTests.Layouts_LinkFoundationInCascadeOrder`).

---

### `wwwroot/css/group-picker.css` (stylesheet, pages layer)

**Analog:** itself. Keep the `@layer pages {` wrapper, the `.group-picker .btn` 44px rule (Logout in the bar), and the phone `@media (max-width: 767.98px)` block shape. Delete the `.group-picker .modern-card` and `.card-title, .card-text` rules (lines 12-22) and add `.group-picker-list/-row/-name/-meta/-chevron` rules per UI-SPEC section 1 (UA button reset: `width:100%; display:flex; background-color:transparent; color:inherit; font:inherit; border:0`; hover `color-mix(in srgb, var(--qb-surface-scrim) 60%, transparent)`). It is linked by `_Layout.GroupPicker.cshtml` itself, so no `@section Styles`.

---

### `wwwroot/css/shell.mobile.css` (one gutter rule)

**Analog:** itself. Everything is scoped under `.mobile-layout` inside the file's existing layer (header comment, lines 1-6). Add inside that layer, with a plain-language comment:
```css
.mobile-layout main > .row {
  --bs-gutter-x: var(--qb-space-md);
}
```
Guards that read this file (`LayeredFiles`, `NoLiteralFiles`): no colour literal, no `!important`. Needs the Gate 1 yes (UI-SPEC section 5). Do not touch `components.css` (Phase 95 edits it).

---

### `wwwroot/js/profile-subscriptions.js` (script, event-driven)

**Analog:** the inline script in `Views/Account/Profile.cshtml:243-337` (identical on `Profile.Mobile.cshtml`). Copy, then apply the listed fixes. File-shape analog: `wwwroot/js/contact-tags.js` (header comment explaining load style; defensive no-op when the element is missing).

**Copy handler to keep** (Profile.cshtml:245-290, clipboard + manual fallback):
```js
if (!navigator.clipboard || !navigator.clipboard.writeText) { showManualFallback(); return; }
navigator.clipboard.writeText(field.value).then(function () {
    button.classList.remove('btn-primary');
    button.classList.add('btn-success');
    button.innerHTML = '<i class="fas fa-check me-2"></i>Copied!';
    restoreLabel();
}).catch(function () { showManualFallback(); });
```
Changes: capture `button.innerHTML` and class list once at wiring time and restore that (not the hard-coded `'<i class="fas fa-copy me-2"></i>Copy'`); rest class becomes `btn-secondary`; phone stays icon-only on success; drop the three unused `restoreLabel` params; keep the strings `Copied!` and `Press Ctrl+C` (`CopyContractStrings` pins them; they now live in this file).

**Add-subscription and Rename modal wiring** (lines 292-327) move verbatim: the `calendar-subscription-add-form` submit handler (disable + `Adding…`), and the `show.bs.modal`/`shown.bs.modal` listeners that read `event.relatedTarget.dataset.subscriptionId/subscriptionName`.

**Delete modal (replaces lines 329-337)**: copy the Rename modal wiring shape; on `show.bs.modal` read `event.relatedTarget.dataset` and set the name span with `textContent` only. Focus the `Keep Subscription` button on `shown.bs.modal`. Do not call `new bootstrap.Modal().show()` (loses focus return); open via `data-bs-toggle="modal"`. The `confirm()` call and the `calendar-subscription-delete-form` handler are deleted. Superseded by plan 96-07: the row's Delete stays a submit button in its own form with no Bootstrap modal attribute, and the module's submit listener on `calendar-subscription-delete-form` cancels the post and opens the dialog with `bootstrap.Modal.getOrCreateInstance(dialog).show(opener)`, then returns focus to the opener on `hidden.bs.modal`, so the dialog never shows while a revoke is sent.

**Load** (replace the inline `<script>` in `@section Scripts`; Profile.cshtml:243 already has the section):
```cshtml
@section Scripts {
    <script type="module" src="~/js/profile-subscriptions.js" asp-append-version="true"></script>
}
```
(Classic `<script src=...>` is the repo precedent; both pass the inline-code regex `<script\b(?![^>]*\bsrc=)[^>]*>`.)

---

### `Views/Account/Profile.cshtml` and `Profile.Mobile.cshtml` (view, CRUD + event-driven)

**Analog:** themselves.

**Rename modal = analog for the Delete modal** (`Profile.cshtml:183-210`; the modal sits outside the card because a `backdrop-filter` ancestor traps fixed descendants; keep that comment's reasoning, in plain words). Trigger pattern (line 134):
```cshtml
data-bs-toggle="modal" data-bs-target="#renameSubscriptionModal" data-subscription-id=... data-subscription-name=...
```
Structure to mirror: `.modal.fade#id[tabindex=-1][aria-labelledby]` > `.modal-dialog` > `.modal-content` > header (`h2.modal-title` + `btn-close[data-bs-dismiss=modal][aria-label=Close]`), body, footer. Delete modal footer: `Keep Subscription` (`btn-secondary`, `data-bs-dismiss`) and a form posting `RevokeCalendarSubscription` with `@Html.AntiForgeryToken()` + hidden `id`.

**Subscription-name encoding:** Razor-encode attributes; never `innerHTML` the name.

**Pinned markup (do not break):** desktop `table table-striped table-hover align-middle`; `<div>@name</div>` immediately followed by `<div class="calendar-subscription-meta">`; exactly one `calendar-subscription-address` per desktop row and one `calendar-subscription-row` per phone row; `maxlength="60"`; `Html.LocalTime` calls byte-identical; `Administrator` / `Dungeon Master & Player` raw `&` on desktop (`AccountControllerIntegrationTests` 116, 133). Add `tabindex="-1"` to the visually-hidden address input. Add `ViewData["Title"] = "Profile"` to the phone twin. Empty-state sentence: change only the clause in both views and in both tests in one commit.

---

### Merged credential views: `Login`, `ForgotPassword`, `SetPassword`, `ChangePassword` (view, request-response)

**Analog:** the existing desktop view of each (card markup, `row justify-content-center` / `col-md-6 [col-lg-4]`) plus `Login.Mobile.cshtml` for the `@section Styles` block and the form body (`asp-validation-summary="All"`, `asp-for` labels). Delete each `.Mobile.cshtml`; `MobileViewLocationExpander` (`ViewExpanders/MobileViewLocationExpander.cs:27-34`) falls back to the single file automatically, no registration.

**Core pattern** (from `Login.Mobile.cshtml:17-24`, upgraded per UI-SPEC section 3):
```cshtml
<div asp-validation-summary="All" class="text-danger mb-3"></div>   @* becomes a refusal-line wrapper rendered only when !ViewData.ModelState.IsValid *@
<form asp-action="Login" asp-route-returnurl="@ViewData["ReturnUrl"]" method="post">
    <div class="mb-3">
        <label asp-for="Email" class="form-label"></label>
        <input asp-for="Email" class="form-control" />   @* add autocomplete="username" *@
        <span asp-validation-for="Email" class="text-danger"></span>   @* class becomes field-validation-error *@
    </div>
```
Title gets `ViewData["Title"]` (Q-76..81): `Log in`, `Forgot Password`, `Set Your Password`, `Change Password`. One `h1` in `.card-header.modern-card-header`. No `account-card-mobile`. Commit buttons `btn-primary` (not `btn-warning`).

---

### `Views/GroupPicker/Index.cshtml` (view, request-response)

**Analog:** itself (lines 1-61 read). Delete `Index.Mobile.cshtml`.

**Keep byte-for-byte** (a test normalises this exact attribute order; echo `returnUrl` nowhere else):
```cshtml
<form asp-action="SelectGroup" method="post">
    @Html.AntiForgeryToken()
    <input type="hidden" name="groupId" value="@group.Id" />
    <input type="hidden" name="returnUrl" value="@Model.ReturnUrl" />
```
**Replace** the `div.card.modern-card.h-100 style="cursor:pointer" onclick="this.closest('form').submit()"` (lines 44-50) and the `row row-cols-*` grid with `<ul class="group-picker-list" role="list"><li><form ...><button type="submit" class="group-picker-row">` holding name and `fa-users` member-count spans (no `.card-text`/`.card-title`; `components.css` boxes `.modern-card .card-text`). Keep: `Layout = "~/Views/Shared/_Layout.GroupPicker.cshtml"` (line 5), `Select Your Group` title (promote `h2` to `h1`), `Go to Platform` with `href="/platform"` (lines 11-17), the no-groups `alert alert-warning` and its sentence `not assigned to any group` (lines 27-32). Add `aria-hidden="true"` to icons.

---

### `Views/Shared/AccessDenied.cshtml`, `Views/Account/Edit(.Mobile).cshtml`, `Views/Home/Index(.Mobile).cshtml` (views)

**Analog:** themselves; same card/`btn-*` mapping as the credential views. AccessDenied keeps the literal `You Don't Have Permission` (apostrophe unencoded, pinned at `AccountControllerIntegrationTests` 35) and gets `h1` + `h2.section-heading`. Edit: `ModelOnly` summary guarded by a model-level error key; phone gains `ViewData["Title"] = "Edit Profile"`; desktop keeps `Want to change your password?` (Q-82). Home: if merged, link `account.css` through `@section Styles` (never a `*.mobile.css`, and never emit the class `mobile-layout` in markup: `MobileCssTests` line 134-145 and `MobileLayoutTests`). Button label `Log in`, link `/Account/Login`.

---

### `Tests/AccountConversionGuardTests.cs` (new; test, file scan)

**Analog:** `QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs` (repo-root resolver and planning-reference scan) and `Tests/UiFoundationMobileShellGuardTests.cs:99-110` (inline-code scan).

**Repo-root helpers to copy verbatim** (`BoardPageStaticGuardTests.cs:18-34`):
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
**Inline-code scan to copy** (`UiFoundationMobileShellGuardTests.cs:105-108`), looped over the area's view paths (existing files only, so deleted twins do not break it):
```csharp
Regex.Matches(markup, @"\sstyle\s*=").Should().BeEmpty($"{path} must carry no inline style attribute");
Regex.Matches(markup, @"<style\b").Should().BeEmpty($"{path} must carry no style block");
Regex.Matches(markup, @"<script\b(?![^>]*\bsrc=)[^>]*>").Should().BeEmpty($"{path} must carry no inline script: only scripts loaded from a file");
```
Add: no `\son[a-z]+\s*=` handler; no `confirm(` in the two Profile views or the script; `profile-subscriptions.js` referenced by both Profile views; `account.css` linked through `@section Styles` in each area view that needs it; no `*.mobile.css` link in Home. Write planning-reference regexes as fragments, as `PlanningReferencePatterns` (lines 36-44) does, so the test file never contains the forbidden strings.

---

### `Tests/UiFoundationLayerGuardTests.cs` (arrays)

**Analog:** itself, lines 52-64. Edit in place:
```csharp
("group-picker.css", "pages"),
+ ("account.css", "pages"),
...
private static readonly string[] NoLiteralFiles = ["base.css", "components.css", "shell.css", "shell.mobile.css", "group-picker.css", "account.css"];
```
This alone buys layer-opening, no-`!important`, no-literal and planning-reference checks. `legacy-pages.css` must still exist and stay unlayered.

---

### `Mobile/MobileViewsTests.cs` (rewrite facts)

**Analog:** itself, `MobileAccountLogin_*` facts (lines ~552-575), using the existing `GetWithUserAgentAsync(url, MobileUserAgent|DesktopUserAgent)` helper. Rewrite to the same intent, not delete: mobile UA 200 with `mobile-layout` + `account.css` + email/password inputs; desktop UA 200 + `account.css` + `NotContain("mobile-layout")`. Update the `account-card-mobile` assertions in the Edit, GroupPicker (~619-663) and Profile (~667-690) facts (`modern-card`, button rows for the picker, `calendar-subscription-row` with a seeded subscription). Phase 95 edits other facts in the same file: keep the diff confined to those regions.

---

### `Tests/ProfileCalendarSubscriptionTests.cs`

**Analog:** itself. Keep by construction: per-row marker counts (172-174, 241-243), table class string and `NotContain("calendar-subscription-row")` on desktop (190-199), the name-cell regex (`<div>(.*?)</div>\s*<div class="calendar-subscription-meta">`, 107-112, 305-308), `maxlength="60"` (261). Change in the same commit as the view: the empty-state `Contain("... The address below acts like a password")` (127-131) to the new clause; reword the message mentioning `account.mobile.css` (277-281).

---

### `Tests/CalendarSubscriptionStaticGuardTests.cs`

**Analog:** itself. Helper `ExtractCssRule(css, "selector {")` (lines 54-62) returns the text to the first `}`. Re-point at `account.css`:
```csharp
var cssPath = ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "account.css");
var rule = ExtractCssRule(css, ".calendar-subscription-icon-btn {");
rule.Should().Contain("min-height: var(--qb-hit-min)");   // was "min-height: 44px"
```
- `QrCodeSizingRule_ExistsInBothStylesheets` (231-247): rewrite to one sheet (`account.css`, `.calendar-subscription-qr svg {`, `max-width: 240px`) plus both Profile views link it.
- `CopyContractStrings` (68-112) and `DeleteConfirmText_IsByteIdenticalBetweenLayouts` (116-132): strings leaving the views are asserted on `wwwroot/js/profile-subscriptions.js`; the delete sentence halves become static modal text in both views, compared byte-identical between layouts; add `NotContain("confirm(")`.
- Repo-wide planning-reference scan (278-308) unchanged.

---

### `Controllers/HomeControllerIntegrationTests.cs`

**Analog:** itself, lines 40-77: `content.Should().Contain("Log In")` appears at 53 and 76. Change both to `"Log in"` in the same commit as the Home button label (only if the capitalisation is approved at Gate 1); `Contain("/Account/Login")` unchanged.

## Shared Patterns

### One layered area sheet, linked through `@section Styles`
**Source:** `wwwroot/css/group-picker.css` (layer shape), `Views/Account/Login.Mobile.cshtml:5-7` (link markup).
**Apply to:** every merged/converted view that needs `account.css` (credential pages, Edit, Profile, AccessDenied, Home if merged).

### No inline code in views
**Source:** `Tests/UiFoundationMobileShellGuardTests.cs:99-110` and `UiFoundationDesktopShellGuardTests.cs:104-107`.
**Apply to:** all Phase 96 views; enforced by the new `AccountConversionGuardTests`.

### Merged twin falls through automatically
**Source:** `ViewExpanders/MobileViewLocationExpander.cs:27-34`; layouts chosen in `Views/_ViewStart.cshtml`.
**Apply to:** the five pairs whose `.Mobile.cshtml` is deleted. A merged view renders under `_Layout` and `_Layout.Mobile`; a root `.row` needs the `shell.mobile.css` gutter rule.

### Bootstrap data-api modal (Rename dialog)
**Source:** `Views/Account/Profile.cshtml:134, 183-210` and the listeners at 308-327.
**Apply to:** the Delete dialog on both Profile twins, plus Rename/QR dialog label changes.

### Guard tests that read files by path
**Source:** repo-root resolver in `BoardPageStaticGuardTests.cs:18-34`.
**Apply to:** `AccountConversionGuardTests`; every path-reading test must be updated when `account.mobile.css`/`.Mobile` views are removed (files that do not exist are skipped by `BoardPageStaticGuardTests`, but `CalendarSubscriptionStaticGuardTests` reads `account.mobile.css` by path and will throw).

### CLAUDE.md comment rule
**Apply to:** all CSS, JS, Razor and C# comments: plain-language reasons only; no D-nn, Q-nn, A-account-nnn, phase, plan or planning-document names (guards scan for these).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| ES-module script loaded with `type="module"` | script | event-driven | No module precedent in `Views/`; only classic `contact-tags.js`/`image-crop.js`. Use RESEARCH Pattern 4 (or a classic script) |
| `Views/Home` merged landing card (`landing-card`) | view | request-response | No converted landing page exists; use UI-SPEC section 6 |
| Scratch screenshot/mockup tooling | scratch | n/a | Lives outside the repo; none committed |

## Metadata

**Analog search scope:** `QuestBoard.Service/Views`, `QuestBoard.Service/wwwroot/{css,js}`, `QuestBoard.IntegrationTests/{Tests,Mobile,Controllers}`.
**Files scanned:** about 30 (full reads of `group-picker.css`, `GroupPicker/Index.cshtml`, Profile script block, selected test regions).
**Pattern extraction date:** 2026-11-10
