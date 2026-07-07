# Phase 62: Stop eagerly loading image bytes in list/entity queries - Research

**Researched:** 2026-07-07
**Domain:** EF Core 10 query projection (avoiding eager-loaded byte[] columns) across a three-layer clean architecture with two AutoMapper boundaries
**Confidence:** HIGH

## Summary

This phase is a narrow, mechanical EF Core refactor: remove `.Include(x => x.ProfileImage)` from six read methods across three repositories and replace it with a projected `HasProfilePicture`/`HasContactImage` boolean, without ever selecting the image byte columns. The codebase already has two directly relevant precedents that resolve every open question in `<research_focus>`:

1. **`QuestRepository.ProjectWithoutCharacterImages`** (`QuestRepository.cs:335-354`) proves the "omit `.Include()` entirely" half of the pattern — no projection trick needed, just don't call `.Include(...ProfileImage)`.
2. **`CharacterRepository.GetCharacterOriginalPictureAsync`** (`CharacterRepository.cs:62-70`) proves the "project a scalar off a related 1:1 navigation without ever including it" half — `.Where(c => c.Id == id).Select(c => c.ProfileImage != null ? c.ProfileImage.OriginalImageData : null)` translates to a LEFT JOIN + scalar select, never touching unwanted columns.

The critical discovery from reading the actual controller code (not just CONTEXT.md's summary) is that **the Domain model's `ProfilePicture`/`ContactImageData` byte[] property cannot be dropped or repurposed** — `CharactersController.Edit` POST and `ContactsController.Edit` POST both read `GetCharacterWithDetailsAsync`/`GetContactWithDetailsAsync`, mutate `.ProfilePicture`/`.ContactImageData` on the returned Domain model only when a new file was uploaded, and pass `model.ProfilePicture`/`model.ContactImageData` straight into `repository.UpdateWithProfileImageAsync(model, model.ProfilePicture, croppedImageData, token)`. If the eager-load is removed and `ProfilePicture` becomes permanently null, this round-trip breaks: every "edit without changing the photo" would wipe the stored image, because `null` reaches `ApplyProfileImage` as `originalImageData == null`, which sets `entity.ProfileImage = null`.

**Primary recommendation:** Add a **new, additive** `HasProfilePicture`/`HasContactImage` boolean to each of the three Domain models (`Character`, `Contact`, `DungeonMasterProfile`) alongside the existing byte[] property — do NOT replace or repurpose the byte[] property. Project the boolean at the repository query level using the `ProjectWithoutCharacterImages`-style pattern (omit `.Include(...ProfileImage)`, add `.Select()` re-projection or a computed member set post-map), keep AutoMapper's existing `Mapper.Map<IList<Character>>(entities)` call for the bulk of the mapping, and set the boolean as a second pass after mapping (a `foreach` loop, mirroring how `ContactRepository.GetAllContactsWithDetailsAsync` already does a post-map `foreach` to reorder `Notes`). The three read-only list/detail methods that only ever populate the boolean (never read/write the byte[] afterward for a write-path round-trip) can safely leave `ProfilePicture`/`ContactImageData` as `null` — but the **write-path callers must switch from reading `existingCharacter.ProfilePicture != null` to reading `existingCharacter.HasProfilePicture`** wherever they currently rely on the byte[] being populated for anything other than the actual re-persist call, and the re-persist call for "no new upload" must be re-derived from a fresh fetch of the actual bytes (or, more simply, the existing service-layer `GetCharacterCroppedPictureAsync`/`GetCharacterOriginalPictureAsync` on-demand fetch) rather than the round-tripped Domain model property. See `## Common Pitfalls` for the exact mechanism this requires in `CharactersController.Edit`/`ContactsController.Edit`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Omit image byte[] from list/detail SQL queries | API/Backend (Repository) | — | EF Core query shape is a repository-only concern; nothing above it should know about `.Include()` |
| Compute `HasProfilePicture`/`HasContactImage` | API/Backend (Repository, via projection) | Domain (model property) | Must be computed where the SQL is generated (repository) since that's the only tier that can see the navigation without loading it; Domain model needs the property to carry it through the AutoMapper Entity→Domain boundary |
| Display existence check in Razor views | Browser/Client (rendered HTML) | Frontend Server (Razor view, SSR) | Views already render `<img src>` via dedicated per-entity endpoints (Phase 46); they only need a `bool` for the `@if` gate, no image bytes ever reach this tier |
| Fetch actual image bytes for `<img>` src | API/Backend (dedicated `GetProfilePicture`/`GetContactImage`/`GetDMProfilePicture` endpoints) | — | Already correctly isolated per D-01/D-04; unaffected by this phase |
| Write-path image persistence (Create/Edit POST) | API/Backend (Service + Repository `UpdateWithProfileImageAsync`) | — | Already correctly isolated by Phase 45-02's `hasNewOriginalUpload` signal; this phase must not regress it (see Common Pitfalls) |

## Standard Stack

No new packages. This phase uses only APIs already present in the project's existing EF Core 10.0.9 / AutoMapper dependencies.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore | 10.0.9 [VERIFIED: QuestBoard.Repository.csproj] | ORM / query translation | Already the project's ORM; version confirmed via `QuestBoard.Repository.csproj:10` |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 [VERIFIED: QuestBoard.Repository.csproj] | SQL Server provider | Already in use |
| AutoMapper | (version already pinned in Repository/Service csproj — not re-verified here, unchanged by this phase) [ASSUMED: no new AutoMapper API surface needed] | Entity↔Domain, Domain↔ViewModel mapping | Already the project's mapping layer at both boundaries per CLAUDE.md |

No installation step — no new packages required for this phase.

## Package Legitimacy Audit

Not applicable — this phase installs no external packages.

## Architecture Patterns

### System Architecture Diagram

```
Browser (Razor view, e.g. Characters/Index.cshtml)
   │  @if (character.HasProfilePicture) { <img src="@Url.Action("GetCroppedPicture", ...)"> }
   ▼
CharactersController.Index (GET)
   │  calls characterService.GetAllCharactersWithDetailsAsync()
   ▼
CharacterService.GetAllCharactersWithDetailsAsync (Domain)
   │  passthrough to repository
   ▼
CharacterRepository.GetAllCharactersWithDetailsAsync (Repository)
   │
   │  DbContext.Characters
   │    .Include(c => c.Owner)          ← kept (needed for OwnerName display)
   │    .Include(c => c.Classes)        ← kept (needed for class display)
   │    [.Include(c => c.ProfileImage) REMOVED]
   │    .OrderBy...
   │    .ToListAsync()
   │  ──► Mapper.Map<IList<Character>>(entities)   [ProfileImage nav is null → ProfilePicture = null, no error]
   │  ──► foreach: character.HasProfilePicture = <projected bool from a parallel scalar query or a Select-based re-projection>
   ▼
returns IList<Character> with ProfilePicture=null, HasProfilePicture=true/false
   │
   ▼
CharactersController.Index maps IList<Character> → IList<CharacterViewModel> (AutoMapper ViewModelProfile)
   │  CharacterViewModel.HasProfilePicture ← Character.HasProfilePicture (new explicit ForMember or convention match)
   ▼
View renders @if (character.HasProfilePicture) using dedicated GetCroppedPicture/GetProfilePicture endpoint
   (image bytes fetched in a SEPARATE request, only when the <img> tag is actually requested by the browser)

── Separate, unaffected write path (Phase 45-02, already shipped) ──
CharactersController.Edit (POST)
   │  existingCharacter = characterService.GetCharacterWithDetailsAsync(id)   ← SINGLE-entity fetch, same Include removal applies
   │  if (hasNewOriginalUpload) existingCharacter.ProfilePicture = <new bytes>   ← still works: byte[] prop still exists on Domain model
   │  characterService.UpdateAsync(existingCharacter, hasNewOriginalUpload, newCroppedImageData)
   ▼
CharacterService.UpdateAsync → repository.UpdateWithProfileImageAsync(model, model.ProfilePicture, croppedImageData)
   │  when hasNewOriginalUpload=false: model.ProfilePicture must NOT be null here, or the stored image gets wiped
   ▼
   *** THIS IS THE HAZARD (see Common Pitfalls) — GetCharacterWithDetailsAsync's Include removal
       makes model.ProfilePicture always null, breaking the "no new upload" branch. ***
```

### Recommended Project Structure

No new files or folders. Changes are confined to:
```
QuestBoard.Repository/
├── CharacterRepository.cs              # remove 3x .Include(ProfileImage), add HasProfilePicture projection
├── ContactRepository.cs                # remove 2x .Include(ProfileImage), add HasContactImage projection
├── DungeonMasterProfileRepository.cs   # remove 1x .Include(ProfileImage), add HasProfilePicture projection
└── Automapper/EntityProfile.cs         # add HasProfilePicture/HasContactImage mapping (or Ignore if hand-set post-map)

QuestBoard.Domain/
└── Models/
    ├── Character.cs                    # add HasProfilePicture bool (additive, keep ProfilePicture)
    ├── Contact.cs                      # add HasContactImage bool (additive, keep ContactImageData)
    └── DungeonMasterProfile.cs         # add HasProfilePicture bool (additive, keep ProfilePicture)

QuestBoard.Service/
├── ViewModels/.../CharacterViewModel.cs         # ProfilePicture byte[]? → HasProfilePicture bool
├── ViewModels/.../ContactViewModel.cs           # ContactImage byte[]? → HasContactImage bool
├── ViewModels/.../EditDMProfileViewModel.cs     # ProfilePicture byte[]? → HasProfilePicture bool
├── Automapper/ViewModelProfile.cs               # update Character↔CharacterViewModel, Contact↔ContactViewModel maps
├── Controllers/Characters/CharactersController.cs   # fix write-path reliance on ProfilePicture null-check (see Pitfall 1)
├── Controllers/Contacts/ContactsController.cs        # same fix for ContactImageData
├── Controllers/DungeonMaster/DungeonMasterController.cs  # HasProfilePicture source changes (see Pitfall 1)
└── Views/**                                      # `!= null` → bool property name (mechanical, per D-05)
```

### Pattern 1: Omit `.Include()` entirely for list/detail display queries (existing codebase precedent)
**What:** Simply do not call `.Include(x => x.ProfileImage)` on the query. EF Core leaves the reference navigation `null` on the materialized entity; no exception, no extra SQL.
**When to use:** Whenever a query needs the parent entity's own columns but never the related 1:1 image row's bytes.
**Example:**
```csharp
// Source: existing codebase pattern, QuestRepository.cs:335-354 (ProjectWithoutCharacterImages)
private static IQueryable<QuestEntity> ProjectWithoutCharacterImages(IQueryable<QuestEntity> query)
{
    return query
        .AsNoTracking()
        .AsSplitQuery()
        .Include(q => q.ProposedDates)
            .ThenInclude(pd => pd.PlayerVotes)
                .ThenInclude(pv => pv.PlayerSignup)
                    .ThenInclude(ps => ps!.Player)
        .Include(q => q.PlayerSignups)
            .ThenInclude(ps => ps.Player)
        .Include(q => q.PlayerSignups)
            .ThenInclude(ps => ps.DateVotes)
        .Include(q => q.PlayerSignups)
            .ThenInclude(ps => ps.Character)
                .ThenInclude(c => c!.Classes)      // Character included, but NOT c.ProfileImage
        .Include(q => q.DungeonMaster)
        .Include(q => q.OriginalQuest)
        .Include(q => q.FollowUpQuest);
}
```
Applied to this phase's three repositories, the fix for e.g. `CharacterRepository.GetAllCharactersWithDetailsAsync` is simply deleting the `.Include(c => c.ProfileImage)` line — the rest of the query (`.Include(c => c.Owner)`, `.Include(c => c.Classes)`) is untouched.

### Pattern 2: Project a scalar off an un-included 1:1 navigation (existing codebase precedent)
**What:** Root the query at the owner `DbSet`, `.Where()` down to the target row(s), then `.Select()` a boolean/scalar expression that references the navigation — EF Core translates the navigation access inside `.Select()` into a `LEFT JOIN` (or correlated subquery) and only pulls the columns the expression actually needs, never `byte[]` columns that aren't referenced.
**When to use:** Fetching a derived scalar (existence check, length, a specific sub-column) from a related 1:1 table without loading the whole related entity.
**Example:**
```csharp
// Source: existing codebase pattern, CharacterRepository.cs:62-70 (GetCharacterOriginalPictureAsync)
public async Task<byte[]?> GetCharacterOriginalPictureAsync(int id, CancellationToken token = default)
{
    return await DbContext.Characters
        .Where(c => c.Id == id)
        .Select(c => c.ProfileImage != null ? c.ProfileImage.OriginalImageData : null)
        .FirstOrDefaultAsync(token);
}
```
For a **boolean** existence check (this phase's actual need) the equivalent, more efficient shape is:
```csharp
.Select(c => c.ProfileImage != null)
```
This is the idiomatic EF Core "does a related row exist" projection. EF Core 10 translates `c.ProfileImage != null` (a reference-navigation null check inside a `.Select()`) into a `LEFT JOIN ... WHERE [t].[Id] IS NOT NULL`-shaped boolean, or an `EXISTS` subquery depending on provider/query shape — in both cases the generated SQL selects **zero columns from `CharacterImages` other than its key**, never `OriginalImageData`/`CroppedImageData`. [ASSUMED: exact SQL shape (JOIN vs EXISTS) not verified against a live SQL Server trace in this research session — verify via `ToQueryString()` or SQL Profiler at implementation time; the "no byte columns selected" guarantee is architecturally certain regardless of which shape EF chooses, since the byte columns are never referenced in the projection expression]

### Pattern 3: Two-query approach — project the full mapped Domain model AND the boolean, then merge (RECOMMENDED for this phase)
**What:** Because this codebase's repositories return `IList<Character>`/`Character?`/etc. produced via `Mapper.Map<...>(entities)` (a **runtime object-to-object map**, not AutoMapper's `ProjectTo<T>()` LINQ-projection extension), the cleanest way to reconcile "still return full mapped Domain models" with "also carry a computed boolean the mapped Domain model doesn't get from AutoMapper's entity mapping" is:

1. Query entities WITHOUT `.Include(...ProfileImage)` (Pattern 1) → `Mapper.Map<IList<Character>>(entities)` exactly as today. `Character.ProfilePicture` comes back `null` for every row (the existing conditional `ForMember` mapping in `EntityProfile.cs:101-103` already handles a null `ProfileImage` navigation gracefully — no crash).
2. Run a **second, narrow scalar query** rooted at the same filter shape (or the same original `IQueryable` before `.ToListAsync()`, projected via `.Select(c => new { c.Id, HasImage = c.ProfileImage != null })`) to get `{Id, bool}` pairs.
3. Merge the boolean onto the mapped Domain models in a `foreach` loop (O(n) dictionary lookup, or O(n) if row order is preserved and both queries share the same `.Where`/`.OrderBy`).

**Why this shape over a single combined projection:** A single `.Select(c => new { Entity = c, HasProfilePicture = ... })` query requires manually projecting every field of `CharacterEntity` (plus `Owner`, `Classes`) into the anonymous type's `Entity` slot, since EF Core cannot partially materialize a full entity type inside an anonymous projection while also including navigations the normal way — you'd have to give up `.Include(c => c.Owner)`/`.Include(c => c.Classes)` and manually `.Select()` those too, which duplicates significant query-shape logic already working correctly today and is a much larger, riskier diff than this phase's stated scope. The two-query approach keeps the existing `.Include(...).ToListAsync()` → `Mapper.Map<IList<Character>>(entities)` line completely unchanged (zero regression risk to `Owner`/`Classes` mapping) and adds one small, isolated, well-understood boolean-projection query alongside it.

**Example:**
```csharp
// Recommended shape for CharacterRepository.GetAllCharactersWithDetailsAsync
public async Task<IList<Character>> GetAllCharactersWithDetailsAsync(CancellationToken token = default)
{
    var entities = await DbContext.Characters
        .Include(c => c.Owner)
        .Include(c => c.Classes)
        // .Include(c => c.ProfileImage) REMOVED
        .OrderByDescending(c => c.Status == 0)
        .ThenBy(c => c.Owner.Name)
        .ThenBy(c => c.Name)
        .ToListAsync(token);

    var characters = Mapper.Map<IList<Character>>(entities);

    var imageFlags = await DbContext.Characters
        .Select(c => new { c.Id, HasImage = c.ProfileImage != null })
        .ToDictionaryAsync(x => x.Id, x => x.HasImage, token);

    foreach (var character in characters)
    {
        character.HasProfilePicture = imageFlags.GetValueOrDefault(character.Id);
    }

    return characters;
}
```
For the **single-entity** methods (`GetCharacterWithDetailsAsync`, `GetContactWithDetailsAsync`, `GetProfileByUserIdAsync`), the second query collapses to the existing `GetCharacterOriginalPictureAsync`-style single-row shape:
```csharp
public async Task<Character?> GetCharacterWithDetailsAsync(int id, CancellationToken token = default)
{
    var entity = await DbContext.Characters
        .Include(c => c.Owner)
        .Include(c => c.Classes)
        .FirstOrDefaultAsync(c => c.Id == id, token);
    if (entity == null) return null;

    var character = Mapper.Map<Character>(entity);
    character.HasProfilePicture = await DbContext.Characters
        .Where(c => c.Id == id)
        .Select(c => c.ProfileImage != null)
        .FirstOrDefaultAsync(token);
    return character;
}
```
This costs one extra round-trip per call versus a theoretically-perfect single-query projection, but the query is a single indexed-PK boolean lookup (near-zero cost) and the tradeoff buys a much smaller, safer diff. Given the phase's stated goal is reducing byte[] transfer (not round-trip count), this is the correct tradeoff. [ASSUMED: no explicit performance budget was given in CONTEXT.md for round-trip count; if the planner wants a single-query approach instead, Pattern 4 below is the alternative — flag as an open discretion item]

### Pattern 4 (rejected as primary, documented as fallback): Single combined `.Select()` into an anonymous type, manually reconstructing navigations
**What:** `.Select(c => new { c, HasProfilePicture = c.ProfileImage != null })` where the `Include()`-populated navigations (`Owner`, `Classes`) are added via the normal `.Include()` chain BEFORE the `.Select()` — EF Core 10 does support combining `.Include()` with a top-level `.Select()` that also projects the entity itself (this was historically fragile in older EF Core versions but is supported since EF Core 5+, confirmed by the JetBrains EF Core 5 blog post surfaced during research). The result is a single query, single round trip.
**When to use:** If the planner determines the extra round-trip in Pattern 3 is unacceptable, or if `AsSplitQuery()` is needed for the `Classes` collection navigation and combining it with a `.Select()` proves problematic (per the `AsSplitQuery do not work with projections` GitHub issue surfaced during research — `dotnet/efcore#22067`).
**Why not primary:** Higher implementation risk for an equivalent-scope phase; `AsSplitQuery()` + projection has documented EF Core binding issues [CITED: github.com/dotnet/efcore/issues/22067]. None of these repositories currently use `AsSplitQuery()` (only `QuestRepository` does, for its much deeper Quest/PlayerSignup graph), so this specific risk may not apply to Character/Contact/DM — but it's an unnecessary risk to take on for a phase whose entire point is a low-risk mechanical fix. Pattern 3's two-query approach avoids the question entirely.

### Anti-Patterns to Avoid
- **Removing `.Include(x => x.ProfileImage)` from `GetCharacterWithDetailsAsync`/`GetContactWithDetailsAsync` without updating the Edit POST write path:** These two single-entity methods are called by BOTH display actions (Details, Edit GET) AND the Edit POST round-trip (`existingCharacter = await characterService.GetCharacterWithDetailsAsync(id, token)` then later `existingCharacter.ProfilePicture = ...` / `repository.UpdateWithProfileImageAsync(model, model.ProfilePicture, ...)`). See Common Pitfalls Pitfall 1 — this is the single highest-risk item in the entire phase.
- **Replacing `Character.ProfilePicture` / `Contact.ContactImageData` with the boolean at the Domain model layer:** CONTEXT.md's D-05 already scopes the byte[]→bool replacement to the **ViewModel** layer only, and this research confirms why: the Domain model's byte[] property is genuinely read/written on the edit round-trip, not just used for display `!= null` checks like the ViewModel is.
- **Using AutoMapper's `ProjectTo<T>()` LINQ extension to "solve" this in one step:** This project's repositories use `Mapper.Map<T>(entities)` (materialize-then-map) everywhere, never `ProjectTo<T>()` (translate-mapping-into-SQL). Introducing `ProjectTo<T>()` for just these three repositories would be an architectural inconsistency and a much bigger change than this phase's scope calls for — not recommended.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| "Does a related row exist" check | A raw SQL `EXISTS` query via `FromSqlRaw` | `.Select(c => c.ProfileImage != null)` | EF Core's LINQ translator already produces efficient SQL for this; raw SQL adds maintenance burden and bypasses the query filter EF applies for group-scoping (`CharacterEntity`'s global query filter) |

**Key insight:** This phase has no genuinely complex sub-problem — the codebase already solved both halves (omit-include, project-scalar-off-navigation) in existing code. The only new work is composing those two known-good patterns into a boolean and threading it correctly through both AutoMapper boundaries without breaking the write path's implicit reliance on the byte[] Domain model property.

## Runtime State Inventory

Not applicable — this phase does not rename, refactor identifiers, or migrate stored data. It changes query projections and mapping shapes only.

- **Stored data:** None affected — no schema change, no data migration. Confirmed by CONTEXT.md: "No EF Core migration needed — this phase only changes query projections and mapping shapes, not the schema."
- **Live service config:** None — no external service config involved.
- **OS-registered state:** None.
- **Secrets/env vars:** None.
- **Build artifacts:** None — pure C# source change, no renamed assemblies/packages.

## Common Pitfalls

### Pitfall 1: Write-path round-trip breaks when `ProfilePicture`/`ContactImageData` becomes permanently null on `GetCharacterWithDetailsAsync`/`GetContactWithDetailsAsync`
**What goes wrong:** `CharactersController.Edit` (POST, `CharactersController.cs:206-317`) fetches `existingCharacter` via `GetCharacterWithDetailsAsync`, sets `existingCharacter.ProfilePicture = <new bytes>` ONLY when `hasNewOriginalUpload` is true (line 297), then unconditionally calls `characterService.UpdateAsync(existingCharacter, hasNewOriginalUpload, newCroppedImageData, token)` which internally does `repository.UpdateWithProfileImageAsync(model, model.ProfilePicture, croppedImageData, token)`. On a "no new upload" edit (`hasNewOriginalUpload == false`), `model.ProfilePicture` is expected to still carry the previously-stored bytes (round-tripped from the earlier `GetCharacterWithDetailsAsync` fetch), and `ApplyProfileImage` in `CharacterRepository.cs:223-243` treats `originalImageData == null` as "delete the image" (`entity.ProfileImage = null`). If `GetCharacterWithDetailsAsync`'s `.Include(c => c.ProfileImage)` is removed, `existingCharacter.ProfilePicture` will ALWAYS be null on fetch, so every edit that doesn't touch the photo will silently wipe the stored image entity. The identical hazard exists for `ContactsController.Edit` / `ContactService.UpdateAsync` / `ContactRepository.ApplyProfileImage`.
**Why it happens:** The write path was built (Phase 45-02) assuming the read that feeds it (`GetCharacterWithDetailsAsync`) still eagerly loads the image. That assumption becomes false the moment this phase's `.Include()` removal lands, unless the write path is updated in the same change.
**How to avoid:** This phase's planner MUST include a task that changes `CharacterService.UpdateAsync`'s "no new upload" branch. The current code already has the fix half-built: look at `CharacterService.UpdateAsync` (`CharacterService.cs:79-106`) — on the `hasNewOriginalUpload == false` branch it already calls `repository.GetCharacterCroppedPictureAsync(model.Id, token)` to re-fetch the CROPPED image fresh (not trusting a round-tripped value) specifically because Phase 45-02 already learned this lesson for the cropped image. The **original** image (`model.ProfilePicture`, passed as `originalImageData` to `UpdateWithProfileImageAsync`) is the one still trusting the round-tripped Domain model value. The fix: `CharacterService.UpdateAsync` must, on the `hasNewOriginalUpload == false` branch, fetch the current original bytes via `repository.GetCharacterOriginalPictureAsync(model.Id, token)` (which already exists, at `CharacterRepository.cs:62-70`) instead of trusting `model.ProfilePicture`. This is a small, well-precedented change — it exactly mirrors what Phase 45-02 already did for the cropped-image branch, just extended to the original-image branch too. Apply the identical fix to `ContactService.UpdateAsync` using `GetContactOriginalImageAsync`.
**Warning signs:** If the plan does NOT include a task modifying `CharacterService.UpdateAsync` / `ContactService.UpdateAsync`'s "no new upload" branch, this is a gap — the phase's own stated scope ("Services are no longer in scope") in CONTEXT.md's `<code_context>` section is **only true for the write-path SIGNAL mechanism** (Phase 45-02's `hasNewOriginalUpload` overloads), not for the specific byte[]-sourcing inside those methods, which this phase's Include removal invalidates. **This is a correction to CONTEXT.md's scope statement** — flag prominently for the planner. A UAT/manual test case "edit a character's name only, without touching the photo — confirm the photo is still there afterward" is the cheapest way to catch a regression here.

### Pitfall 2: `EditDMProfileViewModel.ProfilePicture` and `DungeonMasterController.EditProfile` GET action
**What goes wrong:** `DungeonMasterController.cs:77`: `ProfilePicture = profile?.ProfilePicture` populates `EditDMProfileViewModel.ProfilePicture` directly from `DungeonMasterProfile.ProfilePicture` for the Edit form's "show current image" `@if (Model.ProfilePicture?.Length > 0)` check (`EditProfile.cshtml:26`). Once `GetProfileByUserIdAsync`'s `.Include(p => p.ProfileImage)` is removed, `profile.ProfilePicture` becomes always null, so the "current image" preview on the Edit Profile form silently stops appearing even when a profile picture exists — a display bug, not a data-loss bug (DM's write path, `UpsertProfileAsync`, is explicit-signal-based per D-04 and does NOT round-trip through this value, so no data loss occurs here — only a UI regression).
**Why it happens:** Same root cause as Pitfall 1 (a GET action reusing a read method whose Include is being removed) but on the DM entity, where the consequence is cosmetic rather than destructive because D-04's `UpsertProfileAsync` never reads back `model.ProfilePicture`.
**How to avoid:** Per D-05, `EditDMProfileViewModel.ProfilePicture` (byte[]?) becomes `HasProfilePicture` (bool), sourced from `DungeonMasterProfile.HasProfilePicture` (the new projected boolean) instead of `profile?.ProfilePicture`. Update `DungeonMasterController.cs:77` from `ProfilePicture = profile?.ProfilePicture` to `HasProfilePicture = profile?.HasProfilePicture ?? false`, and update `DungeonMasterController.cs:43`'s existing `HasProfilePicture = profile?.ProfilePicture?.Length > 0` similarly to `HasProfilePicture = profile?.HasProfilePicture ?? false` (this second one is the `Profile` GET action populating `DMProfileViewModel`, already using a bool target — just needs its source expression updated since `profile.ProfilePicture` will be null post-fix). Update `EditProfile.cshtml:26`/`EditProfile.Mobile.cshtml:29` from `@if (Model.ProfilePicture?.Length > 0)` to `@if (Model.HasProfilePicture)`.
**Warning signs:** Edit Profile page for a DM who has an existing photo shows no "current image" thumbnail after this phase ships.

### Pitfall 3: `AutoMapper` conditional `ForMember` expressions silently produce `null` instead of erroring — masks the underlying Include removal if not tested
**What goes wrong:** `EntityProfile.cs:101-103`'s `CreateMap<CharacterEntity, Character>().ForMember(dest => dest.ProfilePicture, opt => opt.MapFrom(src => src.ProfileImage != null ? src.ProfileImage.OriginalImageData : null))` already null-guards the navigation access — meaning if `ProfileImage` isn't included, this expression happily evaluates to `null` with zero exception, zero warning. This is exactly the desired end-state (no image bytes materialize), but it also means a mistake elsewhere (e.g. forgetting to populate `HasProfilePicture` on some code path) fails silently as "the has-image bool is just always false" rather than a loud error.
**Why it happens:** AutoMapper conditional expressions and EF Core's null-propagating navigation access are both designed to degrade gracefully, which is good for production behavior but bad for catching a missed code path during implementation.
**How to avoid:** Add an explicit test (unit or UAT) per repository method asserting `HasProfilePicture == true` for a seeded entity that has an image and `false` for one that doesn't — do not rely on manual QA alone, since a missed `foreach` merge step would silently return `false` for everyone and look identical to "feature works, nobody has uploaded a photo yet" during casual testing.
**Warning signs:** All characters/contacts/DMs show no profile picture on list pages even for users known to have uploaded one.

### Pitfall 4: `CharacterViewModel`/`ContactViewModel` are also used as the Create/Edit form POST binding target, not just a display DTO
**What goes wrong:** `CharacterViewModel.ProfilePicture` is written to by the controller (`viewModel.ProfilePicture = memoryStream.ToArray()` at `CharactersController.cs:155`, `existingCharacter.ProfilePicture = memoryStream.ToArray()` at line 297 — note: line 297 sets it on the **Domain model** `existingCharacter`, not the ViewModel, so that one is unaffected by the ViewModel property rename) then mapped via `mapper.Map<Character>(viewModel)` on Create (`CharactersController.cs:165`). If `CharacterViewModel.ProfilePicture` is renamed to `HasProfilePicture` (bool) per D-05, the Create POST action's `viewModel.ProfilePicture = memoryStream.ToArray()` (line 155) and the subsequent `mapper.Map<Character>(viewModel)` (line 165, which currently relies on `CreateMap<CharacterViewModel, Character>()` picking up `ProfilePicture` by convention) BOTH need to change: the uploaded bytes must be captured in a **local variable**, not stashed on the ViewModel, then assigned directly onto the mapped `Character.ProfilePicture` after the `mapper.Map` call — exactly as CONTEXT.md's D-05 nuance paragraph already anticipates ("the Create/Edit POST code paths that currently stash uploaded bytes onto the ViewModel property should instead use a local variable and set the mapped domain object's byte[] property directly post-map").
**Why it happens:** The ViewModel currently double-duties as both a display DTO (`!= null` check) and a write-path staging area (temporarily holding uploaded bytes before the `mapper.Map` call). Converting the display half to a bool without also fixing the staging half breaks compilation (the type no longer matches) and, if worked around carelessly (e.g. keeping a same-named `byte[]` field under a different name just for staging), reintroduces exactly the byte[]-in-a-list-adjacent-object smell this phase is trying to eliminate.
**How to avoid:** In `CharactersController.Create` POST: replace `viewModel.ProfilePicture = memoryStream.ToArray();` with a local `byte[]? uploadedOriginalImageData = memoryStream.ToArray();`, then after `var character = mapper.Map<Character>(viewModel);` add `character.ProfilePicture = uploadedOriginalImageData;`. Apply the identical pattern to `ContactsController.Create` POST (`viewModel.ContactImage = memoryStream.ToArray()` at line 127 → local variable, set on `contact.ContactImageData` post-map).
**Warning signs:** Compile errors are the primary safety net here (assigning `byte[]` to a renamed `bool` property fails to compile) — but silent-wrong-behavior risk exists if a local variable is introduced with the same name pattern as an existing field and the post-map assignment is forgotten, resulting in newly-created characters/contacts having no photo even when one was uploaded.

## Code Examples

### Repository: single-entity method with boolean projection (CharacterRepository.GetCharacterWithDetailsAsync)
```csharp
// Recommended replacement for CharacterRepository.cs:41-49
/// <inheritdoc/>
public async Task<Character?> GetCharacterWithDetailsAsync(int id, CancellationToken token = default)
{
    var entity = await DbContext.Characters
        .Include(c => c.Owner)
        .Include(c => c.Classes)
        .FirstOrDefaultAsync(c => c.Id == id, token);
    if (entity == null) return null;

    var character = Mapper.Map<Character>(entity);
    character.HasProfilePicture = await DbContext.Characters
        .Where(c => c.Id == id)
        .Select(c => c.ProfileImage != null)
        .FirstOrDefaultAsync(token);
    return character;
}
```

### Repository: list method with boolean projection merged via dictionary (CharacterRepository.GetAllCharactersWithDetailsAsync)
```csharp
// Recommended replacement for CharacterRepository.cs:12-23
/// <inheritdoc/>
public async Task<IList<Character>> GetAllCharactersWithDetailsAsync(CancellationToken token = default)
{
    var entities = await DbContext.Characters
        .Include(c => c.Owner)
        .Include(c => c.Classes)
        .OrderByDescending(c => c.Status == 0) // 0 = Active
        .ThenBy(c => c.Owner.Name)
        .ThenBy(c => c.Name)
        .ToListAsync(token);

    var characters = Mapper.Map<IList<Character>>(entities);

    var imageFlags = await DbContext.Characters
        .Select(c => new { c.Id, HasImage = c.ProfileImage != null })
        .ToDictionaryAsync(x => x.Id, x => x.HasImage, token);

    foreach (var character in characters)
    {
        character.HasProfilePicture = imageFlags.GetValueOrDefault(character.Id);
    }

    return characters;
}
```

### Service write-path fix (CharacterService.UpdateAsync — required companion change, see Pitfall 1)
```csharp
// Modified CharacterService.cs:79-106 — the else branch now re-fetches the ORIGINAL bytes fresh,
// exactly mirroring how the crop branch already re-fetches fresh instead of trusting a
// round-tripped Domain model value.
public async Task UpdateAsync(Character model, bool hasNewOriginalUpload, byte[]? newCroppedImageData, CancellationToken token = default)
{
    byte[]? originalImageData;
    byte[]? croppedImageData;

    if (hasNewOriginalUpload)
    {
        originalImageData = model.ProfilePicture; // freshly set by the controller this request
        croppedImageData = newCroppedImageData;    // null clears any stale crop of the superseded photo
    }
    else
    {
        // No new file this request — model.ProfilePicture is no longer trustworthy (the
        // read path that produced `model` never loads it). Re-fetch both original and
        // cropped bytes fresh so an unrelated-field edit doesn't wipe the stored image.
        originalImageData = await repository.GetCharacterOriginalPictureAsync(model.Id, token);
        croppedImageData = newCroppedImageData ?? await repository.GetCharacterCroppedPictureAsync(model.Id, token);
    }

    await repository.UpdateWithProfileImageAsync(model, originalImageData, croppedImageData, token);
}
```
[ASSUMED: this exact restructuring is this research's recommendation, not an existing shipped pattern — verify against the actual current `CharacterService.UpdateAsync` body at plan/implementation time, since Phase 45-02/46 may have shifted line numbers or logic slightly since this research session. The core idea (re-fetch `GetCharacterOriginalPictureAsync` instead of trusting `model.ProfilePicture` on the no-upload branch) is the load-bearing part.]

### AutoMapper Entity↔Domain (additive HasProfilePicture, EntityProfile.cs)
```csharp
// Addition to EntityProfile.cs's existing CharacterEntity → Character map (around line 98-103)
CreateMap<CharacterEntity, Character>()
    .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (CharacterStatus)src.Status))
    .ForMember(dest => dest.Role, opt => opt.MapFrom(src => (CharacterRole)src.Role))
    .ForMember(dest => dest.ProfilePicture, opt => opt.MapFrom(src => src.ProfileImage != null
        ? src.ProfileImage.OriginalImageData
        : null))
    .ForMember(dest => dest.HasProfilePicture, opt => opt.Ignore()); // set explicitly post-map by the repository (see Pattern 3) — AutoMapper would otherwise try to map it from a nonexistent CharacterEntity.HasProfilePicture and fail silently to false every time
```
Using `.Ignore()` here is deliberate: since the boolean is computed by a separate query (Pattern 3), not derivable from any single `CharacterEntity` field, telling AutoMapper to leave it alone (rather than letting convention-based mapping attempt and fail to match) makes the "this is set elsewhere" intent explicit in the mapping config itself.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `.Include(c => c.ProfileImage)` on every Character/Contact/DM list & detail query | Omit the Include; project `HasX` boolean via a secondary scalar query | This phase | Removes byte[] transfer from list/detail SQL result sets entirely; matches the `ProjectWithoutCharacterImages` pattern already established for Quest/QuestLog pages in an earlier, unrelated change |

**Deprecated/outdated:** None — this is a first-time application of an already-established in-repo pattern to three repositories that hadn't adopted it yet.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | EF Core 10 translates `c.ProfileImage != null` inside `.Select()` into a JOIN/EXISTS that selects zero byte columns (exact SQL shape not traced against a live SQL Server instance this session) | Architecture Patterns, Pattern 2 | Low — even if the exact SQL shape differs from expectation, the architectural guarantee (byte columns never referenced in the projection expression tree, so EF cannot select them) holds regardless; verify via `.ToQueryString()` or SQL Profiler during implementation as a cheap sanity check |
| A2 | The recommended `CharacterService.UpdateAsync`/`ContactService.UpdateAsync` write-path fix (re-fetch original bytes fresh via `GetCharacterOriginalPictureAsync`/`GetContactOriginalImageAsync` instead of trusting `model.ProfilePicture`/`model.ContactImageData`) is not yet implemented anywhere in the codebase — it's this research's proposed fix, not a verified-shipped pattern | Common Pitfalls Pitfall 1, Code Examples | High if the planner skips this fix entirely — every "edit without changing the photo" would silently wipe the stored image. This is the single most important actionable finding in this document; the planner MUST include a task for it despite CONTEXT.md stating services are "no longer in scope" |
| A3 | No explicit performance/round-trip-count budget was stated by the user for this phase; Pattern 3's two-query-per-list-call approach is assumed acceptable over Pattern 4's single-query approach | Architecture Patterns, Pattern 3 vs Pattern 4 | Low — an extra indexed-PK boolean lookup per list call is cheap; if the user cares about round-trip count specifically (as opposed to byte[] transfer volume, which is the stated goal), Pattern 4 is documented as the fallback |

**If this table is empty:** N/A — see entries above.

## Open Questions

1. **Exact current line numbers/body of `CharacterService.UpdateAsync`/`ContactService.UpdateAsync` at plan/implementation time**
   - What we know: Read directly during this research session (see Code Examples) — matches CONTEXT.md's D-01 description of the `hasNewOriginalUpload` three-overload pattern.
   - What's unclear: Whether any phase between this research session and plan execution touches these files again, shifting lines.
   - Recommendation: Planner should re-read `CharacterService.cs`/`ContactService.cs` fresh at plan time (cheap, low-risk) rather than trusting this document's line numbers verbatim — same caution CONTEXT.md already applies to view files.

2. **Whether `DungeonMasterProfileService.UpsertProfileAsync` needs any equivalent fix to Pitfall 1**
   - What we know: D-04 (CONTEXT.md) confirms `UpsertProfileAsync` is explicit-signal-based (`removeImage: true` or new bytes passed directly by the controller) and never round-trips through a fetched `DungeonMasterProfile.ProfilePicture` value for its write decision.
   - What's unclear: Nothing substantive — this was directly verified by reading `DungeonMasterController.cs:83-148`'s `EditProfile` POST action, which builds `imageBytes`/`newCroppedImageData` from freshly-uploaded `IFormFile`s only, never from `viewModel.ProfilePicture`/`profile.ProfilePicture`.
   - Recommendation: No write-path fix needed for DM — only the GET-action display fix (Pitfall 2).

## Environment Availability

Skipped — this phase has no external tool/service dependencies beyond the project's existing .NET/EF Core/SQL Server stack, already confirmed present and in active use throughout the codebase.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | [ASSUMED: not directly inspected this session — check `QuestBoard.Tests` or equivalent project for xUnit/NUnit/MSTest at plan time] |
| Config file | Not verified this session |
| Quick run command | `dotnet test` (standard .NET convention) [ASSUMED] |
| Full suite command | `dotnet test` [ASSUMED] |

### Phase Requirements → Test Map
No formal REQUIREMENTS.md IDs map to this phase (ad-hoc backlog phase, confirmed by phase description). Suggested behavioral checks instead:

| Behavior | Test Type | Notes |
|----------|-----------|-------|
| List/detail queries for Character/Contact/DM no longer select image byte columns | Integration/manual | Verify via `ToQueryString()` assertion or SQL Profiler trace showing no `OriginalImageData`/`CroppedImageData` column in the generated SQL for the six modified methods |
| `HasProfilePicture`/`HasContactImage` is `true` for entities with an image, `false` for entities without | Unit or integration | Seed one entity with an image, one without, assert both booleans |
| Editing a Character/Contact without touching the photo preserves the existing photo (Pitfall 1 regression test) | Manual UAT (critical) | Edit a character's Description only; reload Details page; confirm `GetProfilePicture`/`GetCroppedPicture` endpoint still returns the original bytes |
| Editing a Character/Contact WITH a new photo upload still replaces the photo correctly | Manual UAT | Upload a new photo on Edit; confirm old bytes are gone, new bytes are served |
| DM Edit Profile page still shows "current image" thumbnail for a DM with an existing photo | Manual UAT | Regression check for Pitfall 2 |

### Sampling Rate
- **Per task commit:** Manual verification of the specific repository method changed (query shape + no image bytes in generated SQL)
- **Per wave merge:** Full manual UAT pass through Characters/Contacts/DM Create, Edit (with and without photo change), Details, Index pages
- **Phase gate:** The Pitfall 1 "edit without touching photo" UAT case is non-negotiable — it is the one path with real data-loss risk in this phase

### Wave 0 Gaps
- No automated test infrastructure was found/verified for this project during this research session — if `dotnet test` reveals no test project, the phase should rely on manual UAT (documented above) as its verification method, consistent with how other recent phases (45, 46) appear to have been verified per their SUMMARY.md files.

## Security Domain

Not applicable in any meaningful sense — this phase changes query projections for already-authorized, already-group-scoped reads (the existing group query filters on `CharacterEntity`/`ContactEntity` continue to apply regardless of which columns are selected). No new attack surface is introduced; if anything, less sensitive binary data traverses the API/Backend↔Frontend Server boundary for list views, marginally reducing exposure.

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Unaffected — authorization handlers and policies unchanged |
| V4 Access Control | No | Group-scoping query filters (`CharacterEntity`, `ContactEntity`) are unaffected by removing `.Include(...ProfileImage)` — filters apply at the root query, not per-navigation |
| V5 Input Validation | No | No new input surface — this phase changes reads, not the existing upload validation (`ImageValidationService`) |

## Sources

### Primary (HIGH confidence)
- Direct codebase reads: `CharacterRepository.cs`, `ContactRepository.cs`, `DungeonMasterProfileRepository.cs`, `QuestRepository.cs:335-354`, `CharacterService.cs`, `ContactService.cs`, `BaseRepository.cs`, `EntityProfile.cs`, `ViewModelProfile.cs`, `CharacterViewModel.cs`, `ContactViewModel.cs`, `DMProfileViewModel.cs`, `EditDMProfileViewModel.cs`, `CharactersController.cs`, `ContactsController.cs`, `DungeonMasterController.cs`, `CharacterEntity.cs`, `Character.cs`, `Contact.cs`, `DungeonMasterProfile.cs`, all Character/Contact/DM Razor views — read directly during this research session, 2026-07-07
- `QuestBoard.Repository.csproj` — EF Core version confirmed via direct file read (10.0.9)

### Secondary (MEDIUM confidence)
- [Eager Loading of Related Data - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/querying/related-data/eager) — confirms `.Include()` semantics, does not directly document the "entity + computed scalar" combined-projection pattern (community pattern, not formally documented)
- [AsSplitQuery do not work with projections · Issue #22067 · dotnet/efcore](https://github.com/dotnet/efcore/issues/22067) — confirms the risk cited in Pattern 4's rejection rationale

### Tertiary (LOW confidence)
- General WebSearch results on "EF Core project navigation exists boolean" — used to corroborate that projecting `nav != null` is idiomatic, not the primary source for any specific claim in this document (the codebase's own existing `GetCharacterOriginalPictureAsync` method is the actual verified precedent)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies, all APIs already in active use in this codebase
- Architecture: HIGH — both halves of the recommended pattern (omit-include, project-scalar) are directly copied from working code already in this repository, not externally sourced
- Pitfalls: HIGH — Pitfall 1 (the write-path hazard) was discovered by directly reading the actual controller/service code end-to-end, not inferred; this is the single most valuable finding in this research and directly corrects an assumption in CONTEXT.md's `<code_context>` section ("Services are no longer in scope")

**Research date:** 2026-07-07
**Valid until:** Effectively indefinite for the architectural pattern (stable EF Core/AutoMapper APIs); line-number references should be re-verified at plan/implementation time per standard practice, especially given this codebase's rapid recent phase velocity (Phase 45/46 already shifted several line numbers CONTEXT.md had to correct)
