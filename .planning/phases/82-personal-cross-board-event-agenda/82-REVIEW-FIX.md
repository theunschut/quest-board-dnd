---
phase: 82-personal-cross-board-event-agenda
fixed_at: 2026-08-29T00:00:00Z
review_path: .planning/phases/82-personal-cross-board-event-agenda/82-REVIEW.md
iteration: 1
findings_in_scope: 9
fixed: 7
skipped: 2
status: partial
---

# Phase 82: Code Review Fix Report

**Fixed at:** 2026-08-29
**Source review:** `.planning/phases/82-personal-cross-board-event-agenda/82-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 9 (1 Critical, 8 Warning)
- Fixed: 7
- Skipped: 2 (WR-01, WR-05 — both recorded as follow-ups below)

**Build and test result after all fixes:**

| Suite | Before | After | Delta |
|---|---|---|---|
| `QuestBoard.UnitTests` | 420 passed, 0 failed | **422 passed, 0 failed** | +2 new facts |
| `QuestBoard.IntegrationTests` | 603 passed, 0 failed | **605 passed, 0 failed** | +2 new facts |
| **Total** | 1023 passed | **1027 passed, 0 failed, 0 skipped** | +4 |

`dotnet build` — 6 projects, 0 errors. The 20 warnings are the pre-existing
`HtmlSanitizer`/`AngleSharp.Css` package-constraint warnings, unchanged by this pass.
No regression against the baseline.

## Fixed Issues

### CR-01: "Show More" silently converted an implicit "all boards" view into a permanent session filter

**Files modified:** `QuestBoard.Service/Controllers/AgendaController.cs`,
`QuestBoard.Service/ViewModels/AgendaViewModels/AgendaViewModel.cs`,
`QuestBoard.IntegrationTests/Tests/AgendaControllerIntegrationTests.cs`
**Commit:** `15ff699b`

**Applied fix:** The controller now tracks whether a selection the *viewer* made is actually in
force, separately from the effective board set. `isReset` is hoisted so both branches share one
definition, `stored` is hoisted out of the no-parameter branch, and
`hasExplicitSelection = (boardsProvided && !isReset) || stored != null` distinguishes "the viewer
chose these boards" from "these happen to be all the viewer's boards". `SelectedBoardIds` — the
value both paging links embed — now carries the effective set only when a real selection exists,
and the reset sentinel otherwise. The sentinel round-trips through the existing reset branch,
which is a no-op when nothing is stored, so paging can no longer write a filter into session.
The `SelectedBoardIds` doc comment on the view model was rewritten: it previously claimed the
value exists so paging "never silently resets the filter", which was true but silent about the
opposite failure this bug actually was.

**Regression test added:** `Agenda_PagingWithNoSelectionOfTheViewersOwn_DoesNotTurnPagingIntoAStoredFilter`.
It requests `/Agenda` with no query and no session, asserts the rendered Show More link carries
the reset sentinel rather than a board list, follows that link, then joins a *third* board and
asserts that board's event appears on the very next plain request. **This test was verified to
fail against the pre-fix controller** (`Expected ... to contain "boards=all"`) and to pass after
— it genuinely bites rather than merely passing alongside the fix. It closes the gap the review
identified: all three pre-existing filter tests start from an explicit `?boards=`, so the
implicit-all to paging path had no coverage at all.

### WR-02: The membership re-check dropped foreign rows with no signal to the operator

**Files modified:** `QuestBoard.Domain/Services/EventService.cs`,
`QuestBoard.Domain/Interfaces/IEventService.cs`,
`QuestBoard.UnitTests/Services/CrossBoardAgendaTests.cs`,
`QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs`
**Commit:** `1cd7e564`

**Applied fix:** Took the reviewer's second option (log at Error) rather than the throw. The drop
still happens — the reader stays protected and a read-only page does not start 500ing on an
invariant the viewer cannot influence — but `EventService` now takes `ILogger<EventService>` and
emits an Error entry naming the dropped and fetched counts whenever the re-check removes
anything. The interface XML doc states the new behaviour.

Two facts added: `CrossBoardAgenda_DroppedRow_IsLoggedAsAnError_NotSilentlySwallowed` and
`CrossBoardAgenda_NoDroppedRows_LogsNothing` (the quiet path matters — a logger that fires on
every request is not a signal).

Note on the test harness: `Substitute.For<ILogger<EventService>>()` cannot be constructed because
`EventService` is `internal` and Castle cannot proxy a closed generic over it. Both test classes
therefore use small hand-rolled `ILogger<EventService>` implementations (a recording one where the
assertions need entries, a silent one where they do not) rather than adding
`InternalsVisibleTo("DynamicProxyGenAssembly2")` to the production assembly for a test's benefit.

### WR-03: The isolation suite's doc comment overstated what it proves

**Files modified:** `QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs`
**Commit:** `1189d257`

**Applied fix:** Documentation only, deliberately — see the note under *Follow-ups* below. The
class doc now carries an explicit "what these facts do NOT establish" section naming the InMemory
provider and the three specific properties that never reach a relational query compiler: the
empty-collection containment test, the row limit composed before the includes, and the filter
bypass interacting with the signup entity's own filter. The suite previously read as though it
proved end-to-end isolation.

### WR-04: Outline buttons violated the project's UI convention

**Files modified:** `QuestBoard.Service/Views/Agenda/Index.cshtml`,
`QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml`
**Commit:** `b148e7d4`

**Applied fix:** All five `btn-outline-secondary` occurrences replaced with `btn-secondary` —
2 in the desktop view (filter dropdown toggle, "Show All Boards" reset), 3 in the mobile view
(filter collapse toggle, reset, roster toggle). Verified exhaustive: a repository-wide search for
`btn-outline-secondary` under `Views/Agenda/` returns nothing, and no CSS rule or test assertion
anywhere referenced the outline class.

### WR-06: Applying the board filter discarded the current window size

**Files modified:** `QuestBoard.Service/Views/Agenda/Index.cshtml`,
`QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml`,
`QuestBoard.IntegrationTests/Tests/AgendaControllerIntegrationTests.cs`
**Commit:** `049bb11c`

**Applied fix:** Both filter forms now carry a hidden `take` field bound to `Model.Take`.
`Model.Take` is the server-clamped value, so the round-trip cannot be used to widen the window.
Fact added: `Agenda_FilterForm_CarriesTheCurrentWindowSize`.

### WR-07: The middleware exemption is controller-wide and the docs did not say so

**Files modified:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`
**Commit:** `4c25d3e0`

