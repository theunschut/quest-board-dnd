# Phase 45: Dual-Image Storage Backend - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-07
**Phase:** 45-Dual-Image Storage Backend
**Areas discussed:** Contact image scope, Existing-row fallback behavior, Validation duplication cleanup, Mid-milestone visible state

---

## Contact Image Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Character + DM Profile only | Matches the locked requirement text exactly; ContactImageEntity stays single-image for now | |
| Include Contacts too | Extend the same OriginalImageData/crop treatment to ContactImageEntity in the same phase | ✓ |
| You decide | Claude picks based on lower-risk/more-consistent judgment | |

**User's choice:** Include Contacts too
**Notes:** Contacts (NPC directory) was added in Phase 57, after IMAGE-02/03/05 were written into REQUIREMENTS.md — the requirement text doesn't mention it, but the user chose to fold it in now rather than leave a third inconsistent image-storage shape and reopen this work later.

---

## Existing-Row Fallback Behavior / Column Naming

| Option | Description | Selected |
|--------|-------------|----------|
| Fall back to existing ImageData (recommended) | Original and cropped both show today's photo until re-upload | (superseded by free-text answer) |
| Show nothing/placeholder until re-upload | Original genuinely absent for un-migrated rows | |
| You decide | Claude picks during planning | |

**User's choice (free text):** "The current image column should become the 'originalImageData', and after this namechange, a new column should be added?" — clarified and confirmed as: rename existing `ImageData` → `OriginalImageData`, add a new nullable `CroppedImageData` column.

**Follow-up — new column name:**

| Option | Description | Selected |
|--------|-------------|----------|
| CroppedImageData | Mirrors OriginalImageData naming, matches codebase's plain-descriptive convention | ✓ |
| ImageData (keep name, change meaning) | Reuse current column name for the cropped value | |
| You decide | Claude picks exact name during planning | |

**User's choice:** CroppedImageData
**Notes:** This flips the project research's original proposed naming (research had kept the old column name for "cropped" and added a new column for "original"). The user's reasoning: every photo stored today genuinely *is* an unmodified original since no crop feature has ever existed — so the existing bytes are correctly named "original," not retroactively relabeled as "cropped." Fallback direction for display/cropped-consuming code: `CroppedImageData ?? OriginalImageData`.

---

## Validation Duplication Cleanup

| Option | Description | Selected |
|--------|-------------|----------|
| Leave duplication as-is (recommended) | Add the second field using each location's existing copy-paste pattern, unchanged | |
| Extract a shared IImageValidationService now | Consolidate all 5 copies onto one Domain-layer validator while the code is already open | ✓ |
| You decide | Claude judges the tradeoff during planning | |

**User's choice:** Extract a shared IImageValidationService now
**Notes:** Recommended option leaned on this project's general anti-abstraction preference (CLAUDE.md), but the user opted for consolidation given research had already flagged this duplication as tech debt and the code is already being touched for the new field.

---

## Mid-Milestone Visible State

| Option | Description | Selected |
|--------|-------------|----------|
| Fully invisible (recommended) | No view changes in Phase 45; new field reachable only via direct POST until Phase 46 | (superseded — question became moot) |
| Add a plain (no-JS) second file picker now | Bare file input added to all affected forms now | |
| You decide | Claude picks during planning | |

**User's choice (free text):** "I want to ship both Phase 45 and 46 together. so then i don't think this is an issue?"
**Notes:** Confirmed — Phase 45 and Phase 46 will be planned, executed, and deployed together (no intermediate production release), so Phase 45 makes zero view/form changes; the question of an interim UI state doesn't apply.

---

## Claude's Discretion

- Exact EF Core migration mechanics for the rename+add across three tables (single migration vs. per-table, `RenameColumn` vs. drop/recreate).
- Exact shape/method signatures of `IImageValidationService`.
- Naming of new repository/service read methods for `OriginalImageData`/`CroppedImageData`.
- Whether upsert methods take two separate `byte[]?` params or a parameter object — must guarantee atomic replacement of both columns together.

## Deferred Ideas

None — discussion stayed within phase scope. The Contact-image expansion is a locked scope decision for this phase, not a deferred idea.
