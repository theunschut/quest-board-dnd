# Phase 97: Events, Series, Calendar and Agenda UI conversion - Research

**Researched:** 2026-11-12
**Domain:** Server-rendered Razor/CSS/ES-module UI conversion onto the Phase 94 layered foundation (ASP.NET Core 10 MVC), with a viewport-fitted month calendar, wall-clock date helpers and an ambient-clock guard
**Confidence:** HIGH for the repo facts, tests, dates and clock seam (all read this session); MEDIUM for the calendar port (the mockup renders the month client-side from JSON, production is server-rendered, so the port is a re-implementation, not a copy); the character-width numbers are measured on the mockup's fantasy titles, not production data.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
Copied from `97-CONTEXT.md` `## Implementation Decisions` (summarised only where CONTEXT itself is a list; wording is CONTEXT's).

- **D-01: "+N more" opens the day panel on click, tap or Enter only.** No hover-to-open mode. Non-modal dialog anchored to the cell, focus moves into it, Tab loops inside, Esc / close button / outside click close it and focus returns to "+N more". The mockup's hover mode and switcher strip are not ported.
- **D-02: The per-entry detail card works as in the mockup**: opens on hover, keyboard focus and tap; hoverable, persists (no timeout), Esc dismisses it. First tap opens it, second tap on the same entry follows the link, a tap elsewhere closes it.
- **D-03: Compact entries stay 20px tall, recorded as a contrast-style exception against WCAG 2.5.8 (24px).** Every entry stays reachable at a passing size through the day panel's 48px rows and the detail card. Recorded in the phase verification the way Phase 95 recorded the poster exception. Slot table as in the mockup: 3 slots at 712 to 849px high (2 entries plus "+N more" at 1280x720), 4 at 850 to 979px, 5 from 980px; "+1 more" never appears.
- **D-04: The site footer sits below the fold on the calendar page.** Only Calendar/Index is affected.
- **D-05: Below about 992px wide on a desktop User-Agent, the fit rule does not apply.** Cells grow, the page scrolls, with Q-116's wrapped navigation row. Fit target stays 1280x720 and up. Phones keep the agenda list.
- **D-06: Entries from a linked board the viewer is NOT a member of stay inert.** The `--static` rule exactly: no link, no tab stop, no data attributes, the visually hidden status text stays. Mouse users get the hover detail card; keyboard and screen-reader users get the hidden text, and the full entry in the day panel when the day overflows. `LinkedBoardStaticGuardTests` keeps its intent. (The mockup's note 5, a focusable non-link treatment, is NOT done.)
- **D-07: Entries from a linked board the viewer IS a member of behave as today**: a real link, dashed outline and link glyph, ported to the compact row. Only the look changes.
- **D-08: Fix the Quest Details calendar weekday header and retire its exception.** The five `div.calendar-day-header` entries (3.76:1) go: the weekday header reaches 4.5:1 on Quest Details as well as on Calendar/Index (5.63:1 in the mockup). Quest Details is re-verified on both layouts.
- **D-09: Direct `.ToString` date renderings in the area's views move to `Html.WallClock`, with byte-identical visible text.** About 18 lines in `_Calendar`, `_Calendar.Mobile`, `Calendar/Index.Mobile`, `Events/Index` (both), `Agenda/Index` (both), `Events/Details`, `Series/Details`; all wall-clock values, nothing converted. Where the helper has no style that produces the identical text, a matching style is **added** to its format table. Helpers' conversion behaviour and every existing style stay untouched, existing helper tests stay green unchanged; only new format entries are added. Each moved line keeps its exact current text, proven by a test.
- **D-10: The today highlight reads the board clock (Q-133), and the calendar partials go under the ambient-clock guard.** `_Calendar.cshtml` (and `_Calendar.Mobile.cshtml` if it gains a today marker through Q-46) compare against the board's today (for example `ViewBag.BoardToday` or an equivalent value from `IBoardClock`), never `DateTime.Today`. Its own behaviour test, separate from the restyle. `AmbientClockSeamTests` scans the partials; its closed list is not weakened.
- **D-11: Hybrid sign-off with two operator gates.** Gate 1 early, as scratch mockups approved before any of it is built: the Events/Details reorder (Q-115), desktop and phone, including the DM view; the calendar port with real data (quest voting and finalized, event, cancelled, linked inert and linked member, the no-building-key warning, the viewer's vote marker, today, the empty month), the Q-116 narrow navigation row, and the phone agenda list with the Q-40 and Q-44 to Q-46 additions; the truncation trade-off (D-14), side by side at 1280 and 1440; an Agenda/Index merge proposal if it qualifies (D-13). Gate 2 at the end: one review sheet with every before/after pair, desktop and phone, including Quest Details re-verified with `_Calendar` and the retired weekday-header exception.
- **D-12: A rejected AFTER is iterated until the operator approves it.** Plan for a possible redraw loop at either gate.
- **D-13: Re-check Agenda/Index for a merge once its markup is aligned**; if it qualifies it goes to Gate 1 as a proposal and merges only on the operator's explicit yes. Calendar/Index and Events/Index stay twins. Reversibility: costly.
- **D-15: Run on the Linux machine that ran Phases 94 and 95**, reusing `~/qb-ui-review/phase-94` (Playwright install and tools, Audit Board, `qb-audit-*` accounts, Direction A renders, calendar-fit mockup). BEFORE re-captured from the current branch into `~/qb-ui-review/phase-97/`. Front-end work invokes the `senior-frontend` skill.
- **D-16: The first plan opens with an operator checkpoint** (SQL Server at `localhost:1433` never provisioned by the agent; reuse of the Audit Board accounts). Through the app's own forms (Phase 94 D-25) the executor creates the fixtures: a crowded month overflowing at least one day at every slot count, a six-week month, an empty month; voting, finalized, cancelled quests, a no-building-key quest, events with and without a start time; a series with occurrences; a linked board the viewer is a member of and one they are not; the Agenda's three empty states; Events/Details as a player and as a DM. Credentials stay in the scratch folder.
- **D-17: Work lands in slices on `milestone/v9-rolling-improvements`, one release at the end** (the next minor, about v6.2.0) once both gates passed. Each slice leaves every page usable and the suite green (Phase 94 D-08).

Inherited G1 answers (CONTEXT `<domain>`): Q-40, Q-41, Q-44 to Q-47 "add it to the other view" (phone calendar gains legend, a 20px page title, the viewer's own vote, the key warning and finalized check, a today marker; desktop month gains "Nothing This Month"), each with its G1-R2 constraint (nothing pushes the desktop month below the fold; status items are single markers on the compact entry, never a taller cell); Q-42, Q-43, Q-48, Q-49 deliberate (phone "Also showing" line, DM name only in the expanded desktop entry, phone Events legend behind "What do these mean?", no "Attendance" label on the phone); Q-115 and Q-116 approved in principle and re-confirmed with their AFTER here; Q-133; Q-134; no merges.

### Claude's Discretion
- **D-14: Compact-entry title truncation** (marker + time + title vs marker + title; shown at Gate 1). Full title is always in the detail card and the day panel.
- Slice order and boundaries, and the plan count (by dependencies).
- How the slot count reaches the script (mockup: `--cal-slots` custom property via height media queries), and the module structure for the calendar and the `_SeriesFormScripts` move, within `.claude/ui-guidelines.md` § "No inline code".
- How the board's today value reaches the partials (view model, `ViewBag`, or an injected service), provided it comes from `IBoardClock`.
- The format-table entry names added for D-09.
- How Quest Details' vote calendar is affected by the port: not a compact month; "+N more" and the slot rules belong to Calendar/Index only; Quest Details keeps Phase 95's vote layout and is re-verified.

### Deferred Ideas (OUT OF SCOPE)
None. The discussion stayed within the phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

REQUIREMENTS.md carries no IDs for this phase (`Requirements: TBD`). Phase-local IDs follow the Phase 95/96 convention: the CONTEXT decisions D-01..D-17 (D-14 is discretion) and DoC-1..DoC-10 (the ten-point Definition of converted in ROADMAP Phase 97, numbered in order).

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01..D-03 | "+N more" day panel, detail card, 20px rows, slot table | Mockup port map (section 7), slot mechanism, exception record |
| D-04, D-05 | Footer below fold; no fit below 992px | `--cal-slots` = 0 sentinel under 992px; Q-116 row at the same breakpoint |
| D-06, D-07 | Linked entries inert / member link | Linked partial contract and the tests that pin it (section 6) |
| D-08 | Weekday header 4.5:1 on Quest Details | Wood-filled header from Direction A (section 7) |
| D-09 | `.ToString` dates to `Html.WallClock` | Full line inventory and new style names (section 4) |
| D-10 | Today from the board clock + guard | `ViewBag.BoardToday` route and guard additions (section 3) |
| D-11, D-12 | Gate 1 and Gate 2, redraw loop | Plan structure (section 2), gate packaging (section 8) |
| D-13 | Agenda merge check | Verdict and tests a merge would change (section 5) |
| D-14 | Truncation recommendation | Measurements (section 1) |
| D-15..D-17 | Linux environment, opening checkpoint, slices | Environment Availability, plan structure |
| DoC-1..DoC-10 | Definition of converted | Violation inventory (section 6), Validation Architecture |
</phase_requirements>

## Summary

The phase is a conversion, not a feature: 17 view files, 6 stylesheets and 3 inline-script modules move onto the layered foundation, plus one approved relayout (Events/Details order), one approved header relayout (Calendar/Index below 992px) and one operator-requested month redesign (viewport-fitted compact month with "+N more"). Every claim about current markup below was read from the files this session.

Three findings change how the CONTEXT reads. (1) **The calendar-fit mockup renders the month in the browser from `month-data.js`**, so `calendar-fit.js` cannot be ported as is: production must server-render every entry in every cell (Razor, with the encoding and the `CanOpen` decision made server-side) and a small ES module must *progressively enhance* it (hide overflow entries, add "+N more", clone entry detail into one shared card and one shared panel). (2) **`site.js` re-formats every `<time class="wall-clock">` in the viewer's browser locale** using `TIMESTAMP_FORMATS`; a new server style with no client entry is left as the server rendered it, which is exactly what D-09's "byte-identical" needs. Adding the new styles to the client table would change `HH:mm` to the viewer's hour cycle. So: server-table entries only, and `.claude/ui-guidelines.md` § "Rendering dates and times" needs a one-sentence update. (3) **A `title="..."` attribute cannot hold the `<time>` element `Html.WallClock` emits**, so `_Calendar.cshtml:76` (the only attribute case) cannot literally move to the helper; see section 4 for the recommended handling.

**Primary recommendation:** 15 plans in 14 waves: opening checkpoint, fixtures/BEFORE, Gate 1 (runs in parallel with the two behaviour-only slices: board-clock today, then the date moves), the Events/Series/Agenda slices, the phone calendar, the desktop calendar (markup+CSS, then the module), and Gate 2 with an orphan review branch last. Keep the existing semantic hook classes (`calendar-day`, `linked-entry--open|--static|--cancelled|--proposed|--finalized`, `linked-day-count`, `legend-*`, `agenda-*`, `avail-*`) wherever the new markup still has the element, because roughly 100 rendered-markup assertions key on them; rewrite the few helpers that key on literal markup (`DayCell`) once.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Which entries a cell holds, ordering (events, quests, linked), `CanOpen` for linked entries | API / Backend (view models, `CalendarViewModel`, `LinkedCalendarService`) | — | Authorization of linked entries is decided server-side; the browser must not be able to infer it. D-06 requires no data attributes on static entries |
| Board "today" | API / Backend (`IBoardClock` via controller) | Frontend Server (Razor reads `ViewBag.BoardToday`) | Container clock is UTC; only the controller may read the clock |
| Date/time text | Frontend Server (`Html.WallClock`) | Browser (`site.js` hydration, only for the four existing styles) | Wall-clock values never convert; new styles stay server-only |
| Month fit (slot count, row heights) | Browser / CSS (`@media (min-height)` sets `--cal-slots`; grid rows share leftover height) | Browser JS (reads the property, hides overflow) | Pure presentation; no server knowledge of the viewport |
| "+N more", detail card, day panel | Browser (ES module) | Frontend Server (renders every entry and its detail block) | Behaviour only; content comes from server-rendered nodes, never from strings |
| Row navigation, confirms, availability POSTs | Browser (ES modules from `data-*`) | API (existing endpoints unchanged) | Replaces inline `onclick`/`onsubmit`/`<script>`; endpoints and wording unchanged |
| Stylesheet loading | Frontend Server (`@section Styles` per view) | — | DoC-3: area sheets leave the global list in `_Layout.cshtml` |
| Calendar feed / FeedRevision | Database / Storage + Domain | — | Untouched; this phase writes nothing to `Events` or `Quests` |

## Standard Stack

No new packages. The phase uses what the repo has. Versions below are the repo's own.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core MVC / Razor | net10.0 [VERIFIED: QuestBoard.IntegrationTests.csproj `TargetFramework` net10.0] | Server-rendered views, partials, `.Mobile.cshtml` twins via `MobileViewLocationExpander` | Project constraint (no framework changes) |
| Bootstrap | 5.3.8, self-hosted [VERIFIED: `_Layout.cshtml:15` `~/lib/bootstrap-5.3.8/css/bootstrap.min.css`] | Modal (`data-bs-toggle`), collapse, dropdown | Foundation re-themes it through `--bs-*` variables |
| Font Awesome | 6.7.2 self-hosted [VERIFIED: `_Layout.cshtml:16`] | Status glyphs (`fa-hourglass-half`, `fa-circle-check`, `fa-calendar-day`, `fa-ban`, `fa-link`) as in the mockup | Already loaded |
| Foundation sheets | in repo | `tokens.css`, `base.css`, `components.css`, `shell.css`, `shell.mobile.css`; area sheets in `@layer pages` | Phase 94 output; all `--qb-*` tokens the mockup uses exist (checked name by name against `tokens.css`: every `--qb-*` in `calendar-fit.css` resolves) |
| Vanilla ES modules under `wwwroot/js` | n/a | `calendar-month.js`, `event-details.js`, `event-series-form.js`, `agenda.js`, `row-link.js` (names are a recommendation) | `.claude/ui-guidelines.md` "No inline code"; precedent `quest-details.js`, `quest-manage.js` |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit v3 + FluentAssertions | xunit.v3 3.2.2 [VERIFIED: csproj] | Guard and behaviour tests | Every slice |
| Playwright (scratch only) | 1.64.1 via `~/qb-ui-review/phase-94/tools/node_modules` [CITED: ENVIRONMENT.md] | Captures, probes, behaviour checks | Gate packages and sweeps; bundled Chromium via `chromium.launch()` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Server-render all entries + enhance | Client-render the month from JSON (as the mockup does) | Would need a JSON endpoint and duplicate the `CanOpen`/encoding logic; breaks no-JS fallback; rejected |
| `ViewBag.BoardToday` | `CalendarViewModel.Today` property | Both satisfy D-10; ViewBag matches the six quest views already guarded and Quest Details already sets it (section 3) |
| New helper overload `Html.WallClock(DateOnly, ...)` | Convert at the call site with `DateOnly.ToDateTime(...)` | An overload touches the helper; call-site conversion keeps DoC-9 literally true. Recommended: call-site |

**Installation:** none. **Version verification:** not applicable (no registry packages are added; `npm` is not used by the repo, only by the scratch Playwright folder which is reused by symlink, no install).

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | — | — | — | — | — | No external package is installed by this phase |

**Packages removed due to [SLOP] verdict:** none. **Packages flagged as suspicious [SUS]:** none. The Playwright copy under `~/qb-ui-review` already exists and is symlinked, never installed (operator-approved in the Phase 96 environment note).

## Architecture Patterns

### System Architecture Diagram

```
Request GET /Calendar?year&month  (desktop UA)            Request GET /Calendar (phone UA)
        |                                                         |
CalendarController.Index  --boardClock.Today--> ViewBag.BoardToday  (new, one line)
  builds CalendarViewModel (own quests, events, Linked*, CanManage, SeriesBelowRunway)
        |                                                         |
 Views/Calendar/Index.cshtml                              Views/Calendar/Index.Mobile.cshtml
   @section Styles -> calendar.css                          @section Styles -> calendar.mobile.css
   @section Scripts -> calendar-month.js (module)           agenda list: one section per day with entries,
   header: title, buttons, legend, month nav                stretched row links, status markers, own vote,
   empty-month note                                         today marker (ViewBag.BoardToday)
        |  Html.PartialAsync("_Calendar", Model)
   _Calendar.cshtml  (ViewBag.IsDetailsPage false)
     for each day: <div class="calendar-day [today]">
       entries in DOM order: events, quests, then _LinkedDayEntries   (order is asserted by tests)
       every entry = compact link + its own hidden detail block (server-rendered, text only)
        |
   calendar-month.js (progressive enhancement, runs after load and on height-band change)
     reads --cal-slots from getComputedStyle  -> 0 means "no fit rule" (< 992px) -> do nothing
     hides entries beyond slots-1, appends <button> "+N more"
     hover/focus/tap on an entry -> clone its detail block into the ONE shared card (aria-hidden)
     click/Enter on "+N more"   -> clone the cell's entries into the ONE shared day panel (dialog, non-modal)
     positions via style.setProperty('--pop-x'/'--pop-y')  (same technique as quest-board.js posters)

Quest/Details (desktop)  -> Html.PartialAsync("_Calendar", month)  with ViewBag.IsDetailsPage true
   -> existing vote layout (Phase 95) in the same partial, calendar.css details block + quest-vote.css
Quest/Details.Mobile     -> "_Calendar" resolves to _Calendar.Mobile.cshtml (expander) -> vote rows + _LinkedAlsoThatDay
```

### Recommended Project Structure
```
QuestBoard.Service/
├── Views/Shared/_Calendar.cshtml        # month grid; compact branch (not details) + details branch kept
├── Views/Shared/_Calendar.Mobile.cshtml # Quest Details phone vote rows only (not the Calendar page)
├── Views/Calendar/Index(.Mobile).cshtml
├── wwwroot/css/calendar.css             # @layer pages; month page + details block, no literals, no !important
├── wwwroot/css/calendar.mobile.css      # @layer pages, phone agenda list
├── wwwroot/js/calendar-month.js         # new: slots, "+N more", card, panel
├── wwwroot/js/row-link.js               # new: [data-row-href] row click (Events/Index desktop table only)
├── wwwroot/js/event-details.js          # new: setAvailability / withdraw / confirm via data-*
├── wwwroot/js/event-series-form.js      # new: replaces _SeriesFormScripts + Edit inline script
└── wwwroot/js/agenda.js                 # new: switch-board modal fill
```
Component-to-file mapping lives in the section tables below, not in the diagram.

### Pattern 1: Stretched row link (established)
**What:** one `a.row-nav-link` per card with `::after { content:""; position:absolute; inset:0 }`, the card `position: relative`, the ring drawn on the card via `:has(.row-nav-link:focus-visible)`. [VERIFIED: `home.mobile.css:58-62`, `quest-log.css:100-104`]
**When to use:** Calendar/Index.Mobile rows, Events/Index.Mobile `.avail-card`. Replaces the `onclick="window.location.href=..."` on non-interactive divs (A-events-022). Keep the anchor itself >= 44px tall as `home.mobile.css` does.
**Exception:** the desktop Events/Index table row. The sticky `td.avail-sticky-col` cells are containing blocks for absolutely positioned children, so a stretched link would cover one cell only. Use `row-link.js` (`tr[data-row-href]`, ignores clicks on links/buttons and when text is selected) so the whole-row mouse target survives; the title link stays the keyboard path.

### Pattern 2: Confirm and POST wiring from data attributes (established)
`quest-manage.js` reads `button.dataset.confirmMessage`; `quest-details.js` reads the token from `<meta name="request-verification-token">`. [VERIFIED: `quest-manage.js:12`, `quest-details.js:10-13`] `_QuestFormScripts.cshtml` already emits the meta on Create and Edit; `Events/Details` must emit its own. Messages keep today's wording; `Events/Details` builds them with `Model.Date:dddd, MMMM d, yyyy` interpolation in the view's C# block (not a `.ToString(` call), now written into `data-confirm-message`.

### Pattern 3: Server-rendered detail block per entry
Each compact entry carries a sibling `div.cal-entry-detail` (text and `<time class="wall-clock">` only, `hidden`/CSS-hidden, no `id`, no `data-*` on static linked entries). The module clones it with `cloneNode(true)` into the card. No `innerHTML`, no string-built markup (same rule `QuestManageModule_WritesNoMarkupFromStrings` pins). A static linked entry's detail block reuses today's `.linked-entry-detail` content (board, DM, time) which is already `aria-hidden="true"` beside a `visually-hidden` span. [VERIFIED: `_LinkedDayEntries.cshtml` body templates]

### Anti-Patterns to Avoid
- **Moving the month to client rendering** (as the mockup does): loses the server's `CanOpen` decision point and the no-JS fallback (all entries visible, cell scrolls).
- **A second `<time>` style table on the client for the new styles:** changes visible text per locale.
- **`display:none` detail cards with `aria-hidden` only:** the entry's own accessible name must carry the status, time, DM and board (`visually-hidden` suffix), because the card is `aria-hidden` by design.
- **Editing `_Calendar` and the date moves in the same commit:** the board-clock change (D-10) must be its own tested commit.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Confirm dialogs | A custom modal | Keep `confirm()` with the same wording from a module (A-events-031: no replacement modal unless approved) | Wording and behaviour stay as is |
| Remove-series dialog, switch-board dialog, save-scope dialog | Custom overlays | Bootstrap 5.3.8 modal already used (`data-bs-toggle="modal"`), fix its `aria-labelledby` and close-button name | Focus trap, Esc, scroll lock built in |
| Row focus ring | Per-page `:focus-visible` rules | `base.css` two-tone ring (`.row-nav-link` is in its list) | One ring (A-events-037) |
| Empty states | Hand-built `text-center text-muted py-5` blocks | `.empty-state` structure (`components.css`), copy verbatim from the audit table | A-events-032 |
| Time-only / weekday date text | `.ToString("HH:mm")` in views | `Html.WallClock(..., "<new style>")` | DoC-9, rendering rule |
| Visually hidden text | Inline style | `.visually-hidden` | Already in `components.css` / `shell.css` |
| Status colours | Literals (`#ffc107`, `#28a745`, `#6f42c1`) | `--qb-warning`, `--qb-success`, `--qb-event`, `--qb-neutral` with their `-fg` pairs | Zero colour literals outside `tokens.css` |

**Key insight:** every behaviour in the mockup that is hard (hover intent, Esc suppression, focus return, clamp-to-viewport) already works in `calendar-fit.js` (46 behaviour checks pass). Port its logic, change only the data source (DOM clones instead of `DATA`), drop the hover mode and the switcher.

## Runtime State Inventory

This is a UI conversion, not a rename or migration. Categories answered explicitly:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None. `Events`, `Quests` and the feed revision columns are not written (DoC-9). Verified by the architecture note: writes go through the change tracker only, and this phase adds none | none |
| Live service config | None | none |
| OS-registered state | None | none |
| Secrets/env vars | None. Credentials stay in `~/qb-ui-review/phase-97/credentials.json`, never in the repo | none |
| Build artifacts | Browsers cache `calendar.css` / `events-overview.css` / `agenda.css` by `asp-append-version`; removing the global `<link>`s needs no action. The scratch baseline app (port 8001) is built from a frozen copy of the starting commit | rebuild baseline at plan 02 |

## Common Pitfalls

### Pitfall 1: Date helper hydration rewrites the "byte-identical" text
**What goes wrong:** `site.js` rewrites `time.wall-clock[datetime]` text through `Intl.DateTimeFormat(undefined, ...)` for the four styles it knows. If a new style were also added to `TIMESTAMP_FORMATS`, `HH:mm` would turn into `2:30 PM` for en-US viewers.
**Why it happens:** the hydration pass reads `TIMESTAMP_FORMATS[style]`; an unknown style returns early ("leave the server-rendered invariant-culture text alone"). [VERIFIED: `site.js:74-110`]
**How to avoid:** server table only. Do not touch `site.js`. `SiteJs_DefinesExactlyOneStyleTable_SharedByBothHydrationPasses` counts `'date-time-compact':` occurrences, so it would survive either way, but the visible text would not.
**Warning signs:** a Playwright capture at the Chromium default locale shows `AM/PM` where the BEFORE showed 24-hour times.

### Pitfall 2: Invariant vs current culture
**What goes wrong:** today's `.ToString("dddd, MMMM d")` uses the host's current culture; `BuildWallClock` always uses `CultureInfo.InvariantCulture`. Identical on an invariant or en-US host; not identical on a host with a non-English `LANG`. [VERIFIED: no request-localization or `CultureInfo` configuration exists in `Program.cs`/`appsettings*.json` by grep; production LXC `LANG` not inspected] Record as an assumption (A2); the tests run on the CI/Linux default.

### Pitfall 3: `.ToUpper()` on the phone day label
`Calendar/Index.Mobile.cshtml:98` is `@day.Date.ToString("dddd, MMMM d").ToUpper()` inside `div.agenda-day-label text-uppercase`. A format string cannot upper-case. Render mixed case through the helper and let the existing `text-uppercase` class (already on that div) produce the identical visible text. The server HTML text changes case; the visible and visual text do not. State this in the plan so the test asserts the element text, not the upper-cased string.

### Pitfall 4: `title` attribute cannot carry `<time>`
`_Calendar.cshtml:76` is `title="@questOnDay.Quest.Title - @questOnDay.ProposedDate.Date.ToString("HH:mm")"`. See section 4 for the recommendation (view-model `TimeLabel`, as `EventOnDay` and `LinkedQuestOnDay` already do).

### Pitfall 5: `_Calendar` is shared; the month rules must not reach Quest Details
Quest Details renders `_Calendar` three times and links `calendar.css` itself. [VERIFIED: `Quest/Details.cshtml:32`, `:629,:669,:710`] Today's `calendar.css` mixes the month page (`.calendar-body { grid-auto-rows: 120px }`) with the details overrides (`.details-page .calendar-body { grid-auto-rows: 160px }`, `.quest-details-page`, the `.current-quest` `!important` block). The compact entry, `+N more`, the card, the panel, the fit rows must be scoped under one page class on Calendar/Index (for example `.calendar-page`), and the details block keeps `.details-page`. The weekday header (D-08) is the one rule that is shared on purpose.

### Pitfall 6: Phase 96 is editing shared files in this checkout
Phase 96's plans list `UiFoundationLayerGuardTests.cs`, `MobileViewsTests.cs`, `shell.mobile.css`, `legacy-pages.css`, `AccessDenied.cshtml`, `account.css` and `UiFoundationDesktopShellGuardTests.cs` in `files_modified`. [VERIFIED: grep of `96-*-PLAN.md` `files_modified`] Ports 8000 and 8001 are listening right now (ss), i.e. Phase 96's app and baseline. Phase 97 execution must start after Phase 96 is merged or finished, must **append** to the `LayeredFiles`/`NoLiteralFiles` arrays (never replace them), and must re-read at plan time: `components.css`, `shell.mobile.css` (the phone `.row` gutter fix that A-events-019 depends on), `AccessDenied.cshtml`, `_Layout.cshtml` global link list, `UiFoundationLayerGuardTests` arrays, `MobileViewsTests`.

### Pitfall 7: Slot fit is tight
A six-week row has 91.2px at 720 and a 3-slot cell needs 90px (1.2px slack) in the mockup, which included a 32px strip the real page lacks (about 5px more per row). [CITED: `calendar-fit/NOTES.md` note 6] Any added chrome above the grid (the empty-month note, the runway warning alert for managers, the "Also showing" line) costs rows. The Gate 1 drawing and the fit sweep must include the manager view with the runway alert and the empty-month note.

### Pitfall 8: Legend content in the mockup is not the real legend
The mockup folds the legend into one line: "Voting open, Finalized, Event, Cancelled, Linked board". Production's legend today also has: "Proposed" (not "Voting open"), the linked-board name list, "Dashed = from a linked board", "Dashed entries belong to a linked board. You can open them only if you are a member of that board.", "No building key", and "Click quests or events for details". [VERIFIED: `Calendar/Index.cshtml:71-125`] Tests assert several of these strings (`Legend_BoardLinkedToAnother_NamesTheLinkedBoardAndExplainsTheDashedEdge`, `Index_LegendExplainsEventChip`). No copy changes without the operator and no functionality removed: the Gate 1 drawing must show where each string lives (for example a popover or a second legend line), or the operator must approve the copy changes.

### Pitfall 9: Antiforgery token in script text
`Events/Details.cshtml:200,296`, `Create.cshtml:121`, `Edit.cshtml:138` write `@tokens.RequestToken` into inline script. Replace with the meta element convention above. A module that reads the token must tolerate a missing meta (empty string), as `quest-details.js` does.

## Code Examples

### Board today reaches the partial (recommended)
```csharp
// CalendarController.Index, before `return View(calendarModel);`
// The today highlight is the board's day, not the container's: the container clock is UTC.
ViewBag.BoardToday = boardClock.Today;   // boardClock is already injected (CalendarController.cs)
```
```cshtml
@* _Calendar.cshtml, top of the file; Quest/Details already sets ViewBag.BoardToday (QuestController.cs:420) *@
@{ var boardToday = (DateOnly)ViewBag.BoardToday; }
...
<div class="calendar-day @(day.IsEmpty ? "empty" : "") @(!day.IsEmpty && DateOnly.FromDateTime(day.Date) == boardToday ? "today" : "")">
```
Source: pattern of `Views/Quest/Details.cshtml:16` `var boardToday = (DateOnly)ViewBag.BoardToday;` [VERIFIED: grep this session].

### Slot count to the script (recommended)
```css
@layer pages {
  .calendar-page { --cal-slots: 0; }                       /* < 992px: no fit rule, cells grow */
  @media (min-width: 992px) {
    .calendar-page { --cal-slots: 2; }
    @media (min-height: 712px) { .calendar-page { --cal-slots: 3; } }
    @media (min-height: 850px) { .calendar-page { --cal-slots: 4; } }
    @media (min-height: 980px) { .calendar-page { --cal-slots: 5; } }
  }
}
```
```js
const slots = parseInt(getComputedStyle(grid).getPropertyValue('--cal-slots'), 10) || 0;
if (slots === 0) { restoreAll(); return; }            // below 992px every entry stays visible
const visible = total <= slots ? total : slots - 1;   // "+1 more" never appears
```
The mockup used `readSlots()` returning at least 2; the `0` sentinel is the only change needed for D-05. Week count: render `calendar-grid--weeks-4|5|6` server-side from `GetCalendarDays().Count / 7` instead of the mockup's JS-set `--cal-weeks`.

### Detail block read without innerHTML
```js
card.replaceChildren(entry.querySelector('.cal-entry-detail').cloneNode(true));
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `.modern-card span {color:#F4E4BC !important}` fights, `!important` re-colours | Surface inheritance + tokens + layers | Phase 94 foundation | Area sheets drop to tokens only; 74 `!important` fall away (measured; roadmap says 70) |
| Hover-only chip detail, `title` tooltips | Detail card (hover/focus/tap, Esc) + visually hidden text | this phase | WCAG 1.4.13 / 2.1.1 (A-events-013) |
| Inline `onclick` row navigation | One stretched link per card | Phase 95 | A-events-022 |
| `DateTime.Today` in a view | `ViewBag.BoardToday` from `IBoardClock` | Quest views earlier, calendar now | Q-133 |

**Deprecated/outdated:** the roadmap's counts are slightly off against the files now: inline event handlers 17 by grep (roadmap says 18; counted: Calendar/Index.Mobile 4, Agenda/Index.Mobile 2, Events/Details 7, Series/Details 1, Events/Index 1, Events/Index.Mobile 2 = 17); `!important` 74 by text count (includes occurrences in comments; roadmap says 70). Re-run the guard regexes rather than trusting either number.

## 1. D-14 title truncation (measured)

Method: Chromium (bundled, Playwright 1.64.1 from the Phase 94 install) opened the static mockup `calendar-month.html` at 1280x720 and 1440x900. For every `.cf-entry` the script read the title box width, the time width and the flex gap, measured text with the entry's own font (`600 13px system-ui, ...`, the same stack as `--qb-font-body`), and counted how many characters fit before the ellipsis with and without the time. [VERIFIED: measured this session, scratch script only]

| | 1280x720 | 1440x900 |
|---|---|---|
| Entries in the mockup data | 46 | 56 |
| Title box with a time (px) | 105-106 (87 linked, 142 all-day) | 128-129 (110 linked, 165 all-day) |
| Time element width (px) | 33 | 33 |
| Truncated with marker + time + title | 39 of 46 | 42 of 56 |
| Truncated with marker + title (time dropped) | 33 of 46 | 26 of 56 |
| Characters shown on a truncated title, with time | 12.6 average | 15.7 average |
| Characters shown, same titles, time dropped | 18.0 average | 21.2 average |
| Title length in the mockup data | average 26.3, median 23 | average 25.8, median 23 |

So dropping the time buys about 5.4 characters and rescues 6 titles (13%) at 1280 and 16 (29%) at 1440 from truncation; most titles still truncate at 1280.

**Recommendation: keep marker + time + title.** Reasons: (1) the time is the one fact that tells apart repeated titles on the same day (a quest with several proposed dates on one day, the weekly event, the mockup's own data has "The Drowned Lantern of Vael" three times); (2) cells already sort by time, but order alone is not a time; (3) five characters rarely make a truncated title recognisable, the card and the panel carry the full title. Show both rows side by side at Gate 1 at 1280 and 1440 as D-11 asks, with these figures, and add a third strip for a linked entry (it loses another 18px to the link glyph). Default if the operator is silent: keep the time. Caveats: fantasy titles, Linux Chromium font resolution; re-measure on the Audit Board fixtures before Gate 1 closes. [ASSUMED: production titles are similar in length]

## 2. Slice order, boundaries, plan count and waves

Phase 95 had 18 plans and Phase 96 12 plans, almost all one plan per wave (95: waves 3 only had two plans; 96: strictly sequential), because the plans share `UiFoundationLayerGuardTests`, `MobileViewsTests` and one checkout. [VERIFIED: `wave:` lines of `95-*-PLAN.md` and `96-*-PLAN.md`] Recommended for Phase 97: **15 plans, 14 waves**.

| Plan | Wave | Autonomous | Content | Depends on | Notes |
|------|------|-----------|---------|------------|-------|
| 97-01 | 1 | no | Opening checkpoint (D-16): SQL at 1433, `qb-audit-*` accounts reused, Phase 96 finished and ports 8000/8001 free, scratch folder `~/qb-ui-review/phase-97`, tools copied from `phase-95/tools` and re-pointed, `senior-frontend` loaded | — | Phase 96 environment substitutions apply (ports, bundled Chromium, `ss`, no GC vars, no SQL writes, never start/stop `mssql-dev`) |
| 97-02 | 2 | yes | Fixtures through the app's own forms (crowded and six-week and empty months, quest/event/cancelled/no-key, series, linked member and non-member boards, Agenda empties, Events/Details player and DM), frozen baseline build of the starting commit, BEFORE capture, probes (`measure` for D-14 on real titles) | 01 | Fixtures are Audit Board only |
| 97-03 | 3 | no | **Gate 1** package and hold: Events/Details reorder (desktop, phone, DM), calendar port with real data, Q-116 row, phone agenda additions, D-14 strips, Agenda merge "not proposed" note (section 5); publish the orphan review branch (section 8) | 02 | Rejected AFTER => redraw v2, v3 (D-12); no built code depends on 97-03 before 97-09 |
| 97-04 | 3 | yes | **Slice 1: board-clock today (Q-133).** `ViewBag.BoardToday` in `CalendarController.Index`, `_Calendar` compare, `AmbientClockSeamTests` additions, behaviour test pinned at 2026-09-05T23:30Z | 02 | Own commit(s), no restyle |
| 97-05 | 4 | yes | **Slice 2: wall-clock moves.** New format entries, `_Calendar`/`_Calendar.Mobile`/Calendar.Mobile/Events/Agenda/Details/Series date lines, new helper-style unit test, new `EventsConversionGuardTests` skeleton | 04 | Touches `_Calendar` after 04 to avoid same-file conflicts |
| 97-06 | 5 | yes | **Slice 3: availability partials + Events/Index (both).** `_AvailabilityCell`, `_AvailabilityCounts`, shared `.avail-*` rules to `components.css`, `events-overview(.mobile).css` layered, `row-link.js`, stretched link on phone cards, roster toggle without inline handlers, `.empty-state`, `@section Styles` move for `events-overview.css`, rewrite `EventsOverviewMobileStyleTests` and the `stopPropagation` count fact | 05 | Settles the partials Agenda reuses |
| 97-07 | 6 | yes | **Slice 4: Events/Details** `event-details.js` (set/withdraw/confirm), meta token, reorder Q-115 per the approved Gate 1 drawing, `AccessDenied`-style overflow check (A-events-019), roster/badges | 03 (approval), 06 | `EventDetailsAvailabilityRenderTests` rewritten |
| 97-08 | 7 | yes | **Slice 5: Events/Create + Edit + `_SeriesFormScripts`** to `event-series-form.js`, label `for`/`id` fix (A-events-020), cycle cells 44px on phone and accessible names, Edit save-scope script | 07 | |
| 97-09 | 8 | yes | **Slice 6: Series/Details** inline style removed, `onsubmit` confirm, modal `aria-labelledby`/close name, "View" link names, empty `<th>`, dead `today` class | 08 | |
| 97-10 | 9 | yes | **Slice 7: Agenda (both)** `agenda.js` switch modal, drop inline `stopPropagation` and `style`, 44px filter rows, `agenda(.mobile).css` layered, `@section Styles` move for `agenda.css`, empty states | 06 | D-13 outcome applied |
| 97-11 | 10 | yes | **Slice 8: phone calendar** Calendar/Index.Mobile (stretched links, 20px title, legend, own vote, key warning, finalized check, today marker), `calendar.mobile.css`, `_Calendar.Mobile`, `_LinkedAlsoThatDay` hidden text, rewrite `LinkedBoardMobileCalendarTests` assertions that pin `window.location.href` | 03, 04, 05 | |
| 97-12 | 11 | yes | **Slice 9a: desktop calendar markup and CSS** `_Calendar` compact branch, `_LinkedDayEntries`, `Calendar/Index` header/legend/empty note/Q-116, `calendar.css` layered, D-08 header, `@section Styles` move, `Quest/Details` regression | 03, 04, 05 | Works without JS (all entries visible, cell scrolls) |
| 97-13 | 12 | yes | **Slice 9b: `calendar-month.js`** slots, "+N more", card, panel, tap, Esc; static guards for the module; behaviour sweep (Playwright, touch) | 12 | |
| 97-14 | 13 | yes | **Cleanup and guards**: remove the three global links from `_Layout.cshtml`, finish `EventsConversionGuardTests` lists, `UiFoundationLayerGuardTests` arrays, rewrite remaining listed guards, `contrast-exceptions.json` for D-03, full sweep both UAs | 09, 10, 11, 13 | |
| 97-15 | 14 | no | **Gate 2** sheet, orphan review branch, D-03 exception record, release handoff (v6.2.0, GHCR caveat) | 14 | Operator approves every pair |

Why this order: behaviour-only and gate-independent slices first (04, 05) so they land while Gate 1 is held; shared partials before the pages that consume them (06 before 10); the calendar port last because it needs Gate 1, the clock change and the date moves in the same file. Each slice moves its own sheet link out of the global list when its views link it themselves, so every intermediate state is usable (D-08); 97-14 only removes whatever is left. `calendar.css` cannot leave the global list before Calendar/Index links it itself, which is why that move is in 97-12.

## 3. How the board's today reaches the partials

[VERIFIED: files read this session] `_Calendar` has four render sites: `Calendar/Index.cshtml:66`, `Quest/Details.cshtml:629,:669,:710`, `Quest/Details.Mobile.cshtml:252,:331` (the last resolves to `_Calendar.Mobile.cshtml` on a phone through `MobileViewLocationExpander`, which yields `.Mobile.cshtml` first for every location including partials). `_Calendar.Mobile.cshtml` renders only Quest Details' vote rows and contains no today read. The phone Calendar page does **not** use either partial; it builds its own agenda list in `Calendar/Index.Mobile.cshtml`.

- `QuestController.Details` already sets `ViewBag.BoardToday = boardClock.Today;` (`QuestController.cs:420`, immediately after `ViewBag.IsDetailsPage = true;` at `:416`).
- `CalendarController.Index` has `IBoardClock boardClock` injected and reads `boardClock.Now` for the default month, but sets no `BoardToday`; it needs the one line.
- **Recommendation: `ViewBag.BoardToday` (a `DateOnly`).** One controller line, no change to `CalendarViewModel`, matches the six views that `AmbientClockSeamTests` already pairs with the token `"ViewBag.BoardToday"`, and the partial inherits `ViewData` through `Html.PartialAsync` exactly as it already inherits `IsDetailsPage`/`CurrentQuestId`. Use the same hard cast as the other views (`(DateOnly)ViewBag.BoardToday`): a missing value then fails loudly (500) rather than silently dropping the highlight; both hosts set it.

**`AmbientClockSeamTests` additions (closed list only grows)** [VERIFIED: `AmbientClockSeamTests.cs` read in full]:
- `GuardedRelativePaths`: append `QuestBoard.Service/Views/Shared/_Calendar.cshtml`, `.../Shared/_Calendar.Mobile.cshtml`, `.../Views/Calendar/Index.cshtml`, `.../Views/Calendar/Index.Mobile.cshtml`. `GuardedPath_ContainsNoAmbientClockRead` strips Razor `@* *@`, C `/* */` and `//` text first and then rejects the three shapes `DateTime.Today`, `DateTime.Now`, `DateTime.UtcNow`.
- `RoutedTodayViewTokens`: append `_Calendar.cshtml` and `Calendar/Index.Mobile.cshtml` both with `"ViewBag.BoardToday"` (positive assertion so the test cannot pass vacuously). `_Calendar.Mobile.cshtml` and `Calendar/Index.cshtml` stay in the negative list only (no today consumer), unless Q-46 puts a today marker in a partial.
- `CalendarController.cs` is already in `GuardedRelativePaths` and `BoardClockConsumerPaths`; nothing to add.
- Careful: `StripComments` removes everything after `//` on a line, so a Razor line with `https://...` loses its tail from the scan. Harmless for a negative scan; do not put an ambient read after a URL on the same line.

**Own behaviour test (Q-133):** new facts in a new class modelled on `QuestStatusBoardClockTests` (`FixedTimeProvider`, `TimeZone:BoardTimeZoneId = Europe/Amsterdam`, pinned `2026-09-05T23:30Z`, so the board is already on the 6th). Request `/Calendar?year=2026&month=9` (desktop UA) and assert the day cell containing day 6 has class `today` and day 5 does not; same for the phone agenda's today marker. Against the old `DateTime.Today` neither day matches (September 2026 is not "today" on the test host), so the fact fails before the fix. Template: [VERIFIED: `QuestStatusBoardClockTests.cs` header and `PinnedNow`].

## 4. D-09: every `.ToString(...)` date rendering (file:line, format, type)

[VERIFIED: `grep -nE '\.ToString\('` over the 17 area files this session; types from the view models]

| # | File:line | Exact format string | Value type | Text today (invariant/en-US) | New style (proposed) |
|---|-----------|--------------------|-----------|------------------------------|----------------------|
| 1 | `Calendar/Index.Mobile.cshtml:98` | `"dddd, MMMM d"` then `.ToUpper()` | `DateTime` (`day.Date`) | `SATURDAY, JUNE 14` | `weekday-long` (rely on `text-uppercase`, Pitfall 3) |
| 2 | `Calendar/Index.Mobile.cshtml:121` | `"HH:mm"` | `DateTime` (`ProposedDate.Date`) | `19:00` | `time-24h` |
| 3 | `Agenda/Index.cshtml:107` | `"ddd, MMM d"` | `DateOnly` (`row.Date`) | `Tue, Nov 3` | `weekday-short` |
| 4 | `Agenda/Index.cshtml:110` | `"HH:mm"` | `TimeOnly` (`row.StartTime.Value`) | `19:00` | `time-24h` |
| 5 | `Agenda/Index.Mobile.cshtml:108` | `"ddd, MMM d"` | `DateOnly` | same | `weekday-short` |
| 6 | `Agenda/Index.Mobile.cshtml:111` | `"HH:mm"` | `TimeOnly` | same | `time-24h` |
| 7 | `Shared/_Calendar.cshtml:76` | `"HH:mm"` inside `title="..."` | `DateTime` | `Title - 19:00` | attribute context, see below |
| 8 | `Shared/_Calendar.cshtml:84` | `"HH:mm"` | `DateTime` | `19:00` | `time-24h` |
| 9 | `Shared/_Calendar.Mobile.cshtml:26` | `"dddd, MMMM d"` | `DateTime` | `Saturday, June 14` | `weekday-long` |
| 10 | `Shared/_Calendar.Mobile.cshtml:27` | `"HH:mm"` | `DateTime` | `19:00` (inside `at ...`) | `time-24h` |
| 11 | `Series/Details.cshtml:40` | `"dddd, MMMM d, yyyy"` | `DateOnly` (`Model.AnchorDate`) | `Friday, September 4, 2026` | `weekday-long-year` |
| 12 | `Series/Details.cshtml:64` | `"dddd, MMMM d, yyyy"` | `DateOnly?` (`Model.EndDate.Value`) | same | `weekday-long-year` |
| 13 | `Series/Details.cshtml:116` | `"dddd, MMMM d, yyyy"` | `DateOnly` (`occurrence.Date`) | same, then ` &middot; @occurrence.TimeLabel` | `weekday-long-year` |
| 14 | `Events/Details.cshtml:48` | `"dddd, MMMM d, yyyy"` | `DateOnly` (`Model.Date`) | same | `weekday-long-year` |
| 15 | `Events/Index.cshtml:66` | `"ddd, MMM d"` | `DateOnly` | `Tue, Nov 3` | `weekday-short` |
| 16 | `Events/Index.cshtml:69` | `"HH:mm"` | `TimeOnly` | `19:00` | `time-24h` |
| 17 | `Events/Index.Mobile.cshtml:80` | `"ddd, MMM d"` | `DateOnly` | same | `weekday-short` |
| 18 | `Events/Index.Mobile.cshtml:83` | `"HH:mm"` | `TimeOnly` | same | `time-24h` |

That is 18 lines, matching CONTEXT's "about 18". Not date renderings and left alone: `Events/Edit.cshtml:26` `Model.Date.ToString("yyyy-MM-dd")` (hidden input value read back by script, a machine value; the new guard must allow `yyyy-MM-dd` as it allows `yyyy-MM-ddTHH:mm` in `QuestsConversionGuardTests`), and `_Calendar.cshtml:79` `isDetailsPage.ToString().ToLower()` (a boolean). Also in view C# blocks, not `.ToString(` calls: `Events/Details.cshtml:20-21` `{Model.Date:dddd, MMMM d, yyyy}` inside the cancel and restore confirm messages (text for a dialog; stays, moved into `data-confirm-message`), and `Series/Details.cshtml:11-12` `Model.TodayLabel` (a view-model string, `Today.ToString("MMMM d, yyyy")`, `SeriesDetailsViewModel.cs:30`).

**Existing styles produce none of these.** The helper's table is exactly [VERIFIED: `HtmlHelperExtensions.cs:31-37`]:
```
["date"] = "MMM d, yyyy",
["date-time"] = "MMM d, yyyy, h:mm tt",
["date-compact"] = "MMM d",
["date-time-compact"] = "MMM d, h:mm tt",
```
None equals `HH:mm`, `ddd, MMM d`, `dddd, MMMM d` or `dddd, MMMM d, yyyy`. So four entries are added to the same private dictionary (`LocalTimeFormats`), names above:
```
["time-24h"]          = "HH:mm"
["weekday-short"]     = "ddd, MMM d"
["weekday-long"]      = "dddd, MMMM d"
["weekday-long-year"] = "dddd, MMMM d, yyyy"
```
Consequences to plan for:
- The dictionary is shared by `LocalTime` and `WallClock`, so the new names also become legal for `Html.LocalTime`. Harmless, but the new unit test should pin them for `BuildWallClock` only; do not use them with real instants.
- `WallClockMarkupTests` (four styles) and `LocalTimeMarkupTests` stay green unchanged: they iterate their own four-style `StylesAndExpectedText` data, and `BuildWallClock_UnrecognisedStyle_ThrowsArgumentOutOfRangeException` uses `"fortnight"`. [VERIFIED: `WallClockMarkupTests.cs`] `WallClockUnmovedTests` reads Quest Details and Manage only (finalized game night text and the `local-time` structural marker); the area's views do not appear in it. A new `WallClockMarkupTests`-style class (do not edit the existing file) pins the four new styles' invariant text, and the area views get a rendered-HTML test proving each moved line's text equals the old `ToString` output.
- **No `site.js` change.** The four new names have no `TIMESTAMP_FORMATS` entry, so `hydrateWallClockTimes` returns early for them. This is what keeps "visible text byte-identical" true in every browser locale. Update `.claude/ui-guidelines.md` § "Rendering dates and times" (it says the four names exist on both sides and that the sides must agree) to say the server table may carry server-only styles that the client leaves untouched.
- **Overloads.** `BuildWallClock` takes a `DateTime`. `DateOnly` and `TimeOnly` values convert at the call site: `row.Date.ToDateTime(TimeOnly.MinValue)`, and for the time `row.Date.ToDateTime(row.StartTime.Value)`. The `datetime` attribute then reads `yyyy-MM-ddTHH:mm` (no zone), which is what a floating value should carry. Adding `DateOnly`/`TimeOnly` overloads would touch the helper file; recommended not to (DoC-9). [ASSUMED: the user prefers call-site conversion to a new overload; confirm, Open Question 2]
- **Attribute context (row 7).** `Html.WallClock` returns a `<time>` element and cannot sit inside `title="..."`. The codebase's existing answer for attribute and aria text is a view-model `TimeLabel` string: `EventOnDay.TimeLabel` and `LinkedQuestOnDay.TimeLabel` ([VERIFIED: `LinkedQuestOnDay.cs:16` `When.ToString("HH:mm", CultureInfo.InvariantCulture)`]). Recommended: add `QuestOnDay.TimeLabel` the same way for tooltip/aria text, use `Html.WallClock(..., "time-24h")` for the visible time. On the Calendar page the `title` tooltip is replaced by the detail card, but keep the Quest Details `title` (the audit says "`title` and hidden text added, never removed"). [ASSUMED: this is acceptable to the user as the reading of D-09 for an attribute; confirm, Open Question 3]
- `EventOnDay.TimeLabel` and `EventViewModel.TimeLabel` use `ToString("HH:mm")` in view models, outside the views; D-09 scopes the views, so they stay. They render visible text in `_Calendar` and `Events/Details`; leave them.

## 5. D-13: Agenda/Index merge check against the Phase 94 D-13 criteria

Criteria (from the audit's merge section and Phase 95's qualification notes): the pair must be `identical` or `layout-only`, have no deliberate content difference, and its phone differences must be CSS-only. [CITED: `94-AUDIT-events.md` "Merge proposals (D-13)"; `95-03-PLAN.md` merge note]

Today's differences, read from both files this session:

| Aspect | `Agenda/Index.cshtml` | `Agenda/Index.Mobile.cshtml` | CSS-only? |
|--------|----------------------|------------------------------|-----------|
| Title | `h2` in `card-header` with `header-subtitle` | `h4` with `header-subtitle` | yes (after heading alignment, A-events-028) |
| Board filter | Bootstrap dropdown (`data-bs-toggle="dropdown"`, `data-bs-auto-close="outside"`, inline `style="min-width: 260px;"`) | `collapse` panel inside a `modern-card` | **no**: different component and interaction |
| Rows | one `modern-card` holding `div.agenda-row` rows | one `div.agenda-card` per row, outside any parent card | **no** (wrapper structure) |
| Roster | always visible (`div.agenda-roster`) | behind a "Show Roster" `collapse` toggle with inline `onclick="event.stopPropagation();"` on the toggle and the container | **no**: disclosure on one layout only |
| Action | `Open Event` / `Switch & Open` in an `hr`-separated `text-end` footer | same buttons, same modal | yes |
| Empty states, copy, `BoardPages` gate | identical | identical | yes |

**Verdict: it does not qualify.** Two differences (filter component, roster disclosure) are behaviour, not CSS. Unifying them forces a visible change on one layout: the desktop gains a roster collapse (a new collapse, against D-04's no-collapse rule for relayouts) or the phone loses its collapse (a relayout that removes a toggle; "removal is never offered"). A merge would also need an operator decision on which behaviour wins, which is a relayout with its own sign-off, and it is costly to reverse (D-13). **Recommendation: do not propose a merge at Gate 1. Record the verdict and these reasons on the Gate 1 sheet, keep the twins, and align them** (same heading outline, same markup vocabulary, shared `.agenda-*` rules where possible). If the operator asks for it anyway, the proposal is "roster collapsed on both" plus "filter as the same collapse on both", presented as a relayout.

**Tests a merge would change** (so the operator sees the cost): [VERIFIED: files read this session]
- `AgendaMobileRenderTests` (11 `[Fact]`/`[Theory]`): `Agenda_MobileUserAgent_RendersCardLayout_NotDesktopList` and `Agenda_DesktopUserAgent_RendersDesktopLayout_NotMobileCards` assert `agenda-card` vs `agenda-row` selection (they are the facts that prove the phone view is chosen, which a merge makes impossible by construction); `Agenda_MobileRoster_CollapsedByDefaultAndContainsMemberNames` (`roster-{id}`, `agenda-roster-toggle`); `..._RendersDetailsLink`, `..._RendersSwitchModalTriggerWithGroupIdAndReturnUrl`, `..._ShowsBoardNameAndTypeBadge` keep their intent.
- `AgendaControllerIntegrationTests` (22 facts) and `AgendaTenantIsolationTests` if they assert on `agenda-row`/`agenda-card` markup.
- `MobileViewsTests` (no `/Agenda` fact found by grep; recheck at plan time because Phase 96 edits the file).
- `UiFoundationLayerGuardTests` (the `agenda.mobile.css` list entries) and the new `EventsConversionGuardTests` lists and view-sheet link table.
- `BoardPageStaticGuardTests.FeatureSourceFiles` lists both Agenda views; missing files are skipped by `PresentFiles()`, so no change is needed.
- The merge would delete `Agenda/Index.Mobile.cshtml` and `agenda.mobile.css`; both layouts' stylesheet loading changes.

## 6. Inventory of current violations per file, guard tests, and audit findings by file

### 6a. Views (DoC-1): inline code and raw dates
[VERIFIED: grep of the 17 area files this session]

| File | `style=` | `<script>` | inline handlers | raw `.ToString` dates |
|------|----------|-----------|-----------------|-----------------------|
| `Calendar/Index.cshtml` | 0 | 0 | 0 | 0 |
| `Calendar/Index.Mobile.cshtml` | 0 | 0 | 4 (`:106,:117,:149,:172`, `onclick="window.location.href=..."`) | 2 |
| `Agenda/Index.cshtml` | 1 (`:53` `min-width: 260px`) | 1 (`:241`) | 0 | 2 |
| `Agenda/Index.Mobile.cshtml` | 0 | 1 (`:251`) | 2 (`:143,:152`, `stopPropagation`) | 2 |
| `Events/Index.cshtml` | 0 | 0 | 1 (`:62` row `onclick`) | 2 |
| `Events/Index.Mobile.cshtml` | 0 | 0 | 2 (`:75` card, `:91`/`:94` stopPropagation; the file has 3 handler lines) | 2 |
| `Events/Create.cshtml` | 0 | 1 (`:120`, token into `window.seriesAntiforgeryToken`) | 0 | 0 |
| `Events/Details.cshtml` | 0 | 2 (`:200` withdraw, `:293` set) | 7 (`:76,:85,:95` onsubmit confirm; `:194,:226,:229,:232` onclick) | 1 |
| `Events/Edit.cshtml` | 0 | 1 (`:103`, token and ids written into script) | 0 | 1 (machine value, allowed) |
| `Events/_SeriesFormScripts.cshtml` | 0 | 1 (`:8`) | 0 | 0 |
| `Series/Details.cshtml` | 1 (`:52` `pointer-events: none`) | 0 | 1 (`:146` onsubmit confirm) | 3 |
| `_Calendar.cshtml`, `_Calendar.Mobile.cshtml`, `_Linked*`, `_Availability*` | 0 | 0 | 0 | 4 (+ `DateTime.Today` at `_Calendar.cshtml:33`) |

Totals: 2 `style=`, 0 `<style>`, 7 `<script>`, 17 handlers. The Events/Create antiforgery token is the only place a script and a view share a value; `_QuestFormScripts` (rendered by Create and Edit) already emits `<meta name="request-verification-token">`.

### 6b. Stylesheets (DoC-2): counts [VERIFIED: regex counts this session, comments included]

| Sheet | Lines | hex | `rgb(a)` | `!important` | `@layer` | Notable |
|-------|-------|-----|----------|--------------|----------|---------|
| `calendar.css` | 860 | 32 | 34 | 52 (42 in the `.current-quest` block, `:673-765`) | none | month page + Quest Details vote/current-quest block; 2 raw `max-width` media queries (768, 480); `transition: all` x5; dead `.quest-date-indicator`, `.finalized-icon` |
| `calendar.mobile.css` | 177 | 20 | 12 | 2 | none | `.agenda-card-mobile` glass + `backdrop-filter` (one wrapper, acceptable); `.agenda-card-mobile .text-muted` utility-colour fight with `!important` |
| `events-overview.css` | 124 | 1 | 5 | 7 | none | fights `.modern-card` cell fills with `!important` |
| `events-overview.mobile.css` | 129 | 6 | 10 | 4 | none | `backdrop-filter` on repeated `.avail-card`; `#FFFFFF !important` count rules |
| `agenda.css` | 73 | 3 | 4 | 8 | none | `.agenda-row .text-muted` utility-colour fight |
| `agenda.mobile.css` | 69 | 3 | 6 | 1 | none | `backdrop-filter` on repeated `.agenda-card`; copies of the availability rules |
| **Total** | | 65 | 71 | 74 | | roadmap says 65 / 71 / 70 |

The two utility-colour fights the roadmap counts are `agenda.css` and `calendar.mobile.css`. `.avail-cell-yes-muted`, `.avail-cell-empty`, `.avail-count-*` and `.legend-card` are duplicated across files (A-events-050); one definition belongs in the components layer (`components.css` already holds the event-identity `.cycle-mask-*` rules; `.cycle-mask-cell` is already 32px there and needs the phone 44px rule and a name from the script).

### 6c. Guard and rendered-markup tests that read the area, and what each pins

| Test file | Reads | Pins (intent to keep) | Needed change |
|-----------|-------|-----------------------|---------------|
| `LinkedBoardStaticGuardTests` | `_LinkedDayEntries.cshtml`, `_LinkedAlsoThatDay.cshtml`, `_Calendar(.Mobile).cshtml`, linked model types, a `FeatureSourceFiles` planning-reference scan | linked partials carry none of `<input`, `<form`, `<button`, `<label`, `<select`, `<textarea`, `DateVotes`, `warning-icon-display`, `finalized-icon-display`, `current-quest`, `quest-event`, `calendar-event`, `data-quest-id`, `data-proposed-date-id`, `Html.Raw`, ` id="`, ` for="`, and never the word "vote"; spans only `visually-hidden`; `linked-day-count` is a div with only hidden spans; `_LinkedDayEntries` referenced exactly once in `_Calendar.cshtml` and after `isDetailsPage && questOnDay.Quest.Id == currentQuestId`; `_LinkedAlsoThatDay` once between the date label and the first `calendar-vote-row` in `_Calendar.Mobile.cshtml` | Keep the banned list intact; the detail block must therefore be built **without** `<button>`, `id`, `for` and `data-*` in the linked partials, and without the word "vote" (including "Voting open" in markup: use the status words "Proposed"/"Finalized" in the linked partials). The `_Calendar.cshtml` anchor string `isDetailsPage && questOnDay.Quest.Id == currentQuestId` must survive or the fact is rewritten to the same intent |
| `LinkedBoardCalendarStyleTests` | `calendar.css`, `calendar.mobile.css`, `quests.mobile.css` | `.linked-entry` has `border-left: 4px dashed`; `.linked-entry-detail` is `display: none` at rest and `display: block` on `:hover` and `.linked-entry--open:focus-visible`; cancelled linked entries `text-decoration: line-through` on `.linked-entry--cancelled`, its title, `.agenda-linked-entry--cancelled` (+title) and `.also-that-day--cancelled` | Rewrite per D-07: dashed outline on the compact row, detail card reveal, strike on cancelled. Selectors move in the layered sheet; keep the `ExtractRuleBody` helper and the quests.mobile.css fact (that sheet is Phase 95's) |
| `EventsOverviewMobileStyleTests` | `events-overview.mobile.css` | glass surface, no `#343a40`/`#495057`, `color: #FFFFFF;` and `color: #FFFFFF !important;` on count rules, `min-height: 44px` on the toggle; rendered: `btn btn-sm btn-secondary`, no `btn-outline-`, `avail-card`, `avail-count-summary`, `avail-card-meta`, `avail-roster-name` | Literal colours and `!important` conflict with the foundation: re-pin readable counts (token colour), a glass-or-paper surface, 44px targets |
| `MobileViewsTests` (events parts) | rendered `/Calendar`, `/Agenda` | mobile UA links `calendar.mobile.css`, renders `agenda-quest-entry`, not `calendar-grid`; `agenda-day-label`; `agenda-event-entry`; all-day wording; desktop UA renders no `agenda-quest-entry`; `_Calendar.Mobile` renders `calendar-date-entry-mobile` | Hooks stay; Phase 96 edits this file |
| `BoardPageStaticGuardTests` | planning-reference scan of listed files incl. both Calendar/Agenda views, `Events/Create`, `Events/Edit` | plain-language comments | none beyond keeping comments free of IDs |
| `AmbientClockSeamTests` | section 3 | | additions only |
| `UiFoundationLayerGuardTests` | `LayeredFiles`, `NoLiteralFiles`, layouts' link order, "firstLegacy" must exist | each listed sheet opens `@layer <name> {`, no `!important` outside reduced-motion/visually-hidden, no colour literal, no planning reference | append `("calendar.css","pages")`, `("calendar.mobile.css","pages")`, `("events-overview.css","pages")`, `("events-overview.mobile.css","pages")`, `("agenda.css","pages")`, `("agenda.mobile.css","pages")` and the same names to `NoLiteralFiles`. The layout fact still needs one legacy global sheet after the three links leave (it has `legacy-pages.css`, `shop.css`, `characters.css`, ... so it holds) |
| `LinkedBoardCalendarTenantIsolationTests` (26 hook references) | rendered `/Calendar` | `DayCell(html, n)` splits on `class="calendar-day` and needs the literal `<div class="day-number">{day}</div>`; `linked-entry--open|--static|--cancelled|--proposed|--finalized`, `linked-day-count`, `linked-entry-detail` + `aria-hidden="true"` + `visually-hidden` + `title="...BoardName...DM name..."` on a linked chip, `legend-card`, `legend-item linked`, the legend sentences, no `onclick` in a linked block | Rewrite `DayCell` once if the day-number markup changes; keep the rest by keeping the hook classes. Note the chip assertion requires a `title` attribute on linked chips, so keep it |
| `LinkedBoardMobileCalendarTests` (18) | rendered phone `/Calendar` | `agenda-linked-entry--open|--static|--cancelled|--proposed|--finalized`; `questRow.Should().Contain($"window.location.href='/Quest/Details/{questId}'")` (`:308,:313`) and `NotContain("onclick")` on static rows (`:339`) | The two `window.location.href` assertions pin the inline handler and must be rewritten to "the row has one link to that URL and no handler"; static-row intent kept |
| `LinkedBoardQuestDetailsTests` (13), `LinkedBoardEndToEndTests`, `CalendarBoardTypeScopeTests`, `CalendarHorizonBannerTests`, `CalendarControllerIntegrationTests`, `EventCalendarPartialTests`, `CalendarDungeonMasterSessionTests` | rendered pages | `calendar-linked-note`, `linked-day-count`, `calendar-event`/`calendar-events` order before `quest-events`, `legend-item event`, "Click quests or events for details", `current-quest` | keep hooks, keep strings |
| `EventDetailsAvailabilityRenderTests` | `/Events/Details` | `onclick="setAvailability(` count == 3; `withdrawAvailability(` present/absent by board type and signup | rewrite to the data-attribute buttons: three answer buttons, withdraw control present only for OneShot + own row |
| `EventsOverviewControllerIntegrationTests` (11) | `/Events` | `avail-card`, `avail-card-title`, `avail-grid`, `avail-expand-toggle`, `roster-{id}`, `event.stopPropagation()` count == 2 (`:262`) | rewrite the `stopPropagation` fact: toggle and container carry no handler, collapse works via `data-bs-toggle` |
| `RowNavigationAccessibilityTests` | `/Events` both UAs and quest pages | `<a class="row-nav-link" href="...Events/Details/{id}...">`, `avail-row-clickable`, `avail-card` | keep the anchor markup, `avail-row-clickable` and the hooks |
| `AgendaMobileRenderTests`, `AgendaControllerIntegrationTests`, `AgendaTenantIsolationTests` | `/Agenda` | `agenda-card` vs `agenda-row`, `roster-{id}`, `agenda-roster-toggle`, `Open Event`, `Switch &amp; Open`, `data-group-id`, return URL | keep hooks |
| `StaleAvailabilityOverviewLabelGuardTests` | rendered labels | the retired label never reappears, "Board Availability" present | keep copy verbatim |

Test totals are from the greps above; the counts in the "hook references" column are line counts of matching assertions, not facts.

### 6d. 94-AUDIT-events findings grouped by file (51 findings; S1 3, S2 23, S3 22, S4 3)

| File | Findings |
|------|----------|
| `Shared/_Calendar.cshtml` | 013 (hover-only DM/time), 014 (status by colour alone), 016 (icon-only key/finalized/vote, hidden text), 017 (`DateTime.Today`), 026 (vote controls, host Quest Details), 034 (`transition: all`, hover layout shift), 049 (dead rules); plus Phase 95's 51 warning icons without `aria-hidden` |
| `Calendar/Index.cshtml` | 008 (contrast), 015 (nav chevrons unnamed), 018 (320 overflow, relayout R2/Q-116), 029 (today ring), 039-046 (twin content, Q-40..Q-47 answers), 051 (title icon colour) |
| `Calendar/Index.Mobile.cshtml` | 009, 022 (22px links, inline `onclick`), 028 (no heading), 030 |
| `Agenda/Index(.Mobile)` | 003, 004, 005, 007, 023 (24px filter rows), 028, 030, 032, 035, 038 |
| `Events/Index(.Mobile)` | 001, 002, 003, 005, 006, 024, 028, 030, 032, 035, 047/048 (Q-48, Q-49 deliberate) |
| `Events/Create` | 007, 010, 020 (label targets `IsRecurring`, input is `enableRecurrence`: `Create.cshtml:51-52`), 021 (cycle cells 32px, names), 025, 027, 030, 038 |
| `Events/Edit` | 007, 010, 027, 030 |
| `Events/Details` | 007, 010, 019 (390px overflow, 398 wide), 025, 027, 030, 031 (confirm/alert wording kept) |
| `Series/Details` | 010, 011 (five identical "View" links), 022, 030, 031, 036 (modal name, empty `th`, inline style), 049 |
| `_AvailabilityCell`, `_AvailabilityCounts` | 001, 002, 006, 050 |
| `_SeriesFormScripts` | 021, 030 |
| `calendar.css` / `calendar.mobile.css` / `events-overview*.css` / `agenda*.css` | 012 (tiny text), 033 (cascade fights), 034, 035, 037 (hard-coded `#0d6efd` focus), 049, 050 |

S1 owned: 001 (attendance counts cream on cream), 002 (availability badge labels), 003 (dark subtitle on bare board; fixed by the foundation, verified here). 003 and 004 are noted in the audit as foundation-fixed and only verified here.

## 7. Calendar-fit mockup to production port

### 7a. What maps where

| Mockup piece | Production target | Notes |
|--------------|-------------------|-------|
| `calendar-fit.css` `.cf-mark*` status marker (glyph on a status fill with a ring) | `calendar.css` `@layer pages` (`.cal-mark*`) and the phone agenda list (same marker, Q-46 status markers) | colours `--qb-warning`, `--qb-success`, `--qb-event`, `--qb-neutral` and `-fg` pairs, all in `tokens.css` |
| `.cf-entry`, `.cf-more` | `calendar.css` | 20px, 13px text, 2px gap; linked = `1px dashed var(--qb-ink-muted)` plus alternate parchment and a link glyph |
| `.cf-pop`, `.cf-entry-card`, `.cf-day-panel`, `.cf-day-row` | `calendar.css` | opaque overlay surface (`--qb-surface-overlay`, `--qb-shadow-overlay`); 48px panel rows; reduced-motion block disables the fade |
| `.cf-page` (100dvh, `--cal-slots` height bands), `.cf-main`, `.cf-grid` rows from `--cal-weeks` | `.calendar-page` wrapper in `Calendar/Index.cshtml` + `calendar.css` | the real page has the layout's navbar and no 32px mock strip; footer sits below the fold (D-04) so the page is `min-height`-fitted rather than `height: 100dvh` of a footer-less body: compute the grid from `100dvh - var(--qb-nav-h) - chrome` and let the footer follow |
| `.cf-head` one-line header (title, My Agenda, legend, month nav) | `Calendar/Index.cshtml` header; at `< 992px` it wraps to Q-116's rows (nav row, then a wrapping button row) | the mockup omits "Board Availability" (DM only): add it to the button group; both buttons must wrap, not overflow |
| `.cf-legend` | desktop header legend + phone legend (Q-40) | see Pitfall 8: real legend has more content than the mockup's one line |
| `calendar-fit.js` `render`/`buildCell`/`buildEntry`/`buildRow` | **dropped** (server renders) | replaced by Razor and `cloneNode` |
| `calendar-fit.js` card, panel, focus return, Esc suppression, tap logic, outside-click, Tab loop | `wwwroot/js/calendar-month.js` | port the logic; remove `mode`/hover branch (D-01), `setMode`, the mockbar listeners, `moreData` (use a `WeakMap` from button to the cell element) |
| `month-data.js`, `tokens-extra.css`, `.cf-mockbar` | not ported | |

### 7b. Mockup classes that are not in the foundation
`modern-card--panel`, `page-title`, `page-title--quiet`, `calendar-head`, `calendar-nav`, `calendar-nav__btn`, `calendar-month-name`, `calendar-grid__head`, `calendar-day--blank`, `calendar-day--today`, `calendar-empty-note`, `legend-swatch*`, `agenda-day*`, `cal-chip*` come from Direction A's `pages.css`/`components.css`, not from the production `tokens/base/components/shell` sheets. [VERIFIED: grep of the five foundation sheets for each name this session, no hit; the `agenda-card`/`agenda-day` names that do hit are in `calendar.mobile.css`/`agenda.mobile.css`] They must be written into the area sheets (copying the Direction A rule bodies, tokens only). Do not rely on them existing.

### 7c. Behaviour requirements for the module
- Reads `--cal-slots` (0 = off). Recomputes on a height-band or width change (`matchMedia` change or the mockup's rAF-debounced `resize`), restoring entries before re-hiding.
- Hiding: set the `hidden` attribute on overflow `li`s (not inline style), append one `li > button.cal-more` (`aria-haspopup="dialog"`, `aria-expanded`, `aria-controls`, accessible name "+N more" plus visually hidden " entries on Tuesday, 10 November 2026").
- Card: one instance, `aria-hidden="true"`, positioned with `style.setProperty('--pop-x'/'--pop-y')` (precedent `quest-board.js:36-47`; no CSP exists in the app today by grep). Detail content: status, full title, date, time, DM (expanded entry only: Q-42 deliberate), board, "No building key available", the viewer's availability.
- Static linked entries: no link, no tab stop; the card opens on pointer hover only (D-06). Panel rows for static entries are non-interactive `div`s.
- Tap: first tap shows the card, second tap on the same entry follows the link; on a static entry a tap does nothing.
- Panel: `div role="dialog" aria-modal="false" aria-labelledby`, 420px, 48px+ rows, Tab loop, Esc and close button and outside click, focus returns to "+N more" (the mockup's `panelRestoreFocus` logic).
- Reduced motion: `@media (prefers-reduced-motion: reduce)` switches off the fade and the button transition.
- Without JS: all entries visible, cell items scroll (`overflow-y: auto`) as today's `.day-cell-items` does.

### 7d. Quest Details is not a compact month
Branch on `isDetailsPage` inside `_Calendar.cshtml`; the details branch keeps Phase 95's vote layout (the `.vote-choice` labels with hidden words, `vote-buttons`, `current-quest`). Required work there: weekday header 4.5:1 (D-08: wood-filled header row from Direction A, `background: var(--qb-wood)` with `--qb-fg` text, 5.63:1 in the mockup); `aria-hidden="true"` on the decorative icons and visually hidden text for the key warning, finalized check and the viewer's vote (A-events-016); the `calendar-linked-note` line and linked entries unchanged in content. Convert the `.current-quest` block (42 `!important`) to layered selectors: because `calendar.css` becomes `@layer pages`, the unlayered-beats-layered inversion disappears, so most `!important` can simply be dropped; verify with a computed-style sweep of Quest Details in both layouts (Gate 2 includes Quest Details, D-11). Keep `quest-vote.css` (Phase 95) untouched.

### 7e. Risks of `_Calendar` being shared
(1) A class used by Quest Details but renamed here breaks Phase 95's verified layout: keep `.quest-event`, `.current-quest`, `.vote-buttons`, `.details-page` rules for the details branch. (2) `LinkedBoardStaticGuardTests` and the `DayCell` helper (section 6c). (3) `ViewBag.BoardToday` must be set by every host (Calendar/Index and Quest/Details both do after plan 97-04). (4) `calendar.css` stops being global: the three pages that need it are Calendar/Index and Quest/Details; `Quest/Details.Mobile` does not link it and renders `_Calendar.Mobile` (no `calendar.css` classes), so nothing else needs it. [VERIFIED: grep of the whole `Views` and `Areas` trees for `calendar.css`]

### 7f. Hidden risk: Events/Details A-events-019 depends on Phase 96
The 4px phone overflow is a bare `.row` in `.container-fluid` (394 at 390). Phase 96's plan 09 fixes the root for `AccessDenied` ("top-level `.row` gutter on the phone layout"). Phase 97 must re-read what Phase 96 actually landed, then only verify (or, if Phase 96 scoped it to `AccessDenied` alone, apply the same rule to `Events/Details`).

## 8. Gate review packaging: self-contained orphan review branch

Requirement: pushed, no app code, only the static review sheet and images, viewable without the app, built from a shared checkout without ever switching its branch. Precedent: Phases 95 used `feature/phase-95-gate1-mockups` and `feature/phase-95-gate2-review` (README.md, `gate2/index.html`, 330 downscaled JPEGs, no symlinks, no localhost links); those were pushed and later auto-deleted by the repo's head-branch deletion. [CITED: `95-03-SUMMARY.md`, `95-18-SUMMARY.md`; project memory notes the auto-delete] CI is not triggered by such a push: `dotnet.yml` runs on pushes to `main` and on pull requests to `main`, `docker-publish.yml` and `release.yml` run on `v*.*.*` tags only. [VERIFIED: `.github/workflows/*.yml` `on:` blocks]

**Recommended method: a temporary index and `git commit-tree` (the checkout's HEAD, index and working tree are never touched).** Tested this session in a throwaway repository under the scratchpad (not the project repository): the main branch, status and index stayed unchanged, the new ref was a root commit with only the staged files, and the push created the remote ref.

```bash
REPO=/mnt/Data/repos/quest-board-dnd
STAGE=~/qb-ui-review/phase-97/gate1-branch      # contents: README.md, index.html, img/ (self-contained, relative links only)
BR=feature/phase-97-gate1-review                # gate 2: feature/phase-97-gate2-review
IDX=$(mktemp -u ~/qb-ui-review/phase-97/.review-index.XXXXXX)

cd "$STAGE"
GIT_INDEX_FILE="$IDX" git --git-dir="$REPO/.git" --work-tree="$STAGE" add -A -f .   # -f: image globs may be ignored
TREE=$(GIT_INDEX_FILE="$IDX" git --git-dir="$REPO/.git" write-tree)
COMMIT=$(printf 'review: phase 97 gate 1 sheet\n\nCo-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>\n' \
  | git --git-dir="$REPO/.git" -c commit.gpgsign=false commit-tree "$TREE")           # no -p => orphan root
rm -f "$IDX"
git --git-dir="$REPO/.git" update-ref "refs/heads/$BR" "$COMMIT"
git --git-dir="$REPO/.git" push origin "refs/heads/$BR:refs/heads/$BR"                # explicit refspec only
```
For a v2 redraw, pass `-p "$(git rev-parse refs/heads/$BR)"` to `commit-tree` so the branch grows (history of redraws), and push with a plain fast-forward.

Why not the alternatives:
- `git worktree add --orphan` (git 2.53 supports it [VERIFIED: `git version 2.53.0`]) is safe for the main checkout, but it creates `.git/worktrees/*` metadata and a second working directory, and the repo already carries `.claude/worktrees/shop-create-fix-23fe1a` for another session; one more registered worktree is avoidable state. It is an acceptable fallback if the temp-index method misbehaves.
- `git checkout --orphan` in the shared checkout would switch the branch and rewrite the index under a running Phase 96 session: forbidden.

Safety rules for the plan:
- Pass `--git-dir` and `--work-tree` explicitly (or run from the stage directory), never `git add` inside the repo with the temp index and the repo's work tree.
- Push only the explicit `refs/heads/<name>:refs/heads/<name>`; never bare `git push`, `--all`, `--mirror` or `--force` (the project forbids pushing to main; this ref never merges: it shares no history and must not be opened as a PR).
- Before pushing, assert the tree: `git --git-dir=... ls-tree -r --name-only "$COMMIT"` contains only `README.md`, `index.html` and files under `img/`, no `.cs`, `.cshtml`, `.css` outside the sheet, no `credentials.json`, no `localhost` or `127.0.0.1` in any HTML, no symlinks (`ls-tree` mode `120000`), and the sheet opens from disk (`file://` Playwright load with zero failed requests).
- Content hygiene: fantasy fixture data only, no member names or emails (the Audit Board accounts are `@example.invalid`), no connection strings. Downscale images (precedent: JPEG) to keep the branch small.
- Naming: `feature/phase-97-gate1-review` and `feature/phase-97-gate2-review` (matches the `feature/<short-description>` convention in `CLAUDE.md` and the Phase 95 precedent). The summaries record the commit SHA.
- Viewing without the app: the operator downloads with `git clone --depth 1 --branch feature/phase-97-gate1-review <url> review && open review/index.html`. Add a `README.md` with these two commands and a markdown version of the pair list with relative image links, so the branch also reads in GitHub's file browser without cloning.
- The Phase 96 environment note says "never push (the orchestrator pushes)". A review branch push is the one push this phase needs mid-run; it must be an explicit step the orchestrator or the operator approves in the plan (as in Phase 95, "at the coordinator's request"), and only the review ref is pushed.

## 9. Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.2 + FluentAssertions; `Microsoft.AspNetCore.Mvc.Testing`; EF InMemory, no database [VERIFIED: csproj files; CLAUDE.md "Integration tests use EF Core InMemory"] |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (`parallelizeAssembly: false`) [CITED: 96-RESEARCH.md] |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~EventsConversionGuardTests\|FullyQualifiedName~LinkedBoard\|FullyQualifiedName~EventsOverview\|FullyQualifiedName~Agenda\|FullyQualifiedName~EventDetailsAvailability\|FullyQualifiedName~RowNavigation\|FullyQualifiedName~UiFoundation\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~CalendarControllerIntegrationTests\|FullyQualifiedName~EventCalendarPartialTests\|FullyQualifiedName~CalendarBoardTypeScopeTests"` and `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~AmbientClockSeamTests\|FullyQualifiedName~WallClockMarkupTests\|FullyQualifiedName~LocalTimeMarkupTests"` (filter syntax cited from Phase 95/96; not run here: a build would write `bin/obj` in a checkout Phase 96 shares) |
| Full suite command | `dotnet test` (solution; needs no database). Test projects: `QuestBoard.UnitTests`, `QuestBoard.IntegrationTests`. SQL-backed migrator tests additionally need `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and the SQL Server at `localhost:1433` [VERIFIED: `QuestBoard.IntegrationTests/Migrator/MigratorSqlServer.cs` references it]; they are not affected by this phase and need not be run per slice, only once at the end. Re-run once on `0x80131506`; never set GC env vars |
| Browser checks | scratch Playwright under `~/qb-ui-review/phase-97/` (not in the suite), mobile checks under a mobile User-Agent with touch |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-10 | Today highlight follows the board clock at 2026-09-05T23:30Z; partials in the ambient guard | integration + unit static | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CalendarBoardClockTests"`; `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~AmbientClockSeamTests"` | new (Wave 0) / edit |
| D-09 | Each moved line prints its old text; four new styles pinned; existing helper tests unchanged | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~WallClock"`; rendered-text facts in the new `EventsDateRenderTests` | new (Wave 0) |
| D-06 | Static linked entry: no link, tab stop, data attribute, onclick; hidden text stays | static + integration | `...~LinkedBoardStaticGuardTests`, `~LinkedBoardCalendarTenantIsolationTests` | exists, edit |
| D-07 | Member linked entry is one link to that board's page with dashed outline hook | integration + static | `...~LinkedBoardCalendarStyleTests` | exists, rewrite |
| D-01/D-02/D-03 | Slots table in CSS, no hover mode in the module, card/panel behaviours | static (CSS/JS source) + Playwright behaviour | `...~EventsConversionGuardTests`; Playwright `behaviour-check` (scratch) | new |
| D-04/D-05 | Fit at 1280x720 and up; no fit rule below 992px | Playwright only (no unit proof of layout) | scratch sweep `fit-check` at 1280x720, 1366x768, 1440x900, 1920x1080, 1280x700, plus 991x720 | manual-only, evidence stored |
| D-08 | Weekday header contrast on Quest Details | Playwright contrast probe (scratch `contrast.mjs`); static no-exception entries | scratch sweep; `contrast-exceptions.json` holds no weekday entry | manual + evidence |
| D-11/D-12 | Gate 1 and Gate 2 approvals | human checkpoint | n/a | n/a |
| D-13 | Agenda twins keep selection facts | integration | `...~AgendaMobileRenderTests` | exists |
| DoC-1 | No inline style/script/handler in listed views | static | `...~EventsConversionGuardTests.ConvertedViews_CarryNoInlineStyleScriptOrHandler` (clone of the Quests guard) | new |
| DoC-2 | Zero hex/rgb/`!important` in area sheets, layered | static | `...~UiFoundationLayerGuardTests` after appending the six sheets | exists, edit |
| DoC-3 | Sheets linked from `@section Styles`, not the global list | static | `EventsConversionGuardTests.ConvertedViews_LinkTheirSheets` + `UiFoundationLayerGuardTests.Layouts_LinkFoundationInCascadeOrder` | new + exists |
| DoC-4 | Twin answers present (legend, 20px title, own vote, key/finalized, today, empty month) | integration (phone and desktop markup) | `...~MobileViewsTests`, `~LinkedBoardMobileCalendarTests`, new facts for each answer | exists + new |
| DoC-5 | Accessibility bar per page | Playwright sweeps (contrast, focus, names, 320/390 overflow, reduced motion) | scratch `sweep.mjs` | manual + evidence |
| DoC-6 | BEFORE/AFTER stored outside the repo; relaid-out pages signed off | human | Gate 2 sheet | n/a |
| DoC-7 | Mobile checks under a mobile UA | Playwright (`devices['Pixel 7']`, touch) + the existing mobile-UA integration facts | scratch + `~MobileViewsTests` | exists |
| DoC-8 | Guards rewritten, none deleted | static | the whole guard list in 6c; verify test counts do not drop (compare `dotnet test --list-tests` count before/after) | process |
| DoC-9 | `Html.LocalTime`/`WallClock` untouched; nothing writes to `Events`/`Quests`; feed files untouched | static + unit | `git diff --stat` shows no change to `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`; `~WallClockMarkupTests` `~LocalTimeMarkupTests` unchanged; the existing guard that pins writes through the change tracker stays green | exists |
| DoC-10 | Every page still works | full suite + Playwright sweep | `dotnet test` | exists |

### Sampling Rate
- **Per task commit:** the quick run for the files the task touched (the two commands above, narrowed).
- **Per wave merge:** the full `dotnet test` for both projects (run per project to avoid the CLR flake).
- **Phase gate:** full suite green, then the scratch Playwright sweep (both UAs) and `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs`: clone of `QuestsConversionGuardTests` (converted views, sheets, link table, planning-reference scan, scanner self-test), with the date-format allowlist extended to `yyyy-MM-dd`
- [ ] `QuestBoard.IntegrationTests/Controllers/CalendarBoardClockTests.cs`: Q-133 facts (desktop cell and phone agenda marker)
- [ ] `QuestBoard.UnitTests/Extensions/WallClockAreaStylesTests.cs`: the four new styles' text and `datetime` shape (new file; the existing two test files stay unedited)
- [ ] `QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs`: rendered text per moved line (rows 1-18 above)
- [ ] a static guard for `calendar-month.js`: no `innerHTML`, no `.style.` assignments other than `setProperty('--pop-...')`, no hover-open mode, reads `--cal-slots`
- [ ] scratch tooling copied and re-pointed in plan 97-01 (`run-app`, `login`, `audit-shoot`, `sweep`, `probes`, `contrast`, `gate*-sheet`)
Framework install: none (xUnit and Playwright already present).

## Security Domain

`security_enforcement` is not set to false in `.planning/config.json` (only `nyquist_validation: true` and `use_worktrees: true` were found), so it is enabled.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (no auth change) | existing |
| V3 Session Management | no | existing |
| V4 Access Control | yes | `CanOpen` for linked entries stays server-decided; inert entries carry no URL, id or data attribute; DM-only buttons keep `AuthorizeAsync("DungeonMasterOnly")` and `pages.Serves(BoardPage.Events)`; the Agenda board switch still posts to `GroupPicker/SelectGroup` |
| V5 Input Validation / output encoding | yes | Razor encoding for every value; the module reads server-rendered text and clones nodes, no `innerHTML`; confirm messages go into attributes (Razor attribute encoding) and are used with `confirm()` as text |
| V6 Cryptography | no | none added |
| V13/CSRF | yes | antiforgery token travels in `<meta name="request-verification-token">` instead of inline script text; the `fetch` POST/DELETE endpoints are unchanged |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Cross-board data leak through a linked entry (roster, vote, description) | Information disclosure | linked partials stay form-free and answer-free; `LinkedBoardStaticGuardTests` model-type guards stay; the detail block for a linked entry holds only title, board, DM, time, status |
| DOM XSS in the card/panel | Tampering | text via `textContent`/`cloneNode` of server-encoded nodes only |
| Token exposure in inline script | Information disclosure | meta element, module reads at call time |
| Review branch leaks personal data or credentials | Information disclosure | fantasy fixtures only, tree assertion before push (section 8), no `credentials.json` |
| Calendar feed revision drift | Tampering | nothing in this phase writes to `Events`/`Quests`; feed files untouched |

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes | 10.0.115 (`dotnet --version`) | — |
| Node.js | scratch Playwright tooling | yes | v24.19.0 | — |
| Playwright 1.64.1 | captures, probes, behaviour checks | yes, via symlink to `~/qb-ui-review/phase-94/tools/node_modules` | 1.64.1 | none needed; never `npm install` |
| Chromium (bundled with Playwright) | captures | yes (used this session to measure the mockup) | — | — |
| Python 3 | gate sheet builders, diff tools | yes | 3.14.4 | — |
| SQL Server `localhost:1433` | running the app for captures (not for tests) | listening (`ss`); belongs to the operator's `mssql-dev` container | — | stop and tell the operator; never start or provision |
| Ports 8000 / 8001 | working tree and frozen baseline | **in use** (Phase 96's app and baseline) | — | wait until Phase 96 has finished; plan 97-01 re-checks with `ss -ltnp` (never `pgrep -f` loops) |
| `~/qb-ui-review/phase-96/credentials.json` (roles `board`, `boardId`, `accounts`) | login | yes | Audit Board id 7 | reuse; add no new accounts unless a fixture needs one |
| `rtk`/`rg` | n/a | not relied on | — | use Grep/`grep` |

**Missing dependencies with no fallback:** none at research time. **Blocking at execution time:** Phase 96 still running in the shared checkout and on ports 8000/8001.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Production titles have similar length to the mockup's fantasy titles (avg 26 chars); Linux Chromium resolves `system-ui` close enough to the operator's fonts | Section 1 | Truncation numbers shift by a few characters; re-measure on Audit Board data at Gate 1 |
| A2 | The production host culture is invariant or English, so `BuildWallClock`'s invariant output equals today's current-culture `ToString` | Pitfall 2, section 4 | On a non-English `LANG`, day and month names would change from the host language to English. Confirm `LANG` on the CT or accept |
| A3 | The user prefers call-site `DateOnly.ToDateTime(...)` over adding `DateOnly`/`TimeOnly` overloads to `Html.WallClock` | Section 4 | A planner who adds overloads touches the helper the roadmap says is untouched |
| A4 | Using a view-model `TimeLabel` for the `title` attribute satisfies D-09 where `<time>` cannot go in an attribute | Section 4 | If the user wants zero `.ToString` even in view models, a new plain-text helper method is needed (also a helper change) |
| A5 | New server-only styles with no `site.js` entry are acceptable even though `ui-guidelines.md` says both sides share the four names | Section 4 | If the user wants parity, adding client entries would change visible `HH:mm` text per locale |
| A6 | The operator accepts the legend content moving into a header legend plus popover or second line, with the real strings kept | Pitfall 8 | Tests assert those strings; the drawing at Gate 1 must show them, else copy changes need approval |
| A7 | One `calendar.css` (month page + details block, scoped) is preferable to two sheets | Pitfall 5 | Two sheets avoid shipping month rules to Quest Details but split the shared header rule |
| A8 | The review branch push is permitted mid-phase despite the Phase 96 environment note "never push" | Section 8 | Without it the operator cannot view the gate without the app |

## Open Questions

1. **Does the operator accept the date text being written to the format table only (no `site.js` change)?**
   - What we know: byte-identical text requires it; `ui-guidelines.md` wording says the sides agree.
   - What's unclear: whether the parity sentence should change or the user wants the client table extended.
   - Recommendation: server-only; update the one paragraph in `ui-guidelines.md`.
2. **Call-site conversion versus overloads for `DateOnly`/`TimeOnly`** (A3). Recommendation: call-site.
3. **The `title` attribute on `_Calendar` quest entries** (A4). Recommendation: view-model `TimeLabel`; drop the tooltip on the Calendar page (the card replaces it), keep it on Quest Details.
4. **Where the real legend strings live on the desktop calendar** (A6). Recommendation: put the full legend in the Gate 1 drawing with the four statuses and the linked entry in the header line and the linked-board names plus the two explanatory sentences in a "What do these mean?" disclosure matching Q-48's phone Events pattern; do not drop "No building key".
5. **Events/Details cancelled alert and the phone order for DMs.** On the phone the left column (title card, DM Actions) precedes the right column in DOM order, so Q-115 "first card after the title block" differs for a DM (Actions card sits between). What's unclear: whether Actions should follow Availability for DMs on the phone. Recommendation: draw both at Gate 1 (Availability first, then Actions, versus today's DOM order) and let the operator choose; the cancelled-session alert should sit above Availability.
6. **Phase 96's gutter fix scope** (section 7f). Recommendation: re-read at plan time.
7. **Phase 96 exit timing.** Ports 8000/8001 are occupied now. Recommendation: plan 97-01 gates on Phase 96 completion.

## Project Constraints (from CLAUDE.md)

- Never commit to `main`; work on `milestone/v9-rolling-improvements`; push per project memory only when told (this phase's Phase 96 note says the orchestrator pushes); review branches via the section 8 method.
- SQL Server `localhost:1433`: never install, start, provision or tear down; auth mode Windows-only in `appsettings.json` (do not edit it; user-secrets hold SQL auth here); on Linux never set `DOTNET_GCHeapHardLimit`; re-run once on `0x80131506`.
- No GSD planning references in source, CSS, XML doc comments or string literals (no `D-06`, `Q-133`, `Phase 97`, `RESEARCH.md`, `A-events-xxx` in code). Guards already scan for this; the new `EventsConversionGuardTests` includes the scan. This file's own tags are fine because it is under `.planning/`.
- Before editing any Razor view read `.claude/ui-guidelines.md` (card pattern, filled buttons, `<hr>` before the button row, tokens only, no inline code, dates through the helpers, twins).
- Layer rules: Service to Domain to Repository; EF only in Repository; writes to `Events` and `Quests` only through the change tracker (this phase writes none); `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` are high-risk and untouched.
- Server-side "what day is it" reads go through `IBoardClock`, never `DateTime.Now/UtcNow/Today`; `AmbientClockSeamTests` is closed and only grows.
- Wall-clock values render via `Html.WallClock` (never converted); real instants via `Html.LocalTime`.
- Never cite a fixed member count for the board (project memory).
- Executors invoke the `senior-frontend` skill for front-end work (D-15). RIP MCP navigation is preferred when available; it was not used in this research session (not exposed), file reads and grep were used instead.

## Sources

### Primary (HIGH confidence)
- Repo files read this session: `97-CONTEXT.md`, `97-DISCUSSION-LOG.md`, `ROADMAP.md` Phase 97 section, `94-AUDIT-events.md`, `94-AUDIT.md` (Q-133/Q-134/G1-R2 lines), `95-REVIEW.md` WR-05, `95-18-SUMMARY.md`, `95-VERIFICATION.md`, `96-RESEARCH.md` and the `files_modified` of `96-*-PLAN.md`, `.claude/ui-guidelines.md`, `.claude/architecture.md`, `CLAUDE.md`
- All 17 area views, `CalendarController.cs`, `CalendarViewModel.cs`, `LinkedQuestOnDay.cs`, `HtmlHelperExtensions.cs`, `site.js` (hydration), `MobileViewLocationExpander.cs`, `components.css`/`home.mobile.css`/`quest-log.css` (stretched link), `calendar.css` (full), `calendar.mobile.css` (head), `tokens.css` (token names)
- Tests read: `AmbientClockSeamTests`, `WallClockMarkupTests`, `SiteJsHydrationTests`, `WallClockUnmovedTests`, `LinkedBoardStaticGuardTests`, `LinkedBoardCalendarStyleTests`, `EventsOverviewMobileStyleTests`, `UiFoundationLayerGuardTests`, `QuestsConversionGuardTests`, `QuestStatusBoardClockTests`, parts of `LinkedBoardCalendarTenantIsolationTests`, `EventCalendarPartialTests`, `CalendarControllerIntegrationTests`, `EventDetailsAvailabilityRenderTests`, `EventsOverviewControllerIntegrationTests`, `AgendaMobileRenderTests`
- `~/qb-ui-review/phase-94/mockups/calendar-fit/` (NOTES.md, `calendar-fit.css`, `calendar-fit.js`, `calendar-month.html`, `month-data.js` head), `~/qb-ui-review/phase-96/ENVIRONMENT.md`, `~/qb-ui-review/phase-95/tools/` (`run-app.mjs`, `audit-shoot.mjs` heads), Direction A `pages.css` calendar section and `png/calendar-390.png`
- Measurements and the throwaway-repo test of the temp-index method, run this session (scratchpad only)

### Secondary (MEDIUM confidence)
- Phase 95 precedent summaries for the review branch names and contents (branches themselves no longer exist)

### Tertiary (LOW confidence)
- Production host culture and `LANG` (not inspected); the user's preference on helper overloads and the `title` attribute (assumptions A2-A5)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new packages; every token and class named was checked against the files.
- Architecture: MEDIUM, the server-render-plus-enhance port is a design derived from a client-rendered mockup; the module is new code.
- Pitfalls: HIGH, each is tied to a file and line read this session.
- D-14 numbers: MEDIUM, measured on the mockup's data.

**Research date:** 2026-11-12
**Valid until:** about 2026-12-10, or sooner if Phase 96 lands edits to `components.css`, `shell.mobile.css`, `MobileViewsTests.cs`, `UiFoundationLayerGuardTests.cs` or `_Layout*.cshtml` (re-read those before planning each affected slice).
