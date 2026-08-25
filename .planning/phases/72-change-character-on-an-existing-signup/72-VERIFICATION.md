---
phase: 72-change-character-on-an-existing-signup
verified: 2026-08-25T00:00:00Z
status: human_needed
score: 12/12 truths verified
behavior_unverified: 0
overrides_applied: 0
human_verification:
  - test: "Open the change control on a signup that holds a Retired or Dead character (desktop and mobile), without touching the dropdown, and click Save."
    expected: "The modal opens with that character pre-selected (visibly, in the browser) and its status shown in parentheses; after Save, the signup still holds the same character — it must not silently fall back to the placeholder and clear on an untouched save."
    why_human: "The pre-select/injection logic lives entirely in client-side JavaScript inside _CharacterSelectModal.cshtml (show.bs.modal handler, option-injection branch). No browser/JS test exists in this repo (no Selenium/Playwright harness); the integration tests only assert the trigger's data-current-character-id attribute is correct, not that the browser actually renders the option as selected. Both plan 03 and plan 04 SUMMARY.md explicitly flag this as an open UAT item."
  - test: "Trigger the Remove-character flow (desktop and mobile) and confirm the native confirm() dialog appears, then confirm removal and observe a success toast."
    expected: "A browser confirm() dialog blocks the removal until accepted; after removal, a toast reading 'Character removed from your signup.' appears on both the desktop and mobile layouts."
    why_human: "confirm() and toast rendering are runtime browser behaviors not exercised by any integration test — flagged as open UAT in plan 02/03/04 SUMMARY.md."
  - test: "Visually confirm the mobile participant/waitlist row height is unchanged after adding the inline pencil/plus trigger, and that the trigger sits on the same line as the character name without wrapping."
    expected: "Row height and layout are visually identical to before this phase; the small pencil icon does not push the row taller or wrap to a new line."
    why_human: "Line-box height and visual alignment (the load-bearing p-0/border-0/lh-1/align-baseline/fa-xs class list) cannot be asserted from raw HTML; this is a rendering/CSS concern. Flagged as open UAT in plan 04 SUMMARY.md."
---

# Phase 72: Change Character on an Existing Signup — Verification Report

**Phase Goal:** A player who has already signed up for a quest can change which character they are bringing — or clear it back to none — from both the desktop and mobile quest Details pages, without a DM having to intervene.
**Verified:** 2026-08-25
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

