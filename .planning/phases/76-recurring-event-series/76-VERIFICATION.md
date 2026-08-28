---
phase: 76-recurring-event-series
verified: 2026-08-28T20:55:24Z
status: gaps_found
score: 6/8 must-haves verified
behavior_unverified: 0
overrides_applied: 0
gaps:
  - truth: "A DM sees a banner on the calendar when any active series on the board is running low on upcoming sessions, which is the one place the silent-job failure becomes visible (D-26 / EVTRECUR-03)"
    status: failed
    reason: >
      The horizon banner exists and works correctly on the desktop calendar (Index.cshtml),
      confirmed by 76-12's human verification and by static code review of
      CalendarController.Index and Index.cshtml. It does not exist on the mobile calendar view.
      QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml has zero references to
      SeriesBelowRunway (grep confirmed). 76-10-SUMMARY.md's claim that "the calendar and mobile
      agenda surfaces are fully wired for the cancelled state and the horizon banner" is false for
      the banner half of that sentence -- the cancelled state IS wired on both surfaces (verified
      independently), the banner is not. A DM who works from a phone gets no signal at all when
      the rolling window stops advancing, reproducing exactly the silent failure D-26 exists to
      prevent.
    artifacts:
      - path: "QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml"
        issue: "No SeriesBelowRunway block; the DM-gated horizon banner from Index.cshtml has no mobile counterpart"
    missing:
      - "Port the SeriesBelowRunway banner block from Index.cshtml into Index.Mobile.cshtml, gated the same way (Model.CanManage, Model.SeriesBelowRunway.Any())"
  - truth: "An open-ended campaign never needs manual re-extension, and a DM can observe that fact where they actually look (EVTRECUR-03, D-26)"
    status: failed
    reason: >
      Confirmed in code: the Calendar nav entry is gated to BoardType.OneShot in both
      _Layout.cshtml (line 165ish) and _Layout.Mobile.cshtml (line 141ish) -- a Phase 37 decision
      (NAV-01, commit f7a31fa9) locked by LayoutNavigationTests.Nav_CampaignDm_CalendarLinkAbsent.
      Phase 76's two calendar-hosted read surfaces (the horizon banner and the cancelled-occurrence
      chip) are therefore unreachable through normal navigation on Campaign boards -- which is
      exactly the open-ended, indefinite-recurrence use case EVTRECUR-03's own wording targets
      ("an open-ended campaign never needs manual re-extension"). CalendarController itself carries
      no board-type gate (grep for BoardType/OneShot in CalendarController.cs returns zero
      matches), so /Calendar is already reachable by direct URL on a Campaign board and currently
      renders campaign quests alongside events -- an unrelated data-exposure wrinkle the developer
      also flagged. This is a real, code-confirmed gap, but the fix is cross-phase: it supersedes
      part of Phase 37's NAV-01 decision and requires replacing the test that locks it, plus adding
      board-type-aware filtering to CalendarController (events only on Campaign boards, both on
      OneShot boards). Recommendation: close it as part of this phase's gap-closure plan rather
      than opening a separate later phase, because it blocks EVTRECUR-03's user-visible completeness
      for its primary target audience -- but expect the fix to touch and formally amend NAV-01.
    artifacts:
      - path: "QuestBoard.Service/Views/Shared/_Layout.cshtml"
        issue: "Calendar nav link gated to BoardType.OneShot only (line ~165-169)"
      - path: "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml"
        issue: "Same OneShot-only gate (line ~141-145)"
      - path: "QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs"
        issue: "No board-type gating at all -- fetches and renders all quests and events unconditionally, so /Calendar already leaks campaign quests onto what should become an events-only Campaign calendar"
    missing:
      - "Board-type-aware CalendarController.Index: Campaign boards see events only (quests excluded); OneShot boards keep showing both"
      - "Un-gate (or re-gate to include Campaign) the Calendar nav link in both layouts"
      - "Replace LayoutNavigationTests.Nav_CampaignDm_CalendarLinkAbsent, which currently locks in the behaviour being changed"
human_verification: []
---

