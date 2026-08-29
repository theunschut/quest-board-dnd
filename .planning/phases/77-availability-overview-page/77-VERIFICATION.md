---
phase: 77-availability-overview-page
verified: 2026-08-29T13:30:00Z
status: human_needed
score: 31/31 must-haves verified
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 30/31
  gaps_closed:
    - "A Show More Events control appears only when more events exist beyond the current window, and grows the set through a take query-string value rather than session state — now present on mobile as well as desktop."
  gaps_remaining: []
  regressions: []
---

# Phase 77: Availability Overview Page Verification Report

**Phase Goal:** A DM can see, in one place, who is available for which upcoming events — and tell a real answer apart from an untouched default.
**Verified:** 2026-08-29T13:30:00Z
**Status:** human_needed
**Re-verification:** Yes — after gap closure (plans 77-05 through 77-10)

## Goal Achievement

### Observable Truths (Roadmap Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A page shows upcoming events for the current board as a grid of events against players, with each player's availability | ✓ VERIFIED | Desktop (`Index.cshtml`) and mobile (`Index.Mobile.cshtml`) both independently read and confirmed. Mobile now contains `@if (Model.CanShowMore) { ... Model.NextTake ... }` at lines 96-103, mirroring the desktop block. `EventOverviewViewModel.CanShowMore => HasMore && NextTake > Take` (read directly). A new integration test, `Index_MobileUserAgent_MoreEventsThanTake_ShowsShowMoreControl`, seeds 2 events, requests `/Events?take=1` with a mobile User-Agent, and asserts the response contains `"Show More Events"` and `"take=11"` — this is a genuine behavioral proof, not a presence check, and it fails if the control or the `NextTake` arithmetic regresses. |
| 2 | An untouched default is visually distinct from an answer the player actually gave | ✓ VERIFIED (code) / see Human Verification | `_AvailabilityCell.cshtml` still renders the muted default with a distinct `fa-clock` icon, italic `<em>Yes</em>`, and a `1px dashed` border — three independent non-colour signals. The fill weight was changed by orchestrator commit `db469594` (muted-Yes now uses solid `var(--bs-success)` instead of a hollow `-bg-subtle` tint) because the previous hollow fill was unreadable. This removes fill-weight as a fourth distinguishing signal but does not remove any of the three signals D-02 actually requires (icon, italic label, dashed border), none of which ever depended on fill weight. Judgement: D-02's mandate ("a second signal that survives greyscale... something non-colour is mandatory") still holds on the code evidence. Perceived at-a-glance distinctness remains a human call (see Human Verification #2, escalated in priority by this change). |
| 3 | Each event shows an availability count, so a poorly-attended date is obvious at a glance | ✓ VERIFIED | Unchanged from the prior pass; `_AvailabilityCounts.cshtml` renders all three figures unconditionally. `Index_RendersAllThreeCounts` was strengthened in 77-09 (WR-08) to assert the actual rendered numbers (`<strong>3</strong> Yes`, `(2 confirmed)`, `2 Maybe`) rather than substrings the legend also renders unconditionally — read directly, confirmed present. |
| 4 | No event or member from another board ever appears, proven by a two-group integration test | ✓ VERIFIED | `EventsOverviewTenantIsolationTests` unchanged and still passing (5 facts, confirmed in the full suite run below). `grep -c 'IgnoreQueryFilters'` re-run against `EventRepository.cs`, `EventService.cs`, `EventsController.cs` after all six gap-closure plans: 0 for all three. The named risk was not reintroduced during gap closure. |

**Score:** 4/4 roadmap truths verified. All 31 plan-level must-haves (8+7+10+6+8 across 77-01..77-04, plus the truths added by 77-05..77-10) verified — see Plan-Level Truths below.

### Plan-Level Truths

