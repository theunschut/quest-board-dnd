# Phase 45: Dual-Image Storage Backend - Pattern Map

**Mapped:** 2026-07-07
**Files analyzed:** 15 (3 entities modified, 3 repository classes + 3 interfaces modified, 3 services modified, 3 controllers modified, 1 new validation service + interface, 1 AutoMapper profile modified, 1 new migration, 2-4 test files)
**Analogs found:** 15 / 15 (all files have a same-repo analog; the pattern is "widen an existing file" for almost everything in this phase, not "invent from scratch")

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/Entities/CharacterImageEntity.cs` | model (EF entity) | CRUD | itself (rename+add in place) | exact — self-modify |
| `QuestBoard.Repository/Entities/DungeonMasterProfileImageEntity.cs` | model (EF entity) | CRUD | `CharacterImageEntity.cs` (identical shape) | exact |
| `QuestBoard.Repository/Entities/ContactImageEntity.cs` | model (EF entity) | CRUD | `CharacterImageEntity.cs` (identical shape) | exact |
| `QuestBoard.Repository/CharacterRepository.cs` | repository | CRUD | itself (widen `UpdateProfileImageAsync`, add `Get*CroppedPictureAsync`) | exact — self-modify |
| `QuestBoard.Repository/DungeonMasterProfileRepository.cs` | repository | CRUD | `CharacterRepository.cs` (same upsert/read shape) | exact |
| `QuestBoard.Repository/ContactRepository.cs` | repository | CRUD | `CharacterRepository.cs` (same upsert/read shape) | exact |
| `QuestBoard.Domain/Interfaces/ICharacterRepository.cs` | interface | CRUD | itself (widen signatures) | exact — self-modify |
| `QuestBoard.Domain/Interfaces/IDungeonMasterProfileRepository.cs` | interface | CRUD | `ICharacterRepository.cs` | exact |
| `QuestBoard.Domain/Interfaces/IContactRepository.cs` | interface | CRUD | `ICharacterRepository.cs` | exact |
| `QuestBoard.Domain/Services/CharacterService.cs` | service | CRUD | itself (widen `UpdateAsync` image call) | exact — self-modify |
| `QuestBoard.Domain/Services/ContactService.cs` | service | CRUD | `CharacterService.cs` (identical `UpdateAsync` image pattern) | exact |
| `QuestBoard.Domain/Services/DungeonMasterProfileService.cs` | service | CRUD | `CharacterService.cs`/own file (`UpsertProfileAsync` widen) | exact |
| `QuestBoard.Domain/Interfaces/IImageValidationService.cs` (NEW) | service interface | request-response | `IEmailRenderService.cs` (small single-purpose Domain interface, no repository) | role-match |
| `QuestBoard.Domain/Services/ImageValidationService.cs` (NEW) | service (validator) | request-response | Inline validation blocks in the 3 controllers (logic source), `IEmailRenderService`/`EmailRenderService` (interface/DI shape) | role-match (logic exists today, just not extracted) |
| `QuestBoard.Service/Controllers/Characters/CharactersController.cs` | controller | request-response (file upload) | itself (replace inline validation blocks with `IImageValidationService` call) | exact — self-modify |
| `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` | controller | request-response (file upload) | `CharactersController.cs` (adds the missing MIME/extension check via the new shared service) | exact |
| `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` | controller | request-response (file upload) | `CharactersController.cs` (same inline-block-to-service-call transformation) | exact |
| `QuestBoard.Domain/Extensions/ServiceExtensions.cs` | config (DI registration) | — | itself (add one `AddScoped` line) | exact — self-modify |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` | config (AutoMapper) | transform | itself (3 `.ForMember` blocks reference renamed property) | exact — self-modify |
| `QuestBoard.Repository/Migrations/{timestamp}_RenameImageColumnsAddCropped.cs` (NEW) | migration | batch (DDL) | `20260304113417_MoveCharacterImagesToSeparateTable.cs` | role-match (that migration creates+backfills a table; this one renames+adds columns — same file/hand-edit discipline, different `MigrationBuilder` calls) |
| `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` (extend) | test | CRUD | itself (add new `[Fact]`s following existing seed/arrange/act/assert shape) | exact — self-modify |
| `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` (extend) | test | CRUD | `CharacterRepositoryTests.cs` (near-identical seed helper/test shape) | exact |
| `QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs` (NEW — confirmed absent) | test | CRUD | `CharacterRepositoryTests.cs` (closest shape: single-owner 1:1 image table, `GetProfileByUserIdAsync`-style read, no group-filter complication since DM profiles aren't group-scoped the same way — verify during planning) | role-match |
| `QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs` (NEW) | test | request-response | No existing Domain-service unit test file found in this codebase (searched `QuestBoard.UnitTests/Services/` — directory does not exist yet); closest structural analog is `CharacterRepositoryTests.cs`'s Arrange/Act/Assert `[Fact]` style, applied to a plain-object-under-test instead of an EF context | no analog (new territory — see "No Analog Found") |

## Confirmed: `DungeonMasterProfileRepositoryTests.cs` does not exist

RESEARCH.md flagged this as unconfirmed ("confirm whether this file already exists — not yet checked"). Direct glob search (`**/*RepositoryTests.cs`) against the full `QuestBoard.UnitTests` tree returned exactly four files:

```
QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs
QuestBoard.UnitTests/Repository/UserTransactionRepositoryTests.cs
QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs
QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
```

No `DungeonMasterProfileRepositoryTests.cs`. The plan must create this file from scratch, modeled on `CharacterRepositoryTests.cs` (see Pattern Assignments below) — there is no existing DM-profile-specific repository test to extend.

## Pattern Assignments

### `QuestBoard.Repository/Entities/CharacterImageEntity.cs`, `DungeonMasterProfileImageEntity.cs`, `ContactImageEntity.cs` (model, CRUD)

**Analog:** each other (all three are byte-identical in shape today)

**Current shape** (`CharacterImageEntity.cs:1-17`, identical in the other two except class/table/nav-property names):
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("CharacterImages")]
public class CharacterImageEntity : IEntity
{
    [Key]
    [ForeignKey(nameof(Character))]
    public int Id { get; set; }

