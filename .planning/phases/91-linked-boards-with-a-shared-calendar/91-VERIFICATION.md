---
phase: 91-linked-boards-with-a-shared-calendar
verified: 2026-10-16T00:00:00Z
status: passed
score: 11/11 must-haves verified
behavior_unverified: 0
overrides_applied: 0
re_verification: false
next_action: "Phase goal achieved. Proceed to ship / milestone flow. Optionally schedule a small hardening follow-up for WR-01 and WR-02 (advisory, not blocking) and a physical-phone glance when convenient."
requirements_in_plans: none
gaps: []
deferred: []
advisory:
  - id: WR-01
    severity: warning
    blocks_goal: false
    summary: "CreateAsync/RenameAsync catch every DbUpdateException and report it as a duplicate set name; RenameAsync recovery uses FirstAsync and can throw if the set was deleted in between."
  - id: WR-02
    severity: warning
    blocks_goal: false
    summary: "AddBoard/RemoveBoard are check-then-write with an unconditional overwrite; two concurrent super admins can move a board between sets without the loser being told, and a set deleted between read and write surfaces as a 500 instead of NotFound."
  - id: D-20-unlinked-change
    severity: note
    blocks_goal: false
    summary: "Unlinked boards change in exactly one documented way: players no longer see DM-only sessions on /calendar and the Quest Details calendar. This is locked decision D-20 (a deliberate fix, covered by CalendarDungeonMasterSessionTests), not a regression."
---

# Phase 91: Linked Boards with a Shared Calendar - Verification Report

**Phase Goal:** A super admin can link two or more boards of the same board type in the Platform area. On every linked board, `/calendar` then shows the events and quests of all boards in the link as well as its own, each marked with the board it belongs to, so a campaign split across boards for the same group of players never gets a date booked twice. Unlinking restores each board to its own calendar. Boards that are not linked behave exactly as they do today.
**Verified:** 2026-10-16
**Status:** passed
**Re-verification:** No, initial verification
**Branch/HEAD:** milestone/v9-rolling-improvements

## Goal Achievement

### Observable Truths

