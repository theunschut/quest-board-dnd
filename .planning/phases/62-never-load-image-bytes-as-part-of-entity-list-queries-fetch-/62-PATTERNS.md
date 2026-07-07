# Phase 62: Stop eagerly loading image bytes in list/entity queries - Pattern Map

**Mapped:** 2026-07-07
**Files analyzed:** 15 (3 repositories, 2 services, 3 domain models, 4 viewmodels, 2 automapper profiles, 3 controllers — some files appear in multiple roles)
**Analogs found:** 15 / 15 (all analogs are in-repo, no external analogs needed — this phase composes two patterns that already exist in this codebase)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|-----------------|---------------|
| `QuestBoard.Repository/CharacterRepository.cs` (3 methods) | repository | CRUD (read-projection) | `QuestBoard.Repository/QuestRepository.cs:335-354` (`ProjectWithoutCharacterImages`) + `CharacterRepository.cs:62-70` (`GetCharacterOriginalPictureAsync`) | exact — both halves of the pattern already live in this same file/sibling file |
| `QuestBoard.Repository/ContactRepository.cs` (2 methods) | repository | CRUD (read-projection) | Same as above, plus `ContactRepository.cs:12-30` own existing post-map `foreach` (Notes reorder) as the merge-step precedent | exact |
| `QuestBoard.Repository/DungeonMasterProfileRepository.cs` (1 method) | repository | CRUD (read-projection) | `DungeonMasterProfileRepository.cs:40-58` (`GetOriginalPictureAsync`/`GetCroppedPictureAsync`, non-group-scoped scalar projection) | exact |
| `QuestBoard.Domain/Services/CharacterService.cs` (`UpdateAsync` no-upload branch) | service | request-response | Same file, own `UpdateAsync` cropped-image branch (`repository.GetCharacterCroppedPictureAsync`, line ~102) — extend the identical idiom to the original-image branch | exact (self-referential — mirror an already-shipped branch in the same method) |
| `QuestBoard.Domain/Services/ContactService.cs` (`UpdateAsync` no-upload branch) | service | request-response | Same file, own `UpdateAsync` cropped-image branch (`repository.GetContactCroppedImageAsync`, line ~57) | exact |
| `QuestBoard.Domain/Models/Character.cs` | model | — | `QuestBoard.Service/ViewModels/DungeonMasterViewModels/DMProfileViewModel.cs:8` (`HasProfilePicture` bool — the target end-state shape) | role-match (existing bool precedent, different layer) |
| `QuestBoard.Domain/Models/Contact.cs` | model | — | Same as above | role-match |
| `QuestBoard.Domain/Models/DungeonMasterProfile.cs` | model | — | Same as above | role-match |
| `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` | model (viewmodel) | transform | `DMProfileViewModel.HasProfilePicture` (bool) | exact — this IS the target shape being copied |
| `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` | model (viewmodel) | transform | `DMProfileViewModel.HasProfilePicture` | exact |
| `QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs` | model (viewmodel) | transform | `DMProfileViewModel.HasProfilePicture` | exact |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` | config (mapping) | transform | Own existing `ForMember(dest => dest.ProfilePicture, ...)` conditional-null-guard mappings (lines 91-103, 122-125, 135-140) | exact — same file, add sibling `ForMember(..., opt.Ignore())` entries |
| `QuestBoard.Service/Automapper/ViewModelProfile.cs` | config (mapping) | transform | Own existing `CreateMap<Character, CharacterViewModel>` / `CreateMap<Contact, ContactViewModel>` (lines 62-85) | exact — same file, rename mapped member |
| `QuestBoard.Service/Controllers/Characters/CharactersController.cs` | controller | request-response | Own existing Create/Edit POST actions (lines 130-155, 215-316) | exact — self-modification, see Pitfall notes below |
| `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` | controller | request-response | Own existing Create/Edit POST actions (lines 102-127, 176-242) | exact — self-modification |
| `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` | controller | request-response | Own existing `Profile` GET action's `HasProfilePicture = profile?.ProfilePicture?.Length > 0` (line 43) — the ALREADY-CORRECT bool pattern; only needs its source expression updated | exact |
| Views (Characters/Contacts/DungeonMaster Index/Details/Edit/Create, desktop + mobile) | component (Razor view) | request-response | `Views/DungeonMaster/Profile.cshtml`/`.Mobile.cshtml` (already renders via `@if (Model.HasProfilePicture)`, confirmed correct per CONTEXT.md) | exact — this is the target end-state every other view is converging toward |

## Pattern Assignments

### `QuestBoard.Repository/CharacterRepository.cs` — `GetAllCharactersWithDetailsAsync`, `GetCharactersByOwnerIdAsync`, `GetCharacterWithDetailsAsync` (repository, CRUD read-projection)

**Analog 1 (omit-include half):** `QuestBoard.Repository/QuestRepository.cs:335-354` — `ProjectWithoutCharacterImages`

```csharp
private static IQueryable<QuestEntity> ProjectWithoutCharacterImages(IQueryable<QuestEntity> query)
{
    return query
        .AsNoTracking()
        .AsSplitQuery()
        .Include(q => q.PlayerSignups)
            .ThenInclude(ps => ps.Character)
                .ThenInclude(c => c!.Classes)      // Character included, but NOT c.ProfileImage
        ...;
}
```
The applicable half of this pattern: simply delete the `.Include(c => c.ProfileImage)` line from each of the three methods. `Owner`/`Classes` includes stay untouched.

**Analog 2 (scalar-projection half):** `QuestBoard.Repository/CharacterRepository.cs:62-70` — `GetCharacterOriginalPictureAsync` (already in this same file)

```csharp
public async Task<byte[]?> GetCharacterOriginalPictureAsync(int id, CancellationToken token = default)
{
    // Rooted at the filtered Characters DbSet (not CharacterImages directly) so the
    // CharacterEntity group filter applies — a cross-group id returns null here.
    return await DbContext.Characters
        .Where(c => c.Id == id)
        .Select(c => c.ProfileImage != null ? c.ProfileImage.OriginalImageData : null)
        .FirstOrDefaultAsync(token);
}
```
For a boolean instead of bytes: `.Select(c => c.ProfileImage != null)`.

**Current code being modified (lines 12-49):**
```csharp
public async Task<IList<Character>> GetAllCharactersWithDetailsAsync(CancellationToken token = default)
{
    var entities = await DbContext.Characters
        .Include(c => c.Owner)
        .Include(c => c.ProfileImage)   // ← DELETE THIS LINE
        .Include(c => c.Classes)
        .OrderByDescending(c => c.Status == 0) // 0 = Active
        .ThenBy(c => c.Owner.Name)
        .ThenBy(c => c.Name)
        .ToListAsync(token);
    return Mapper.Map<IList<Character>>(entities);   // ← add post-map foreach setting HasProfilePicture
}
```
Same shape applies to `GetCharactersByOwnerIdAsync` (lines 26-38) and `GetCharacterWithDetailsAsync` (lines 41-49, single-entity — use the single-row scalar query, not a dictionary merge).

**Merge-step precedent (post-map foreach on a list method):** `QuestBoard.Repository/ContactRepository.cs:24-29` (own sibling file) already does exactly this shape for a different field:
```csharp
var contacts = Mapper.Map<IList<Contact>>(entities);
foreach (var contact in contacts)
{
    contact.Notes = [.. contact.Notes.OrderByDescending(n => n.CreatedAt)];
}
return contacts;
```
Use the identical `foreach` shape to assign `character.HasProfilePicture` from a `Dictionary<int, bool>` built via `.Select(c => new { c.Id, HasImage = c.ProfileImage != null }).ToDictionaryAsync(...)`.

**Note on `GetMainCharacterForUserAsync` (lines 52-59):** out of scope — confirmed dead code, do not touch.

---

### `QuestBoard.Repository/ContactRepository.cs` — `GetAllContactsWithDetailsAsync`, `GetContactWithDetailsAsync` (repository, CRUD read-projection)

**Analog:** same two patterns as CharacterRepository above (`ProjectWithoutCharacterImages` omit-include + `GetContactOriginalImageAsync` at `ContactRepository.cs:48-56`, same file).

**Current code (lines 12-45):**
```csharp
public async Task<IList<Contact>> GetAllContactsWithDetailsAsync(CancellationToken token = default)
{
    var entities = await DbContext.Contacts
        .Include(c => c.ProfileImage)   // ← DELETE
        .Include(c => c.CreatedByUser)
        .Include(c => c.Notes).ThenInclude(n => n.Author)
        .OrderBy(c => c.Name)
        .ToListAsync(token);

    var contacts = Mapper.Map<IList<Contact>>(entities);
    foreach (var contact in contacts)
    {
        contact.Notes = [.. contact.Notes.OrderByDescending(n => n.CreatedAt)];
        // ← ADD: contact.HasContactImage = imageFlags.GetValueOrDefault(contact.Id);
    }
    return contacts;
}
```
The existing `foreach` loop over `contacts` is the exact insertion point for the new `HasContactImage` assignment — no new loop needed, extend the one that's already there.

`GetContactWithDetailsAsync` (lines 33-45) is single-entity — mirror the `GetCharacterWithDetailsAsync` single-row scalar shape.

---

### `QuestBoard.Repository/DungeonMasterProfileRepository.cs` — `GetProfileByUserIdAsync` (repository, CRUD read-projection)

**Analog:** `DungeonMasterProfileRepository.cs:40-58` — `GetOriginalPictureAsync`/`GetCroppedPictureAsync` (same file), notably **not group-scoped** (roots directly at `DbContext.DungeonMasterProfileImages`, unlike Character/Contact which must root at the owner DbSet):

```csharp
public async Task<byte[]?> GetOriginalPictureAsync(int userId, CancellationToken token = default)
{
    // DM profile images are not group-scoped, so this reads directly from the image
    // DbSet rather than rooting at an owner DbSet (unlike Character/Contact).
    return await DbContext.DungeonMasterProfileImages
        .Where(p => p.Id == userId)
        .Select(p => p.OriginalImageData)
        .FirstOrDefaultAsync(token);
}
```

**Current code (lines 31-37):**
```csharp
public async Task<DungeonMasterProfile?> GetProfileByUserIdAsync(int userId, CancellationToken token = default)
{
    var entity = await DbContext.DungeonMasterProfiles
        .Include(p => p.ProfileImage)   // ← DELETE
        .FirstOrDefaultAsync(p => p.Id == userId, token);
    return entity == null ? null : Mapper.Map<DungeonMasterProfile>(entity);
    // ← after mapping, set HasProfilePicture via a scalar query against DungeonMasterProfileImages, same non-group-scoped rooting as GetOriginalPictureAsync
}
```

---

### `QuestBoard.Domain/Services/CharacterService.cs` — `UpdateAsync` no-upload branch (service, request-response)

**CRITICAL — this file IS in scope despite CONTEXT.md's "services are no longer in scope" statement.** RESEARCH.md's Pitfall 1 (A2 in Assumptions Log, HIGH risk) identifies that removing the `.Include` from `GetCharacterWithDetailsAsync` makes `model.ProfilePicture` always null on the round-trip, which will silently wipe every character's photo on "no new upload" edits unless this branch is fixed.

**Analog — the exact idiom to mirror already exists in this same method, one branch over** (`CharacterService.cs:97-103`, the cropped-image branch that Phase 45-02 already fixed the same way):
```csharp
else
{
    // No new file; model.ProfilePicture is the round-tripped existing original. Fetch
    // the currently-stored crop and pass it through unchanged so it survives an
    // unrelated-field edit.
    croppedImageData = await repository.GetCharacterCroppedPictureAsync(model.Id, token);
}

