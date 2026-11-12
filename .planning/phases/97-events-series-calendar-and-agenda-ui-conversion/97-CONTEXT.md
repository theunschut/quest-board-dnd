# Phase 97: Events, Series, Calendar and Agenda UI conversion - Context

**Gathered:** 2026-11-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Convert the calendar, agenda, events overview, event details, event create and edit, and series
pages, with their shared partials (`Shared/_Calendar`, `_Calendar.Mobile`, `_LinkedDayEntries`,
`_LinkedAlsoThatDay`, `Events/_AvailabilityCell`, `_AvailabilityCounts`, `_SeriesFormScripts`), to the
Phase 94 foundation. The desktop month fits without scrolling at 1280 by 720 and up, and every date
still renders through the wall-clock and local-time helpers. The scope, counts, stylesheet list,
`@section Styles` moves, guard tests, deferred items and the ten-point "Definition of converted"
with its phase-specific extras are fixed by `.planning/ROADMAP.md` § "Phase 97" and are not
repeated here.

Every content question for this area was answered at the Phase 94 G1 sign-off (2026-11-01) and is
inherited as recorded in `94-AUDIT.md` § "Operator questions for sign-off":

- Q-40, Q-41, Q-44 to Q-47 "add it to the other view": the phone calendar gains the legend, a 20px
  page title, the viewer's own vote, the key warning and finalized check, and a today marker; the
  desktop month gains the "Nothing This Month" empty state. Each carries the G1-R2 constraint
  recorded beside it (nothing pushes the desktop month below the fold; status items are single
  markers on the compact entry, never a taller cell).
- Q-42, Q-43, Q-48, Q-49 deliberate: the phone "Also showing" line, the DM name only in the expanded
  desktop entry, the phone Events legend behind "What do these mean?", and no "Attendance" column
  label on the phone.
- Q-115 (Events/Details: "Your Availability" above "Description", order only) and Q-116
  (Calendar/Index below the large breakpoint: navigation on its own row, the two buttons in a
  wrapping row), approved in principle and re-confirmed with their AFTER here; plus G1-R2, the
  desktop month fit.
- Q-133: the today highlight moves to the board clock, with its own test and a guarded-list entry.
- Q-134: Phase 95 patched the `_Calendar` vote UI; this phase owns the partial and the vote and
  `.current-quest` block of `calendar.css`, and re-verifies Quest Details in both layouts.
- No merges: none qualified at G1.

This discussion confirmed the "+N more" behaviour that G1-O3 left open (the operator's confirmation
"before this phase starts" is recorded here as D-01) and settled the remaining implementation and
process choices. Process decisions mirror Phase 95 D-18 to D-24 and Phase 96 D-01 to D-06, as the
operator expects answers already given to carry over. No capability is added: viewing the month,
the agenda and the events overview, answering availability, creating, editing, moving and cancelling
events and series keep working exactly as today.

</domain>

<decisions>
## Implementation Decisions

### Desktop month: "+N more" and the compact entries (G1-O3 / G1-R2 confirmed)
- **D-01: "+N more" opens the day panel on click, tap or Enter only.** No hover-to-open mode. This is
  the calendar-fit mockup's default "click mode": a non-modal dialog anchored to the cell, focus
  moves into it, Tab loops inside, Esc, the close button or an outside click close it and focus
  returns to "+N more". The mockup's hover mode and its switcher strip are not ported.
- **D-02: The per-entry detail card works as in the mockup**: it opens on hover, keyboard focus and
  tap; it can be hovered, it persists (no timeout) and Esc dismisses it. A first tap opens it, a
  second tap on the same entry follows the link, and a tap elsewhere closes it.
- **D-03: Compact entries stay 20px tall, recorded as a contrast-style exception against WCAG 2.5.8
  (24px target).** This is the density the operator asked for. Every entry stays reachable at a
  passing size through the day panel's 48px rows and through the detail card. It is recorded in the
  phase verification the way Phase 95 recorded the poster exception. The slot table is as in the
  mockup: 3 slots at 712 to 849px high (2 entries plus "+N more" at 1280x720), 4 at 850 to 979px,
  and 5 from 980px; "+1 more" never appears.
- **D-04: The site footer sits below the fold on the calendar page.** The month fits the viewport,
  and the footer is reached by scrolling. Only Calendar/Index is affected.
- **D-05: Below about 992px wide on a desktop User-Agent, the fit rule does not apply.** Cells grow
  and the page scrolls normally, with Q-116's wrapped navigation row. The fit target stays at 1280
  by 720 and up. Phones keep the agenda list.

