# Phase 91 — UI Review

**Audited:** 2026-10-17
**Baseline:** 91-UI-SPEC.md (approved 2026-10-15) plus `.claude/ui-guidelines.md`
**Screenshots:** captured in the in-app browser pane (not stored; no screenshot files were written). See Live Checks.

Method: a code audit by `gsd-ui-auditor` against the contract, then every auditor claim re-checked against the code and the running app (`http://localhost:8000`, desktop 1024px and a 375px Android-UA emulation). The app's local database held **no linked sets** during this review and no data was created, edited or deleted, so linked entries could not be seen with real rows. Where a linked surface is described as "probed", it was a client-side DOM replica built from the partial's exact markup and classes, inserted into the real page (real ancestors, real CSS) and discarded on reload. Probes show how the shipped CSS renders that markup; they are not real data.

Registry audit: skipped (no shadcn / `components.json`).

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Contract strings match almost verbatim; the count badge reads "1 entries from linked boards" and repeated "Remove Board" buttons have no per-row name |
| 2. Visuals | 3/4 | Structure, dashed edge, count badge and mobile rows are right; the desktop chip's text is washed out (counted under Color) |
| 3. Color | 2/4 | Desktop linked chip title and hover detail render parchment `#F4E4BC` on a 60-80% white chip, 1.0-2.3:1, against the contract's 6.1:1 / 5.3:1 |
| 4. Typography | 3/4 | Sizes all match; weights drift: chip title 500 (contract 600), mobile linked title 700 (contract 600), mobile Platform muted copy 600 (contract 400) |
| 5. Spacing | 4/4 | Every measured value matches the declared scale; title text lines up across own and linked chips (both at x=384) |
| 6. Experience Design | 3/4 | Empty, disabled, confirm(), member/non-member and unlinked states all covered; count badge and repeated destructive buttons lack accessible names |

**Overall: 18/24**

No finding breaks task completion, so nothing is classed BLOCKER under the rubric. Finding 1 below is high priority and should be fixed before ship: it defeats the contract's one explicit contrast guarantee on the phase's main new desktop surface.

---

## Top Priority Fixes

