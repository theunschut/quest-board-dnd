---
phase: 54-fix-mobile-signup-for-finalized-quests-inconsistent-with-des
reviewed: 2026-07-06T00:00:00Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - QuestBoard.IntegrationTests/Controllers/QuestJoinFinalizedQuestTests.cs
  - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs
findings:
  critical: 1
  warning: 3
  info: 1
  total: 5
status: issues_found
---

# Phase 54: Code Review Report

**Reviewed:** 2026-07-06T00:00:00Z
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

This phase ports a "Join This Quest" card from the desktop `Details.cshtml` finalized-quest view
to the mobile `Details.Mobile.cshtml` view, adjusts the `JoinFinalizedQuest` controller action to
waitlist (rather than reject) a `Player` join when the quest is full, and adds test coverage for
both. The controller change itself is correct and well-guarded (server-side recompute, no trust of
client state). However, the mobile view port dropped a critical guard clause from its desktop
analog: the "Join This Quest" card renders for **any** authenticated, not-yet-signed-up user,
regardless of whether the quest is finalized or the board type is Campaign. This means the card
(and its `JoinFinalizedQuest`-posting forms) renders simultaneously with the "Choose a Date" card
on open, non-Campaign quests — clicking "Join as Player" there would silently 404, since
`JoinFinalizedQuest` explicitly rejects non-finalized quests. This is exactly the class of
mobile/desktop inconsistency this phase was chartered to fix, reintroduced in the opposite
direction. No test in this phase's new coverage catches it, because every new "Join This Quest"
mobile test only exercises already-finalized quests.

