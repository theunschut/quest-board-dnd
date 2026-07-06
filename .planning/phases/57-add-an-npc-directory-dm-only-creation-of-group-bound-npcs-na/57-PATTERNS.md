# Phase 57: Contacts (NPC Directory) - Pattern Map

**Mapped:** 2026-07-06
**Files analyzed:** 22 (new) + 6 (modified)
**Analogs found:** 22 / 22

**Naming note:** Follows RESEARCH.md's correction — the mirror target is the already-renamed `CharactersController`/`Views/Characters/`/`CharacterViewModel` (post-Phase-58), NOT `GuildMembersController`. `CharacterEntity`/`CharacterImageEntity`/`Character.cs` domain model were never renamed (Phase 58 only touched the Service layer) and remain accurate mirror targets as-is.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/Entities/ContactEntity.cs` | model (EF entity) | CRUD | `QuestBoard.Repository/Entities/CharacterEntity.cs` | exact |
| `QuestBoard.Repository/Entities/ContactImageEntity.cs` | model (EF entity) | file-I/O | `QuestBoard.Repository/Entities/CharacterImageEntity.cs` | exact |
| `QuestBoard.Repository/Entities/ContactNoteEntity.cs` | model (EF entity) | CRUD (child collection) | composite: `CharacterClassEntity` (child-FK shape) + `QuestEntity.DungeonMasterId` (author FK shape) | role-match (novel shape) |
| `QuestBoard.Repository/ContactRepository.cs` | service (repository) | CRUD | `QuestBoard.Repository/CharacterRepository.cs` | exact |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` (modified) | config | CRUD (query filter registration) | same file, existing `CharacterEntity`/`CharacterImageEntity`/`CharacterClassEntity` filter block (lines 297-318) | exact |
| `QuestBoard.Domain/Models/Contact.cs` | model (domain) | CRUD | `QuestBoard.Domain/Models/Character.cs` | exact |
| `QuestBoard.Domain/Interfaces/IContactRepository.cs` | service (interface) | CRUD | `QuestBoard.Domain/Interfaces/ICharacterRepository.cs` | exact |
| `QuestBoard.Domain/Interfaces/IContactService.cs` | service (interface) | CRUD | `QuestBoard.Domain/Interfaces/ICharacterService.cs` | exact |
| `QuestBoard.Domain/Services/ContactService.cs` | service | CRUD | `QuestBoard.Domain/Services/CharacterService.cs` | exact |
| `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` | controller | request-response (+ file-I/O for image) | `QuestBoard.Service/Controllers/Characters/CharactersController.cs` | exact |
| `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` | model (ViewModel) | request-response | `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` | exact |
| `QuestBoard.Service/Views/Contacts/Index.cshtml` + `.Mobile.cshtml` | component (view) | request-response | `QuestBoard.Service/Views/Characters/Index.cshtml` + `.Mobile.cshtml` | exact |
| `QuestBoard.Service/Views/Contacts/Details.cshtml` + `.Mobile.cshtml` | component (view) | request-response | `QuestBoard.Service/Views/Characters/Details.cshtml` + `.Mobile.cshtml` | exact |
| `QuestBoard.Service/Views/Contacts/Edit.cshtml` + `.Mobile.cshtml` | component (view) | request-response | `QuestBoard.Service/Views/Characters/Edit.cshtml` + `.Mobile.cshtml` | exact |
| `QuestBoard.Service/Views/Contacts/Create.cshtml` + `.Mobile.cshtml` | component (view) | request-response | `QuestBoard.Service/Views/Characters/Create.cshtml` + `.Mobile.cshtml` | exact |
| `QuestBoard.Service/Constants/SessionKeys.cs` (modified) | config | event-driven (session) | same file, `ActiveGroupId` constant | exact |
| `QuestBoard.Service/Services/ActiveGroupContextService.cs` (reference only, not modified) | service | event-driven (session read) | n/a — pattern source for toggle read/write | exact |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` (modified) | config | transform | same file, Character mapping block (lines 87-103) | exact |
| `QuestBoard.Service/Automapper/ViewModelProfile.cs` (modified) | config | transform | same file's Character mapping block | exact |
| `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (modified) | config | n/a (DI registration) | existing `ICharacterService` line | exact |
| `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (modified) | config | n/a (DI registration) | existing `ICharacterRepository` line | exact |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` (modified) | component (nav) | request-response | existing Characters `<li>` (lines 130-134) | exact |
| `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` (modified) | component (nav) | request-response | existing Characters `<li>` (mobile) | exact |
| EF Core migration `AddContactsFeature` | migration | batch | recent migration files (e.g. `AddGroupIdToCharacters`) | exact |
| `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` | test | CRUD | `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` | exact |
| `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` | test | request-response | `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs` | exact |

## Pattern Assignments

### `QuestBoard.Repository/Entities/ContactEntity.cs` (model, CRUD)

**Analog:** `QuestBoard.Repository/Entities/CharacterEntity.cs` (read in full, 51 lines)

**Full pattern to mirror:**
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("Characters")]
public class CharacterEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public int OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public virtual UserEntity Owner { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual CharacterImageEntity? ProfileImage { get; set; }

    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;
}
```

