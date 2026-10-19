---
phase: 81-contact-tags-and-filtering
fixed_at: 2026-10-19T00:00:00Z
review_path: .planning/phases/81-contact-tags-and-filtering/81-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 81: Code Review Fix Report

**Fixed at:** 2026-10-19
**Source review:** .planning/phases/81-contact-tags-and-filtering/81-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 4 (CR-01, WR-01, WR-02, WR-03; IN-01 is Info and out of scope)
- Fixed: 4 (1 of them already fixed before this run, 3 fixed here)
- Skipped: 0

## Fixed Issues

### CR-01: `AddNote` lets any authenticated user inject a note onto another board's Contact

**Status:** already fixed before this run (no change made here)
**Files modified:** none in this run
**Commits:** `19dbe8dc` (controller resolves `contactId` through the board-filtered read before inserting), `51671672` (`ContactRepository.AddNoteAsync` also refuses to insert when the contact does not resolve through the filtered set), both from quick task 260831-hz9.
**Verification:** re-read the current code. `ContactsController.AddNote` calls `GetContactWithDetailsAsync` and returns `NotFound()` for an unresolvable id before anything is written, and `ContactRepository.AddNoteAsync` checks `DbContext.Contacts.AnyAsync(...)` and returns without inserting when it fails. Both layers of the review's recommended fix are present.

### WR-01: Stale test comment documents filter behavior that was already removed

**Files modified:** `QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs`
**Commit:** c153f3e6
**Applied fix:** Rewrote the comment above `Index_MobileActiveFilterNoMatches_RendersNoMatchHeading` to say the filter narrows strictly by intersection, that the no-match branch renders whether or not the board has visible contacts, and that this test covers the empty-board case. The comment contains no planning references. The test body is unchanged.

### WR-02: Concurrent creation of the same new tag name on one board can throw an unhandled `DbUpdateException`

**Files modified:** `QuestBoard.Repository/ContactRepository.cs`, `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs`
**Commit:** 721db498
**Applied fix:** `ReplaceContactTagsAsync` now delegates one reconciliation attempt to a private `ApplyContactTagsAsync`. If that attempt throws `DbUpdateException` while the change tracker holds a pending new `ContactTagEntity` (the only situation in which the failure can be the duplicate-name race), the tracker is cleared and the attempt runs once more. The retry re-reads the board vocabulary through the filtered `ContactTags` set, finds the winner's row and reuses it. The retry is not wrapped, so a persistent failure surfaces after exactly one retry rather than looping. All writes still go through the change tracker and the board query filters; there is no `IgnoreQueryFilters` and no `ExecuteUpdate`. The retry is provider-agnostic: it does not inspect `SqlException` numbers, which also makes it testable on InMemory.
**Tests added (2):** the InMemory provider has no unique index, so a `SaveChangesInterceptor` simulates the lost race.
- `ReplaceContactTagsAsync_ConcurrentSameNewName_RetriesAndReusesWinnersRow` commits the winner's identically named row from a second context on the first tag-inserting save, then fails that save. The test asserts that no exception reaches the caller, that exactly one tag row exists, and that both contacts share it.
- `ReplaceContactTagsAsync_NewTagSaveKeepsFailing_RetriesOnceThenSurfacesTheFailure` asserts exactly two attempts and that `DbUpdateException` surfaces.
**Status note:** this is a concurrency-handling change, so a human should confirm the retry semantics (clearing the tracker on the failure path is safe because the only pending work is the reconciliation that just failed, and both callers redirect afterwards).

### WR-03: `Index` fetches the full contact/notes/tags detail set twice in the same request

**Files modified:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs`
**Commit:** 56bf16ad
**Applied fix:** Split the vocabulary derivation out of `GetVisibleTagVocabularyAsync` into a static `BuildVisibleTagVocabulary(allContacts, currentUserId, viewerIsDmTier, includeHidden, scope)`. `Index` now builds `availableTags` from the `allContacts` it already loaded, so the visible list and the vocabulary come from one read. `GetVisibleTagVocabularyAsync` still loads the contacts and delegates to the same helper, for Create GET, Edit GET and the invalid-model re-renders. What each viewer sees is unchanged. The scoping from the later ownership change is preserved: the non-DM-tier short-circuit, the `IsVisibleTo` gate, and the `AreTagsVisibleTo` narrowing for `ViewerVisible` all remain inside the shared helper. For a DM-tier viewer, `Index`'s `includeHidden` equals the session toggle the old helper read itself. Updated the neighbouring comments to match. No new test was added: the ownership x toggle x role x surface matrix in `ContactsTagOwnershipTests` already pins the observable behaviour, and the request-count change is not observable through the HTTP surface.

## Skipped Issues

None. IN-01 (tag-name max length constant duplicated in three places) is Info and was out of scope for `fix_scope: critical_warning`.

## Verification

Ran in this isolated worktree (a worktree supplied by the orchestrator), not the main checkout. I did not create a second nested worktree. The same build and test commands run from the merged branch.
- `dotnet build`: 0 errors.
- Unit tests: 1105 passed, 0 failed, 0 skipped (baseline 1103, plus the 2 new tests).
- Integration tests: 1324 passed, 0 failed, 11 skipped (matches the baseline; the skips are SQL Server-only migrator tests).
- The affected suites also passed while iterating: Contacts integration filter, 97 passed; `ContactRepositoryTests`, 28 passed.

---

_Fixed: 2026-10-19_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
