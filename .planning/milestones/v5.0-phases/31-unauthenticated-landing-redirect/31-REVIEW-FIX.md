---
phase: 31-unauthenticated-landing-redirect
fixed_at: 2026-07-01T08:34:24Z
review_path: .planning/phases/31-unauthenticated-landing-redirect/31-REVIEW.md
iteration: 1
findings_in_scope: 7
fixed: 6
skipped: 1
status: partial
---

# Phase 31: Code Review Fix Report

**Fixed at:** 2026-07-01T08:34:24Z
**Source review:** .planning/phases/31-unauthenticated-landing-redirect/31-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 7 (2 Critical, 5 Warning — Info findings excluded per `fix_scope: critical_warning`)
- Fixed: 6
- Skipped: 1

Before applying fixes, each finding was checked against the actual current state of the source
(not just the REVIEW.md prose), because several findings described a code state that had already
partially changed. Notably: `GroupPickerController` already had `returnUrl`/`Url.IsLocalUrl`
handling (WR-01's controller-side half was already correct — only the middleware side was
missing it), and the session-key constant referenced in IN-01 (out of scope, but discovered
during investigation) already existed as `SessionKeys.ActiveGroupId`/`ActiveGroupName`.

## Fixed Issues

### CR-01: Non-GET requests are silently redirected, discarding POST form data without warning

**Files modified:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`
**Commit:** 12b59b6
**Applied fix:** Added an explicit `HttpMethods.IsGet`/`IsHead` check before the redirect branch.
Non-idempotent requests (POST/PUT/PATCH/DELETE) now short-circuit with `409 Conflict` instead of
`Response.Redirect`, so a client submitting a form with an expired group session gets a
distinguishable failure signal instead of having the body silently dropped by the browser's
302-to-GET re-issue behavior. GET/HEAD requests still redirect as before, now additionally
carrying a `returnUrl` (see WR-01 below, fixed in the same commit since both issues live in the
same code branch).

### CR-02: Skip-list bypass check does not use segment-safe matching for every route, permitting prefix-collision bypass

**Files modified:** `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs`
**Commit:** 0ffe001
**Applied fix:** The REVIEW.md suggested deriving the exempt-list from `[AllowAnonymous]` endpoint
metadata. Investigation of the actual codebase found this suggestion does not port safely here:
this project does not use `[AllowAnonymous]` anywhere, and authorization is applied *inconsistently*
per-action rather than per-controller (e.g. `QuestController` has `[Authorize]` on some actions
such as `Create`/`Edit`/`Details POST` but NOT on others such as `Delete`, `Manage`, `Finalize`,
`Open`, `SendReminder`, `CreateFollowUp` — these are protected today only because the middleware's
deny-by-default exempt-list gates them, not because of their own `[Authorize]` attribution). Also,
no global fallback authorization policy exists (`AddAuthorizationBuilder()` sets specific policies
only). Replacing the hard-coded exempt-list with an "exempt if no `IAuthorizeData` metadata"
check would therefore have **introduced** a critical tenant-isolation regression by treating most
of `QuestController`'s unattributed actions as anonymous-exempt — the opposite of CR-02's intent.

Given this, the review's own explicitly-stated fallback was applied instead: reflection/route-
level regression tests that pin the middleware's actual gating behavior for the three newly-added
controller areas named in the review (`CalendarController`, `DungeonMasterController`,
`QuestLogController`), so a future overly-broad or missing exempt-list edit is caught by a
failing test rather than discovered in production. This closes the "silent drift" concern without
introducing the runtime regression the literal suggested fix would have caused.

**Note for maintainers:** a genuine architectural fix for CR-02 (deriving the group-gate from
authorization metadata) would first require establishing a consistent, class-level `[Authorize]`
convention (or a global fallback policy) across all controllers — that is a larger, separate
change than can be safely scoped into this fixer pass, and is not something this fixer applied.

### WR-01: No return-URL preservation through the group-selection redirect

**Files modified:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`
**Commit:** 12b59b6 (same commit as CR-01 — both issues live in the same redirect branch)
**Applied fix:** `GroupPickerController.Index`/`SelectGroup` already validated and used
`returnUrl` via `Url.IsLocalUrl` (this half was already implemented prior to this fix pass). The
missing piece was the middleware itself never sent a `returnUrl` when redirecting to
`/groups/pick`. Added `Uri.EscapeDataString(context.Request.Path + context.Request.QueryString)`
as a `returnUrl` query parameter on the GET/HEAD redirect path, completing the round trip.
Verified end-to-end with a new integration test (see WR-05 below).

