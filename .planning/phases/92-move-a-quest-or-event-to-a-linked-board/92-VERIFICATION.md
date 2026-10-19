---
phase: 92-move-a-quest-or-event-to-a-linked-board
verified: 2026-10-19T00:00:00Z
status: human_needed
score: 21/21 locked decisions and roadmap truths verified in code and tests
behavior_unverified: 0
overrides_applied: 0
re_verification: false
human_verification:
  - test: "Open the Move page at 390px width (desktop devtools or a phone) for a quest and an event, with a long target board name and a long quest title"
    expected: "Heading, target select and the confirm button wrap without horizontal scroll or clipped text; the .Mobile twin renders the cards"
    why_human: "No test renders CSS. move-entry.css and move-entry.mobile.css are only checked for being linked, not for how they lay out"
  - test: "Move a quest as a super admin who is a member of the target board, then as one who is not"
    expected: "Redirect to the source Quest Board with the toast; the member variant shows a working link to the entry on the target board, the non-member variant shows plain text"
    why_human: "Toast layout and the link crossing the board switch were never seen in a browser. Tests assert the markup only"
  - test: "Preview the QuestMoved email (all three audiences) and the Waitlist Promoted moved line on EmailPreviewController with a very long quest title"
    expected: "No overflow or broken layout in a real mail client width"
    why_human: "Email rendering is checked as HTML text, not visually"
  - test: "Press the confirm button twice quickly on a slow save (WR-01)"
    expected: "Not a bare 404 as the final response. The move itself is done after the first press"
    why_human: "Needs a real browser and timing. The review reports this as a known gap with no submit-disable guard"
gaps: []
deferred: []
---

# Phase 92: Move a Quest or Event to a Linked Board - Verification Report

**Phase Goal:** A super admin can move an existing quest or event from its board to another board in the same link group, from that entry's Details page. After the move the entry lives on the target board as if it had been created there, and the boards in the group stop disagreeing about where it belongs. Boards that are not linked offer no move.
**Verified:** 2026-10-19
**Status:** human_needed (all automated checks pass; visual backstops and one UX race need a person)
**Re-verification:** No, initial verification

## Requirement ID Cross-reference

The ROADMAP assigns no requirement IDs (`Requirements: TBD`). The nine PLAN frontmatters declare IDs `D-01` to `D-21`. These are the locked decision IDs from `92-CONTEXT.md`, not REQUIREMENTS.md entries. REQUIREMENTS.md has no `D-xx` entry and no Phase 92 entry (checked with grep; the only substring hits are unrelated IDs such as CALFEED-01). Nothing is orphaned in REQUIREMENTS.md. Each declared decision is verified below against the code, so the "Requirements Coverage" table maps decisions, not requirements. REQUIREMENTS.md was not updated for this phase. That is a bookkeeping gap and does not block the goal.

## Goal Achievement

### Observable Truths

