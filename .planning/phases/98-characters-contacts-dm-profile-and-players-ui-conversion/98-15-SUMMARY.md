---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 15
subsystem: dm-profile-form
tags: [ui-conversion, razor, form-contract, merge, guard-tests]
requires: [98-14]
provides:
  - "DungeonMaster/EditProfile as one view for both layouts on the form contract (form-page wrapper, h1 title card, picture block, Markdown bio editor, shared crop dialog, action row)"
  - "wwwroot/css/dm-profile-form.css (pages layer, tokens only): the 128px round photo as a form-only class"
  - "the stacked action row as a phone-layout rule only"
  - "DmEditProfileRenderTests and a bio-editor markup fact under both User-Agents"
affects: [98-16, 98-23]
tech-stack:
  added: []
  patterns: [form-only photo class so a still-global sheet never restyles a converted form; stack fallback scoped to the phone layout]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/dm-profile-form.css
    - QuestBoard.IntegrationTests/Tests/DmEditProfileRenderTests.cs
  modified:
    - QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/MarkdownEditorMarkupTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/dm-editprofile.mobile.css
decisions:
  - "The commit is btn-primary (was btn-warning) and the exit keeps its label Back to Profile, as the operator directed for this page; the phone action row uses the stack fallback (Save Profile first on screen, exit below) because the label does not fit one row at 360 or 390"
  - "The stack modifier is scoped to the phone layout in components.css; as written it also stacked the desktop row, which the approved desktop frame does not show"
  - "The current photo is the form-only class form-photo-round (128px, glass border) on both layouts; the old phone view's 80px thumbnail is gone"
  - "The validation summary uses the All mode with no text-danger class; the field and file messages use the tag helper with no class, as the other converted forms do"
metrics:
  tasks: 3
  commits: 2
status: complete
actuals:
  tokens: 22000
  tasks: 3
  commits: 2
---

# Phase 98 Plan 15: DM EditProfile merged onto the form contract Summary

The Dungeon Master profile form is one view for the desktop and phone layouts on the shared form contract, with the validation summary showing every error, its own page sheet that the still-global profile sheet cannot restyle, and the bio editor's Tab fix confirmed live.

## The merge

- **View** (commit `c5a3c7a6`): `dm-profile-form-page form-page` wrapper (the nested container, row and centred-column wrappers are gone), page-title card with one `h1` (Edit DM Profile, decorative icon), picture block in the left column (current photo as `rounded-circle form-photo-round` with no inline style, `Re-crop Photo` `btn-warning btn-sm` with `data-crop-original-url`, the file input with its crop data attributes, the cropped-file input hidden by the attribute, the size hint, the file message and the crop error), the Markdown bio editor partial unchanged, `hr`, then `form-actions form-actions--stack` with `Back to Profile` `btn-secondary` first in markup and `Save Profile` `btn-primary`. The Styles section links `dm-profile-form.css` and `image-crop.css`; the Scripts section is unchanged (no inline script).
- **Deleted:** `EditProfile.Mobile.cshtml` and `dm-editprofile.mobile.css`. No controller, service, POST field or validator changed.
- **Validation summary:** `asp-validation-summary="ModelOnly"` became `All` (grep: 0 and 1). A refused submission with a 2001-character bio now lists "Bio cannot exceed 2000 characters" in the summary on the overlay chip with the danger stripe, with the field's own message beside it.
- **dm-profile-form.css:** one `@layer pages` block, tokens only, no important flag: `.dm-profile-form-page .form-photo-round` (128px, cover, 3px `--qb-surface-glass-border`).
- **dm-profile.css:** unchanged. `legacy-collision.py` on the merged view prints nothing, so no rule was removed and no class renamed on the form side. The Profile pages keep `.dm-profile-photo` and the rest of the sheet. The form's old `dm-profile-photo` class was replaced by the form-only photo class so the sheet can no longer restyle the form.

## Tests (none deleted; rewrite logged in tests-renamed.md)

