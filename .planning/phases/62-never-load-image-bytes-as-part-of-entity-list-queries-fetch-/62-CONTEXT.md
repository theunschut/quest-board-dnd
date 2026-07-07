# Phase 62: Stop eagerly loading image bytes in list/entity queries - Context

**Gathered:** 2026-07-07
**Status:** Ready for planning — **blocked on Phase 45 (45-02, 45-03) and Phase 46 completing first**

<domain>
## Phase Boundary

No repository query for Characters, Contacts, or DungeonMaster profiles pulls the associated image `byte[]` into memory as part of returning list or single-entity data for display. Every page that renders a portrait/photo fetches that image only via its existing dedicated per-entity endpoint (`GetProfilePicture` / `GetContactImage` / `GetDMProfilePicture`), matching the pattern `QuestRepository.ProjectWithoutCharacterImages` already uses for Quest and QuestLog pages.

**Stated broader principle (user's own words, confirmed during discussion):** "The essence of what I'm trying to do is that all images are only retrieved on demand, and not by querying the related entities like a Quest for instance. This drops the load on the DB and shows pages faster. Only when the page is done loading, retrieve the images needed for that specific page." This is the actual goal — the ROADMAP.md method list (below) is this session's best enumeration of it, not a deliberately narrower boundary. Where a method with the identical pattern was found but not named, it is in scope too (see D-02).

**Read-side methods in scope** (verified against current code — exact method names WILL have changed by the time this phase executes, see D-06):
- `CharacterRepository.GetAllCharactersWithDetailsAsync`, `GetCharactersByOwnerIdAsync`, `GetCharacterWithDetailsAsync`
- `ContactRepository.GetAllContactsWithDetailsAsync` **and** `GetContactWithDetailsAsync` (D-02)
- `DungeonMasterProfileRepository.GetProfileByUserIdAsync`

**Explicitly out of scope:**
- `CharacterRepository.GetMainCharacterForUserAsync` — has the identical `.Include(c => c.ProfileImage)` pattern but is confirmed dead code (zero production callers anywhere in `QuestBoard.Service`, `QuestBoard.Domain`, or tests — only referenced in an old v1.0 phase plan doc). Not reachable from any list/display page, so it isn't part of the problem this phase fixes. Leave it alone; removing dead code is a separate concern.
- Per-entity write-path fetches that legitimately need the tracked image (e.g. `CharacterRepository.UpdateAsync`'s own internal `.Include(c => c.ProfileImage)` used to restore the tracked instance, `UpdateProfileImageAsync`'s own fetch) — these are single-row write operations, not list/display reads, and are unaffected by this phase.
- Quest/QuestLog pages — already correctly handled via `ProjectWithoutCharacterImages`, used as the reference pattern, not touched by this phase.

</domain>

<decisions>
## Implementation Decisions

### Phase sequencing — CRITICAL, resolves the biggest risk found this session
- **D-01:** Phase 62 depends on **Phase 46** (which itself depends on Phase 45) — corrected from the roadmap's original "Depends on: Phase 61" (ROADMAP.md updated this session). Reason: Phase 45's remaining plans (45-02, 45-03 — planned but **not yet executed** as of this session) already touch the exact same files/methods this phase needs to change:
  - 45-02 adds an explicit `hasNewOriginalUpload` boolean signal to `CharacterService.UpdateAsync` / `ContactService.UpdateAsync` (new overload), replacing today's implicit reliance on the round-tripped model's `ProfilePicture`/`ContactImageData` byte[] to "preserve the existing image when no new photo is uploaded." This is the **exact same write-path safety net** this phase would otherwise have to build itself (see D-03/D-04 below for why it's needed) — 45-02/45-03's "Pitfall 4 (fetch-and-preserve)" and "Pitfall 5 (clear-stale-crop)" are the same hazard class.
  - 45-02/45-03 rename the per-entity read methods this phase's goal text references (`GetCharacterProfilePictureAsync` → `GetCharacterOriginalPictureAsync`/`GetCharacterCroppedPictureAsync` with a `CroppedImageData ?? OriginalImageData` query-level fallback), and repoint the controller serving actions (`GetProfilePicture`/`GetContactImage`/`GetDMProfilePicture`) to the renamed methods.
  - Running Phase 62 before 45/46 finish means duplicating the write-safety fix now, then having Phase 45 rework the same methods again later, and building against method names that will be renamed out from under it. Running after means Phase 62 inherits the fix and the final method names for free, and only has to do the read-side (list/detail query) change.
- **Consequence for planning:** if 45-02/45-03/46 have landed by the time this phase is researched/planned, `CharacterService.UpdateAsync`/`ContactService.UpdateAsync` will likely already take a `hasNewOriginalUpload`-style parameter and the image-safety concern (D-03/D-04) is likely already resolved — the planner should verify this against the then-current code rather than re-implementing it.

### Why the write-path matters here at all (background for D-01, so the risk isn't rediscovered from scratch)
- **D-03 (background, not a new task if D-01's sequencing holds):** Naively removing `.Include(x => x.ProfileImage)` from the read-side methods above, without also touching the write path, reproduces a real bug: `CharacterService.UpdateAsync`/`ContactService.UpdateAsync` today unconditionally call `repository.UpdateProfileImageAsync(model.Id, model.ProfilePicture, token)` — passing `null` wipes the stored image (`if (imageData == null) entity.ProfileImage = null;`). Today this doesn't break anything only because `GetCharacterWithDetailsAsync`/`GetContactWithDetailsAsync` eagerly loads the *old* bytes, which get silently round-tripped back unchanged when no new file is uploaded. Reproducible today via: edit a character's Name (no new photo) → `CharactersController.Edit` POST → `characterService.UpdateAsync(existingCharacter)` → would wipe the photo once the eager-load is gone. Also reachable via `CharactersController.ToggleRetirement` and `ContactsController.ToggleReveal` (both fetch-then-`UpdateAsync`, neither touches the photo intentionally).
- **D-04:** `DungeonMasterProfileService.UpsertProfileAsync` is **already correct** — it only calls `UpsertProfileImageAsync` when the caller explicitly passes new bytes or a `removeImage` flag, never relying on a round-tripped read value. This is the pattern Phase 45-02 is generalizing to Character/Contact. Confirms DM profile's `GetProfileByUserIdAsync` eager-load is pure waste with zero write-path risk — safe to fix independent of the Character/Contact sequencing concern, though bundling with 45/46 (D-01) is still cleanest.

### Contact scope — apply the same treatment to the single-entity fetch too
- **D-02:** `ContactRepository.GetContactWithDetailsAsync` (Contact's single-entity fetch — powers Details/Edit/ToggleReveal) gets the identical eager-load removal as `GetAllContactsWithDetailsAsync`, even though ROADMAP.md's text only explicitly named the list method. Confirmed via user's stated broader principle (see `<domain>`) — this isn't scope creep, it's the same bug the roadmap's own goal statement already describes ("...as part of returning list **or single-entity** data for display"). Mirrors how Character's own single-entity method (`GetCharacterWithDetailsAsync`) was already explicitly named.

### ViewModel shape — replace the byte[] display properties with the boolean the roadmap calls for
- **D-05:** `CharacterViewModel.ProfilePicture` (byte[]?) and `ContactViewModel.ContactImage` (byte[]?) — used **only** for `!= null` checks in every view that references them (Index/Details/Create/Edit, desktop and mobile, confirmed via exhaustive grep — no view ever renders the actual byte content; the real pixels always come from the dedicated `GetProfilePicture`/`GetContactImage` endpoint URL) — become `HasProfilePicture: bool` / `HasContactImage: bool` on the display path. Matches the roadmap's own wording and the pattern already established by `DMProfileViewModel.HasProfilePicture` (DM Profile's *display* ViewModel already did this; only its separate *edit* ViewModel, `EditDMProfileViewModel`, still carries the byte[]-for-preview pattern).
  - **Important nuance the planner must account for:** the *Domain model* layer (`Character.ProfilePicture`, `Contact.ContactImageData`) is NOT being replaced — it genuinely carries real upload bytes on the write path (`CharactersController.Create`/`Edit` set `viewModel.ProfilePicture`/`existingCharacter.ProfilePicture` from the uploaded `IFormFile`, which flows into `Character.ProfilePicture` and then `CharacterService.UpdateAsync`/`AddAsync`). Only the **ViewModel's read/display use** of these properties is being replaced with a bool; the Create/Edit POST code paths that currently stash uploaded bytes onto the ViewModel property should instead use a local variable and set the mapped domain object's byte[] property directly post-map (small, mechanical change — same shape as Phase 45-02/45-03 will likely already need for the `hasNewOriginalUpload` wiring).
  - `EditDMProfileViewModel.ProfilePicture` (byte[]?) — same treatment: only ever used for `Model.ProfilePicture?.Length > 0` in `EditProfile.cshtml`/`.Mobile.cshtml`, never rendered directly. Replace with `HasProfilePicture: bool` there too, for consistency with `DMProfileViewModel`.

### Claude's Discretion
- Exact mechanism for projecting the boolean at the query level (e.g. `.Select(c => new { Entity = c, HasProfilePicture = c.ProfileImage != null })` translated to a SQL `EXISTS`/join, vs. some other EF Core projection shape) — implementation detail, planner/researcher's call. The one hard constraint: the generated SQL must not select the image byte columns.
- Whether the boolean is computed in the repository (returned as part of a projection), the service layer, or the controller (mirroring how DM's current `HasProfilePicture = profile?.ProfilePicture?.Length > 0` is computed in the controller today) — planner's call, but should land consistently across all three entities rather than three different layers for three near-identical cases.
- Whether `Character`/`Contact`/`DungeonMasterProfile` Domain models gain a `HasProfilePicture`/`HasContactImage` property of their own, or whether that's purely a ViewModel-layer concern computed from whatever the repository returns — planner's call; no existing precedent forces either way (DM's existing bool lives only at the ViewModel/controller layer today).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 45/46 — the dependency this phase is sequenced behind (D-01)
- `.planning/phases/45-dual-image-storage-backend/45-CONTEXT.md` — full context for the dual-image storage work; read for the `OriginalImageData`/`CroppedImageData` column shape and its rationale
- `.planning/phases/45-dual-image-storage-backend/45-02-PLAN.md` — the write-path safety fix (`hasNewOriginalUpload` signal, Pitfall 4/5) this phase's D-03/D-04 risk maps directly onto; **re-check this plan's actual execution state (SUMMARY.md existence) before planning 62** — if executed, its `UpdateAsync(model, hasNewOriginalUpload, token)` overload should be reused as-is rather than re-derived
- `.planning/phases/45-dual-image-storage-backend/45-03-PLAN.md` — renames the per-entity image-read methods (`GetCharacterOriginalPictureAsync`/`GetCharacterCroppedPictureAsync` etc.) that this phase's goal text references by their pre-45 names; **method names in this CONTEXT.md's `<code_context>` below are pre-45-02/45-03 and will be stale — re-verify against current code at research/plan time**
- `.planning/phases/45-dual-image-storage-backend/45-PATTERNS.md` — established patterns for the dual-image work, useful precedent for how this phase's own read-side changes should be structured
- `.planning/phases/46-*/` (does not exist yet as of this session — Phase 46 has not started) — once it exists, its CONTEXT/PLAN will show the final ViewModel/view shape for cropped-vs-original display that this phase's `HasProfilePicture`/`HasContactImage` work must not conflict with
- `.planning/ROADMAP.md` — Phase 45 (line ~189) and Phase 46 (line ~214) entries; Phase 62 entry (line ~538) updated this session with the corrected dependency and rationale

### Reference pattern already solved (Quest/QuestLog)
- `QuestBoard.Repository/QuestRepository.cs:335-354` — `ProjectWithoutCharacterImages` — the existing precedent this phase's goal explicitly names: omits `.ThenInclude(c => c!.ProfileImage)` entirely when loading `PlayerSignup.Character` for Quest/QuestLog pages, since those pages never show character portraits at all (not even a has-image boolean). Character/Contact/DM's own list pages DO need a has-image boolean (unlike Quest), so the exact mechanism differs, but the "don't eager-load the byte[] at all" principle is the same.

### Repositories to change (current state — see staleness warning above)
- `QuestBoard.Repository/CharacterRepository.cs` — `GetAllCharactersWithDetailsAsync` (11-23), `GetCharactersByOwnerIdAsync` (24-38), `GetCharacterWithDetailsAsync` (40-49) — remove `.Include(c => c.ProfileImage)`, project `HasProfilePicture` instead
- `QuestBoard.Repository/ContactRepository.cs` — `GetAllContactsWithDetailsAsync` (12-30), `GetContactWithDetailsAsync` (33-45, per D-02) — remove `.Include(c => c.ProfileImage)`, project `HasContactImage` instead
- `QuestBoard.Repository/DungeonMasterProfileRepository.cs` — `GetProfileByUserIdAsync` (31-37) — remove `.Include(p => p.ProfileImage)`, project `HasProfilePicture` instead

### Services (write-path risk area, D-03/D-04 — verify Phase 45-02 state first)
- `QuestBoard.Domain/Services/CharacterService.cs:67-71` — `UpdateAsync` unconditionally calls `repository.UpdateProfileImageAsync(model.Id, model.ProfilePicture, token)` — the exact hazard D-03 describes
- `QuestBoard.Domain/Services/ContactService.cs:22-26` — same pattern, `model.ContactImageData`
- `QuestBoard.Domain/Services/DungeonMasterProfileService.cs:17-38` — `UpsertProfileAsync` — the already-correct reference pattern (D-04)

### ViewModels / AutoMapper (D-05)
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs:35` — `ProfilePicture` byte[]? → `HasProfilePicture` bool
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs:22` — `ContactImage` byte[]? → `HasContactImage` bool
- `QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs:13` — `ProfilePicture` byte[]? → `HasProfilePicture` bool (for consistency with `DMProfileViewModel.HasProfilePicture`, already correct)
- `QuestBoard.Service/ViewModels/DungeonMasterViewModels/DMProfileViewModel.cs:8` — `HasProfilePicture` bool — the existing correct precedent to mirror for the other two view models
- `QuestBoard.Repository/Automapper/EntityProfile.cs` (Entity ↔ Domain, note: NOT `QuestBoard.Domain/Automapper/` as CLAUDE.md states — that reference is stale) — `CharacterEntity ↔ Character` (~87-104), `ContactEntity ↔ Contact` (~112-126, includes the `OriginalImageData`/`ContactImageData` mapping), `DungeonMasterProfileEntity ↔ DungeonMasterProfile` (~134-139)
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` — `Character ↔ CharacterViewModel` (~62-73), `Contact ↔ ContactViewModel` (~77-89, includes the `ContactImageData ↔ ContactImage` explicit `ForMember` mapping)

### Views affected by the D-05 ViewModel property rename (all currently do `!= null` checks only)
- `QuestBoard.Service/Views/Characters/Index.cshtml:36,111`, `Index.Mobile.cshtml:22,71`, `Details.cshtml:16`, `Details.Mobile.cshtml:18`, `Create.cshtml:30`, `Edit.cshtml:27`, `Edit.Mobile.cshtml:24`
- `QuestBoard.Service/Views/Contacts/Index.cshtml:48`, `Index.Mobile.cshtml:40`, `Details.cshtml:13`, `Details.Mobile.cshtml:15`, `Edit.cshtml:24`, `Edit.Mobile.cshtml:22`
- `QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml:26`, `EditProfile.Mobile.cshtml:29` (Profile.cshtml/.Mobile.cshtml already use the bool)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `DMProfileViewModel.HasProfilePicture` + `DungeonMasterController.Profile`'s `HasProfilePicture = profile?.ProfilePicture?.Length > 0` — the exact target end-state pattern already proven for one of the three entities; mirror this shape for Character/Contact and for `EditDMProfileViewModel`.
- `QuestRepository.ProjectWithoutCharacterImages` — proof this codebase already has precedent for "omit the image include entirely from a query used for display."

### Established Patterns
- Explicit boolean-signal parameters over implicit round-tripped state: `UpdateQuestPropertiesWithNotificationsAsync(..., updateProposedDates: bool, ...)` (Phase 61) and Phase 45-02's planned `hasNewOriginalUpload` signal are the same idiom this phase's write-path concern (D-03) should follow if it turns out not to be fully resolved by Phase 45 landing first.
- All three image entities (`CharacterImageEntity`, `ContactImageEntity`, `DungeonMasterProfileImageEntity`) are already dual-column (`OriginalImageData` required, `CroppedImageData` nullable) as of Phase 45-01 (committed: `075b5f0 feat(45-01): rename ImageData to OriginalImageData, add CroppedImageData column`) — any new query projection this phase writes must account for both columns existing, not a single `ImageData` column.

### Integration Points
- Three repositories, three services (write-path only if D-01's sequencing doesn't already resolve it), ~4 ViewModels, ~13 view files (desktop + mobile pairs) — see `<canonical_refs>` for the full file list.
- No EF Core migration needed — this phase only changes query projections and mapping shapes, not the schema (which Phase 45-01 already changed).

</code_context>

<specifics>
## Specific Ideas

User's own words on the actual intent behind the roadmap's goal text: "The essence of what I'm trying to do, is that all images are only retrieved on demand, and not by querying the related entities like a Quest for instance. This drops the load on the DB and shows pages faster. Only when the page is done loading, retrieve the images needed for that specific page." Treat ROADMAP.md's enumerated method list as this session's best-effort capture of that principle, not a hard boundary — D-02 (Contact single-entity scope) follows directly from this.

</specifics>

<deferred>
## Deferred Ideas

- Removing `CharacterRepository.GetMainCharacterForUserAsync` / `ICharacterService.GetMainCharacterForUserAsync` entirely as dead code (zero production callers, confirmed via grep across the whole solution) — noticed during this session's investigation but explicitly left alone as out-of-scope dead-code cleanup, not part of this phase's goal. Not deferred to a specific future phase, just flagged here in case a future cleanup pass wants it.

### Reviewed Todos (not folded)
None — `gsd-sdk query todo.match-phase 62` returned zero matches.

</deferred>

---

*Phase: 62-never-load-image-bytes-as-part-of-entity-list-queries-fetch-*
*Context gathered: 2026-07-07*