| # | Truth (source) | Status | Evidence |
|---|----------------|--------|----------|
| 1 | Only a SuperAdmin reaches the Move GET and POST, enforced server-side (D-01, ROADMAP) | VERIFIED | `MoveController` has class-level `[Authorize(Policy = "SuperAdminOnly")]`. The policy is `RequireRole("SuperAdmin")` (Program.cs:90). Both POSTs carry `[ValidateAntiForgeryToken]`. `BoardMoveAuthorizationTests.NonSuperAdmin_IsRefusedOnGetAndPost...` runs as Admin and DM of both boards and as Player, and checks the quest stays on its board. The event twin test does the same. |
| 2 | The Details button renders only for a SuperAdmin and not for an Admin or DM of both boards (D-01, D-13) | VERIFIED | `QuestController.cs:356` and `EventsController.cs:78` gate `ViewBag.MoveControl` on `User.IsInRole("SuperAdmin")`. `BoardAdminOrDmOfBothBoards_NeverSeesTheControl...` passes on desktop and mobile. |
| 3 | The target must be in the source board's set, read fresh on both GET and POST. The source board and out-of-set ids are refused (D-02, D-15) | VERIFIED | `QuestMoveService`/`EventMoveService.BuildPreviewAsync` call `GetLinkedBoardsForViewerAsync` on every preview and on `MoveAsync`, which rebuilds the whole preview. The repository query excludes the active board and checks set type. It does not filter by viewer membership, so a non-member super admin sees all targets. `APostedTargetOutsideTheSetOrTheSourceItself...` runs with the source board and a board outside the set. |
| 4 | A board in no set, or in a set of one, offers no move control and refuses a direct request (D-13) | VERIFIED | `MoveControlService` returns null when `linked.Boards.Count == 0`, so the view renders no control. `SuperAdminOnABoardOutsideAnySetOrInASetOfOne_SeesNoControl...` and `ASourceBoardInNoSetOrASetOfOne_GetsNoPageAndMovesNothing` pass on desktop and mobile. |
| 5 | After a move the entry's stored `GroupId` is the target, `FeedRevision` rose by one, the source board 404s and the target shows it (D-18, D-20, ROADMAP) | VERIFIED | Both repositories assign `GroupId` on the tracked entity and call one `SaveChangesAsync`. No `ExecuteUpdate`, `IgnoreQueryFilters`, `FromSql` or hand-set state in any new file (grep). Quest tests assert revision +1 and 404/OK per board. `ConfirmPost_MovesAOneOffEventAndShowsItOnlyOnTheTargetBoard` asserts revision 2, source 404 and target 200. |
| 6 | A kept signup (target member) keeps role, selection, times and votes, with `CharacterId` null (D-04) | VERIFIED | `QuestMoveRepository.ApplyAsync` sets `CharacterId = null` and touches nothing else on kept signups. `Move_KeepsMembersWithTheirVotesAndClearsTheirCharacter` passes. |
| 7 | A non-member's signup and its votes are dropped, for every role, and named on the page (D-05) | VERIFIED | `RemoveRange(signup.DateVotes)` plus `Remove(signup)`. The preview lists dropped people by name, selected first. `Move_RemovesNonMembersOfEveryRoleTogetherWithTheirVotes` passes. |
| 8 | Waitlist: dropping a selected Player on a finalized quest promotes the top kept waitlisted signup, and the email carries the target board id (D-06) | VERIFIED | `SelectPromotions` is pinned to `GetTopWaitlistedCandidateAsync` by `BoardMoveWaitlistParityTests`. `Move_PromotesTheTopWaitlistedMemberIntoTheFreedSeat` passes. The new `EnqueueWaitlistPromotedAfterMoveEmail` is called with `target.Id`. `BoardMoveQuestMailFlowTests` finds one `QuestWaitlistPromotedEmailJob`. The job signature is unchanged. |
| 9 | Proposed dates, reminder logs and kept votes follow the quest unchanged (D-07) | VERIFIED | No code touches them, and the query filters follow `Quest.GroupId`. `Move_KeepsProposedDatesAndReminderLogsOnTheQuest` passes. |
| 10 | Event answers are reconciled. Members' answers are kept, non-members' are dropped, and a Campaign target gets an automatic Yes row for each member without one (D-08) | VERIFIED | `EventMoveService` computes `campaignAdds` only when the set type is Campaign. `EventMoveRepository` adds `Availability = Yes` rows with the answered marker unset. `CampaignTarget_KeepsMemberAnswersDropsStrangersAndAddsAutomaticYesRows` and `OneShotTarget_AddsNoRowsAndPluralisesTheRemovedAnswers` pass. |
| 11 | A series occurrence cannot move. A one-off, including a detached one, can (D-09) | VERIFIED | `MoveEligibility.ForEvent` returns `SeriesOccurrence` when `SeriesId != null`. `EventSeriesRepository` clears `SeriesId` on detach (line 180). `SeriesOccurrence_IsRefused...` and `DetachedOccurrence_Moves` pass. |
| 12 | Chain links are cut at both ends and stored as null (D-10) | VERIFIED | The repository loads follow-ups before the move and nulls `OriginalQuestId` on the quest and on each follow-up. `Move_CutsBothChainLinksAndStoresThemAsNull` passes. The page names the quests via `OriginalQuestTitle` and `FollowUpQuestTitle`. |
| 13 | The quest's DM must be a DM or Admin on the target. A SuperAdmin DM passes with no row (D-03) | VERIFIED | `QuestMoveService` checks `GetGroupRoleAsync` and then `IsSuperAdminAsync`. The three tests (Player-only DM, no-row DM, SuperAdmin DM with no row) pass. The controller shows the reason. |
| 14 | Only unplayed quests and upcoming events can move. A cancelled upcoming event moves and stays cancelled (D-11, D-12) | VERIFIED | `MoveEligibility` takes `boardClock.Today` (no ambient read). `AmbientClockSeamTests` passes. Closed, played and past tests pass. `UpcomingCancelledEvent_MovesAndKeepsItsCancellation` passes. |
| 15 | A dedicated Move page exists in desktop and `.Mobile.cshtml` form, shows the consequences for the chosen target and preselects the only other board (D-14) | VERIFIED | `Views/Move/Index.cshtml` (270 lines) and `Index.Mobile.cshtml` (251 lines) both have the target picker, the GET preview and the POST confirm form with target and stamp. `BoardMovePageTests` covers desktop and mobile, the picker, the single target and the outside-set alert. Entry points exist in `Quest/Details.cshtml`, `Quest/Details.Mobile.cshtml` and `Events/Details.cshtml`. Events has no `.Mobile` twin, as planned. |
| 16 | The POST recomputes everything, and a changed preview re-shows the page instead of moving (D-15, discretion) | VERIFIED | `MoveAsync` rebuilds the preview and compares an ordinal stamp. The stamp covers kept, dropped, promoted, unlinked and added ids. A stale stamp gives `Changed`. `StaleStamp_ReShowsThePageAndMovesNothing` and the quest equivalents pass. |
| 17 | Afterwards the super admin lands on the source board's list with a toast. A target member also gets a link (D-16) | VERIFIED | `ConfirmQuest` redirects to Quest/Index and `ConfirmEvent` to Events/Index. `SetMovedToast` adds the link only when `ViewerIsMember`. The toast tests pass, including the encoded-title case. |
| 18 | A quest move mails the DM, the kept signups and the dropped players, never the actor and only confirmed addresses. An event move mails nobody (D-17) | VERIFIED | `EnqueueMoveMail` does an actor and confirmed-email check with a de-duplicating claim, and runs only after `Moved`. `Move_EnqueuesNoEmailOfAnyKind` (event) and the quest mail tests pass. The mail job takes the target board id. `QuestMoved.razor` has three audiences and an Email Preview entry. |
| 19 | The calendar feed code is unchanged (D-18) | VERIFIED | `git diff` of the phase range for `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and `FeedRevisionStamper.cs` is empty. No migration was added. The end-to-end feed test shows a kept member's feed keeps the quest under the target board and a dropped player's feed loses it. |
| 20 | One structured log line per move and nothing stored on the entry (D-19) | VERIFIED | Both services log actor, ids, source, target and counts. No schema change. `OneQuestMove_WritesExactlyOneAuditRecord...` and the event equivalent pass. |
| 21 | One move is one unit of work and a failure leaves the entry on the source board (D-20) | VERIFIED | Each repository makes one `SaveChangesAsync`. It runs after all reads and before nothing else. Failure clears the tracker. `BoardMoveRepositorySingleSaveTests` pins this. InMemory cannot prove a real DB rollback, but a single EF save is transactional. |

**Score:** 21/21 truths verified, 0 present-but-behavior-unverified. Every state-transition truth (waitlist promotion, chain cut, character clear, drop) has a passing test that exercises it.

### Required Artifacts and Key Links

| Artifact | Status | Details |
|----------|--------|---------|
| `MoveController.cs` | VERIFIED | Authorized, four actions, wired to both services and the toast keys |
| `QuestMoveService`, `EventMoveService`, `MoveControlService` | VERIFIED | Registered in Domain `ServiceExtensions`. Used by the controllers |
| `QuestMoveRepository`, `EventMoveRepository` | VERIFIED | Registered in Repository `ServiceExtensions`. One save each |
| `Views/Move/Index(.Mobile).cshtml`, `move-entry(.mobile).css` | VERIFIED (visual unproven) | Substantive and wired. Layout never seen in a browser |
| Details control in the three views | VERIFIED | Enabled link or disabled button with reason, behind `moveControl != null` |
| `QuestMoved.razor`, `QuestMovedEmailJob`, dispatcher methods, `EmailPreviewController` | VERIFIED | Wired, with unit and render tests |
| `_Toasts.cshtml`, `TempDataKeys` | VERIFIED | Toast with link, with a local-URL re-check |
| Key link: controller to service to repository to `SaveChangesAsync` to `FeedRevisionStamper` | WIRED | Writes go through the change tracker. Revision +1 asserted in tests |
| Key link: Details view to Move page to POST to redirect and toast | WIRED | End-to-end tests walk it for quest and event |

Data flow (Level 4): the page values (kept counts, dropped names, seat counts, chain titles) come from the live store reads in `BuildPreviewAsync`. No static or hollow props were found.

### Behavioral Spot-Checks and Test Runs

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| Phase unit tests plus the guard tests | `dotnet test QuestBoard.UnitTests --filter "BoardMove\|AmbientClock\|FeedRevisionWriteSeam\|CrossBoardIgnoreQueryFilters"` | 221 passed, 0 failed | PASS |
| Phase integration tests | `dotnet test QuestBoard.IntegrationTests --filter "BoardMove\|QuestMoved"` | 181 passed, 0 failed | PASS |
| Full suite counts (1084 unit, 1286 integration) | Not re-run. Only the phase-filtered tests were run, per the single-run rule | n/a | Not independently confirmed |

Probe execution: none declared. Skipped.

### Anti-Patterns

Scanned all 78 phase source, view, CSS and test files. No `TBD`, `FIXME`, `XXX`, `TODO` or `HACK`. No planning IDs, phase numbers or planning-doc names in the files (`BoardMoveStaticGuardTests` also passes). No stubs or empty handlers. No blockers.

| File | Finding | Severity |
|------|---------|----------|
| `MoveController.cs:54,109` | WR-01: a repeated confirm after a successful move hits `NotFound` and renders a bare 404. The confirm form has no submit-disable guard. The move itself is correct and the success toast is in TempData | Warning, UX only |
| `QuestMoveRepository.cs:34-37`, `EventMoveRepository.cs:27-32` | WR-02: the repository staleness check compares signup id sets only. The service has already rebuilt the plan from a fresh read in the same request, and the stamp also covers the promoted ids. The remaining window is the gap inside one request. A DM changing selection between page render and confirm does not re-show the page, but the move applies to the current state, not the stale page | Warning, narrow race |
| `MoveStatus.cs` | IN-01: `Ready` is zero | Info |
| `MoveController.cs` | IN-02: an unresolved actor becomes Id 0 and is not rejected. The class is SuperAdmin-only, so this needs a principal that passes the role check but has no user row | Info |
| `BoardMove*Tests.cs` | IN-04: hard-coded 2031 dates will make some integration tests fail in 2031. `BoardMoveEndToEndTests` reads `DateTime.Today` | Info, future test rot |
| Others | IN-03, IN-05, IN-06, IN-07 as in `92-REVIEW.md` | Info |

I agree with the review's severities. Neither warning makes a must-have truth false, so neither is a gap.

### Requirements Coverage (locked decisions)

D-01 to D-21 are all declared by at least one plan and verified above (truths 1 to 21). D-21's twelve required proofs map to existing passing tests: authorization (truth 1), target rules (3, 4), the move happens (5), kept and dropped signups (6, 7), the DM rule (13), waitlist and target-board email (8), series (11), chains (12), eligibility (14), Campaign events (10) and mail (18). `DisposeAsync` resets `ActiveGroupId` to 1 in the move test classes (10 occurrences).

### Human Verification Required

1. **Phone-width layout of the Move page.** Open the Move page at 390px for a quest and an event, with a long target board name and a long title. Expected: no horizontal scroll, no clipped heading, select or confirm button. Why human: no test renders CSS.
2. **Toast and link after a move.** Move a quest as a super admin who is a target member and as one who is not. Expected: redirect to the source board, a toast, a working link only for the member. Why human: layout and the board-switch hop were never seen in a browser.
3. **Email rendering.** Preview all three QuestMoved audiences and the Waitlist Promoted moved line with a long title. Expected: no overflow. Why human: the tests check HTML text only.
4. **Double-press on Confirm (WR-01).** Expected: the final response is not a bare 404. Decide whether to fix with a submit-disable guard (the review's suggestion).

### Gaps Summary

No gaps. The phase goal holds in the code. A super admin can move a quest or event between boards in the same link group from its Details page. The move is a single tracked save, so the feed revision rises. People are reconciled against the target board's membership. Chain links are cut and nothing on the entry points back at the source board. Boards in no set, or in a set of one, offer no control and refuse direct requests. The constraints hold: super-admin only, enforced by the controller policy, the set checked fresh on each request, no feed-file changes, and both `.Mobile.cshtml` twins updated (Events/Details has no twin, as CONTEXT notes).

The status is `human_needed` rather than `passed` because nothing was viewed in a browser and four items need a person. Two non-blocking warnings (WR-01, WR-02) are carried forward from the code review. Bookkeeping notes: plans cite decision IDs that REQUIREMENTS.md does not track, and REQUIREMENTS.md has no Phase 92 entry.

---

_Verified: 2026-10-19_
_Verifier: Claude (gsd-verifier)_
