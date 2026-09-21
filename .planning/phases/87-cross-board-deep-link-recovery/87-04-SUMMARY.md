---
phase: 87-cross-board-deep-link-recovery
plan: 04
subsystem: auth
tags: [aspnet-core-mvc, ef-core-query-filters, session, tenancy, integration-testing]

# Dependency graph
requires:
  - phase: 87-01
    provides: "CrossBoardWebApplicationFactory harness, CrossBoardLinkRepository, ICrossBoardLinkResolver/CrossBoardLinkResolverService, CrossBoardDeepLinkMiddleware, TempDataKeys, the tracer route's paired-parity fact this plan generalises"
  - phase: 87-02
    provides: "CrossBoardLinkRegistry widened to all 18 routes and 8 lookup kinds, the entity-backed projections this plan's family theory exercises"
  - phase: 87-03
    provides: "GroupPickerController's picker-skip path, proven separately and not re-tested here"
provides:
  - "CrossBoardOracleParityTests -- the non-member/nonexistent-id paired-parity fact generalised from one route to all 8 lookup-kind families, plus a SuperAdmin, plus the no-half-switch and bare-404 structural facts"
  - "CrossBoardAuthorizationBoundaryTests -- both directions of the middleware's pre-authorization pipeline position pinned as a tested contract, plus proof that a cancelled event and a Draft shop item still resolve and switch even though the page itself declines to serve them"
affects: []

# Actuals (#2632)
actuals:
  tokens: 7600
  tasks: 2
  commits: 3

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Family-level parity theory: one paired non-member/nonexistent-id fact per lookup kind rather than per route, since routes sharing a kind share the exact same resolver code path"
    - "Header-set comparison with a named volatile-header exclusion list (Date, Request-Id, traceparent), stronger than the tracer's original status/body/Content-Type-only comparison"
    - "Pipeline-position-as-contract: an authorization-boundary fact that fails specifically if the middleware's Program.cs registration moves after UseAuthorization, rather than merely documenting the ordering in a comment"

key-files:
  created:
    - QuestBoard.IntegrationTests/Security/CrossBoardOracleParityTests.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs
  modified:
    - QuestBoard.Service/Views/Shared/_Toasts.cshtml
    - QuestBoard.Service/wwwroot/css/modern-card.css

key-decisions:
  - "Family routes for the parity theory use each family's natural Details-style read route where one exists (Quest, Event, Character, Contact, ShopItem, EventSeries); ContactCategory and BoardMember have no registered Details route, so their one registered route (an Edit page, a Profile page) stands in for the family, per the plan's own recorded per-family (not per-route) scoping choice"
  - "The viewer in the parity seed is Admin on every board they belong to, isolating the parity fact to the resolver's own behavior -- no policy check in any of the eight routes under test can itself distinguish the non-member request from the nonexistent-id request, so the only thing that can differ is what CrossBoardLinkRepository does"
  - "The 'must now succeed' authorization-boundary fact makes the viewer the target quest's own Dungeon Master (not merely a DM-tier role on the target board), because QuestController.Edit carries an independent ownership check beyond the DungeonMasterOnly policy -- isolating the fact to the policy question the plan asks, rather than tangling it with a second, unrelated ownership question the action asks afterward"
  - "The two 'resolves but the page will not serve it' facts assert the board-switch banner's board name and the entity's own visibility marker in the same response the resolver produced, rather than a separate follow-up request -- proving both the switch and the page's own decision in one HTTP round trip, matching the tracer suite's existing style"

requirements-completed: [D-01, D-11, D-12, D-13, D-14, D-15, D-17, D-18, D-19, D-20, D-21]

duration: ~50min
completed: 2026-09-21
status: complete
---

# Phase 87 Plan 4: Full-Width Oracle Parity and Authorization Boundary Summary

**Both automated tasks land clean on the first test run -- 11 paired-parity facts across all 8 lookup-kind families (plus a SuperAdmin, a no-half-switch fact, and a bare-404 fact) and 4 authorization-boundary facts pinning the middleware's pre-authorization pipeline position in both directions -- and a first human-verification pass on the board-switch banner found three real mobile defects (translucent background, navbar occlusion, a near-invisible switch-back button), all fixed and then re-verified by measurement, closing the phase with a conditional operator approval.**

## Performance

- **Duration:** ~50 min (includes a mobile-defect fix pass between the first and second human-verify rounds)
- **Started:** 2026-09-21 (worktree spawn)
- **Completed:** 2026-09-21
- **Tasks:** 3 of 3
- **Files modified:** 4 (2 created, 2 modified)

## Accomplishments