await repository.UpdateWithProfileImageAsync(model, model.ProfilePicture, croppedImageData, token);
```

**Required change:** extend the identical "don't trust the round-tripped value, re-fetch fresh" idiom to the original-image argument too, using the already-existing `repository.GetCharacterOriginalPictureAsync(model.Id, token)` (`CharacterRepository.cs:62-70`, untouched by this phase):
```csharp
byte[]? originalImageData = hasNewOriginalUpload
    ? model.ProfilePicture
    : await repository.GetCharacterOriginalPictureAsync(model.Id, token);

await repository.UpdateWithProfileImageAsync(model, originalImageData, croppedImageData, token);
```

---

### `QuestBoard.Domain/Services/ContactService.cs` — `UpdateAsync` no-upload branch (service, request-response)

**Analog:** identical structure to `CharacterService.UpdateAsync` above — same file's own cropped-image branch (`ContactService.cs:52-57`):
```csharp
else
{
    croppedImageData = await repository.GetContactCroppedImageAsync(model.Id, token);
}

await repository.UpdateWithProfileImageAsync(model, model.ContactImageData, croppedImageData, token);
```
Apply the identical fix using the already-existing `repository.GetContactOriginalImageAsync(model.Id, token)` (`ContactRepository.cs:48-56`, untouched by this phase).

**`DungeonMasterProfileService.UpsertProfileAsync` needs NO equivalent fix** — confirmed by RESEARCH.md Open Question 2: it's already explicit-signal-based (`removeImage`/new bytes from the controller), never round-trips through a fetched `ProfilePicture`.

---

### `QuestBoard.Domain/Models/Character.cs`, `Contact.cs`, `DungeonMasterProfile.cs` (model, additive property)

**Analog:** `DungeonMasterProfile.cs` itself is the simplest of the three models today (no bool yet) — the actual bool-property *shape* to copy is `DMProfileViewModel.HasProfilePicture` (bool) at the ViewModel layer, since no Domain model in this codebase has this bool yet. This is a **new** property on all three Domain models, additive alongside the existing byte[] property — do NOT remove `ProfilePicture`/`ContactImageData`.

```csharp
// Character.cs — add alongside existing ProfilePicture (line 14)
public bool HasProfilePicture { get; set; }