Roadmap has no itemised Success Criteria for phase 91, so the must-haves are the goal sentence split into testable clauses (Option C), plus the locked decisions in 91-CONTEXT that the plan frontmatter carries.

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A super admin can create a set and add/remove/rename/delete boards in the Platform area; nobody else can | VERIFIED | `QuestBoard.Service/Areas/Platform/Controllers/LinkedBoardsController.cs` has `[Authorize(Policy = "SuperAdminOnly")]` on the class and `[ValidateAntiForgeryToken]` on all five POSTs. Linked from `Group/Index.cshtml` and `Group/Index.Mobile.cshtml`. `LinkedBoardsPlatformIntegrationTests.Page_WhenNotSuperAdmin_DeniesGetAndPostAndLeavesTheBoardUnchanged` passes. Desktop and mobile views exist (`LinkedBoards/Index.cshtml`, `Index.Mobile.cshtml`). |
| 2 | Same-board-type is enforced on the server path, including for an empty set; a board is in at most one set | VERIFIED | `LinkedBoardSetService.AddBoardAsync` returns `TypeMismatch` when `board.BoardType != set.BoardType` and `AlreadyLinked` when `board.LinkedBoardSetId != null`. Set type is stored on `LinkedBoardSetEntity.BoardType`, so an empty set still refuses the wrong type. At-most-one is structural: a single nullable `Groups.LinkedBoardSetId`. Tests `AddBoard_WhenCampaignBoardPostedIntoOneShotSet_IsRefused...`, `AddBoard_WhenOneShotBoardPostedIntoEmptyCampaignSet_IsRefused`, `AddBoard_WhenBoardIsAlreadyInAnotherSet_IsRefused...` pass against direct POSTs. |
| 3 | On a linked board, `/calendar` (desktop and mobile) shows the other boards' events and quests as well as its own, each marked with its board | VERIFIED | `CalendarController.Index` calls `ILinkedCalendarService.GetForViewerAsync` and fills `LinkedEvents/LinkedQuests/LinkedBoards`. `CalendarViewModel.GetCalendarDays` builds `CalendarDay.Linked` per day. Desktop renders `_LinkedDayEntries` from `_Calendar.cshtml` (board name in tooltip, detail line and visually-hidden text). Mobile `Index.Mobile.cshtml` renders `agenda-linked-entry` rows with board name as text and includes link-only days in `agendaDays`. Tests `Calendar_BoardLinkedToAnother_ShowsTheLinkedBoardsEntryAndNeverTheOutsideBoards`, `Calendar_ActiveOnTheLinkedBoard_ShowsTheFirstBoardsEntryAndNotTheOutsideBoard`, `Calendar_DayWithOnlyALinkedEntry_...`, and the mobile suite pass. Operator confirmed on real SQL Server and in a browser (91-10-SUMMARY). |
| 4 | The Quest Details date-vote calendar (desktop and mobile) also shows linked entries, read-only, so a clash is visible while choosing dates | VERIFIED | `QuestController.Details` loads one linked window across the shown months. `_Calendar.cshtml` shows the "Also showing ... (read-only)" note and `_LinkedDayEntries`; `_Calendar.Mobile.cshtml` renders `_LinkedAlsoThatDay` under each proposed date. Both partials take their own `LinkedDayEntries` model and contain no form control. `LinkedBoardQuestDetailsTests.Details_Desktop_LinkedBlocksCarryNoFormControlOrVoteName` and `DetailsMobile_LinesCarryNoFormControlIdOrForAttributeAndAddNoVoteInput` pass. The board's own events stay off Details (`Details_Desktop_KeepsTheBoardsOwnEventOffThePageWhileLinkedEventsShow`). |
| 5 | Unlinking restores each board to its own calendar, effective on the next request | VERIFIED | The set and viewer roles are read fresh per request in `LinkedBoardSetRepository.GetLinkedBoardsForViewerAsync` and nothing is cached. `Calendar_AfterABoardIsRemovedFromTheSet_NextRequestShowsNothingFromIt` and `LinkedBoardEndToEndTests` (RemoveBoard then four GETs show nothing linked) pass. Operator also confirmed unlink in the browser. Deleting a set un-links its boards (`DeleteAsync` plus `SetNull` FK; `DeleteSet_UnlinksEveryBoardAndRemovesTheSetWithoutDeletingBoards`). Deleting a board needs no handling: the link is on its own row (`DeletingALinkedBoard_LeavesTheSetAndItsOtherBoardsIntact`). |
| 6 | Boards that are not linked behave as today | VERIFIED | A board in no set or a set of one gets `ViewerLinkedBoards.Empty`, so `LinkedCalendarData.Empty` and no linked markup (`Calendar_BoardInNoSet_...`, `Calendar_BoardAloneInItsSet_...`, `Legend_BoardInNoSet_...`, `Details_Desktop_BoardInNoSet_...`, `DetailsMobile_BoardInNoSet_...`). The new view-model collections default empty. One intentional, documented change: players no longer see DM-only sessions on the calendar (locked D-20; see advisory below). The own-board reads are otherwise untouched. |
| 7 | The cross-board read is tenant-safe: narrowed to linked ids, bypass confined to the allowlist, fails closed, pure read | VERIFIED | `EventRepository.GetLinkedEventsForCalendarAsync` and `QuestRepository.GetLinkedQuestsForCalendarAsync` use `IgnoreQueryFilters()` with `linkedGroupIds.Contains(GroupId)` in the same predicate and short-circuit on an empty list. Both files are already on `CrossBoardIgnoreQueryFiltersSeamTests` allowlist (no new file added); the guard test passes. `LinkedCalendarService` re-checks every row's `GroupId` against the linked set, logs and drops strays. Ids come from a server-side lookup, not request input. No write to `Events`/`Quests`. |
| 8 | A linked entry carries only title, time, board name (and DM name for quests): no roster, votes or signup state | VERIFIED | Both linked reads are flat `Select` projections (`LinkedEventEntry`, `LinkedQuestEntry`); no Include of signups, votes or players, and `ProjectForCalendar` is not used. `Calendar_LinkedQuestWithSignupsAndVotes_NeverShowsAnotherBoardsRosterOrPollAnswers` and `Calendar_LinkedQuestChip_CarriesTheDmNameAndNoQuestDataAttributes` pass. |
| 9 | Member of the owning board gets a link that goes through the Phase 87 board switch; a non-member gets plain text | VERIFIED | `ViewerIsMember` is derived from a fresh `UserGroups` lookup (`GetLinkedBoardsForViewerAsync`), with no SuperAdmin branch. `_LinkedDayEntries` emits `<a href=Details>` for members and a link-free, tab-stop-free `div` for others. `Calendar_MemberOfTheLinkedBoard_GetsOneAnchorToItsOrdinaryDetailsPage`, `Calendar_NonMemberChip_CarriesNoLinkTabStopRoleOrDataAttributes`, `LinkedBoardDeepLinkTests` pass. Operator clicked a chip and saw the board switch and banner. |
| 10 | Placement and inclusion rules match the board's own: finalized on its day, open on every proposed date, cancelled events struck through, DM-only by role on the owning board, Campaign sets events-only | VERIFIED | `CalendarViewModel.GetLinkedQuestsForDate` mirrors `GetQuestsForDate`. `GetLinkedEventsForDate` includes cancelled events flagged; CSS `.linked-entry--cancelled`, `.also-that-day--cancelled` and `.agenda-linked-entry--cancelled` apply `line-through`. `LinkedCalendarService.GetVisibleQuestsAsync` uses the viewer's role on the owning board (`ViewerRole is DungeonMaster or Admin`, SuperAdmin counts as Admin) and the Campaign branch never fetches quests. Tests: `Calendar_LinkedFinalizedQuest_...`, `Calendar_LinkedOpenQuest_...`, `Calendar_CancelledLinkedEvent_...`, the four `Calendar_LinkedDmOnlySession_*` role cases, `Calendar_CampaignSet_ShowsLinkedEventsButNeverLinkedQuests`, plus `CalendarDungeonMasterSessionTests` pass. |
| 11 | Schema ships as a migration; D-13 surfaces untouched | VERIFIED | `20261016061911_AddLinkedBoardSets` adds `Groups.LinkedBoardSetId`, `LinkedBoardSets` (unique name index) and the FK with `SetNull`; snapshot updated; operator confirmed it applied on SQL Server with FK delete action SET_NULL. `git diff` of the feed writer, subscription service, `FeedRevisionStamper`, agenda, events controller/views and series service against the pre-phase commit is empty (91-10-SUMMARY D-13 check; the changed-file list from `git diff f8ce15b8..HEAD` contains none of them). |

