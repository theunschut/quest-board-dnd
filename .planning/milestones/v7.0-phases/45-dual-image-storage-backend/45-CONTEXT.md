# Phase 45: Dual-Image Storage Backend - Context

**Gathered:** 2026-07-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Server stores both an original and a cropped image per upload, entirely without server-side image processing. Covers requirements IMAGE-02 and IMAGE-03, and — per D-01 below — extends beyond their literal text to a third entity that didn't exist when those requirements were written.

**In scope:** Schema/migration, Repository, and Domain-layer plumbing for `CharacterImageEntity`, `DungeonMasterProfileImageEntity`, and `ContactImageEntity` (all three 1:1 owner-keyed image tables). No server-side image-decoding/processing library is added.

**Not in scope:** Any crop UI, any view/form changes, any JS. That's entirely Phase 46, and per the "Mid-milestone visible state" decision below, the two phases are being shipped together — so Phase 45 leaves the running app visually and behaviorally identical to today.

</domain>

<decisions>
## Implementation Decisions

### Contact Image Scope
- **D-01:** Extend this phase's dual-image treatment to `ContactImageEntity` as well, not just `CharacterImageEntity`/`DungeonMasterProfileImageEntity`. Contacts (the NPC directory) didn't exist when IMAGE-02/03/05 were written into REQUIREMENTS.md (Phase 57 added it afterward) — the user chose to fold it in now rather than leave a third inconsistent image-storage shape and reopen this exact work later. **Note for downstream:** REQUIREMENTS.md/ROADMAP.md's literal text ("character photo, DM profile photo") is now stale relative to this decision; Phase 46 planning should treat Contact's crop UI as in-scope too, sourced from this decision, not from the requirement text.

### Column Naming & Existing-Row Fallback
- **D-02:** Rename the existing `ImageData` column to `OriginalImageData` on all three tables (`CharacterImages`, `DungeonMasterProfileImages`, `ContactImages`), and add a new nullable `CroppedImageData` column to each. Rationale (user's own, supersedes the project research's proposed naming): every photo stored today genuinely *is* an unmodified original — no crop feature has ever existed to derive a cropped version from it — so keeping the existing bytes under the "original" name is the semantically correct read, not a retroactive relabeling.
- **D-02a (fallback):** Anywhere the app wants "the display/cropped image," read `CroppedImageData ?? OriginalImageData`. For any row where `CroppedImageData` is still NULL (i.e., not re-uploaded/re-cropped since this ships), this resolves to today's existing photo — nothing changes visually for any existing character, DM profile, or contact until its owner re-uploads. This also satisfies the "no user-facing functionality may be removed" constraint: without this fallback, un-migrated rows would show a blank/missing image wherever the cropped value is read.

### Validation Duplication Cleanup
- **D-03:** Extract a shared `IImageValidationService` (Domain layer) now, while this code is already being touched to add the second file/column. It replaces all 5 existing copy-pasted MIME-type/size validation blocks — `CharacterViewModel`'s attribute-based `MaxFileSizeAttribute`/`AllowedExtensionsAttribute` pair (used by Character Create/Edit), and `ContactsController`'s inline `allowedMimeTypes`/size-check blocks (Create/Edit) — onto one shared validator. Applies to both the original and cropped file on every upload path across all three controllers (`CharactersController`, `DungeonMasterController`, `ContactsController`).

### Mid-Milestone Visible State
- **D-04:** Phase 45 and Phase 46 will be planned, executed, and deployed together — there is no intermediate production release of Phase 45 alone. This makes the "what does the interim UI look like" question moot: Phase 45 makes **zero view/form changes**. The new `CroppedImageData` field/column exists purely in the data layer (schema, Repository, Domain, and — per D-03 — the consolidated validation service) until Phase 46 wires it into the actual forms with the crop UI.