1. **Desktop linked chip text is overridden to parchment** (WARNING, high) — `modern-card.css:93-103` sets `.modern-card span { color: #F4E4BC !important; text-shadow: <dark 3-layer glow> !important; font-weight: 500 }`. The calendar sits inside `.card.modern-card` on both `/calendar` (`Calendar/Index.cshtml:10`) and Quest Details (`Quest/Details.cshtml:26`). `_LinkedDayEntries.cshtml:17-19,41-43` renders the chip's title, hover detail and hidden text as `<span>`, so `.linked-entry-title` and `.linked-entry-detail` lose to the `!important` rule (`calendar.css:548-566` sets `#1a1a1a`/`#262626`, no `!important`). Probed live on both pages: computed title and detail colour is `rgb(244, 228, 188)`, weight 500, with a black halo. Own chips escape this because they are `a > div`. User impact: the only thing a linked chip shows at rest (its title) and the hover line (board, DM, time) are pale cream on a pale chip. Measured contrast of `#F4E4BC` on the chip surface: 2.26:1 at best (black backdrop, 0.6 surface), 1.0-1.7:1 over the wood backdrop; the contract's `#1a1a1a` / `#262626` give 6.11:1 / 5.31:1 on the same worst case, so the contract values are sound, they just are not what renders. Fix: win the specificity fight for the three spans, for example `.linked-entry .linked-entry-title, .linked-entry .linked-entry-detail { color: #1a1a1a !important; text-shadow: none !important; }` with the detail at `#262626` and weights 600 / 400, or render title and detail as `<div>` like own chips. The same `.modern-card span` rule also repaints `.linked-day-count` (colour happens to match the contract, shadow and weight do not) and has no effect on the mobile surfaces. Add a computed-style check to whatever guards the chip so it cannot regress; the existing tests pin markup and class names only, which is why this passed 1088 integration tests and the operator check.
2. **Mobile linked agenda titles render at 700, not 600** (WARNING) — `Calendar/Index.Mobile.cshtml:135,160` put `fw-bold` on `.agenda-linked-title`; Bootstrap's `fw-bold` is `font-weight: 700 !important`, so `calendar.mobile.css:136-142` (`font-weight: 600`) is dead. Probed live at 375px: computed 700. Contract: two weights, 400/600. Note that the own rows beside them also use `fw-bold` (`Index.Mobile.cshtml:107,118`), so visually the linked rows match their neighbours; the choice is to drop `fw-bold` on linked rows (contract-true) or amend the contract. Remove the dead CSS weight either way.
3. **Accessible names for repeated and iconic controls** (WARNING) — (a) every `Remove Board` button in a set reads identically to a screen reader (`Index.cshtml:130-132`, `Index.Mobile.cshtml:111-113`); add `<span class="visually-hidden"> @board.Name</span>` after the label. (b) `.linked-day-count` (`_Calendar.cshtml:40`) is an icon plus a bare number; its only description is a `title`, which keyboard and touch users never get. Add `<span class="visually-hidden"> linked entries</span>`. (c) The `title` reads "1 entries from linked boards" when the count is 1; pluralise (the contract's literal string has the same flaw).

### Resolution (2026-10-17, commit 960a6c5a)

All three top fixes are done and verified live on local SQL Server.
1. **Chip text.** The chip title and detail in `_LinkedDayEntries.cshtml` are now `<div>`s, like the board's own chips, so `.modern-card span` no longer reaches them. The day-count badge in `_Calendar.cshtml` is a `<div>` for the same reason. Computed in the browser on both `/calendar` and Quest Details: the title is `rgb(26, 26, 26)` at 600 with no shadow, the hover detail is `rgb(38, 38, 38)` at 400, and the badge is parchment at 600 with its own single shadow. Two static guard facts in `LinkedBoardStaticGuardTests` fail if a visible span returns to the partial or to the badge; both fail on the previous markup.
2. **Mobile title weight.** The contract-true option was taken: `fw-bold` is dropped from `.agenda-linked-title`, so `calendar.mobile.css`'s 600 applies (probed 600; own rows stay 700).
3. **Accessible names and copy.**
   - Each Remove Board button now announces its board, for example "Remove Board D&D Nerdjes/Vriendjes". Measured at 390px, the buttons are still 44px with no horizontal scroll.
   - The day count reads "1 entry from linked boards" or "N entries from linked boards" to screen readers, and its tooltip pluralises the same way. The UI-SPEC literal still says "entries" and can be amended when the contract is next touched.

Follow-up (2026-10-17):
- **Pillar 4 note:** fixed with a scoped `.linked-boards-mobile p.text-muted { font-weight: 400 }`, probed at 400 live. It is scoped to paragraphs so the empty-state icon keeps its icon-font weight.
- **Minor Recommendation 3 (UI-SPEC reconciliation):** done. The accent list now names `.calendar-linked-note`, the state-table row matches the Copywriting Contract, and the count-badge literal is pluralised and specified as a div.
- **Still open:** Minor Recommendations 1 and 2 predate this phase. Recommendation 4 lists the held human backstops.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)

Verified as matching the contract (spot-checked in the views and, for the Platform page, in the running app):
- Platform intro, create labels, help text, `Create Set`, `Rename Set`, `Add Board`, `Delete Set`, `Remove Board`, badges, empty heading and body, `No unlinked {type} boards are available.`: `Index.cshtml:17-37,50-51,70-97,131-166`; mobile twin `Index.Mobile.cshtml:21-47,86-140`. The empty state was seen live on both twins.
- Toasts: `Areas/Platform/Controllers/LinkedBoardsController.cs` (TempData writes at lines 33, 87, 91 and the `ErrorMessage` helper). Confirm strings: `Index.cshtml:64,123`, `Index.Mobile.cshtml:58,105`.
- Calendar: legend block `Calendar/Index.cshtml:96-112`, mobile note `Calendar/Index.Mobile.cshtml:38`, Details note `Shared/_Calendar.cshtml:11-14`, `Also that day:` `_LinkedAlsoThatDay.cshtml:13,27`, tooltips and hidden text `_LinkedDayEntries.cshtml:15,19,39,43`. Only board names are rendered to players, never the set name.
- Group Index entry buttons: `Group/Index.cshtml` `btn btn-info`, `Group/Index.Mobile.cshtml` `btn btn-info btn-sm`, both with `fa-link` and the label `Linked Boards`, as specified.

Findings:
- WARNING: `title="1 entries from linked boards"` for a count of 1 (`_Calendar.cshtml:40`).
- WARNING: repeated `Remove Board` buttons carry no board name for assistive tech (see fix 3). `Rename Set` and `Delete Set` sit inside their set's card, so they are less ambiguous.
- INFO: the UI-SPEC state table (line 305) says a set with 0 or 1 boards shows "No boards in this set yet." / "Inactive: add at least one more board…". The code follows the Copywriting table instead, which assigns the Inactive note to a 1-board set and the empty note to a 0-board set (`Index.cshtml:102-111`, `Index.Mobile.cshtml:92-101`). This is consistent with the Copywriting Contract; the state-table row is just loosely worded. No code change.

