---
phase: 85-one-shot-quests-in-the-calendar-feed
reviewed: 2026-09-18T00:00:00Z
depth: standard
files_reviewed: 13
files_reviewed_list:
  - QuestBoard.Domain/Enums/CalendarFeedSource.cs
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Interfaces/IQuestRepository.cs
  - QuestBoard.Domain/Models/CalendarFeedEntry.cs
  - QuestBoard.Domain/Models/CalendarFeedOptions.cs
  - QuestBoard.Domain/Services/CalendarFeedWriter.cs
  - QuestBoard.Domain/Services/CalendarSubscriptionService.cs
  - QuestBoard.Repository/QuestRepository.cs
  - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs
  - QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs
  - QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs
  - QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs
findings:
  critical: 0
  warning: 2
  info: 2
  total: 4
status: issues_found
---

# Phase 85: Code Review Report

**Reviewed:** 2026-09-18
**Depth:** standard
**Files Reviewed:** 13
**Status:** issues_found

## Summary

This phase adds one-shot quest sessions as a second source to the existing anonymous, token-addressed ICS calendar feed. The repository read (`QuestRepository.GetFeedQuestsForUserAsync`) is correctly rooted at `Quests` (structurally preventing the duplicate-`UID` hazard the phase's own design calls out as load-bearing), correctly narrows on both membership and board type via a caller-derived `oneShotGroupIds` set, and correctly implements the seat-or-Dungeon-Master disjunction (`IsSelected == true` seat, or `DungeonMasterId == userId`) with no role branch, matching D-01 through D-04. The writer changes (`Duration`-driven `AppendTimedEvent`, `Source`-gated `BuildSummary`) correctly close the `VoteType.No`-default landmine that a naive port would have hit — a quest entry that never sets `Availability` would otherwise silently render `(declined)` on every subscriber's phone, and the fix (gate on `Source` before ever reading `Availability`) is in place and tested. The merge/order step, the second-layer re-check, the `QuestDurationHours` validation, the `CalendarFeedSource.Quest` UID namespacing, and the events-only-feed byte-identical guarantee are all implemented as specified and are backed by a substantial, well-targeted test suite (integration facts seed two boards per scoping claim rather than relying on absence alone, and the new unit suite reaches the drop-and-log branch a healthy filtered repository structurally cannot trigger).

I found no Critical/security issues — the scoping predicates are correct as shipped, and every cross-board/board-type leak scenario I traced is closed by either the primary query or the second-layer re-check. I found two Warnings worth fixing or at least tracking, both about the *completeness* of the defensive re-check rather than about current correctness, plus two minor Info-level observations.

No stray planning/tracking IDs (`D-`, `QUESTFEED-`, `CALFEED-`, `Phase 8x`, `85-0x`) were found in any production source file, in compliance with `CLAUDE.md`. No EF packages were added outside `QuestBoard.Repository`. The two items already known and recorded (the Linux apphost static-guard collision, and the deferred relational-translation verification) are not repeated here.

## Warnings

### WR-01: The second-layer re-check validates board scope only — it cannot catch a regression in the seat-or-Dungeon-Master predicate

**File:** `QuestBoard.Domain/Services/CalendarSubscriptionService.cs:126-134`
**Issue:** The quest branch's defensive re-check is:
```csharp
var checkedQuests = fetchedQuests.Where(q => oneShotGroupIds.Contains(q.GroupId)).ToList();
```
This re-verifies that every returned row's `GroupId` is inside the caller's own one-shot board set — genuinely defensive against a lost or mistranslated *board* predicate, and it mirrors the existing event branch faithfully. But it does **not** re-verify the other independent condition the design locks down: that the reader actually holds a confirmed seat on the quest, or is its Dungeon Master. `Quest` (the mapped domain model returned by the repository) does carry a `PlayerSignups` collection, but the repository query never `.Include()`s it, so it is always empty at this point and cannot be used to re-check seat ownership without adding a load the design deliberately avoids (mirroring `EventSignupRepository`'s own "no roster reaches an entry" comment).

The practical consequence: if a future change to `QuestRepository.GetFeedQuestsForUserAsync` ever drops or weakens the `q.PlayerSignups.Any(...) || q.DungeonMasterId == userId` clause — for example during a refactor that keeps the board-containment filter intact but loosens the seat clause — every finalized quest on every one-shot board the reader is a member of would reach their feed, silently, with **zero** error logged. This is precisely the design's own named constraint ("Only quests the reader is signed up for. Not every quest on their one-shot boards.") failing exactly the way it isn't supposed to be able to fail, and the re-check that exists specifically "because a feed is read by a machine, so a leak has no reader to notice it" would not fire for this failure mode.

This is not a defect in the code as shipped today — the predicate is correct — and the gap is inherited from the same shape the pre-existing event branch already accepts (that re-check also only covers board membership, not "does this signup row belong to this user"). It is flagged here because the review was specifically asked to evaluate whether this re-check is genuinely defensive or can mask a bug, and the honest answer is: defensive for one of the two independent predicates, silent for the other.

**Fix:** At minimum, extend the doc comment on `GetFeedQuestsForUserAsync` and the re-check itself to state explicitly which predicate the re-check does and does not cover, so a future maintainer doesn't read "second-layer re-check passed, no errors logged" as proof that seat/DM scoping is intact. If the cost is acceptable, consider a lightweight re-check that doesn't require loading signups — e.g., re-deriving `checkedQuests` against a repository-supplied flag on each row (the repository already knows which branch matched; a boolean or enum discriminator returned alongside each `Quest` would let the second layer assert `HasSeatOrIsDm == true` without an extra query).

### WR-02: `oneShotGroupIds` derivation depends on `GroupWithMemberCount.BoardType` staying correct at the moment of the request; no defensive re-check independently re-derives board type

**File:** `QuestBoard.Domain/Services/CalendarSubscriptionService.cs:71-76`
**Issue:** `oneShotGroupIds` is computed once, in memory, from the same `memberships` list already used for `boardNamesById`:
```csharp
var oneShotGroupIds = memberships.Where(m => m.BoardType == BoardType.OneShot).Select(m => m.Id).ToList();
```
This is exactly the pattern the phase's context calls for ("This single derived set encodes membership and board type at once... there is no way to satisfy one predicate without the other"), and the accompanying comment explaining why is accurate. The finding here is narrower than WR-01: because `oneShotGroupIds` is the *only* signal both the query and the re-check use for board-type narrowing, a bug in `GroupService.GetGroupsForUserAsync`/`GroupWithMemberCount` mapping (e.g., `BoardType` populated from a stale or cached value) would propagate identically into both the query and the re-check, and the re-check — being derived from the same source, not an independent one — would never catch it. This mirrors WR-01's shape (a re-check that shares its blind spot with the thing it's checking) but is lower likelihood since `GetGroupsForUserAsync` is a simple, already-tested read.

**Fix:** No code change required. Worth a short note alongside the existing comment clarifying that the re-check's guarantee is scoped to "the query didn't diverge from the board-id set it was handed," not "the board-id set itself is correct" — the same caveat that applies to the membership re-check inherited from Phase 84.

## Info

### IN-01: The `Source` merge-order tiebreak (event before quest at an identical date and time) has no dedicated test

**File:** `QuestBoard.Domain/Services/CalendarSubscriptionService.cs:143-149`, `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs:980-1047`
**Issue:** The merge order is `OrderBy(Date).ThenBy(StartTime).ThenBy(Source).ThenBy(SourceId)`, and the comment explicitly states the intent ("Source is the third key so an event precedes a quest at an identical date and time"). `Feed_MergedDocument_OrdersEventsAndQuestsByDateThenStartTimeRegardlessOfSource` proves interleaving by time (a 19:00 quest before a 20:00 event on the same day), but no seeded pair shares an identical `Date` and `StartTime` across the two sources, so the `Source` tiebreak clause itself is never exercised — a regression that removed or reversed `.ThenBy(e => e.Source)` would not be caught by any test in this suite.
**Fix:** Add one fact (or extend the existing ordering fact) with a quest and an event sharing the same date and start time, asserting the event's `SUMMARY` line precedes the quest's.

### IN-02: `DTSTAMP` for a quest entry never changes across a reschedule, relying entirely on `Feed_RescheduledQuest_...`'s UID-stability assertion rather than an explicit DTSTAMP assertion

**File:** `QuestBoard.Domain/Services/CalendarSubscriptionService.cs:135` (`CreatedAt = q.CreatedAt`), `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs:641-699`
**Issue:** This is a correct implementation of the phase's inherited Assumption A5 (`SEQUENCE:0` stays constant; a plain-`PUBLISH` feed relies on `UID` refetch, not `DTSTAMP`/`SEQUENCE` comparison, to update in place) — not a bug. Noted only because A5 is explicitly named in `85-CONTEXT.md` as "the research's own least-certain claim," and the reschedule fact in this file proves `UID` stability but does not assert anything about `DTSTAMP` before/after the move. If A5 is ever re-tested per the context's own instruction ("if a stale-entry report ever surfaces, it is the first thing to re-test, for both sources at once"), this file is where that second source's half of the re-test belongs.
**Fix:** No action needed now; flagged for whoever eventually re-tests A5.

---

_Reviewed: 2026-09-18_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
