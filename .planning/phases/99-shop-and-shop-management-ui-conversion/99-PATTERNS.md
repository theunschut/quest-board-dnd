# Phase 99: Shop and Shop management UI conversion - Pattern Map

**Mapped:** 2026-11-15
**Files analyzed:** about 45 (11 views, 3 layouts or partials touched, about 6 sheets, 5 new modules, 7 test files, tooling)
**Analogs found:** all but three (see No Analog Found)

Paths are repository-relative. Phase 98's map (`.planning/phases/98-.../98-PATTERNS.md`) still applies for: `@section Styles/Scripts` linking, the no-inline-code regexes, `@layer pages` sheets, `UiFoundationLayerGuardTests` array appends, `RetiredFiles`, `*ConversionGuardTests` structure and the scratch tooling. Only what is new or different is below. Re-read the shared arrays before appending, since Phases 96 to 98 already grew them.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `Views/Shared/_Layout.cshtml` (+ `.Mobile`) `<html data-theme>` seam | layout | request-response | none (see No Analog Found); token block `wwwroot/css/tokens.css:183-205` | no analog |
| `Views/Shop/Index.cshtml` / `.Mobile` | view | request-response | `Views/Events/Index(.Mobile).cshtml` (Phase 97) | role-match |
| `Views/Shop/Details.cshtml` / `.Mobile` | view | request-response | `Views/Characters/Details.cshtml:23` (confirm module section) | role-match |
| `Views/Shared/_ShopItemDetailsContent.cshtml` | partial | request-response | `Views/Shared/_CharacterAvatar.cshtml` (placeholder tile, Phase 98) | role-match |
| `Views/ShopManagement/{Index,Create,Edit}` (+ `.Mobile`) | view | CRUD | `Views/Characters/Details.cshtml`, `Views/Events/Create.cshtml` | role-match |
| `wwwroot/js/shop-item-sell.js` (new, delegated Sell total and confirm) | script | event-driven | `wwwroot/js/confirm-submit.js`, `wwwroot/js/row-link.js` | role-match |
| `wwwroot/js/shop-merchant.js` (new) | script | event-driven | `wwwroot/js/event-details.js` (data attrs, text only) | role-match |
| `wwwroot/js/shop-item-form.js` (new, pricing hints from 4 views) | script | event-driven | `wwwroot/js/quest-form.js`, `contact-tags.js` | role-match |
| `wwwroot/js/shop-item-modal.js` (new, fetch into `#itemDetailsBody`) | script | request-response | `wwwroot/js/agenda.js` (dialog fill from trigger) | role-match |
| Purchase-row Return/Sell forms | view markup | request-response | `Views/ShopManagement/Index.cshtml:88` (antiforgery form) | exact |
| `wwwroot/css/shop.css` + 5 phone sheets + legacy-pages Shop ranges into page sheets | stylesheet | n/a | `member-cards.css`, `contact-detail.css` (layered, tokens) | exact |
| Status label mapping (D-06) | helper | transform | none in repo; closest is the `switch` in `ShopManagement/Index.Mobile.cshtml` | partial |
| Item type display name view-model property | model | transform | RESEARCH "Item type display name" | partial |
| `Tests/ShopConversionGuardTests.cs` (new) | test | file scan | `Tests/CharactersConversionGuardTests.cs` | exact |
| Shop render tests (phone parity, Q-56/58/63-66) | test | request-response render | `Tests/AgendaConversionTests.cs` | exact |
| Rewritten guards (`MobileViewsTests` Shop entries, `BoardPageStaticGuardTests`, `BoardPageGateMatrixTests`, Shop controller integration tests) | test | render / file scan | themselves | exact |
| `UiFoundationLayerGuardTests.cs` arrays | test | file scan | itself (`LayeredFiles` line 54, `NoLiteralFiles` line 103) | exact |
| `~/qb-ui-review/phase-99/tools/*` | tooling | batch | `~/qb-ui-review/phase-98/tools/*` | exact |

## Pattern Assignments

### 1. Theme seam: `ViewData["Theme"]` to `<html data-theme>` (no analog)