    [Required]
    public byte[] ImageData { get; set; } = [];

    public virtual CharacterEntity Character { get; set; } = null!;
}
```

**Target shape (per D-02):** rename `ImageData` → `OriginalImageData` (keep `[Required]`), add `CroppedImageData` (nullable, no `[Required]`):
```csharp
[Table("CharacterImages")]
public class CharacterImageEntity : IEntity
{
    [Key]
    [ForeignKey(nameof(Character))]
    public int Id { get; set; }

    [Required]
    public byte[] OriginalImageData { get; set; } = [];

    public byte[]? CroppedImageData { get; set; }

    public virtual CharacterEntity Character { get; set; } = null!;
}
```
Apply identically to `DungeonMasterProfileImageEntity.cs` and `ContactImageEntity.cs` — only the class name, `[Table(...)]` value, and navigation property/type differ (`DungeonMasterProfile`/`DungeonMasterProfileEntity`, `Contact`/`ContactEntity`). No Fluent API change needed in `QuestBoardContext.OnModelCreating` for any of the three (Character/DM have explicit `HasOne().WithOne()` Fluent config, Contact relies on attribute-only inference — both already work today per RESEARCH.md Pitfall 3; this rename touches neither).

---

### `QuestBoard.Repository/CharacterRepository.cs` (repository, CRUD)

**Analog:** itself — widen in place.

**Current read pattern** (`CharacterRepository.cs:61-70`):
```csharp
/// <inheritdoc/>
public async Task<byte[]?> GetCharacterProfilePictureAsync(int id, CancellationToken token = default)
{
    // Rooted at the filtered Characters DbSet (not CharacterImages directly) so the
    // CharacterEntity group filter applies — a cross-group id returns null here.
    return await DbContext.Characters
        .Where(c => c.Id == id)
        .Select(c => c.ProfileImage != null ? c.ProfileImage.ImageData : null)
        .FirstOrDefaultAsync(token);
}
```
This is the pattern to clone for `GetCharacterOriginalPictureAsync` (`.ProfileImage.OriginalImageData`) and `GetCharacterCroppedPictureAsync` (`.ProfileImage.CroppedImageData ?? c.ProfileImage.OriginalImageData`). Keep the "rooted at the owner DbSet, not the image DbSet directly" comment/discipline — it's what makes group scoping apply; do not root the new methods at `DbContext.Set<CharacterImageEntity>()`.

**Current upsert pattern** (`CharacterRepository.cs:129-155`):
```csharp
/// <inheritdoc/>
public async Task UpdateProfileImageAsync(int characterId, byte[]? imageData, CancellationToken token = default)
{
    var entity = await DbContext.Characters
        .Include(c => c.ProfileImage)
        .FirstOrDefaultAsync(c => c.Id == characterId, token);
    if (entity == null) return;

    if (imageData == null)
    {
        entity.ProfileImage = null;
    }
    else if (entity.ProfileImage == null)
    {
        entity.ProfileImage = new CharacterImageEntity
        {
            Id = entity.Id,
            ImageData = imageData
        };
    }
    else
    {
        entity.ProfileImage.ImageData = imageData;
    }

    await DbContext.SaveChangesAsync(token);
}
```
Widen to the two-parameter form shown in RESEARCH.md's Pattern 2 (`byte[]? originalImageData, byte[]? croppedImageData`) — same `Include` + null-check + single `SaveChangesAsync` shape, just setting two properties on the same tracked instance instead of one. Preserve the exact comment style (explain *why*, not *what*).

**`UpdateAsync` override interaction** (`CharacterRepository.cs:72-127`, esp. lines 79-95): this method restores `entity.ProfileImage = trackedProfileImage` after `Mapper.Map(model, entity)` specifically because AutoMapper's default child-navigation mapping would otherwise detach the entity EF is tracking. No change needed here for this phase — the widened `UpdateProfileImageAsync` call still happens *before* this method in `CharacterService.UpdateAsync` (see below), so the tracked `ProfileImage` this method restores already has both columns set correctly. Do not duplicate the two-column logic into `UpdateAsync` itself.

---

### `QuestBoard.Repository/DungeonMasterProfileRepository.cs` and `QuestBoard.Repository/ContactRepository.cs` (repository, CRUD)

**Analog:** `CharacterRepository.cs` (near copy-paste; only the DbSet name and image-entity type differ).

`DungeonMasterProfileRepository.GetProfilePictureAsync` (lines 39-46) and `UpsertProfileImageAsync` (lines 48-74) and `ContactRepository.GetContactImageAsync` (lines 47-56) and `UpdateProfileImageAsync` (lines 81-107) follow the identical shape to `CharacterRepository`'s methods above — widen the same way. One difference to note: `DungeonMasterProfileRepository.GetProfilePictureAsync` is rooted directly at `DbContext.DungeonMasterProfileImages` (not at `DbContext.DungeonMasterProfiles`), unlike Character/Contact's "rooted at owner DbSet" pattern — this is because DM profiles have no group-scoping query filter to preserve. Keep this same distinction when adding `GetOriginalPictureAsync`/`GetCroppedPictureAsync` for DM — do not introduce an unnecessary `.Include`/owner-DbSet root that doesn't exist today.

`ContactRepository.UpdateAsync` (lines 58-79) has the same "restore tracked ProfileImage after Mapper.Map" pattern as `CharacterRepository.UpdateAsync` — no change needed there either, same reasoning as above.

---

### `QuestBoard.Domain/Interfaces/ICharacterRepository.cs`, `IDungeonMasterProfileRepository.cs`, `IContactRepository.cs` (interface, CRUD)

**Analog:** each other + RESEARCH.md's own Code Examples section already gives the exact target shape.

**Current signatures to widen** (`ICharacterRepository.cs:27-35`):
```csharp
/// <summary>
/// Returns the raw profile image bytes for a character, or null if none is set.
/// </summary>
Task<byte[]?> GetCharacterProfilePictureAsync(int id, CancellationToken token = default);

