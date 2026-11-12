# Phase 97: Events, Series, Calendar and Agenda UI conversion - Pattern Map

**Mapped:** 2026-11-12
**Files analyzed:** 36 new/modified (17 views, 6 sheets, 5 new modules, 1 helper, 1 controller, 6 test files)
**Analogs found:** 34 / 36 (two no-analog items listed at the end)

All paths are repository-relative. Excerpts were read from the checkout on 2026-11-12 while a Phase 96 executor was running; line numbers drift only in files Phase 96 touches (see "Phase 96 contention").

## Phase 96 contention (re-read at execution)

| Shared file | Phase 96 status when read | Phase 97 action |
|---|---|---|
| `QuestBoard.Service/wwwroot/css/shell.mobile.css` | The phone `.row` gutter rule is NOT there yet (no `main > .row` / `gutter` match). Phase 96 plans 03/04/08/09/10 carry it, gated by Gate 1 answers (`~/qb-ui-review/phase-96/gate1/answers.json`) | RE-READ at execution. If landed, only verify `Events/Details` at 390px (A-events-019). If Phase 96 scoped it to `AccessDenied`, apply the same selector to `Events/Details`. Do not duplicate the rule |
| `QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs` | `LayeredFiles` (lines 54-75) and `NoLiteralFiles` (79-80) currently end at Phase 95 names; Phase 96 appends `account.css` | APPEND only; re-read arrays first |
| `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` | Phase 96 rewrites Account/GroupPicker facts | Edit only the Calendar/Agenda/Events regions; re-read |
| `QuestBoard.Service/Views/Shared/AccessDenied.cshtml`, `wwwroot/css/legacy-pages.css`, `account.css` | Phase 96 owns | Do not touch; only read to learn the gutter fix |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` lines 25-27 (global `calendar.css`, `events-overview.css`, `agenda.css`) | Phase 96 may remove other legacy links nearby | Re-read before deleting the three links |
| `components.css` | Phase 95 added `.move-control-reason`; Phase 96 may add rules | Re-read before adding the shared `.avail-*` rules |
| `~/qb-ui-review/phase-96/tools/` | Phase 96 is editing it | Copy from `phase-95/tools` (stable) for Phase 97; use `phase-96/tools` as a read-only reference only |

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `Controllers/QuestBoard/CalendarController.cs` (add `ViewBag.BoardToday`) | controller | request-response | `Controllers/QuestBoard/QuestController.cs:420` | exact |
| `Views/Shared/_Calendar.cshtml` (today compare, compact branch) | view/partial | request-response | `Views/Quest/Details.cshtml:16` (board-today cast) | role-match |
| `Views/Calendar/Index.Mobile.cshtml` (today marker, stretched links) | view | request-response | `Views/Quest/Index.Mobile.cshtml:11` + `home.mobile.css` stretched link | role-match |
| `Extensions/HtmlHelperExtensions.cs` (4 new format entries) | utility | transform | itself, lines 31-37 | exact |
| `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` (list appends) | test | file scan | itself, lines 14-100 | exact |
| new `Controllers/CalendarBoardClockTests.cs` (today behaviour) | test | request-response | `Controllers/QuestStatusBoardClockTests.cs` | exact |
| new unit test for four new styles | test | transform | `QuestBoard.UnitTests/.../WallClockMarkupTests.cs` (do not edit it; copy shape) | exact |
| 18 date-line moves in 9 views | view | transform | `Html.WallClock` call sites in `Views/Quest/Details.cshtml` | exact |
| `wwwroot/js/calendar-month.js` (slots, "+N more", card, non-modal panel) | script (ES module) | event-driven | `wwwroot/js/quest-details.js` (module shape), `quest-board.js:25-48` (CSS-var positioning), mockup `calendar-fit.js` (logic) | role-match |
| `wwwroot/js/event-details.js`, `event-series-form.js`, `agenda.js`, `row-link.js` | script (ES module) | event-driven | `wwwroot/js/quest-details.js`, `quest-manage.js` | role-match |
| `wwwroot/css/calendar.css`, `calendar.mobile.css`, `events-overview(.mobile).css`, `agenda(.mobile).css` | stylesheet (`@layer pages`) | n/a | `wwwroot/css/quest-vote.css`, `home.mobile.css`, `group-picker.css` | exact |
| `wwwroot/css/components.css` (shared `.avail-*`, 44px cycle cell) | stylesheet | n/a | itself | exact |
| `Views/Shared/_LinkedDayEntries.cshtml`, `_LinkedAlsoThatDay.cshtml` | partial | request-response | themselves (hidden-text pattern line 24, 48) | exact |
| `Views/Events/Details.cshtml` (reorder, no inline code, gutter) | view | CRUD | `Views/Quest/Details.cshtml` + `quest-details.js` | role-match |
| `Views/Events/Create|Edit.cshtml`, `_SeriesFormScripts.cshtml`, `Series/Details.cshtml` | view | CRUD | `Views/Quest/Create.cshtml` + `_QuestFormScripts.cshtml` (token meta) | role-match |
| `Views/Agenda/Index(.Mobile).cshtml`, `Events/Index(.Mobile).cshtml`, `_AvailabilityCell/Counts` | view | request-response | `Views/Quest/Index.Mobile.cshtml` | role-match |
| new `Tests/EventsConversionGuardTests.cs` | test | file scan | `Tests/QuestsConversionGuardTests.cs` | exact |
| `UiFoundationLayerGuardTests.cs` arrays | test | file scan | itself | exact |
| rewritten guards (`LinkedBoard*`, `EventsOverviewMobileStyleTests`, `EventDetailsAvailabilityRenderTests`, etc.) | test | file scan / render | themselves | exact |
| `AccessDenied` gutter fix applied to `Events/Details` | css | n/a | Phase 96 `shell.mobile.css` rule | pending |
| scratch gate-sheet tooling (`~/qb-ui-review/phase-97/tools/`) | scratch | batch | `~/qb-ui-review/phase-95/tools/{run-app,login,fixtures,audit-shoot,sweep,build_gate1,gate2-sheet,probes,contrast}` | exact |

## Pattern Assignments

### 1. Board-clock today seam: `CalendarController.Index` + `_Calendar.cshtml` (controller + partial, request-response)

**Analog (controller):** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:414-420`