### WR-02: `GroupSessionMiddlewareIntegrationTests.cs` does not cover the authenticated-user-with-zero-group-memberships case

**Files modified:** `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs`
**Commit:** c2ebcb2
**Applied fix:** `GroupPickerController.Index` already handled the zero-membership case
(`HasNoGroups` view-model flag rendering a friendly "not assigned to any group" message) —
this was already implemented but untested. Added
`Index_WhenUserHasNoGroupMemberships_ShouldReturnFriendlyEmptyState`, which creates a user via
`CreateAuthenticatedClientWithUserAsync(..., roles: [])` (skipping the helper's `UserGroups`
seeding step) and asserts a 200 with the friendly empty-state message, never a 500 or redirect
loop.

### WR-03: Duplicated/hand-maintained path literals between `GroupSessionMiddleware` skip-list and controller routes create silent drift risk

**Files modified:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`
**Commit:** 71626a7
**Applied fix:** Introduced a `ControllerNameOf<TController>()` helper using `nameof`-equivalent
reflection (`typeof(TController).Name` with the conventional `"Controller"` suffix stripped) and
replaced the `"/GroupPicker"` and `"/Account"` literals with
`$"/{ControllerNameOf<GroupPickerController>()}"` / `$"/{ControllerNameOf<AccountController>()}"`.
Renaming either controller class now requires the middleware file to be touched/recompiled
(surfaced by any rename-refactor tool), rather than silently breaking at runtime. `"/groups/pick"`
(an explicit custom `[Route]` on `GroupPickerController`, not derivable from the class name) and
`"/platform"`/`"/Error"` (an MVC area prefix and the exception-handler path, neither controller-
name-derived) remain literals with an explanatory comment, since `nameof` cannot represent them.

### WR-05: No full round-trip integration test for the deep-link -> group-picker -> original-destination flow

**Files modified:** `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs`
**Commit:** a1c5d2e
**Applied fix:** WR-01 being fixed made this round trip testable for the first time. Added
`DeepLink_NoActiveGroup_SelectGroup_ReturnsToOriginalDestination`, which: (1) requests `/Calendar`
as an authenticated user with no active group, asserts the redirect to `/groups/pick` carries
`returnUrl=/Calendar`; (2) POSTs `SelectGroup` with that `returnUrl`; (3) asserts the final
redirect lands back on `/Calendar` rather than the `RedirectToLocal` fallback destination. Because
this test harness authenticates via a static per-request header rather than a persisted login
cookie (documented in an existing comment on `SelectGroup_ShouldPersistActiveGroupInSession`),
the two hops are chained explicitly via the `returnUrl` value carried in each response rather than
relying on session-cookie round-tripping between requests.

## Skipped Issues

### WR-04: `HomeController` mixes anonymous landing-page and authenticated-redirect responsibilities in one action without a clear single-responsibility boundary

**File:** `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs`
**Reason:** Skipped as too risky relative to the finding's own stated severity. The review
explicitly labels this "Not blocking" and purely a code-clarity/readability concern (the method
body has to be read to know it also performs authenticated routing), not a functional defect. The
suggested fix — moving the "authenticated + group selected -> redirect to Quest board" branch out
of `HomeController.Index` and into `GroupSessionMiddleware` or a dedicated post-login handler —
is a genuine behavioral relocation, and `HomeControllerIntegrationTests.cs` has zero existing
coverage for the authenticated-user-hits-`/` case needed to safely validate such a refactor
without risking a regression. Given the finding is explicitly non-blocking and no safety net
exists to verify a behavioral move, this was left for a maintainer to address deliberately with
new test coverage added first, rather than forcing a refactor through this automated fix pass.
**Original issue:** The `Index` action branches internally on
`User.Identity.IsAuthenticated` plus group-selection state to decide between rendering the public
landing view or redirecting to the Quest board, conflating "public landing page" and "authenticated
app entry point routing" in a single `[AllowAnonymous]`-equivalent (unattributed, anonymous-by-
default) action.

## Verification

All fixes were verified via:
1. Re-reading the modified file sections (Tier 1, always).
2. `dotnet build` of the affected project(s) after each change (Tier 2).
3. Running the directly affected test file(s) after each change.
4. A full `dotnet test` run after every change: 55 unit tests + up to 188 integration tests
   (181 baseline + 7 new across this fix pass), 0 failures throughout — no regressions introduced.

---

_Fixed: 2026-07-01T08:34:24Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
