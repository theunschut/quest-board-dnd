---
phase: 36-campaign-quest-posting-closing
verified: 2026-07-03T15:17:14Z
status: passed
score: 6/6 must-haves verified
overrides_applied: 0
---

# Phase 36: Campaign Quest Posting & Closing Verification Report

**Phase Goal:** A DM running a campaign-type group can post quests and manage their lifecycle (close/reopen) without any of the one-shot scheduling machinery, and campaign quests never trigger scheduling-related emails.
**Verified:** 2026-07-03T15:17:14Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth (ROADMAP Success Criteria) | Status | Evidence |
|---|---------|------------|----------|
| 1 | DM posting a quest in a campaign group sees no proposed-dates picker, and the quest saves successfully without dates | VERIFIED | `Create.cshtml`/`Create.Mobile.cshtml` wrap CR/TotalPlayerCount/DMSession/ProposedDates fields in `@if (boardType != BoardType.Campaign)` (lines 34-83); `QuestController.Create` POST (lines 96-111) resolves `GetActiveBoardTypeAsync` and defaults campaign fields server-side, skipping the ProposedDates validation error. Integration test `Campaign_Create_WithNoProposedDates_Persists` passes (verified via live test run); contrasting `OneShot_Create_WithNoProposedDates_ReRendersWithValidationError` confirms one-shot still requires a date. |
| 2 | A campaign quest's detail/manage page shows no signup or date-voting section — only quest content and a Close/Reopen control | VERIFIED | `Manage.cshtml` wraps CR badge (line 27) and the entire signup/date-voting block (lines 61-537) in `@if (boardType != BoardType.Campaign)`; Close/Reopen buttons (lines 538-561) render only `@if (boardType == BoardType.Campaign)`. `Details.cshtml` mirrors the CR/signup removal (lines 21, 32, 686). Mobile variants (`Manage.Mobile.cshtml`, `Details.Mobile.cshtml`) carry the equivalent conditionals. |
| 3 | DM can close an open campaign quest via a single action, and it immediately disappears from the active quest board | VERIFIED | `QuestController.Close` (line 709) sets `IsClosed=true`/`ClosedDate=DateTime.UtcNow` via `QuestService.CloseQuestAsync` → `QuestRepository.CloseQuestAsync`. Board filters `GetQuestsWithSignupsAsync`/`GetQuestsWithSignupsForRoleAsync` AND `!q.IsClosed` (QuestRepository.cs:61,73). Integration test `Close_OwningDm_RedirectsToManage_AndClosesQuest` passes (redirect to Manage + `IsClosed==true` persisted, verified via live test run). |
| 4 | DM can reopen a closed campaign quest, and it immediately reappears on the active quest board | VERIFIED | `QuestController.Reopen` (line 748) sets `IsClosed=false`/`ClosedDate=null`; same board filters admit the quest again once `!IsClosed` holds. Integration test `Reopen_OwningDm_RedirectsToManage_AndReopensQuest` passes (verified via live test run). |
| 5 | A closed campaign quest appears in the Quest Log right away (no next-day wait), while one-shot finalized quests keep their existing next-day Quest Log behavior unchanged | VERIFIED | `QuestService.GetCompletedQuestsAsync` (QuestService.cs:176-188) ORs in `q.IsClosed && !q.DungeonMasterSession` alongside the existing next-day-wait finalized branch; ordering keys off `q.IsClosed ? q.ClosedDate : q.FinalizedDate`. `QuestLog/Index.cshtml` computes `questDate = (quest.FinalizedDate ?? quest.ClosedDate)` so a real date renders (not "Unknown Date"). Unit tests `GetCompletedQuestsAsync_IncludesClosedCampaignQuest_WithNoNextDayWait`, `GetCompletedQuestsAsync_PreservesOneShotNextDayWait`, `GetCompletedQuestsAsync_OrdersClosedAndFinalizedQuestsTogether_ClosedNotSortedAsNull` all pass (verified via live test run). |
| 6 | No email is sent (posted/reminder/finalized) for any quest action inside a campaign group — verified for post, close, and reopen | VERIFIED | `QuestService.CloseQuestAsync`/`ReopenQuestAsync` (lines 97-106) are thin passthroughs to the repository with zero reference to `dispatcher` (grep-confirmed). `Create` never calls the dispatcher (no `EnqueueFinalizedEmail`/`EnqueuePostedEmail` call site exists in `QuestController` at all). `DailyReminderJob`/`QuestFinalizedEmailJob`/`GetQuestsForTomorrowAllGroupsAsync` filter exclusively on `FinalizedDate.HasValue`, which is never set for campaign quests (only `ClosedDate` is). Unit tests assert `_dispatcher.DidNotReceiveWithAnyArgs().EnqueueFinalizedEmail(...)` for both Close and Reopen (verified via live test run). |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Repository/Entities/QuestEntity.cs` | `IsClosed`/`ClosedDate` columns | VERIFIED | Both properties present, unannotated, mirroring `IsFinalized`/`FinalizedDate` shape |
| `QuestBoard.Domain/Models/QuestBoard/Quest.cs` | `IsClosed`/`ClosedDate` domain fields | VERIFIED | Both properties present |
| `QuestBoard.Repository/Migrations/20260703135517_AddQuestCloseFields.cs` | Reversible additive migration | VERIFIED | `Up()` adds both columns (bit default false, datetime2 nullable); `Down()` drops both cleanly |
| `QuestBoard.Domain/Interfaces/IQuestRepository.cs` + `IQuestService.cs` | `CloseQuestAsync`/`ReopenQuestAsync` signatures | VERIFIED | Both interfaces declare the methods |
| `QuestBoard.Repository/QuestRepository.cs` | Close/Reopen impl + `!IsClosed` board filters | VERIFIED | Implementations set `IsClosed`/`ClosedDate` correctly; both board filters AND `!q.IsClosed` |
| `QuestBoard.Domain/Services/QuestService.cs` | Close/Reopen passthroughs (no dispatcher); `GetCompletedQuestsAsync` OR-branch | VERIFIED | Zero `dispatcher` reference in Close/Reopen; OR-branch includes `&& !q.DungeonMasterSession` (WR-01 fix applied) |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` | Close/Reopen actions, `GetActiveBoardTypeAsync`, Create/Edit sanitization, `ViewBag.BoardType` | VERIFIED | All present; CR-01 (server-side BoardType guard on Close/Reopen) and CR-02 (Edit sanitization) fixes confirmed in current source |
| `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` | Guards admit `IsClosed`; `ViewBag.BoardType` | VERIFIED | `Details`/`UpdateRecap` guards admit closed campaign quests; WR-02 fix (DungeonMasterSession exclusion) present in both |
| `QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs` | `ProposedDates` relaxed | VERIFIED (not directly re-read this pass, confirmed via controller/view behavior and passing tests) | Empty-list default confirmed functionally via Create tests |
| `QuestBoard.Service/Views/Quest/*.cshtml` (+ Mobile) | Conditional BoardType rendering | VERIFIED | Index/Manage/Details/Create desktop + mobile all confirmed with correct conditional wraps |
| `QuestBoard.Service/Views/QuestLog/*.cshtml` (+ Mobile) | Conditional BoardType rendering, ClosedDate fallback | VERIFIED | Index/Details desktop + mobile confirmed |
| `QuestBoard.IntegrationTests/Controllers/QuestCloseTests.cs` | Close/Reopen + campaign Create integration tests | VERIFIED | 5 tests, all pass on live run; substantive assertions (DB re-read, status codes, persisted state) |
| `QuestBoard.UnitTests/Services/QuestServiceTests.cs` | Close/Reopen + GetCompletedQuestsAsync unit tests | VERIFIED | 9 relevant tests (5 new + regression), all pass on live run |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `QuestController.Close/Reopen` | `QuestService.CloseQuestAsync/ReopenQuestAsync` | Authorized POST action | WIRED | Confirmed by source read + passing integration tests |
| `QuestService.CloseQuestAsync/ReopenQuestAsync` | `IQuestRepository.CloseQuestAsync/ReopenQuestAsync` | Thin passthrough | WIRED | No dispatcher reference; confirmed by source read |
| `QuestController.Create/Edit` | `GetActiveBoardTypeAsync` | Server-side BoardType resolution | WIRED | Confirmed in both actions; `BoardType` never bound from posted form (no `BoardType` property on `QuestViewModel`/`EditQuestViewModel`) |
| `QuestController` render actions | `ViewBag.BoardType` | Single lookup per action | WIRED | Index/Create/Details/Manage all set it; QuestLogController.Index/Details also set it |
| `QuestLogController.Details/UpdateRecap` | `Quest.IsClosed` | OR-branch guard | WIRED | Confirmed; WR-02 fix aligns `UpdateRecap`'s check with `Details` |
| `GetCompletedQuestsAsync` predicate | `Quest.IsClosed` | OR-branch, DM-session-excluded | WIRED | WR-01 fix confirmed (`|| (q.IsClosed && !q.DungeonMasterSession)`) |
| `Manage.cshtml`/`Manage.Mobile.cshtml` Close/Reopen forms | `QuestController.Close/Reopen` | `asp-action` POST + `@Html.AntiForgeryToken()` | WIRED | Confirmed in both desktop and mobile views |
| `DailyReminderJob`/`QuestFinalizedEmailJob` | `Quest.FinalizedDate` | Structural exclusion of campaign quests | WIRED (by omission) | `GetQuestsForTomorrowAllGroupsAsync` filters exclusively on `FinalizedDate.HasValue`, which campaign quests never set |