/// <summary>
/// Sets, replaces, or clears (when imageData is null) the character's profile image.
/// </summary>
Task UpdateProfileImageAsync(int characterId, byte[]? imageData, CancellationToken token = default);
```
Target shape is given verbatim in RESEARCH.md's "Code Examples → Repository interface widening" section — copy that directly. Same doc-comment style (plain `<summary>`, no GSD IDs) applies to `IDungeonMasterProfileRepository.cs:12-20` and `IContactRepository.cs:20-29`.

---

### `QuestBoard.Domain/Services/CharacterService.cs`, `ContactService.cs`, `DungeonMasterProfileService.cs` (service, CRUD)

**Analog:** each other; `CharacterService.UpdateAsync` is the cleanest single-call-site analog.

**Current pattern** (`CharacterService.cs:66-71`):
```csharp
/// <inheritdoc/>
public override async Task UpdateAsync(Character model, CancellationToken token = default)
{
    await repository.UpdateProfileImageAsync(model.Id, model.ProfilePicture, token);
    await repository.UpdateAsync(model, token);
}
```
`ContactService.UpdateAsync` (lines 21-26) is byte-identical in shape. This is exactly the call site RESEARCH.md's Pitfall 4 and Open Question 1 discuss — the Service layer must pass the correct "other" column value (existing stored value, not null) when widening this call, mirroring how `model.ProfilePicture`/`model.ContactImageData` already round-trips today via the GET-action-populates-ViewModel-hidden-field flow. This is the single highest-risk change in the whole phase; the plan should make the Service layer fetch the current image row (or accept a pre-resolved pair from the caller) before calling the widened repository method.

`DungeonMasterProfileService.UpsertProfileAsync` (lines 16-38) is structurally different (branches lazy-create vs. existing-profile update, and already threads a `removeImage` bool) — widen both branches' `UpsertProfileImageAsync` calls to pass both byte arrays, preserving the existing `imageBytes != null || removeImage` gating logic shape.

---

### `QuestBoard.Domain/Interfaces/IImageValidationService.cs` + `QuestBoard.Domain/Services/ImageValidationService.cs` (NEW — service, request-response)

**Analog:** No direct repository-backed service analog exists (this is a stateless validator, not a CRUD service over an `IBaseRepository<T>`). Closest structural analogs:
- `IEmailRenderService.cs` (`QuestBoard.Domain/Interfaces/IEmailRenderService.cs:1-11`) — a small, single-purpose Domain interface with no `IBaseService<T>` base, registered directly in `ServiceExtensions.cs`. Use this as the shape template for `IImageValidationService` (plain interface, no repository dependency, one or two focused methods).
- The 5 existing inline validation blocks (`CharactersController.cs:127-148,249-272`, `ContactsController.cs:100-120,180-202`, `DungeonMasterController.cs:107-120`) are the *logic* source — copy the MIME-allowlist/extension/size-check logic verbatim into the new service, don't re-derive it.

**Exact target shape** — RESEARCH.md's Architecture Patterns → Pattern 4 gives a complete, ready-to-use interface + implementation (`QuestBoard.Domain/Interfaces/IImageValidationService.cs` and `QuestBoard.Domain/Services/ImageValidationService.cs` code blocks, lines 326-401 of RESEARCH.md). Use that directly as the implementation starting point — do not re-derive the validation constants.

**IMPORTANT — corrects RESEARCH.md's Open Question 3 / Assumption A2:** RESEARCH.md recommended primitive parameters (`byte[] bytes, string contentType, string fileName`) over `IFormFile`, reasoning that `QuestBoard.Domain` "today has no `IFormFile` usage anywhere" and flagged whether Domain has an `Microsoft.AspNetCore.Http.Abstractions` reference as an open, unverified assumption (A2). **Direct read of `QuestBoard.Domain.csproj` (lines 1-17) confirms Domain already has `<FrameworkReference Include="Microsoft.AspNetCore.App" />`** — the full ASP.NET Core shared framework, which includes `Microsoft.AspNetCore.Http.Abstractions` and therefore `IFormFile`, is already available to `QuestBoard.Domain` with zero new package reference needed. This means the interface can validly take `IFormFile?` directly if the planner prefers that shape over primitives — both are equally valid from a layering standpoint now that A2 is resolved. The planner should make an explicit choice (RESEARCH.md's own primitive-parameter recommendation is still reasonable for testability/decoupling, but "Domain can't reference IFormFile" is no longer a valid constraint driving that choice).

**DI registration pattern to copy** — see Shared Patterns → DI Registration below.

---

### `QuestBoard.Service/Controllers/Characters/CharactersController.cs`, `DungeonMasterController.cs`, `ContactsController.cs` (controller, request-response file upload)

**Analog:** each other; `CharactersController.Create`/`Edit` is the fullest example (has both MIME+extension+size checks, unlike DM's size-only check).

**Current inline validation block to replace** (`CharactersController.cs:127-148`, `Create` action):
```csharp
// Handle profile picture upload
if (viewModel.ProfilePictureFile != null && viewModel.ProfilePictureFile.Length > 0)
{
    var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif" };
    if (!allowedMimeTypes.Contains(viewModel.ProfilePictureFile.ContentType,
        StringComparer.OrdinalIgnoreCase))
    {
        ModelState.AddModelError(nameof(viewModel.ProfilePictureFile),
            "Only JPG, PNG, or GIF images are accepted.");
        return View(viewModel);
    }
    const long maxFileSizeBytes = 5 * 1024 * 1024;
    if (viewModel.ProfilePictureFile.Length > maxFileSizeBytes)
    {
        ModelState.AddModelError(nameof(viewModel.ProfilePictureFile),
            "Profile picture cannot exceed 5 MB.");
        return View(viewModel);
    }
    using var memoryStream = new MemoryStream();
    await viewModel.ProfilePictureFile.CopyToAsync(memoryStream, token);
    viewModel.ProfilePicture = memoryStream.ToArray();
}
```
The identical block repeats at `CharactersController.cs:249-272` (Edit), `ContactsController.cs:100-120` (Create), `ContactsController.cs:180-202` (Edit), and a size-only variant at `DungeonMasterController.cs:107-120` (EditProfile — this one is missing the MIME/extension check entirely, a pre-existing gap this phase's consolidation closes as a side effect).

**Replacement call site pattern** (from RESEARCH.md Pattern 4, "Controller call site"):
```csharp
var validationErrors = imageValidationService.ValidateImagePair(
    viewModel.ProfilePictureFile, nameof(viewModel.ProfilePictureFile),
    null, string.Empty); // no cropped file exists yet in this phase

