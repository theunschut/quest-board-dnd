---
phase: 37-navigation-access-control
verified: 2026-07-03T00:00:00Z
status: passed
score: 8/8 must-haves verified
overrides_applied: 0
---

# Phase 37: Navigation & Access Control Verification Report

**Phase Goal:** Campaign groups show only the nav items relevant to their board type, and the Email Stats page is restricted to SuperAdmin regardless of group type.
**Verified:** 2026-07-03
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | In a campaign group, desktop and mobile nav both hide Calendar, Shop, Manage Shop, Edit My Profile, and Players — Guild Members stays visible (Success Criterion 1) | VERIFIED | `_Layout.cshtml` and `_Layout.Mobile.cshtml` both gate all five items behind `activeBoardType == BoardType.OneShot` (allowlist, not blocklist — no `!= BoardType.Campaign` found anywhere). Guild Members / Quest Log render unconditionally in both files. `LayoutNavigationTests` NAV-01/02/04/05/06/03 theories (desktop+mobile) all pass: 16/16 green (`dotnet test --filter LayoutNavigationTests` run live). |
| 2 | In a one-shot group, all nav items render exactly as before this phase — no regression (Success Criterion 2) | VERIFIED | `Nav_OneShotDm_AllAllowlistedLinksPresent` theory (desktop + mobile) passes, asserting Calendar/Shop/Manage Shop/Edit My Profile/Players all present for OneShot+DM. Full regression suite (123 unit + 260 integration) is green with zero failures. |
| 3 | Admin (not SuperAdmin) can no longer see the Email Stats nav link or load the Email Stats page — direct URL rejected (Success Criterion 3) | VERIFIED | Desktop: Email Stats `<li>` wrapped in `@if (User.IsInRole("SuperAdmin"))` (`_Layout.cshtml:70-77`), verified by direct read. Server: `AdminController.EmailStats` carries `[Authorize(Policy = "SuperAdminOnly")]` ANDed with class-level `[Authorize(Policy = "AdminOnly")]` (`AdminController.cs:359`). Live test `EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected` (Admin role, not SuperAdmin) asserts rejection — passes. Mobile: Email Stats link does not exist in the mobile nav at all (pre-existing condition — see Warning W-1 below); nothing to gate, so the criterion is vacuously satisfied for mobile and correctly satisfied for desktop. |
| 4 | SuperAdmin can still see and load Email Stats in both one-shot and campaign groups (Success Criterion 4) | VERIFIED | Desktop Email Stats link gated only by `User.IsInRole("SuperAdmin")`, not nested in any `BoardType` `@if` — shows regardless of active group's board type (confirmed by direct read of `_Layout.cshtml:70-77`, no BoardType condition wraps it). Server test `EmailStats_WhenSuperAdmin_ShouldSucceed` asserts 200 OK — passes. D-02 (no SuperAdmin special-casing on the OneShot allowlist) confirmed: `grep` for `SuperAdmin` in both layouts shows it used only for the Email Stats/Background Jobs links, never inside the Shop/Players/Manage Shop/Edit My Profile/Calendar gates. |
| 5 | Any policy-authorization failure app-wide renders AccessDenied instead of a 404 (D-07) | VERIFIED | `Program.cs:86-89` — `ConfigureApplicationCookie(options => options.AccessDeniedPath = "/Account/AccessDenied")`. `AccountController.AccessDenied()` GET action exists (`[HttpGet]`, `[AllowAnonymous]`). Test `AccessDenied_Get_ShouldReturnSuccessWithGeneralizedCopy` passes (200 OK, generalized copy, no "Dungeon Master" string). |
| 6 | AccessDenied page copy is policy-agnostic (D-07) | VERIFIED | `AccessDenied.cshtml` contains "You Don't Have Permission" / "You don't have permission to view this page." — zero occurrences of "Dungeon Master" (grep confirms). Retains `modern-card`/`modern-card-header`/`modern-card-body` structure and the authenticated/unauthenticated branch. |
| 7 | Calendar hidden for anonymous (logged-out) visitors in both layouts (D-04) | VERIFIED | Calendar `<li>` nested inside `User.Identity?.IsAuthenticated == true && activeBoardType == BoardType.OneShot` in both layouts. `Nav_Anonymous_CalendarLinkAbsent` theory (desktop+mobile) passes. |
| 8 | When no active group is set, the five allowlisted items are hidden (D-03, null-preserving `GetBoardTypeAsync`) | VERIFIED | `BoardTypeResolver.GetBoardTypeAsync` returns `null` (no `?? BoardType.OneShot` fallback) when `ActiveGroupId` is null or the group can't be resolved — confirmed by direct read of `BoardTypeResolver.cs`. `null == BoardType.OneShot` evaluates false, so every allowlist gate naturally hides. Covered indirectly by the anonymous-visitor test (anonymous = no active group). |