Verified: no layout reads `ViewData["Theme"]`, and no view sets it (`<html lang="en">` is plain at `_Layout.cshtml:6`, `_Layout.Mobile.cshtml:6`, `_Layout.GroupPicker.cshtml:2`). This phase creates the seam. Token side already exists (`tokens.css:183-205`):
```css
[data-theme='shop'] { --qb-board-fallback: #0d1126; ... --qb-action: #2f5f9e; ... --bs-dark-rgb: 18, 22, 43; }
```
Header comment (`tokens.css:184-186`) states it must sit on `<html>` to recolour the page fallback. Closest code shape for a layout hook: how `_Layout.cshtml` already reads `ViewData["Title"]` (line 10). Both `_Layout.cshtml` and `_Layout.Mobile.cshtml` must emit the attribute, and a render test must assert it for Shop and ShopManagement under both UAs, and absence elsewhere. `shop.css` leaves the global list at `_Layout.cshtml:25` (and `legacy-pages.css:24` stays until its Shop ranges are gone).

### 2. Delegated module for fetched HTML and confirm (D-05)

**Confirm module** (`wwwroot/js/confirm-submit.js:1-12`, reuse as is):
```js
document.querySelectorAll('form[data-confirm-message]').forEach(function (form) {
    form.addEventListener('submit', function (event) {
        if (!confirm(form.dataset.confirmMessage)) { event.preventDefault(); }
    });
});
```
Caveat: this binds forms present at load. Forms inside the fetched modal are not present, so the sell module must use a document-level delegated `submit` listener (RESEARCH Pattern 2) rather than reuse this file for the modal. The management lists and the purchase history rows are server-rendered, so `confirm-submit.js` serves them. View side: build the sentence in the Razor block and write `data-confirm-message="@msg"` (analog `Views/Events/Details.cshtml:78,87`; load via `<script type="module" src="~/js/confirm-submit.js" asp-append-version="true"></script>` in `@section Scripts`, as `Views/Characters/Details.cshtml:23`, `Views/Contacts/Details.cshtml:214`).

**Module header and rules** (copy tone of `confirm-submit.js:1-4`): "nothing is exposed globally and no markup is built from strings". Merchant quote is inserted with `textContent`.

### 3. Return/Sell row forms (A-shop-002, pitfall 1)

Current markup `Views/Shop/Index.cshtml:114-135` hides the forms (`div.return-sell-buttons position-absolute`) and its two forms carry NO antiforgery token. Once un-hidden they must post with one. Analog with token inside a form: `Views/ShopManagement/Index.cshtml:88`, `Views/Shop/Details.Mobile.cshtml:34`:
```cshtml
<form method="post" action="@Url.Action("Sell", "Shop")">
    @Html.AntiForgeryToken()
    <input type="hidden" name="id" value="@purchase.Id" />
    <input type="hidden" name="quantity" value="@purchase.RemainingQuantity" />
    <button type="submit" class="btn ...">Return (@purchase.RemainingQuantity)</button>
</form>
```
`d-inline` goes to a layered class (no inline style). Remove `.return-sell-buttons { opacity:0; visibility:hidden }` from `legacy-pages.css` (the former `site.css`, renamed in Phase 94) and its copy in `shop.css`. Phone `Index.Mobile` gains the same section (Q-56), one structure under the grid and paging links (D-10). Dates there already use `Html.LocalTime(purchase.TransactionDate, "date-compact")` (`Shop/Index.cshtml:109,155`; `ShopManagement/Index.cshtml:148`), which is correct for real instants; keep.

### 4. Dates: `Html.LocalTime` vs `Html.WallClock` (A-shop-038)

Instant: `@Html.LocalTime(item.DeniedAt.Value, "date")` (`ShopManagement/Index.cshtml:148`). Wall-clock: `@Html.WallClock(x.ToDateTime(TimeOnly.MinValue), "weekday-long-year")` (`Views/Events/Details.cshtml:50`). Availability start and end dates on items are date pickers (no UTC instant), so they match WallClock; helper is in `HtmlHelperExtensions.cs` (~line 33). Do not change the helpers.

### 5. Sheets: one layered page sheet per area

Analog `wwwroot/css/member-cards.css` and `contact-detail.css`: open with `@layer pages {`, tokens only, no hex/rgb, no `!important`, no `transition: all`, `.mobile-layout`-scoped phone rules (UA, not width; RESEARCH Pitfall 11). Linked from each view by `@section Styles { <link rel="stylesheet" href="~/css/shop.css" asp-append-version="true" /> }` (`Views/Events/Create.cshtml:9-11`). A partial cannot render `@section Styles` (Pitfall 7), so the sheet is linked by the pages that render `_ShopItemDetailsContent`, including Shop/Index for the modal. Append to `UiFoundationLayerGuardTests` `LayeredFiles` (line 54) and `NoLiteralFiles` (line 103); remove deleted sheet names in the same commit. Retire: `shop.mobile.css`, `shop-details.mobile.css`, `shop-management-{index,create,edit}.mobile.css` (list in a new `RetiredFiles` if merged). Pitfall 6: unlayered legacy rules beat layered ones, so delete the old rules in the same slice.