foreach (var error in validationErrors)
{
    ModelState.AddModelError(error.FieldName, error.Message);
}
if (!ModelState.IsValid)
{
    return View(viewModel);
}
```
Constructor-inject `IImageValidationService imageValidationService` into all three controllers alongside their existing dependencies (matches the existing primary-constructor DI style, e.g. `CharactersController(ICharacterService characterService, IUserService userService, IActiveGroupContext activeGroupContext, IMapper mapper)` at `CharactersController.cs:13-17`).

**Existing serving/retrieval pattern to mirror for new original/cropped GET actions** (`CharactersController.cs:360-375`):
```csharp
[HttpGet]
public async Task<IActionResult> GetProfilePicture(int id, CancellationToken token = default)
{
    var profilePicture = await characterService.GetCharacterProfilePictureAsync(id, token);
    if (profilePicture == null)
    {
        return NotFound();
    }

    return File(profilePicture, DetectImageMimeType(profilePicture));
}

private static string DetectImageMimeType(byte[] data) =>
    data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 ? "image/png" :
    data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 ? "image/gif" :
    "image/jpeg";
```
`DungeonMasterController.GetDMProfilePicture` (lines 127-142) and `ContactsController.GetContactImage` (lines 317-347, note this one also re-checks `IsVisibleTo`/group membership before serving bytes — the strictest of the three) are the other two analogs. Any new "get original"/"get cropped" action must copy the exact same auth-check sequence already in front of the existing action, not just the `File(...)`/MIME-sniff tail.

---

### `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (config, DI registration)

