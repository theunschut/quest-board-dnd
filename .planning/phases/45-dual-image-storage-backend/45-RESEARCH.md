# Phase 45: Dual-Image Storage Backend - Research

**Researched:** 2026-07-07
**Domain:** EF Core schema migration (column rename + additive nullable column) across three 1:1 owner-keyed tables; Domain-layer validation-service extraction; Repository upsert/read widening — all in an ASP.NET Core 10 MVC / EF Core 10 / SQL Server codebase. No new external packages.
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-01 (Contact Image Scope):** Extend this phase's dual-image treatment to `ContactImageEntity` as well, not just `CharacterImageEntity`/`DungeonMasterProfileImageEntity`. Contacts (the NPC directory) didn't exist when IMAGE-02/03/05 were written into REQUIREMENTS.md (Phase 57 added it afterward) — the user chose to fold it in now rather than leave a third inconsistent image-storage shape and reopen this exact work later. Note for downstream: REQUIREMENTS.md/ROADMAP.md's literal text ("character photo, DM profile photo") is now stale relative to this decision; Phase 46 planning should treat Contact's crop UI as in-scope too, sourced from this decision, not from the requirement text.

**D-02 (Column Naming & Existing-Row Fallback):** Rename the existing `ImageData` column to `OriginalImageData` on all three tables (`CharacterImages`, `DungeonMasterProfileImages`, `ContactImages`), and add a new nullable `CroppedImageData` column to each. Rationale (user's own, supersedes the project research's proposed naming): every photo stored today genuinely *is* an unmodified original — no crop feature has ever existed to derive a cropped version from it — so keeping the existing bytes under the "original" name is the semantically correct read, not a retroactive relabeling.

**D-02a (fallback):** Anywhere the app wants "the display/cropped image," read `CroppedImageData ?? OriginalImageData`. For any row where `CroppedImageData` is still NULL (i.e., not re-uploaded/re-cropped since this ships), this resolves to today's existing photo — nothing changes visually for any existing character, DM profile, or contact until its owner re-uploads. This also satisfies the "no user-facing functionality may be removed" constraint: without this fallback, un-migrated rows would show a blank/missing image wherever the cropped value is read.

**D-03 (Validation Duplication Cleanup):** Extract a shared `IImageValidationService` (Domain layer) now, while this code is already being touched to add the second file/column. It replaces all 5 existing copy-pasted MIME-type/size validation blocks — `CharacterViewModel`'s attribute-based `MaxFileSizeAttribute`/`AllowedExtensionsAttribute` pair (used by Character Create/Edit), and `ContactsController`'s inline `allowedMimeTypes`/size-check blocks (Create/Edit) — onto one shared validator. Applies to both the original and cropped file on every upload path across all three controllers (`CharactersController`, `DungeonMasterController`, `ContactsController`).

**D-04 (Mid-Milestone Visible State):** Phase 45 and Phase 46 will be planned, executed, and deployed together — there is no intermediate production release of Phase 45 alone. This makes the "what does the interim UI look like" question moot: Phase 45 makes **zero view/form changes**. The new `CroppedImageData` field/column exists purely in the data layer (schema, Repository, Domain, and — per D-03 — the consolidated validation service) until Phase 46 wires it into the actual forms with the crop UI.

### Claude's Discretion