# Phase 76: Recurring Event Series Verification Report

**Phase Goal:** A DM can set up a repeating schedule — including "two sessions on, two off" — and get correct dates generated indefinitely, while still being able to cancel, move, or edit any single occurrence.
**Verified:** 2026-08-28T20:55:24Z
**Status:** gaps_found
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Cadence (interval + weekday) + anchor date + repeating on/off cycle mask generate dates that match the mask exactly (EVTRECUR-01) | ✓ VERIFIED | `EventSeriesDateGenerator.GenerateSlots`/`DateForSlot` implement `date(N)=Anchor+(N×IntervalWeeks)wks`, `fires(N)=mask[N mod len]`. `EventSeriesDateGeneratorTests` (21/21 pass, independently re-run with `--no-build`). |
| 2 | The setup screen previews the next ~10 generated dates live, before saving, and the previewed dates are exactly the dates created (EVTRECUR-02, D-05) | ✓ VERIFIED | `EventsController.PreviewSeries` calls `IEventSeriesService.PreviewAsync`, the same Domain generator the create path and top-up job use. `_SeriesFormScripts.cshtml` wires `fetch('/Events/PreviewSeries')` with debounce/re-render. 76-12 human verification: previewed dates (Sep 5, 12, Oct 3, 10, 31, Nov 7, 28, Dec 5, 26, Jan 2) exactly matched the dates the calendar showed after saving. `EventsControllerIntegrationTests.PreviewSeries_*` (3/3) pass. **Not yet marked complete in REQUIREMENTS.md** — implementation and behavioral evidence both support marking it satisfied. |
| 3 | Occurrences exist ahead of time on a rolling window and are topped up automatically — an open-ended campaign never needs manual re-extension (EVTRECUR-03) | ✗ FAILED | Materializer and job are correct (`EventSeriesService.TopUpAsync`, `RecurringOccurrenceTopUpJob` registered nightly at 03:00, per-board scoped via `HangfireJobHelper.RunInScopeAsync`). But the requirement's *observable, user-facing* half — a DM being able to tell the rolling window is/isn't working — fails twice: the horizon banner is absent from the mobile calendar (D1, confirmed in code), and the calendar (where both the banner and cancelled-chip live) is unreachable through nav on Campaign boards, the primary open-ended use case (O2, confirmed in code). See Gaps. |
| 4 | A single occurrence can be cancelled, moved, or edited without affecting the rest of the series (EVTRECUR-04, 05, 06) | ✓ VERIFIED | `EventsController.Cancel`/`Restore` set/clear `CancelledAt` as a tombstone; `Delete` refuses series occurrences server-side (`SeriesId.HasValue` check, re-resolved on POST, not just hidden in markup); `Edit` POST branches on `EventEditScope`, sweeps only `ThisAndFutureEvents` via `ApplyTemplateToFutureAsync`. Cancelled state renders (struck-through, muted) on desktop calendar chip, mobile agenda entry, and occurrence details page — confirmed both by grep (`cancelled` class present in `_Calendar.cshtml`, `Index.Mobile.cshtml`, `calendar.css`, `calendar.mobile.css`) and by 76-12 human UAT. Edit-scope sweep correctly skips a cancelled sibling (76-12 UAT: events 22/23/25/26 renamed, cancelled event 24 untouched). |
| 5 | Re-running the generator never duplicates, resurrects, or overwrites an occurrence (EVTRECUR-07) | ✓ VERIFIED | Filtered unique index `IX_Events_SeriesId_SeriesSlotIndex` (`filter: "[SeriesId] IS NOT NULL"`, confirmed in migration `20260828130415_AddSeriesRecurrence.cs`) is the DB backstop. `GetSlotIndexesForSeriesAsync` reads every slot ever produced with no date predicate, so a moved occurrence still reads as handled. `EventSeriesMaterializationTests` (14/14 pass, independently re-run). |
| 6 | Two boards with mirrored cycle masks on the same cadence and anchor produce interleaved, non-colliding dates (EVTRECUR-08) | ✓ VERIFIED | `GenerateSlots_MirroredMasksOnSameAnchorAndInterval_ShareNoFiringDate` exists and passes (part of the 21/21 generator test run). |
| 7 | A series and its occurrences on one board are invisible from another board, on every read/write surface (D-18, cross-cutting) | ✓ VERIFIED | `EventSeriesTenantIsolationTests` (12 facts) proves this end-to-end through the real request pipeline: desktop calendar, mobile agenda, occurrence Details, series Details, and every mutating action (Cancel, End, Delete, Detach, future-scope Edit), plus the server-side stamp on a spoofed board id at create. Spot-run independently (`GroupFilter_HidesSeriesFromOtherGroupOnDesktopCalendar` — pass). |
| 8 | The series detail page shows the rule and template read-only, with no way to edit the cadence, and offers End (date-based) and Remove (delete-vs-detach with split past/future/answer counts) (D-06, D-07, D-10, D-11, D-12, D-13) | ✓ VERIFIED | `SeriesController.Details/End/Delete/Detach` implement the read-only rule display and the two-outcome removal. `Edit.cshtml` for an occurrence carries no cadence/anchor/mask fields (grep: zero matches). 76-12 UAT: "Delete Series" dialog reported real counts (20 sessions, 0 past/20 upcoming, 1 answer). |