- `MobileViewsTests.GetMobilePage_DmEditProfile_ReturnsSuccessAndMobileLayout`: same name, now asserts the merged view (phone 200, `mobile-layout`, `form-actions`, `dm-profile-form.css`; neither the phone card wrapper nor the phone sheet) instead of the deleted phone card markup and sheet.
- New `DmEditProfileRenderTests` (8 cases): the same form under both User-Agents (one `h1`, sheet links, picture block, crop dialog, bio editor, action row shape), the current photo as a class with Re-crop and no inline style, no photo means no Re-crop, and a refused submission whose summary lists the field error.
- `MarkdownEditorMarkupTests.DungeonMasterProfileForm_BioEditor_RendersEditorHintAndLabelUnderBothUserAgents` (2 cases).
- `CharactersConversionGuardTests`: view added to `ConvertedViews` and `ViewSheetLinks` (`dm-profile-form.css`, `image-crop.css`), sheet to `ConvertedSheets` and `PageOnlySheets`, the two deleted files to `RetiredFiles`, new files to `PhaseSourceFiles` (the deleted phone view was removed from that list, as the guard instructs). `UiFoundationLayerGuardTests`: sheet appended to `LayeredFiles` and `NoLiteralFiles`.
- Unit: 1416 passed, 0 failed, 0 skipped, 1416 total.
- Integration: 2163 passed, 0 failed, 16 skipped, 2179 total (2153 before; plus 10 new cases).
- `TestHostFileWatching.cs` and `QuestBoard.Service/Controllers` untouched (git diff against PLAN_START is empty).

## Live checks (8310, scratch in `~/qb-ui-review/phase-98/after/98-15/`)

- **Sweep** of `dm-editprofile`, `dm-editprofile-errors` and `crop-dm-edit` under both User-Agents: 12 captures, 0 not 200. `readability-compare.py` against BEFORE with an empty allow file: 12 compared, 0 failing rows, 0 poster-glyph violations, `ok` (no accessible name changed, so no allow entry was needed).
- **Look check** (`lookcheck.json`): phone against `quest-form.mobile.html`: 16 properties, pass, only the two intended differences. Desktop against `quest-form.html`: the two intended differences plus `h1.color` (reference parchment, page gold `rgb(255, 193, 7)`), the approved gold form title, so the tool reports pass false for that one property only.
- **Editor keyboard check** (`editor-trap.json`): under both User-Agents Tab and Shift+Tab leave the bio editor, Escape keeps focus usable, Save Profile is reached in 2 Tab presses.
- I viewed the captures: desktop shows the gold Cinzel title, the round photo with Re-crop under the Profile Photo label level with the Bio editor, and one action row (Back to Profile left, wood Save Profile right). The phone shows the same blocks stacked with Save Profile filling the width first and Back to Profile below it. The crop dialog on the phone shows the shared control row and footer. The oversize-file refusal renders its icon and sentence on the chip in the picture column.
- The p98dm profile was never submitted: the sweep rows only read the form, set a file client-side or open the crop dialog, so the bio and photo are as before.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Stack modifier applied on desktop**
- **Found during:** Task 1, comparing the view with the approved frames.
- **Issue:** `.form-actions--stack` in `components.css` was an unscoped rule, so the opt-in modifier would also have stacked the action row on desktop, where the approved frame and the operator's answer (stack only on the phone at 360 and 390) show one row.
- **Fix:** the two stack rules now sit under `.mobile-layout`; the comment says phone only. The class is in the markup for both layouts, so one view serves both.
- **Files modified:** `QuestBoard.Service/wwwroot/css/components.css`
- **Commit:** `c5a3c7a6`

### Plan interpretations

1. **[Exit label]** The operator's brief for this page keeps `Back to Profile` rather than the global Cancel from the answers file; the view and the render test use `Back to Profile`.
2. **[Tracer gate]** No auto flag is set and this sequential run cannot be resumed, so (as in 98-13) the gate was the automated re-run of the tracer's targeted tests (142 passed) plus the live sweep under both User-Agents before closing the plan.
3. **[Test-first]** The render tests were written beside the view in the same commit rather than seen failing against the old view first; the first run did expose one wrong regexp in the new summary test, which was fixed.
4. **[Files]** `components.css` and `PhaseSourceFiles` entries were touched beyond the plan's list for the deviation above; `dm-profile.css` needed no change.

## Known Stubs

None.

## Threat Flags

None. No controller, service, field name, antiforgery token or validator changed (T-98-36, T-98-38); the ownership and cross-group facts in `DungeonMasterControllerIntegrationTests` stay green. The summary tag helper encodes messages (T-98-37).

## Self-Check: PASSED

- `EditProfile.cshtml` and `dm-profile-form.css` exist; `EditProfile.Mobile.cshtml` and `dm-editprofile.mobile.css` are gone; commits `c5a3c7a6` and `e5d162c1` exist.
- `grep -c 'asp-validation-summary="ModelOnly"'` prints 0 and `...="All"` prints 1 on the merged view; no style attribute, inline script or handler; `legacy-collision.py` prints nothing.
- Report, sweep captures, `lookcheck.json` and `editor-trap.json` exist under `after/98-15/`.