**Current pattern** (`QuestBoard.Domain/Extensions/ServiceExtensions.cs:9-26`):
```csharp
public static class ServiceExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailSettings>().BindConfiguration("EmailSettings");

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPlayerSignupService, PlayerSignupService>();
        services.AddScoped<IQuestService, QuestService>();
        services.AddScoped<IShopService, ShopService>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IDungeonMasterProfileService, DungeonMasterProfileService>();
        services.AddScoped<IGroupService, GroupService>();

        return services;
    }
}
```
Add one line: `services.AddScoped<IImageValidationService, ImageValidationService>();` — same `AddScoped<TInterface, TImpl>()` convention as every existing registration. No changes needed to `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (`AddRepositoryServices`, lines 11-34) since no new repository interface is being added — only existing repository interfaces are widened.

---

### `QuestBoard.Repository/Automapper/EntityProfile.cs` (config, transform)

**Current pattern** (`EntityProfile.cs:87-137`, three separate mapping blocks):
```csharp
// Character mapping
CreateMap<Character, CharacterEntity>()
    .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (int)src.Status))
    .ForMember(dest => dest.Role, opt => opt.MapFrom(src => (int)src.Role))
    .ForMember(dest => dest.ProfileImage, opt => opt.MapFrom(src => src.ProfilePicture == null
        ? null
        : new CharacterImageEntity
        {
            ImageData = src.ProfilePicture
        }));

CreateMap<CharacterEntity, Character>()
    .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (CharacterStatus)src.Status))
    .ForMember(dest => dest.Role, opt => opt.MapFrom(src => (CharacterRole)src.Role))
    .ForMember(dest => dest.ProfilePicture, opt => opt.MapFrom(src => src.ProfileImage != null
        ? src.ProfileImage.ImageData
        : null));