### Pillar 2: Visuals (3/4)

- Desktop chip geometry matches the contract: probed at 1024px, linked chip `padding: 2px 4px 2px 3px`, `border-left: 4px dashed #ffc107`, 24px tall at rest, 48px on hover with the detail line revealed (`calendar.css:516-580`). Own chip title and linked chip title both start at x=384, so the 4px-dashed/3px-padding compensation works.
- Count badge sits top-right inside `.calendar-day` (position: relative) and outside the scrolling `.day-cell-items` (probed: badge top 216px, items top 245px). With one own chip plus two linked chips the scroll region overflowed (scrollHeight 100 vs clientHeight 78), which is the situation the badge exists for.
- Focal point preserved: linked chips come after own entries; mobile linked rows are lighter `#454c53` than own `#343a40`, with a dashed edge.
- INFO: own chip at rest measured 26px against the linked chip's 24px in the same cell (own chips grow with content; the contract's "identical height" is true to the `min-height` value, off by 2px in practice). Cosmetic.
- WARNING: the washed-out chip text (Priority Fix 1) is the one thing that undermines the visual hierarchy: the contract wants "quieter", the render is "nearly unreadable". Scored under Color; Visuals keeps 3 because structure, edges, badge and mobile rows are right.
- WARNING: `.calendar-linked-note` (Details desktop note) is parchment, which the UI-SPEC accent-reserved list (line 115) does not list, while section 2 (line 242) asks for it. The contract contradicts itself; amend the reserved list. Not a code defect.
- Platform pages: both twins follow `ui-guidelines.md` (`modern-card` / `modern-card-header`, `<hr>` before the footer row, primary action right; mobile `group-card-mobile`). Mobile page seen live at 375px: no horizontal scroll, `Create Set` and `Back to Groups` are 44px.

### Pillar 3: Color (2/4)

- WARNING (high): desktop linked chip text colour is overridden, see Priority Fix 1. Contrast table (computed, `#F4E4BC` vs the chip surface over three backdrops):

  | Backdrop behind chip | 0.6 surface | 0.8 surface (hover) |
  |---|---|---|
  | black (contract worst case) | 2.26:1 | 1.27:1 |
  | mid wood | 1.68:1 | 1.12:1 |
  | light wood | 1.34:1 | 1.02:1 |

  For comparison the contract's `#1a1a1a` gives 6.11:1 on the worst case and `#262626` gives 5.31:1, matching the UI-SPEC figures. The dark text-shadow adds an outline but does not make 10px/500 text AA-readable.
- WARNING: cancelled linked rows on mobile (`opacity: 0.55` over the already dim surface) estimate at roughly 3:1 for the 14px board line. The contract acknowledges this shared limitation for own cancelled rows and compensates with visually-hidden "Cancelled." text; noted, not scored down further.
- Status borders use the contract values on both surfaces (`#ffc107` / `#28a745` / `#6f42c1`, `calendar.css:530-546`, `calendar.mobile.css`); linked surfaces use `rgba(255,255,255,0.6/0.8)` and `#454c53`. Own entries untouched.
- Accent use is otherwise as contracted: parchment on the mobile "Also showing" note, count badge, Details note, mobile linked titles, "Also that day" lines. The desktop legend swatch is the neutral dashed grey `#6c757d` (probed: 12px swatch, `4px dashed rgb(108,117,125)`).
- INFO: legend sub-copy (`Calendar/Index.cshtml:111`, `small.text-muted`) renders as faded parchment `rgba(244,228,188,0.7)`, 12px/500, on the glass legend card (probed). That is the same treatment as the existing "Click quests or events for details" line, so it is consistent with its neighbours even though the contract's "never alpha text" wording would forbid it. Hard-coded hex values everywhere follow the project's convention (no token file).

### Pillar 4: Typography (3/4)