### Linked-board entries
- **D-06: Entries from a linked board the viewer is NOT a member of stay inert.** The `--static`
  rule is kept exactly: no link, no tab stop, no data attributes, and the visually hidden status text
  stays. Mouse users get the hover detail card; keyboard and screen-reader users get the entry's
  hidden text, and the full entry in the day panel when the day overflows. `LinkedBoardStaticGuardTests`
  keeps its intent. This resolves the mockup's note 5, which asked for a focusable non-link treatment:
  that is not done.
- **D-07: Entries from a linked board the viewer IS a member of behave as today**: a real link to
  that board's page, with the dashed outline and the link glyph, ported to the compact row. Only the
  look changes.

### Phase 95 handovers
- **D-08: Fix the Quest Details calendar weekday header and retire its exception.** The five
  `div.calendar-day-header` entries (3.76:1) that Phase 95 Gate 2 accepted go: the weekday header in
  `calendar.css` reaches 4.5:1 on Quest Details as well as on Calendar/Index (5.63:1 in the
  calendar-fit mockup). Quest Details is re-verified on both layouts.
- **D-09: Direct `.ToString` date renderings in the area's views move to `Html.WallClock`, with
  byte-identical visible text.** This covers about 18 lines in `_Calendar`, `_Calendar.Mobile`,
  `Calendar/Index.Mobile`, `Events/Index` (both), `Agenda/Index` (both), `Events/Details` and
  `Series/Details`; all are wall-clock values, so nothing is converted. Where the helper has no style
  that produces the identical text, a matching style is **added** to its format table. This is an
  operator decision refining Definition of converted item 9: the helpers' conversion behaviour and
  every existing style stay untouched, and the existing helper tests stay green unchanged; only new
  format entries are added. Each moved line keeps its exact current text (the roadmap's "every date
  line keeps its format"), proven by a test.
- **D-10: The today highlight reads the board clock (Q-133), and the calendar partials go under the
  ambient-clock guard.** `_Calendar.cshtml` (and `_Calendar.Mobile.cshtml` if it gains a today marker
  through Q-46) compare against the board's today (for example `ViewBag.BoardToday` or an equivalent
  value from `IBoardClock`), never `DateTime.Today`. The change gets its own behaviour test, separate
  from the restyle. `AmbientClockSeamTests` scans the partials so an ambient read cannot return. Its
  closed list is not weakened.

