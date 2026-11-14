---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
reviewed: 2026-11-14T00:00:00Z
depth: standard
files_reviewed: 80
files_reviewed_list:
  - QuestBoard.Repository/QuestRepository.cs
  - QuestBoard.Service/ViewModels/Shared/CharacterAvatarViewModel.cs
  - QuestBoard.Service/Views/Characters/Create.cshtml
  - QuestBoard.Service/Views/Characters/Details.cshtml
  - QuestBoard.Service/Views/Characters/Edit.cshtml
  - QuestBoard.Service/Views/Characters/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Characters/Index.cshtml
  - QuestBoard.Service/Views/Characters/_CharacterClassRows.cshtml
  - QuestBoard.Service/Views/ContactCategoryManagement/Edit.cshtml
  - QuestBoard.Service/Views/ContactCategoryManagement/Manage.Mobile.cshtml
  - QuestBoard.Service/Views/ContactCategoryManagement/Manage.cshtml
  - QuestBoard.Service/Views/Contacts/Create.cshtml
  - QuestBoard.Service/Views/Contacts/Details.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.cshtml
  - QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Index.cshtml
  - QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml
  - QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml
  - QuestBoard.Service/Views/DungeonMaster/Profile.cshtml
  - QuestBoard.Service/Views/Players/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Players/Index.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Quest/Manage.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.cshtml
  - QuestBoard.Service/Views/Shared/_CharacterAvatar.cshtml
  - QuestBoard.Service/Views/Shared/_ImageCropDialog.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/wwwroot/css/character-detail.css
  - QuestBoard.Service/wwwroot/css/character-form.css
  - QuestBoard.Service/wwwroot/css/characters.css
  - QuestBoard.Service/wwwroot/css/characters.mobile.css
  - QuestBoard.Service/wwwroot/css/components.css
  - QuestBoard.Service/wwwroot/css/contact-categories.css
  - QuestBoard.Service/wwwroot/css/contact-categories.mobile.css
  - QuestBoard.Service/wwwroot/css/contact-detail.css
  - QuestBoard.Service/wwwroot/css/contact-form.css
  - QuestBoard.Service/wwwroot/css/contacts.css
  - QuestBoard.Service/wwwroot/css/contacts.mobile.css
  - QuestBoard.Service/wwwroot/css/dm-profile-form.css
  - QuestBoard.Service/wwwroot/css/dm-profile.css
  - QuestBoard.Service/wwwroot/css/dm-profile.mobile.css
  - QuestBoard.Service/wwwroot/css/image-crop.css
  - QuestBoard.Service/wwwroot/css/member-cards.css
  - QuestBoard.Service/wwwroot/css/players.css
  - QuestBoard.Service/wwwroot/css/players.mobile.css
  - QuestBoard.Service/wwwroot/css/tagify-vendor.css
  - QuestBoard.Service/wwwroot/js/character-form.js
  - QuestBoard.Service/wwwroot/js/confirm-submit.js
  - QuestBoard.Service/wwwroot/js/contact-notes.js
  - QuestBoard.Service/wwwroot/js/contact-tags.js
  - QuestBoard.Service/wwwroot/js/image-crop.js
  - QuestBoard.UnitTests/Repository/QuestRepositoryPictureFlagTests.cs
  - QuestBoard.IntegrationTests/Tests/CharacterAvatarRenderTests.cs
  - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/ContactsIndexRenderTests.cs
findings:
  critical: 0
  warning: 2
  info: 6
  total: 8
status: issues_found
---

# Phase 98: Code Review Report

**Reviewed:** 2026-11-14
**Depth:** standard
**Files Reviewed:** 80 (51 product files read in full; test files skimmed for tests that cannot fail)
**Status:** issues_found

## Summary

No blocker found. The areas the brief singled out hold up:

- **Encoding in data attributes.** `data-whitelist` is `JsonSerializer.Serialize` output inside a Razor attribute, so it is HTML-attribute-encoded. `contact-tags.js` reads it with `JSON.parse`, filters to strings and falls back to `[]` on a bad value. A null `AvailableTagNames` serialises to `null` and also falls back safely. `data-confirm-message` is Razor-encoded and handed to `confirm()` as plain text through `dataset`. No injection path.
- **CSRF.** Every state-changing form uses `asp-action` with `method="post"` (the tag helper emits the token) or an explicit `AntiForgeryToken()`. The notes forms, delete forms and category reorder forms are all covered.
- **Form binding after the merges.** The control names `Classes[i].Class`, `Classes[i].ClassLevel`, `CroppedPictureFile`, `ProfilePictureFile`, `ContactImageFile`, `TagsInput`, `Bio` and `Description` match the pre-merge views, which I checked by diffing the names. `character-form.js` renumbers `name`, `id` and `for` from zero after every add or remove, so the binder never meets a gap.
- **Picture-flag query.**
  - `ApplyCharacterPictureFlagsAsync` only runs on the two loaders whose pages render avatars (`GetQuestWithDetailsAsync`, `GetQuestWithManageViewDetailsAsync`).
  - It reads `DbContext.Characters`, so the active-board filter applies the same way as the character Include.
  - A SuperAdmin cross-board view gets no flags, but it gets no characters either, so nothing regresses.
  - `GetCroppedPicture` falls back to the original image, so "has an image row" is the correct flag.
- **Planning-ID rule.** No planning references in the scanned source files.
- **Unused CSS tokens.** Every `var(--qb-*)` used in the phase sheets is defined.

What remains is one real stale-state bug in the crop module, one accessibility regression on the phone Contacts page, and a handful of smaller quality items.

## Warnings

### WR-01: A rejected or cleared second file pick leaves the previous crop in the hidden input and uploads it

**File:** `QuestBoard.Service/wwwroot/js/image-crop.js:297-334`
**Issue:** `hiddenInput` is a module-level variable holding the previous crop. The `change` handler has several exits that clear the visible file input but never clear the hidden one:
- A new file over 5 MB (line 304-310).
- A new file of a disallowed type (line 311-315).
- A decode failure in `prepareImageForCropper` (line 326-332).
- Chrome clearing the selection when the user cancels the file chooser. The `!file` early return at line 299-301 does nothing.

Sequence: the user picks A, the crop dialog fills `CroppedPictureFile` from A, the user picks B, and B is rejected. The error chip shows, the visible input is empty, and the form still posts A's crop in `CroppedPictureFile`. The user believes nothing is being uploaded, or that the new photo was refused. The server takes the cropped file as the new picture. The GIF branch (line 316-322) already handles this correctly through `resetCropState()`.
**Fix:** Clear the hidden output whenever the visible input is emptied or rejected. Resolve `hiddenInput` first, so the clear works even when it was never opened.
```js
function discardPendingCrop(input) {
    hiddenInput = document.querySelector(
        'input[type="file"][name="' + CSS.escape(configOf(input).outputName) + '"]');
    resetCropState();
}

input.addEventListener('change', async () => {
    const file = input.files && input.files[0];
    if (!file) {
        discardPendingCrop(input);   // chooser cancelled or cleared
        return;
    }
    clearError();
    if (file.size > MAX_FILE_BYTES) {
        discardPendingCrop(input);
        // ...showError, input.value = '', return
    }
    // same one-line call before the type check and in the decode-failure catch
```

### WR-02: Phone Contacts page has no h1 when the board has no categories

**File:** `QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml:242-247`
**Issue:** The title card's `<h1>` is rendered only inside `@if (Model.HasCategories)`. On a board with no categories, the phone page has no h1 at all. Only the section `<h2>` "Contacts" (line 345) remains, so heading navigation starts at an h2. The desktop view always renders the h1, and the page title bar is also missing on the phone for these boards. `ContactsIndexRenderTests` asserts "one h1" only on a seeded board that has categories, so no test covers the no-category phone case. This is a regression against the single-h1 convention the rest of the phase follows.
**Fix:** Always render the title card. If a doubled "Contacts" heading in the ungrouped section is the concern, drop the ungrouped section's own header on the phone instead:
```cshtml
<div class="card modern-card mb-3">
    <div class="card-header modern-card-header">
        <h1 class="mb-0">Contacts</h1>
    </div>
    @if (Model.ViewerIsDmTier) { ... }
</div>
```
Add a phone test with no categories that asserts exactly one `<h1>`.

## Info

### IN-01: Empty-state heading is an h2 nested inside a section whose title is already an h2

