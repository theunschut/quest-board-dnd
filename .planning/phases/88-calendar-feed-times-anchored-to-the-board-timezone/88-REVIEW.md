---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
reviewed: 2026-09-30T00:00:00Z
depth: standard
files_reviewed: 28
files_reviewed_list:
  - .claude/architecture.md
  - .gitignore
  - QuestBoard.Domain/Models/CalendarFeedEntry.cs
  - QuestBoard.Domain/Models/Event.cs
  - QuestBoard.Domain/Models/EventFeedRow.cs
  - QuestBoard.Domain/Models/QuestBoard/Quest.cs
  - QuestBoard.Domain/Services/CalendarFeedWriter.cs
  - QuestBoard.Domain/Services/CalendarSubscriptionService.cs
  - QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarFeedQuestRevisionTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/Entities/EventEntity.cs
  - QuestBoard.Repository/Entities/QuestBoardContext.cs
  - QuestBoard.Repository/Entities/QuestEntity.cs
  - QuestBoard.Repository/EventSignupRepository.cs
  - QuestBoard.Repository/FeedRevisionStamper.cs
  - QuestBoard.Repository/Migrations/20260930161610_AddFeedEntryRevisions.cs
  - QuestBoard.Repository/Migrations/20260930161610_AddFeedEntryRevisions.Designer.cs
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
  - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
  - QuestBoard.UnitTests/Architecture/FeedRevisionWriteSeamTests.cs
  - QuestBoard.UnitTests/Repository/FeedRevisionStamperTests.cs
  - QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs
  - QuestBoard.UnitTests/Services/CalendarFeedRevisionInputTests.cs
  - QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs
  - QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs
findings:
  critical: 0
  warning: 2
  info: 5
  total: 7
status: issues_found
---

# Phase 88: Code Review Report (incremental re-review)

**Reviewed:** 2026-09-30
**Depth:** standard
**Files Reviewed:** 28
**Status:** issues_found

This is an incremental re-review of everything changed since commit 5e512668. It covers the WR-01 and WR-02 fixes from the previous review, plus the G-88-4 gap closure (feed entry revisions). The previous review's warnings are recorded as fixed in 88-REVIEW-FIX.md. This report overwrites it.

## Summary

The revision mechanism is sound on its main paths. I traced these and found no correctness defect in any of them:

- **Write paths.** Every production write to `Events` and `Quests` goes through tracked entities: `BaseRepository.UpdateAsync` (FindAsync plus `Mapper.Map` onto the tracked entity), `ApplyTemplateToOccurrencesAsync`, `DetachOccurrencesAndDeleteAsync`, the quest finalize/open/property-update methods and `AddAsync`. A production-wide search finds no `ExecuteUpdate`, `ExecuteSql`, `Attach`, `Update(` or `Entry(`. The only `EntityState` references are in the stamper.
- **Mapper.** Both AutoMapper entity maps ignore `FeedRevision` and `FeedRevisedAt`, so a domain model cannot overwrite them.
- **Stamper.** It restores the stored values before it decides anything. It bumps by exactly one per save and never moves the stamp backwards. It clears `IsModified` on a no-change save, so the UPDATE carries neither column. A retried `SaveChanges` is idempotent because the bump is computed from the original values.
- **Save overloads.** Both `SaveChanges(bool)` and `SaveChangesAsync(bool, ct)` are overridden, and every other overload funnels into them.
- **Writer.** `SEQUENCE` is floored at 1. DTSTAMP and LAST-MODIFIED come from one string, labelled with `SpecifyKind` and never converted. The host-zone conversion shapes are banned in both feed files by a test.
- **Tenancy.** The diff adds no `IgnoreQueryFilters` site. The stamper issues no query.
- **Migration.** Up and Down are correct for SQL Server. `AddColumn` and the `UPDATE` run as separate commands, so the new column is visible to the bump. `DropColumn` removes the default constraints automatically. The bump uses the DB-side UTC clock.
- **Tests.** The rewritten pins do catch a regression to a constant SEQUENCE or to CreatedAt. This holds at three layers:
  - Writer: distinct per-entry sequences (3, 7, 4) and non-default stamps.
  - Service: an assertion that the stored revision time is not the creation time.
  - Integration: the SEQUENCE and DTSTAMP movement across fetches.

Two warnings remain, both about what the design claims to guarantee. The revision guarantee is enforced only by read-modify-write inside one context, and the write-path guard test has a hole that silently defeats the stamper. There are five info items.

## Warnings

### WR-01: Concurrent or stale saves can lose a bump or lower SEQUENCE

**File:** `QuestBoard.Repository/FeedRevisionStamper.cs:102-131`
**Issue:** The stamper writes an absolute `originalRevision + 1`, and the UPDATE has no concurrency predicate on `FeedRevision`. Two contexts that load the same row therefore lose information:

- **Lost bump.** A and B both load revision 2. A saves a title change and stores 3. B saves a date change and also stores 3. The row has changed twice but was published at revision 3 both times. A client that fetched A's version (SEQUENCE 3) and then B's version (SEQUENCE 3, later DTSTAMP) sees no higher sequence. Clients that compare the sequence first and the stamp second, as the writer's own comment says they do, may keep the stale copy.
- **Backwards write.** B loads revision 2. Two other saves raise the row to 4. B then saves and writes 3. SEQUENCE goes backwards, which the docs and the writer's floor-at-1 logic explicitly say must never happen. A client holding 4 ignores every later revision until the number passes 4 again.

