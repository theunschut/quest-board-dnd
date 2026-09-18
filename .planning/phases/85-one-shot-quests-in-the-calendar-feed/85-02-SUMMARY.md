---
phase: 85-one-shot-quests-in-the-calendar-feed
plan: 02
subsystem: api
tags: [ef-core, ical, rfc5545, calendar-feed, aspnet-core, quest-board]

requires:
  - phase: 84-calendar-feed-foundation-and-event-subscription
    provides: CalendarSubscriptionService.GetFeedAsync pipeline, CalendarFeedWriter, CalendarFeedOptions, CalendarFeedSource enum, the pinned-membership tenant-safety pattern
provides:
  - "GetFeedQuestsForUserAsync on IQuestRepository/QuestRepository: a single Quests-rooted, IgnoreQueryFilters()-bypassed query combining one-shot board-id containment with a seat-or-Dungeon-Master disjunction inside one Where"
  - "CalendarFeedSource.Quest enum member with BuildUid namespacing verified at a colliding numeric id"
  - "CalendarFeedEntry.Duration (TimeSpan, default 1h) and CalendarFeedOptions.QuestDurationHours (int, default 4) with a refuse-to-start validation clause"
  - "CalendarFeedWriter.AppendTimedEvent driven by entry.Duration instead of a literal hour; BuildSummary gated on Source before consulting Availability"
  - "CalendarSubscriptionService.GetFeedAsync's quest branch: oneShotGroupIds derivation, second-layer re-check, projection, and a merged/ordered document with events"
affects: [85-03-one-shot-quests-in-the-calendar-feed, 85-04-one-shot-quests-in-the-calendar-feed, 85-05-one-shot-quests-in-the-calendar-feed]

actuals:
  tokens: 8800
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Quests-rooted Any()-in-Where disjunction (mirrors GroupRepository.GetGroupsForUserAsync) instead of a literal Union+Distinct — structurally prevents a duplicate identifier rather than removing one after the fact"
    - "Source-gated writer branching (entry.Source == CalendarFeedSource.Event) evaluated before a shared field with a landmine default (VoteType.No) is ever read"
    - "Second cross-board read in CalendarSubscriptionService.GetFeedAsync reproducing the pinned-membership-set + IgnoreQueryFilters() + in-memory re-check pattern for a second entity type"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs
  modified:
    - QuestBoard.Domain/Enums/CalendarFeedSource.cs
    - QuestBoard.Domain/Models/CalendarFeedEntry.cs
    - QuestBoard.Domain/Models/CalendarFeedOptions.cs
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Domain/Services/CalendarFeedWriter.cs
    - QuestBoard.Domain/Interfaces/IQuestRepository.cs
    - QuestBoard.Repository/QuestRepository.cs
    - QuestBoard.Domain/Services/CalendarSubscriptionService.cs
    - QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs
    - QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs

key-decisions:
  - "Ordered the merged event+quest list with no sentinel substituted for an absent start time (per this plan's decision 8), matching the shipped event query's own null-first ordering rather than reversing it with a MaxValue sentinel"
  - "Kept the Any()/|| single-query shape for D-01 rather than a literal Union+Distinct — the query is rooted at Quests, so each quest is visited at most once and the dedup requirement is satisfied structurally"
  - "Re-ran the tracer's <verify> end-to-end after committing Task 1 and continued directly to the expansion tasks, per this plan's autonomous:true frontmatter and the worktree wave-execution context (no live human available to checkpoint against mid-wave)"

patterns-established:
  - "A shared entry type's per-source-meaningless field (Availability) is protected by gating the consuming code on Source before the field is ever read, rather than trusting every construction site to set a safe value"

requirements-completed: [QUESTFEED-01, QUESTFEED-02, QUESTFEED-08, QUESTFEED-09, QUESTFEED-10, QUESTFEED-11, QUESTFEED-12, QUESTFEED-17, QUESTFEED-18]