- `CrossBoardOracleParityTests.cs` -- 11 facts proving the non-member response and the nonexistent-id response are indistinguishable in status code, response body, and every header not itself a function of wall-clock time or per-request tracing plumbing, across all 8 lookup-kind families (Quest, Event, Character, Contact, ShopItem, EventSeries, ContactCategory, BoardMember), for a SuperAdmin on the Quest family, and two structural facts: a failed resolution never leaves the board half-switched, and the unresolvable case is still the bare framework 404 (empty body, no rendered error page).
- `CrossBoardAuthorizationBoundaryTests.cs` -- 4 facts proving the landed page's role check is judged against the board the request was just switched to: a Player-on-active/DungeonMaster-on-target viewer reaches the target board's quest edit page (and the board stays switched on a following request), while a DungeonMaster-on-active/Player-on-target viewer is refused that same page yet still ends up switched to the target board -- proving the resolver never consults roles. Two further facts show a cancelled event and a Draft-status shop item both resolve and switch the board even though each page's own rendering rules, not the resolver, are what withhold normal service.
- Both test classes passed on the first full run against the real solution: `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardOracleParity` (11/11), `--filter FullyQualifiedName~CrossBoardAuthorizationBoundary` (4/4), the full `QuestBoard.UnitTests` suite (692/692), the full `QuestBoard.IntegrationTests` suite (932/932), and the solution-wide `dotnet test --filter "FullyQualifiedName~CrossBoard"` (82 unit + 63 integration, all green).
- Task 3 -- the blocking human-verification checkpoint covering a real wrong-board link in a real browser, the signed-out emailed-link chain, and the banner on a real mobile user agent -- was run twice against this worktree's app over a real SQL Server. The first round found three mobile defects in the board-switch banner (documented under Deviations below); the second round re-verified all three fixes by settled-state DOM hit-testing on both a real Android UA and desktop, and the operator gave conditional approval. See "Human Verification Outcome" below for the full record, including what was and was not exercised live.

## Task Commits

1. **Task 1: Prove the non-member response and the nonexistent response are the same response, family by family** - `2f15fb48` (test)
2. **Task 2: Prove the landed page is judged with the board it landed on** - `217bb864` (test)
3. **Fix: opaque board-switch banner, mobile navbar clearance, readable switch-back button** - `409615eb` (fix) - see Deviations below
4. **Task 3: Human verification -- a real link, a real browser, a real phone** - two rounds run; conditionally approved by the operator on the second round -- see "Human Verification Outcome" below

**Plan metadata:** this SUMMARY's own commits (STATE.md/ROADMAP.md updates are the orchestrator's responsibility to apply centrally after merge, per this plan's execution instructions)

## Files Created/Modified

- `QuestBoard.IntegrationTests/Security/CrossBoardOracleParityTests.cs` - paired non-member/nonexistent-id parity theory across all 8 lookup-kind families, a SuperAdmin fact, a no-half-switch fact, and a bare-404 fact
- `QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs` - both directions of the pre-authorization pipeline-position contract, plus the two resolves-but-page-refuses facts (cancelled event, Draft shop item)
- `QuestBoard.Service/Views/Shared/_Toasts.cshtml` - the board-switch banner's switch-back button changed from `btn-outline-light` to `btn-dark`
- `QuestBoard.Service/wwwroot/css/modern-card.css` - added `.board-switch-toast` rules: an opaque `--bs-toast-bg`, and a mobile-only (`max-width: 767.98px`) `margin-top` clearing the fixed navbar

## Decisions Made

- Per-family (not per-route) parity coverage, per the plan's own recorded scoping choice: eighteen routes share eight lookup kinds and the same three-line resolver code path per kind, so a pair per route would exercise the same lines eighteen times over.
- The parity seed's viewer is Admin on every board they belong to, so no authorization policy in any of the eight family routes can itself distinguish the two requests under test -- isolating the fact entirely to `CrossBoardLinkRepository`'s own behavior.
- The authorization-boundary "must now succeed" fact makes the viewer the actual owning Dungeon Master of the target quest, not merely DM-tier on the target board, because `QuestController.Edit` layers an independent ownership check on top of the `DungeonMasterOnly` policy; conflating the two would have made the fact fail for the wrong reason.
- The two "resolves but the page will not serve it" facts read the board-switch banner and the entity's own visibility marker off the same HTTP response the resolver produced, rather than issuing a second request, matching the style already established in the tracer suite.

## Deviations from Plan

Tasks 1 and 2 executed exactly as written, with no deviations. Task 3's first human-verification pass found three real mobile defects in the board-switch banner, all fixed in this pass (Rule 1 -- bugs found during the plan's own human-verify step, not scope creep: the banner is part of what this phase built in 87-01, and unreadable/unreachable UI on the phase's own core deliverable is squarely in scope).

### Auto-fixed Issues