// Contact.cs — add alongside existing ContactImageData (line 13)
public bool HasContactImage { get; set; }

// DungeonMasterProfile.cs — add alongside existing ProfilePicture (line 7)
public bool HasProfilePicture { get; set; }
```

---

### `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs`, `ContactViewModel.cs`, `EditDMProfileViewModel.cs` (viewmodel, transform — D-05)

**Analog:** `QuestBoard.Service/ViewModels/DungeonMasterViewModels/DMProfileViewModel.cs:8` — the already-correct target shape:
```csharp
public bool HasProfilePicture { get; set; }
```

**Current code to replace:**
- `CharacterViewModel.cs:35` — `public byte[]? ProfilePicture { get; set; }` → `public bool HasProfilePicture { get; set; }`
  - **Caution:** `ProfilePictureFile`/`CroppedPictureFile` (`IFormFile?`, lines 39-43) are unrelated upload-binding properties — leave those alone, only the raw byte[] display property is renamed.
- `ContactViewModel.cs:22` — `public byte[]? ContactImage { get; set; }` → `public bool HasContactImage { get; set; }`
- `EditDMProfileViewModel.cs:13` — `public byte[]? ProfilePicture { get; set; }   // populated from DB; drives current-image display` → `public bool HasProfilePicture { get; set; }`