**File:** `QuestBoard.Service/Views/Characters/Index.cshtml:266,329`, `Characters/Index.Mobile.cshtml:413,468`, `DungeonMaster/Profile.cshtml:199`, `DungeonMaster/Profile.Mobile.cshtml:312`, `Players/Index.cshtml:62,100`, `Players/Index.Mobile.cshtml:159,184`
**Issue:** Each empty state sits inside a `<section aria-labelledby>` whose title is an `h2`, then adds a sibling-level `h2` (`empty-state-heading`). This flattens the outline: "No Characters Yet" reads as a peer of "My Characters", not a child. `Contacts/Details.cshtml:452` correctly uses `h3`.
**Fix:** Use `<h3 class="empty-state-heading">` where the empty state is inside a titled section. `ContactsIndexRenderTests` pins `h2` for the page-level empty states, which are not inside a section, so those stay as they are.

### IN-02: Contact tag filter group has no accessible name (desktop)

**File:** `QuestBoard.Service/Views/Contacts/Index.cshtml:100-109`
**Issue:** "Tags:" is a bare `<span>` beside a run of checkboxes, so the group is announced with no name. The phone drawer has the same structure under an `h5` title.
**Fix:** Use `<fieldset class="contact-filter-row"><legend class="contact-filter-label">Tags</legend>…</fieldset>`, or `role="group" aria-labelledby` pointing at the span.

### IN-03: Doubled antiforgery token on forms that already get one from the tag helper

**File:** `QuestBoard.Service/Views/ContactCategoryManagement/Edit.cshtml:19`, `Manage.cshtml:63,102,109,125`, `Manage.Mobile.cshtml:173,202,209,223`
**Issue:** `<form asp-action=… method="post">` already emits `__RequestVerificationToken`. The extra `@Html.AntiForgeryToken()` renders a second identical hidden input per form. This is harmless but redundant, and it duplicates the field name in the DOM.
**Fix:** Remove the explicit `@Html.AntiForgeryToken()` calls.

### IN-04: Non-descriptive alt text on the current-picture thumbnails and doubled names beside undecorated avatars

**File:** `QuestBoard.Service/Views/Characters/Edit.cshtml:228`, `Contacts/Edit.cshtml:154`, `Quest/Details.cshtml` (the three `_CharacterAvatar` calls), `Quest/Manage.cshtml` (the two calls)
**Issue:**
- The edit-form thumbnails use `alt="Current"`, which names nothing. `alt="Current picture of @Model.Name"` is more useful, and the DM edit form already uses a descriptive alt.
- The Quest Details and Manage calls leave `Decorative` false while the character name is printed in the next span, so a screen reader announces the name twice. The partial's `Decorative` flag exists for exactly this case, and `QuestLog/Details` sets it.

**Fix:** Pass `Decorative = true` where the name is printed beside the avatar, and improve the thumbnail alt text.

### IN-05: Unguarded `null` textarea in the notes editor

**File:** `QuestBoard.Service/wwwroot/js/contact-notes.js:35-43`
**Issue:** `expand()` queries `.markdown-editor-textarea` and passes it straight to `window.initMarkdownEditor`. If the shared Markdown script failed to load (it is a separate classic script) or the textarea is absent, the click handler throws an uncaught `TypeError`. The slip has already had its text hidden and its form shown by then, so the user is left with an empty, non-functional slip.
**Fix:** Check `textarea && typeof window.initMarkdownEditor === 'function'` before hiding the text. Otherwise leave the slip untouched.

### IN-06: Picture-only avatars outside the shared partial have no load-failure fallback

**File:** `QuestBoard.Service/Views/Characters/Index.cshtml:227,293`, `Contacts/Index.cshtml:16`, `Contacts/Index.Mobile.cshtml:201`
**Issue:** The new has-picture flag stops requests for pictures that do not exist, but a stored picture that later fails to load (deleted row, transient 5xx) shows a broken-image box on these cards and rows. Only `_CharacterAvatar` pairs the `<img>` with a hidden placeholder that `avatar-fallback.js` reveals. The contacts avatar on the phone uses the same `avatar-32` class but has no placeholder sibling.
**Fix:** Optional. Reuse the avatar markup (or a shared contact avatar partial) for the contact rows, or load `avatar-fallback.js` on these pages.

---

_Reviewed: 2026-11-14_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