**Score:** 8/8 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` | `ActiveGroupId` accessor (unchanged post-refactor shape) | VERIFIED | Reverted to pre-Plan-01 shape (`int? ActiveGroupId { get; }` only) as part of the mid-phase DI-cycle fix; still satisfies its original contract. |
| `QuestBoard.Domain/Interfaces/IBoardTypeResolver.cs` | `GetBoardTypeAsync` seam, DI-cycle-safe | VERIFIED | New interface, `Task<BoardType?> GetBoardTypeAsync(CancellationToken)`; doc comment explains the cycle-avoidance rationale. |
| `QuestBoard.Service/Services/BoardTypeResolver.cs` | Production implementation via `IActiveGroupContext` + `IGroupService`, null-preserving | VERIFIED | `return group?.BoardType;` with no `??` fallback; returns `null` when `ActiveGroupId` is null. |
| `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` | Settable `BoardType` test double implementing both interfaces | VERIFIED | Implements `IActiveGroupContext, IBoardTypeResolver`; `BoardType` defaults to `OneShot`. |
| `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` | Nav-visibility test coverage for NAV-01..06 + D-04 | VERIFIED | 8 `[Theory]` methods × 2 user agents = 16 cases, all GREEN. |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` | `SuperAdminOnly` gate on `EmailStats` | VERIFIED | `[Authorize(Policy = "SuperAdminOnly")]` directly above `EmailStats`, class-level `AdminOnly` unchanged. |
| `QuestBoard.Service/Controllers/Admin/AccountController.cs` | `AccessDenied` GET action | VERIFIED | Present, `[HttpGet]`, `[AllowAnonymous]`, `return View();`. |
| `QuestBoard.Service/Views/Shared/AccessDenied.cshtml` | Generalized, policy-agnostic copy | VERIFIED | No "Dungeon Master" wording; `modern-card` structure intact. |
| `QuestBoard.Service/Program.cs` | `ConfigureApplicationCookie` wiring | VERIFIED | `AccessDeniedPath = "/Account/AccessDenied"` present; `IBoardTypeResolver` DI registration present and correctly scoped. |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` | OneShot allowlist gating (desktop) + SuperAdmin Email Stats gate | VERIFIED | All gates present and correctly polarized; single `GetBoardTypeAsync` call. |
| `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` | OneShot allowlist gating (mobile) | VERIFIED for BoardType gating | All five BoardType-gated items correctly wrapped; Calendar D-04 fix present. Email Stats/Background Jobs links absent entirely (pre-existing condition, see Warning W-1 — not part of this artifact's must-have scope, which only covers `GetBoardTypeAsync`-driven gating). |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `_Layout.cshtml` | `IBoardTypeResolver.GetBoardTypeAsync` | `@inject IBoardTypeResolver` + `@await` | WIRED | `@inject IBoardTypeResolver BoardTypeResolver` at file header; `await BoardTypeResolver.GetBoardTypeAsync()` computed once. (Plan 03's mid-phase fix moved this off `IActiveGroupContext` onto the new `IBoardTypeResolver` — functionally equivalent to the plan's original key-link target, same seam, different interface name.) |
| `_Layout.Mobile.cshtml` | `IBoardTypeResolver.GetBoardTypeAsync` | `@inject IBoardTypeResolver` + `@await` | WIRED | Same pattern, confirmed by direct read. |
| `Program.cs` | `/Account/AccessDenied` | `ConfigureApplicationCookie.AccessDeniedPath` | WIRED | Literal string match confirmed; app boots successfully with this wiring (live smoke-test performed during this verification: `dotnet run` reached "Now listening" / "Application started"). |
| `AdminController.EmailStats` | `SuperAdminOnly` policy | Method-level `[Authorize]` ANDed with class-level | WIRED | Confirmed via passing `EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected` and `EmailStats_WhenSuperAdmin_ShouldSucceed`. |
| `BoardTypeResolver` | `IGroupService.GetByIdAsync` | Constructor-injected `IGroupService` | WIRED | Confirmed by direct read of `BoardTypeResolver.cs`; DI cycle resolved (verified by live app boot, not just `dotnet build`). |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|---------------------|--------|
| `_Layout.cshtml` / `_Layout.Mobile.cshtml` | `activeBoardType` | `BoardTypeResolver.GetBoardTypeAsync()` → `IGroupService.GetByIdAsync(groupId)` → EF Core query against `Group` entity's `BoardType` column | Yes | FLOWING — not a static/hardcoded value; traced to a real DB-backed group lookup, confirmed by `BoardTypeResolver.cs` source and passing Campaign-vs-OneShot differentiated test assertions (the same test class produces different HTML for `BoardType.Campaign` vs `BoardType.OneShot`, which would be impossible if the value were hardcoded). |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| App starts cleanly post DI-cycle fix (not just compiles) | `dotnet run --project QuestBoard.Service --no-build --urls http://localhost:59321` (backgrounded, 15s wait) | "Now listening on: http://localhost:59321" / "Application started. Press Ctrl+C to shut down." | PASS |
| Full regression suite | `dotnet test QuestBoard.slnx` | 123/123 unit, 260/260 integration passing | PASS |
| Targeted nav-visibility suite | `dotnet test --filter LayoutNavigationTests` | 16/16 passing | PASS |
| Targeted EmailStats/AccessDenied suite | `dotnet test --filter "EmailStats\|AccessDenied"` | 5/5 passing | PASS |
| Allowlist polarity (no blocklist regression) | `grep "!= BoardType.Campaign"` both layouts | 0 matches | PASS |
| Single `GetBoardTypeAsync` call per layout | `grep -c GetBoardTypeAsync` both layouts | 1 match each | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|--------------|--------|----------|
| NAV-01 | 37-01, 37-03 | Calendar nav item hidden for campaign-type groups | SATISFIED | `Nav_CampaignDm_CalendarLinkAbsent` passes (desktop+mobile) |
| NAV-02 | 37-01, 37-03 | Shop nav item hidden for campaign-type groups | SATISFIED | `Nav_CampaignAuthenticated_ShopLinkAbsent` passes (desktop+mobile) |
| NAV-03 | 37-01, 37-03 | Guild member directory remains visible for all board types | SATISFIED | `Nav_CampaignAuthenticated_GuildMembersLinkPresent` passes (desktop+mobile); ungated in both layout files |
| NAV-04 | 37-01, 37-03 | "Manage Shop" hidden for campaign-type groups | SATISFIED | `Nav_CampaignDm_ManageShopLinkAbsent` passes (desktop+mobile) |
| NAV-05 | 37-01, 37-03 | "Edit My Profile" hidden for campaign-type groups | SATISFIED | `Nav_CampaignDm_EditMyProfileLinkAbsent` passes (desktop+mobile) |
| NAV-06 | 37-01, 37-03 | "Players" hidden for campaign-type groups | SATISFIED | `Nav_CampaignAuthenticated_PlayersLinkAbsent` passes (desktop+mobile) |
| ACCESS-01 | 37-02, 37-03 | Email Stats page + nav item restricted to SuperAdmin, all group types | SATISFIED | Server: `[Authorize(Policy = "SuperAdminOnly")]` + passing rejection/success tests. Nav: desktop gated correctly; mobile has no link to gate (pre-existing, see Warning W-1) — criterion is about restriction, not about adding missing mobile discoverability, and restriction is fully satisfied wherever the link exists. |