**Adapt for Contact:**
- Replace `Name`/`Description` (keep `StringLength(2000)` on Description per D-05).
- Add `TownCity` — `[StringLength(200)]` optional string, no required attribute (D-04). No exact analog for the string length; use a reasonable free-text cap consistent with the codebase's other optional-string fields (e.g. `SheetLink` used 500 — Contact's TownCity/SubLocation are short strings, 200 is a defensible cap; confirm with planner discretion).
- Add `SubLocation` — same shape as `TownCity` (D-03).
- Replace `OwnerId`/`Owner` with `CreatedByUserId`/`CreatedByUser` (same FK-to-UserEntity shape, but **carries no authorization meaning** per D-07 — do not name it `OwnerId` to avoid implying the `CanManageCharacterAsync`-style owner check that does NOT apply here).
- Add `IsRevealed` — `public bool IsRevealed { get; set; } = false;` (D-14 default hidden).
- Keep `ProfileImage` (→ `ContactImageEntity`), `GroupId`/`Group`, `CreatedAt` verbatim.
- Add `virtual ICollection<ContactNoteEntity> Notes { get; set; } = [];` (mirrors `Classes` collection shape).
- No `Level`/`SheetLink`/`Backstory`/`Status`/`Role`/`PlayerSignups` — those are Character-specific, do not carry over.

---

### `QuestBoard.Repository/Entities/ContactImageEntity.cs` (model, file-I/O)

**Analog:** `QuestBoard.Repository/Entities/CharacterImageEntity.cs` (read in full, 17 lines) — copy verbatim, renamed:
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("ContactImages")]
public class ContactImageEntity : IEntity
{
    [Key]
    [ForeignKey(nameof(Contact))]
    public int Id { get; set; }

    [Required]
    public byte[] ImageData { get; set; } = [];