**Score:** 11/11 truths verified (0 behavior-unverified)

### Required Artifacts

| Artifact | Status | Details |
|----------|--------|---------|
| `QuestBoard.Repository/Entities/LinkedBoardSetEntity.cs`, `GroupEntity.cs` link column, `QuestBoardContext.cs` config | VERIFIED | Present, substantive, unique name index, SetNull FK |
| `Migrations/20261016061911_AddLinkedBoardSets` + snapshot | VERIFIED | Matches the entity config |
| `LinkedBoardSetRepository.cs`, `LinkedBoardSetService.cs`, `LinkedCalendarService.cs` | VERIFIED | Registered in both `ServiceExtensions.cs`; consumed by controllers |
| `EventRepository.GetLinkedEventsForCalendarAsync`, `QuestRepository.GetLinkedQuestsForCalendarAsync` | VERIFIED | Pinned, narrow projection |
| `LinkedBoardsController` + `LinkedBoards/Index(.Mobile).cshtml` | VERIFIED | SuperAdminOnly, antiforgery, mobile twin |
| `_LinkedDayEntries.cshtml`, `_LinkedAlsoThatDay.cshtml`, `CalendarViewModel` linked members | VERIFIED | Wired into `_Calendar`, `_Calendar.Mobile`, `Calendar/Index.Mobile` |
| `calendar.css`, `calendar.mobile.css`, `quests.mobile.css`, `platform-linked-boards.mobile.css` | VERIFIED | Linked, cancelled, count-badge and Remove Board `flex-shrink`/`nowrap` rules present |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| `CalendarController.Index` | `ILinkedCalendarService` | WIRED | Result assigned to `LinkedEvents/LinkedQuests/LinkedBoards` |
| `QuestController.Details` | `ILinkedCalendarService` | WIRED | One window across the shown months; result copied onto each month model |
| `LinkedCalendarService` | repositories | WIRED | Set lookup, then event and quest reads with the set's ids |
| `GetCalendarDays` | `CalendarDay.Linked` | WIRED | Rendered by the desktop partial and mobile agenda |
| Linked chip (member) | `CrossBoardDeepLinkMiddleware` | WIRED | Ordinary `/Quest/Details/{id}` and `/Events/Details/{id}` URLs; verified by `LinkedBoardDeepLinkTests` and the operator |
| Platform Group index | Linked Boards page | WIRED | Header button on both layouts |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real data | Status |
|----------|------|--------|-----------|--------|
| Desktop and mobile calendar linked entries | `LinkedEvents`, `LinkedQuests` | EF queries on `Events` and `Quests` bypassing the filter, pinned to the set | Yes (operator saw real chips on SQL Server) | FLOWING |
| Legend, "Also showing" | `LinkedBoards` | `GetLinkedBoardsForViewerAsync` on `Groups` | Yes | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Linked unit, seam and ambient-clock guards | `dotnet test QuestBoard.UnitTests --filter "Linked\|CrossBoardIgnoreQueryFilters\|AmbientClock"` | 135 passed, 0 failed | PASS |
| Linked integration suites and DM-only session tests | `dotnet test QuestBoard.IntegrationTests --filter "Linked\|CalendarDungeonMasterSession"` | 136 passed, 0 failed | PASS |
| Full suite (not re-run; relied on orchestrator) | unit 916 passed; integration 1088 passed, 11 skipped (pre-existing SQL Server migrator tests) | as reported | Accepted |

