---
phase: 70-dm-profile-shop-fields
reviewed: 2026-07-10T00:00:00Z
depth: standard
files_reviewed: 13
files_reviewed_list:
  - QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml
  - QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml
  - QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml
  - QuestBoard.Service/Views/DungeonMaster/Profile.cshtml
  - QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml
  - QuestBoard.Service/Views/Shop/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Shop/Index.cshtml
  - QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml
  - QuestBoard.Service/Views/ShopManagement/Create.cshtml
  - QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/ShopManagement/Edit.cshtml
  - QuestBoard.Service/Views/ShopManagement/Index.cshtml
  - QuestBoard.Service/wwwroot/js/markdown-editor.js
findings:
  critical: 0
  warning: 2
  info: 5
  total: 7
status: issues_found
---

# Phase 70: Code Review Report

**Reviewed:** 2026-07-10T00:00:00Z
**Depth:** standard
**Files Reviewed:** 13
**Status:** issues_found

## Summary

This phase wires DM Profile Bio and Shop Item Description into the shared Markdown editor/renderer
pipeline (`_MarkdownEditor.cshtml` for authoring, `Html.Markdown()` for display, `IMarkdownService`
for plain-text teasers). The core sanitization path (`MarkdownService.RenderToHtml` →
`HtmlSanitizer`) is sound and correctly used everywhere raw markdown is rendered — no XSS or
injection issues were found. The `markdown-editor.js` change (stashing the EasyMDE instance on
`textarea.easyMDE`) is a legitimate, well-reasoned fix that the new bespoke submit-validation
scripts in `ShopManagement/Create.cshtml` and `ShopManagement/Edit.cshtml` correctly depend on.

Two real defects were found, both introduced by this phase: every view that calls
`@Html.Markdown(...)` for the two new fields double-wraps the result in a redundant
`.markdown-content` div (the helper already emits one), diverging from how every other
`@Html.Markdown()` call site in the codebase (Quest, Character, Contacts, QuestLog) uses it; and
the DM Bio field silently lost its `maxlength="2000"` client-side enforcement when the plain
`<textarea>` was replaced by the `_MarkdownEditor` partial, leaving the 2000-character limit
enforced only server-side. A handful of lower-severity/pre-existing issues incidentally surfaced
in the reviewed files are noted under Info.

## Warnings

### WR-01: Redundant nested `.markdown-content` wrapper div around `@Html.Markdown()` output

**File:** `QuestBoard.Service/Views/DungeonMaster/Profile.cshtml:52-54`
**Also in:**
- `QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml:48-50`
- `QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml:137-139`
- `QuestBoard.Service/Views/Shop/Details.Mobile.cshtml:22-24`

**Issue:** `HtmlHelperExtensions.Markdown()` already wraps its sanitized output in
`<div class="markdown-content">...</div>` (`QuestBoard.Service/Extensions/HtmlHelperExtensions.cs:23`),
explicitly documented as giving "every rendered field a single, consistent styling hook." All four
call sites touched by this phase wrap the helper call in their *own* additional
`<div class="markdown-content ...">` element, producing:

```html
<div class="markdown-content dm-profile-bio-text">
    <div class="markdown-content">
        <p>...</p>
    </div>
</div>
```

Every other `@Html.Markdown()` call site in the codebase (`Quest/Details.cshtml`,
`Quest/Manage.cshtml`, `Character/Details.cshtml`, `Contacts/Details.cshtml`,
`QuestLog/Details.cshtml`, etc.) does **not** re-wrap the call in `.markdown-content` — they use a
different, purpose-specific wrapper class (`.quest-description-box`, `.character-info-row`,
`.card-body`) or no wrapper at all. This phase's four call sites are the only ones in the codebase
that duplicate the class. It doesn't break the current CSS (the one selector that cares about
`.markdown-content` being a direct child — `.modern-card-body > .markdown-content h1` — still
matches because the heading remains a descendant, just one level deeper), but it's dead, incorrect
markup that will bite the next person who writes a `.markdown-content > *` or
`.markdown-content:first-child` rule, and it's inconsistent with the established, documented
convention.

**Fix:** Drop the redundant outer div and apply any extra styling class directly, e.g.:
```cshtml
@* Profile.cshtml *@
@Html.Markdown(Model.Bio)

@* Profile.Mobile.cshtml, if the extra class is still needed *@
<div class="dm-profile-bio-text">
    @Html.Markdown(Model.Bio)
</div>
```

### WR-02: DM Bio field lost its 2000-character client-side limit when migrated to the Markdown editor

**File:** `QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml:46-53`
**Also in:** `QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml:48-55`