coverage:
  - id: D1
    description: "A finalized one-shot quest a reader holds a confirmed seat on reaches that reader's existing calendar address as a correct, timed, quest-namespaced VEVENT over real anonymous HTTP with no active board"
    requirement: "QUESTFEED-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_ServesASeatedReadersFinalizedOneShotQuest_ToAnonymousCaller"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs (Phase 84 event suite, unchanged)"
        status: pass
    human_judgment: false
  - id: D2
    description: "The quest session length is configurable (default 4h) and the application refuses to start if it is set below one hour, with the failure message naming the key"
    requirement: "QUESTFEED-09"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs (4 new facts + mutation check)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The writer emits a timed VEVENT whose end is exactly entry.Duration after its start, and never appends an availability suffix to a non-event source for any availability value, with events unchanged"
    requirement: "QUESTFEED-08, QUESTFEED-10, QUESTFEED-11, QUESTFEED-12, QUESTFEED-17"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs (7 new facts + 2 mutation checks)"
        status: pass
    human_judgment: false

duration: 30min
completed: 2026-09-18
status: complete
---

# Phase 85 Plan 02: One-Shot Quests Wired End-to-End Into the Calendar Feed Summary

**A single Quests-rooted repository query, a source-aware writer, and a merged/ordered document deliver a seated reader's finalized one-shot quest to a real anonymous calendar subscription as a correct, timed VEVENT — proven by a new end-to-end integration fact, with every Phase 84 event fact and the existing writer/options golden-byte suites unchanged.**

## Performance

- **Duration:** ~30 min
- **Completed:** 2026-09-18T15:49:44Z
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files modified:** 10 modified, 1 created

## Accomplishments
- `QuestRepository.GetFeedQuestsForUserAsync`: a single `IgnoreQueryFilters()`-bypassed, `Quests`-rooted query combining the one-shot board-id set with a seat-or-Dungeon-Master `Any()`/`||` disjunction inside one `Where`, structurally preventing a duplicate identifier rather than removing one after the fact
- `CalendarSubscriptionService.GetFeedAsync` grew a quest branch: `oneShotGroupIds` derived from the existing membership read, a second-layer re-check mirroring the event branch, a quest projection, and a merged/ordered document (date, then start time with no sentinel, then source, then source id)
- `CalendarFeedWriter` became source-aware in exactly two places: `AppendTimedEvent` now adds `entry.Duration` instead of a literal hour, and `BuildSummary` gates the availability suffix on `Source == Event` before ever reading `Availability` — closing the `VoteType.No`-default landmine for any non-event source
- `CalendarFeedSource.Quest`, `CalendarFeedEntry.Duration` (default 1h), and `CalendarFeedOptions.QuestDurationHours` (default 4, validated ≥1) added additively; `BuildUid` untouched
- New `CalendarSubscriptionQuestFeedTests` proves the tracer fact over real anonymous HTTP with no active board
- `CalendarFeedOptionsValidationTests` and `CalendarFeedWriterTests` extended with the session-length and quest-source facts, each backed by a one-shot mutation check proving the new guard actually guards

## Task Commits

Each task was committed atomically:

1. **Task 1: End-to-end tracer** - `2cc09833` (feat)
2. **Task 2: Pin the session length's configuration contract and refuse-to-start guard** - `418e5b3d` (test)
3. **Task 3: Extend the writer suite with quest-source duration, the no-marker invariant, and identifier namespacing** - `1e5008e4` (test)

**Plan metadata:** pending (this commit)