```csharp
ViewBag.IsDetailsPage = true;
ViewBag.CurrentQuestId = id;
ViewBag.CurrentUserId = currentUser?.Id;
ViewBag.BoardType = await GetActiveBoardTypeAsync(token);
ViewBag.BoardToday = boardClock.Today;
```
Other setters: `QuestController.cs:72`, `:981`, `AdminController.cs:431`. `CalendarController.cs` already injects `IBoardClock boardClock` (line 23) and reads `boardClock.Now` at line 31; add the single line `ViewBag.BoardToday = boardClock.Today;` before `return View(calendarModel);` (line 110) with a plain-language comment (container clock is UTC). Do not touch the calendar-feed files.

**Analog (view cast):** `Views/Quest/Details.cshtml:16`, `Admin/Quests.cshtml:12`, `Quest/Index.Mobile.cshtml:11`:
```cshtml
var boardToday = (DateOnly)ViewBag.BoardToday;
```
Hard cast on purpose (missing value fails loudly). Replace `_Calendar.cshtml:33` `day.Date.Date == DateTime.Today` with `DateOnly.FromDateTime(day.Date) == boardToday`. Quest/Details already sets it, so the shared partial works on both hosts. The phone `Calendar/Index.Mobile.cshtml` needs the same cast for its today marker (Q-46); `_Calendar.Mobile.cshtml` stays negative-list unless it gains a marker.