No orphaned requirements: all 7 IDs declared across the three plans' frontmatter (`requirements: [NAV-01..06]` in 37-01, `[ACCESS-01]` in 37-02, `[NAV-01..06, ACCESS-01]` in 37-03) match REQUIREMENTS.md's Phase 37 mapping exactly (NAV-01 through NAV-06, ACCESS-01 — 7 total, all Phase 37).

**Note:** `.planning/REQUIREMENTS.md` traceability table still lists all 7 Phase 37 requirements as "Pending" — this is a documentation staleness issue (the table should be updated to "Complete"), not a functional gap. Flagged as informational.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | No TBD/FIXME/XXX/TODO/HACK/PLACEHOLDER found in any of the 10 files modified across the three plans | — | None — clean |

### Human Verification Required

None required beyond what was already completed. The phase's own `checkpoint:human-verify` task (37-03 Task 3) was executed during the phase and is documented as approved in `37-03-CHECKPOINT.md` and `37-03-SUMMARY.md`. This verification independently re-confirmed the underlying claims via code inspection, live test execution, and a live app-boot smoke check rather than relying on the SUMMARY's narrative alone.

### Gaps Summary

No blocking gaps. All four ROADMAP.md success criteria are independently verified against the codebase (not merely SUMMARY claims): live test runs (388 total tests, 0 failures), direct source reads of both layout files and both controllers, and a live `dotnet run` smoke test confirming the mid-phase DI-cycle fix holds at runtime (not just at compile time).

