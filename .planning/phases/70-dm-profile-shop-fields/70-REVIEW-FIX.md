---
phase: 70-dm-profile-shop-fields
fixed_at: 2026-07-10T18:08:38Z
review_path: .planning/phases/70-dm-profile-shop-fields/70-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 70: Code Review Fix Report

**Fixed at:** 2026-07-10T18:08:38Z
**Source review:** .planning/phases/70-dm-profile-shop-fields/70-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 2 (fix_scope: critical_warning — 0 critical, 2 warning; 5 info findings excluded)
- Fixed: 2
- Skipped: 0

## Fixed Issues

### WR-01: Redundant nested `.markdown-content` wrapper div around `@Html.Markdown()` output

**Files modified:**
- `QuestBoard.Service/Views/DungeonMaster/Profile.cshtml`
- `QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml`
- `QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml`
- `QuestBoard.Service/Views/Shop/Details.Mobile.cshtml`

**Commit:** `26018737`

**Applied fix:** Removed the redundant outer `.markdown-content` wrapper at each of the four call
sites, since `HtmlHelperExtensions.Markdown()` already emits that wrapper internally. Where the
call site's wrapper carried an additional page-specific class (`Profile.Mobile.cshtml`'s
`dm-profile-bio-text`, `Details.Mobile.cshtml`'s `parchment-text-muted mt-2`), the wrapper div was
kept but stripped of the duplicated `markdown-content` class, leaving only the page-specific
class(es). Where the wrapper carried no extra class (`Profile.cshtml`, `_ShopItemDetailsContent.cshtml`),
the wrapper div was removed entirely and `@Html.Markdown(...)` is called directly. Verified with a
full `dotnet build` of `QuestBoard.Service.csproj` (0 errors, 0 warnings), which compiles all
touched Razor views.

### WR-02: DM Bio field lost its 2000-character client-side limit when migrated to the Markdown editor

**Files modified:**
- `QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs`
- `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml`
- `QuestBoard.Service/wwwroot/js/markdown-editor.js`
- `QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml`
- `QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml`

**Commit:** `d7ba4bd4`

**Applied fix:** Added an optional `MaxLength` property to `MarkdownEditorViewModel` (defaults to
`null`, so all 25 other `_MarkdownEditor` call sites are unaffected). `_MarkdownEditor.cshtml` now
renders `maxlength="@Model.MaxLength"` on the underlying `<textarea>` and, when `MaxLength` is set,
a live `current/max characters` counter beneath the field. Since EasyMDE/CodeMirror intercepts all
keystrokes itself and never touches the raw `<textarea>` until submit, the native `maxlength`
attribute alone doesn't enforce anything while typing — `markdown-editor.js` now reads
`textarea.maxLength` at init time and, when non-negative, wires a `codemirror.on('change', ...)`
handler that truncates the live editor content back to the limit and keeps the counter in sync.
Both `EditProfile.cshtml` and `EditProfile.Mobile.cshtml` now pass `MaxLength = 2000` for the Bio
field, restoring client-side enforcement to match the existing server-side `[StringLength(2000)]`
on `EditDMProfileViewModel.Bio`. Verified with a full `dotnet build` of `QuestBoard.Service.csproj`
(0 errors, 0 warnings) and `node -c` syntax check on `markdown-editor.js` (both passed).

---

_Fixed: 2026-07-10T18:08:38Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