**1. [Rule 1 - Bug] Board-switch banner was translucent, letting page content bleed through**
- **Found during:** Task 3 (human verification), a real Android UA (Pixel 8, 375x812) against a real SQL Server
- **Issue:** `.board-switch-toast` carries `data-bs-autohide="false"` (deliberately -- the viewer must not lose the banner before reading it) but otherwise inherited Bootstrap's default `--bs-toast-bg: rgba(255,255,255,0.85)`. Every other toast in the shared partial auto-dismisses in 5-6 seconds, so the same 85%-opacity default was never previously noticeable; this toast can sit on screen indefinitely, and on a phone it landed on top of a quest card with the card's title and status badge visibly bleeding through at 15%.
- **Fix:** Added `.board-switch-toast { --bs-toast-bg: #ffffff; }` in `modern-card.css` -- opaque, scoped to this one toast, `.toast-container` and every other toast untouched.
- **Files modified:** `QuestBoard.Service/wwwroot/css/modern-card.css`
- **Verification:** `dotnet build` clean; full `QuestBoard.UnitTests` (692/692) and `QuestBoard.IntegrationTests` (932/932) suites green; the 11 oracle-parity and 4 authorization-boundary facts re-run individually, still green. This executor has no browser-preview tool in this environment; the operator's re-verification confirmed it directly by settled-state DOM measurement (`rgb(255, 255, 255)` at opacity 1, `document.elementFromPoint` at the toast's own centre resolving to `DIV.toast-body`) -- see "Human Verification Outcome" below.
- **Committed in:** `409615eb`

**2. [Rule 1 - Bug] Board-switch banner occluded the mobile navbar for as long as it stayed up**
- **Found during:** Task 3 (human verification), same session
- **Issue:** The toast occupies roughly the viewport's top 174px on a 375px-wide phone; the fixed mobile navbar occupies roughly its top 64px. `document.elementFromPoint` at the navbar's centre and at the hamburger toggler both resolved to the toast, not the navbar, for as long as the (deliberately persistent) banner was up.
- **Fix:** Added a `@media (max-width: 767.98px)` rule giving `.board-switch-toast` a `margin-top: 56px` -- clearing the reported 64px navbar boundary with an 8px margin, scoped to this one toast, leaving the shared `.toast-container` and desktop layout (which does not match the media query) untouched.
- **Files modified:** `QuestBoard.Service/wwwroot/css/modern-card.css`
- **Verification:** Same build/test run as above. The media query only fires below Bootstrap's `md` breakpoint (768px), so desktop rendering is structurally unaffected. The operator's re-verification confirmed the navbar became reachable by hit-testing its centre and the hamburger toggler directly -- see "Human Verification Outcome" below.
- **Committed in:** `409615eb`