**Score:** 6/8 truths verified (2 present-and-partially-working but user-facing guarantee failed — counted as FAILED per the gaps above, not UNCERTAIN, because the code paths that would need to change are identified and the absence is directly observable, not ambiguous)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Domain/Services/EventSeriesDateGenerator.cs` | Pure cycle-mask generator + mask parser | ✓ VERIFIED | Present, substantive, no DI, static, no clock read. |
| `QuestBoard.Repository/Migrations/20260828130415_AddSeriesRecurrence.cs` | Template fields, EndDate, CancelledAt, filtered unique index | ✓ VERIFIED | All four present; index confirmed `unique: true, filter: "[SeriesId] IS NOT NULL"`. |
| `QuestBoard.Domain/Services/EventSeriesService.cs` / `IEventSeriesService.cs` | Materializer, top-up, preview, lifecycle, edit-scope | ✓ VERIFIED | `TopUpAsync`, `PreviewAsync`, `ApplyTemplateToFutureAsync`, `CountLiveSiblingsOnDateAsync`, `EndAsync`, `DeleteAsync` all present and called from controllers. |
| `QuestBoard.Service/Jobs/RecurringOccurrenceTopUpJob.cs` | Nightly per-group top-up | ✓ VERIFIED | Registered in `Program.cs` (`"0 3 * * *"`), iterates real groups, per-group scope, reports failure without stopping other boards. |
| `QuestBoard.Service/Controllers/Events/EventsController.cs` (Cancel/Restore/Delete/Edit) | Tombstone cancel, Delete refusal, scope-aware edit | ✓ VERIFIED | All server-side, re-resolved on POST. |
| `QuestBoard.Service/Controllers/Events/SeriesController.cs` | Series lifecycle page | ✓ VERIFIED | Details/End/Delete/Detach present. |
| `QuestBoard.Service/Views/Calendar/Index.cshtml` | Cancelled chip + horizon banner (desktop) | ✓ VERIFIED | Both present, `CanManage`-gated. |
| `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` | Cancelled chip + horizon banner (mobile) | ⚠️ PARTIAL | Cancelled chip present; horizon banner **absent** — see Gap 1. |
| `QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs` | Two-board isolation + refusal proof | ✓ VERIFIED | 12 facts, spot-run passing. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `_SeriesFormScripts.cshtml` | `POST /Events/PreviewSeries` | `fetch('/Events/PreviewSeries', ...)` | ✓ WIRED | Debounced, re-renders on response. |
| `EventsController.PreviewSeries` | `EventSeriesService.PreviewAsync` | Direct call | ✓ WIRED | Same generator path as create/top-up. |
| `RecurringOccurrenceTopUpJob` | `EventSeriesService.TopUpAsync` | Direct call, per-group scope | ✓ WIRED | Confirmed in job source. |
| `CalendarController.Index` | `Index.cshtml` `SeriesBelowRunway` banner | `Model.CanManage && Model.SeriesBelowRunway.Any()` | ✓ WIRED | Desktop only. |
| `CalendarController.Index` | `Index.Mobile.cshtml` `SeriesBelowRunway` banner | — | ✗ NOT_WIRED | No such binding exists in the mobile view at all. |
| `_Layout.cshtml` / `_Layout.Mobile.cshtml` nav | `CalendarController.Index` | `@if (activeBoardType == BoardType.OneShot)` | ⚠️ PARTIAL (by design, now a gap) | Nav link hidden on Campaign boards; controller itself has no matching gate, so the route is reachable directly but not discoverable, and currently renders quests it shouldn't on a Campaign board. |
| `EventsController.Edit` (future scope) | `EventSeriesService.ApplyTemplateToFutureAsync` | Direct call, `EventEditScope.ThisAndFutureEvents` branch | ✓ WIRED | Confirmed skip-cancelled-sibling behavior in 76-12 UAT. |
| `EventsController.CheckOccurrenceCollision` | Edit.cshtml collision notice | AJAX call feeding `#saveScopeCollisionNotice` | ✓ WIRED | Confirmed in view script. |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Cycle-mask arithmetic incl. mirrored-mask interleave | `dotnet test QuestBoard.UnitTests --no-build --filter FullyQualifiedName~EventSeriesDateGeneratorTests` | 21/21 passed | ✓ PASS |
| Idempotent materialization (no dup/resurrect/overwrite) | `dotnet test QuestBoard.UnitTests --no-build --filter FullyQualifiedName~EventSeriesMaterializationTests` | 14/14 passed | ✓ PASS |
| Cross-board isolation, desktop calendar | `dotnet test QuestBoard.IntegrationTests --no-build --filter FullyQualifiedName~EventSeriesTenantIsolationTests.GroupFilter_HidesSeriesFromOtherGroupOnDesktopCalendar` | 1/1 passed | ✓ PASS |
| Full unit suite (existence proof for the 385 claim) | `dotnet test QuestBoard.UnitTests --no-build` | 385/385 passed | ✓ PASS |
| Full integration suite (existence proof for the 513 claim) | `dotnet test QuestBoard.IntegrationTests --no-build` | 513/513 passed | ✓ PASS |
| Mobile calendar view renders `SeriesBelowRunway` banner | `grep -n "SeriesBelowRunway" QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` | 0 matches | ✗ FAIL — confirms Gap 1 |
| `CalendarController` gates by `BoardType` | `grep -n "BoardType\|OneShot" QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` | 0 matches | ✗ FAIL — confirms Gap 2 |

