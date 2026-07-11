---
phase: 68-character-fields
reviewed: 2026-07-10T00:00:00Z
depth: standard
files_reviewed: 8
files_reviewed_list:
  - QuestBoard.Service/Views/Characters/Create.cshtml
  - QuestBoard.Service/Views/Characters/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Characters/Edit.cshtml
  - QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml
  - QuestBoard.Service/wwwroot/css/markdown-editor.css
  - QuestBoard.Service/Views/Characters/Details.cshtml
  - QuestBoard.Service/Views/Characters/Details.Mobile.cshtml
  - QuestBoard.Service/wwwroot/css/character-detail.mobile.css
findings:
  critical: 0
  warning: 5
  info: 1
  total: 6
status: issues_found
---

# Phase 68: Code Review Report

**Reviewed:** 2026-07-10T00:00:00Z
**Depth:** standard
**Files Reviewed:** 8
**Status:** issues_found

## Summary

Reviewed the Character create/edit/details views (desktop + mobile) and the two new CSS files added for markdown editing/rendering support. `Html.Markdown()` and `_MarkdownEditor.cshtml` (not in scope, read only for context) are called correctly and safely — Razor auto-encodes the raw textarea value on the write side, and rendering is routed through a single sanitizing helper on the read side. `SheetLink` is protected server-side by `[Url]`, so `href` injection via that field is not exploitable.

The real problems are in the four near-duplicate create/edit view files: one of the four "add class" script blocks lost the empty-list guard that the other three have, producing a genuine index-collision bug for an edge case; one entire branch of `Create.cshtml` is provably unreachable given how the controller renders views; and one of the two new CSS files documents itself as mobile-only but isn't actually scoped that way. There's also a stale CSS rule/comment left behind by the Description/Backstory migration to Markdown rendering. No critical/security-severity issues were found in the reviewed files.

## Warnings

### WR-01: `classIndex` initialization drops the empty-classes guard in Edit.cshtml, causing duplicate `Classes[0]` field names

**File:** `QuestBoard.Service/Views/Characters/Edit.cshtml:199`

**Issue:** The Razor loop that renders existing class rows guards against an empty `Model.Classes` list:

```csharp
@for (var i = 0; i < (Model.Classes.Any() ? Model.Classes.Count : 1); i++)
```

When `Model.Classes` is empty, the loop still renders **one** row (`i = 0`) using a synthesized default (`new CharacterClassViewModel { ClassLevel = 1 }`, line 77), with input names `Classes[0].Class` / `Classes[0].ClassLevel`.

The `add-class` script block's counter seed does **not** apply the same guard:

```javascript
let classIndex = @Model.Classes.Count;   // = 0 when Model.Classes is empty
```

So the very next class row a user adds via "Add Another Class" is also emitted with `Classes[0].*` names — colliding with the already-rendered default row. The form then submits two sets of fields both named `Classes[0].Class` / `Classes[0].ClassLevel`, which the ASP.NET Core model binder cannot correctly resolve into two distinct list entries (best case: a binding/validation error is shown and the edit silently fails to save; worst case: one of the two submitted class rows is dropped).

Compare the three sibling files, which all apply the guard correctly:
- `Create.cshtml:202` — `let classIndex = @(Model.Classes.Any() ? Model.Classes.Count : 1);`
- `Create.Mobile.cshtml:181` — same guarded pattern
- `Edit.Mobile.cshtml:188` — same guarded pattern

Only `Edit.cshtml:199` is missing the ternary. This is a low-frequency edge case in practice (the UI never lets a user delete row 0, since the remove button is only rendered for `i > 0` — see line 95), but it is reachable for a character whose stored `Classes` collection is empty when the Edit page is requested (e.g. legacy/imported data, or any future code path that clears the collection).

**Fix:**
```csharp
let classIndex = @(Model.Classes.Any() ? Model.Classes.Count : 1);
```

---

### WR-02: `isEdit` branch in Create.cshtml is dead code — this view is never rendered with `Model.Id > 0`

**File:** `QuestBoard.Service/Views/Characters/Create.cshtml:6, 14-16, 19-23, 31, 139-140`

**Issue:** `Create.cshtml` computes:

```csharp
var isEdit = Model.Id > 0;
```