**Behaviour test analog:** `QuestBoard.IntegrationTests/Controllers/QuestStatusBoardClockTests.cs` (copy structure, new class `CalendarBoardClockTests`):
```csharp
public class QuestStatusBoardClockTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
private const string BoardZoneId = "Europe/Amsterdam";
// 23:30 UTC on the 5th is 01:30 board time on the 6th
private static readonly DateTimeOffset PinnedNow = new(2026, 9, 5, 23, 30, 0, TimeSpan.Zero);
private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

private WebApplicationFactory<Program> CreatePinnedFactory() => factory.WithWebHostBuilder(builder =>
{
    builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["TimeZone:BoardTimeZoneId"] = BoardZoneId }));
    builder.ConfigureTestServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedTimeProvider(PinnedNow)); });
});
// facts: await TestDataHelper.ClearDatabaseAsync(factory.Services); var pinnedFactory = CreatePinnedFactory();
// var (client, admin) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(pinnedFactory, "qsbc_admin1", "qsbc_admin1@example.com");
// var response = await client.GetAsync("/Calendar?year=2026&month=9", TestContext.Current.CancellationToken);
```
Mobile UA const (lines 20-21): `"Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) ..."`. Assertions: day cell for the 6th has class `today`, the 5th does not, desktop and phone marker. Use unique usernames per class (`cbc_admin1`). Keep this class separate from the restyle commit.

---

### 2. `Html.WallClock` format-table additions (utility, transform)

**Analog:** `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs:31-37` (table) and `:91-103` (`BuildWallClock`):
```csharp
private static readonly IReadOnlyDictionary<string, string> LocalTimeFormats = new Dictionary<string, string>
{
    ["date"] = "MMM d, yyyy",
    ["date-time"] = "MMM d, yyyy, h:mm tt",
    ["date-compact"] = "MMM d",
    ["date-time-compact"] = "MMM d, h:mm tt",
};
...
internal static IHtmlContent BuildWallClock(DateTime wallClock, string style)
{
    if (!LocalTimeFormats.TryGetValue(style, out var format)) throw new ArgumentOutOfRangeException(nameof(style), style, "Unrecognised wall-clock style.");
    var tag = new TagBuilder("time");
    tag.MergeAttribute("class", "wall-clock");
    tag.MergeAttribute("data-style", style);
    tag.MergeAttribute("datetime", wallClock.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
    tag.InnerHtml.SetContent(wallClock.ToString(format, CultureInfo.InvariantCulture));
    return tag;
}
```
Add only (append four entries, leave the four existing and both builders untouched): `["time-24h"] = "HH:mm"`, `["weekday-short"] = "ddd, MMM d"`, `["weekday-long"] = "dddd, MMMM d"`, `["weekday-long-year"] = "dddd, MMMM d, yyyy"`. No `site.js` change: unknown styles are left as server-rendered. Update the table comment (it currently says the table is "kept in sync with the client-side table"; it now also holds server-only styles) and `.claude/ui-guidelines.md` "Rendering dates and times". Call-site conversion for `DateOnly`/`TimeOnly`: `Html.WallClock(row.Date.ToDateTime(TimeOnly.MinValue), "weekday-short")`, `row.Date.ToDateTime(row.StartTime.Value)` for time; no new overload. Phone day label: render mixed-case, keep existing `text-uppercase` class. `_Calendar.cshtml:76` (`title="..."`): use a `QuestOnDay.TimeLabel` modelled on `LinkedQuestOnDay.TimeLabel` (`When.ToString("HH:mm", CultureInfo.InvariantCulture)`).

**Test analog:** copy the shape of `WallClockMarkupTests` into a NEW file (existing file stays unchanged); for each new style assert the exact text and `datetime`, plus the "unrecognised style throws" shape.

---

### 3. `AmbientClockSeamTests` list appends (test, file scan)

**Analog:** `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` itself.