---

### `QuestBoard.Repository/Automapper/EntityProfile.cs` (config, transform — Entity ↔ Domain boundary)

**Analog:** own existing conditional-null-guard `ForMember` mappings (lines 91-103, 122-125, 135-140) — same file, same idiom, add a sibling `.Ignore()` entry per RESEARCH.md's Code Examples section:

```csharp
// Existing, unchanged:
CreateMap<CharacterEntity, Character>()
    .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (CharacterStatus)src.Status))
    .ForMember(dest => dest.Role, opt => opt.MapFrom(src => (CharacterRole)src.Role))
    .ForMember(dest => dest.ProfilePicture, opt => opt.MapFrom(src => src.ProfileImage != null
        ? src.ProfileImage.OriginalImageData
        : null))
    // ADD:
    .ForMember(dest => dest.HasProfilePicture, opt => opt.Ignore()); // set explicitly post-map by the repository — not derivable from a single CharacterEntity field
```
Same `.Ignore()` addition for `CreateMap<ContactEntity, Contact>()` (lines 122-125, target `HasContactImage`) and `CreateMap<DungeonMasterProfileEntity, DungeonMasterProfile>()` (lines 135-137, target `HasProfilePicture`).

**Reverse-direction maps (`Character → CharacterEntity` etc., lines ~85-96, ~113-120, ~139-140) are unaffected** — `HasProfilePicture`/`HasContactImage` are read-only display flags, never written back to an entity.

---

### `QuestBoard.Service/Automapper/ViewModelProfile.cs` (config, transform — Domain ↔ ViewModel boundary)

**Analog:** own existing `ForMember` for the byte[]→byte[] passthrough being replaced (lines 62-85):

```csharp
// Current (line 79):
CreateMap<Contact, ContactViewModel>()
    .ForMember(dest => dest.ContactImage, opt => opt.MapFrom(src => src.ContactImageData))
    ...

// Becomes:
CreateMap<Contact, ContactViewModel>()
    .ForMember(dest => dest.HasContactImage, opt => opt.MapFrom(src => src.HasContactImage))
    ...
```
`CreateMap<Character, CharacterViewModel>()` (lines 62-67) likely maps `ProfilePicture` by convention (same name on both sides) rather than an explicit `ForMember` — check at implementation time; renaming both sides to `HasProfilePicture` should preserve convention-based mapping with zero explicit `ForMember` needed, mirroring how `DMProfileViewModel.HasProfilePicture` requires no special mapping today.

