---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
fixed_at: 2026-09-30T00:00:00Z
review_path: .planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 88: Code Review Fix Report

**Fixed at:** 2026-09-30
**Source review:** .planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 2 (Critical and Warning; the five Info items are out of scope)
- Fixed: 2
- Skipped: 0

This report overwrites the previous round's. That round fixed the earlier WR-01 (feed window) and WR-02 (DTSTAMP labelling) in commits b43a4f74 and e98ad91d. The IDs below belong to this review only.

## Fixed Issues

### WR-01: Concurrent or stale saves can lose a bump or lower SEQUENCE

**Files modified:** `QuestBoard.Repository/Entities/QuestBoardContext.cs`, `QuestBoard.Repository/FeedRevisionStamper.cs`, `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs`, `QuestBoard.UnitTests/Repository/FeedRevisionStamperTests.cs`, `QuestBoard.UnitTests/Architecture/FeedRevisionWriteSeamTests.cs`
**Commit:** 2dc50af0
**Status:** fixed: requires human verification (concurrency logic)

**Applied fix:** `FeedRevision` on `EventEntity` and `QuestEntity` is now an EF concurrency token. Every UPDATE or DELETE of those rows carries `WHERE FeedRevision = @original`, so a stale writer can no longer overwrite a newer revision. A save that loses the race is retried inside the context (see the mechanism choice below).

**Mechanism choice and why:**

- **Chosen: concurrency token plus an in-context rebase-and-retry.**
  - The two save overrides in `QuestBoardContext` catch `DbUpdateConcurrencyException`. `FeedRevisionStamper.TryAdoptStoredRevision[Async]` reads the row's stored values, adopts the stored `FeedRevision` and `FeedRevisedAt` as the new originals, and the save runs again. The stamper then computes stored + 1, and only the properties this context changed are written.
  - The invariant holds: SEQUENCE is always stored + 1, so it cannot go backwards, and both saves are published under distinct rising revisions.
  - A lost race is a benign race between two saves, not an error. The edit form, finalize, template sweeps and background jobs all go through these two overrides, so one fix covers every caller and nobody sees an error.
  - Retries are bounded at 5. A row that keeps changing under every retry surfaces the original `DbUpdateConcurrencyException`.
- **Rejected: token with the exception propagating.** The exception would have to be handled at every call site: the DM edit forms, finalize, the series sweep, and background jobs. A benign race would be a 500 wherever a handler was missed. It also makes unrelated saves fail (a description edit racing a rename).
- **Rejected: atomic SQL increment (`SET FeedRevision = FeedRevision + 1`).** It would need `ExecuteUpdate` or raw SQL, which the write-seam guard bans because it bypasses the change tracker. It would also split the save into two statements that need a transaction, and the InMemory provider used by the integration tests cannot run it.
- **No schema change and no migration.** The token only adds a predicate to the SQL. The model snapshot gained two `.IsConcurrencyToken()` lines, and the existing `AddFeedEntryRevisions` migration was not touched. `dotnet ef migrations has-pending-model-changes` reports "No changes have been made to the model since the last migration". `dotnet ef database update` was not run.

**What the user sees:** nothing changes for a benign race. The form saves and both edits are kept. Each save writes only the fields that user changed, the same last-writer-wins rule every other column already had. The deleted-row case is unchanged: if another context deleted the row, the save still throws `DbUpdateConcurrencyException`, as EF did before the token. A test pins this boundary.

**Tests added (FeedRevisionStamperTests).** Four fail against the old code, for example "Expected stored.FeedRevision to be 4, but found 2" for the stale-context case:

- `Event_TwoContextsSavingFeedChangesFromTheSameRevision_PublishTwoDistinctRevisions` (lost bump; expects 3)
- `Quest_TwoContextsSavingFeedChangesFromTheSameRevision_PublishTwoDistinctRevisions` (expects 3)
- `Event_TwoContextsSavingSynchronouslyFromTheSameRevision_PublishTwoDistinctRevisions` (sync overload)
- `Event_AStaleContextSavingAfterTheRevisionMovedOn_NeverWritesALowerRevision` (backwards write; expects 4)