`GuardedRelativePaths` (lines 15-48, alphabetical by group; Views entries end at `Views/Series/Details.cshtml`): append
```csharp
"QuestBoard.Service/Views/Calendar/Index.cshtml",
"QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml",
"QuestBoard.Service/Views/Shared/_Calendar.cshtml",
"QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml",
```
`RoutedTodayViewTokens` (lines 94-103): append, same `"ViewBag.BoardToday"` token shape as the quest views:
```csharp
["QuestBoard.Service/Views/Shared/_Calendar.cshtml"] = "ViewBag.BoardToday",
["QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml"] = "ViewBag.BoardToday",
```
`_Calendar.Mobile.cshtml` and `Calendar/Index.cshtml` are guard-only (no token) unless they consume today. `CalendarController.cs` already appears in `GuardedRelativePaths` (line 33) and `BoardClockConsumerPaths`. The list only grows; never remove an entry. `StripComments` removes `@* *@`, `/* */` and everything after `//` on a line (so a URL line hides its tail from the scan, harmless). Comments in the new views must avoid literal ambient reads and planning IDs.

---

### 4. Non-modal dialog / popover ES module: `wwwroot/js/calendar-month.js` (script, event-driven)

**File-shape analog:** `wwwroot/js/quest-details.js` (lines 1-50): a header comment explaining it is shared, wiring from data attributes, "nothing is exposed globally and no markup is built from strings (nodes are created with createElement and textContent)", plus the token helper:
```js
function antiforgeryToken() {
    const meta = document.querySelector('meta[name="request-verification-token"]');
    return meta ? meta.content : '';
}
```
Loaded as `@section Scripts { <script type="module" src="~/js/quest-details.js" asp-append-version="true"></script> }` (`Views/Quest/Details.cshtml:977`). Use the same for `calendar-month.js`, `event-details.js`, `event-series-form.js`, `agenda.js`, `row-link.js`.

**Positioning analog (CSS custom properties, no inline style text):** `wwwroot/js/quest-board.js:25-48`:
```js
poster.style.setProperty('--poster-x', leftOffset + column * (posterWidth + gap) + 'px');
container.style.setProperty('--board-height', padTop + ... + 'px');
function layoutSafely() { try { layoutMasonry(); } catch { /* leave CSS column flow */ } }
```
Use `style.setProperty('--pop-x'/'--pop-y')` for the card and panel, and a `try/catch` fallback that leaves every entry visible.

**Logic analog (outside repo):** `~/qb-ui-review/phase-94/mockups/calendar-fit/calendar-fit.js` (card, panel, focus return, Esc suppression, tap logic, outside click, Tab loop; 46 behaviour checks pass). Port per RESEARCH 7a/7c: drop render/buildCell (server renders), drop hover mode and switcher (D-01), `WeakMap` button-to-cell instead of `moreData`, read `--cal-slots` via `getComputedStyle` (0 = no fit rule below 992px), set `hidden` on overflow items, `cloneNode(true)` of the server `.cal-entry-detail` into the one `aria-hidden` card, panel is `div role="dialog" aria-modal="false" aria-labelledby`, focus returns to "+N more". Static linked entries (D-06) get no handlers, no data attributes. No `innerHTML`.

**Static guard analog for the module:** `QuestManageModule_WritesNoMarkupFromStrings` in the quest guards (find with `grep -rn "WritesNoMarkupFromStrings" QuestBoard.IntegrationTests`); add the equivalent for `calendar-month.js` in `EventsConversionGuardTests`.

---

### 5. Layered area sheets and `@section Styles` linking (stylesheet, `@layer pages`)

**Analogs:** `wwwroot/css/quest-vote.css` (opens with a plain-language header comment then `@layer pages {`), `wwwroot/css/home.mobile.css`, `wwwroot/css/group-picker.css`.

