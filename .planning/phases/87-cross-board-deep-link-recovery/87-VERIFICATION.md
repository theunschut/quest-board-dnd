---
phase: 87-cross-board-deep-link-recovery
verified: 2026-09-22T00:00:00Z
status: passed
score: 21/21 D-NN decisions verified (no truths behavior-unverified, no overrides needed)
covered_files: [".planning/phases/87-cross-board-deep-link-recovery/87-01-PLAN.md", ".planning/phases/87-cross-board-deep-link-recovery/87-01-SUMMARY.md", ".planning/phases/87-cross-board-deep-link-recovery/87-02-PLAN.md", ".planning/phases/87-cross-board-deep-link-recovery/87-02-SUMMARY.md", ".planning/phases/87-cross-board-deep-link-recovery/87-03-PLAN.md", ".planning/phases/87-cross-board-deep-link-recovery/87-03-SUMMARY.md", ".planning/phases/87-cross-board-deep-link-recovery/87-04-PLAN.md", ".planning/phases/87-cross-board-deep-link-recovery/87-04-SUMMARY.md", ".planning/phases/87-cross-board-deep-link-recovery/87-CONTEXT.md", ".planning/phases/87-cross-board-deep-link-recovery/87-RESEARCH.md", ".planning/phases/87-cross-board-deep-link-recovery/87-REVIEW.md", ".planning/phases/87-cross-board-deep-link-recovery/87-SECURITY.md", "QuestBoard.Domain/Enums/CrossBoardLookupKind.cs", "QuestBoard.Domain/Extensions/ServiceExtensions.cs", "QuestBoard.Domain/Interfaces/ICrossBoardLinkRepository.cs", "QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs", "QuestBoard.Domain/Models/CrossBoardTarget.cs", "QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs", "QuestBoard.IntegrationTests/Controllers/CrossBoardPickerSkipTests.cs", "QuestBoard.IntegrationTests/Helpers/CrossBoardWebApplicationFactory.cs", "QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs", "QuestBoard.IntegrationTests/Middleware/CrossBoardRouteCoverageTests.cs", "QuestBoard.IntegrationTests/Middleware/CrossBoardTestHarnessTests.cs", "QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs", "QuestBoard.IntegrationTests/Security/CrossBoardOracleParityTests.cs", "QuestBoard.Repository/CrossBoardLinkRepository.cs", "QuestBoard.Repository/Extensions/ServiceExtensions.cs", "QuestBoard.Service/Constants/TempDataKeys.cs", "QuestBoard.Service/Controllers/GroupPickerController.cs", "QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs", "QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs", "QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs", "QuestBoard.Service/Program.cs", "QuestBoard.Service/Services/ActiveBoardSwitcherService.cs", "QuestBoard.Service/Services/IActiveBoardSwitcher.cs", "QuestBoard.Service/Views/Shared/_Toasts.cshtml", "QuestBoard.Service/wwwroot/css/modern-card.css", "QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs", "QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs", "QuestBoard.UnitTests/Helpers/CrossBoardRouteTargetTests.cs"]
covered_digest: "v1:sha256:a28e291362a1b361b11e16946de57998df976e3939c9cc56be90ffbdacfe904c"
behavior_unverified: 0
overrides_applied: 0
---

# Phase 87: Cross-Board Deep Link Recovery Verification Report

**Phase Goal:** A member who follows a link to a quest, event, character or contact on a board they
belong to lands on that page, on that board — instead of the error they get today because a
different board happens to be selected in their session.

**Verified:** 2026-09-22
**Status:** passed
**Re-verification:** No — initial verification

## Requirement Traceability Note

This phase has no `.planning/REQUIREMENTS.md` rows (confirmed by grep — no `Phase 87` entries
exist there). Per the phase's own recorded planning decision (`87-CONTEXT.md` § "No external
specs", and repeated in every plan's frontmatter comment), traceability runs on the locked `D-NN`
decision IDs in `87-CONTEXT.md` instead, the same convention used by Phase 86. This is not reported
as a gap.

The union of `requirements:`/`requirements-completed:` IDs across the four plans/summaries is
D-01 through D-21 (all 21 IDs `87-CONTEXT.md` defines). Every one is cross-referenced below against
the actual codebase, not just against plan claims.