Two pins pass on both old and new code and guard the boundaries:

- `Event_AStaleContextSavingAFieldTheFeedDoesNotShow_LeavesTheStoredRevisionAndStampAlone`
- `Event_AStaleContextSavingARowAnotherContextDeleted_StillFails`

`TheContext_OverridesBothSaveMethods_AndRunsTheStamperInEach` in the seam tests now accepts `override async Task<int> SaveChangesAsync(`, since the override became `async`.

**Notes for verification:**

- Against SQL Server, a failed save rolls its transaction back, so no partial write survives before the retry. With the InMemory provider used in tests, a conflict part-way through a multi-entity save can leave earlier rows applied. On retry those rows also conflict and get a second bump. That is an extra upward bump, never a lower one.
- The SQL Server path for the token (predicate in the UPDATE) was not exercised against a real database, only via InMemory's equivalent token check. It is worth one manual two-tab edit of the same event against the local database.

### WR-02: Write-path guard misses the state-setting shape that silently defeats the stamper

**Files modified:** `QuestBoard.UnitTests/Architecture/FeedRevisionWriteSeamTests.cs`, `.claude/architecture.md`
**Commit:** 4c722f40

**Applied fix:**

- The banned shapes are now `BannedShape` records (a literal or a regex, plus a sample line). New bans: `.Entry(`, `.TrackGraph(`, and any assignment to a `.State` property, matched by `\.State\s*=(?!=)`.
- The review suggested banning the literal `EntityState.Modified`. That would break the stamper's legitimate `case EntityState.Modified:` and `is EntityState.Modified or EntityState.Deleted` reads. Banning the assignment instead keeps those reads allowed and needs no allow-list entry for the stamper, so the stamper stays subject to every other ban.
- `StripComments` now uses `StripLineComment`, which ignores `//` inside a string literal. A banned call after such a literal on the same line was previously hidden (the second half of the review's WR-02 note).
- The self-check now proves each detector fires on its own sample and stays quiet in a comment. New theories cover:
  - the spellings `Entry(x).State = ...`, `State=...`, `entry.State = state`, `.CurrentValues.SetValues`, `.ReloadAsync` and `TrackGraph`;
  - the reads that must stay allowed (`switch (entry.State)`, `== EntityState.Modified`, `is EntityState.Modified or ...`, `Entries()`);
  - the string-literal case.
- `.claude/architecture.md` now also lists setting an entry's state by hand as forbidden.

**Guard proven both ways:**

- It passes on today's production code. The scan test `NoProductionSource_WritesRowsPastTheChangeTracker` is green.
- It fails on a planted violation. A temporary `QuestBoard.Repository/PlantedViolation.cs` containing `context.Entry(detached).State = EntityState.Modified;` made the scan fail with `{"QuestBoard.Repository/PlantedViolation.cs: .Entry(, assigning an entity state"}`. The file was deleted and was never committed.

## Verification

- Gates ran in the isolated worktree `.claude/worktrees/rf-88-29247-1790791200`, cut from the milestone branch tip. Its commits were then fast-forwarded onto `milestone/v9-rolling-improvements` and the worktree, temp branch and recovery sentinel were removed. The numbers are reproducible from the main checkout; a re-run there was not repeated after the fast-forward.
- Full `dotnet test`: **805 unit (baseline 787) and 951 integration (baseline 951)**, 0 failures. The unit delta of +18 is 6 new stamper tests and 12 new guard cases (7 detector theory rows, 4 allowed-read rows, 1 string-literal test).
- Byte-pinned feed contract intact: the writer and service tests and the integration feed tests pass unchanged. Unedited entries stay byte-identical, SEQUENCE is floored at 1, and DTSTAMP = LAST-MODIFIED = `FeedRevisedAt` as a labelled UTC instant. No feed-writer or service code was touched.
- C# edits were made only with the Edit tool. `git diff --stat` was checked before each commit, and CRLF endings were preserved.

---

_Fixed: 2026-09-30_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