### Relayout sign-off and gates
- **D-11: Hybrid sign-off with two operator gates, as in Phases 95 and 96.** Gate 1, early, drawn as
  scratch mockups and approved before any of it is built:
  - the Events/Details reorder (Q-115), desktop and phone, including the DM view;
  - the calendar port with real data: the calendar-fit mockup redrawn with the real entry types
    (quest voting and finalized, event, cancelled, linked inert and linked member, the
    no-building-key warning, the viewer's vote marker, today, the empty month), the Q-116 narrow
    navigation row, and the phone agenda list with the Q-40 and Q-44 to Q-46 additions;
  - the truncation trade-off (D-14), side by side at 1280 and 1440;
  - an Agenda/Index merge proposal if it qualifies (D-13).

  Gate 2, at the end: one review sheet with every before/after pair, desktop and phone, including
  Quest Details re-verified with `_Calendar` and the retired weekday-header exception.
- **D-12: A rejected AFTER is iterated until the operator approves it.** The phase waits on it; plan
  for a possible redraw loop at either gate.

### Twin merges
- **D-13: Re-check Agenda/Index for a merge once its markup is aligned.** Today it is layout-only
  but not CSS-only (collapse containers with an inline `stopPropagation`, a different card wrapper,
  a collapse filter against a dropdown). After conversion, research checks it against the Phase 94
  D-13 criteria and lists the tests a merge would change. If it qualifies, it goes to Gate 1 as a
  proposal and merges only on the operator's explicit yes. Calendar/Index and Events/Index stay
  twins (content differs).
  — **Reversibility:** costly — a merge touches the view, both layouts' CSS loading and the
  mobile-view tests that prove the `.Mobile` view is selected under a real phone User-Agent.

### Working baseline and delivery
- **D-15: Run on the Linux machine that ran Phases 94 and 95**, reusing `~/qb-ui-review/phase-94`:
  its Playwright install and tools, the Audit Board and its `qb-audit-*` accounts, the Direction A
  renders and the calendar-fit mockup. BEFORE is re-captured from the current branch into
  `~/qb-ui-review/phase-97/`. Front-end work (mockups, gate sheets, view and CSS conversion) invokes
  the `senior-frontend` skill.
- **D-16: The first plan opens with an operator checkpoint**, as in 94-01, 95 and 96. The operator
  confirms SQL Server at `localhost:1433` (never provisioned by the agent) and the reuse of the
  Audit Board accounts. Through the app's own forms (Phase 94 D-25), the executor creates the
  fixtures the captures need:
  - a crowded month that overflows at least one day at every slot count, a six-week month, and an
    empty month;
  - voting, finalized and cancelled quests, a no-building-key quest, and events with and without a
    start time;
  - a series with occurrences;
  - a linked board the viewer is a member of, and one they are not;
  - the Agenda's three empty states;
  - Events/Details as a player and as a DM.

  Credentials stay in the scratch folder, never in the repository.
- **D-17: Work lands in slices on `milestone/v9-rolling-improvements`, with one release at the end**
  (the next minor, about v6.2.0) once both gates have passed. Each slice leaves every page usable and
  the suite green (Phase 94 D-08).

### Claude's Discretion
- **D-14: Compact-entry title truncation.** The operator left the trade-off open. Options include the
  mockup's marker, time and title (about 13 characters at 1280 and 16 at 1440), or dropping the time
  from the compact row so the title gains about 5 characters. Research and the mockup step choose,
  and the result is shown at Gate 1 for approval. The full title is always in the detail card and
  the day panel.
- Slice order and boundaries, and the plan count. The operator left the order to the planner, by
  dependencies. One sensible order is the availability and linked-entry partials, then the Events
  pages and Series, then Agenda, with the gated calendar port last.
- How the slot count reaches the script (the mockup uses a `--cal-slots` custom property set by
  height media queries), and the module structure for the calendar and the `_SeriesFormScripts` move,
  within `.claude/ui-guidelines.md` § "No inline code".
- How the board's today value reaches the partials (view model, `ViewBag`, or an injected service),
  provided it comes from `IBoardClock`.
- The format-table entry names added for D-09.
- How Quest Details' vote calendar is affected by the port. It is not a compact month: the
  "+N more" and the slot rules belong to Calendar/Index only, and Quest Details keeps Phase 95's vote
  layout and is re-verified.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and inherited answers
- `.planning/ROADMAP.md` § "Phase 97: Events, Series, Calendar and Agenda UI conversion": scope,
  counts, stylesheet list, `@section Styles` moves, guard tests to rewrite, inherited decisions,
  approved relayouts with their BEFORE and AFTER paths, the Definition of converted and the phase
  extras (linked-entry dashed border, the inert `--static` rule and hidden status text stay; no date
  conversion; calendar-feed files untouched; narrow-width rules for twin-less views in the `pages`
  layer).
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT.md` § "Operator questions for
  sign-off" (Q-40 to Q-49 with their G1 answers and G1-R2 constraints, Q-115, Q-116, Q-133, Q-134),
  § "Sign-off record (G1, 2026-11-01)" (G1-O3 / G1-R2) and the relayout list.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-events.md`: all 51 findings
  (A-events-001 to 051), the twin comparison, merge proposals (none, with the Agenda/Index note),
  relayout proposals R1 and R2, empty-state copy (verbatim; no copy changes without the operator)
  and the area-phase scope notes.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-CONTEXT.md`: D-04 (relayout rules),
  D-07 (release per phase), D-08 (usable between merges), D-12 (inline code), D-13 to D-15 (twins,
  merges, mobile verification), D-16 and D-17 (accessibility bar, signature colours), D-25 (fixture
  writes).
- `.planning/phases/94-whole-app-ui-review-and-unification/94-UI-SPEC.md`: the design contract the
  foundation implements (tokens, components, contrast bar, Direction A values, rule A7).
- `~/qb-ui-review/phase-94/mockups/calendar-fit/NOTES.md` (outside the repository) with
  `calendar-month.html`, `calendar-fit.css`, `calendar-fit.js`, `fit-check.json`,
  `behaviour-check.json`, `contrast.json`: the month-fit sizes, slot table, entry, detail card and
  day panel behaviour, and the list of what could not meet a requirement (resolved by D-03, D-04,
  D-05 and D-06).
- `~/qb-ui-review/phase-94/mockups/direction-a/png/calendar-1440.png`, `calendar-390.png`,
  `calendar-820.png`, `calendar--empty-*.png`: the Direction A AFTER references.

### Phase 95 and 96 handovers
- `.planning/phases/95-quests-and-quest-log-ui-conversion/95-CONTEXT.md` D-18 to D-24: the process
  decisions this phase mirrors.
- `.planning/phases/95-quests-and-quest-log-ui-conversion/95-REVIEW.md` § WR-05: the `_Calendar`
  ambient today read and the direct wall-clock formatting (D-09, D-10).
- `.planning/phases/95-quests-and-quest-log-ui-conversion/95-18-SUMMARY.md` and `95-VERIFICATION.md`:
  the five weekday-header exception entries (D-08), and the 51 decorative warning icons without
  `aria-hidden` in the partial's day cells, left to this phase.
- `.planning/phases/95-quests-and-quest-log-ui-conversion/95-RESEARCH.md`: the vote-patch details in
  `_Calendar` and `_Calendar.Mobile`, and `.move-control-reason` (used by `Events/Details`), promoted
  to `components.css`.
- `.planning/phases/96-account-and-pickers-ui-conversion/96-CONTEXT.md` D-06: the `AccessDenied`
  overflow fix that this phase reuses on `Events/Details` (A-events-019).

### UI system and project rules
- `.claude/ui-guidelines.md`: tokens and layers, components, colour, accessibility checklist, mobile
  twins, no inline code (`@section Styles` / `@section Scripts`, ES modules), vendor files, dates.
- `.claude/architecture.md` § "Time and the board clock": `Html.LocalTime` and `Html.WallClock`,
  `IBoardClock`, `AmbientClockSeamTests`, and the calendar feed rules. `CalendarFeedWriter.cs`,
  `CalendarSubscriptionService.cs` and `FeedRevisionStamper.cs` are high-risk and are not touched.
- `CLAUDE.md`: branching, no planning IDs in source or CSS comments, local database contract.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Foundation sheets `wwwroot/css/tokens.css`, `base.css`, `components.css`, `shell.css`,
  `shell.mobile.css`: tokens, layers and components, including `--qb-fg-muted`, the opaque modal
  surface, `.modern-card--interactive` and the promoted `.move-control-reason`.
- The calendar-fit mockup's CSS and JS (`calendar-fit.css`, `calendar-fit.js`) already meet the
  foundation rules (no `!important`, no colour literal, no inline style, no `innerHTML`) and are the
  starting point for the port.
- `_LinkedDayEntries.cshtml` (around line 129) has the visually hidden status text pattern that the
  audit recommends for the icon-only markers in `_Calendar` (A-events-016).

### Established Patterns
- `Views/Shared/_Calendar.cshtml:33` compares `day.Date.Date == DateTime.Today` (D-10);
  `_Calendar.cshtml:76,84`, `_Calendar.Mobile.cshtml:26-27`, `Calendar/Index.Mobile.cshtml:98,121`,
  `Events/Index(.Mobile).cshtml`, `Agenda/Index(.Mobile).cshtml`, `Events/Details.cshtml:48` and
  `Series/Details.cshtml:40,64,116` format dates with `.ToString(...)` (D-09).
- `_Calendar` is rendered by `Calendar/Index.cshtml`, `Quest/Details.cshtml` and
  `Quest/Details.Mobile.cshtml`.
- `Calendar/Index.Mobile.cshtml` rows navigate through an inline `onclick` on a non-interactive
  `div` with 22px `a.row-nav-link` links (A-events-022); `Agenda/Index.Mobile.cshtml` uses
  `onclick="event.stopPropagation()"` inside collapse containers.
- `Events/Create.cshtml:51-52`: the recurrence checkbox label targets `IsRecurring` while the input
  is `enableRecurrence` (A-events-020).

### Integration Points
- `MobileViewLocationExpander` selects `.Mobile.cshtml` views by User-Agent; mobile checks run under
  a mobile User-Agent with touch (Phase 94 D-15).
- Guard tests to rewrite to the same intent: `EventsOverviewMobileStyleTests`,
  `LinkedBoardStaticGuardTests`, `LinkedBoardCalendarStyleTests`, the Events parts of
  `MobileViewsTests` and `BoardPageStaticGuardTests`, and `AmbientClockSeamTests` (gains the
  board-clock today read and the partials, D-10). `WallClockUnmovedTests` and the existing helper
  tests stay green unchanged (D-09).
- `calendar.css`, `events-overview.css` and `agenda.css` leave the global list of `_Layout.cshtml`;
  `Quest/Details` already links `calendar.css` itself (added by Phase 95).

</code_context>

<specifics>
## Specific Ideas

- The operator expects answers already given in earlier phases to carry over; the process
  decisions here are taken from Phases 95 and 96 for that reason.
- At G1 the operator asked for the desktop month to fit without scrolling, with dense one-line
  entries; D-03 and D-04 accept the costs of that density rather than give up rows.

</specifics>

<deferred>
## Deferred Ideas

None. The discussion stayed within the phase scope.

</deferred>

---

*Phase: 97-events-series-calendar-and-agenda-ui-conversion*
*Context gathered: 2026-11-12*