### Behavioral Spot-Checks / Live Verification

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full solution builds clean | `dotnet build` | 6 projects, 0 errors, 0 warnings | PASS |
| Unit tests (Close/Reopen/GetCompletedQuestsAsync) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestServiceTests"` | 9/9 passed | PASS |
| Integration tests (Close/Reopen/Create authz/CSRF) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestCloseTests"` | 5/5 passed | PASS |
| Full test suite (regression check) | `dotnet test` | 123/123 unit + 241/241 integration passed | PASS (better than the 240/241 documented in 36-03-SUMMARY.md — the previously-flaky `AdminControllerIntegrationTests` test passed on this run, confirming it is not a phase-36 regression) |
| No GSD phase/requirement ID leaks in modified source | grep across controllers/services/views/tests | No matches | PASS |
| No debt markers (TBD/FIXME/XXX/TODO/HACK) in modified files | grep across controllers/services/views | No real matches (only legitimate HTML `placeholder` attrs and pre-existing CSS class names) | PASS |

### Code Review Fix Verification (36-REVIEW.md / 36-REVIEW-FIX.md)

All 7 findings from the code review (3 critical, 4 warning) were independently re-verified against the current source, not just trusted from 36-REVIEW-FIX.md's claims:

| Finding | Description | Verified Fix Present |
|---------|-------------|----------------------|
| CR-01 | Close/Reopen never verify BoardType server-side | CONFIRMED — `GetActiveBoardTypeAsync()` check + `400 BadRequest` guard present in both `Close` (line 720) and `Reopen` (line 759) |
| CR-02 | Edit POST doesn't sanitize DungeonMasterSession/CR/TotalPlayerCount for Campaign | CONFIRMED — sanitization block present in `Edit` POST (lines 237-244), mirrors Create |
| CR-03 | `CreateFollowUpQuestAsync` never sets `GroupId` | CONFIRMED — `GroupId = original.GroupId` present in the follow-up `Quest` constructor |
| WR-01 | `GetCompletedQuestsAsync` closed-branch bypasses DM-session filter | CONFIRMED — `q.IsClosed && !q.DungeonMasterSession` in the OR-branch |
| WR-02 | `UpdateRecap`'s completed-quest check diverges from `Details` | CONFIRMED — both now compute `isCompletedOneShot` with `&& !quest.DungeonMasterSession` |
| WR-03 | Broad `"unique"` substring match in `AddAsync` exception filter | CONFIRMED — generic fallback removed, only specific `IX_Quests_OriginalQuestId` check remains |
| WR-04 | DM comparison by Name instead of Id in Index views | CONFIRMED — `Index.cshtml`/`Index.Mobile.cshtml` now compare `currentUserId.Value == quest.DungeonMaster?.Id` |

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|-------------|-----------------|-------------|--------|----------|
| CQUEST-01 | 36-03, 36-04 | DM can post a quest for a campaign group without selecting proposed dates | SATISFIED | Create view field hiding + controller validation/defaulting + passing integration test |
| CQUEST-02 | 36-04 | Campaign quest pages show no per-quest signup or date-voting UI | SATISFIED | Manage/Details CR-badge + signup/date-voting sections wrapped and hidden for campaign |
| CQUEST-03 | 36-01, 36-02, 36-03, 36-04 | DM can close a campaign quest, hiding it from the active board | SATISFIED | Close action + `!IsClosed` board filter + passing test |
| CQUEST-04 | 36-01, 36-02, 36-03, 36-04 | DM can reopen a closed campaign quest | SATISFIED | Reopen action + board filter re-admission + passing test |
| CQUEST-05 | 36-01, 36-02, 36-03, 36-05 | Party can browse closed campaign quests in the Quest Log immediately | SATISFIED | `GetCompletedQuestsAsync` OR-branch + ClosedDate fallback + passing unit tests |
| CQUEST-06 | 36-01, 36-02, 36-03 | No quest-related emails sent for campaign-group quests | SATISFIED | Structural absence of dispatcher calls in Close/Reopen; FinalizedDate-gated email jobs never see campaign quests |