## Goal Achievement

### Observable Truths (roadmap goal, decomposed)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A member with the wrong board active, following a real top-level navigation to a registered route for an entity on a board they belong to, lands on the page (not a 404) with no confirm step (D-01) | ✓ VERIFIED | `CrossBoardDeepLinkMiddleware.cs` guards 1–7 read exactly as designed; `CrossBoardDeepLinkMiddlewareTests`, `CrossBoardRouteCoverageTests` (26 facts) exercise this end to end; independently re-ran `CrossBoardAuthorizationBoundary` (4/4 pass) and `CrossBoardOracleParity` (11/11 pass) this session |
| 2 | The switch happens inside the same request — no redirect (D-06) | ✓ VERIFIED | `CrossBoardDeepLinkMiddleware.InvokeAsync` calls `await switcher.SwitchAsync(...)` then `await next(context)` — no `Redirect`/`RedirectResult` in the file |
| 3 | The landed page carries a one-shot banner naming the target board; gone on next navigation (D-03) | ✓ VERIFIED | `_Toasts.cshtml:78-105` renders from `TempDataKeys.BoardSwitchTargetName` (TempData is one-shot by ASP.NET Core contract); `CrossBoardDeepLinkMiddlewareTests` asserts marker present then absent on the following request |
| 4 | Switch-back posts to `SelectGroup` with only the previous board id — cannot bounce back onto the triggering link (D-04) | ✓ VERIFIED | `_Toasts.cshtml:90-101` form has exactly `__RequestVerificationToken` + hidden `groupId`, no return-path field; "no ping-pong" fact in `CrossBoardDeepLinkMiddlewareTests` and `GroupPickerController.SelectGroup`/`RedirectToLocal` unchanged |
| 5 | A non-navigation request (image, prefetch-hinted, background fetch) leaves the board untouched and gets today's response (D-02) | ✓ VERIFIED | `IsRealTopLevelNavigation` requires `Sec-Fetch-Dest: document` + `Sec-Fetch-Mode: navigate` and refuses `Sec-Purpose`/`Purpose`/`X-Moz` prefetch hints; header-matrix theory in `CrossBoardDeepLinkMiddlewareTests` and image-route theory in `CrossBoardRouteCoverageTests` assert the board is unchanged, not just the response |
| 6 | A POST to a cross-board URL behaves exactly as today (D-19) | ✓ VERIFIED | Explicit `!IsGet && !IsHead` guard (guard 2) in the middleware, ahead of any header/registry logic; verb-check fact in `CrossBoardDeepLinkMiddlewareTests` |
| 7 | A non-member's request and a nonexistent-id request are indistinguishable — status, body, headers — for every entity family and for SuperAdmin (D-13, D-12, D-15) | ✓ VERIFIED | No branch exists at middleware guard 6 or in the picker's fall-through; `CrossBoardOracleParityTests` (11/11, re-run independently this session) covers all 8 lookup kinds + SuperAdmin + no-half-switch + bare-404 |
| 8 | Exactly one place writes the three active-board session keys; all three former call sites converge on it (D-08) | ✓ VERIFIED | `ActiveBoardSwitcherService.SwitchAsync` is the only writer; `grep -c 'Session.SetInt32' GroupPickerController.cs` (excluding comments) is 0; middleware never writes session keys directly, only via `IActiveBoardSwitcher` |
| 9 | The lookup returns only a board id; the banner's board name comes from the viewer's own membership list (D-10) | ✓ VERIFIED | Every projection in `CrossBoardLinkRepository.cs` is `Select(x => (int?)x.GroupId)`; `CrossBoardLinkResolverService` reads the name from `groupService.GetGroupsForUserAsync`, not from the repository |
| 10 | The resolver takes only a userId — no role flag — so SuperAdmin gets the same non-member miss as anyone else (D-12) | ✓ VERIFIED | `ICrossBoardLinkResolver.ResolveAsync(kind, id, int userId, ...)` signature carries no `ClaimsPrincipal`/role; `CrossBoardOracleParityTests`' SuperAdmin fact passes |
| 11 | Middleware registered after `GroupSessionMiddleware`, before `UseAuthorization` (D-06, D-18) | ✓ VERIFIED | `Program.cs:327` `GroupSessionMiddleware`, `:332` `CrossBoardDeepLinkMiddleware`, `:334` `UseAuthorization` — confirmed by direct grep with line numbers; both directions pinned by `CrossBoardAuthorizationBoundaryTests` (4/4, re-run independently) |
| 12 | `GroupSessionMiddleware` unchanged; the two paths stay separately testable (D-07) | ✓ VERIFIED | `git diff 0ff07765 HEAD -- QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` returns nothing (checked against the pre-phase base commit recorded in 87-SECURITY.md) |
| 13 | A route absent from the closed registry never resolves; renames/additions surface as failing tests (D-09) | ✓ VERIFIED | `CrossBoardLinkRegistry` is a closed, `nameof`-derived dictionary; `CrossBoardLinkRegistryTests` asserts exact membership |
| 14 | All 18 board-scoped GET routes (8 read + 10 edit/manage) resolve; the 6 image routes and shop modal excluded for free (D-16) | ✓ VERIFIED | Registry contains exactly 18 entries (confirmed by reading the file); `CrossBoardRouteCoverageTests` (26 facts) proves each by real HTTP round trip |
| 15 | Profile links resolve only when the answer is unambiguous; a missing id is a self-edit no-op (D-17) | ✓ VERIFIED | `CrossBoardLinkResolverService.ResolveBoardMemberAsync` returns null unless `sharedGroupIds.Count == 1`; route reader requires a parseable id, so a null id never reaches the resolver |
| 16 | Filter bypass confined to one allowlisted repository class; a new call site elsewhere fails the build (D-11) | ✓ VERIFIED | Independent repo-wide grep for `IgnoreQueryFilters` across `QuestBoard.Repository/Domain/Service` returns exactly the 5 allowlisted files; `CrossBoardIgnoreQueryFiltersSeamTests` (7/7, re-run independently) enforces this at build time |
| 17 | Oracle parity is structural — same code path for non-member and nonexistent id (D-13) | ✓ VERIFIED | Same as #7; no distinguishing branch found in a direct read of the middleware and the picker |
| 18 | The bare 404 stays exactly as it is — no status-code page registered (D-14) | ✓ VERIFIED | `grep -n "UseStatusCodePages" Program.cs` returns nothing |
| 19 | A viewer's session expired with a resolvable `returnUrl` skips the picker entirely (D-05) | ✓ VERIFIED | `GroupPickerController.Index`'s new branch (lines 46-59), gated on `!isSuperAdmin`; `CrossBoardPickerSkipTests` (9 facts, per 87-03-SUMMARY) proves the skip, the fall-throughs, and the SuperAdmin exemption |
| 20 | Email job URLs and the calendar feed are unchanged (D-20, D-21) | ✓ VERIFIED | `git diff 0ff07765 HEAD -- QuestBoard.Service/Jobs QuestBoard.Domain/Services/CalendarFeedWriter.cs QuestBoard.Domain/Services/CalendarSubscriptionService.cs` returns nothing |
| 21 | A resolved link the page won't serve (cancelled event, restricted page) still switches; visibility/authz stays with the page (D-18) | ✓ VERIFIED | Resolver has no visibility/role logic by construction; `CrossBoardAuthorizationBoundaryTests` proves both the cancelled-event and Draft-shop-item cases resolve-and-switch while the page independently refuses/limits |