**3. [Rule 1 - Bug] Switch-back button was nearly invisible, and the fix for Defect 1 alone would have made it worse**
- **Found during:** Task 3 (human verification), flagged after Defects 1 and 2, before the opacity fix landed
- **Issue:** The switch-back `<button>` used `btn-outline-light` (white text, white border) -- a style meant for a dark background. Against the toast's *original* translucent body the darker page bleeding through accidentally gave the white text a little contrast; making the background opaque white (Defect 1's fix) would have taken that accidental contrast to exactly 1:1, i.e. invisible. `btn-outline-light` was also already off house style (`.claude/ui-guidelines.md` calls for filled buttons, not outline).
- **Fix:** Changed the button to `btn-dark` in `_Toasts.cshtml` (filled, per house style). White text on `#212529` (Bootstrap's `--bs-dark`) measures approximately **15.4:1** by the WCAG relative-luminance formula -- comfortably clearing both the 4.5:1 bar for normal-size text and the 3:1 bar for a UI component boundary, against the new opaque white background from Defect 1's fix. `bg-info` (the header's own background, `#0dcaf0`) was deliberately avoided as a candidate: white text on it measures only about 1.9:1 and would not have cleared either bar.
- **Files modified:** `QuestBoard.Service/Views/Shared/_Toasts.cshtml`
- **Verification:** Same build/test run as above. The button's existing `btn-sm` sizing was left untouched -- it already measures 201x44px, meeting the 44px tap-target guideline. The operator's re-verification measured the settled contrast directly (white text on `rgb(33, 37, 41)`) and confirmed the button still measures 201x44 -- see "Human Verification Outcome" below.
- **Committed in:** `409615eb`

---

**Total deviations:** 3 auto-fixed (all Rule 1 -- bugs in the phase's own UI found during its own human-verify step)
**Impact on plan:** All three fixes are scoped entirely to `.board-switch-toast` and its own button; `.toast-container` and every other toast in the shared partial are untouched, `data-bs-autohide="false"` is untouched, and desktop rendering is untouched (the navbar-clearance rule only fires below the `md` breakpoint). No scope creep beyond the reported defects.

## Issues Encountered

Two things needed care during the fix, both resolved: (1) since `_Toasts.cshtml` has no `.Mobile.cshtml` twin and desktop/mobile layouts load separate stylesheets (`site.css` vs `mobile.css`), a fix living in either file alone would not reach the other layout -- resolved by adding the rules to `modern-card.css`, the file this codebase already uses for exactly this "shared by both layouts, no per-layout duplicate" situation (see its own file-level comment for `.modern-card`). (2) The opacity fix and the button-contrast fix are not independent -- making the background opaque without also fixing the button would have made the button's accidental low-contrast readability worse, not better; both were verified together in the same pass rather than one at a time.

`dotnet build` succeeded with 0 new warnings (only the two pre-existing `NU1608` package-constraint warnings) on both the Task 1-2 run and the post-fix run. No CLR flake was hit on either run; `QuestBoard.UnitTests` (692/692) and `QuestBoard.IntegrationTests` (932/932) both ran clean as full suites after the fix, and the 11 `CrossBoardOracleParity` and 4 `CrossBoardAuthorizationBoundary` facts were re-run individually post-fix and stayed green (these facts exercise the resolver and authorization pipeline, not the toast's CSS, so they were never expected to be affected -- re-run anyway per the coordinator's instruction).

## Human Verification Outcome

**Approval is conditional, not unqualified.** The operator's own words: *"I'll approve it for now. If there's anything broken, I'll let you know."* Part of what stands behind this approval is test coverage rather than a live browser exercise -- recorded below so this SUMMARY does not overclaim.

**What was directly re-verified, by settled-state DOM measurement (not a screenshot -- the first desktop screenshot attempt caught the toast's own `.fade` transition mid-flight and looked like bleed-through that was not real; the hit-test below is the evidence that counts):**

- **Mobile (real Android UA, Pixel 8, 375x812, mobile view served):** background measured `rgb(255, 255, 255)` at opacity 1; `document.elementFromPoint` at the toast's own centre resolves to `DIV.toast-body` (nothing drawn over it, nothing bleeding through). The navbar centre now hit-tests to `A.navbar-brand` and the hamburger to `SPAN.navbar-toggler-icon` -- before the fix both resolved to the toast itself (`DIV.toast-header` / `BUTTON.btn-close`). The toast starts at y=72 against a 64px-tall navbar: an 8px clearance, exactly the margin this fix intended. The switch-back button is `btn-dark`, white text on `rgb(33, 37, 41)`, still measuring 201x44 -- the tap target survived the change.
- **Desktop (1280x900):** the `max-width: 767.98px` media query correctly does not fire -- `margin-top` computes to `0px` and the toast sits at its original y=16 top-right position. Background opaque, button readable, underlying navbar links reachable.

**What was NOT exercised live, and why that is an accepted gap rather than an oversight:**

- **The picker-skip path (part of the plan's check 2, the emailed-link flow).** The branch that skips the board picker and lands directly on the page is gated on `!isSuperAdmin`, and the only account available for this verification session was a SuperAdmin -- the path is structurally unreachable for that account, so it could not be exercised live in this session regardless of how the rest of check 2 behaved. The operator accepted the 9 integration facts covering this path instead of a live exercise.
- **The mobile banner (check 3).** Verified with a mobile user-agent override on a desktop browser, not on a physical handset.
- **Whether the automatic switch removes the friction it exists to fix (check 4).** Approved provisionally, without an extended real-world session across real boards over real time -- the operator's "I'll let you know if anything's broken" is the standing signal for this specific point.

## Next Phase Readiness

- All three tasks are complete. The phase's two governing properties -- no existence/membership oracle, and no authorization decision against the wrong board -- are proven at the full 18-route, 8-family width the phase widened to, and the human-facing banner is now opaque, clear of the mobile navbar, and its switch-back button is readable on both layouts.
- The phase closes on a **conditional** approval, not an unqualified one. The three gaps above (picker-skip path unreachable for the verifying account, mobile check done via UA override rather than a physical device, and the friction-removal question answered provisionally) are the operator's own accepted risk, not defects -- but they are real limits on what this phase's evidence actually covers, and a future session that turns up a problem in any of the three should not be read as a regression of something this phase proved, since none of the three claims were proven by direct observation here.
- No blockers. The application builds and all test suites pass; every fix in this plan is scoped to `.board-switch-toast` and its own button, with `.toast-container`, every other toast, and desktop rendering all left untouched.

## Self-Check: PASSED

All four created/modified files confirmed present on disk; all commits (`2f15fb48`, `217bb864`, `409615eb`) confirmed in `git log`.

---
*Phase: 87-cross-board-deep-link-recovery*
*Plan: 04*
*Completed: 2026-09-21*