### 6. Icon/tile sizing (D-07): safe analog is the v6.3.2 details-page fix

The emblem tile has no picture, so no crop, but size it so it cannot distort. Fixed shape (`git show v6.3.2:QuestBoard.Service/wwwroot/css/contact-detail.css`, lines 20-34 of that version):
```css
.contact-detail-page .contact-detail-portrait { height: auto; margin: 0 auto var(--qb-space-md); border-radius: var(--qb-radius-md); }
@media (max-width: 575.98px) { .contact-detail-page .contact-detail-portrait { max-height: 220px; } }
.contact-detail-page .contact-detail-portrait--placeholder {
    width: 100%; max-width: 248px; aspect-ratio: 1 / 1;
    display: flex; align-items: center; justify-content: center;
    background-color: var(--qb-neutral); color: var(--qb-neutral-fg);
}
```
Lesson: v6.3.0 used a fixed height plus `object-fit: cover` and cropped the picture; v6.3.1 and v6.3.2 switched to `height: auto` with a `max-height` cap. The excerpt above is an analog for the picture host only. **For the shop emblem tile, 99-UI-SPEC.md governs and is stricter: no `aspect-ratio` and no `object-fit` anywhere in shop sheets, views or scripts (guarded by a scanner with a self-test).** Build the tile as a `min-block-size` block (144px desktop, 120px phone) with flex centring, a glyph smaller than the tile, no `overflow: hidden`, and `aria-hidden="true"` on the icon. Rarity tint goes through a modifier class per rarity (colour tokens), never an inline `style=`. Markdown images in descriptions get `max-inline-size: 100%; block-size: auto`.

### 7. Status labels (D-06)

Current drift: `ShopManagement/Index.Mobile.cshtml` uses a switch to "Pending Review"/"Active"; desktop `Index` shows "Draft"/"Available"; Edit views use `Model.Status.ToString().Replace("Published", "Available")` (`ShopManagement/Edit.cshtml:35`) next to a badge switch (`:30`: `ItemStatus.Published => "bg-success"`). No shared helper exists in the repo, so create one (a static display extension on `ItemStatus` or a view-model property), returning "Pending Review", "Available", "Archived", "Denied", and have all six views call it. Guard test: no view contains `.Replace("Published"` and none renders the raw `Status.ToString()`.

### 8. Modules from view inline scripts (merchant, pricing, bulk, deny, preview)

Data attributes in, text out. Analog `wwwroot/js/event-series-form.js` header (`data-collision-url`), `wwwroot/js/agenda.js` (copies values from the trigger into the dialog as field values and text content). Token for any `fetch` POST: `<meta name="request-verification-token">` (read as in `quest-manage.js:1-10`). Merchant skeleton and Return/Sell row example are in RESEARCH "Code Examples"; the toast is a Bootstrap Toast with `role="status"` and `textContent`, kept visible on hover and focus (D-02). Modal fill (`shop-item-modal.js`): clear `#itemDetailsBody`, fetch `/Shop/Details/{id}?isModal=true`, insert, rely on the document-level sell handlers. Pitfall 8: measure only once the modal is shown. Pitfall 3: item names with apostrophes go through data attributes, not inline JS strings. Pitfall 4: select the form by id, not `document.querySelector('form')`.

### 9. Bootstrap modal: Deny, Bulk and Preview dialogs

Dialog analog: `#itemDetailsModal` in `Shop/Index.cshtml` and the Phase 98 `_ImageCropDialog` (opaque modal, `.modal-content` on the foundation surface; no `bg-dark text-light`, no `style.display`, toggle `hidden`). Preview (D-03) builds the card in the browser from form values using `textContent` and cloned `<template>` content (no `innerHTML`; the guard below forbids it). Bulk Actions `alert()` calls move into the module with today's wording unchanged.

### 10. Guard tests