Additional issues: a `ModelState` error added just before a `RedirectToAction` in
`JoinFinalizedQuest` is silently discarded (redirects don't carry `ModelState`), so all three
failure paths in that action give the user no feedback; a stale exact-`DateTime` comparison for
locating the finalized `ProposedDate` (vs. the `.Date`-only comparison used elsewhere in the same
file) can cause the "Vote" column to silently show "No Vote" for participants/waitlisted players
even when they voted; and GSD requirement IDs (`D-06`, `VOTE-04/05/06`) are embedded in test file
comments, which CLAUDE.md prohibits.

## Critical Issues

### CR-01: Mobile "Join This Quest" card renders on non-finalized and Campaign quests, posting to an action that will reject them

**File:** `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:37-39, 261`
**Issue:**
The mobile view computes:
```csharp
var selectedPlayersCount = Model.Quest?.PlayerSignups.Where(ps => ps.IsSelected && ps.Role == SignupRole.Player).Count() ?? 0;
var hasSpace = selectedPlayersCount < Model.Quest?.TotalPlayerCount;
var userCanJoin = User.Identity?.IsAuthenticated == true && !isPlayerSignedUp;
```
and renders the card with `@if (userCanJoin)` (line 261) — with no check for
`Model.Quest?.IsFinalized == true` and no check for `boardType != BoardType.Campaign`.

The desktop analog this was ported from (`Details.cshtml` lines 33-40) computes the equivalent
`userCanJoin` **nested inside** `@if (boardType != BoardType.Campaign) { @if (Model.Quest?.IsFinalized == true) { ... } }`,
so it can only ever be true for a finalized, non-Campaign quest. The mobile port copied the
variable formula verbatim but placed it at the top of the file's `@{ }` block, unguarded, and the
`@if (userCanJoin)` render block is likewise unguarded.

Concretely, for an authenticated user who has not signed up for an **open** (non-finalized),
non-Campaign quest, the mobile Details page now renders both:
- The correct "Choose a Date" card (lines 148-183, correctly gated on `Model.Quest?.IsFinalized == false`), and
- The new "Join This Quest" card, whose three forms post to `JoinFinalizedQuest`.

`QuestController.JoinFinalizedQuest` (`QuestController.cs` line 420) explicitly returns
`NotFound()` when `!quest.IsFinalized`, so clicking any of the three buttons on an open quest
silently 404s. The `54-UI-SPEC.md` design doc itself describes this card's applicability as
"authenticated + not yet signed up **on a finalized quest**" (line 92), confirming the guard was
intended but never implemented.

No test added in this phase catches this: all four new "Join This Quest" mobile tests in
`MobileViewsTests.cs` (`MobileQuestDetails_FinalizedQuest_*`) only seed finalized quests; there is
no test asserting the card is **absent** on an open quest.

**Fix:**
```csharp
var userCanJoin = User.Identity?.IsAuthenticated == true
    && !isPlayerSignedUp
    && boardType != BoardType.Campaign
    && Model.Quest?.IsFinalized == true;
```
Add a regression test mirroring `MobileQuestDetails_FinalizedQuest_AuthenticatedNotSignedUp_RendersJoinCard`
but seeding a non-finalized quest and asserting `html.Should().NotContain("joinPlayerFormMobile")`.

**Resolved:** Fixed in `6bba3f9` — `userCanJoin` now includes both checks exactly as suggested above,
and `MobileQuestDetails_OpenQuest_AuthenticatedNotSignedUp_DoesNotRenderJoinCard` was added asserting
the card is absent (and "Choose a Date" is present) on an open quest. Full suite green (501 tests)
after the fix.

## Warnings

### WR-01: `ModelState` errors in `JoinFinalizedQuest` are silently discarded by `RedirectToAction`

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:431-432, 449-450, 460-461`
**Issue:** All three failure branches in `JoinFinalizedQuest` call `ModelState.AddModelError(...)`
and then `return RedirectToAction("Details", new { id = questId })`. A redirect issues a fresh GET
request; `ModelState` does not survive across requests (no `TempData` is used to carry the error),
so the added errors are never rendered anywhere. Contrast with the sibling `Details(PlayerSignup, int)`
POST action (lines 388-389, 404-405), which uses `return await Details(questId)` — a direct
in-request re-render that does surface `ModelState` errors via the view. Neither `Details.cshtml`
nor `Details.Mobile.cshtml` renders `TempData["Error"]` or a validation summary for this action, so
a user who is already signed up, selects an invalid character, or hits the "could not find
finalized date" edge case gets redirected back to the same page with zero feedback about why their
join attempt did nothing.
**Fix:** Either call `return await Details(questId)`-equivalent logic to keep `ModelState` in the
same request, or (simpler, given this is a POST-redirect-GET flow) set `TempData["Error"]` for each
branch and render it in both `Details.cshtml` and `Details.Mobile.cshtml`, consistent with how
`Finalize`/`SendReminder`/`CreateFollowUp` already use `TempData["Error"]` elsewhere in this same
controller.

### WR-02: Inconsistent finalized-date lookup (exact `DateTime` equality vs. date-only) causes the "Vote" column to silently misreport

**File:** `QuestBoard.Service/Views/Quest/Details.cshtml:86, 217`
**Issue:** Two lookups in this file locate the `ProposedDate` matching the quest's finalized date
using exact `DateTime` equality:
```csharp
var finalizedProposedDateForSelected = Model.Quest?.ProposedDates
    .FirstOrDefault(pd => pd.Date == Model.Quest.FinalizedDate);   // line 86
...
var finalizedProposedDate = Model.Quest?.ProposedDates
    .FirstOrDefault(pd => pd.Date == Model.Quest.FinalizedDate);   // line 217
```
Every other equivalent lookup in this same file (line 71, 85 lines above the first occurrence) and
throughout `Details.Mobile.cshtml` (lines 31, 456 in `QuestController.cs`) instead compares
`.Date` only:
```csharp
var waitlistFinalizedProposedDate = ... .FirstOrDefault(pd => pd.Date.Date == Model.Quest.FinalizedDate.Value.Date);
```
If `FinalizedDate` and the corresponding `ProposedDate.Date` ever differ by so much as a
millisecond of time-of-day component (e.g. due to any future normalization difference, or a
re-finalize path that doesn't copy the exact `DateTime` verbatim), the exact-equality lookups at
lines 86 and 217 return `null`, and every participant/waitlisted-player row silently falls back to
"No Vote" in the rendered table — even for players who did vote — with no error or log signal.
**Fix:** Use the same `.Date`-only comparison consistently:
```csharp
var finalizedProposedDateForSelected = Model.Quest?.ProposedDates
    .FirstOrDefault(pd => pd.Date.Date == Model.Quest.FinalizedDate!.Value.Date);
```
and hoist a single `finalizedProposedDate` variable computed once near the top of the finalized
branch instead of recomputing it (with drifting logic) in three different places in the same file.

### WR-03: GSD requirement IDs embedded in test-file comments, violating CLAUDE.md

**File:** `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs:496`; `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs:142, 163, 184`
**Issue:** CLAUDE.md states: "Never embed GSD planning/tracking references in source code — no
requirement IDs (`D-06`, `TENANT-03`, `EMAIL-04`)... in comments... These references go stale the
moment a phase closes." This phase introduces:
```csharp
/// When a finalized quest's Player slots are full, the mobile Join card shows the locked
/// D-06 waitlist copy instead of implying a Player join is rejected.
```
and
```csharp
// Assert: VOTE-05 — Maybe keeps the seat, no promotion signal
// Assert: VOTE-06 — waitlisted signup voting No stays on the waitlist, no seat freed
// Assert: VOTE-04 — selected signup voting No frees the seat and signals promotion
```
These IDs will go stale once phase 54 closes, exactly the failure mode CLAUDE.md calls out.
**Fix:** Rewrite in plain language independent of the phase, e.g.:
```csharp
/// When a finalized quest's Player slots are full, the mobile Join card shows the waitlist
/// copy instead of implying a Player join is rejected.
```
```csharp
// Assert: a selected signup that votes Maybe keeps its seat, no promotion signal fires
```

**Resolved (partial):** The `MobileViewsTests.cs:496` comment (the `D-06` reference) was genuinely
introduced by this phase — confirmed via `git blame` (commit `e551d9b`, today) — and is fixed in
`6bba3f9`. The `PlayerSignupRepositoryTests.cs:142,163,184` `VOTE-04/05/06` comments predate this
phase (`git blame` shows commit `b2f7a097`, 2026-07-05, from Phase 44) — left as-is; fixing
pre-existing comments elsewhere in that file is out of this phase's scope.

## Info

### IN-01: `JoinFinalizedQuest`'s TOCTOU capacity race is unresolved but pre-existing/out of scope

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:440-441`
**Issue:** `isPlayerRoleWithSpace` is computed by reading `quest.PlayerSignups` fetched earlier in
the same request, then used to decide `IsSelected` on the new signup. Two concurrent `Player` joins
against the last open seat can both read `hasSpace = true` and both get seated, exceeding
`TotalPlayerCount` by one. This is the same recompute-then-decide shape the codebase already uses
elsewhere (e.g. `Finalize`'s `playerRoleCount` check), so it isn't a regression introduced by this
phase, but it's worth flagging since this phase specifically touches this capacity logic.
**Fix:** Not required for this phase; if addressed, use an application-level lock or a
`DbUpdateConcurrencyException`-driven retry keyed on quest id, or move the capacity check into the
same transaction/`SaveChanges` call as the signup insert with a `WHERE` guard.

---

_Reviewed: 2026-07-06T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