**`CharacterViewModel → Character` / `ContactViewModel → Contact` (reverse direction, lines 68-76, 83-86) — per Pitfall 4, do NOT map `HasProfilePicture`/`HasContactImage` back onto the Domain model.** These are write-path staging properties in the reverse direction (`viewModel.ProfilePicture = memoryStream.ToArray()` today); per D-05's nuance, the controller must instead use a local variable and set the mapped Domain model's byte[] property directly post-map — see controller pattern below.

---

### `QuestBoard.Service/Controllers/Characters/CharactersController.cs` (controller, request-response)

**Pitfall 4 fix — Create POST (lines 130-165):**
```csharp
// Current (line 155):
await newProfilePictureFile.CopyToAsync(memoryStream, token);
viewModel.ProfilePicture = memoryStream.ToArray();   // ← won't compile once ViewModel.ProfilePicture is a bool
...
var character = mapper.Map<Character>(viewModel);    // line 165 (approx)

// Required change:
await newProfilePictureFile.CopyToAsync(memoryStream, token);
var uploadedOriginalImageData = memoryStream.ToArray();   // local variable, not staged on the ViewModel
...
var character = mapper.Map<Character>(viewModel);
character.ProfilePicture = uploadedOriginalImageData;      // set directly on the mapped Domain model, post-map
```

**Edit POST (lines 215-316) — already correctly uses the Domain model directly, NOT the ViewModel, for staging:**
```csharp
var existingCharacter = await characterService.GetCharacterWithDetailsAsync(id, token);   // line 215
...
var hasNewOriginalUpload = viewModel.ProfilePictureFile != null && viewModel.ProfilePictureFile.Length > 0;   // line 267
...
if (hasNewOriginalUpload)
{
    ...
    await newProfilePictureFile.CopyToAsync(memoryStream, token);
    existingCharacter.ProfilePicture = memoryStream.ToArray();   // line 297 — Domain model, unaffected by ViewModel rename
}
...
await characterService.UpdateAsync(existingCharacter, hasNewOriginalUpload, newCroppedImageData, token);   // line 316
```
This Edit POST path needs **no change** for the ViewModel rename itself (it already avoids staging bytes on the ViewModel) — its only exposure is the service-layer fix (see `CharacterService.UpdateAsync` above), which this controller depends on transitively but doesn't itself need to change.

**Analog for `GetProfilePicture` GET endpoint (line 397) — unaffected, already correctly isolated per D-01, no changes needed.**

---

### `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` (controller, request-response)

**Identical pattern to CharactersController** — Create POST (lines 102-127) needs the local-variable fix (`viewModel.ContactImage = memoryStream.ToArray()` at line 127 → local var + post-map assignment to `contact.ContactImageData`); Edit POST (lines 176-242) already stages onto the Domain model (`existingContact.ContactImageData = memoryStream.ToArray()` at line 227) and needs no ViewModel-rename-related change, only depends on the `ContactService.UpdateAsync` fix.

---

### `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` (controller, request-response)

**Analog — this controller ALREADY has the target bool pattern for one of its two call sites** (`Profile` GET, line 43):
```csharp
HasProfilePicture = profile?.ProfilePicture?.Length > 0,   // line 43 — already a bool target, just needs its source expression updated
```
**Required change (Pitfall 2):**
```csharp
HasProfilePicture = profile?.HasProfilePicture ?? false,   // source is now the repository-projected bool, not a byte[] length check
```

**`EditProfile` GET (line 77) — currently stages the raw byte[] onto `EditDMProfileViewModel.ProfilePicture` for a `Length > 0` check in the view:**
```csharp
// Current:
ProfilePicture = profile?.ProfilePicture   // line 77

// Required change, per D-05 + Pitfall 2:
HasProfilePicture = profile?.HasProfilePicture ?? false
```

**`EditProfile` POST (lines 83-148) — unaffected.** It builds `imageBytes`/`newCroppedImageData` from freshly-uploaded `IFormFile`s only (lines 108-143), never reads `viewModel.ProfilePicture`/`profile.ProfilePicture` — this is D-04's already-correct explicit-signal pattern, no round-trip hazard exists here.

---

## Shared Patterns