Stretched row link (phone Calendar rows, Events/Index.Mobile cards, Agenda cards) from `home.mobile.css:57-66`:
```css
/* One link per card, stretched over the whole card so the whole card is the target. */
.quest-card-mobile .row-nav-link::after { content: ""; position: absolute; inset: 0; }
/* The ring is drawn on the card, not on the link text alone; the link itself shows none. */
.quest-card-mobile:has(.row-nav-link:focus-visible) { outline: var(--qb-focus-width) solid var(--qb-focus-outer); ... }
```
Keep the anchor at `min-height: var(--qb-hit-min)` (home.mobile.css:52-54). Focus ring for `.row-nav-link` already lives in `base.css:135`; no per-page `:focus-visible` rules. Rules: tokens only (`--qb-warning/-fg`, `--qb-success`, `--qb-event`, `--qb-neutral`, `--qb-surface-overlay`, `--qb-shadow-overlay`, `--qb-hit-min`), no hex/rgb, no `!important`, no `transition: all`. `.calendar-page`-scope the month rules so they never reach Quest Details (which keeps `.details-page` rules and links `calendar.css` itself, `ViewSheetLinks` in `QuestsConversionGuardTests`). The weekday-header 4.5:1 fix (D-08) is the one deliberately shared rule.

Link from a view (analog `Views/Quest/Index.Mobile.cshtml:13-15`):
```cshtml
@section Styles {
    <link href="~/css/home.mobile.css" asp-append-version="true" rel="stylesheet" />
}
```
Then delete lines 25-27 of `Views/Shared/_Layout.cshtml` (`calendar.css`, `events-overview.css`, `agenda.css`) in the slice that links them per-view; `legacy-pages.css` must still exist (layout guard needs one legacy global).

Slot-count custom property (RESEARCH, new, no analog): `@media (min-width: 992px)` plus `min-height` bands 712/850/980 setting `--cal-slots` on `.calendar-page`.

---

### 6. Date-line, inline-code and token patterns in the Events views (views)

**Token for scripts:** `Views/Quest/_QuestFormScripts.cshtml` emits `<meta name="request-verification-token">`; `Events/Details` and `Series` need their own meta (RESEARCH Pitfall 9), replacing `@tokens.RequestToken` in inline script at `Events/Details.cshtml:200,296`, `Create.cshtml:121`, `Edit.cshtml:138`.

**Confirm wording:** `quest-manage.js:12` reads `button.dataset.confirmMessage`; copy for `event-details.js`. Messages keep today's wording, built in the view's C# block (`Events/Details.cshtml:20-21`) and written to `data-confirm-message`.

**Modals:** keep the Bootstrap data-api modal pattern (Phase 96 map: `Views/Account/Profile.cshtml:134, 183-210`, shipped as Rename dialog; fix `aria-labelledby` and close-button name in `Series/Details`).

**Empty states:** `.empty-state` structure in `components.css`, copy verbatim from `94-AUDIT-events.md`.

**Linked-entry hidden text (keep):** `Views/Shared/_LinkedDayEntries.cshtml:24, 48`:
```cshtml
<span class="visually-hidden">@(item.Entry.BoardName). @(item.TimeLabel).@(item.Entry.IsCancelled ? " Cancelled." : "")</span>
<span class="visually-hidden">@(item.Entry.BoardName). DM @(item.Entry.DungeonMasterName). @(item.TimeLabel). @(item.IsFinalized ? "Finalized." : "Proposed.")</span>
```
Apply the same shape to the key warning, finalized check and viewer's vote in `_Calendar` (A-events-016) and add `aria-hidden="true"` to the decorative icons. The linked partials must still pass `LinkedBoardStaticGuardTests` (no `<button>`, `id=`, `for=`, `data-*`, the word "vote", `Html.Raw`).

---

### 7. `Tests/EventsConversionGuardTests.cs` (new, test, file scan)