**Applied fix:** Took the documentation option rather than narrowing the entry to `/Agenda/Index`,
because narrowing requires listing the bare `/Agenda` default-action path as a second entry and
would silently break the navigation links if that were ever missed — a worse failure mode than
the one being guarded. Two changes: the class doc's exemption bullet now states that every entry
is a `StartsWithSegments` path *prefix*, so a controller-name entry covers every action on that
controller including future and non-idempotent ones; and the inline comment on the agenda entry
states the scope explicitly and sets the rule for anyone adding an action there (derive scope
from a fresh membership read, do not assume a non-null active board, otherwise replace the entry
with explicit action paths *plus* the bare controller path).

### WR-08: `"all"` and `"none"` were undeclared magic strings across four files

**Files modified:** `QuestBoard.Service/Constants/SessionKeys.cs`,
`QuestBoard.Service/Controllers/AgendaController.cs`,
`QuestBoard.Service/Views/Agenda/Index.cshtml`,
`QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml`
**Commit:** `81369827`

**Applied fix:** `AgendaBoardFilterResetSentinel` and `AgendaBoardFilterNoneSentinel` declared in
`SessionKeys` next to the key they belong to, each with a doc comment explaining why it is a
constant (a typo fails silently, not loudly). Both views reference the reset sentinel through the
constant; the controller references both.

The review also flagged the unexplained comparison asymmetry — the reset sentinel is matched
`OrdinalIgnoreCase`, the stored marker `Ordinal`. Rather than making them uniform, the asymmetry
is now deliberate and explained in a comment: the reset sentinel arrives from a URL a reader can
type or edit, while the stored marker is only ever written by one line in the same method, so the
stricter comparison is both correct and cheaper. The stored-marker comparison was also changed
from `==` to an explicit `string.Equals(..., StringComparison.Ordinal)` so the choice is visible
rather than incidental.

