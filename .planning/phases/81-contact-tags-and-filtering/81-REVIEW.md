---
phase: 81-contact-tags-and-filtering
reviewed: 2026-08-31T00:00:00Z
depth: standard
files_reviewed: 33
files_reviewed_list:
  - QuestBoard.Domain/Interfaces/IContactRepository.cs
  - QuestBoard.Domain/Interfaces/IContactService.cs
  - QuestBoard.Domain/Models/Contact.cs
  - QuestBoard.Domain/Services/ContactService.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs
  - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs
  - QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/ContactRepository.cs
  - QuestBoard.Repository/Entities/ContactEntity.cs
  - QuestBoard.Repository/Entities/ContactTagEntity.cs
  - QuestBoard.Repository/Entities/QuestBoardContext.cs
  - QuestBoard.Repository/Migrations/20260831081102_AddContactTags.Designer.cs
  - QuestBoard.Repository/Migrations/20260831081102_AddContactTags.cs
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
  - QuestBoard.Service/Automapper/ViewModelProfile.cs
  - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
  - QuestBoard.Service/ViewModels/ContactViewModels/ContactTagViewModel.cs
  - QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
  - QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs
  - QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Create.cshtml
  - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Details.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.cshtml
  - QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Index.cshtml
  - QuestBoard.Service/wwwroot/css/contact-detail.mobile.css
  - QuestBoard.Service/wwwroot/css/contact-form.mobile.css
  - QuestBoard.Service/wwwroot/css/contacts.css
  - QuestBoard.Service/wwwroot/css/contacts.mobile.css
  - QuestBoard.Service/wwwroot/js/contact-tags.js
  - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/QuestBoardContextFilterTests.cs
  - QuestBoard.UnitTests/Services/ContactServiceTests.cs
findings:
  critical: 1
  warning: 3
  info: 1
  total: 5
status: issues_found
---

# Phase 81: Code Review Report

**Reviewed:** 2026-08-31T00:00:00Z
**Depth:** standard
**Files Reviewed:** 36
**Status:** issues_found

## Summary

The new tag/filter feature itself (`ContactTagEntity`, the `Contact <-> ContactTag` join, `ReplaceContactTagsAsync`'s reconcile/prune logic, `QuestBoardContext`'s query filters, and the Index tag-filter path) is carefully built and heavily tested: the tag entity and its implicit join carry a fail-closed `HasQueryFilter`, the reconciliation path re-resolves matches through the board-filtered `DbSet` (never through the change tracker by raw id), tag-name comparison correctly uses `OrdinalIgnoreCase` in memory to match the column's SQL collation, orphan pruning re-checks `Contacts.Count` after every mutation, and the non-DM response paths (`Index`, `Details`) clear `Tags` server-side in addition to gating the view. Razor's default encoding and the default (HTML-safe) `JsonSerializerOptions` encoder mean tag names typed by a DM cannot break out of the rendered chip markup or the `Tagify` whitelist `<script>` block — verified against `Program.cs` (no `AddJsonOptions` override) and confirmed by `ContactsTagsDesktopMarkupTests.Index_TagNameWithMarkupCharacters_IsEscaped`.

While reading the full (not just diffed) `ContactsController.cs` as instructed, I found one pre-existing, unrelated defect in the note-authoring code path that is a genuine cross-tenant data-integrity hole (see CR-01) — flagged because it directly matches the "cross-tenant leak" theme this review was asked to focus on, even though the note actions were not touched by this phase's diff. The tag/filter feature itself has no equivalent hole. I also found a stale test comment that documents a filter-widening behavior that was already fixed in a prior phase-81 commit (WR-01), a missing safety net for a concurrent duplicate-tag-name race (WR-02), and a redundant full-detail contact re-fetch inside a single request (WR-03).

## Critical Issues

### CR-01: `AddNote` lets any authenticated user inject a note onto another board's Contact (cross-tenant write, no query-filter protection)