### Omit-Include-entirely for list/detail display queries
**Source:** `QuestBoard.Repository/QuestRepository.cs:335-354` (`ProjectWithoutCharacterImages`)
**Apply to:** All 6 repository read methods (`CharacterRepository` x3, `ContactRepository` x2, `DungeonMasterProfileRepository` x1)
```csharp
// Simply delete the .Include(x => x.ProfileImage) line; EF Core leaves the reference
// navigation null on the materialized entity, no exception, no extra SQL.
```

### Scalar boolean projection off an un-included 1:1 navigation
**Source:** `QuestBoard.Repository/CharacterRepository.cs:62-70` (`GetCharacterOriginalPictureAsync`), same idiom in `ContactRepository.cs:48-56` and `DungeonMasterProfileRepository.cs:40-47`
**Apply to:** All 6 repository read methods, to compute `HasProfilePicture`/`HasContactImage`
```csharp
.Select(c => c.ProfileImage != null)   // boolean existence check — EF Core translates to JOIN/EXISTS, never selects byte columns
```

### Post-map foreach to merge a computed field onto Mapper.Map output
**Source:** `QuestBoard.Repository/ContactRepository.cs:24-29` (existing `Notes` reorder foreach)
**Apply to:** All 3 list-returning repository methods (`GetAllCharactersWithDetailsAsync`, `GetCharactersByOwnerIdAsync`, `GetAllContactsWithDetailsAsync`) — extend the existing loop where one already exists (Contacts), add a new one where none exists yet (Characters)
```csharp
var mapped = Mapper.Map<IList<T>>(entities);
foreach (var item in mapped)
{
    item.HasX = imageFlags.GetValueOrDefault(item.Id);
}
return mapped;
```

### Re-fetch fresh instead of trusting a round-tripped Domain model byte[] property on write
**Source:** `QuestBoard.Domain/Services/CharacterService.cs` `UpdateAsync`'s existing cropped-image branch (lines ~97-103), and the identical idiom in `ContactService.cs` (lines ~52-57) — Phase 45-02's shipped pattern, now extended to the original-image argument too
**Apply to:** `CharacterService.UpdateAsync`, `ContactService.UpdateAsync` no-upload branches — the single highest-risk item in this phase (RESEARCH.md Pitfall 1 / Assumption A2, HIGH risk if skipped)
```csharp
byte[]? originalImageData = hasNewOriginalUpload
    ? model.ProfilePicture
    : await repository.GetCharacterOriginalPictureAsync(model.Id, token);   // don't trust the round-tripped null
```

### AutoMapper `.Ignore()` for a computed field not derivable from a single source field
**Source:** `QuestBoard.Repository/Automapper/EntityProfile.cs` existing conditional `ForMember` mappings (lines 91-103, 122-125, 135-140) as the pattern to extend
**Apply to:** `EntityProfile.cs`'s three `Entity → Domain` maps, for `HasProfilePicture`/`HasContactImage`
```csharp
.ForMember(dest => dest.HasProfilePicture, opt => opt.Ignore()); // set explicitly post-map by the repository
```

### Local variable + post-map assignment for write-path upload staging
**Source:** RESEARCH.md Pitfall 4 (documented target, mirrors how `existingCharacter.ProfilePicture = memoryStream.ToArray()` already stages directly onto the Domain model in the Edit POST path, `CharactersController.cs:297`)
**Apply to:** `CharactersController.Create` POST, `ContactsController.Create` POST (the two Create paths that currently stage bytes onto the ViewModel, which will no longer compile once the ViewModel property becomes a bool)
```csharp
var uploadedOriginalImageData = memoryStream.ToArray();   // local, not on the ViewModel
var character = mapper.Map<Character>(viewModel);
character.ProfilePicture = uploadedOriginalImageData;      // post-map, direct on Domain model
```

## No Analog Found

None — every file in scope has a strong, exact in-repo analog. This phase is explicitly scoped (per RESEARCH.md) as "composing two already-solved patterns," not introducing anything novel.

## Metadata

**Analog search scope:** `QuestBoard.Repository/`, `QuestBoard.Domain/Services/`, `QuestBoard.Domain/Models/`, `QuestBoard.Service/ViewModels/`, `QuestBoard.Service/Automapper/`, `QuestBoard.Service/Controllers/Characters/`, `QuestBoard.Service/Controllers/Contacts/`, `QuestBoard.Service/Controllers/DungeonMaster/`
**Files scanned:** 15 direct reads + 2 targeted greps (ViewModelProfile.cs member lookups, controller line lookups)
**Pattern extraction date:** 2026-07-07