### Probe Execution

Step 7c: SKIPPED. No probe scripts are declared by the phase plans.

### Requirements Coverage

All ten PLAN files carry `requirements: []`, and ROADMAP lists "Requirements: TBD". There are no requirement IDs to cross-reference against REQUIREMENTS.md and no orphaned requirements are mapped to phase 91. Not applicable.

### Anti-Patterns Found

Scanned every source and test file changed since `f8ce15b8`.

| Check | Result |
|-------|--------|
| TBD / FIXME / XXX | None |
| TODO / HACK / PLACEHOLDER | None |
| Planning IDs (D-xx, phase/plan numbers, planning doc names) in source comments | None. The only regex hit is `LinkedBoardStaticGuardTests`, which is the guard that enforces this rule. |
| Stubs / hollow props | None. Linked collections default empty by design and are populated by both controllers. |

### Assessment of the supplied review warnings

- **WR-01 (broad `DbUpdateException` catch):** does not block the goal. It sits on a SuperAdmin-only write path and affects only error wording under a rare DB fault. The unique index still rejects the duplicate, and the pre-check catches ordinary duplicates (tested). The `FirstAsync` recovery can throw only if the set is deleted in the same instant as a failed rename. Recommended as a follow-up hardening item: narrow the catch to SQL 2601/2627, or re-run `NameExistsAsync`.
- **WR-02 (check-then-write race on AddBoard/RemoveBoard):** does not block the goal. A board is always in at most one set (single column), and the calendar read also re-verifies that the set's type equals the active board's type (`setType != active.BoardType` returns empty), so a race cannot produce a leak or a mixed-type calendar. The worst case is a silent last-writer-wins move between two sets by two concurrent super admins, or a 500 for a set deleted mid-request. Recommended follow-up: conditional `ExecuteUpdate` on `Groups` (permitted; the change-tracker rule covers only `Events` and `Quests`) with expected current set id.
- Info items IN-01 to IN-04: cosmetic or defensive; IN-04 (SQL translation unproven by InMemory) is closed by the operator's SQL Server run recorded in 91-10-SUMMARY.

### Human Verification Decision

The three items that 91-10-SUMMARY lists as not human-verified do not warrant `human_needed`:

1. **Event chips and cancelled styling.** The event and quest paths share the same chip markup and CSS pattern, and the quest chips were seen in the browser. Cancelled events render through three one-rule `line-through` classes. Integration tests assert the class and the visually-hidden "Cancelled." text on desktop, mobile agenda and mobile Details (`Calendar_CancelledLinkedEvent_IsShownAndMarkedCancelled`, `DetailsMobile_CancelledLinkedEvent_IsStruckThroughAndAnnounced`). The event query itself ran on SQL Server without error.
2. **Legend and "Also showing" with 4+ boards.** Pure iteration over `LinkedBoards`; `Legend_WithTwoLinkedBoards_ListsBothNamesAlphabetically` covers the multi-board loop and the operator saw the 3-board case. `legend-board-name` and the agenda note have `overflow-wrap: anywhere` / wrapping, so a longer list wraps instead of overflowing.
3. **Physical phone.** The 390x844 emulated Android UA drove the same server-side `.Mobile.cshtml` selection and the same CSS; no horizontal scroll and 44px targets were measured. The residual risk is touch feel, which is a taste item and not a goal condition.

Each is low-risk and backed by automated assertions plus adjacent human evidence. They stay visible in this report as optional smoke checks for the operator, not as blocking items.

### Gaps Summary

No gaps. Every clause of the phase goal is implemented, wired, covered by passing automated tests and, for the SQL Server, desktop, mobile, deep-link, unlink and Platform page behaviors, confirmed by the operator. The one change to unlinked boards (DM-only sessions hidden from players on the calendar) is the locked decision D-20, tested, and called out in 91-CONTEXT as deliberate.

---

_Verified: 2026-10-16_
_Verifier: Claude (gsd-verifier)_