Truths below merge the five ROADMAP.md Success Criteria with the load-bearing must-haves from all four plans' frontmatter, deduplicated. Each was checked against the actual codebase — not against SUMMARY.md claims — including re-reading the two blockers the code review found and independently confirming their fixes.

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A player whose signup already has a character can open a change control on the desktop Details page — in both the finalized-participants table and the waitlist table (SIGNCHAR-01) | ✓ VERIFIED | `Details.cshtml:127-138` (finalized cell) and `:258-269` (waitlist cell) both render a `btn-primary` trigger with `fa-edit`, guarded only by `isCurrentUser`, carrying `data-current-character-id="@participant.Character.Id"` / `@player.Character.Id`. Pinned by `QuestDetailsCharacterControlTests.cs` (14 test methods; own-row trigger identity, waitlist wiring). |
| 2 | The same player can do all of that from the mobile Details page, which previously showed the character as plain text only (SIGNCHAR-02) | ✓ VERIFIED | `Details.Mobile.cshtml:221` (participant row) and `:266` (waitlist row) render an inline `btn-link` trigger inside the existing `<small>` element, guarded by `isCurrentUser`. Pinned by `QuestDetailsMobileCharacterControlTests.cs` (14 test methods), all issued via `HttpRequestMessage` with an explicit mobile User-Agent, including one test proving a desktop User-Agent does **not** receive this markup (`MobileDetails_MobileUserAgentSelectsTheMobileView_AndDesktopUserAgentDoesNot`). |
| 3 | Submitting the change form with no character selected clears the signup's `CharacterId` to null, verified in the database (SIGNCHAR-03) | ✓ VERIFIED | `QuestController.UpdateSignupCharacter` treats an absent `characterId` as a clear and calls `UpdateSignupCharacterAsync(playerSignup.Id, null)`. `UpdateSignupCharacter_Post_WithNoCharacterIdField_ClearsSignupCharacterIdToNull` posts a body with the field omitted and asserts `CharacterId` is null via a fresh DB read. Passed in a live `dotnet test` run (431/431 integration tests green). |
| 4 | A signup holding a Retired or Dead character shows that character as the current selection with its status labelled; the change/clear UI does not silently wipe it (SIGNCHAR-04) | ✓ VERIFIED (server + markup), see also Human Verification #1 | Server: `UpdateSignupCharacter` and both signup-time save paths dropped the `CharacterStatus.Active` gate entirely (`grep` for `CharacterStatus.Active` / `character.Status` across `QuestController.cs` returns 0 matches); `UpdateSignupCharacter_Post_WithRetiredCharacter_AssignsIt`, `..._WithDeadCharacter_AssignsIt`, and `..._ResubmittingTheCurrentRetiredCharacter_LeavesItAssigned` all pass. Markup: `CharacterDisplayExtensions.ToSelectLabel()` appends the status name for non-Active characters (5 unit tests, all passing), and every one of the six `ViewBag.UserCharacters` read sites route through it. `Details_Get_WhenSignupHoldsARetiredCharacter_RendersTheTriggerCarryingThatCharacterId` (desktop) and its mobile counterpart confirm the trigger carries the Retired character's id and label even when it's the player's only character. The remaining piece — that the modal's client-side JS actually pre-selects it on open — is JS/browser behavior with no automated coverage; routed to Human Verification #1. |
| 5 | Changing the character remains possible after a quest is finalized, with no time-based cutoff (SIGNCHAR-05) | ✓ VERIFIED | `UpdateSignupCharacter` has no `IsFinalized` check (unlike its sibling `UpdateSignup`). `UpdateSignupCharacter_Post_OnFinalizedQuest_UpdatesSignupCharacterId` seeds a finalized quest 7 days out and asserts the swap persists. Passed. |
| 6 | Changing the character remains possible for waitlisted signups and all three signup roles (SIGNCHAR-06) | ✓ VERIFIED | `UpdateSignupCharacter_Post_ForWaitlistedSignup_UpdatesSignupCharacterId` and the 3-case `[Theory]` `UpdateSignupCharacter_Post_ForEachSignupRole_UpdatesSignupCharacterId` (Player=0, Spectator=1, AssistantDM=2) all pass. |
| 7 | A player cannot set their signup to a character owned by another user or belonging to another group, proven by an automated cross-group regression test (SIGNCHAR-07) | ✓ VERIFIED | `UpdateSignupCharacter_Post_WithAnotherUsersCharacterInSameBoard_ReturnsBadRequestAndLeavesCharacterUnchanged` and `..._WithCharacterFromAnotherBoard_ReturnsBadRequestAndLeavesCharacterUnchanged` both assert `HttpStatusCode.BadRequest` and an unchanged `CharacterId`. Both pass. The cross-board case seeds via `factory.Database.CreateContext()` (bypassing the `ActiveGroupId` filter) to genuinely construct a second-board character. |
| 8 | Changing a character does not delete the signup's date votes (code-review blocker CR-01) | ✓ VERIFIED | Confirmed by direct code read: `PlayerSignupRepository.UpdateCharacterAsync` (`PlayerSignupRepository.cs:23-30`) does a targeted `entity.CharacterId = characterId; SaveChangesAsync()` scalar write with no `.Include(DateVotes)` round-trip, and `PlayerSignupService.UpdateSignupCharacterAsync` (`PlayerSignupService.cs:35-46`) delegates to it instead of the general `UpdateAsync` aggregate path. Regression test `UpdateSignupCharacter_Post_WhenSignupHasDateVotes_LeavesThoseVotesIntact` seeds a `PlayerDateVoteEntity`, posts a character change, and re-reads the vote row — passes. REVIEW.md's Resolution table confirms this was verified to fail against the pre-fix code. |
| 9 | The two signup-time save paths (Details POST, JoinFinalizedQuest) accept the same widened character list the pickers now offer, rather than silently rejecting non-Active picks (code-review blocker CR-02) | ✓ VERIFIED | Confirmed by direct code read: `grep -n "CharacterStatus.Active\|character.Status"` on `QuestController.cs` returns 0 matches — the status gate is gone from both `:414` (Details POST) and `:461` (JoinFinalizedQuest), leaving only `character.OwnerId != user.Id`. `QuestSignupCharacterStatusTests.cs` (8 test methods) proves Retired/Dead characters now create real signups on both save paths, and that ownership is still enforced (`JoinFinalizedQuest_Post_WithAnotherPlayersCharacter_CreatesNoSignup`). REVIEW.md confirms these were verified to fail against the pre-fix code. |
| 10 | `ViewBag.UserCharacters` is populated once, unfiltered by status, and consumed by all six read sites without re-narrowing (D-12) | ✓ VERIFIED | `QuestController.Details` GET (`:318-339`) is the single writer, with no `.Where` on `Status`. All six read sites (`Details.cshtml` ×4, `Details.Mobile.cshtml` ×2, plus the modal's own option loop) consume it directly with no local status filter — confirmed by reading each read site's code. |
| 11 | Both host views render the shared `_CharacterSelectModal` partial exactly once | ✓ VERIFIED | `grep RenderPartialAsync("_CharacterSelectModal")` returns exactly one hit in `Details.cshtml:843` and exactly one in `Details.Mobile.cshtml:420`. `Details_Get_RendersTheSharedModalExactlyOnce` and its mobile counterpart both pass. |
| 12 | No requirement ID, phase number, or plan ID appears in any comment/string literal written by this phase (CLAUDE.md Code Comments rule) | ✓ VERIFIED | `grep -rnE "SIGNCHAR-|Phase 72|72-0[0-9]"` across all 11 files modified/created by this phase (controller, extension, partial, both host views, repository, service, and all 5 test files) returns 0 matches. |

**Score:** 12/12 truths verified (0 present-behavior-unverified — the one behavior-dependent truth, #4, has full server-side and markup-level test evidence; only its client-side JS pre-select behavior lacks automated coverage and is routed to Human Verification #1, not left as an unresolved truth).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` | `UpdateSignupCharacter` reworked, status gate dropped everywhere, board-scope defense-in-depth | ✓ VERIFIED | Confirmed by direct read; matches PLAN 01 must-haves exactly, plus post-review CR-01/CR-02 fixes. |
| `QuestBoard.Repository/PlayerSignupRepository.cs` — `UpdateCharacterAsync` | Targeted scalar write that doesn't touch `DateVotes` | ✓ VERIFIED | Present, substantive (not a stub), wired from `PlayerSignupService.UpdateSignupCharacterAsync`. |
| `QuestBoard.Service/Extensions/CharacterDisplayExtensions.cs` | Public `ToSelectLabel` extension | ✓ VERIFIED | Exists, public, used at all 7 render sites across `Details.cshtml`, `Details.Mobile.cshtml`, `_CharacterSelectModal.cshtml`. 5 unit tests pass. |
| `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml` | Model-less shared modal partial | ✓ VERIFIED | Exists, no `@model`, reads `ViewBag.UserCharacters` directly, renders once per host view. |
| `QuestBoard.Service/Views/Quest/Details.cshtml` | Old add-only modal replaced, change triggers added to both cells | ✓ VERIFIED | `addCharacterModal`/`addCharacterForm` blocks fully removed (`grep -c` returns 0 for both); shared partial rendered once. |
| `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` | Inline change/add triggers on both row types, shared partial rendered once | ✓ VERIFIED | Confirmed by direct read of both row blocks. |
| `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs` | ≥10 test methods | ✓ VERIFIED | 24 test methods/cases (including a 3-case Theory), all passing. |
| `QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs` | ≥6 test methods | ✓ VERIFIED | 14 test methods, all passing. |
| `QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs` | ≥7 test methods | ✓ VERIFIED | 14 test methods, all passing, real User-Agent throughout. |
| `QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs` | ≥5 test methods | ✓ VERIFIED | Exactly 5, all passing. |
| `QuestBoard.IntegrationTests/Controllers/QuestSignupCharacterStatusTests.cs` | Post-review CR-02 regression coverage | ✓ VERIFIED | Exists (not in original plan frontmatter — added during review remediation), 8 test methods, all passing. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `Details.cshtml` / `Details.Mobile.cshtml` triggers | `_CharacterSelectModal.cshtml` `show.bs.modal` handler | `data-quest-id`/`data-current-character-id`/`data-current-character-label` | ✓ WIRED | Same three attribute names used verbatim on both host views; confirmed by direct code read on all four trigger sites (desktop ×2, mobile ×2). |
| `ViewBag.UserCharacters` (single writer, `Details` GET) | 6 read sites | direct consumption, no re-filter | ✓ WIRED | Confirmed — no `.Where` re-narrowing found anywhere downstream. |
| Modal form | `POST /Quest/UpdateSignupCharacter` | `asp-action="UpdateSignupCharacter"` | ✓ WIRED | Confirmed in `_CharacterSelectModal.cshtml:22`. |
| `UpdateSignupCharacter` success/error | `TempData["Success"]`/`["Error"]` | `_Toasts.cshtml` on both layouts | ✓ WIRED | `TempData["Success"]` set on both swap and clear paths (distinct wording); `TempData["Error"]` set on the no-signup redirect path. `_Toasts.cshtml` is already rendered by both `_Layout.cshtml` and `_Layout.Mobile.cshtml` (pre-existing, unchanged). |
| `PlayerSignupService.UpdateSignupCharacterAsync` | `PlayerSignupRepository.UpdateCharacterAsync` | direct delegation | ✓ WIRED | Confirmed — the CR-01 fix routes through the new targeted method rather than the aggregate `UpdateAsync`. |

### Data-Flow Trace (Level 4)

Not applicable in the traditional sense (no dashboard/API-fed component), but the equivalent trace — server persistence → DB → next page render — was exercised directly: every integration test that asserts a positive outcome re-reads the `PlayerSignups` table (and, for the CR-01 regression, the `PlayerDateVotes` table) from a fresh `DbContext` scope rather than trusting the HTTP response. This rules out a hollow "returns 302 but doesn't actually write" failure mode.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full test suite passes (independent run, not trusted from SUMMARY/execution_state) | `dotnet test` | 313 unit + 431 integration, 0 failures | ✓ PASS |
| Build is clean | `dotnet build` | 6 projects, 0 errors, 20 pre-existing NU1608 package-constraint warnings (unrelated to this phase) | ✓ PASS |
| No status gate remains in the controller | `grep -n "CharacterStatus.Active\|character.Status" QuestController.cs` | 0 matches | ✓ PASS |
| No debt markers introduced | `grep -inE "TBD\|FIXME\|XXX\|TODO\|HACK\|PLACEHOLDER"` on all 7 modified/created source files | 0 matches (2 unrelated pre-existing `character-mini-avatar-placeholder` CSS class hits, not debt markers) | ✓ PASS |
| No requirement/phase ID leaked into source | `grep -rnE "SIGNCHAR-\|Phase 72\|72-0[0-9]"` on all 11 phase files | 0 matches | ✓ PASS |
| Working tree clean, all work committed | `git status --short` | empty | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| SIGNCHAR-01 | 72-01, 72-02, 72-03, 72-04 | Change character on desktop, both tables | ✓ SATISFIED | Truths #1, #6, #10, #11 |
| SIGNCHAR-02 | 72-02, 72-04 | Change character on mobile (previously impossible) | ✓ SATISFIED | Truth #2 |
| SIGNCHAR-03 | 72-01, 72-02, 72-03, 72-04 | Clear character back to none, desktop and mobile | ✓ SATISFIED | Truth #3 |
| SIGNCHAR-04 | 72-01, 72-02, 72-03, 72-04 | Retired/Dead character shown labelled, no silent wipe | ✓ SATISFIED (server + markup); browser pre-select behavior → Human Verification #1 | Truth #4 |
| SIGNCHAR-05 | 72-01 | No cutoff after finalization | ✓ SATISFIED | Truth #5 |
| SIGNCHAR-06 | 72-01 | Waitlisted + all 3 roles | ✓ SATISFIED | Truth #6 |
| SIGNCHAR-07 | 72-01 | Cross-user/cross-board rejection, automated test | ✓ SATISFIED | Truth #7 |

**Note on REQUIREMENTS.md staleness:** `.planning/REQUIREMENTS.md`'s Traceability table still lists all seven SIGNCHAR IDs as "Not started" — this predates phase completion and needs updating by the closing workflow; it is not evidence against the phase, just a stale tracking artifact. No requirement ID mapped to Phase 72 in either REQUIREMENTS.md or ROADMAP.md was left unclaimed by a plan — all 7 are covered above. No orphaned requirements found.

### Anti-Patterns Found

None that block the phase goal. Two pre-existing, phase-review-documented Warning/Info items remain intentionally open (see REVIEW.md "Still open" list) and are noted here for completeness, not as new findings:

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `_CharacterSelectModal.cshtml` | 62-66 | `show.bs.modal` handler returns before its reset block runs when `event.relatedTarget` is falsy (WR-01) | ⚠️ Warning | Only reachable via a non-trigger-driven modal open (no such call site exists in this codebase today); does not affect the normal trigger-driven flow this phase ships. Pre-existing, documented, not a regression this verification introduces. |
| `_CharacterSelectModal.cshtml` | 128 | Clear is encoded as field-absence rather than an explicit flag (WR-02) | ⚠️ Warning | Theoretical fragility (a future refactor renaming the field would silently clear); works correctly today. Documented in REVIEW.md, left open by design decision, not a phase-goal blocker. |
| `QuestDetailsCharacterControlTests.cs` | 168-194 | "Other player's row" test's viewer has no own character, so it can't distinguish correct scoping from "no controls rendered at all" (WR-05) | ⚠️ Warning | Test-strength gap only — the underlying view code was independently confirmed correct by direct read (per-row `isCurrentUser` check scoped inside the `@foreach`, `participant.Character.Id` used, not a page-level id). The mobile equivalent test does this correctly. |
| `Details.cshtml` / `Details.Mobile.cshtml` | 843 / 420 | Shared modal rendered unconditionally, including on pages with no trigger (IN-04) | ℹ️ Info | Dead markup / registered but inert JS handler on non-finalized or unauthenticated views. Cosmetic, not a functional defect. |
| `_CharacterSelectModal.cshtml` | 38 | Remove button uses `btn-outline-danger` against CLAUDE.md's "filled buttons, not outline" convention (IN-01) | ℹ️ Info | Hidden by `d-none` until JS reveals it; visible convention deviation once revealed. |

All of the above were independently re-confirmed by direct code inspection during this verification (not merely copied from REVIEW.md), and none prevent a player from changing, clearing, or seeing a labelled inactive character on either platform.

## Human Verification Required

### 1. Retired/Dead character pre-select on modal open

**Test:** Open the change control on a signup that already holds a Retired or Dead character (desktop and mobile), without touching the dropdown, then click Save.
**Expected:** The modal opens with that character visibly pre-selected in the dropdown, its status shown in parentheses (e.g. "Aldric the Bold - Level 5 (Fighter 5) (Retired)"); after Save, the signup still holds the same character.
**Why human:** The pre-select/option-injection logic (`_CharacterSelectModal.cshtml:62-114`) is client-side JavaScript with no browser-test harness in this repo. All automated coverage stops at "the trigger element carries the correct `data-current-character-id`/`data-current-character-label` attributes" — it does not prove the browser actually renders the `<select>` with that option selected. This is the exact risk (D-09, "silent character wipe") the phase was designed around, and it is the one link in the chain without an automated proof. Both plan 03 and plan 04 SUMMARY.md list this as an open UAT item.

### 2. Remove-character confirm dialog and success toast

**Test:** Trigger Remove-character on an existing signup (desktop and mobile); confirm the native browser `confirm()` dialog appears and blocks removal until accepted; after confirming, verify a toast reading "Character removed from your signup." appears.
**Expected:** `confirm()` blocks; toast renders on both layouts.
**Why human:** Runtime browser dialog and toast rendering are not exercised by any integration test in this repo. Flagged as an open UAT item in plan 02/03/04 SUMMARY.md.

### 3. Mobile row height/layout stability

**Test:** Visually compare a mobile participant/waitlist row before and after this phase.
**Expected:** Row height is unchanged; the pencil/plus trigger sits inline on the same text line as the character name, does not wrap, and does not visually enlarge the row.
**Why human:** This is exactly the CSS/line-box concern the plan called "load-bearing" (`p-0 border-0 lh-1 align-baseline fa-xs`) and explicitly deferred to visual UAT rather than claiming it from markup alone.

## Gaps Summary

No gaps. Both blockers found by the code review (CR-01: date-vote deletion on character change; CR-02: signup-time save paths silently rejecting the now-widened picker options) were independently re-verified as fixed by direct code inspection — not merely accepted on the review's or SUMMARY's word. Their regression tests were read and confirmed to assert the correct invariant against a real seeded state, and the full suite (313 unit + 431 integration) passes in a fresh, independent run. All seven SIGNCHAR requirement IDs have corresponding, passing test evidence. The three items routed to Human Verification are genuine browser/JS/visual behaviors with no automated test surface in this codebase (no Selenium/Playwright harness exists) — they were flagged as such by the executing plans themselves, not manufactured by this verification, and their absence does not indicate a broken implementation; the underlying server contract and markup wiring for all three are independently confirmed correct.

---

_Verified: 2026-08-25_
_Verifier: Claude (gsd-verifier)_