**Plans 77-01–77-04 (original phase, unaffected by gap closure) — re-confirmed by regression, not re-derived from scratch:**
- 77-01 (aggregate read + domain aggregation): unchanged except `EventService` now takes an injected `TimeProvider` (77-07). Confirmed by direct read: `internal class EventService(IEventRepository repository, IMapper mapper, TimeProvider timeProvider)`; `DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)` replaces the prior `DateTime.Today` read.
- 77-02 (CSS + navigation): `.avail-cell-yes-muted` dashed border still present in both stylesheets (now with an updated fill, see Truth #2). `LayoutNavigationTests` still 32/32 green (part of the 560 integration total below), with the class doc comment now planning-ID-free.
- 77-03 (view models, views, controller): the one previously-failing truth (mobile Show More control) now passes — see Truth #1.
- 77-04 (tenant isolation): unaffected, re-confirmed clean.

**Plan 77-05 (mobile paging control + own-column CSS spec) — verified.** `EventOverviewViewModel.CanShowMore` computed property present and used by both views (read directly). `Index.Mobile.cshtml` carries the Show More block and an inert roster container (`onclick="event.stopPropagation();"` on the `.collapse` div at line 81, in addition to the toggle button's own guard at line 78 — two guards total, confirmed by the `guardCount.Should().Be(2)` assertion in `Index_MobileUserAgent_RendersRosterToggle`). No `<form>`/`method=post` introduced (grep clean). No `IgnoreQueryFilters` added.

**Plan 77-06 (own-column highlight + frozen-column fix) — verified.** `events-overview.css` now declares `.modern-card .table th.avail-col-self` / `td.avail-col-self` at the same specificity+`!important` shape as the sticky-column rules, so the highlight is no longer dead CSS (WR-02 closed). `.avail-col-event` now uses a shared `--avail-event-col-width` custom property for both its own fixed width (`width`/`min-width`/`max-width: 200px`, no longer a bare `min-width` floor) and `.avail-col-attendance`'s `left` offset, so the two frozen columns cannot drift apart on a long title (WR-03 closed). No rule broadened to also repaint the sticky columns or row-hover tint (confirmed by direct read — the two new rules apply only to `.avail-col-self`).

**Plan 77-07 (UTC clock injection + options validation) — verified.** `TimeProvider` registered via `services.TryAddSingleton(TimeProvider.System)` and injected into `EventService`; the date boundary now reads `timeProvider.GetUtcNow().UtcDateTime`, matching the UTC timestamps used elsewhere in the feature (IN-08 closed). `EventsOverviewOptions.IsValid()` added and wired through `.Validate(o => o.IsValid(), ...).ValidateOnStart()` in `ServiceExtensions.cs` (WR-04 closed) — confirmed genuinely wired, not just present, by `EventsOverviewOptionsValidationTests.AddDomainServices_InvalidCeiling_ResolvingOptionsThrowsOptionsValidationException`, which builds a real `ServiceCollection`, calls `AddDomainServices`, and asserts `OptionsValidationException` is thrown on resolution with `MaxTake=0` — a test that fails if the validation is ever removed. The controller's own clamp is additionally defended with `Math.Max(1, options.MaxTake)` so it cannot throw even if a host bypasses the startup check. No EF package or reference introduced outside `QuestBoard.Repository` (confirmed — `EventsOverviewOptionsValidationTests.cs` lives in `QuestBoard.UnitTests` and uses only `Microsoft.Extensions.*` and `QuestBoard.Domain` references).

**Plan 77-08 (navigation test independence + comment cleanup) — verified.** `LayoutNavigationTests` implements `IAsyncLifetime`; `DisposeAsync` resets `_factory.TestGroupContext.BoardType = BoardType.OneShot` after every test (confirmed by direct read at line 35-38). Every test body now sets its own board type explicitly, including the two previously-silent cases (`Nav_DungeonMaster_CreateEventEntryPresent` at line 270, `Nav_Player_CreateEventEntryAbsent` at line 283) — IN-07 closed. `grep -c 'NAV-0'` and `grep -n 'D-0'` against the file both return zero matches — IN-02 closed, including the auto-fixed deviation that caught four additional `NAV-0` separator comments the plan's own acceptance criteria required removed. All 32 pre-existing/expanded navigation assertions still pass (confirmed in the full suite run below), none deleted, renamed, or weakened.

**Plan 77-09 (mobile rendering test coverage + requirements traceability) — verified.** `EventsOverviewControllerIntegrationTests.cs` now contains `Index_MobileUserAgent_RendersCardList`, `Index_MobileUserAgent_RendersRosterToggle`, `Index_MobileUserAgent_NoEvents_RendersEmptyState`, and `Index_MobileUserAgent_MoreEventsThanTake_ShowsShowMoreControl` — all four confirmed present and passing, closing WR-07 (the structural reason CR-01 shipped unnoticed). The clamp tests (WR-05) were strengthened to seed 105 events and assert an exact `Should().Be(100)` boundary rather than `<= 100`, and the zero/negative case asserts exactly one row — both would now fail if the clamp were deleted (confirmed by reading the assertions directly, not merely their names). `.planning/REQUIREMENTS.md` shows all four `EVTVIEW-0{1..4}` checkboxes as `[x]` and all four traceability rows as `Complete` — the prior documentation gap (EVTVIEW-04 left unchecked) is closed.

**Plan 77-10 (keyboard-reachable navigation, IN-06) — verified.** `.row-nav-link` class added to `modern-card.css`, loaded by both `_Layout.cshtml` and `_Layout.Mobile.cshtml`. Every one of the 13 mouse-only `onclick="window.location.href=...` navigation sites across 11 views (`Calendar/Index.Mobile`, `Characters/Index.Mobile`, `Contacts/Index.Mobile`, `DungeonMaster/Profile.Mobile`, `Events/Index`, `Events/Index.Mobile`, `Players/Index.Mobile`, `Quest/Index`, `Quest/Index.Mobile`, `QuestLog/Index`, `QuestLog/Index.Mobile`) now also carries a `row-nav-link` anchor to the identical destination — confirmed by a direct per-file grep count match (`onclick` count == `row-nav-link` count in every one of the 11 relevant files). The two `Quest/Manage*.cshtml` `window.location.href` occurrences are a post-delete AJAX redirect, not a row/card navigation site, and correctly were not touched. Both conditional-destination sites (`Quest/Index.cshtml`, `Quest/Index.Mobile.cshtml`) reuse the exact same ternary/`navUrl` expression for the anchor's `href` as the row's `onclick`, so an owner still reaches `Manage` and a non-owner still reaches `Details` via keyboard. New test file `RowNavigationAccessibilityTests.cs` proves a focusable `<a class="row-nav-link" href="...Events/Details/{id}...">` exists on both a desktop and a mobile surface via regex match against live rendered HTML.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` | Mobile card list with paging control | ✓ VERIFIED | Card list, counts, collapsible roster (now inert), empty state, and Show More control all present, wired, and test-covered (previously ⚠️ INCOMPLETE) |
| `QuestBoard.Service/ViewModels/EventViewModels/EventOverviewViewModel.cs` | Computed `CanShowMore` | ✓ VERIFIED | Present, correctly gates on `HasMore && NextTake > Take` |
| `QuestBoard.Service/wwwroot/css/events-overview.css` / `.mobile.css` | Own-column highlight, fixed sticky-column offsets, readable muted-Yes fill | ✓ VERIFIED | All three read directly and confirmed |
| `QuestBoard.Domain/Services/EventService.cs` | UTC-clock-driven date boundary | ✓ VERIFIED | `TimeProvider` injected, `GetUtcNow().UtcDateTime` used |
| `QuestBoard.Domain/Models/EventsOverviewOptions.cs` | `IsValid()` predicate | ✓ VERIFIED | Present, wired to `ValidateOnStart()`, covered by a test that would fail if unwired |
| `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` | Self-contained tests, no stale planning IDs | ✓ VERIFIED | `IAsyncLifetime` reset hook present, all comments plain-language |
| `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs` | Mobile-rendering + strengthened clamp/count facts | ✓ VERIFIED | Four new `Index_MobileUserAgent_*` facts, exact-boundary clamp assertions, exact-figure count assertions |
| `QuestBoard.Service/wwwroot/css/modern-card.css` | Shared `.row-nav-link` class | ✓ VERIFIED | Present, loaded by both layouts |
| `QuestBoard.IntegrationTests/Controllers/RowNavigationAccessibilityTests.cs` | Focusable-link proof on desktop and mobile | ✓ VERIFIED | Both facts present and passing |
| `.planning/REQUIREMENTS.md` | All four EVTVIEW IDs checked off | ✓ VERIFIED | Confirmed by direct read |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `EventOverviewViewModel.CanShowMore`/`NextTake` | Show More control (mobile) | conditional render + href | ✓ WIRED | Previously ✗ NOT WIRED — now confirmed wired and behaviorally tested |
| `EventOverviewViewModel.CanShowMore`/`NextTake` | Show More control (desktop) | conditional render + href | ✓ WIRED | Unchanged from prior pass, now gated on `CanShowMore` instead of bare `HasMore` (WR-01 closed on both surfaces) |
| `AddDomainServices` | `EventsOverviewOptions.IsValid()` | `.Validate(...).ValidateOnStart()` | ✓ WIRED | Confirmed by a test that resolves the options and asserts the thrown exception |
| `TimeProvider` registration | `EventService` constructor | DI injection | ✓ WIRED | `services.TryAddSingleton(TimeProvider.System)` → constructor parameter, confirmed by direct read |
| the own-column class on header/body cells | the two new own-column CSS rules | matching specificity + `!important` | ✓ WIRED | Confirmed by direct read; no longer dead CSS |
| the row-navigation link class | `modern-card.css`, loaded by both layouts | `<link>` in both `_Layout.cshtml` / `_Layout.Mobile.cshtml` | ✓ WIRED | Confirmed present in both |
| each new anchor's `href` | the same `Url.Action`/ternary target as the existing `onclick` | identical expression reused | ✓ WIRED | Confirmed on all 13 sites, including both conditional-destination sites |
| `EventRepository.GetUpcomingWithSignupsAsync` | ambient query filters | no manual predicate, no bypass | ✓ WIRED | `grep -c 'IgnoreQueryFilters'` = 0 across all three production files, re-confirmed after gap closure |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Whole-solution build | `dotnet build` | 0 errors, 20 pre-existing unrelated NuGet-version warnings | ✓ PASS |
| Full unit + integration suite (run once) | `dotnet test` | `QuestBoard.UnitTests`: 408 passed, 0 failed. `QuestBoard.IntegrationTests`: 560 passed, 0 failed. Total 968, 0 failed | ✓ PASS |
| Mobile paging control now present and behaviorally correct | direct read + `Index_MobileUserAgent_MoreEventsThanTake_ShowsShowMoreControl` | Seeds 2 events, requests `take=1` with a mobile UA, asserts `"Show More Events"` and `"take=11"` present | ✓ PASS |
| Mobile roster tap no longer navigates away | `Index_MobileUserAgent_RendersRosterToggle` | Asserts exactly 2 `event.stopPropagation()` guards (toggle + collapse container) | ✓ PASS |
| Options validation actually fails closed | `AddDomainServices_InvalidCeiling_ResolvingOptionsThrowsOptionsValidationException` | Builds a real `ServiceCollection`, resolves with `MaxTake=0`, asserts `OptionsValidationException` thrown | ✓ PASS |
| No filter bypass reintroduced | `grep -c 'IgnoreQueryFilters' EventRepository.cs EventService.cs EventsController.cs` | `0` / `0` / `0` | ✓ PASS |
| No stale planning identifiers remain in `LayoutNavigationTests.cs` | `grep -c 'NAV-0'` / `grep -n 'D-0'` | 0 matches for both | ✓ PASS |
| No debt markers in any file touched across 77-05..77-10 | `grep -nE "TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER"` across 16 touched files | 0 matches | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| EVTVIEW-01 | 77-01, 77-02, 77-03, 77-05, 77-06, 77-09, 77-10 | Grid of upcoming events × players with each player's availability | ✓ SATISFIED | Previously ⚠️ PARTIALLY SATISFIED (mobile truncation with no paging). Mobile now has full parity with desktop, test-covered end to end. |
| EVTVIEW-02 | 77-01, 77-02, 77-03 | Untouched default visually distinct from an actual answer | ✓ SATISFIED (code) | Three non-colour signals intact after the fill-weight change; perceived distinctness remains a human item, now higher-priority given the mechanism shift (see Human Verification #2) |
| EVTVIEW-03 | 77-01, 77-03, 77-09 | Per-event availability count | ✓ SATISFIED | Strengthened assertion (WR-08) now verifies the actual rendered figures, not substrings the legend also renders |
| EVTVIEW-04 | 77-04 | Never displays events/members from another board | ✓ SATISFIED | Unchanged, re-confirmed clean. `.planning/REQUIREMENTS.md` checkbox now ticked, closing the prior traceability gap. |

No orphaned requirements. All four EVTVIEW IDs are checked off in `.planning/REQUIREMENTS.md` and the traceability table (lines 169-172) reads "Complete" for all four — this is now justified by the evidence above, not merely asserted.

### Anti-Patterns Found

None blocking. All previously-found blockers and warnings from `77-REVIEW.md` were addressed:

| Finding | Prior Status | Current Status |
|---------|--------------|-----------------|
| CR-01 (mobile paging control missing) | 🛑 Blocker | ✓ Closed (77-05, test-covered by 77-09) |
| WR-01 (Show More dead link at `MaxTake`) | ⚠️ Warning | ✓ Closed (`CanShowMore` gate, 77-05) |
| WR-02 (own-column highlight dead CSS) | ⚠️ Warning | ✓ Closed (77-06) |
| WR-03 (sticky columns overlap on long titles) | ⚠️ Warning | ✓ Closed (77-06) |
| WR-04 (`Math.Clamp` throws on bad config) | ⚠️ Warning | ✓ Closed (77-07, startup validation + defensive floor) |
| WR-05 (clamp tests cannot fail) | ⚠️ Warning | ✓ Closed (77-09, exact-boundary assertions) |
| WR-06 (mobile roster tap navigates away) | ⚠️ Warning | ✓ Closed (77-05, test-covered by 77-09) |
| WR-07 (no mobile rendering test coverage) | ⚠️ Warning | ✓ Closed (77-09) |
| WR-08 (count test asserts substrings, not figures) | ⚠️ Warning | ✓ Closed (77-09) |
| WR-09 (unguarded `Cells[i]` indexing) | ⚠️ Warning | ✓ Closed (77-05, `i < row.Cells.Count ? row.Cells[i] : Empty` on both surfaces) |
| IN-01 (`Take` was dead) | ℹ️ Info | ✓ Closed (used in `CanShowMore`'s gate) |
| IN-02 (stale planning IDs in `LayoutNavigationTests`) | ℹ️ Info | ✓ Closed (77-08) |
| IN-06 (mouse-only click targets) | ℹ️ Info | ✓ Closed (77-10) |
| IN-07 (test fixture state not restored) | ℹ️ Info | ✓ Closed (77-08) |
| IN-08 (server-local clock mixed into UTC feature) | ℹ️ Info | ✓ Closed (77-07) |

Minor, non-actioned info items (IN-03, IN-04, IN-05) were also folded in by the gap plans' incidental cleanup where touched (IN-04's `.legend-card` rules are now duplicated into `events-overview.mobile.css`), but were not independently re-audited here since they carry no must-have or requirement weight.

**Newly reviewed scope (orchestrator-committed UI fixes, `db469594` / `c7f84da9`):**

| Change | Assessment |
|--------|------------|
| `.avail-cell-yes-muted` fill changed to solid `var(--bs-success)` with white dashed border | Consistent with D-01 ("muted Yes chip; a confirmed answer renders solid" — mechanism changed but intent, a distinguishable muted state, preserved). D-02's mandatory non-colour-signal requirement still holds: icon, italic label, and dashed border are all independent of fill weight and none were removed. Flagged for human visual confirmation (see Human Verification #2). |
| `modern-card.css` added to `_Layout.Platform.cshtml` | Restores styling that was missing since an earlier refactor; scoped, single-line addition, does not touch any Phase 77 must-have. No regression risk identified. |
| Calendar cross-links to Availability Overview changed from `btn-outline-secondary` to `btn-secondary` | Matches `CLAUDE.md`'s filled-button convention. Two-line change, confirmed via diff, no functional impact. |

No debt markers (`TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER`) found in any file touched by the six gap-closure plans or the two orchestrator commits.

## Human Verification Required

These items were flagged in the previous verification pass as requiring human judgment and remain open — they do not by themselves change the phase's completion status beyond routing to `human_needed`, since all automated must-haves now pass.

### 1. Mobile layout behavior under a real mobile user agent

**Test:** Load `/Events` on an actual mobile device/browser (not devtools emulation), tap a card, expand a roster, tap within the expanded roster, and — on a board with more than the default page size of upcoming events — tap "Show More Events".
**Expected:** The mobile layout renders correctly; tapping the "Show players" toggle expands the roster without navigating; tapping within the expanded roster (a name, a badge) also does not navigate, now that both the toggle and the collapse container carry `stopPropagation` guards; "Show More Events" loads a larger set.
**Why human:** Mobile views in this app are user-agent-selected, not breakpoint-driven — devtools emulation never exercises `Index.Mobile.cshtml`. Automated coverage now exists (`Index_MobileUserAgent_*` facts) and gives strong confidence the fix is real, but a live-device pass remains the only way to confirm actual touch behavior end to end.

### 2. Perceived visual distinctness of muted-default vs. confirmed cells

**Test:** View the availability grid/card list as a sighted user and, separately, simulate a colour-blindness filter or greyscale view.
**Expected:** The unconfirmed-default chip (solid green fill, white dashed border, clock icon, italic "Yes") should still read as clearly different from the confirmed-Yes chip (solid green fill, no border, check icon, normal weight) at a glance, and the empty cell (bare em-dash, no badge) should read as different from both.
**Why human:** This check is now more important than in the previous pass: the muted chip's fill weight was changed from a hollow tint to the same solid fill as the confirmed chip (orchestrator commit `db469594`, for readability), so the visual distinction now rests entirely on the dashed border, the clock icon, and the italic label rather than partly on fill weight too. Code-level evidence confirms all three signals are present and none were removed, but whether they read as *sufficiently* distinct at a glance — particularly the subtlety of a white dashed border against a solid button-sized badge — is a genuine, unavoidable human-judgment call.

## Gaps Summary

No gaps remain. The single blocking gap from the previous verification pass — the mobile availability overview having no paging control — is closed: `Index.Mobile.cshtml` now reads `Model.CanShowMore` and `Model.NextTake` exactly as the desktop view does, and this is proven by a new integration test (`Index_MobileUserAgent_MoreEventsThanTake_ShowsShowMoreControl`) that renders the mobile view under a mobile User-Agent, requests a small page size, and asserts both the control's presence and the correct `NextTake` arithmetic in the rendered `href`. This test would fail if the regression recurred, closing the structural gap (WR-07, no mobile rendering coverage at all) that let the original gap ship unnoticed.

All nine warnings and the five actionable info items from `77-REVIEW.md` were also closed by the six gap-closure plans, each with either a genuine behavioral test (WR-04, WR-05, WR-06, WR-07, WR-08) or direct code/CSS evidence (WR-01, WR-02, WR-03, WR-09, IN-01, IN-02, IN-06, IN-07, IN-08) confirmed by independent reading rather than by trusting the SUMMARY narratives. The full test suite (408 unit + 560 integration = 968 tests) passes with 0 failures, re-run independently as part of this verification. The named risk of the phase — a repeat of the tenant-scoping trap via `IgnoreQueryFilters()` on an aggregating page — was not reintroduced during gap closure; the audit grep remains clean across all three production files.

Two items remain open as human-verification-only, carried forward from the previous pass: real-device mobile behavior (unchanged in nature, now with stronger automated backing), and perceived visual distinctness of the muted-vs-confirmed cell (elevated in importance because an orchestrator-applied readability fix removed the fill-weight component of the distinction, though the three signals D-02 actually mandates remain intact on the code evidence). Neither is a functional gap; both route this report to `human_needed` per the standard decision tree rather than `passed`.

---

_Verified: 2026-08-29T13:30:00Z_
_Verifier: Claude (gsd-verifier)_