// Contact mapping
CreateMap<Contact, ContactEntity>()
    .ForMember(dest => dest.ProfileImage, opt => opt.MapFrom(src => src.ContactImageData == null
        ? null
        : new ContactImageEntity
        {
            ImageData = src.ContactImageData
        }))
    .ForMember(dest => dest.Notes, opt => opt.Ignore());

CreateMap<ContactEntity, Contact>()
    .ForMember(dest => dest.ContactImageData, opt => opt.MapFrom(src => src.ProfileImage != null
        ? src.ProfileImage.ImageData
        : null));

// DungeonMasterProfile mappings
CreateMap<DungeonMasterProfileEntity, DungeonMasterProfile>()
    .ForMember(dest => dest.ProfilePicture, opt => opt.MapFrom(src =>
        src.ProfileImage != null ? src.ProfileImage.ImageData : null));

CreateMap<DungeonMasterProfile, DungeonMasterProfileEntity>()
    .ForMember(dest => dest.ProfileImage, opt => opt.Ignore());
```
Every `.ImageData` reference (6 occurrences across the 3 pairs) becomes `.OriginalImageData` per D-02 — this is a pure find-and-replace of the property name in this file, no structural change to the mapping shape. Per RESEARCH.md's own Code Examples "Decision point," do **not** add a `CroppedProfilePicture`-style Domain-model property or a corresponding `CroppedImageData` mapping this phase — D-04 means nothing produces a cropped byte array yet, so only the read-side repository methods need the new column; the full-entity AutoMapper round-trip stays original-only.

---

### `QuestBoard.Repository/Migrations/{timestamp}_RenameImageColumnsAddCropped.cs` (NEW, migration, batch/DDL)

**Analog:** `20260304113417_MoveCharacterImagesToSeparateTable.cs` (full file read, 63 lines) — establishes this codebase's precedent for hand-editing a scaffolded migration rather than trusting the auto-generated output, and for using `migrationBuilder.Sql(...)` when a raw data-preserving statement is needed.

**What to reuse from that migration:** the file-level structure (`Up`/`Down` pair, `#nullable disable`, `partial class ... : Migration`, `/// <inheritdoc />` XML comments with no GSD IDs) and the *discipline* of hand-verifying the scaffolder's output before accepting it — that migration used `CreateTable` + `Sql` backfill + `DropColumn`; this phase's migration uses `RenameColumn` + `AddColumn` instead (per RESEARCH.md Pattern 1, which gives the complete `Up`/`Down` code for all three tables — copy that directly, it is already correct and ready to use).

**Do not copy:** the `CreateTable`/`Sql INSERT`/`DropColumn` shape itself — that was for moving a column *out* of the owner table into a new one; this migration renames+adds columns on tables that already exist, which is `RenameColumn`/`AddColumn` only (no `CreateTable`, no `Sql` needed, since `RenameColumn` preserves data without a manual `INSERT`/`UPDATE`).

---

### `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` (extend, test)

**Analog:** itself — extend with new `[Fact]`s following the exact existing shape.

**Structure to replicate** (full file read, 168 lines):
```csharp
public class CharacterRepositoryTests
{
    private static QuestBoardContext CreateContext(string databaseName, MutableTestGroupContext groupContext)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, groupContext);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    private static async Task SeedTwoGroupCharactersAsync(QuestBoardContext context, MutableTestGroupContext groupContext)
    {
        var originalActiveGroupId = groupContext.ActiveGroupId;
        groupContext.ActiveGroupId = null; // see-all during seeding
        // ... seed Groups, UserEntities, two CharacterEntity (one per group), each with
        // ProfileImage = new CharacterImageEntity { Id = N, ImageData = [byte, byte, byte] } ...
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        groupContext.ActiveGroupId = originalActiveGroupId;
    }

    [Fact]
    public async Task GetCharacterProfilePictureAsync_ForCharacterInActiveGroup_ReturnsImageData()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetCharacterProfilePictureAsync_ForCharacterInActiveGroup_ReturnsImageData), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var picture = await repository.GetCharacterProfilePictureAsync(1, TestContext.Current.CancellationToken);

        // Assert
        picture.Should().NotBeNull();
        picture.Should().Equal([1, 2, 3]);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
```
Naming convention: `MethodName_Scenario_ExpectedResult`. Uses xUnit v3 (`TestContext.Current.CancellationToken`, not a manually-constructed `CancellationToken`) + FluentAssertions (`.Should().NotBeNull()`, `.Should().Equal([...])`). New tests per RESEARCH.md's Test Map should follow this exact shape:
- `UpdateProfileImageAsync_SetsOriginalImageData` (rename existing `ImageData` seeding to `OriginalImageData`, verify read-back)
- `GetCharacterCroppedPictureAsync_FallsBackToOriginal_WhenCroppedIsNull`
- `UpdateProfileImageAsync_ReplacesBothColumnsAtomically`
- `GetCharacterOriginalAndCroppedPictureAsync_ReturnDistinctValues`
- `UpdateProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped` (Pitfall 5 regression test)