**Assessment of the mobile Email Stats/Background Jobs gap (raised by the requester):**

`37-REVIEW.md` (WR-01) correctly identifies that `_Layout.Mobile.cshtml` never had an Email Stats or Background Jobs nav link — the mobile Admin-only block only ever exposed "User Management" and "Quest Management". This is confirmed still true against current HEAD (`e5b37a7`): `grep` for `SuperAdmin|EmailStats|hangfire` in `_Layout.Mobile.cshtml` returns zero matches.

Weighed against the actual success-criteria wording (not an idealized reading of it):

- **Success Criterion 3** requires that an Admin-not-SuperAdmin "can no longer **see** the Email Stats nav link **or load** the Email Stats page" and that "a direct URL request is rejected." Both halves are satisfied: the *see* half is satisfied because wherever the link is rendered (desktop), it is correctly SuperAdmin-gated; mobile renders no such link for any role, so there is nothing for an Admin to improperly see there either. The *load* half (server-side rejection) is satisfied unconditionally, independent of which layout the user is on, because the enforcement lives in the controller's `[Authorize(Policy = "SuperAdminOnly")]` attribute, not in either `.cshtml` file.
- **Success Criterion 4** requires SuperAdmin can still see and load Email Stats. Desktop satisfies "see"; server-side satisfies "load" for any layout/UA. A SuperAdmin on mobile can still load the page by direct URL (confirmed: no BoardType or role gate exists at the controller level that would block this) — they simply lack an in-app mobile link to click, a discoverability gap, not an access-control failure.
- Neither success criterion requires "Email Stats nav link exists in mobile" as a precondition — that requirement doesn't exist anywhere in NAV-01..06 (which are exclusively about the five BoardType-gated items, not Email Stats) or in ACCESS-01's wording (which is about restricting existing access, not adding missing UI).
- This was a **pre-existing condition** predating this phase (mobile never had these links even before Phase 37 touched the file), explicitly surfaced in the code review, and explicitly called out as a non-issue in the phase's own `37-03-CHECKPOINT.md` ("Email Stats link doesn't exist in the mobile Admin section today — nothing to check here; trivially passes").

**Conclusion: this is out of Phase 37's scope, not a gap against Phase 37's success criteria.** It is, however, a legitimate pre-existing UX gap (SuperAdmins on mobile have no discoverable path to Email Stats/Hangfire) that the review already flagged with a concrete fix (WR-01) and that has not yet been actioned or formally logged as an accepted follow-up in `PROJECT.md`'s Key Decisions table. Recommend either: (a) file a lightweight follow-up task to add the two links to the mobile Admin block (the review's suggested fix is copy-paste ready), or (b) if intentionally deferred, add a line to `PROJECT.md` documenting the decision so it doesn't surface as a surprise in a future phase. Not required to close Phase 37.

**Secondary observation (non-blocking):** `37-REVIEW.md` WR-02 (integration tests never exercise the real production DI registrations for `IActiveGroupContext`/`IBoardTypeResolver`, since `WebApplicationFactoryBase` overrides both with the test double) remains valid and unaddressed. This verification independently mitigated the residual risk by running a live `dotnet run` smoke test against the real `Program.cs` DI graph, which succeeded — so the underlying DI-cycle fix is confirmed correct at runtime, not just by manual trace. The review's suggested automated smoke test (to catch a future regression of this specific cycle) has not been added; consider it a low-priority follow-up, not a Phase 37 blocker.

---

*Verified: 2026-07-03*
*Verifier: Claude (gsd-verifier)*