and branches extensively on it (icon, form `asp-action`, whether to render the hidden `Id` field, whether to show the existing profile picture, the submit button's icon/label). But `CharactersController` never renders `Create.cshtml` for anything but the two `Create` actions:

```csharp
[HttpGet]  public async Task<IActionResult> Create(...) { ... return View(viewModel); }
[HttpPost] public async Task<IActionResult> Create(CharacterViewModel viewModel, ...) { ... return View(viewModel); }
```

Both calls are unqualified `View(viewModel)`, which resolves to the `Create` view by convention — there is no `View("Create", ...)` call anywhere else in the codebase that could route an edit-context model into this file. The GET action's `viewModel` is always freshly constructed with no `Id` set, and the POST action's `viewModel` is bound from the Create form, which (because of the `isEdit`-dead branch) never even emits a hidden `Id` input. So `Model.Id` is always `0` on every render of this view, and `isEdit` is always `false`.

`Edit.cshtml` is a separate file that already exists specifically to serve the edit case (its own header comment says "Use the same form as Create, just with different title and action") — so this isn't a case of `Create.cshtml` legitimately being shared; the `isEdit` logic is leftover scaffolding that will never execute and misleads future maintainers into thinking this file might be reused for edits.

**Fix:** Remove the `isEdit` variable and all of its dependent branches from `Create.cshtml`, hard-coding the create-only behavior (matching what `Create.Mobile.cshtml` already does, which has no `isEdit` logic at all).

---

### WR-03: `markdown-editor.css` documents itself as a mobile-only fix but is loaded — and applies — on desktop too

**File:** `QuestBoard.Service/wwwroot/css/markdown-editor.css:1-15`

**Issue:** The file's header comment explicitly frames the rules as fixing mobile-specific problems:

> "It only fixes two things EasyMDE's defaults get wrong for this app's mobile requirements: touch targets below the 44px accessibility floor, and toolbar chrome that doesn't leave room for all 7 buttons to fit one row on a 320px-wide viewport."

but the rules themselves have no media query or mobile-scoping selector:

```css
.editor-toolbar button {
    min-width: 44px;
    min-height: 44px;
    margin: 0;
}

.editor-toolbar {
    padding: 0;
}
```

and the stylesheet is linked unconditionally from **both** layouts:
- `QuestBoard.Service/Views/Shared/_Layout.cshtml:24` (desktop)
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:17` (mobile)

So the 44px touch-target minimum and the zero-padding toolbar chrome are applied to the desktop EasyMDE toolbar as well, even though nothing about desktop needs a 320px-viewport fix or a touch-target floor. This contradicts the file's own stated scope and will confuse whoever next edits desktop toolbar styling and can't find where the (unexpectedly large) button sizing is coming from.

**Fix:** Either scope the rule to the mobile layout (e.g. wrap in a class applied only by `_Layout.Mobile.cshtml`, or split into a `markdown-editor.mobile.css` following the project's existing `*.mobile.css` convention used elsewhere in this phase), or update the comment to state the rule is intentionally applied everywhere and drop the "mobile requirements" framing.

---

### WR-04: Stale CSS rule and misleading comment left behind by the Description/Backstory → Markdown migration

**File:** `QuestBoard.Service/wwwroot/css/character-detail.mobile.css:62-65`

**Issue:**
```css
/* Preserve typed line breaks in Description and Backstory */
.character-info-value {
    white-space: pre-wrap;
}
```

This comment claims the rule preserves line breaks for Description and Backstory, but neither field carries the `character-info-value` class anymore. In `Details.Mobile.cshtml`, Description and Backstory are now rendered through the Markdown pipeline:

```html
<div class="character-info-row">
    <span class="form-label">Description</span>
    @Html.Markdown(Model.Description)
</div>
```

(`Details.Mobile.cshtml:81-94`), where `@Html.Markdown()` wraps its output in `<div class="markdown-content">…</div>` — never `.character-info-value`. The only elements that still carry `.character-info-value` are the single-line "Owned by …" paragraph (line 28) and the Level value span (line 52), where `white-space: pre-wrap` has no visible effect. The rule and comment are dead weight from the pre-Markdown version of this page and will mislead the next person who touches Description/Backstory rendering into thinking line-break preservation still flows through this class.

**Fix:** Remove the now-unused rule/comment, or if `.markdown-content` genuinely still needs whitespace handling for some rendering edge case, retarget the selector and comment at the correct element.

---

### WR-05: Crop-modal markup and "add class" script are duplicated near-verbatim across all four form views

**File:** `QuestBoard.Service/Views/Characters/Create.cshtml:151-249`, `Create.Mobile.cshtml:130-228`, `Edit.cshtml:148-246`, `Edit.Mobile.cshtml:137-235`

**Issue:** The photo-crop modal markup, the `add-class`/`remove-class` JavaScript, and the CropperJS `<script>` includes are copy-pasted across all four files with only cosmetic differences (`col-md-*` vs `col-12`, `modal-lg` vs not). This is exactly the kind of duplication that produced WR-01: a change (the empty-list guard fix) landed in three of the four copies and was missed in the fourth, because there's no single source of truth to change once. Since this phase's own summaries note it took two separate executor passes (68-01, 68-02) to land the Markdown editor across all four forms, this file layout is a demonstrated source of drift risk going forward as well.

**Fix:** Extract the class-list add/remove script and the crop modal into shared partials (e.g. `_ClassListEditor.cshtml`, `_CropPhotoModal.cshtml`) parameterized by the small set of things that actually differ (grid column classes, modal size), and include them from all four views.

## Info

### IN-01: "Classes" required-field asterisk shown on desktop forms but omitted on mobile

**File:** `Create.cshtml:73`, `Edit.cshtml:70` vs `Create.Mobile.cshtml:55`, `Edit.Mobile.cshtml:62`

**Issue:** Desktop forms label the Classes section `Classes <span class="text-danger">*</span>`, signaling it's required. The mobile equivalents render only `Classes`, with no visual required-marker, even though the underlying validation (`ValidateCharacterClassLevelsAsync`, sum of class levels must equal character level) is identical on both surfaces.

**Fix:** Add the same `<span class="text-danger">*</span>` marker to `Create.Mobile.cshtml:55` and `Edit.Mobile.cshtml:62` for parity with the desktop forms.

---

_Reviewed: 2026-07-10T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