Note: `dotnet build`/`dotnet test` with a fresh build failed in this session because `QuestBoard.Service.exe` (pid 7608) is running under the debugger and holds a lock on `QuestBoard.Domain.dll`/`QuestBoard.Repository.dll` — a known environment constraint documented in CLAUDE.md. Worked around by running `dotnet test --no-build` against already-built test binaries, which independently reproduces the 385/513 counts reported in 76-11-SUMMARY.md rather than merely trusting the prose.

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|---|---|---|---|---|
| EVTRECUR-01 | 76-01, 76-06 | Base cadence + anchor + cycle mask | ✓ SATISFIED (marked complete) | Generator + Create form wiring, confirmed. |
| EVTRECUR-02 | 76-04, 76-06 | Live preview of next ~10 dates before saving | ✓ SATISFIED — **should be marked complete, currently unchecked in REQUIREMENTS.md** | Preview endpoint, wiring, tests, and 76-12 UAT all confirm exact date match. No code gap found; this is a tracking-doc gap, not a functionality gap. |
| EVTRECUR-03 | 76-04, 76-05, 76-09, 76-10 | Rolling window topped up automatically, no manual re-extension needed | ✗ BLOCKED (correctly left unmarked) | Mechanism is correct and tested; the user-visible "it's working / it's not working" signal fails on mobile (Gap 1) and is unreachable via nav on Campaign boards, the primary open-ended target (Gap 2). Confirms the developer's judgement in 76-12-SUMMARY.md — do not mark complete yet. |
| EVTRECUR-04 | 76-02, 76-03, 76-07, 76-10 | Cancel a single occurrence, rest unaffected | ✓ SATISFIED (marked complete) | Tombstone cancel, three-surface rendering, Delete refusal, all confirmed in code and UAT. |
| EVTRECUR-05 | 76-03, 76-08 | Move a single occurrence, rest unaffected | ✓ SATISFIED (marked complete) | Edit POST + collision notice confirmed. |
| EVTRECUR-06 | 76-03, 76-08, 76-09 | Edit a single occurrence's details, rest unaffected | ✓ SATISFIED (marked complete) | Scope-aware Edit POST + this-and-future sweep confirmed, including cancelled-sibling skip in UAT. |
| EVTRECUR-07 | 76-03, 76-04, 76-05 | Generator re-run never duplicates/resurrects/overwrites | ✓ SATISFIED (marked complete) | Filtered unique index + slot-existence query + idempotency tests confirmed. |
| EVTRECUR-08 | 76-01, 76-03 | Mirrored masks on two boards interleave without collision | ✓ SATISFIED (marked complete) | Dedicated unit test confirmed passing. |