**File:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:457-483`, `QuestBoard.Repository/ContactRepository.cs:287-294`

**Issue:** `AddNote(int contactId, ...)` builds a `ContactNote` from the raw, caller-supplied `contactId` route/form value and passes it straight to `contactService.AddNoteAsync` → `ContactRepository.AddNoteAsync`, which does an unconditional `DbContext.Set<ContactNoteEntity>().Add(entity)` followed by `SaveChangesAsync` — with no prior lookup of the contact through the board-filtered `Contacts` set.

EF Core's `HasQueryFilter` on `ContactNoteEntity` (and on `ContactEntity`) only constrains **queries** (`SELECT`); it does nothing to constrain an `Add()`/`INSERT`. Every other note action in this same controller (`EditNote`, `DeleteNote`) is safe specifically because they re-resolve the note through a **query** against the filtered `ContactNoteEntity`/`ContactEntity` sets before mutating — `AddNote` is the one path that skips that resolve step entirely.

Contact ids are a single, globally auto-incrementing identity column shared across every board (not a per-board sequence), so a contact id belonging to another campaign's board is trivially guessable/enumerable by any authenticated player on any board. Concretely: a Player on Board A can `POST /Contacts/AddNote` with `contactId` set to a small integer belonging to a contact on Board B, and the note is durably inserted against Board B's contact — attributed to the Board-A user's real `AuthorUserId` — even though Board A's active-group query filter means that same attacker cannot see, list, or otherwise reach that contact through any read path. The subsequent `RedirectToAction(nameof(Details), new { id = contactId })` correctly 404s for the attacker (since `Details` reads through the filter), which likely masked this in manual testing — but the write already committed before the redirect.

This is not part of this phase's diff (the note actions predate contact tags), but it directly matches the "cross-tenant data leak" risk this review was asked to prioritize, so it is called out here rather than passed over.

**Fix:** Resolve the contact through the board-filtered service/repository before adding the note, exactly as `EditNote`/`DeleteNote` already do implicitly through the query filter:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AddNote(int contactId, ContactNoteViewModel viewModel, CancellationToken token = default)
{
    var currentUser = await userService.GetUserAsync(User);
    if (currentUser.Id == 0)
    {
        return Challenge();
    }

    // Resolve through the board-filtered read path first -- a contactId belonging to
    // another board must never reach AddNoteAsync's unconditional Add().
    var contact = await contactService.GetContactWithDetailsAsync(contactId, token);
    if (contact == null)
    {
        return NotFound();
    }

    if (!ModelState.IsValid)
    {
        TempData["Error"] = "Note text is required and cannot exceed 2000 characters.";
        return RedirectToAction(nameof(Details), new { id = contactId });
    }

    var note = new ContactNote
    {
        ContactId = contactId,
        Text = viewModel.Text,
        AuthorUserId = currentUser.Id
    };

    await contactService.AddNoteAsync(note, token);

    return RedirectToAction(nameof(Details), new { id = contactId });
}
```
Alternatively (and more robustly, so no future caller of `AddNoteAsync` can reintroduce this), have `ContactRepository.AddNoteAsync` itself verify the target contact resolves through the filtered `Contacts` set before inserting, mirroring the guard already present in `UpdateNoteAsync`.

## Warnings

### WR-01: Stale test comment documents filter behavior that was already removed

**File:** `QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs:198-201`

**Issue:** The comment above `Index_MobileActiveFilterNoMatches_RendersNoMatchHeading` states: "The shared filter helper falls back to the full visible list whenever a selection matches zero contacts, so the no-match branch is only reachable when the board has no visible contacts at all." This describes the *old*, buggy behavior of `ApplyTagFilter` that was explicitly removed by commit `2bee884c` ("fix(81-07): stop the tag filter from widening to the full list on zero matches"), which predates this test file. The current `ApplyTagFilter` (`ContactsController.cs`) does not fall back to anything — it returns an empty list on zero matches unconditionally:
```csharp
private static IList<Contact> ApplyTagFilter(IList<Contact> visibleContacts, IList<int> selectedTagIds)
{
    if (selectedTagIds.Count == 0) return visibleContacts;
    return visibleContacts.Where(c => c.Tags.Any(t => selectedTagIds.Contains(t.Id))).ToList();
}
```
This is also directly disproven by a sibling test in the same suite family — `ContactsTagsDesktopMarkupTests.Index_FilterMatchesNothing_RendersNoMatchBranchNotEmptyListBranch` — which seeds a board **with a visible contact** and still reaches the "no match" branch by filtering on an unrelated tag id, contradicting the claim that the branch is "only reachable when the board has no visible contacts at all." The test's assertions are still correct; only the comment is wrong. Left as-is, a future maintainer trusting this comment could reintroduce the fixed widening bug.

**Fix:** Update the comment to describe current behavior, e.g.:
```csharp
// ApplyTagFilter narrows strictly by intersection -- a selection matching zero contacts
// renders the "no contacts match your filters" branch regardless of how many contacts the
// board has (see Index_FilterMatchesNothing_RendersNoMatchBranchNotEmptyListBranch for a
// non-empty-board case). This test additionally covers the same branch on an empty board.
```