**Issue:** The original markup was:
```cshtml
<textarea asp-for="Bio" class="form-control" rows="6" maxlength="2000"></textarea>
```
The replacement renders via `_MarkdownEditor.cshtml`, whose `<textarea>` has no `maxlength`
attribute and no equivalent EasyMDE character-limit option:
```cshtml
<textarea name="@Model.FieldName" id="@elementId" class="form-control markdown-editor-textarea" rows="4" placeholder="@Model.Placeholder" required="@Model.Required">@Model.Value</textarea>
```
`EditDMProfileViewModel.Bio` still enforces `[StringLength(2000)]` server-side, so no invalid data
can be persisted, but a user can now type well past 2000 characters with zero feedback until they
submit the form and get bounced back by `asp-validation-summary`. The "max 2000 characters" helper
text right below the editor (`EditProfile.cshtml:54-56`) is no longer backed by any client-side
enforcement or live counter.

**Fix:** Either add a `maxlength` (or a JS char-count/truncate handler tied to
`easyMDE.codemirror.on('change', ...)`) to `_MarkdownEditor.cshtml`, gated behind an optional
`MaxLength` property on `MarkdownEditorViewModel`, or add a small live counter component to the
Bio field specifically:
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Bio",
       Value = Model.Bio,
       Label = "Bio",
       Required = false,
       MaxLength = 2000,
   }); }
```

## Info

### IN-01: Stray unmatched `</script>` closing tag

**File:** `QuestBoard.Service/Views/ShopManagement/Edit.cshtml:334`
**Issue:** The file ends with `</style>` immediately followed by a second, unmatched `</script>`
(the actual `<script>` block was already closed at line 316). Browsers silently ignore an
unmatched closing tag so this has no runtime effect, but it's invalid markup that predates this
phase and was left untouched by it.
**Fix:** Remove the stray `</script>` at line 334.

### IN-02: Duplicated plain-text-teaser truncation logic

**File:** `QuestBoard.Service/Views/Shop/Index.cshtml:307-312`
**Also in:** `QuestBoard.Service/Views/ShopManagement/Index.cshtml:8-14`
**Issue:** Both views independently added near-identical "ExtractPlainText + truncate at N chars +
append `...`" logic (120 chars in `Shop/Index.cshtml`, 50 chars in `ShopManagement/Index.cshtml`)
in this phase. Same intent, duplicated implementation.
**Fix:** Extract a shared `Truncate(string text, int maxLength)` helper (e.g. on `IMarkdownService`
or a small Razor/string extension) so both views call the same code.

### IN-03: `label for=` / input `id=` mismatch for the profile photo control (pre-existing)

**File:** `QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml:26,35-37`
**Also in:** `QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml:29,38-40`
**Issue:** `<label asp-for="ProfilePictureFile">` auto-generates `for="ProfilePictureFile"`, but the
sibling `<input asp-for="ProfilePictureFile" ... id="dmProfilePictureInput">` overrides its `id` to
`dmProfilePictureInput`. The label's `for` no longer matches the input's actual `id`, so clicking
the label won't focus/activate the file input (an accessibility regression, not introduced by this
phase but still present in a file reviewed here).
**Fix:** `<label for="dmProfilePictureInput" ...>` or drop the explicit `id` override on the input.

### IN-04: Inconsistent price-suggestion button state and Equipment-type carve-out between Create and Edit (pre-existing)

**File:** `QuestBoard.Service/Views/ShopManagement/Edit.cshtml:87-92,246-253`
**Issue:** `randomPriceBtn`/`calculatedPriceBtn` have no `disabled` attribute here (unlike
`Create.cshtml`, which starts them `disabled` until a rarity is chosen), and `updatePriceSuggestion()`
special-cases `itemType == 'Equipment'` to blank the suggestion text regardless of the selected
rarity — a branch that doesn't exist in `Create.cshtml`'s otherwise-identical function. Neither is
new in this phase, but the behavior now diverges visibly between the two forms for the same field.
**Fix:** Align the two `updatePriceSuggestion()` implementations (or extract one shared script), and
add the same initial `disabled` state to `Edit.cshtml`'s buttons.

### IN-05: Silently swallowed fetch errors in the shop item detail modal (pre-existing)

**File:** `QuestBoard.Service/Views/Shop/Index.cshtml:460-463`
**Issue:** `.catch(() => {})` — if `GET /Shop/Details/{id}?isModal=true` fails, the modal opens
with an empty body and no error indication to the user. Not introduced by this phase (the fetch
call itself is untouched), but worth calling out since the modal now also carries the markdown
description content added here.
**Fix:** Render a fallback error message in `#itemDetailsBody` on failure, e.g.
`.catch(() => { document.getElementById('itemDetailsBody').innerHTML = '<p class="text-danger">Failed to load item details.</p>'; })`.

---

_Reviewed: 2026-07-10T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