## Skipped Issues

### WR-01: The cross-board API takes the membership set as a parameter

**File:** `QuestBoard.Domain/Interfaces/IEventService.cs:61`, `QuestBoard.Domain/Services/EventService.cs:77-94`
**Reason:** Skipped deliberately — blast radius exceeds a fix pass, and the suggested fix carries
a real runtime cost that deserves its own decision.

The suggested change injects `IGroupService` into `EventService` and re-derives the membership set
inside the domain method. That would mean a **second, identical membership query on every agenda
page load**: the controller already reads memberships and cannot stop, because it needs the board
*names* and *types* to render row badges, the filter checklist and the active-board marker. So
the fix is not a relocation of a query, it is an addition of one, on the hot path of the page. It
also introduces a domain-service-to-domain-service dependency that does not currently exist in
this layer.

Against that: the reviewer confirms there is no live leak. There is exactly one caller, it is
correct, and the intersect that makes "the filter cannot widen" true happens before the call.

I also declined the reviewer's fallback ("rename the parameter to `viewerMemberGroupIds`"),
because the finding's own complaint is that the boundary is protected by documentation rather
than by code — adding better documentation is not progress against that complaint, and it risks
reading as though the finding had been addressed.

**Recommended follow-up:** a small phase that decides between (a) accepting the duplicate
membership read in exchange for type-level enforcement, or (b) restructuring so the controller
hands the domain a membership value it cannot forge, which enforces the boundary without a second
query. Both are design decisions, not defect fixes.

### WR-05: ~110 lines duplicated between the desktop and mobile agenda views

**File:** `QuestBoard.Service/Views/Agenda/Index.cshtml:43-55,172-236`, `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml:43-55,182-246`
**Reason:** Skipped — this is a design change, not a defect fix, and it is not risk-free.

The desktop/mobile split in this codebase is a deliberate, user-agent-selected pattern in which
some duplication is inherent; a sibling phase duplicated CSS for exactly this reason and
documented it. Extracting shared partials and a shared `agenda.js` changes rendered markup and
moves an inline script into a static asset, both of which the mobile render tests assert against
by string matching — a non-trivial chance of churn in tests that exist to pin mobile rendering,
in exchange for no behavioural improvement.

The reviewer's underlying concern is legitimate: the duplicated block includes the board-switch
modal, which is the security-relevant part of the page, so a fix applied to one copy and not the
other is a genuine drift risk. That argues for doing the extraction properly with its own
verification, not for doing it inside a fix pass.

**Recommended follow-up:** extract `_BoardFilterForm.cshtml` and `_SwitchBoardModal.cshtml` plus
`wwwroot/js/agenda.js` as a small dedicated phase. That work would also resolve IN-05 (the two
inline `onclick` handlers, which block a future `script-src 'self'` CSP) and enable IN-08's
suggested assertion.

## Follow-ups Recorded

1. **Relational coverage for the cross-board query (from WR-03).** The isolation suite runs on the
   EF Core InMemory provider. Only the misleading doc comment was corrected here — the provider
   gap itself is untouched, because switching the suite's provider is a test-infrastructure change
   that could destabilise 600+ integration tests. A single SQLite in-memory smoke test proving the
   query translates and that the empty-collection containment case returns zero rows would close
   the real gap without touching the shared harness.
2. **Enforce the cross-board tenant boundary in code rather than in a comment (WR-01).**
3. **Extract the shared agenda view partials and script (WR-05, IN-05, IN-08).**
4. **Info-tier findings IN-01 through IN-08 were out of scope** for this pass and remain open.

## Verification Notes

- Every edited file was checked for CRLF line endings after editing; all are 100% CRLF (verified
  by comparing carriage-return line counts against total line counts, not by assumption).
- The changed source was scanned for GSD tracking references (finding IDs, phase numbers, plan
  numbers, requirement IDs) before each commit. None present — comments explain the *why* in
  plain language. Commit messages reference finding IDs, which is the intended exception.
- `dotnet build` was run and confirmed clean after every individual fix, not only at the end.
- The CR-01 regression test was explicitly verified to fail against the pre-fix code before being
  accepted.

---

_Fixed: 2026-08-29_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