**Analog:** `QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs` (read lines 1-125). Copy:
- the repo-root helpers `ResolveRepoFile` / `FindRepositoryRoot` (lines 22-37, walk up to the `*.slnx` or `.git`);
- the four list shapes: `ConvertedViews` (string[] of repo-relative `.cshtml` paths), `ConvertedSheets` (names under `CssRoot = "QuestBoard.Service/wwwroot/css"`), `ViewSheetLinks` (`Dictionary<string, string[]>`, e.g. `["QuestBoard.Service/Views/Events/Index.cshtml"] = ["events-overview.css"]`, `["QuestBoard.Service/Views/Calendar/Index.cshtml"] = ["calendar.css"]`, `["QuestBoard.Service/Views/Agenda/Index.cshtml"] = ["agenda.css"]`), and `PhaseSourceFiles` (planning-reference scan list; do not include the test file itself);
- the inline-code regexes from `UiFoundationMobileShellGuardTests.cs:105-108`:
```csharp
Regex.Matches(markup, @"\sstyle\s*=").Should().BeEmpty(...);
Regex.Matches(markup, @"<style\b").Should().BeEmpty(...);
Regex.Matches(markup, @"<script\b(?![^>]*\bsrc=)[^>]*>").Should().BeEmpty(...);
```
plus `\son[a-z]+\s*=` for handlers and a raw-date scan allowing `yyyy-MM-dd` (Events/Edit hidden machine value) and `yyyy-MM-ddTHH:mm`. Write planning-reference regexes as fragments so the test file never contains the forbidden strings (Phase 96 map, `PlanningReferencePatterns`). Existing files only (twins may be merged away).

`Tests/UiFoundationLayerGuardTests.cs`: append `("calendar.css","pages")`, `("calendar.mobile.css","pages")`, `("events-overview.css","pages")`, `("events-overview.mobile.css","pages")`, `("agenda.css","pages")`, `("agenda.mobile.css","pages")` to `LayeredFiles` (lines 54-75) and the same names to `NoLiteralFiles` (79-80). Phase 96 is editing both arrays; append, re-read.

**Rewritten guards (keep intent):** see RESEARCH 6c table; hook classes (`calendar-day`, `linked-entry--open|--static|--cancelled|--proposed|--finalized`, `linked-day-count`, `linked-entry-detail`, `avail-*`, `agenda-row|agenda-card`) are pinned by about 100 assertions, so keep them. Rewrite only: `DayCell` helper (splits on `class="calendar-day` and `<div class="day-number">`), `window.location.href` assertions in `LinkedBoardMobileCalendarTests` (:308,:313,:339), `onclick="setAvailability(` count in `EventDetailsAvailabilityRenderTests`, `event.stopPropagation()` count in `EventsOverviewControllerIntegrationTests:262`, `EventsOverviewMobileStyleTests` literal colours, `LinkedBoardCalendarStyleTests` selectors.

---

### 8. Phone `.row` gutter fix (AccessDenied) reused on `Events/Details`

**Analog:** Phase 96, not yet in the tree. Its planned shape (96-PATTERNS, `shell.mobile.css`):
```css
.mobile-layout main > .row { --bs-gutter-x: var(--qb-space-md); }
```
inside the existing `components` layer of `wwwroot/css/shell.mobile.css`, scoped under `.mobile-layout`. **Re-read at execution** (see contention table). Verify `Events/Details` at 390px has no horizontal overflow (A-events-019: bare `.row` in `.container-fluid`, 394 at 390). If Phase 96 scoped it to `AccessDenied` only, add the same selector for the Events/Details root; do not fork a second gutter mechanism.

---

### 9. Scratch gate-sheet tooling (`~/qb-ui-review/phase-97/tools/`)

Copy from `~/qb-ui-review/phase-95/tools/` (stable, Linux-proven, per `~/qb-ui-review/phase-96/ENVIRONMENT.md`), re-point paths; do not copy from `phase-96/tools` (in flux, read-only reference for Gate 1 sheet design). Phase 96 environment substitutions apply: app on 8000, frozen baseline on 8001, bundled `chromium.launch()`, `ss -ltnp`, `python3`, symlink `tools/node_modules` to `~/qb-ui-review/phase-94/tools/node_modules`, never start/stop `mssql-dev`, no GC env vars, no SQL writes.