**Analog:** `QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs` (870 lines): copy `ConvertedViews` (line 42), `ConvertedSheets` (68), `ViewSheetLinks` (72), `RetiredFiles` (99), `PageOnlySheets` (121), `InlineCodePatterns` (123), `PlanningReferencePatterns` (223, written as fragments so the test never contains forbidden strings), `PhaseScope_IsFullyConverted` (`AreaViewFolders` line 672: set to `["Shop", "ShopManagement"]`, `AreaSharedPartials = ["_ShopItemDetailsContent.cshtml"]`), `Layouts_LinkNoAreaSheet`, the repo-root helpers `ResolveRepoFile`/`FindRepositoryRoot` (lines 19-37), plus the card-title typography fact (`CssRule`, `TitleTypography`, lines 720-790) and the raw-date-format fact (`DateFormatCall`, line 803). Module facts, copy `EventsConversionGuardTests.cs:495-521`:
```csharp
var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/agenda.js");
Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\b|insertAdjacentHTML|document\.write")
    .Select(m => m.Value).Should().BeEmpty("agenda.js must build no markup from strings");
```
Inline-code regexes (`UiFoundationMobileShellGuardTests.cs:105-108`): `\sstyle\s*=`, `<style\b`, `<script\b(?![^>]*\bsrc=)[^>]*>`, `\son[a-z]+\s*=`. Additional Shop-specific facts: no `onclick="showMerchantDialog` and no `.return-sell-buttons`; the layout list has no `shop.css`; Return/Sell forms carry an antiforgery token; `Details` link in `ShopManagement/Edit*` targets `/Shop/Details/`. Missing files are skipped, never removed from guards.

**Render tests:** `Tests/AgendaConversionTests.cs:22-34,120-122` (`MobileUserAgent` const, `AgentFor(bool mobile)`, `[Theory][InlineData(false)][InlineData(true)]`) for phone parity: purchase history, paging line, empty states, Availability card, Sell on phone Details, stock `max` and "Max: N available". Test data: `AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "name", "name@test.com")`, `TestDataHelper.ClearDatabaseAsync`, unique usernames per class. Existing guards to rewrite to the same intent: `MobileViewsTests` Shop/ShopManagement entries (about lines 716-733 and 1123; line numbers may have shifted), where the purchase-panel omission facts flip.

### 11. Scratch tooling under `~/qb-ui-review/phase-98/tools` (copy into `phase-99/tools`, then adapt)

Nothing is committed; no review branch. Set `QBR` (default in `run-app.mjs:19` is `~/qb-ui-review/phase-98`) and `ROOT` (`login.mjs`) to `phase-99`; replace `p98*` role keys with `p99player`, `p99dm`, `p99empty`.

| Tool | Role and key facts |
|---|---|
| `run-app.mjs` | `node run-app.mjs <src-dir> <port> [--restart] [--env K=V]`. Starts the service in Development on the port, logs to `$QBR/logs/app-<port>.log`, waits for `/health`. Forces `EmailSettings__SuppressSending=true` and SMTP `127.0.0.1:1025` (Mailpit), refuses overrides, never stops a listener it did not start. Pick a free port pair first (D-discretion). |
| `stop-app.mjs` | `node stop-app.mjs <port>`; stops only the pid recorded by `run-app.mjs`. |
| `login.mjs` | `node login.mjs <role> [--port N] [--board <name>]`; saves Playwright `storageState` per port (auth cookie key ring is scoped to the content root, so state is per tree and port). |
| `register98.mjs` | Creates accounts through the app's Admin > Create user and Reset password pages as the Audit Board admin; idempotent; passwords only into `credentials.json` (mode 600), never stdout. Copy as `register99.mjs` with the three p99 accounts. Pitfall 12: Admin account creation queues Welcome email jobs (Mailpit only). |
| `fixtures.mjs` + `fixlib.mjs` | Idempotent fixture creation through the app's own forms (`session`, `loadFixtures`, `saveFixtures`, `fillMd`, `idByTitle`; `fixtures.json` shape `{ key: { id, url, board, role, expect, created, declared?, unavailable?, reason? } }`; steps arg; `--check <keys|all> --out <file>` loads each URL as its role under both UAs). Replace steps with: boards, items (every rarity, type, status incl. Denied with reason, stock states, long names, dates, D&D Beyond link, enough to page), purchases (made shortly before captures that need Return). Gold must come from an app form; if none, stop and ask. |
| `audit-shoot.mjs`, `page-shoot.mjs`, `phone-shoot.mjs`, `gate-shoot.mjs`, `frames-shoot-98.mjs`, `phone-before.mjs` | Capture (`stateFor(role, port)` exported from `audit-shoot.mjs`); desktop 1440x900 and `devices['Pixel 7']`. `shopmodal.mjs` and `uat102-shop.mjs` are shop-specific starting points (modal fetch capture). |
| `manifest.mjs`, `register.mjs`, `before-summary-98.py` | Capture manifest (before/after pairs); register BEFORE paths by manifest id. |
| `build-gate1.py` / `build_gate1.py`, `build_gate1_img.py`, `build-look-mockups.py`, `build-look-pairs.py`, `look-check-98.mjs`, `lookcheck.mjs`, `mockup-shoot.mjs`, `gate1-shoot.mjs`, `serve-v1.mjs` | Gate 1 package (`gate1/index.html`, `README.md`, `img/`); look checks compare computed styles (`backgroundImage`, `backdropFilter`, `boxShadow`, `color`, `fontFamily`) to the Direction A values. Reference renders: `~/qb-ui-review/phase-94/mockups/direction-a/png/shop-a-{1440,820,390}.png`, `shop-a--empty-*.png`, `*-report.json`. |
| `build-gate2.py`, `gate2-sheet.mjs`, `gate2-crops.mjs`, `gate2-images.py`, `gate-hygiene.py`, `gate-open.mjs`, `pair-compare.py` | Gate 2 sheet: one self-contained HTML (one CSS block, relative images, no script, no external link, opens from `file://`); partial sheets via `--pages ids --ua --widths`; contrast and target-size exceptions as explicit accept/reject rows. `build-gate2.py` docstring is the contract. |
| `contrast.mjs`, `a11y-*.mjs`, `a11y-table-98-*.py`, `tab-order-98-11.mjs`, `confirm-check-98-11.mjs`, `focus-frames-98-22.mjs`, `pixel-check-98-22.mjs` | Contrast, focus, tab-order and confirm checks; `confirm-check-98-11.mjs` is the pattern for proving native `confirm()` wording on the new forms. |
| `check-sheet.mjs`, `selector-scan.py`, `legacy-collision.py` | Find unlayered legacy rules that collide with new layered ones (Pitfall 6). |