**Score:** 21/21 D-NN decisions verified against the actual codebase (0 present-but-behavior-unverified, 0 overrides needed).

### Human Verification (already run within the phase's own gate, not re-opened here)

`87-04-PLAN.md` Task 3 is a `checkpoint:human-verify` gate that already executed twice within this
phase (per `87-04-SUMMARY.md`) and received the operator's resume signal. Per this verification's
task instructions, these are pre-resolved and not treated as gaps or re-opened as new
human-verification items:

- A real wrong-board link in a real browser: confirmed working, banner readable, switch-back does
  not bounce.
- Three real mobile UI defects (translucent banner, navbar occlusion, low-contrast button) were
  found on the first pass, fixed (commit `409615eb`), and re-verified by settled-state DOM
  measurement on a real Android UA.
- Two items remain **explicitly accepted as unproven by live observation, per the operator's own
  record** (not hidden by this verification):
  - The picker-skip path was accepted on 9 integration facts rather than a live exercise, because
    it is gated on `!isSuperAdmin` and the only verifying account was a SuperAdmin — structurally
    unreachable for that account, not a defect.
  - The mobile banner was checked via a Pixel 8 user-agent override, not a physical device.
- The operator's approval is explicitly conditional ("I'll approve it for now. If there's anything
  broken, I'll let you know.").