No orphaned requirements found — all eight IDs declared in plan frontmatter map 1:1 to the eight IDs REQUIREMENTS.md assigns to Phase 76.

### Anti-Patterns Found

None found in the files this phase modified beyond the two gaps already tracked above (missing mobile banner block; unfiltered `CalendarController`). No `TBD`/`FIXME`/`XXX` markers, no stub returns, no hardcoded empty data flowing to rendering.

### Human Verification Required

None outstanding — 76-12 already completed the four manual-only checks this phase's validation strategy named (live preview re-render, three-surface cancelled state, mobile agenda on a genuine mobile UA, horizon banner). The one item 76-12 left unexercised (the amber collision-notice strip appearing) was confirmed present in code by this verification (`Edit.cshtml`'s `showCollisionNotice`/`CheckOccurrenceCollision` wiring) — low risk, does not warrant a second human pass.

### Gaps Summary

Two gaps block full completion of EVTRECUR-03, both already identified by the developer during 76-12 and independently confirmed in code by this verification:

1. **The horizon banner does not render on the mobile calendar.** `Index.Mobile.cshtml` has no `SeriesBelowRunway` reference at all, while `Index.cshtml` has full, correct, `CanManage`-gated banner logic. 76-10-SUMMARY.md's claim that both surfaces are "fully wired for the cancelled state and the horizon banner" is false for the banner. Fix is scoped and small: port the block into the mobile view.

2. **Campaign boards cannot reach the calendar through normal navigation**, so neither the horizon banner nor the cancelled-occurrence chip (both calendar-hosted) is discoverable on the board type EVTRECUR-03's own "open-ended campaign" language targets. `CalendarController` itself has zero board-type gating, so `/Calendar` is already reachable by direct URL on a Campaign board and currently renders campaign quests alongside events — an unrelated but real data-exposure issue. This gap's root cause is a deliberate Phase 37 decision (NAV-01, locked by a test), so its fix necessarily amends that decision rather than being pure Phase 76 code. Recommend closing it in this phase's gap-closure plan (it blocks EVTRECUR-03 for the primary use case) while explicitly noting in the closure plan that it supersedes and must replace `LayoutNavigationTests.Nav_CampaignDm_CalendarLinkAbsent`.

Both gaps are scoped, well-understood, and testable — no ambiguity requiring human judgment beyond confirming the fix direction (events-only Campaign calendar) the developer already stated as desired end state.

One non-blocking documentation item: EVTRECUR-02 is functionally complete and UAT-confirmed but is not yet checked off in REQUIREMENTS.md. Recommend marking it complete alongside EVTRECUR-03 once the gap-closure plan lands, per 76-12-SUMMARY.md's own stated intent.

---

_Verified: 2026-08-28T20:55:24Z_
_Verifier: Claude (gsd-verifier)_