## Files Created/Modified
- `QuestBoard.Domain/Enums/CalendarFeedSource.cs` - added `Quest` member, corrected the now-stale "future second source" comment
- `QuestBoard.Domain/Models/CalendarFeedEntry.cs` - added `Duration` (default 1h), corrected the stale all-day comment on `StartTime`
- `QuestBoard.Domain/Models/CalendarFeedOptions.cs` - added `QuestDurationHours` (default 4) and its `IsValid()` clause
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - extended the refuse-to-start failure message to name the new key
- `QuestBoard.Domain/Services/CalendarFeedWriter.cs` - `AppendTimedEvent` uses `entry.Duration`; `BuildSummary` gates the suffix on `Source`
- `QuestBoard.Domain/Interfaces/IQuestRepository.cs` - added `GetFeedQuestsForUserAsync` with `IEventSignupRepository`-style doc-comment discipline
- `QuestBoard.Repository/QuestRepository.cs` - implemented `GetFeedQuestsForUserAsync`
- `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` - added the quest branch: `oneShotGroupIds`, the read, the re-check, the projection, the merge+order
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` - new file, one end-to-end tracer fact
- `QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs` - 4 new facts (default=4, valid at 1, invalid at 0/-1, wiring throws naming the key) + extended the default-constructed-options fact
- `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` - 7 new facts (duration at 4h/2h, event-default regression guard, no-marker theory + enum-count guard, UID namespacing at a colliding id, never-all-day)

## Decisions Made
- Ordered the merged event+quest list with no sentinel for an absent start time (this plan's decision 8), matching the event query's own null-first ordering
- Kept the `Any()`/`||` single-query shape for the D-01 disjunction over a literal `Union`+`Distinct`, per this plan's decision 2 and the research's precedent (`GroupRepository.GetGroupsForUserAsync`)
- Re-ran the tracer's `<verify>` end-to-end after Task 1's commit and continued directly to Tasks 2-3 rather than emitting an interactive checkpoint, given this plan's `autonomous: true` frontmatter and the worktree wave-execution context

## Deviations from Plan

### Auto-fixed Issues

None — no bugs, missing critical functionality, or blocking issues required a fix outside the plan's own instructions.

### Acceptance-criteria discrepancy (not a code defect)

**Task 1's acceptance criterion `grep -c 'IgnoreQueryFilters' QuestBoard.Repository/QuestRepository.cs outputs 1` does not hold — it outputs 3.**
- **Found during:** Task 1 verification.
- **Cause:** the criterion assumed no other method in `QuestRepository` already bypassed the ambient filter. In fact `GetQuestsForTomorrowAllGroupsAsync` (pre-existing, unrelated to Phase 84/85, used by `DailyReminderJob`'s system-wide sweep) already contains one `IgnoreQueryFilters()` call plus an explanatory comment line — 2 matching lines pre-existing, +1 new line from this plan's `GetFeedQuestsForUserAsync`, totalling 3.
- **Resolution:** no code change — this is the intended, mandatory bypass for the new method (verified narrower-than-filter, re-checked in the service layer). The criterion's underlying intent — "the new method's bypass is immediately re-narrowed and doesn't leak" — is separately confirmed by the passing tracer fact and the unchanged Phase 84 suite. Not a deviation from the plan's actual functional requirement, just a stale literal count in the acceptance criteria text.

---

**Total deviations:** 0 auto-fixed. One acceptance-criterion text discrepancy noted above (pre-existing code, not a functional issue).
**Impact on plan:** None on scope or correctness.

## Issues Encountered

**Full-solution `dotnet test` in this sandbox hits environment-level resource limits unrelated to this plan.** Running the entire `QuestBoard.IntegrationTests` project (816 facts) in this Linux sandbox produces ~130-190 unrelated failures from `System.IO.IOException: The configured user limit (128) on the number of inotify instances has been reached` (each parallel `WebApplicationFactory` host registers a `FileSystemWatcher`), plus one pre-existing, Phase-84-era static-guard test (`CalendarSubscriptionStaticGuardTests.NoPlanningOrTrackingReference_ReachedTheSourceTree`) that fails deterministically on Linux because the build's native apphost binary (`QuestBoard.Service`, no extension) collides with the project-folder name its `ResolveRepoFile` helper is walking up to find — a bug that never surfaces on the project's normal Windows environment, where the apphost carries a `.exe` extension. Neither issue touches any file this plan modified; confirmed by re-running `CalendarSubscriptionQuestFeedTests`, `CalendarSubscriptionFeedTests`, and the full `QuestBoard.UnitTests` suite individually — all green, every time. Logged in full at `.planning/phases/85-one-shot-quests-in-the-calendar-feed/deferred-items.md` per the scope-boundary rule; not fixed, as neither is caused by or in scope for this plan.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

The tracer proves the full five-layer wire-up (repository → service → writer → live HTTP) on the thinnest real path. Plans 85-03 through 85-05 can now expand the predicate's remaining behavioural facts (Dungeon Master branch, all signup roles, `DungeonMasterSession` non-filtering, disappearance cases, window edges, board-type narrowing, tenant isolation) against this same `GetFeedQuestsForUserAsync`/`GetFeedAsync`/`CalendarFeedWriter` wiring without any further architectural change — this plan's own `<decisions_this_plan_resolves>` block already resolves every open call those plans would otherwise need to re-litigate.

No blockers. The known environment-only test-runner limitation (above) does not block downstream plans, since it is orthogonal to the calendar-feed code path and reproduces identically regardless of which phase's tests are run.

---
*Phase: 85-one-shot-quests-in-the-calendar-feed*
*Completed: 2026-09-18*
