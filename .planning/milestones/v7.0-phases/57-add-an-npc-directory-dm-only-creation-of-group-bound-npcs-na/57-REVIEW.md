---
phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
reviewed: 2026-07-06T00:00:00Z
depth: standard
files_reviewed: 36
files_reviewed_list:
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Interfaces/IContactRepository.cs
  - QuestBoard.Domain/Interfaces/IContactService.cs
  - QuestBoard.Domain/Models/Contact.cs
  - QuestBoard.Domain/Services/ContactService.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/ContactRepository.cs
  - QuestBoard.Repository/Entities/ContactEntity.cs
  - QuestBoard.Repository/Entities/ContactImageEntity.cs
  - QuestBoard.Repository/Entities/ContactNoteEntity.cs
  - QuestBoard.Repository/Entities/QuestBoardContext.cs
  - QuestBoard.Repository/Extensions/ServiceExtensions.cs
  - QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.Designer.cs
  - QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
  - QuestBoard.Service/Automapper/ViewModelProfile.cs
  - QuestBoard.Service/Constants/SessionKeys.cs
  - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
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
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/wwwroot/css/contact-detail.mobile.css
  - QuestBoard.Service/wwwroot/css/contact-form.mobile.css
  - QuestBoard.Service/wwwroot/css/contacts.css
  - QuestBoard.Service/wwwroot/css/contacts.mobile.css
  - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
findings:
  critical: 1
  warning: 3
  info: 2
  total: 6
status: issues_found
---

# Phase 57: Code Review Report

**Reviewed:** 2026-07-06
**Depth:** standard
**Files Reviewed:** 36
**Status:** issues_found

## Summary

Reviewed the Contacts (NPC directory) feature end-to-end: domain model/service, repository, EF
entities/migration, AutoMapper profiles (both directions), controller, view models, Razor views
(desktop + mobile), CSS, and tests.

The previously-identified `ContactViewModel.ContactImage` / `Contact.ContactImageData` AutoMapper
mismatch in `ViewModelProfile.cs` has been verified as correctly and completely fixed: both
`CreateMap<Contact, ContactViewModel>()` and `CreateMap<ContactViewModel, Contact>()` now carry
explicit `.ForMember(...)` mappings between `ContactImage` and `ContactImageData`, and the
`EntityProfile.cs` mappings for `Contact <-> ContactEntity.ProfileImage` were already correct
before this fix and remain correct. Traced the full round-trip for both Create (viewModel ->
Contact -> ContactEntity -> ContactImageEntity) and Edit (ContactEntity -> Contact ->
existingContact.ContactImageData retained-or-replaced -> UpdateProfileImageAsync) and both are
now consistent. No new issue was introduced by the fix.

The main new issue found is a hidden-contact visibility bypass in `GetContactImage`: unlike
`Details`/`Index`, this action does not apply the `IsVisibleTo` check, so any authenticated user
who can guess or enumerate a hidden contact's numeric ID can fetch its profile image directly,
even though the contact itself is otherwise correctly hidden from them. Additional lower-severity
issues: missing server-side validation on the note text fields (relies entirely on client-side
`maxlength`), and a couple of quality/dead-code items.

## Critical Issues

### CR-01: `GetContactImage` bypasses the hidden-contact visibility check, leaking hidden NPC portraits