Note: `SeedTwoGroupCharactersAsync`'s existing `ProfileImage = new CharacterImageEntity { Id = 1, ImageData = [1, 2, 3] }` (line 47) and `{ Id = 2, ImageData = [4, 5, 6] }` (line 58) must themselves be updated to `OriginalImageData = [...]` once the entity is renamed — this seed helper is shared by every existing test in the file, so it's a required update, not optional, for the file to compile post-rename.

---

### `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` (extend, test)

**Analog:** `CharacterRepositoryTests.cs` (near-identical `CreateContext`/`CreateMapper`/seed-helper/`[Fact]` shape — confirmed by partial read, lines 1-60). Same treatment: rename seeded `ImageData` → `OriginalImageData` in `SeedTwoGroupContactsAsync`, add the same five new-behavior test categories scoped to Contact.

Note: this file currently carries a header comment marking it as a "Wave 0 RED scaffold" from a prior phase (Phase 57) that intentionally referenced not-yet-existing production symbols — that comment is now stale (`ContactEntity`/`ContactImageEntity`/`ContactRepository` all exist and compile today) and unrelated to this phase; do not treat it as guidance for this phase's own test additions, and do not feel obligated to preserve or update it (it documents Phase 57's history, not this phase's approach).

---

### `QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs` (NEW)

**Analog:** `CharacterRepositoryTests.cs` — closest available shape, adapted for the facts that (a) DM profiles have no group-scoped query filter the way Character/Contact do (confirmed: `DungeonMasterProfileRepository.GetProfilePictureAsync` reads directly from `DbContext.DungeonMasterProfileImages`, not rooted at an owner DbSet — see `DungeonMasterProfileRepository.cs:39-46`), so the seed helper does not need the two-group/`MutableTestGroupContext` scaffolding Character/Contact tests use for filter verification, and (b) `DungeonMasterProfileEntity` uses `DatabaseGeneratedOption.None` (`Id = UserId`), so seeding should follow the AddAsync-then-retry-on-conflict shape documented in `DungeonMasterProfileRepository.AddAsync` (lines 16-28) if a test needs to simulate concurrent first-saves — not required for straightforward image-column tests, just noted so the new test file doesn't reinvent DM-profile seeding incorrectly.

This is a brand-new file — no existing DM-profile-specific repository test exists to extend (confirmed by direct glob, see "Confirmed" section above). Build it fresh using `CharacterRepositoryTests.cs`'s `CreateContext`/`CreateMapper` helpers verbatim (same `UseInMemoryDatabase` + `EntityProfile` mapper construction) and a simpler single-profile seed (no two-group split needed unless the planner also wants to verify DM profile picture retrieval has no unintended group leakage — check `QuestBoardContext.cs` DM profile query filter configuration during planning to confirm whether one exists before deciding test scope).

---

### `QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs` (NEW)

**No analog found.** `QuestBoard.UnitTests/Services/` does not exist as a directory in this codebase today — every existing unit test targets a Repository class against an InMemory `DbContext`. `ImageValidationService` has no repository dependency and no `DbContext`, so its test file will look structurally different: plain object construction, no `CreateContext`/seed helper needed, likely table-driven `[Theory]`/`[InlineData]` cases for MIME/extension/size combinations rather than the Arrange-seed-Act-Assert-per-`[Fact]` shape used elsewhere. Use xUnit v3 + FluentAssertions (confirmed project-wide convention from `CharacterRepositoryTests.cs`) but construct the service directly (`new ImageValidationService()`) rather than through DI or an EF context. See "No Analog Found" section.

## Shared Patterns

### DI Registration (Domain services)
**Source:** `QuestBoard.Domain/Extensions/ServiceExtensions.cs:9-26`
**Apply to:** `IImageValidationService`/`ImageValidationService`
```csharp
services.AddScoped<IImageValidationService, ImageValidationService>();
```
Add alongside the existing `AddScoped<ICharacterService, CharacterService>()` etc. lines, same convention, no special configuration needed (no `AddOptions`/`BindConfiguration` required since `ImageValidationService`'s limits are compile-time constants per RESEARCH.md's Pattern 4 example).

