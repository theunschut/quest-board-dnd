---
phase: 72-change-character-on-an-existing-signup
fixed_at: 2026-08-26T00:00:00Z
review_path: .planning/phases/72-change-character-on-an-existing-signup/72-REVIEW.md
iteration: 1
findings_in_scope: 8
fixed: 8
skipped: 0
status: all_fixed
---

# Phase 72: Code Review Fix Report

**Fixed at:** 2026-08-26T00:00:00Z
**Source review:** `.planning/phases/72-change-character-on-an-existing-signup/72-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 8 (0 Critical, 8 Warning — Info findings excluded by `fix_scope: critical_warning`)
- Fixed: 8
- Skipped: 0

**Verification:** every fix was built with `dotnet build` (Razor views compile into
`QuestBoard.Service.dll` via the Razor source generator, so `.cshtml` edits are covered by the
build). After the last fix the full suite ran green: **313 unit + 433 integration tests, 0
failures**.

All work was done in an isolated git worktree on `gsd-reviewfix/72-fix`, then fast-forwarded
into `milestone/v9-rolling-improvements`; the worktree, temp branch, and recovery sentinel are
cleaned up.

## Fixed Issues

### WR-01: `ViewBag.UserCharacters` hard casts rest on an AutoMapper implementation detail

**Files modified:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`,
`QuestBoard.Service/Views/Quest/Details.cshtml`,
`QuestBoard.Service/Views/Quest/Details.Mobile.cshtml`,
`QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml`
**Commit:** `cfba063`
**Applied fix:** Pinned the concrete type at the writer (`userCharacters?.ToList() ?? new
List<Character>()`) and widened all eight reads. Both Details views now hoist a single
`var ownedCharacters = ViewBag.UserCharacters as IEnumerable<Character> ?? new List<Character>()`
in their top `@{ }` block; the four hard `(List<Character>)` casts and three soft
`as List<Character>` casts read through it. The shared modal reads
`as IEnumerable<Character>`. The 500-in-one-branch / silent-empty-`required`-picker split is
gone.

### WR-02: A non-selected AssistantDM gets no character-change control