- Exact EF Core migration mechanics for the rename+add across three tables (single migration vs. one per table; whether to use `RenameColumn` or drop/recreate — `RenameColumn` should preserve existing data safely, confirm during planning/research).
- Exact shape/method signatures of `IImageValidationService` (e.g., one method validating both files vs. one call per file; where exactly it sits in `QuestBoard.Domain`).
- Naming of the new repository/service methods for reading `OriginalImageData` and `CroppedImageData` (e.g., `GetCharacterOriginalPictureAsync`, `GetCharacterCroppedPictureAsync`, or similar) — should mirror existing naming conventions (`GetCharacterProfilePictureAsync`, `GetProfilePictureAsync`, `GetContactImageAsync`) as closely as possible.
- Whether the upsert methods (`UpdateProfileImageAsync`, `UpsertProfileImageAsync`, and `ContactRepository`'s equivalent) take two separate `byte[]?` parameters or a small parameter object — implementation detail, must guarantee atomic replacement of both columns together (no partial-update state), per the original research's Pitfall 5.

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope. The Contact-image expansion (D-01) is a locked scope decision for this phase, not a deferred idea.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| IMAGE-02 | The crop happens entirely client-side — no server-side image-processing library | Confirmed feasible with zero new packages (Standard Stack, Package Legitimacy Audit); this phase adds only EF Core schema/Repository/Domain plumbing, no image-decoding library anywhere. Verification method given in Validation Architecture's Phase Requirements → Test Map (static grep for absence of SkiaSharp/ImageSharp/Magick.NET references). |
| IMAGE-03 | Both the original uploaded image and the cropped result are saved | Directly addressed by D-02/D-02a schema design (Architecture Patterns, Pattern 1 migration + Pattern 2 atomic upsert + Pattern 3 fallback read), confirmed against this codebase's existing `CharacterImageEntity`/`DungeonMasterProfileImageEntity`/`ContactImageEntity` shape and the `MoveCharacterImagesToSeparateTable`/`AddContactsFeature` migration precedents. Pitfall 4 and Pitfall 5 specifically address the atomic-replace and no-partial-update-state requirements from the phase's success criterion #2. |

</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **EF Core packages are Repository-only.** All migration/entity work in this phase belongs in `QuestBoard.Repository`; `QuestBoard.Domain` and `QuestBoard.Service` must not take a direct EF Core package reference. This directly informs the Open Question #3 recommendation (use primitive `byte[]`/`string` parameters for `IImageValidationService`, not an EF-aware type) and confirms the plan should place all schema/migration work exclusively in `QuestBoard.Repository`.
- **No GSD tracking references in source code, comments, or string literals.** Do not write `D-02`, `IMAGE-03`, `Phase 45`, or similar identifiers into C# comments, XML doc comments, entity/property names, or migration class names. The migration class name and all code comments in this phase's deliverables must describe behavior in plain language (e.g., `RenameImageColumnsAddCropped` and "Rename the existing image column to reflect that it now stores only the original, unmodified upload" — not "D-02: rename column"). This constraint is already reflected in the Code Examples above (no GSD IDs appear in any code comment).
- **Windows development environment.** Use Windows-style paths/CRLF line endings when creating or editing files; SQL Server runs on the Windows host (use `localhost` for local dev, `sqlserver` service name only inside Docker).
- **Migrations auto-apply on startup** via `context.Database.Migrate()` — no manual `dotnet ef database update` needed in dev, but the hand-edited `RenameColumn` migration should still be manually dry-run verified per this research's Validation Architecture "Phase gate" step, since auto-apply-on-startup does not substitute for pre-merge verification against a populated database.
- **Never commit directly to `main`** — this phase's work must land on the existing milestone branch (`milestone/v7-backlog-cleanup`), consistent with the current git state.
- **UI/UX guidelines (modern-card pattern, etc.) do not apply to this phase** — D-04 locks in zero view/form changes for Phase 45.

## Summary

This phase is pure data-layer plumbing with no new library, no new endpoint shape, and no UI change — everything needed is already precedented in this exact codebase. The `CharacterImages`, `DungeonMasterProfileImages`, and `ContactImages` tables are all structurally identical: a single `[Key][ForeignKey(nameof(Owner))] int Id` (no separate PK — the owner's own Id is the PK/FK) plus one `[Required] byte[] ImageData` column, created via `[Table("...")]` attribute + (for Character/DM only) an explicit Fluent `HasOne().WithOne().HasForeignKey<T>()` in `QuestBoardContext.OnModelCreating`. `ContactEntity` relies on the data-annotation convention alone (no Fluent config) — EF Core 10 already infers this identically, confirmed by the existing `AddContactsFeature` migration successfully creating the FK. Renaming `ImageData` → `OriginalImageData` and adding `CroppedImageData byte[]?` requires zero Fluent API changes to any `OnModelCreating` block — only the C# property name on each of the three entity classes, one hand-edited migration using `RenameColumn` (not the auto-scaffolded `DropColumn`+`AddColumn`, which would silently destroy every existing photo), and consequent updates to the model snapshot, AutoMapper profiles, repository methods, and Domain interfaces.

The five existing validation call sites (`CharactersController.Create`/`Edit` — inline MIME+size checks; `DungeonMasterController.EditProfile` — inline size check only, no MIME/extension check today; `ContactsController.Create`/`Edit` — inline MIME+size checks) sit *alongside*, not instead of, the ViewModel-level `MaxFileSizeAttribute`/`AllowedExtensionsAttribute` data annotations already present on `CharacterViewModel.ProfilePictureFile`, `EditDMProfileViewModel.ProfilePictureFile`, and `ContactViewModel.ContactImageFile`. This is double validation in three of five call sites today (data-annotation extension/size check *and* inline MIME/size check both run), and the DM profile path is inconsistent (no extension check, no MIME check at all — only size). The safest consolidation is to keep the declarative `[MaxFileSize]`/`[AllowedExtensions]` attributes as the client/model-binding-time UX layer (unchanged — this preserves existing jQuery-unobtrusive-validation behavior with zero markup changes) and introduce `IImageValidationService` purely as the *server-side, post-model-binding* re-validation gate that every controller calls explicitly before persisting bytes — the same shape as today's inline blocks, just deduplicated into one Domain-layer call taking both files (original + cropped) as a pair.

**Primary recommendation:** One migration, hand-edited to use three `RenameColumn` + three `AddColumn` (nullable) pairs; widen the three repository upsert methods to accept two `byte[]?` parameters atomically in a single `SaveChangesAsync` call; add three new `GetXCroppedPictureAsync` methods implementing the `CroppedImageData ?? OriginalImageData` fallback at the query level (not in application code, so it's provably atomic and consistent); introduce `IImageValidationService.ValidateImagePairAsync` (or per-file overload) in `QuestBoard.Domain` called explicitly by all five controller actions, replacing every inline `allowedMimeTypes`/size block without touching the existing ViewModel data-annotation attributes.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Client-time file-size/extension UX hint | Browser (jQuery unobtrusive validation via data annotations) | — | Already implemented via `MaxFileSizeAttribute`/`AllowedExtensionsAttribute`; out of scope to change per D-04 (zero view changes) |
| Server-side authoritative validation (MIME/size/magic-byte) | API / Backend (Domain layer: new `IImageValidationService`) | — | Must not trust client-supplied `ContentType` header or extension alone; this is the actual security boundary (see Security Domain below) |
| Byte storage (original + cropped) | Database / Storage (three image tables) | Repository (EF Core mapping) | Cardinality is strictly 1:1 with owner, always read/written together — no new table, no new relationship |
| Atomic dual-column replace-on-reupload | Repository | — | Both columns must be set in the same tracked-entity mutation before one `SaveChangesAsync()` — this is what makes the replace atomic, not a transaction wrapper |
| Cropped-value fallback resolution (`Cropped ?? Original`) | Repository (query-level `??` in the LINQ projection) | — | Doing the fallback in the SQL projection (not in a service/controller after two separate fetches) guarantees there's no window where a caller could observe inconsistent state |
| MIME sniffing for HTTP response `Content-Type` | API / Backend (existing `DetectImageMimeType`/inline magic-byte checks in controllers) | — | Already established pattern in `ContactsController.DetectImageMimeType` and `DungeonMasterController.GetDMProfilePicture`; new original/cropped GET actions must reuse this, not reinvent it |

## Standard Stack

### Core
No new packages. This phase's "stack" is entirely EF Core 10 (already installed) and the existing Domain/Repository project structure.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore | 10.0.9 [VERIFIED: QuestBoard.Repository.csproj] | ORM / migrations | Already the project's ORM; `RenameColumn` is a built-in `MigrationBuilder` method, no additional package needed |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 [VERIFIED: QuestBoard.Repository.csproj] | SQL Server provider | Already installed |
| Microsoft.EntityFrameworkCore.Design | 10.0.9 [VERIFIED: QuestBoard.Repository.csproj] | `dotnet ef migrations add` tooling | Already installed |

### Supporting
None needed — this phase introduces zero new NuGet packages, consistent with IMAGE-02's "no server-side image-processing library" constraint and the phase's own success criterion #3.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Single migration renaming+adding across all 3 tables | Three separate migrations (one per table) | Three migrations is more granular/revertable individually but adds ceremony for a mechanically identical change applied to structurally identical tables; a single migration matches how `AddContactsFeature` already bundled multiple related table changes in one file. Single migration recommended — see Common Pitfalls #1 for the one risk this introduces. |
| Query-level `CroppedImageData ?? OriginalImageData` fallback | Fallback resolved in Domain/Service layer after two separate repository calls | Two-call resolution is more code, more places to forget the fallback, and creates a window (however small in a synchronous method) where cropped/original could theoretically be fetched from different transactions. Query-level (single `Select` projection) is simpler and provably atomic — recommended. |
| One `IImageValidationService.ValidateAsync(IFormFile)` called twice per upload | One `ValidateImagePairAsync(IFormFile original, IFormFile? cropped)` call taking both files | A pair-validating method lets the service enforce "if cropped is present, original must also be present" (or vice versa) as a single invariant, matching D-04/atomic-replace intent; the planner should pick this shape (see Code Examples) rather than two independent single-file calls that can't see each other. |

**Installation:** N/A — no new packages.

**Version verification:** `Microsoft.EntityFrameworkCore*` packages confirmed at 10.0.9 by direct read of `QuestBoard.Repository.csproj` [VERIFIED: QuestBoard.Repository.csproj:10-12]. No registry lookup needed since no new package is being added.

## Package Legitimacy Audit

Not applicable — this phase adds zero new NuGet packages (per IMAGE-02 and the phase's explicit success criterion #3: no image-processing library, and no other dependency is implicated by this phase's scope). `slopcheck`/registry verification steps are skipped; there is nothing to audit.

## Architecture Patterns

### System Architecture Diagram

```
                    ┌─────────────────────────────────────────────┐
                    │   Controllers (unchanged UI/routes, D-04)    │
                    │  CharactersController / DungeonMasterController │
                    │        / ContactsController                  │
                    └───────────────┬───────────────────────────────┘
                                    │  IFormFile(s) already bound by
                                    │  existing ViewModel + data annotations
                                    ▼
                    ┌─────────────────────────────────────────────┐
                    │  IImageValidationService (NEW, Domain layer)  │
                    │  ValidateImagePairAsync(original, cropped?)   │
                    │  - MIME allowlist check (both files)          │
                    │  - size limit check (both files)              │
                    │  - (extension check, matching existing rules) │
                    └───────────────┬───────────────────────────────┘
                                    │ byte[] original, byte[]? cropped
                                    ▼
                    ┌─────────────────────────────────────────────┐
                    │  CharacterService / ContactService /          │
                    │  DungeonMasterProfileService (existing,       │
                    │  widened call sites only)                     │
                    └───────────────┬───────────────────────────────┘
                                    │ (characterId/contactId/userId,
                                    │  originalBytes, croppedBytes)
                                    ▼
                    ┌─────────────────────────────────────────────┐
                    │  I*Repository.UpdateProfileImageAsync/        │
                    │  UpsertProfileImageAsync (WIDENED)            │
                    │  - loads tracked owner + Include(ProfileImage)│
                    │  - sets BOTH OriginalImageData AND            │
                    │    CroppedImageData on the SAME tracked       │
                    │    entity instance                            │
                    │  - single SaveChangesAsync() call             │
                    │    => atomic replace (success criterion #2)   │
                    └───────────────┬───────────────────────────────┘
                                    │
                                    ▼
     ┌───────────────────────────────────────────────────────────────┐
     │  CharacterImages / DungeonMasterProfileImages / ContactImages  │
     │  Id (PK=FK to owner) | OriginalImageData (renamed, required)   │
     │                      | CroppedImageData (new, nullable)        │
     └───────────────────────────────────────────────────────────────┘
                                    ▲
                                    │ read path (new methods)
                    ┌───────────────┴───────────────────────────────┐
                    │ GetXOriginalPictureAsync  -> OriginalImageData │
                    │ GetXCroppedPictureAsync   -> CroppedImageData  │
                    │                              ?? OriginalImageData│
                    │ (fallback resolved in the LINQ Select itself)  │
                    └─────────────────────────────────────────────────┘
```

### Recommended Project Structure
No new folders. Changes land in existing files only:
```
QuestBoard.Domain/
├── Interfaces/
│   ├── ICharacterRepository.cs            # widen UpdateProfileImageAsync signature, add Get*CroppedPictureAsync
│   ├── IDungeonMasterProfileRepository.cs # same
│   ├── IContactRepository.cs              # same
│   └── IImageValidationService.cs         # NEW interface
├── Services/
│   ├── ImageValidationService.cs          # NEW implementation
│   ├── CharacterService.cs                # widen UpdateAsync's image call, add pass-through for cropped read
│   ├── ContactService.cs                  # same
│   └── DungeonMasterProfileService.cs     # widen UpsertProfileAsync signature
└── Extensions/ServiceExtensions.cs        # register IImageValidationService

QuestBoard.Repository/
├── Entities/
│   ├── CharacterImageEntity.cs            # ImageData -> OriginalImageData, + CroppedImageData
│   ├── DungeonMasterProfileImageEntity.cs # same
│   └── ContactImageEntity.cs              # same
├── CharacterRepository.cs                 # widen UpdateProfileImageAsync, add GetCharacterCroppedPictureAsync
├── DungeonMasterProfileRepository.cs      # widen UpsertProfileImageAsync, add GetCroppedPictureAsync
├── ContactRepository.cs                   # widen UpdateProfileImageAsync, add GetContactCroppedImageAsync
├── Automapper/EntityProfile.cs            # ImageData -> OriginalImageData references (3 mappings)
└── Migrations/
    └── {timestamp}_RenameImageColumnsAddCropped.cs  # NEW, hand-edited migration

QuestBoard.Service/
└── Controllers/
    ├── Characters/CharactersController.cs      # replace inline validation with IImageValidationService call
    ├── DungeonMaster/DungeonMasterController.cs # same (also fixes missing MIME/extension check today)
    └── Contacts/ContactsController.cs           # same
```

### Pattern 1: Rename-preserving EF Core migration across multiple tables in one file
**What:** Scaffold the migration from the renamed C# properties, then hand-edit the generated `DropColumn`+`AddColumn` pairs into `RenameColumn` calls (one per table), and add three `AddColumn<byte[]>(nullable: true)` calls for the new `CroppedImageData` columns.
**When to use:** Any time an EF Core migration renames an existing column — the scaffolder cannot distinguish "rename" from "drop one, add another" and defaults to the latter, which is data-destroying.
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing
// (Column renames section) — confirmed HIGH confidence, official Microsoft Learn docs
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Hand-edited from the scaffolder's auto-generated DropColumn+AddColumn pair.
    migrationBuilder.RenameColumn(
        name: "ImageData",
        table: "CharacterImages",
        newName: "OriginalImageData");

    migrationBuilder.RenameColumn(
        name: "ImageData",
        table: "DungeonMasterProfileImages",
        newName: "OriginalImageData");

    migrationBuilder.RenameColumn(
        name: "ImageData",
        table: "ContactImages",
        newName: "OriginalImageData");

    migrationBuilder.AddColumn<byte[]>(
        name: "CroppedImageData",
        table: "CharacterImages",
        type: "varbinary(max)",
        nullable: true);

    migrationBuilder.AddColumn<byte[]>(
        name: "CroppedImageData",
        table: "DungeonMasterProfileImages",
        type: "varbinary(max)",
        nullable: true);

    migrationBuilder.AddColumn<byte[]>(
        name: "CroppedImageData",
        table: "ContactImages",
        type: "varbinary(max)",
        nullable: true);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "CroppedImageData", table: "CharacterImages");
    migrationBuilder.DropColumn(name: "CroppedImageData", table: "DungeonMasterProfileImages");
    migrationBuilder.DropColumn(name: "CroppedImageData", table: "ContactImages");

    migrationBuilder.RenameColumn(
        name: "OriginalImageData",
        table: "CharacterImages",
        newName: "ImageData");

    migrationBuilder.RenameColumn(
        name: "OriginalImageData",
        table: "DungeonMasterProfileImages",
        newName: "ImageData");

    migrationBuilder.RenameColumn(
        name: "OriginalImageData",
        table: "ContactImages",
        newName: "ImageData");
}
```
**Model snapshot:** `QuestBoardContextModelSnapshot.cs` is regenerated automatically by `dotnet ef migrations add` — do not hand-edit it. After scaffolding + hand-editing the migration file itself, re-run `dotnet ef migrations add` is NOT needed a second time; the snapshot is written once at scaffold time and reflects the *model* (post-rename), which is already correct as long as the three entity classes were renamed before scaffolding. [VERIFIED: QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs current entries confirmed at lines 234-246, 294-306, 356-368 — each currently shows the single `ImageData` `[Required]` property; scaffolding after the C# rename will regenerate these three blocks with `OriginalImageData` (required) + `CroppedImageData` (nullable).]

### Pattern 2: Atomic dual-column upsert (single tracked-entity mutation, single SaveChangesAsync)
**What:** Widen `UpdateProfileImageAsync`/`UpsertProfileImageAsync` to accept both byte arrays and set both properties on the same EF-tracked entity before one `SaveChangesAsync()` call.
**When to use:** Any time two columns must change together with no observable partial-update state (success criterion #2 / CONTEXT.md's Pitfall 5).
**Example:**
```csharp
// Source: existing pattern in CharacterRepository.cs:130-155, widened
public async Task UpdateProfileImageAsync(
    int characterId, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default)
{
    var entity = await DbContext.Characters
        .Include(c => c.ProfileImage)
        .FirstOrDefaultAsync(c => c.Id == characterId, token);
    if (entity == null) return;

    if (originalImageData == null)
    {
        // Clearing the original clears both — there is no valid state with a
        // cropped image but no original per the domain's own semantics.
        entity.ProfileImage = null;
    }
    else if (entity.ProfileImage == null)
    {
        entity.ProfileImage = new CharacterImageEntity
        {
            Id = entity.Id,
            OriginalImageData = originalImageData,
            CroppedImageData = croppedImageData
        };
    }
    else
    {
        entity.ProfileImage.OriginalImageData = originalImageData;
        entity.ProfileImage.CroppedImageData = croppedImageData;
    }

    await DbContext.SaveChangesAsync(token); // single call => atomic replace of both columns
}
```
**Caller contract to decide during planning:** what does the caller pass for `croppedImageData` when only the original is being re-uploaded (i.e., Phase 45's own zero-UI-change state, where no crop widget exists yet to produce a second file)? Recommendation: callers in this phase always pass `croppedImageData: null` for every upload (since no crop UI exists yet), which correctly leaves every row's `CroppedImageData` NULL and falls back to `OriginalImageData` per D-02a — this is intentional and matches "zero view/form changes" (D-04). Phase 46 is what will start passing a real cropped byte array.

### Pattern 3: Query-level nullable-fallback projection for the "display" image
**What:** Resolve `CroppedImageData ?? OriginalImageData` inside the `Select` projection itself, not in application code after two separate fetches.
**When to use:** Any read where the caller wants "whichever cropped image exists, falling back to original" (D-02a) — the guild-member-list / Phase 46 consumer.
**Example:**
```csharp
// Source: existing pattern in CharacterRepository.cs:62-70 (GetCharacterProfilePictureAsync), extended
public async Task<byte[]?> GetCharacterCroppedPictureAsync(int id, CancellationToken token = default)
{
    // Rooted at the filtered Characters DbSet so the CharacterEntity group filter applies,
    // exactly like GetCharacterProfilePictureAsync/GetCharacterOriginalPictureAsync.
    return await DbContext.Characters
        .Where(c => c.Id == id)
        .Select(c => c.ProfileImage != null
            ? (c.ProfileImage.CroppedImageData ?? c.ProfileImage.OriginalImageData)
            : null)
        .FirstOrDefaultAsync(token);
}
```
Note: EF Core translates `??` inside a `Select` on a `byte[]?` column to `COALESCE(...)` in the generated SQL for SQL Server — this is a standard, well-supported translation, not a client-evaluation fallback. [ASSUMED — based on general EF Core LINQ-to-SQL translation behavior for the `??` operator on scalar columns; not independently re-verified against EF Core 10's translation test suite this session. Low risk: worst case is a client-eval warning at query-build time, easily caught by any smoke test that exercises this method, not a runtime data-correctness risk.]

### Pattern 4: Domain-layer paired-file validation service
**What:** A single `IImageValidationService` call validating an "original + optional cropped" pair, replacing all 5 existing inline validation blocks.
**When to use:** Server-side authoritative validation immediately before persisting bytes, on every controller upload path.
**Example:**
```csharp
// NEW — QuestBoard.Domain/Interfaces/IImageValidationService.cs
namespace QuestBoard.Domain.Interfaces;

public interface IImageValidationService
{
    /// <summary>
    /// Validates one or two uploaded image files (original required-if-present, cropped optional)
    /// against the shared MIME-type allowlist and size limit. Returns a list of error messages
    /// (empty if valid) keyed by a caller-supplied field name for ModelState binding.
    /// </summary>
    IList<ImageValidationError> ValidateImagePair(
        IFormFile? originalFile, string originalFieldName,
        IFormFile? croppedFile, string croppedFieldName);
}

public record ImageValidationError(string FieldName, string Message);
```
```csharp
// NEW — QuestBoard.Domain/Services/ImageValidationService.cs
namespace QuestBoard.Domain.Services;

internal class ImageValidationService : IImageValidationService
{
    private static readonly string[] AllowedMimeTypes = ["image/jpeg", "image/png", "image/gif"];
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public IList<ImageValidationError> ValidateImagePair(
        IFormFile? originalFile, string originalFieldName,
        IFormFile? croppedFile, string croppedFieldName)
    {
        var errors = new List<ImageValidationError>();
        ValidateSingle(originalFile, originalFieldName, errors);
        ValidateSingle(croppedFile, croppedFieldName, errors);
        return errors;
    }

    private static void ValidateSingle(IFormFile? file, string fieldName, List<ImageValidationError> errors)
    {
        if (file == null || file.Length == 0) return;

        if (!AllowedMimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(new ImageValidationError(fieldName, "Only JPG, PNG, or GIF images are accepted."));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            errors.Add(new ImageValidationError(fieldName, "Only JPG, PNG, or GIF file extensions are accepted."));
        }

        if (file.Length > MaxFileSizeBytes)
        {
            errors.Add(new ImageValidationError(fieldName, "Image cannot exceed 5 MB."));
        }
    }
}
```
```csharp
// Controller call site — replaces the inline block in CharactersController.Create, e.g.:
var validationErrors = imageValidationService.ValidateImagePair(
    viewModel.ProfilePictureFile, nameof(viewModel.ProfilePictureFile),
    null, string.Empty); // no cropped file exists yet in this phase — see Pattern 2 caller contract

foreach (var error in validationErrors)
{
    ModelState.AddModelError(error.FieldName, error.Message);
}
if (!ModelState.IsValid)
{
    return View(viewModel);
}
```
**Why this shape:** `QuestBoard.Domain` cannot reference `Microsoft.AspNetCore.Mvc` types like `ModelStateDictionary` (per CLAUDE.md's EF/layering rule — Domain must stay framework-agnostic of the Service layer's MVC concerns), but `IFormFile` itself lives in `Microsoft.AspNetCore.Http.Abstractions`, which has no MVC dependency and is already referenced transitively wherever `IFormFile` appears on Domain-facing surfaces in this codebase today (none currently — `IFormFile` today only appears on Service-layer ViewModels). **Decision point for planner:** confirm whether `QuestBoard.Domain` already has (or can cleanly add) a reference to `Microsoft.AspNetCore.Http.Abstractions` for the `IFormFile` type, or whether `IImageValidationService` should instead take `(byte[] bytes, string contentType, string fileName)` primitives read out of the `IFormFile` by the controller first — the latter keeps Domain fully framework-agnostic at the cost of the controller doing one extra `CopyToAsync` before validation instead of after. **Recommendation: use the primitive-parameter shape** (`byte[]`/`string`/`string`), not `IFormFile`, to keep `QuestBoard.Domain` free of any ASP.NET Core package reference — this also directly satisfies CLAUDE.md's "EF packages belong only in QuestBoard.Repository" layering spirit by keeping Domain free of any additional framework package reference. Check `QuestBoard.Domain.csproj` during planning to confirm it currently has zero `Microsoft.AspNetCore.*` package references before finalizing this signature.

### Anti-Patterns to Avoid
- **Auto-scaffolded `DropColumn`+`AddColumn` for the rename:** EF Core's migration scaffolder always generates this by default for a renamed property — never apply it as-is; every existing character/DM/contact photo would be silently deleted. Always hand-edit to `RenameColumn`. [CITED: learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing]
- **Resolving the `Cropped ?? Original` fallback in the Service or Controller layer via two separate repository calls:** creates unnecessary complexity and a (small) window for observing inconsistent reads; do it in the repository's LINQ projection instead (Pattern 3).
- **Trusting client-supplied `IFormFile.ContentType` as sole validation:** it's an HTTP header the client sets; a malicious upload can set `Content-Type: image/png` on an arbitrary file. The existing codebase does not currently do magic-byte validation on *upload* (only on *serving*, via `DetectImageMimeType`) — see Security Domain below for whether this phase should close that gap.
- **Skipping the "no cropped file exists yet" case in validation:** `ValidateImagePair`/equivalent must treat a null/absent cropped file as valid (not an error) throughout this phase, since D-04 means no UI produces a second file yet.
- **Embedding GSD requirement/decision IDs in code comments:** per CLAUDE.md, do not write `D-02`, `IMAGE-03`, `Phase 45`, etc. into C# comments, XML doc comments, or migration class/file names — write plain-language comments describing behavior instead (see Code Examples above for the correct style).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Column rename preserving data | Manual `DROP COLUMN` + `ADD COLUMN` + app-level backfill script | `migrationBuilder.RenameColumn(...)` | Built into `MigrationBuilder`; a single SQL Server `sp_rename` under the hood, zero data movement needed [CITED: learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing] |
| Cross-request atomic replace of 2 related columns | Manual transaction/lock wrapper | Single tracked-entity mutation + one `SaveChangesAsync()` | EF Core's change tracker + SaveChanges already wraps all pending changes for one `DbContext` instance in one implicit transaction; no explicit `BeginTransaction` needed for a single `SaveChangesAsync` call |
| MIME/extension/size validation | A 6th copy-pasted inline block per new controller action | `IImageValidationService` (this phase's own D-03 deliverable) | Exactly the problem D-03 identifies — don't let this phase's own new endpoints (e.g., new `GetXCroppedPicture` actions, if any need upload-side validation) reintroduce a 6th copy |

**Key insight:** Nothing in this phase requires a new abstraction beyond what CONTEXT.md already locked in (D-02/D-02a/D-03). The risk is entirely in getting the *existing* EF Core/AutoMapper/Repository mechanics right for a rename (not an add), not in needing new tooling.

## Common Pitfalls

### Pitfall 1: Auto-scaffolded migration silently drops all existing image data
**What goes wrong:** Running `dotnet ef migrations add RenameImageColumns` after renaming the three C# properties generates `DropColumn("ImageData")` + `AddColumn("OriginalImageData")` for each table. Applying this as-is on a database with existing rows (every current character/DM/contact photo) deletes all of them.
**Why it happens:** EF Core's scaffolder diffs the old model snapshot against the new one purely structurally — a property rename is indistinguishable from "delete property A, add unrelated property B" at that level. [CITED: learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing]
**How to avoid:** Always hand-edit the generated migration to replace each `DropColumn`+`AddColumn` pair with `RenameColumn`, per Pattern 1. Verify by reading the generated `.cs` file before applying, and confirm the scaffolder's own data-loss warning appeared in the CLI output (it warns whenever `DropColumn` is generated) — treat that warning as the trigger to hand-edit, not ignore.
**Warning signs:** `dotnet ef migrations add` prints "An operation was scaffolded that may result in the loss of data" to the console; the generated migration's `Up()` method contains both a `DropColumn` and an `AddColumn` referencing what you know to be the same logical field.

### Pitfall 2: Combining all three tables in one migration file makes partial-failure recovery harder
**What goes wrong:** If the single combined migration fails partway (e.g., a lock timeout on the second table's rename during `Up()`), the migration is left partially applied and SQL Server's migration history table doesn't record it as applied — but two of three tables may already be renamed depending on where the failure occurred, since EF Core wraps a single migration in one transaction, but that transaction only protects against a mid-`Up()` exception if the provider fully supports transactional DDL. SQL Server does support transactional DDL for all operations used here (`RenameColumn`/`AddColumn`), so this is a low-probability pitfall for this database, but still worth a one-line note for planning: verify (or simply trust) that SQL Server rolls back the whole migration transaction on any exception within `Up()`.
**Why it happens:** Combining independent-but-related schema changes into one migration is efficient but couples their failure/rollback behavior.
**How to avoid:** Since SQL Server supports transactional DDL and EF Core wraps each migration's `Up()` in a transaction by default, a single combined migration is safe here — this is a documented note, not a required mitigation. If the planner still prefers isolation, three separate migrations (one per table) is the alternative with no meaningful additional cost in this codebase (see Alternatives Considered).
**Warning signs:** N/A for SQL Server + EF Core's default transactional migration behavior; would only matter for a database provider without transactional DDL (not applicable to this project's SQL Server stack).

### Pitfall 3: `ContactImageEntity`'s Fluent-API-free 1:1 config silently diverges from Character/DM's explicit config
**What goes wrong:** `CharacterEntity`/`DungeonMasterProfileEntity` have an explicit `modelBuilder.Entity<T>().HasOne(...).WithOne(...).HasForeignKey<TImage>(...)` in `OnModelCreating`; `ContactEntity` has none — it relies purely on `ContactImageEntity`'s `[Key][ForeignKey(nameof(Contact))]` data annotations for EF Core to infer the identical 1:1 shape. This already works correctly today (confirmed: `AddContactsFeature` migration created the FK/PK correctly), so this phase's rename+add change requires **no new Fluent config for any of the three tables** — but a planner unfamiliar with this asymmetry might add a redundant/inconsistent Fluent block for Contact "to match" Character/DM, or worse, might assume Contact needs different migration handling than the other two.
**Why it happens:** The codebase has two valid, functionally-identical ways to declare a 1:1 owned-shape relationship (attribute-only vs. Fluent-explicit) and used both across different phases (Character/DM predate Contact by a full phase-generation).
**How to avoid:** Treat all three tables identically for this migration — same `RenameColumn`+`AddColumn` shape, no `OnModelCreating` edits needed for any of them. Confirmed via direct read of `QuestBoardContext.cs:133-137` (Character, explicit Fluent), `:173-176` (DM, explicit Fluent), and the complete absence of a Contact equivalent in the same file — Contact's FK/PK is entirely attribute-driven and already correctly scaffolded by the Phase 57 migration.
**Warning signs:** N/A — this is purely a "don't add unnecessary code" pitfall, not a runtime failure risk.

### Pitfall 4: `CharacterService.UpdateAsync`/`ContactService.UpdateAsync` call the image-upsert method unconditionally on every edit, even with no new picture
**What goes wrong:** Both `CharacterService.UpdateAsync` (line 69) and `ContactService.UpdateAsync` (line 24) call `repository.UpdateProfileImageAsync(model.Id, model.ProfilePicture / model.ContactImageData, token)` on **every single edit**, not just edits where a new file was uploaded — because the ViewModel-to-Domain-model mapping already carries forward the *existing* picture bytes (fetched in the GET action, round-tripped through the Edit form as a hidden/existing value) whenever no new file is uploaded. This means the existing pattern already "always re-writes the image row on every edit," which is important context: when this phase widens these calls to pass `croppedImageData` too, the *same* existing-value round-trip logic must apply, or an edit that doesn't touch the picture at all could accidentally null out an existing `CroppedImageData` if the widened call isn't given the correct value to preserve.
**Why it happens:** The current single-image code works by accident here — `model.ProfilePicture`/`model.ContactImageData` is never null on a no-photo-change edit (it's populated from the DB read in the GET action and silently passed through), so passing it straight back through `UpdateProfileImageAsync` is a no-op overwrite with identical bytes. Introducing a second column changes this: the planner must decide what value the widened call passes for `croppedImageData` on a "no new upload" edit — it must be the *existing* `CroppedImageData` value (or explicitly re-fetched), not `null`, or every unrelated field edit (e.g., changing a character's Level) would silently wipe out that character's cropped image.
**How to avoid:** When widening `UpdateProfileImageAsync`/`UpsertProfileImageAsync` calls in `CharacterService`/`ContactService`/`DungeonMasterProfileService`, ensure the value passed for the *unchanged* column (whichever one wasn't touched by this upload) is the entity's current stored value, not null — either by having the repository method internally preserve "unchanged" values when passed a sentinel (e.g., a 3-state parameter), or by having the Service layer fetch-then-pass-through the existing value explicitly, mirroring how `model.ProfilePicture` already round-trips today. This is the single highest-risk implementation detail in this phase given success criterion #2 ("never a state where one is updated and the other still reflects a prior upload") — it applies equally to "an edit doesn't touch either image" (must preserve both) and "an edit only touches the original" (must preserve cropped, since no crop UI exists this phase to produce a new one).
**Warning signs:** Any wave-0/unit test that edits a character's Level (no photo touched) and then asserts `CroppedImageData` is unchanged — write this test; it is the direct regression check for this pitfall.

### Pitfall 5: Original/cropped divergence on re-upload (already flagged in project-level research, still applies)
**What goes wrong:** If a user re-uploads a new original photo but only one column gets updated (e.g., a bug where `CroppedImageData` isn't touched because this phase's controllers don't yet produce a second file), the stored "cropped" value can end up representing a completely different, stale photo relative to the just-uploaded original — a real semantic bug even before Phase 46 ships a UI to make it visible.
**Why it happens:** Two logically-coupled columns updated via what could easily become two separate code paths or two separate `SaveChangesAsync` calls if not deliberately designed as one atomic mutation.
**How to avoid:** Pattern 2 (single tracked-entity mutation, single `SaveChangesAsync`) plus the explicit business rule for this phase: **every re-upload of a new original photo (with no crop UI yet to supply a new cropped photo) should also clear the stale `CroppedImageData` back to null**, not leave a mismatched previous crop in place. This is a Claude's-discretion implementation detail not explicitly locked in CONTEXT.md — flag it as an explicit decision point for the plan: does uploading a new *original* (with no accompanying crop) null out any existing `CroppedImageData`, or leave it untouched? **Recommendation: null it out.** Leaving a stale crop attached to a brand-new original violates the spirit of D-02a's fallback (which exists precisely so a *never-cropped* row shows the original, not so a row can show a crop of a *different, superseded* photo). This must be a named behavior in the plan's task list, not an incidental side effect.
**Warning signs:** A wave-0 test that uploads photo A (original+crop), then re-uploads original-only photo B, then asserts `GetXCroppedPictureAsync` returns B's bytes (via the fallback), not A's stale crop.

## Code Examples

### Repository interface widening (apply identically to all three)
```csharp
// Source: existing pattern in ICharacterRepository.cs, widened per this phase's schema decisions
namespace QuestBoard.Domain.Interfaces;

public interface ICharacterRepository : IBaseRepository<Character>
{
    // ... existing members unchanged ...

    /// <summary>
    /// Returns the character's original (unmodified) profile image bytes, or null if none is set.
    /// </summary>
    Task<byte[]?> GetCharacterOriginalPictureAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns the character's cropped/display profile image, falling back to the original
    /// image if no cropped version has ever been saved. Null if neither is set.
    /// </summary>
    Task<byte[]?> GetCharacterCroppedPictureAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Atomically sets, replaces, or clears both the original and cropped profile image
    /// together. Passing null for originalImageData clears both columns.
    /// </summary>
    Task UpdateProfileImageAsync(
        int characterId, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default);
}
```
**Naming note (Claude's Discretion in CONTEXT.md):** `GetCharacterOriginalPictureAsync`/`GetCharacterCroppedPictureAsync` mirror the existing `GetCharacterProfilePictureAsync` naming convention closely. **Decision point for planner:** whether to keep `GetCharacterProfilePictureAsync` as an alias for "original" (for backward call-site compatibility with any code not yet updated) or rename it outright to `GetCharacterOriginalPictureAsync` and update all call sites. Recommendation: rename outright — `Phase 46`'s "show original on character-details page" success criterion implies the character-details page's existing `GetProfilePicture` action (`CharactersController.cs:361`) will keep calling whatever this method is named, so a clean rename (with a corresponding controller-action rename) is less confusing long-term than keeping a "ProfilePicture" name that no longer clearly means "original." This is explicitly listed as Claude's Discretion in CONTEXT.md — the plan should make the call and document it, not leave it ambiguous.

### AutoMapper profile updates (all three mappings)
```csharp
// Source: existing pattern in EntityProfile.cs:88-103, updated for the column rename
// Character mapping
CreateMap<Character, CharacterEntity>()
    .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (int)src.Status))
    .ForMember(dest => dest.Role, opt => opt.MapFrom(src => (int)src.Role))
    .ForMember(dest => dest.ProfileImage, opt => opt.MapFrom(src => src.ProfilePicture == null
        ? null
        : new CharacterImageEntity
        {
            OriginalImageData = src.ProfilePicture
            // CroppedImageData intentionally NOT set here — AddAsync's mapping path only ever
            // carries the original at creation time in this phase (no crop UI exists yet).
        }));