Both races are narrow for a DM edit form. They are real for a Hangfire or series sweep running alongside a DM edit. No test covers either case.
**Fix:** Make the stored revision a concurrency token so a stale writer fails instead of overwriting:
```csharp
// QuestBoardContext.OnModelCreating
modelBuilder.Entity<EventEntity>().Property(e => e.FeedRevision).IsConcurrencyToken();
modelBuilder.Entity<QuestEntity>().Property(q => q.FeedRevision).IsConcurrencyToken();
```
This adds `WHERE FeedRevision = @original` to every UPDATE of these rows, and needs no schema change, only a snapshot update. Callers already have to tolerate `DbUpdateConcurrencyException` from the Identity stack. Alternatively, raise the revision atomically in SQL (`SET FeedRevision = FeedRevision + 1`). Either way, add a stamper test with two contexts saving from the same original.

### WR-02: Write-path guard misses the state-setting shape that silently defeats the stamper

**File:** `QuestBoard.UnitTests/Architecture/FeedRevisionWriteSeamTests.cs:29-37`
**Issue:** `BannedWriteShapes` lists `ExecuteUpdate`, `ExecuteSql`, `.Attach(`, `.AttachRange(`, `.Update(` and `.UpdateRange(`. It omits `context.Entry(detached).State = EntityState.Modified`, which is the same failure mode. A detached entity marked Modified has original values equal to its current values. Every feed field therefore compares equal, `feedChanged` is false, and the stamper clears `IsModified` on the two revision columns. The edit is written (Title and the other properties are all flagged modified) but the revision is never raised. That is exactly the stale-copy bug this phase closes, and the guard exists to prevent it.

The guard's own header says it fails the build "the moment such a write shape appears". It does not for this shape. Note also that `.Update(`/`.Attach(` are banned only as text in three roots. The comment-stripper cuts at the first `//` on a line, which could hide a banned call after a string literal containing `//` on the same line.
**Fix:** Add the missing shapes to the list:
```csharp
"EntityState.Modified",
".Entry(",
```
The production search found no current use of either, so this adds no offender. Consider also pinning `.Database.ExecuteSql` explicitly, since the `ExecuteSql` substring only happens to cover it.

## Info

### IN-01: Stale writer header comment

**File:** `QuestBoard.Domain/Services/CalendarFeedWriter.cs:9-10`
**Issue:** The class comment still says the feed "emits a five-field VEVENT". Each entry now carries UID, DTSTAMP, LAST-MODIFIED, DTSTART, DTEND, SUMMARY, TRANSP and SEQUENCE. This was already imprecise before this change and is wrong now.
**Fix:** Drop the field count: "emits a small fixed VEVENT".

### IN-02: Garbled ETag comment

**File:** `QuestBoard.Domain/Services/CalendarSubscriptionService.cs:180-183`
**Issue:** The edit left a tautology: "an event edit produces a new tag automatically: the tag changes exactly when the document's bytes change", directly after the sentence saying the same thing.
**Fix:** Collapse to one sentence.

### IN-03: Re-posting the same availability moves the reader's stamp, and the write uses the ambient clock

**File:** `QuestBoard.Repository/EventSignupRepository.cs:27-28, 40-41`
**Issue:** `SetAvailabilityAsync` sets `UpdatedAt = DateTime.UtcNow` even when the answer is unchanged. The reader's DTSTAMP and the ETag then change with no visible change, which is a mild breach of the byte-identical-when-unchanged rule. The file also reads `DateTime.UtcNow` twice on the create path, so `CreatedAt` and `UpdatedAt` can differ by ticks. The stamper uses the context's injected `TimeProvider` while this write uses the ambient clock. They only agree because both are the system clock today. The file is also not in the `AmbientClockSeamTests` closed list.
**Fix:** Skip the write when `entity.Availability == (int)availability`. Take one `var now = timeProvider.GetUtcNow().UtcDateTime` and use it for both columns.

### IN-04: An availability change is signalled only through DTSTAMP

**File:** `QuestBoard.Domain/Services/CalendarSubscriptionService.cs:113-120`
**Issue:** The SUMMARY suffix "(maybe)" or "(declined)" is a visible change for one reader, but SEQUENCE stays the same by design. A client that compares only SEQUENCE, or that ignores same-sequence updates, will keep a stale availability suffix. This is the stated operator design and the tests pin it, so it is not a defect. It is a known client-dependent limitation that the architecture note's known-limitations list does not mention.
**Fix:** Add one line to the known limitations in `.claude/architecture.md`.

### IN-05: An unset revision stamp is published silently as year 1, and the added-row rule is weaker than documented

**File:** `QuestBoard.Domain/Models/CalendarFeedEntry.cs:36`, `QuestBoard.Repository/FeedRevisionStamper.cs:85-98`
**Issue:** The `LastRevisedAt` default is `default(DateTime)`, so a caller that forgets it emits `DTSTAMP:00010101T000000Z` with no failure. Separately, `StampNewRow` honours any caller-supplied revision of at least 1 and any non-default stamp on an inserted row, although the docs say no caller can set these columns. Through the mappers this cannot happen, because they ignore both members. A directly constructed entity with a far-future `FeedRevisedAt` would freeze that row's stamp permanently, since the never-backwards rule keeps the larger value.
**Fix:** Mark `LastRevisedAt` as `required` on `CalendarFeedEntry`. For inserts, either always overwrite with revision 1 and `CreatedAt`, or cap the stamp at `utcNow`.

---

_Reviewed: 2026-09-30_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