**Files modified:** `QuestBoard.Service/Views/Quest/Details.cshtml`,
`QuestBoard.Service/Views/Quest/Details.Mobile.cshtml`,
`QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs`,
`QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs`
**Commit:** `d60ccbe`
**Status:** fixed — requires human verification (new user-visible UI section)
**Applied fix:** Chose "render the missing rows" over "narrow the requirement". Both views now
compute `unselectedNonPlayers` (`!ps.IsSelected && ps.Role != SignupRole.Player`) and render a
**Not Selected** section alongside the waitlist — a table on desktop, a `participant-list-mobile`
block on mobile — carrying the character cell, the change/add trigger, and a role badge, in the
same shape as the existing participant and waitlist rows. Added the two rendering tests the
review asked for (`signupRole: 2, isSelected: false` on a finalized quest, asserting a trigger
carrying that signup's character id appears), one desktop and one mobile. Both fail against the
pre-fix views and pass now.

**Human check requested:** the "Not Selected" section is new user-facing UI that has not been
through UAT. Worth eyeballing the desktop table and the mobile list once.

### WR-03: Board-scope re-check applied to one write path, skipped on the other two

**Files modified:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`
**Commit:** `df9437c`
**Applied fix:** Extracted the gate into a single private
`ResolveCharacterAssignmentAsync(int characterId, int userId)` returning a private
`CharacterAssignment` enum (`Assignable` / `NotFound` / `NotAssignable`), and routed all three
writers of the signup's `CharacterId` column through it — `Details` POST, `JoinFinalizedQuest`,
and `UpdateSignupCharacter`. The ownership check plus the active-board comparison now cover every
door, not just one. A three-state enum was used rather than the review's `bool` helper so that
WR-05 could still tell "vanished" apart from "never yours".

### WR-04: Five `ModelState.AddModelError` calls that can never reach a user

**Files modified:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`
**Commit:** `81e40da`
**Applied fix:** All five now set `TempData["Error"]`, which `_Toasts` renders from every layout.
The three already-redirecting sites keep their redirect. The two that re-rendered via
`return await Details(questId)` were converted to PRG (`TempData` + `RedirectToAction`) rather
than adding a validation summary — this matches the working sibling `UpdateSignupCharacter`,
keeps the whole action on one response shape, and avoids the set-then-read-in-same-request
TempData hazard that would have risked the toast reappearing on the next page load. Response
codes on those two error paths change from 200 to 302; no test asserted the 200 (checked
project-wide) and the full suite is green.

### WR-05: A reachable, non-tampering state produces a raw 400 text page

**Files modified:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`,
`QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs`
**Commit:** `c7dddb1`
**Status:** fixed — requires human verification (behaviour change on a security-adjacent path)
**Applied fix:** `UpdateSignupCharacter` now splits the outcomes: `NotFound` gets
`TempData["Error"] = "That character no longer exists."` plus a redirect back to the quest;
`NotAssignable` keeps the hard `BadRequest`.

**Human check requested:** as the review itself noted, the entity's model-level board filter
resolves a *cross-board* character to `null` before the action's own comparison runs — so a
cross-board submit now lands in the `NotFound` branch and returns 302 instead of 400. The write
is still rejected and the signup is unchanged; only the response shape softened.
`UpdateSignupCharacter_Post_WithCharacterFromAnotherBoard_...` was renamed from
`ReturnsBadRequestAndLeavesCharacterUnchanged` to `RejectsAndLeavesCharacterUnchanged`, its
assertion updated to 302 + `Location` pointing at the quest, and its doc comment updated to
explain why. The same-board different-owner test still pins the 400. Confirm the softer response
for cross-board submits is acceptable.

### WR-06: TOCTOU between the signup check and the write surfaces as an unhandled 500

**Files modified:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`
**Commit:** `db01c7e`
**Applied fix:** Wrapped `playerSignupService.UpdateSignupCharacterAsync` in a `try`/`catch
(ArgumentException)` that reports "You are no longer signed up for this quest." through
`TempData` and redirects — the same state the pre-write check already handles gracefully. Only
`ArgumentException` is caught: the review also mentioned an FK `DbUpdateException`, but catching
that would require an EF type in the Service project, which `CLAUDE.md` forbids.

### WR-07: The "shared" modal partial has two unguarded host contracts

**Files modified:** `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml`
**Commit:** `4f6e3ce`
**Applied fix:** Added `asp-controller="Quest"` to the form so the action URL no longer depends
on the ambient `ViewContext`, and documented both host obligations in the partial's header
comment block — the `ViewBag.UserCharacters` requirement (and what silently breaks without it)
alongside the existing trigger contract.

### WR-08: Status assertions are loose enough to pass on a failed request

**Files modified:** `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs`
**Commit:** `c5f1e50`
**Applied fix:** All eight `BeOneOf(OK, Redirect, Found)` assertions replaced with
`Should().Be(HttpStatusCode.Found)` plus a `Location` header assertion pointing back at the
quest, matching the tight pattern the no-signup test already used. The remaining
`BeOneOf(Redirect, Found)` (the same 302 value written twice) was collapsed to `Be(Found)` too;
the file now contains zero `BeOneOf` calls.

## Skipped Issues

None.

## Notes

- Info findings (IN-01 through IN-07) were **out of scope** for this run
  (`fix_scope: critical_warning`) and remain open. IN-05 in particular asks the operator to
  confirm that offering Dead/Retired characters for a *brand-new* signup is intended, not merely
  tolerated — that is a product decision, not a code fix.
- No GSD requirement IDs, phase numbers, or review-finding IDs were written into source
  comments, per `CLAUDE.md`. All finding IDs live in commit messages and this report only.

---

_Fixed: 2026-08-26T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