This verification treats the phase's own already-completed human-verify gate as satisfying Step 8
of the goal-backward process; it does not require a fresh human-verification round, and the overall
status below is not downgraded to `human_needed` on account of the two accepted-and-recorded
limits above, per the operator's own explicit acceptance recorded in `87-04-SUMMARY.md`.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Domain/Enums/CrossBoardLookupKind.cs` | 8-member closed enum | ✓ VERIFIED | Read directly: `Quest, Event, Character, Contact, ShopItem, EventSeries, ContactCategory, BoardMember` |
| `QuestBoard.Domain/Models/CrossBoardTarget.cs` | `GroupId`/`GroupName` record | ✓ VERIFIED | Present, used by resolver |
| `QuestBoard.Domain/Interfaces/ICrossBoardLinkRepository.cs` / `QuestBoard.Repository/CrossBoardLinkRepository.cs` | Membership-pinned lookup, `int?` only | ✓ VERIFIED | Read directly; every projection is `Select(x => (int?)x.GroupId)` |
| `QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs` / `CrossBoardLinkResolverService.cs` | userId-only resolution | ✓ VERIFIED | Read directly, matches design |
| `QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs` | Closed 18-route, area-aware table | ✓ VERIFIED | Read directly; area added by fix commit `f98a011f` (post-review) |
| `QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs` | Route-value + return-URL readers | ✓ VERIFIED | Both `TryFromRouteValues` and `TryFromLocalUrl` present, both call the registry |
| `QuestBoard.Service/Services/IActiveBoardSwitcher.cs` / `ActiveBoardSwitcherService.cs` | Sole session writer | ✓ VERIFIED | Writes all 3 keys together, read directly |
| `QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs` | 7-guard middleware | ✓ VERIFIED | Read directly, matches the documented guard order exactly |
| `QuestBoard.Service/Controllers/GroupPickerController.cs` | Picker-skip branch | ✓ VERIFIED | Read directly, matches design; both former inline writers now call the switcher |
| `QuestBoard.Service/Views/Shared/_Toasts.cshtml` | One-shot banner, no `.Mobile.cshtml` twin needed | ✓ VERIFIED | Partial rendered from `_Layout.cshtml:250`, `_Layout.Mobile.cshtml:211`, `_Layout.GroupPicker.cshtml:32` — confirmed by grep |
| `QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs` | 5-file allowlist confinement test | ✓ VERIFIED | 7/7 tests pass (independently re-run); repo-wide grep confirms exactly the same 5 files |
| Test files (10 total: unit + integration) | Full coverage per plan | ✓ VERIFIED | All present on disk; spot-run `CrossBoardOracleParity` (11/11), `CrossBoardAuthorizationBoundary` (4/4), `CrossBoardLinkRegistry` (47/47), `CrossBoardIgnoreQueryFilters` (7/7) — all green independently this session |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `Program.cs` | pipeline order | `UseMiddleware<CrossBoardDeepLinkMiddleware>` between `GroupSessionMiddleware` and `UseAuthorization` | ✓ WIRED | Confirmed by grep with line numbers: 327 / 332 / 334 |
| `ActiveGroupContextService` | `QuestBoardContext` filters | live Session read, no captured value | ✓ WIRED | `QuestBoardContext.cs` filter block untouched (`git diff` clean against pre-phase base) |
| `CrossBoardLinkResolverService` | `IGroupService.GetGroupsForUserAsync` | one read, two uses (membership set + banner name) | ✓ WIRED | Confirmed by reading `CrossBoardLinkResolverService.cs` |
| `_Toasts.cshtml` | all three layouts | shared partial | ✓ WIRED | grep confirms `_Layout.cshtml`, `_Layout.Mobile.cshtml`, `_Layout.GroupPicker.cshtml` all render it |
| `CrossBoardWebApplicationFactory` | real `IActiveGroupContext` | restores session-backed context for tests | ✓ WIRED | File present, referenced by all 6 new integration test classes as `IClassFixture` |
| `GroupPickerController.Index` picker-skip | `CrossBoardRouteTarget.TryFromLocalUrl` → `ICrossBoardLinkResolver` → `IActiveBoardSwitcher` | shared registry/resolver/switcher, third caller | ✓ WIRED | Confirmed by reading the controller |