- Declared sizes are used everywhere: chip 10px, count badge 10px, Details note 12px, 14px for the mobile note / time / board line / "Also that day", 16px for the mobile linked title. Probed on mobile Details: `.also-that-day` is 14px/400 with the board in 600, exactly as specified.
- WARNING: chip title and detail render at weight 500 and the badge at 500, because `.modern-card span` forces `font-weight: 500` (Priority Fix 1). Contract: title 600, detail 400, badge 600.
- WARNING: mobile linked title is 700 (Priority Fix 2).
- WARNING: on the mobile Platform page, `p.text-muted` lines (empty-state body, set notes) render at weight 600 (probed) because `mobile.css:53-57` sets `.text-muted { font-weight: 600 }` and `platform-group.mobile.css:38-43` only overrides colour and shadow. Contract: 400. Add `font-weight: 400` to that scoped rule or use a plain `p`.
- INFO: `.linked-entry-detail` sets `font-style: normal` as required (no italic on linked secondary lines).

### Pillar 5: Spacing (4/4)

All measured values match the declared scale:
- Desktop chip padding `2px 4px 2px 3px`, `min-height` 24/48px, 1px gap and margin; title left edges align with own chips (x=384 both).
- Mobile linked row `8px 12px`, `margin-bottom: 8px`, 4px dashed edge; Details line `margin-top: 4px` (probed 4px) and the `.also-that-day + .calendar-vote-row` rule gives the vote row its 8px top margin (probed 8px).
- Mobile Platform: board row `padding: 8px 0; gap: 8px`, buttons 44px (live: `Create Set` 44px, `Back to Groups` 44px), no horizontal overflow at 375px (`scrollWidth` 375). The Remove Board wrap fixed in 2eeb578e is in place (`platform-linked-boards.mobile.css:25-27`).
- INFO: `.calendar-date-entry-mobile .also-that-day:last-of-type { margin-bottom: 8px }` (`quests.mobile.css:166-168`) does not match when a vote row div follows, but then `.also-that-day + .calendar-vote-row` supplies the gap; it matters only when no vote row follows. Harmless, not dead.

### Pillar 6: Experience Design (3/4)

Covered:
- Empty page with no sets (seen live, both twins); set with 0 boards, 1 board (Inactive note), add-board picker disabled with its explanatory line (`Index.cshtml:145-158`, `Index.Mobile.cshtml:122-133`).
- Destructive POSTs prompt with `confirm()` and the message is serialised through `Json.Serialize` (`Index.cshtml:64-65,123-124`); forms use tag helpers, so antiforgery tokens are emitted.
- Unlinked board: both desktop and mobile `/calendar` render zero linked markup. Probed live on a real board with no set: no `linked`, `also-that-day`, `agenda-linked` or `calendar-linked-note` text anywhere in the HTML, legend unchanged (contract D-15 holds).
- Non-member chips and rows: no `href`, `onclick`, `tabindex` or `data-*` (`_LinkedDayEntries.cshtml:30-31,54-55`; mobile non-member branches). Member chips are plain anchors; mobile member rows use `onclick` plus an inner `row-nav-link` anchor. The linked partials contain no inputs, forms, vote markup or ids.
- Mobile Details: the `Also that day:` lines were probed inside the real vote form at 375px: no horizontal overflow, the cancelled variant struck through, the title underlined link for members.

Findings:
- WARNING: count badge and repeated `Remove Board` buttons lack accessible names (fix 3).
- INFO: the auditor flagged inline `onsubmit`/`onclick` handlers as a possible CSP break. The application sets no Content-Security-Policy anywhere (searched `.cs`, `.json`, `.cshtml`, `.csproj`), and 125 existing view lines already use these handlers, so there is nothing to break.
- INFO: touch-only tablets get no hover reveal; the contract accepts this, and the visually-hidden text carries the information. Non-member chips are not focusable by design.
- Not verified (no linked data available): linked-entry ordering and the DM-only visibility filter, which live in the domain/view-model layer and are covered by tests rather than by this audit.

---

## Auditor claims not upheld

The code auditor ran on the cheapest model profile and scored every pillar 3/4. Re-checking its claims:

- "Global `.text-muted` forces brown 600-weight text onto phase 91 copy" (its priority fix 1): **not upheld as stated.** `site.css:639-643` and `mobile.css:53-57` do set brown `#2c1810 !important`, but the scoped rules win in every phase 91 context: `.modern-card .text-muted` (desktop Platform, legend) and `.platform-group-card-mobile .text-muted` (mobile Platform) resolve to faded parchment, as probed. Only the 600 weight on the mobile Platform copy survives (kept above as a Typography warning).
- "Board names in parchment on Platform mobile are a contract violation" (its priority fix 2): **downgraded to nothing.** All text on that mobile surface is parchment (labels, paragraphs, the set name); the contract's "never for board names" governs the linked calendar surfaces, and the contract itself shows board names in parchment on the mobile "Also showing" line.
- "0-board set lacks the Inactive note": **not upheld**; the Copywriting table assigns that note to 1-board sets (see Copywriting INFO).
- "`:last-of-type` selector is dead": **not upheld**, see Spacing INFO.
- "Inline handlers may violate CSP": **moot**, no CSP exists.
- It also missed Priority Fix 1 entirely, because that defect only shows when the CSS cascade is evaluated in a browser.