### WR-02: Concurrent creation of the same new tag name on one board can throw an unhandled `DbUpdateException`

**File:** `QuestBoard.Repository/ContactRepository.cs:74-124`, `QuestBoard.Repository/Entities/QuestBoardContext.cs:504-506`

**Issue:** `ReplaceContactTagsAsync` resolves a submitted name against the board's current tag vocabulary (`DbContext.ContactTags.ToListAsync()`) read at the start of the method, and creates a new `ContactTagEntity` in-memory when no match is found. If two DM-tier requests submit the same brand-new tag name for two different contacts on the same board concurrently (two DMs editing at once, or a double-submit), both requests can independently decide "no existing match" and both attempt to insert a `ContactTagEntity` with the same `(GroupId, Name)`. The unique index defined in `QuestBoardContext` (`HasIndex(t => new { t.GroupId, t.Name }).IsUnique()`) will reject the second `SaveChangesAsync`, and nothing in `ReplaceContactTagsAsync`, `ContactService`, or `ContactsController` catches `DbUpdateException`/`DbUpdateConcurrencyException` — the request surfaces as an unhandled 500 rather than a retry or a friendly "someone already added that tag" message.

This is a narrow race window (same board, same brand-new name, same moment) but it is a real robustness gap in an otherwise carefully-guarded reconciliation path, and the failure mode (unhandled exception, generic error page) is worse than necessary for what is a completely legitimate concurrent-use scenario.

**Fix:** Catch the unique-constraint violation in `ReplaceContactTagsAsync` and retry the resolve-against-current-vocabulary step once (the loser of the race will find the winner's row on retry and reuse it), or catch it in the controller and re-render with a "tag name already in use, please retry" validation error rather than letting it bubble to a 500.

### WR-03: `Index` fetches the full contact/notes/tags detail set twice in the same request

**File:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:41,684-696`

**Issue:** `Index` calls `contactService.GetAllContactsWithDetailsAsync(token)` directly (line 41) and then, two lines later, calls `GetVisibleTagVocabularyAsync(...)`, which internally calls `contactService.GetAllContactsWithDetailsAsync(token)` again (line 692) to build the tag vocabulary. Both calls run the identical `AsSplitQuery()` multi-include query (Notes + Tags + Category + CreatedByUser, plus a separate image-flags scalar query) against the same board, in the same request, with no caching between them. Beyond the duplicated cost, this is a maintainability hazard: the method's own doc comment claims "there is no second vocabulary query anywhere in this controller," which is true only for the *vocabulary* query specifically — the underlying contact fetch it depends on is in fact issued twice, and a future change to filter or project `GetAllContactsWithDetailsAsync` differently on one call site than the other would silently desync the visible-contacts list from the tag vocabulary it's supposed to be derived from.

**Fix:** Fetch `allContacts` once in `Index` and pass it into a vocabulary-building helper that takes the already-fetched list, rather than having `GetVisibleTagVocabularyAsync` re-fetch:
```csharp
var allContacts = await contactService.GetAllContactsWithDetailsAsync(token);
var visibleContacts = allContacts.Where(c => IsVisibleTo(c, currentUser.Id, includeHidden)).ToList();
var availableTags = mapper.Map<List<ContactTagViewModel>>(BuildTagVocabulary(visibleContacts));
```
and give `Create`/`Edit`/`PopulateTagSuggestionsAsync` their own single-fetch call (they don't have an existing `allContacts` to reuse, so they're unaffected).

## Info

### IN-01: Tag-name max length (30) is a magic number duplicated in three places

**File:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:605`, `QuestBoard.Repository/Entities/ContactTagEntity.cs:16-17`, `QuestBoard.Domain/Models/Contact.cs:76`

**Issue:** The 30-character tag-name cap is independently declared as `const int maxTagNameLength = 30` in `ContactsController.ValidateTagNameLengths`, as `[StringLength(30)]` on `ContactTagEntity.Name`, and as `[StringLength(30)]` on `ContactTag.Name`. All three currently agree, but nothing enforces that they stay in sync — a future change to one (e.g., relaxing the entity column to 50 chars via a migration) would silently leave the controller's client-facing validation message and cutoff at the old value.

**Fix:** Hoist the value into a single shared constant (e.g., on `ContactTag` or a small `ContactTagConstraints` type in `QuestBoard.Domain`) referenced by both the entity attribute and the controller's validation helper.

---

_Reviewed: 2026-08-31T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