## Shared Patterns

- **Foundation layers and tokens:** `tokens.css`, `base.css`, `components.css`, `shell.css`, `shell.mobile.css`; area sheets in `@layer pages`. Focus ring for focusable elements lives in `base.css` (no per-page `:focus-visible` unless a stretched link needs it).
- **No inline code:** enforced by the new `ShopConversionGuardTests`; modules `type="module"` in `@section Scripts`.
- **Antiforgery:** every `method="post"` form carries `@Html.AntiForgeryToken()`; script posts read `meta[name=request-verification-token]`.
- **Confirmations:** `data-confirm-message` plus `confirm-submit.js`, wording byte-identical to today.
- **Dates:** instants through `Html.LocalTime`, wall-clock through `Html.WallClock`; no raw `ToString`/`ToString("...")` on dates (guard analog `CharactersConversionGuardTests.cs:803`).
- **Comments:** plain-language why; no requirement, phase, plan or finding IDs and no planning document names in views, CSS, JS or C#.
- **Writes to `Events`/`Quests`** are not touched by this phase (Shop only), so the change-tracker rule is not engaged.
- **Release:** v6.4.0 via PR at the end, after both gates; no review branch.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `ViewData["Theme"]` to `<html data-theme>` in the layouts | layout | request-response | No layout or view uses the seam yet; only the token block exists. Planner follows RESEARCH Pattern 1. |
| Shared status-label helper (D-06) | helper | transform | Only per-view switches and `.Replace` exist; create one mapping. |
| Merchant toast with polite live region, hover/focus pause and capped bob | script + partial | event-driven | Nearest toast is the foundation toast (`UiFoundationToastGuardTests`); use RESEARCH "Merchant module skeleton". |
| Live Preview card dialog from form values | script + partial | event-driven | No in-repo preview built from form fields; use `<template>` cloning and `textContent`. |

## Metadata

**Analog search scope:** `QuestBoard.Service/{Views,wwwroot/css,wwwroot/js}`, `QuestBoard.IntegrationTests/Tests`, `~/qb-ui-review/phase-98/tools`, git tags v6.3.1 and v6.3.2, Phase 98 PATTERNS.
**Files scanned:** about 30 read or grepped; RESEARCH inventory sections (current state per file, guard tests, merchant, pricing, bulk) are authoritative for line numbers in the Shop views, and were not re-verified here beyond the excerpts above.
**Pattern extraction date:** 2026-11-15