### Requirements Coverage

No `.planning/REQUIREMENTS.md` rows exist for Phase 87 (confirmed by grep — this is a recorded
planning decision, not an omission). Coverage is instead tracked against the 21 `D-NN` decisions in
`87-CONTEXT.md`, all of which are marked ✓ VERIFIED in the Observable Truths table above.

### Anti-Patterns Found

Scanned all files in `covered_files` above for debt markers, placeholders, and stub patterns:

```
grep -n -E "TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER" across all new/modified production files → no matches
grep -rnE "(Phase 87|D-[0-2][0-9]|87-0[1-9]|RESEARCH\.md|CONTEXT\.md)" QuestBoard.Domain QuestBoard.Service QuestBoard.Repository --include='*.cs' --include='*.cshtml' → no matches
```

No blockers. The `87-REVIEW.md` code review (0 critical, 1 warning, 2 info) findings were all
addressed after the review was written:

| Finding | Status | Evidence |
|---------|--------|----------|
| WR-01 (registry not area-safe) | ✓ FIXED | Commit `f98a011f`: registry key now `{area}/{controller}/{action}`; `CrossBoardLinkRegistry.cs` and `CrossBoardRouteTarget.cs` read directly confirm the fix; `CrossBoardLinkRegistryTests`/`CrossBoardRouteTargetTests` extended accordingly |
| IN-01 (`TempDataKeys` comment omitted the picker as a second writer) | ✓ FIXED | Commit `4b6a26a2`: `TempDataKeys.cs` comment now names both writers, confirmed by direct read |
| IN-02 (`ResolveSharedBoardIdsForUserAsync`'s `IgnoreQueryFilters()` bypasses a filter that doesn't exist on `UserGroupEntity`) | Acknowledged, not a code defect | This is an info-level documentation nuance, not a behavioral issue — the method's actual safety comes from the `memberGroupIds.Contains(...)` predicate regardless of whether a filter exists to bypass. The method's own comment (read directly) already explains this is deliberate/future-proofing. Non-blocking per the review's own classification. |

### Behavioral Spot-Checks (independently re-run this session, not just read from SUMMARYs)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Oracle parity holds across all 8 families + SuperAdmin | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardOracleParity` | 11/11 passed | ✓ PASS |
| Authorization judged against the switched board, both directions | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardAuthorizationBoundary` | 4/4 passed | ✓ PASS |
| Registry closed at exactly 18 routes, area-aware | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~CrossBoardLinkRegistry` | 47/47 passed | ✓ PASS |
| Filter bypass confined to exactly 5 files | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~CrossBoardIgnoreQueryFilters` | 7/7 passed | ✓ PASS |
| No leaked planning references in source | `grep -rnE "(Phase 87|D-[0-2][0-9]|87-0[1-9]|RESEARCH\.md|CONTEXT\.md)" ...` | no matches | ✓ PASS |
| `GroupSessionMiddleware`/`QuestBoardContext`/email jobs/calendar feed untouched | `git diff 0ff07765 HEAD -- <files>` | empty diff | ✓ PASS |
| Working tree clean apart from planning tracking | `git status --porcelain` | only `.planning/STATE.md` modified, `.planning/state.json` untracked | ✓ PASS |

Note: the full-suite regression gate (703 unit + 932 integration, 0 failures) was already established
this session per the task instructions and is not re-run here; the above are additional, independently
executed spot-checks specific to this phase's surface.

### Gaps Summary

None. All 21 `D-NN` decisions this phase is scoped to are implemented and verified directly against
the codebase (not merely claimed by SUMMARY.md), all cited automated tests pass on independent
re-execution, the code review's one warning and two info findings were fixed in commits after the
review was written, the security gate is closed at 0 open threats, and the phase's own
human-verification checkpoint already ran twice and received conditional operator approval with its
accepted limits explicitly recorded (not hidden). No blockers found.

---

_Verified: 2026-09-22_
_Verifier: Claude (gsd-verifier)_