### Claude's Discretion
- Exact EF Core migration mechanics for the rename+add across three tables (single migration vs. one per table; whether to use `RenameColumn` or drop/recreate — `RenameColumn` should preserve existing data safely, confirm during planning/research).
- Exact shape/method signatures of `IImageValidationService` (e.g., one method validating both files vs. one call per file; where exactly it sits in `QuestBoard.Domain`).
- Naming of the new repository/service methods for reading `OriginalImageData` and `CroppedImageData` (e.g., `GetCharacterOriginalPictureAsync`, `GetCharacterCroppedPictureAsync`, or similar) — should mirror existing naming conventions (`GetCharacterProfilePictureAsync`, `GetProfilePictureAsync`, `GetContactImageAsync`) as closely as possible.
- Whether the upsert methods (`UpdateProfileImageAsync`, `UpsertProfileImageAsync`, and `ContactRepository`'s equivalent) take two separate `byte[]?` parameters or a small parameter object — implementation detail, must guarantee atomic replacement of both columns together (no partial-update state), per the original research's Pitfall 5.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Roadmap
- `.planning/ROADMAP.md` §"Phase 45: Dual-Image Storage Backend" — phase goal, success criteria, requirements mapping, Phase 46 dependency note
- `.planning/REQUIREMENTS.md` §"Character & Profile Image Cropping" — IMAGE-01 through IMAGE-05 full text. **Stale relative to D-01**: text says "character photo, DM profile photo" only — does not yet reflect the Contact-scope expansion decided in this discussion.

### Project-Level Research (pre-dates the Contact-scope and column-naming decisions above — read with those overrides in mind)
- `.planning/research/SUMMARY.md` — full stack/architecture/pitfall research for the dual-image + crop-UI work. **Superseded by D-02**: research proposed keeping the existing column as "cropped" and adding a new "original" column; this discussion decided the opposite naming (existing column → `OriginalImageData`, new column → `CroppedImageData`). The rest of the research (no server-side image library, atomic-replace-on-reupload requirement, EXIF/canvas/touch pitfalls for Phase 46) still applies. Note research also did not know about `ContactImageEntity` (added in Phase 57, after research ran on 2026-07-04) — apply the same schema pattern to it per D-01.
- `.planning/PROJECT.md` §"Key Decisions" — row "Profile picture crop paused" — the original SkiaSharp-availability concern this phase's zero-server-processing approach resolves by avoidance

### Related Phase Context
- `.planning/phases/44-post-finalization-voting-waitlist-auto-promotion/44-CONTEXT.md` — confirms Phase 44/45 are independent ("different tables entirely")

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CharacterImageEntity` (`QuestBoard.Repository/Entities/CharacterImageEntity.cs`) — `[Table("CharacterImages")]`, `Id` is `[ForeignKey(nameof(Character))]` (owner-keyed, no separate PK), single required `ImageData byte[]`.
- `DungeonMasterProfileImageEntity` (`QuestBoard.Repository/Entities/DungeonMasterProfileImageEntity.cs`) — identical shape, `[Table("DungeonMasterProfileImages")]`.
- `ContactImageEntity` (`QuestBoard.Repository/Entities/ContactImageEntity.cs`, added Phase 57) — identical shape again, `[Table("ContactImages")]`. All three are the migration/rename target per D-01/D-02.
- `CharacterRepository.UpdateProfileImageAsync` (`QuestBoard.Repository/CharacterRepository.cs:130`) and `DungeonMasterProfileRepository.UpsertProfileImageAsync` (`QuestBoard.Repository/DungeonMasterProfileRepository.cs:49`) and `ContactRepository`'s equivalent upsert (`QuestBoard.Repository/ContactRepository.cs` ~line 90-103) — existing upsert-or-null pattern (create the image row if absent, else mutate `ImageData` in place) to widen for two `byte[]?` params with atomic replacement.
- `CharacterViewModel` (`QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs`) — `MaxFileSizeAttribute`/`AllowedExtensionsAttribute` custom `ValidationAttribute` classes, one of the two existing validation styles `IImageValidationService` (D-03) will replace.
- `ContactsController.Create`/`Edit` (`QuestBoard.Service/Controllers/Contacts/ContactsController.cs:100-201`) — the other existing validation style: inline `allowedMimeTypes`/size checks directly in the controller action. Also the source of the second style D-03 consolidates away.
- `ContactsController.GetContactImage` (`QuestBoard.Service/Controllers/Contacts/ContactsController.cs:318-346`) — existing magic-byte content-type sniffing for serving image bytes with the correct response `Content-Type`; new "get original"/"get cropped" actions should follow this exact pattern.
- `CharactersController.GetProfilePicture` (`QuestBoard.Service/Controllers/Characters/CharactersController.cs:361`) and the equivalent DM profile picture action (`QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs:132`) — existing retrieval-endpoint pattern, already carrying the group/membership scoping fixes from Phase 49. Any new retrieval endpoint must mirror these auth checks, not reinvent them.

### Established Patterns
- All three image tables are 1:1 with their owner, keyed by the owner's own Id (no separate auto-increment PK) — the rename+add migration extends this existing shape, it does not introduce a new table or relationship.
- Zero server-side image-processing library anywhere in the codebase today (no SkiaSharp/ImageSharp/Magick.NET) — must remain that way; this is IMAGE-02 and an explicit REQUIREMENTS.md "Out of Scope" entry.
- EF Core additive-migration convention already established for these exact tables (`MoveCharacterImagesToSeparateTable` migration, 2026-03-04) — this phase's rename+add migration is a compatible extension of that convention, not a new pattern.

### Integration Points
- Three repository classes (`CharacterRepository`, `DungeonMasterProfileRepository`, `ContactRepository`) each need: (1) their upsert method widened to accept both original and cropped `byte[]?`, atomically, and (2) a new read method for the cropped value (with the `CroppedImageData ?? OriginalImageData` fallback per D-02a) alongside the existing original-value read method (`GetCharacterProfilePictureAsync`, `GetProfilePictureAsync`, `GetContactImageAsync` — repurposed or supplemented to read `OriginalImageData` directly).
- New shared `IImageValidationService` (Domain layer, D-03) becomes the single validation call site for all three controllers, replacing both existing validation styles.
- One EF Core migration renames `ImageData` → `OriginalImageData` and adds nullable `CroppedImageData` across `CharacterImages`, `DungeonMasterProfileImages`, and `ContactImages` in the same schema change.
- AutoMapper profiles (`QuestBoard.Repository/Automapper/EntityProfile.cs`) referencing `ImageData` need updating for the column rename across all three entity mappings.

</code_context>

<specifics>
## Specific Ideas

- The column-naming direction (existing column becomes `OriginalImageData`, new column is `CroppedImageData`) is the user's own correction to the project research's proposed naming — captured verbatim in D-02 because it changes which column every downstream repository/service method reads from.
- Phase 45 and Phase 46 are being shipped together as one release, not sequentially deployed — this is why Phase 45 makes no view changes at all (D-04).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. The Contact-image expansion (D-01) is a locked scope decision for this phase, not a deferred idea.

</deferred>

---

*Phase: 45-Dual-Image Storage Backend*
*Context gathered: 2026-07-07*