### Group/membership auth-check pattern for image-serving GET actions
**Source:** `ContactsController.GetContactImage` (`ContactsController.cs:317-347`, the strictest of the three existing analogs — re-derives `IsVisibleTo`/hidden-toggle visibility before serving bytes) and `DungeonMasterController.GetDMProfilePicture` (`DungeonMasterController.cs:127-142`, uses `IsTargetInActiveGroupAsync`)
**Apply to:** any new "get original image" / "get cropped image" controller actions added in this phase
```csharp
[HttpGet]
public async Task<IActionResult> GetDMProfilePicture(int id, CancellationToken token = default)
{
    if (!await IsTargetInActiveGroupAsync(id)) return NotFound();

    var bytes = await dmProfileService.GetProfilePictureAsync(id, token);
    if (bytes == null || bytes.Length == 0) return NotFound();

    var contentType = bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50
        ? "image/png"
        : bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46
        ? "image/gif"
        : "image/jpeg";

    return File(bytes, contentType);
}
```
Copy the auth-check call (`IsTargetInActiveGroupAsync`/`IsVisibleTo` depending on controller) verbatim in front of any new original/cropped read action — never re-derive or simplify it. This is called out explicitly in RESEARCH.md's Security Domain (IDOR mitigation) as a "must copy, not reinvent" requirement.

### Magic-byte Content-Type sniffing (serving, not upload)
**Source:** duplicated identically in `CharactersController.cs:372-375`, `ContactsController.cs:344-347`, and inline in `DungeonMasterController.cs:134-139`
```csharp
private static string DetectImageMimeType(byte[] data) =>
    data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 ? "image/png" :
    data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 ? "image/gif" :
    "image/jpeg";
```
This is a pre-existing triplicated helper (not part of D-03's validation-consolidation scope, which is about *upload-side* MIME/size checks, not *serving*-side sniffing) — RESEARCH.md's scope note treats closing this particular duplication as optional/out of scope for this phase. Reuse the existing per-controller private method for any new original/cropped GET action rather than centralizing it, unless the planner deliberately wants to fold it into `IImageValidationService` too (not required by any locked decision).

### Constructor-injection DI style for controllers
**Source:** all three controllers use C# primary-constructor DI, e.g. `CharactersController.cs:13-17`
```csharp
public class CharactersController(
    ICharacterService characterService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IMapper mapper) : Controller
```
Add `IImageValidationService imageValidationService` as one more primary-constructor parameter in all three controllers, same style, no other DI mechanism used anywhere in this codebase's controllers.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs` | test | request-response | No `QuestBoard.UnitTests/Services/` directory exists yet — every current unit test targets a Repository+InMemory-DbContext pair; this is the first pure-Domain-service (no repository, no DbContext) unit test in the codebase. Use xUnit v3 + FluentAssertions (confirmed project-wide) but there is no existing file to model the Arrange/Act/Assert shape on beyond the general convention — build fresh, likely `[Theory]`-driven for the MIME/extension/size matrix. |
| `QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs` | test | CRUD | Confirmed absent from the codebase (see "Confirmed" section) — not an analog gap so much as a "build using `CharacterRepositoryTests.cs`'s shape, simplified for DM's lack of group-scoping" task, called out separately here because RESEARCH.md flagged it as unconfirmed and it needed direct verification. |

## Metadata

**Analog search scope:** `QuestBoard.Repository/` (Entities, root repository classes, Automapper, Migrations, Extensions), `QuestBoard.Domain/` (Interfaces, Services, Extensions), `QuestBoard.Service/Controllers/{Characters,DungeonMaster,Contacts}/`, `QuestBoard.Service/ViewModels/{CharacterViewModels,ContactViewModels,DungeonMasterViewModels}/`, `QuestBoard.UnitTests/Repository/`
**Files scanned:** 24 files read directly (3 entities, 3 repositories, 3 repository interfaces, 3 services, 3 controllers, 2 ServiceExtensions.cs, 1 AutoMapper profile, 1 migration, 2 ViewModels + 1 EditDMProfileViewModel, 2 test files, 1 .csproj, 2 service interfaces for the new-service shape)
**Pattern extraction date:** 2026-07-07