CreateMap<CharacterEntity, Character>()
    .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (CharacterStatus)src.Status))
    .ForMember(dest => dest.Role, opt => opt.MapFrom(src => (CharacterRole)src.Role))
    .ForMember(dest => dest.ProfilePicture, opt => opt.MapFrom(src => src.ProfileImage != null
        ? src.ProfileImage.OriginalImageData
        : null));
```
**Decision point:** confirm whether `Character`/`Contact`/`DungeonMasterProfile` Domain models need a *new* property (e.g., `Character.CroppedProfilePicture`) mirroring `ProfilePicture`/`ContactImageData`, or whether the cropped value is only ever exposed through the dedicated `GetXCroppedPictureAsync` repository/service methods and never round-trips through the full entity mapping. Given D-04 (zero view/form changes this phase, no crop UI to populate a second field), **recommend NOT adding a Domain-model property for cropped image this phase** — only the dedicated read methods are needed, since nothing produces a cropped byte array to write through the full-entity mapping path yet. Re-evaluate in Phase 46 when the ViewModel needs a `CroppedPictureFile` field.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Single `ImageData` column per image table, one value per owner | `OriginalImageData` (renamed) + `CroppedImageData` (new, nullable) | This phase (45) | Every existing photo becomes "the original" with no re-upload required; nothing changes visually until Phase 46 ships and a user re-crops |
| Inline per-controller MIME/size validation (5 duplicated blocks, one missing a MIME check entirely) | Single `IImageValidationService` call site (Domain layer) | This phase (45) | Closes the DM-profile-upload gap where MIME/extension were never checked (only size) — a genuine, if minor, security hardening side-effect of this phase's own consolidation work |

**Deprecated/outdated:** None — this is additive/renaming work on an already-current EF Core 10 stack, not a library upgrade.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | EF Core 10 translates `CroppedImageData ?? OriginalImageData` inside a LINQ `Select` on `byte[]?` columns to SQL Server `COALESCE(...)`, not client-side evaluation | Architecture Patterns, Pattern 3 | Low — worst case is an EF Core client-evaluation warning surfaced at query-build/test time (not a silent runtime bug); any wave-0 test exercising `GetXCroppedPictureAsync` against the InMemory or a real SQL Server test DB would immediately reveal a translation failure as either an exception or an easily-caught warning log |
| A2 | `QuestBoard.Domain.csproj` currently has zero `Microsoft.AspNetCore.*` package references (informing the recommendation that `IImageValidationService` should take primitive `byte[]`/`string` parameters rather than `IFormFile`) | Architecture Patterns, Pattern 4 | Low-medium — if Domain *does* already reference `Microsoft.AspNetCore.Http.Abstractions` for some other reason, the recommended primitive-parameter shape is merely more conservative than necessary, not wrong; the plan should verify this file directly (a 30-second check) before finalizing the interface signature |

**If this table is empty:** N/A — see rows above. Both assumptions are low-risk and independently verifiable in under a minute during planning (re-run a test, or read one `.csproj` file); neither blocks planning from proceeding on the stated recommendation.

## Open Questions

1. **What does a widened `UpdateProfileImageAsync` call pass for the "other" column on an edit that doesn't touch it?**
   - What we know: Today's single-column call always round-trips the existing value from the ViewModel (see Pitfall 4), so the same pattern must extend to two columns without introducing a silent-wipe bug.
   - What's unclear: Whether to solve this via a 3-state parameter (not-provided vs. explicit-null vs. new-bytes) on the repository method, or via the Service layer always fetching-then-passing-through both existing values explicitly before calling the repository.
   - Recommendation: Service layer explicitly fetches and passes through the existing value for whichever column isn't being replaced in this call (mirrors the existing `model.ProfilePicture` round-trip pattern exactly, requires no new repository-method complexity). Plan this as an explicit task with its own verification step (Pitfall 4's suggested test).

2. **Should uploading a new original (no crop UI yet) clear a stale `CroppedImageData`?**
   - What we know: CONTEXT.md doesn't explicitly decide this; it's implied by D-02a's fallback rationale but not stated as a rule for the re-upload case.
   - What's unclear: Whether this is truly in-scope for Phase 45 (which has no crop UI to make the distinction observable yet) or something Phase 46 should own instead, since Phase 45+46 ship together per D-04.
   - Recommendation: Decide and implement in Phase 45 regardless (it's a one-line addition to the same widened repository method already being built), since it's cheap now and closes Pitfall 5 before it can ever manifest, rather than leaving a latent bug for Phase 46 to discover.

3. **Exact `IImageValidationService` parameter type — `IFormFile` vs. primitives?**
   - What we know: `QuestBoard.Domain` today has no `IFormFile` usage anywhere; `IFormFile` only appears on Service-layer ViewModels.
   - What's unclear: Whether adding `Microsoft.AspNetCore.Http.Abstractions` as a Domain package reference is acceptable under this codebase's layering conventions (CLAUDE.md is explicit about EF packages being Repository-only, but is silent on ASP.NET Core abstractions in Domain).
   - Recommendation: Default to primitive parameters (`byte[] bytes, string contentType, string fileName`) unless the planner finds an existing precedent of `Microsoft.AspNetCore.*` types already referenced in `QuestBoard.Domain` — check the `.csproj` directly (trivial, see Assumption A2).

## Environment Availability

Skipped — this phase has no new external dependencies. EF Core 10.0.9 tooling (`dotnet ef`) is already used throughout this codebase's migration history (most recent: `20260706194635_AddRewardsToQuest.cs`), confirming the toolchain is present and functional in this environment.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (3.2.2) + FluentAssertions 8.10.0 [VERIFIED: QuestBoard.UnitTests.csproj] |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (xUnit v3 SDK-style, no separate runsettings) |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CharacterRepositoryTests\|FullyQualifiedName~ContactRepositoryTests"` |
| Full suite command | `dotnet test` (runs both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`) |

Repository-layer tests use EF Core's InMemory provider (`UseInMemoryDatabase`), confirmed in `CharacterRepositoryTests.cs:15` and `ContactRepositoryTests.cs`. **Important InMemory-provider caveat:** the InMemory provider does not execute real migrations (`RenameColumn`/SQL Server DDL) — it builds the schema directly from the current `DbContext` model. This means unit tests can fully verify the *post-migration* entity/repository behavior (renamed properties, new nullable column, atomic upsert, fallback read) but **cannot** verify the migration file itself applies cleanly against an existing populated SQL Server database. That verification requires either the integration test suite (if it targets a real SQL Server instance) or a manual `dotnet ef database update` dry run against a copy of production/dev data.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| IMAGE-02 | No server-side image-processing library added | manual/static-check | grep `QuestBoard.Repository.csproj`/`QuestBoard.Domain.csproj`/`QuestBoard.Service.csproj` for SkiaSharp/ImageSharp/Magick.NET references — none should exist | N/A (verification is absence-of-reference, not a runnable test) |
| IMAGE-03 (original persisted) | Uploading a photo persists `OriginalImageData` unmodified | unit | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests.UpdateProfileImageAsync_SetsOriginalImageData` | ❌ Wave 0 — extend `CharacterRepositoryTests`/`ContactRepositoryTests`/new DM profile repository test |
| IMAGE-03 (cropped persisted, nullable) | A row with no cropped upload yet reads back via `Cropped ?? Original` fallback | unit | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests.GetCharacterCroppedPictureAsync_FallsBackToOriginal_WhenCroppedIsNull` | ❌ Wave 0 |
| Success criterion #2 (atomic replace) | Re-upload replaces both columns together, no partial-update state | unit | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests.UpdateProfileImageAsync_ReplacesBothColumnsAtomically` | ❌ Wave 0 |
| Success criterion #2 (edit without photo touch preserves both) | Editing an unrelated field (e.g., Level) does not null out `CroppedImageData` | unit | `dotnet test --filter FullyQualifiedName~CharacterServiceTests.UpdateAsync_UnrelatedFieldEdit_PreservesExistingImages` | ❌ Wave 0 — no `CharacterServiceTests.cs` exists yet; check whether Domain-layer service tests exist elsewhere first |
| Success criterion #4 (independently retrievable) | `GetXOriginalPictureAsync` and `GetXCroppedPictureAsync` return distinct values when both are set | unit | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests.GetCharacterOriginalAndCroppedPictureAsync_ReturnDistinctValues` | ❌ Wave 0 |
| D-03 (validation service replaces 5 duplicated blocks) | `IImageValidationService` rejects oversized/wrong-MIME/wrong-extension files consistently across all 3 controllers | unit | `dotnet test --filter FullyQualifiedName~ImageValidationServiceTests` | ❌ Wave 0 — new test file |
| Pitfall 5 (stale crop cleared on original-only re-upload) | Re-uploading a new original with no new crop nulls the stale `CroppedImageData` | unit | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests.UpdateProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Image"` (fast, targets only the new/modified image-related tests)
- **Per wave merge:** `dotnet test` (full suite, both Unit and Integration test projects)
- **Phase gate:** Full suite green before `/gsd:verify-work`, plus a manual `dotnet ef database update` dry run (or equivalent staging deploy) confirming the hand-edited `RenameColumn` migration applies cleanly against a database with existing rows in all three tables — this specific check cannot be automated via the InMemory-provider unit tests (see Test Framework caveat above) and must be called out as an explicit manual verification step in the plan.

### Wave 0 Gaps
- [ ] Extend `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` — new tests for widened `UpdateProfileImageAsync`, `GetCharacterOriginalPictureAsync`, `GetCharacterCroppedPictureAsync`
- [ ] Extend `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` — same, for Contact
- [ ] New `QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs` — confirm whether this file already exists (not yet checked); if absent, create it since the DM profile repository currently appears to have no dedicated unit test file among the three
- [ ] New `QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs` — direct unit tests for the new Domain-layer validation service (MIME allowlist, extension allowlist, size limit, pair-validation with one/both/neither file present)
- [ ] Manual/staging verification step for the migration itself (not automatable via InMemory provider) — must be an explicit task in the plan, not assumed covered by unit tests

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Unchanged by this phase — existing `[Authorize]`/policy attributes on controllers are untouched |
| V3 Session Management | no | Not touched by this phase |
| V4 Access Control | yes | Existing group-scoped query filters (`CharacterEntity`/`ContactEntity` fail-closed `GroupId` filters) and the Phase-49-fixed group/membership auth checks on `GetProfilePicture`/`GetDMProfilePicture`/`GetContactImage` must be preserved identically on any new original/cropped read endpoints — this phase adds no new access-control surface, but any new controller action must be built by copying the existing auth-check pattern exactly, not by re-deriving it |
| V5 Input Validation | yes | `IImageValidationService` — MIME-type allowlist + extension allowlist + 5 MB size limit, applied identically to both the original and (when present) cropped file on every upload path |
| V6 Cryptography | no | Not applicable — no crypto operations in this phase |
| V12 File Handling (ASVS 4.0.3 numbering; "Files and Resources" in some ASVS editions) | yes | See Known Threat Patterns below — this is the category most directly relevant to storing user-uploaded byte arrays |

### Known Threat Patterns for ASP.NET Core file-upload-to-varbinary storage

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Client-forged `Content-Type` header bypassing MIME allowlist (e.g., uploading a script/HTML file renamed with a `.jpg` extension and a spoofed `image/jpeg` Content-Type) | Tampering | Extension allowlist (already present) + `Content-Type` allowlist (already present) are both checked today, but **neither validates the actual file bytes** (no magic-byte/signature sniffing on upload — only on serving, via `DetectImageMimeType`). This is a pre-existing gap in the codebase, not introduced by this phase; whether to close it is a scope decision (see below), not an automatic requirement. |
| Oversized upload causing memory pressure (denial of service via large `byte[]` allocations) | Denial of Service | 5 MB size limit already enforced both client-side (data annotations) and server-side (existing inline checks / new `IImageValidationService`) — this phase must preserve this limit for the cropped file too, not just the original |
| Stored XSS via a malicious SVG or HTML-polyglot file served back with an incorrect `Content-Type` | Tampering / Info Disclosure | The existing `.jpg`/`.jpeg`/`.png`/`.gif` extension allowlist already excludes SVG; the existing `DetectImageMimeType` magic-byte check on the *serving* path (not upload) already forces one of `image/png`/`image/gif`/`image/jpeg` regardless of what's actually in the byte array — this means even a successfully-uploaded non-image file would be served back with an image `Content-Type` header, which is a defense (not a vulnerability) against browser content-sniffing-based XSS, since browsers won't execute an `image/*`-declared response as HTML/script in a normal `<img>` context |
| Cross-tenant image access via id-guessing (IDOR) | Elevation of Privilege / Info Disclosure | Already mitigated by existing group-scoped query filters and the Phase-49 auth-check pattern on the `GetProfilePicture`/`GetDMProfilePicture`/`GetContactImage` actions — any new original/cropped-specific read actions added in this phase must copy this exact pattern, not bypass it |

**Scope note — server-side magic-byte validation on *upload* (not just serving):** This gap pre-dates this phase and is not called out as in-scope by CONTEXT.md's D-03 (which is about *deduplicating* the existing MIME/size checks, not about *adding* a new validation layer). Project-level research (SUMMARY.md) flagged this exact question as an open gap ("decide during Phase 2 planning exactly how deep this validation goes... e.g., magic-byte check only, vs. skip entirely given this is a small trusted-group internal tool") but that project-level Phase 2 became part of what is now this Phase 45. **Recommendation:** treat closing this gap as optional/discretionary for this phase — `IImageValidationService` can be built to accept an easy follow-on magic-byte check later (e.g., by structuring `ValidateSingle` so a magic-byte check could be added as one more condition) without it being a blocking requirement now, since this is a small, trusted-group internal tool per the project's own stated risk tolerance (PROJECT.md), and CONTEXT.md's locked decisions don't ask for it. Flag this explicitly in the plan as a deliberate scope decision, not a silent omission.

## Sources

### Primary (HIGH confidence)
- Direct codebase reads: `QuestBoard.Repository/Entities/CharacterImageEntity.cs`, `DungeonMasterProfileImageEntity.cs`, `ContactImageEntity.cs`, `CharacterEntity.cs`, `ContactEntity.cs`, `QuestBoardContext.cs` (OnModelCreating, lines 118-176), `CharacterRepository.cs`, `DungeonMasterProfileRepository.cs`, `ContactRepository.cs`, `ICharacterRepository.cs`, `IDungeonMasterProfileRepository.cs`, `IContactRepository.cs`, `CharacterService.cs`, `ContactService.cs`, `DungeonMasterProfileService.cs`, `BaseService.cs`, `CharactersController.cs`, `DungeonMasterController.cs`, `ContactsController.cs`, `CharacterViewModel.cs`, `ContactViewModel.cs`, `EditDMProfileViewModel.cs`, `EntityProfile.cs`, `ViewModelProfile.cs`, `ServiceExtensions.cs` (both Domain and Repository), `QuestBoardContextModelSnapshot.cs` (lines 234-368, 930-1010), `MoveCharacterImagesToSeparateTable` migration, `AddContactsFeature` migration, `CharacterRepositoryTests.cs`, `QuestBoard.Repository.csproj`, `QuestBoard.UnitTests.csproj`, `.planning/config.json`, `CLAUDE.md`
- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing — official Microsoft Learn docs, `RenameColumn` mechanics and rationale for hand-editing scaffolded rename migrations (fetched directly this session)

### Secondary (MEDIUM confidence)
- None required beyond the primary sources above — this phase's research question was fully answerable from direct codebase inspection plus one official-docs fetch.

### Tertiary (LOW confidence)
- General WebSearch results corroborating the `RenameColumn`/data-loss-on-scaffold behavior (codegenes.net, Mitchel Sellers blog) — used only to confirm the official docs' framing was not idiosyncratic; not cited as an independent source of fact anywhere in this document.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; existing EF Core version directly confirmed from `.csproj`
- Architecture: HIGH — every integration point (repositories, services, controllers, ViewModels, AutoMapper, DbContext Fluent config, model snapshot) verified by direct file reads, not inference
- Pitfalls: HIGH — Pitfall 1 (rename scaffolding) confirmed via official Microsoft Learn docs; Pitfalls 2-5 derived from direct reading of this codebase's actual current call patterns (`CharacterService.UpdateAsync`, `ContactService.UpdateAsync`), not speculation

**Research date:** 2026-07-07
**Valid until:** 2026-08-06 (30 days — this is stable, non-fast-moving internal architecture work with no external library version dependency)
