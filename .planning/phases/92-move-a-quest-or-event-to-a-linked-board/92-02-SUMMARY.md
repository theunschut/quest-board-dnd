---
phase: 92-move-a-quest-or-event-to-a-linked-board
plan: 02
subsystem: email
tags: [email, hangfire, razor, quest-move]
requires: []
provides:
  - QuestMovedAudience enum (Player, DungeonMaster, Removed)
  - QuestMoved.razor email template, three audiences
  - WaitlistPromoted.MovedToBoardName optional moved note
  - IQuestEmailDispatcher.EnqueueQuestMovedEmail and EnqueueWaitlistPromotedAfterMoveEmail
  - QuestMovedEmailJob and QuestWaitlistPromotedEmailJob.ExecuteAfterMoveAsync
  - Email Preview entries for the four new renders
  - MoveWebApplicationFactory capturing quest mail
affects: [92-05]
tech-stack:
  added: []
  patterns:
    - one Hangfire job per recipient, singular recipient argument
    - new entry point instead of optional argument for Hangfire lambdas
key-files:
  created:
    - QuestBoard.Domain/Enums/QuestMovedAudience.cs
    - QuestBoard.Service/Components/Emails/QuestMoved.razor
    - QuestBoard.Service/Jobs/QuestMovedEmailJob.cs
    - QuestBoard.IntegrationTests/Helpers/MoveWebApplicationFactory.cs
    - QuestBoard.IntegrationTests/Emails/QuestMovedRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveMailEnqueueTests.cs
    - QuestBoard.UnitTests/Services/BoardMoveMailJobTests.cs
  modified:
    - QuestBoard.Service/Components/Emails/WaitlistPromoted.razor
    - QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs
    - QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs
    - QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs
    - QuestBoard.Service/Services/NullQuestEmailDispatcher.cs
    - QuestBoard.Service/Jobs/QuestWaitlistPromotedEmailJob.cs
decisions:
  - Email Preview uses a fixed sample session date so the five-cosmetic-uses guard stays green
  - Existing waitlist ExecuteAsync kept byte-compatible; shared body moved to a private SendAsync
metrics:
  tasks: 2
  commits: 2
status: complete
actuals:
  tokens: 28000
  tasks: 2
  commits: 2
---

# Phase 92 Plan 02: Quest move mail plumbing Summary

Quest Moved email (player, Dungeon Master, removed) plus an optional "moved to {Board}" line on Waitlist Promoted, with per-recipient Hangfire jobs, Email Preview entries and a test factory that captures quest mail.

## What was built

- `QuestMoved.razor` follows the QuestDateChanged poster structure. One `Audience` parameter drives subject, preview text, heading, subtitle, info labels, note and CTA. The removed variant has no button but keeps the 96px bottom-padding row. The session date is formatted in the template as `dddd, MMMM d`, or "Still being decided" when null, and is never converted between zones. All text goes through Razor encoding.
- `WaitlistPromoted.MovedToBoardName` is optional. The note renders only when it is set, and an unset value renders the same as an explicit null.
- `IQuestEmailDispatcher` gains `EnqueueQuestMovedEmail` and `EnqueueWaitlistPromotedAfterMoveEmail`, implemented in the Hangfire dispatcher (every lambda argument explicit) and as no-ops in the Null dispatcher.
- `QuestMovedEmailJob` is one job per recipient and runs inside `HangfireJobHelper.RunInScopeAsync` with the supplied group id.
- `QuestWaitlistPromotedEmailJob.ExecuteAfterMoveAsync` is added. `ExecuteAsync` keeps its signature so jobs already queued still bind.
- Four Email Preview actions and index links, using one fixed sample date.
- `MoveWebApplicationFactory` re-registers `HangfireQuestEmailDispatcher` so the capturing job client records quest mail. `WebApplicationFactoryBase` is untouched.

## Verification

- `dotnet build` succeeds.
- Integration tests (QuestMovedRender, WaitlistPromoted render, BoardMoveMailEnqueue and the wider Email filter): 38 passed.
- Unit tests (BoardMoveMail, QuestService, EmailConfirmationJobGuard, Architecture including AmbientClockSeamTests): 101 passed.
- No planning references in added source lines.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Unit test expression tree and scope substitute**
- **Found during:** Task 2
- **Issue:** An `is` pattern inside an NSubstitute `Arg.Is` expression does not compile, and `CreateAsyncScope().Returns(...)` mismatched on `CreateScope`.
- **Fix:** Used `object.Equals` plus a cast, and configured `CreateScope().Returns(scope)`.
- **Files modified:** QuestBoard.UnitTests/Services/BoardMoveMailJobTests.cs
- **Commit:** 21fd2342

Otherwise the plan ran as written. The Email Preview fixed sample date was the plan's own recorded choice. Tests and implementation were committed together per task rather than as separate RED and GREEN commits.

## Known Stubs

None.

## Not run

The visual backstop (rendering the three previews with a long title and long board name to check the poster's bottom edge) is not automated and was not run. Word-wrap styles were added to the title, subtitle, values and note cells.

## Threat Flags

None. The four new Email Preview actions sit behind the existing AdminOnly controller and render fixed sample data.

## Commits

- 00d02c18: Quest Moved email and moved note on Waitlist Promoted
- 21fd2342: dispatcher methods, jobs and capturing factory

## Self-Check: PASSED

All created files exist and both commits are in `git log`.