| Need | Source tool |
|---|---|
| start/stop app and baseline | `phase-95/tools/run-app.mjs`, `stop-app.mjs` |
| login with `qb-audit-*` accounts | `login.mjs`, `register.mjs`, `credentials.json` (in scratch only) |
| fixtures through the app's forms | `fixtures.mjs`, `verify-fixtures.mjs` |
| BEFORE/AFTER capture, both UAs | `audit-shoot.mjs`, `sweep.mjs`, `manifest.mjs` |
| contrast/size probes, exceptions | `probes.mjs`, `contrast.mjs` (record D-03 in `contrast-exceptions.json`, precedent `~/qb-ui-review/phase-96/contrast-exceptions.json`) |
| Gate 1 sheet | `phase-95/tools/build_gate1.py` + `gate1-shoot.mjs`; section-function layout from `phase-96/tools/build-gate1.py` (self-contained CSS block, `section_*` functions, fantasy text only) |
| Gate 2 sheet and images | `gate2-sheet.mjs`, `gate2-images.py`, `webp.py`, `pair-compare.py` |
| mockup frames (static HTML opened from disk, measure selectors) | `phase-96/tools/mockup-shoot.mjs` (frames.json contract, `report.json` merged by frame name) |

Gate review branch method: temp-index plus `git commit-tree` per RESEARCH section 8 (explicit `--git-dir`/`--work-tree`, explicit refspec push, never touching the shared checkout's HEAD). The orchestrator owns commits and pushes in this session; the plan wraps the commands for the executor.

## Shared Patterns

### Foundation tokens and layers
**Source:** `wwwroot/css/tokens.css`, `base.css`, `components.css`, `shell.css`, `shell.mobile.css`. **Apply to:** every sheet. Area sheets open `@layer pages {`, tokens only, no `!important`, no colour literal, no planning IDs (`UiFoundationLayerGuardTests`).

### No inline code
**Source:** `UiFoundationMobileShellGuardTests.cs:99-110`. **Apply to:** all 17 area views, enforced by `EventsConversionGuardTests`.

### Dates
**Source:** `HtmlHelperExtensions.cs` (`WallClock`/`LocalTime`), `.claude/architecture.md` "Time and the board clock". **Apply to:** every date line in the area; wall-clock values never converted; server reads of "today" only through `IBoardClock`.

### Mobile twin selection
**Source:** `ViewExpanders/MobileViewLocationExpander.cs:27-34`. `_Calendar` on a phone resolves to `_Calendar.Mobile.cshtml` for Quest Details only; the phone Calendar page builds its own agenda list. Agenda/Index twins stay (RESEARCH verdict: does not qualify for a merge).

### Comments
CLAUDE.md rule: plain-language why; no D-nn, Q-nn, A-events-nnn, phase or plan IDs in views, CSS, JS or C#.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| Non-modal dialog with Tab loop and focus return | script | event-driven | No in-repo module does this (Bootstrap modals are modal). Port the logic from the scratch `calendar-fit.js`; shape from `quest-details.js` |
| `--cal-slots` height-band custom property and viewport-fitted grid | stylesheet | n/a | No existing viewport-fit grid; use RESEARCH "Slot count to the script" and the mockup `calendar-fit.css` |

## Metadata

**Analog search scope:** `QuestBoard.Service/{Controllers,Views,Extensions,wwwroot/css,wwwroot/js}`, `QuestBoard.IntegrationTests/{Tests,Controllers}`, `QuestBoard.UnitTests/Architecture`, `~/qb-ui-review/phase-95|96/tools`, Phase 95 and 96 pattern maps and plans.
**Files scanned:** about 30 read or grepped.
**Pattern extraction date:** 2026-11-12