---

## Minor Recommendations

1. Pre-existing, not phase 91: `Shared/_Calendar.Mobile.cshtml:26-27` formats the proposed date with `ToString("dddd, MMMM d")` / `ToString("HH:mm")` in the view, against `ui-guidelines.md`. Not introduced here, not scored.
2. Pre-existing, not phase 91: on the mobile calendar the `Board Availability` / `My Agenda` buttons are 44px tall but their label sits at the top (block display, 4px padding); only visible in the unlinked state, untouched by this phase.
3. Reconcile UI-SPEC: parchment on `.calendar-linked-note` is used but missing from the accent-reserved list (line 115 vs line 242); the state-table "0 or 1 boards" row (line 305) vs the Copywriting table.
4. Held-out checks the contract lists as backstops remain open: the legend and "Also showing" lines with 1 and 4+ linked boards, long set names at 390px with real rows, and event chips (see Live Checks).

---

## Live Checks

Environment: in-app browser, signed in as the local super admin; app at `http://localhost:8000` against `mssql-dev`; zero linked sets in the database; Euphoria Inn selected as the active board (session-only, nothing persisted). 375px checks used an Android 14 user agent so the `.Mobile.cshtml` twins were served.

| Surface | Result |
|---|---|
| Platform Linked Boards, desktop, empty state | Matches contract: heading, intro, create form, empty message; renders at 1024px with no horizontal scroll |
| Platform Linked Boards, mobile 375px, empty state | Matches contract; `Create Set` and `Back to Groups` 44px, inputs 44px, `scrollWidth` 375; `p.text-muted` weight 600 (contract 400) |
| Desktop `/calendar`, unlinked board | Zero linked markup; legend unchanged |
| Mobile `/calendar`, unlinked board | Zero linked markup; no horizontal scroll; own rows unchanged |
| Desktop linked chip (probe replica, `/calendar` and `/Quest/Details/12040`) | Geometry matches; **text colour overridden to parchment, weight 500** (Priority Fix 1); hover grows 24px to 48px and reveals the detail line; count badge top-right outside the scroll region |
| Mobile linked agenda rows (probe replica) | Surface `#454c53`, dashed status edge, note 14px parchment, cancelled row struck through at 0.55; title computes to 700 (Priority Fix 2); no horizontal scroll |
| Mobile Quest Details `Also that day` lines (probe replica) | 14px/400 parchment, board 600, member title underlined, cancelled variant struck through, 4px/8px spacing as specified |
| Desktop legend rows (probe replica) | Dashed swatch 12px grey; long board name wraps in the 136px legend column; sub-copy faded parchment 12px/500 |

Not seen live, even as a probe or with real data: event chips on real events (the database has none), a real linked-only day, the 4+ board legend and "Also showing" lines, non-member chips with real rows, a physical phone.

---

## Files Audited

- `QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml`, `Index.Mobile.cshtml`
- `QuestBoard.Service/Areas/Platform/Controllers/LinkedBoardsController.cs` (toast strings only)
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml`, `Index.Mobile.cshtml` (entry buttons, via diff)
- `QuestBoard.Service/Views/Calendar/Index.cshtml`, `Index.Mobile.cshtml`
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml`, `_Calendar.Mobile.cshtml`, `_LinkedDayEntries.cshtml`, `_LinkedAlsoThatDay.cshtml`
- `QuestBoard.Service/wwwroot/css/calendar.css`, `calendar.mobile.css`, `platform-linked-boards.mobile.css`, `quests.mobile.css` (phase 91 additions)
- `QuestBoard.Service/wwwroot/css/modern-card.css`, `site.css`, `mobile.css`, `platform-group.mobile.css` (cascade checks)
- `.planning/phases/91-linked-boards-with-a-shared-calendar/91-UI-SPEC.md`, `91-10-SUMMARY.md`, `.claude/ui-guidelines.md`