**File:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:305-315`
**Issue:** `Index` and `Details` both gate visibility of a hidden `Contact` through
`IsVisibleTo(contact, currentUserId, includeHidden)` (creator sees own hidden contact; revealed
contacts are visible to all; DM-tier with the "Show Hidden" toggle sees all; plain Players never
see hidden contacts). `GetContactImage` skips this check entirely — it only relies on the
group-scoped EF query filter (`ContactEntity`/`ContactImageEntity` `HasQueryFilter`), which lets
any authenticated member of the group fetch the raw image bytes for a hidden contact by requesting
`/Contacts/GetContactImage/{id}` directly, as long as they can guess or enumerate a valid id in
their own group (ids are small sequential integers). This defeats the "hidden until revealed"
guarantee the feature is built around (D-12/D-13/D-15 in the integration test suite), and also
allows a caller to infer the existence of a hidden contact (200 vs 404) without ever seeing it on
Index/Details.
**Fix:**
```csharp
[HttpGet]
public async Task<IActionResult> GetContactImage(int id, CancellationToken token = default)
{
    var contact = await contactService.GetContactWithDetailsAsync(id, token);
    if (contact == null)
    {
        return NotFound();
    }

    var currentUser = await userService.GetUserAsync(User);
    var viewerIsDmTier = currentUser.Id != 0 && await IsDmTierAsync();
    var includeHidden = viewerIsDmTier && ReadShowHiddenToggle();

    if (!IsVisibleTo(contact, currentUser.Id, includeHidden))
    {
        return NotFound();
    }

    var image = await contactService.GetContactImageAsync(id, token);
    if (image == null)
    {
        return NotFound();
    }

    return File(image, DetectImageMimeType(image));
}
```
Note this adds an extra `GetContactWithDetailsAsync` round-trip per image fetch; if that is a
concern, add a cheaper `IContactRepository`/`IContactService` method that returns just
`(bool isRevealed, int createdByUserId)` for the visibility check instead of the full details
projection.

## Warnings

### WR-01: `AddNote`/`EditNote` never check `ModelState.IsValid`, so an over-length note text reaches the database unvalidated

**File:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:260-294`
**Issue:** `ContactNoteViewModel.Text` carries `[Required]` and `[StringLength(2000)]`, but neither
`AddNote` nor `EditNote` checks `ModelState.IsValid` before constructing a `ContactNote` and
calling `AddNoteAsync`/`UpdateNoteAsync`. The view's `<textarea maxlength="2000">` is
client-side-only and trivially bypassed (disable JS, curl the endpoint, browser dev tools). A note
longer than 2000 characters (or an empty note, since `[Required]` is likewise unenforced) will be
passed straight through to `ContactNoteEntity.Text` (`nvarchar(2000)`), causing SQL Server to throw
a truncation error on `SaveChangesAsync`, which surfaces as an unhandled 500 rather than a
validation message. This differs from `Create`/`Edit` on the same controller, both of which do
check `ModelState.IsValid`.
**Fix:**
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

    if (!ModelState.IsValid)
    {
        return RedirectToAction(nameof(Details), new { id = contactId });
        // or re-render Details with the validation errors, matching the Edit/Create UX
    }

    var note = new ContactNote { ContactId = contactId, Text = viewModel.Text, AuthorUserId = currentUser.Id };
    await contactService.AddNoteAsync(note, token);
    return RedirectToAction(nameof(Details), new { id = contactId });
}
```
Apply the same guard to `EditNote`.

### WR-02: `ContactService.UpdateAsync` always rewrites the profile image row, even when the image did not change

**File:** `QuestBoard.Domain/Services/ContactService.cs:22-26`
**Issue:** `UpdateAsync` unconditionally calls `repository.UpdateProfileImageAsync(model.Id,
model.ContactImageData, token)` before `repository.UpdateAsync(model, token)`. Because the
controller's `Edit` action leaves `existingContact.ContactImageData` populated with whatever
`GetContactWithDetailsAsync` originally loaded when no new file is uploaded, this doesn't lose
data — but it does mean every plain metadata edit (name/description/etc.) and every
`ToggleReveal` call performs a redundant `SaveChangesAsync` write of the (unchanged) image bytes.
Beyond the extra write, this couples `ContactService.UpdateAsync` to always requiring a
fully-populated `ContactImageData` on every model passed in; a future caller that builds a partial
`Contact` for `UpdateAsync` (e.g., a bulk-metadata-only update helper) would silently wipe the
image because `ContactImageData` would be `null` and `UpdateProfileImageAsync` treats `null` as
"clear the image." This implicit "you must round-trip the full image every time" contract is not
documented on `IContactService.UpdateAsync` (inherited from `IBaseService<Contact>`, whose XML doc
says nothing about image semantics).
**Fix:** Only touch the image path when the caller actually intends to change it — e.g. add an
explicit `bool imageChanged` flag/overload, or have the controller call
`UpdateProfileImageAsync` directly only inside the `if (viewModel.ContactImageFile != null && ...)`
branch, and have `UpdateAsync` on the service only persist core fields:
```csharp
public override async Task UpdateAsync(Contact model, CancellationToken token = default)
{
    await repository.UpdateAsync(model, token);
}