No orphaned requirements — all 6 IDs mapped to Phase 36 in REQUIREMENTS.md are claimed by at least one plan's frontmatter and independently verified against source.

### Anti-Patterns Found

None. No debt markers (TBD/FIXME/XXX), no unresolved TODO/HACK/PLACEHOLDER markers, no empty stub implementations, and no GSD phase/requirement ID leakage found in any file modified by this phase (including the review-fix commits).

### Human Verification Required

None outstanding. Both `checkpoint:human-verify` gates in this phase (36-04 Task 3: board/Manage/Details/Create rendering; 36-05 Task 2: Quest Log entry + recap flow) were executed and approved during the phase itself, per 36-04-SUMMARY.md ("Human-verify checkpoint approved by user after the wax-seal fix; all 9 verification steps confirmed on desktop, mobile, and one-shot regression check") and 36-05-SUMMARY.md ("Human-verify checkpoint approved on first pass"). No new human-facing behavior was introduced by the subsequent code-review fix commits that would require re-verification — all 7 fixes are server-side validation/logic changes (BoardType guards, sanitization, filter predicates, exception-filter narrowing, Id-based comparison) with no visual/UX change.

### Gaps Summary

No gaps. All 6 ROADMAP success criteria are verified against live, currently-building, currently-passing code — not just SUMMARY.md narrative. All 7 code-review findings (3 critical, 4 warning) were independently re-confirmed present in the current source rather than trusted from 36-REVIEW-FIX.md's claims. Full test suite (364 tests: 123 unit + 241 integration) passes with zero failures, including the previously-documented flaky test which passed on this verification run.

---

_Verified: 2026-07-03T15:17:14Z_
_Verifier: Claude (gsd-verifier)_