    public virtual ContactEntity Contact { get; set; } = null!;
}
```
1:1 FK-as-PK — `Id` is both PK and FK to `ContactEntity.Id`, exactly as `CharacterImageEntity` does.

---

### `QuestBoard.Repository/Entities/ContactNoteEntity.cs` (model, CRUD child collection)

**No direct single analog** — RESEARCH.md's Pattern 3 already fully designed this (composite of `CharacterClassEntity`'s child-FK shape + `QuestEntity.DungeonMasterId`'s author-FK shape). Use RESEARCH.md's exact code as written (lines 164-192 of RESEARCH.md):
```csharp
[Table("ContactNotes")]
public class ContactNoteEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int ContactId { get; set; }

    [ForeignKey(nameof(ContactId))]
    public virtual ContactEntity Contact { get; set; } = null!;

    [Required]
    [StringLength(2000)]
    public string Text { get; set; } = string.Empty;

    [Required]
    public int AuthorUserId { get; set; }

    [ForeignKey(nameof(AuthorUserId))]
    public virtual UserEntity Author { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
```

---

### `QuestBoard.Repository/Entities/QuestBoardContext.cs` (modified — query filter registration)

**Analog:** same file, lines 297-318 (`CharacterEntity`/`CharacterClassEntity`/`CharacterImageEntity` filters, read in full)

**Exact pattern to copy (with the same critical warning comment preserved):**
```csharp
// ContactEntity deliberately does NOT offer a SuperAdmin cross-group view like Quest/ShopItem
// do above — same "per-group roster" shape as CharacterEntity. An empty Contact list when no
// group is selected is the intended behavior here, not an oversight.
modelBuilder.Entity<ContactEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

modelBuilder.Entity<ContactImageEntity>()
    .HasQueryFilter(ci =>
        activeGroupContext.ActiveGroupId != null &&
        ci.Contact.GroupId == activeGroupContext.ActiveGroupId);

modelBuilder.Entity<ContactNoteEntity>()
    .HasQueryFilter(cn =>
        activeGroupContext.ActiveGroupId != null &&
        cn.Contact.GroupId == activeGroupContext.ActiveGroupId);
```
**CRITICAL (copy this warning verbatim from the existing code comment, lines 251-252):** Do NOT capture `activeGroupContext.ActiveGroupId` into a local variable before the lambda — always reference the service instance inside the lambda. Do NOT add a SuperAdmin cross-group bypass (unlike `QuestEntity`/`ShopItemEntity`) — Contacts is a per-group roster shape like Characters, not a platform-admin cross-group shape.

**Insertion point:** immediately after the existing `CharacterImageEntity` filter block (after line 318), before the `UserEntity` exclusion comment.

---

### `QuestBoard.Repository/CharacterRepository.cs` → `ContactRepository.cs` (service, CRUD)

**Analog:** read in full (157 lines)

**Core CRUD/query pattern** (lines 12-49):
```csharp
public async Task<IList<Character>> GetAllCharactersWithDetailsAsync(CancellationToken token = default)
{
    var entities = await DbContext.Characters
        .Include(c => c.Owner)
        .Include(c => c.ProfileImage)
        .Include(c => c.Classes)
        .OrderByDescending(c => c.Status == 0)
        .ThenBy(c => c.Owner.Name)
        .ThenBy(c => c.Name)
        .ToListAsync(token);
    return Mapper.Map<IList<Character>>(entities);
}
```
Adapt: `GetAllContactsWithDetailsAsync(bool includeHidden, int currentUserId, CancellationToken token)` — per RESEARCH.md Pitfall 2, apply the 3-branch visibility rule as an explicit `.Where(...)` here (creator-sees-own-hidden OR `IsRevealed` OR `includeHidden` toggle), NOT as a query filter. Order `.OrderBy(c => c.Name)` per D-17 (flat alphabetical, no owner grouping).

**Image byte-fetch pattern** (lines 62-70) — copy verbatim, rename `Contacts`/`ProfileImage`→`Image`:
```csharp
public async Task<byte[]?> GetCharacterProfilePictureAsync(int id, CancellationToken token = default)
{
    return await DbContext.Characters
        .Where(c => c.Id == id)
        .Select(c => c.ProfileImage != null ? c.ProfileImage.ImageData : null)
        .FirstOrDefaultAsync(token);
}
```

**UpdateAsync child-collection reconciliation pattern (CRITICAL — Pitfall 4)** (lines 73-127): Per RESEARCH.md's Assumption A3/Pitfall 4, the recommended approach is Option (a) — do NOT route Notes through the generic `UpdateAsync`. Give `ContactRepository` dedicated `AddNoteAsync`/`UpdateNoteAsync`/`DeleteNoteAsync` methods that directly manipulate `DbContext.Set<ContactNoteEntity>()` (or a `ContactNotes` DbSet), bypassing AutoMapper's collection-replacement problem entirely. `ContactRepository.UpdateAsync` (for Name/Image/Description/TownCity/SubLocation only, no Notes) can be a simpler straight `Mapper.Map(model, entity)` — restoring `ProfileImage` from tracked state exactly as `CharacterRepository.UpdateAsync` line 95 does (`entity.ProfileImage = trackedProfileImage;`), since image updates go through a separate `UpdateProfileImageAsync` call (lines 130-155, copy verbatim renamed).

**Note ordering** (D-10, newest first) — new query, no direct precedent needed beyond `.OrderByDescending(n => n.CreatedAt)`.

---

### `QuestBoard.Service/Controllers/Characters/CharactersController.cs` → `ContactsController.cs` (controller, request-response)

**Analog:** read in full (390 lines)

**Imports pattern** (lines 1-8):
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.CharacterViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
```

**Class-level auth pattern** (line 12) — Character uses `[Authorize]` at class level, then per-action guards. For Contacts, per D-09b, put `[Authorize(Policy = "DungeonMasterOnly")]` directly on Create/Edit/Delete/reveal-toggle actions (class-level stays plain `[Authorize]` since Index/Details/notes actions are open to all group members):
```csharp
[Authorize]
public class ContactsController(
    IContactService contactService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IMapper mapper) : Controller
{
    [HttpGet]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Create(...) { ... }
}
```

**Index pattern** (lines 19-47) — adapt: read the "Show Hidden" toggle from session (only relevant for DM-tier viewers), call `GetAllContactsWithDetailsAsync(includeHidden, currentUser.Id)`, order alphabetically flat (D-17) rather than My/Other split.

**Create POST — image upload validation block** (lines 102-148) — copy verbatim, renamed `viewModel.ProfilePictureFile`→`viewModel.ContactImageFile` (or similar), `character`→`contact`. Tag `contact.GroupId = activeGroupContext.RequireActiveGroupId();` (line 154) — identical pattern. Also set `contact.CreatedByUserId = currentUser.Id;` and `contact.IsRevealed = false;` (D-14 default).

**Details — role resolution pattern** (lines 50-78) — adapt per RESEARCH.md Pitfall 1/Pitfall 2: instead of `IsOwner`/`CanEdit`, compute the 3-branch visibility check (creator OR revealed OR (DM-tier AND toggle-on)) and return `NotFound()` if none apply (D-13). Still use `GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId())` (line 67) to resolve DM-tier for the toggle-visibility branch, exactly as Character's Details does for `CanEdit`.

**Edit/Delete pattern** (lines 161-320) — simplified vs. Character: no `CanManageCharacterAsync`-style owner-or-admin guard needed (D-09b is a flat policy gate, not a per-record ownership check) — the `[Authorize(Policy = "DungeonMasterOnly")]` attribute alone is the security boundary. **Do not port `CanManageCharacterAsync`** (RESEARCH.md Anti-Pattern, explicitly called out).

**Image-serving pattern** (lines 360-375) — copy `GetProfilePicture`/`DetectImageMimeType` verbatim, renamed:
```csharp
[HttpGet]
public async Task<IActionResult> GetContactImage(int id, CancellationToken token = default)
{
    var image = await contactService.GetContactImageAsync(id, token);
    if (image == null) return NotFound();
    return File(image, DetectImageMimeType(image));
}

private static string DetectImageMimeType(byte[] data) =>
    data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 ? "image/png" :
    data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 ? "image/gif" :
    "image/jpeg";
```

**New actions with no Character analog:**
- `ToggleReveal` (POST, `[Authorize(Policy = "DungeonMasterOnly")]`) — mirrors `ToggleRetirement`'s shape (lines 322-358: fetch, guard, flip a bool/enum field, `UpdateAsync`, redirect to Details) but flips `IsRevealed` instead of `Status`.
- `ToggleShowHidden` (POST, `[Authorize(Policy = "DungeonMasterOnly")]`) — new, uses the session pattern below (Shared Patterns section).
- `AddNote`/`EditNote`/`DeleteNote` (POST, plain `[Authorize]`, no DM-tier gate per D-09) — call the dedicated `ContactService` note methods, redirect to `Details`.

---

### `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` → `ContactViewModel.cs`

**Analog:** read in full (107 lines)

**Full imports + validation attribute pattern to copy verbatim** (`MaxFileSizeAttribute`/`AllowedExtensionsAttribute`, lines 60-106) — these are generic and directly reusable without modification; either duplicate them into `ContactViewModel.cs` or (better, avoids duplication) leave them in `CharacterViewModel.cs`'s namespace and reference via `using QuestBoard.Service.ViewModels.CharacterViewModels;` if the planner prefers a shared location — RESEARCH.md flags this as a planner discretion point ("reuse verbatim (or move to a shared location... functionally identical)").

**Field pattern to adapt** (lines 6-46):
```csharp
public class ContactViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Contact name is required")]
    [StringLength(100, ErrorMessage = "Contact name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; set; }

    [StringLength(200, ErrorMessage = "Town/city cannot exceed 200 characters")]
    public string? TownCity { get; set; }

    [StringLength(200, ErrorMessage = "Sub-location cannot exceed 200 characters")]
    public string? SubLocation { get; set; }

    public byte[]? ContactImage { get; set; }

    [MaxFileSize(5 * 1024 * 1024, ErrorMessage = "Image cannot exceed 5 MB")]
    [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".gif" }, ErrorMessage = "Only image files (JPG, PNG, GIF) are allowed")]
    public IFormFile? ContactImageFile { get; set; }

    public bool IsRevealed { get; set; }

    public int CreatedByUserId { get; set; }

    public bool CanReveal { get; set; }   // DM-tier viewer flag, computed server-side like CanEdit
    public bool CanManage { get; set; }   // drives Edit/Delete button visibility

    public List<ContactNoteViewModel> Notes { get; set; } = [];
}

public class ContactNoteViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(2000, ErrorMessage = "Note cannot exceed 2000 characters")]
    public string Text { get; set; } = string.Empty;

    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```
No `IsOwner`/`CanEdit` boolean pair like Character — those encoded owner-or-admin. Contacts instead needs `CanManage` (= DM-tier per D-09b, drives Edit/Delete/Reveal-toggle button visibility) computed the same way Character computes `CanEdit` (line 75: `role == GroupRole.Admin` equivalent, but here it's simply "is DM-tier" with no owner branch since there's no owner concept, per D-07/D-09b).

---

### Views (`Views/Contacts/*.cshtml` + `.Mobile.cshtml`)

**Analog:** `Views/Characters/Details.cshtml` (read in full, 173 lines) — directly demonstrates the `modern-card`/`modern-card-header`/`modern-card-body` convention already mandated by CLAUDE.md.

**Portrait card pattern** (lines 10-57) — copy structure verbatim: image-or-placeholder-icon, name, badges. For Contacts, replace the Status/Role badges (lines 30-55) with a single "Hidden" muted badge shown per D-15's visibility rules:
```html
@if (Model.IsRevealed == false)
{
    <span class="badge bg-secondary fs-6">
        <i class="fas fa-eye-slash me-1"></i>Hidden
    </span>
}
```
Replace `@Model.OwnerName` (line 28) with `@Model.TownCity` / `@Model.SubLocation`.

**Actions card pattern** (lines 59-96) — copy structure verbatim, gate the whole card on `Model.CanManage` (not `Model.CanEdit`/owner check), swap "Retire/Reactivate" form for a "Reveal/Hide" toggle form (`asp-action="ToggleReveal"`), keep Delete form's `onsubmit="return confirm(...)"` pattern verbatim with updated confirm text.

**Right-column info card pattern** (lines 99-163) — copy structure verbatim for Description; drop Level/SheetLink/Classes (Character-specific); add a new Notes card below it per D-16/D-20 (add-note form + newest-first list with per-note edit/delete inline controls — no existing analog for the notes UI itself, build fresh using the same `modern-card` shell and `form-control-plaintext`/`form-label fw-bold` label conventions used throughout this file).

**Back-link pattern** (lines 165-169) — copy verbatim, renamed "Back to Contacts".

For `Index`/`Edit`/`Create` and all `.Mobile.cshtml` counterparts, read the corresponding Character view at build time and mirror 1:1 (same `modern-card` shell, same button layout convention from CLAUDE.md: `d-flex justify-content-between`, filled colored buttons, FontAwesome + `me-2`, `<hr>` before button section).

---

## Shared Patterns

### DungeonMasterOnly authorization policy
**Source:** `QuestBoard.Service/Authorization/DungeonMasterHandler.cs` (read in full, 37 lines)
**Apply to:** `ContactsController`'s Create/Edit/Delete/ToggleReveal/ToggleShowHidden actions (attribute-only — confirms SuperAdmin bypass + Admin/DungeonMaster group-role success, verified per D-09b)
```csharp
[Authorize(Policy = "DungeonMasterOnly")]
```
**Pitfall (per RESEARCH.md Pitfall 1):** This policy resolves differently from `GetEffectiveGroupRoleAsync` for the null-active-group case — do not conflate the two. Use the policy attribute for action gating; use `GetEffectiveGroupRoleAsync` separately in the controller/view for computing `CanManage`/toggle-visibility flags.

### Group-scoped session toggle (Show Hidden)
**Source:** `QuestBoard.Service/Constants/SessionKeys.cs` (read in full, 12 lines) + `QuestBoard.Service/Services/ActiveGroupContextService.cs` (read in full, 37 lines)
**Apply to:** `ContactsController.Index` (read) and a new `ToggleShowHidden` POST action (write)
```csharp
// Add to SessionKeys.cs, alongside existing ActiveGroupId/ActiveGroupName consts:
public static string ShowHiddenContactsKey(int groupId) => $"ShowHiddenContacts_{groupId}";

// Read (mirrors ActiveGroupContextService's HttpContext.Session?.GetInt32 usage):
var groupId = activeGroupContext.RequireActiveGroupId();
var showHidden = HttpContext.Session.GetInt32(SessionKeys.ShowHiddenContactsKey(groupId)) == 1;

// Write:
HttpContext.Session.SetInt32(SessionKeys.ShowHiddenContactsKey(groupId), showHidden ? 1 : 0);
```
Always call `RequireActiveGroupId()` (not the nullable `ActiveGroupId` property) before building the key, per RESEARCH.md Pitfall 3.

### Group-scoped EF Core query filter (fail-closed, no SuperAdmin bypass)
**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs` lines 297-318 (CharacterEntity/CharacterClassEntity/CharacterImageEntity filters, read in full)
**Apply to:** `ContactEntity`, `ContactImageEntity`, `ContactNoteEntity`
See full pattern under `QuestBoardContext.cs` file section above. Never capture `ActiveGroupId` into a local variable in `OnModelCreating`.

### Image upload validation (MaxFileSize / AllowedExtensions)
**Source:** `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` lines 60-106 (read in full)
**Apply to:** `ContactViewModel.ContactImageFile`
Reuse `MaxFileSizeAttribute(5 * 1024 * 1024)` and `AllowedExtensionsAttribute([".jpg", ".jpeg", ".png", ".gif"])` verbatim (D-06). Do not write new validation logic.

### Image MIME-sniffing on read
**Source:** `QuestBoard.Service/Controllers/Characters/CharactersController.cs` lines 372-375 (`DetectImageMimeType`, read in full)
**Apply to:** `ContactsController.GetContactImage`
Copy verbatim — byte-signature check for PNG (`0x89 0x50`)/GIF (`0x47 0x49`), else JPEG.

### AutoMapper Entity↔Domain and Domain↔ViewModel mapping shape
**Source:** `QuestBoard.Repository/Automapper/EntityProfile.cs` lines 87-103 (Character mapping, read in full)
```csharp
CreateMap<Contact, ContactEntity>()
    .ForMember(dest => dest.ContactImage, opt => opt.MapFrom(src => src.ContactImageData == null
        ? null
        : new ContactImageEntity { ImageData = src.ContactImageData }));

CreateMap<ContactEntity, Contact>()
    .ForMember(dest => dest.ContactImageData, opt => opt.MapFrom(src => src.ContactImage != null
        ? src.ContactImage.ImageData
        : null));
```
**Apply to:** both `EntityProfile.cs` (Entity↔Domain) and `ViewModelProfile.cs` (Domain↔ViewModel, same shape, follow the existing Character block in that file too — not read this session but structurally identical per CLAUDE.md's "AutoMapper runs at two boundaries" note).

### DI registration insertion points
**Source:** `QuestBoard.Domain/Extensions/ServiceExtensions.cs` line 20, `QuestBoard.Repository/Extensions/ServiceExtensions.cs` line 24 (existing Character lines, per RESEARCH.md Code Examples)
```csharp
services.AddScoped<IContactService, ContactService>();
services.AddScoped<IContactRepository, ContactRepository>();
```

### Nav link insertion (both desktop + mobile layouts)
**Source:** `QuestBoard.Service/Views/Shared/_Layout.cshtml` lines 130-134 (read in full via grep context, existing Characters `<li>`)
```razor
<li class="nav-item">
    <a class="nav-link" asp-controller="Contacts" asp-action="Index">
        <i class="fas fa-address-book me-1"></i>Contacts
    </a>
</li>
```
Insert immediately after the existing Characters `<li>` (after line 134), before the `@if (activeBoardType == BoardType.OneShot)` block (line 135) — this places it in the unconditional "all board types" section per D-19. Mirror the identical insertion in `_Layout.Mobile.cshtml` (icon uses `me-2` there per RESEARCH.md's Code Examples, matching that file's existing spacing convention).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `QuestBoard.Repository/Entities/ContactNoteEntity.cs` | model | CRUD | No existing "freeform authored + timestamped note list" entity in this codebase — composed from two existing patterns (see Pattern Assignments section above); not a gap in coverage, just a novel composite shape RESEARCH.md already fully designed |
| Notes UI block on `Details.cshtml` (add-note form + inline edit/delete list) | component | request-response | No existing view in this codebase renders an editable, author-attributed, timestamped list — build fresh using the `modern-card` shell and existing label/form-control conventions already present in `Details.cshtml` |
| "Show Hidden" toggle button UI (Index page) | component | event-driven | No existing toggle-button UI in Characters/Quest Log to mirror — new small form/button posting to `ToggleShowHidden`, styled consistently with the existing "+ Character" button already on `Views/Characters/Index.cshtml` |

## Metadata

**Analog search scope:** `QuestBoard.Repository/Entities/`, `QuestBoard.Repository/` (repositories), `QuestBoard.Domain/Models/`, `QuestBoard.Domain/Interfaces/`, `QuestBoard.Domain/Services/`, `QuestBoard.Service/Controllers/Characters/`, `QuestBoard.Service/ViewModels/CharacterViewModels/`, `QuestBoard.Service/Views/Characters/`, `QuestBoard.Service/Views/Shared/`, `QuestBoard.Service/Constants/`, `QuestBoard.Service/Services/`, `QuestBoard.Service/Authorization/`, `QuestBoard.Repository/Automapper/`, `QuestBoard.Repository/Entities/QuestBoardContext.cs`
**Files scanned/read in full:** `CharactersController.cs`, `CharacterEntity.cs`, `CharacterImageEntity.cs`, `CharacterViewModel.cs`, `CharacterRepository.cs`, `Character.cs` (domain), `QuestBoardContext.cs` (query filter block), `EntityProfile.cs`, `SessionKeys.cs`, `ActiveGroupContextService.cs`, `DungeonMasterHandler.cs`, `Views/Characters/Details.cshtml`, `Views/Shared/_Layout.cshtml` (nav block)
**Pattern extraction date:** 2026-07-06