// controller Edit POST, inside the image-uploaded branch only:
await contactService.UpdateProfileImageAsync(id, newImageBytes, token); // new IContactService method
```

### WR-03: `EditNote` ignores a `contactId` that does not match the note's actual owning contact

**File:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:282-294`
**Issue:** `EditNote` builds a `ContactNote { Id = id, ContactId = contactId, Text = ... }` and
calls `UpdateNoteAsync`, but `ContactRepository.UpdateNoteAsync` looks up the entity purely by
`note.Id` and never checks that `entity.ContactId == note.ContactId`. If a caller passes a valid
`id` for a note on Contact A together with a different `contactId` (Contact B, e.g. a stale form
or a manually crafted request), the edit silently succeeds and the user is redirected to Contact
B's Details page even though the note that changed still belongs to Contact A. This is confusing
UX at minimum, and a mismatch nobody will notice because there is no error path for it.
**Fix:**
```csharp
public async Task<IActionResult> EditNote(int id, int contactId, ContactNoteViewModel viewModel, CancellationToken token = default)
{
    var note = new ContactNote { Id = id, ContactId = contactId, Text = viewModel.Text };
    await contactService.UpdateNoteAsync(note, token);
    ...
}
```
should instead verify the note belongs to `contactId` before/while updating (e.g. have
`UpdateNoteAsync` filter by both `Id` and `ContactId` and no-op otherwise), consistent with how
`DeleteNote` should behave too.

## Info

### IN-01: Duplicate MIME-type/size validation logic between `ContactViewModel` attributes and controller inline checks

**File:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:100-120, 180-202`
**Issue:** `ContactViewModel` already declares `[MaxFileSizeAttribute(5 MB)]` and
`[AllowedExtensionsAttribute(...)]` on `ContactImageFile`, which run as part of `ModelState`
validation. The `Create` and `Edit` POST actions then re-implement the same checks manually
(`allowedMimeTypes`/`maxFileSizeBytes`) after `ModelState.IsValid` has already passed. This is
harmless (both checks agree) but is duplicated logic in two places that must be kept in sync by
hand; a future change to the size/type policy in one location and not the other would silently
diverge.
**Fix:** Drop the duplicate manual checks and rely solely on the `ModelState` validation (which
already returns to the view with field-level error messages), or keep the manual checks and remove
the attributes — but not both.

### IN-02: `GetContactImage` MIME sniffing does not fall back safely for corrupt/non-image data

**File:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:317-320`
**Issue:** `DetectImageMimeType` defaults to `"image/jpeg"` for any byte sequence that isn't a PNG
or GIF signature match, including arbitrary/corrupt data that somehow ended up in the column. This
is a minor labeling correctness issue (not a security issue, since content-type upload validation
already restricts what can be stored) but will mislabel e.g. a truncated/corrupted JPEG-that-is-
actually-something-else as `image/jpeg` regardless of its real content.
**Fix:** Low priority; if stricter correctness is desired, add a JPEG signature check
(`0xFF, 0xD8`) as a fourth branch and only default to a generic `application/octet-stream` when
none of the three signatures match.

---

_Reviewed: 2026-07-06_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
