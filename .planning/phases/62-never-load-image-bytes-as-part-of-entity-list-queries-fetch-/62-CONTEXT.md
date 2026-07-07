# Phase 62: Stop eagerly loading image bytes in list/entity queries - Context

**Gathered:** 2026-07-07
**Sanity-checked against current code:** 2026-07-07 — Phase 45 (45-02, 45-03) and Phase 46 have both finished executing since this context was first gathered. All claims below were re-verified against the current codebase; stale ones were corrected in place.
**Status:** Ready for planning — **no longer blocked.** Phase 45/46 dependency is resolved.

<domain>
## Phase Boundary

No repository query for Characters, Contacts, or DungeonMaster profiles pulls the associated image `byte[]` into memory as part of returning list or single-entity data for display. Every page that renders a portrait/photo fetches that image only via its existing dedicated per-entity endpoint (`GetProfilePicture`/`GetCroppedPicture` / `GetContactImage`/`GetCroppedContactImage` / `GetDMProfilePicture`), matching the pattern `QuestRepository.ProjectWithoutCharacterImages` already uses for Quest and QuestLog pages.

**Stated broader principle (user's own words, confirmed during discussion):** "The essence of what I'm trying to do is that all images are only retrieved on demand, and not by querying the related entities like a Quest for instance. This drops the load on the DB and shows pages faster. Only when the page is done loading, retrieve the images needed for that specific page." This is the actual goal — the ROADMAP.md method list (below) is this session's best enumeration of it, not a deliberately narrower boundary. Where a method with the identical pattern was found but not named, it is in scope too (see D-02).

**Read-side methods in scope** (re-verified against current code on 2026-07-07 — the bug is CONFIRMED STILL PRESENT in all six; Phase 45/46 did not touch these):
- `CharacterRepository.cs` — `GetAllCharactersWithDetailsAsync` (12-23), `GetCharactersByOwnerIdAsync` (26-38), `GetCharacterWithDetailsAsync` (41-49) — each still has `.Include(c => c.ProfileImage)`
- `ContactRepository.cs` — `GetAllContactsWithDetailsAsync` (12-30) **and** `GetContactWithDetailsAsync` (33-45, per D-02) — each still has `.Include(c => c.ProfileImage)`
- `DungeonMasterProfileRepository.cs` — `GetProfileByUserIdAsync` (31-36) — still has `.Include(p => p.ProfileImage)`

**Explicitly out of scope:**
- `CharacterRepository.GetMainCharacterForUserAsync` (52-59) — has the identical `.Include(c => c.ProfileImage)` pattern but is **still confirmed dead code** as of this re-check (zero production callers anywhere in `QuestBoard.Service`, `QuestBoard.Domain`, or tests — only referenced in an old v1.0 phase plan doc). Not reachable from any list/display page, so it isn't part of the problem this phase fixes. Leave it alone; removing dead code is a separate concern.
- Per-entity write-path fetches that legitimately need the tracked image — these are single-row write operations, not list/display reads, and are unaffected by this phase. **Note:** the write path this bullet originally worried about (D-03/D-04) has since been rebuilt by Phase 45-02/45-03 into explicit `hasNewOriginalUpload`-signal overloads — see D-01 below.
- Quest/QuestLog pages — already correctly handled via `ProjectWithoutCharacterImages` (`QuestRepository.cs:335-354`, confirmed unchanged), used as the reference pattern, not touched by this phase.

</domain>

<decisions>
## Implementation Decisions

### Phase sequencing — RESOLVED (was the biggest risk found in the original session)
- **D-01 [informational] (RESOLVED as of this sanity check):** Phase 62 was sequenced behind Phase 45 (45-02, 45-03) and Phase 46 because those phases touched the exact files/methods this phase needs to change. **Both have now fully executed** (every sub-plan has a SUMMARY.md). Verified directly against current code:
  - `QuestBoard.Domain/Services/CharacterService.cs` `UpdateAsync` now has three overloads (lines ~67-106): the original 2-arg signature defaults to `hasNewOriginalUpload: false`, plus explicit `UpdateAsync(model, hasNewOriginalUpload, token)` and `UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData, token)` overloads. It calls `repository.UpdateWithProfileImageAsync(model, model.ProfilePicture, croppedImageData, token)` — no longer the old unconditional `UpdateProfileImageAsync` that could wipe images.
  - `QuestBoard.Domain/Services/ContactService.cs` `UpdateAsync` has the identical pattern (lines ~21-60), same three overloads, same `hasNewOriginalUpload` signal.
  - The per-entity image-read methods were renamed and confirmed live: `CharacterRepository.GetCharacterOriginalPictureAsync`/`GetCharacterCroppedPictureAsync` (lines 62-81), `ContactRepository.GetContactOriginalImageAsync`/`GetContactCroppedImageAsync` (lines 48-67) — both cropped-read methods do the `CroppedImageData ?? OriginalImageData` fallback at the query level.
  - Controller actions repointed and confirmed: `CharactersController.GetProfilePicture` (397) / `GetCroppedPicture` (409), `ContactsController.GetContactImage` (355) / `GetCroppedContactImage` (382), `DungeonMasterController.GetDMProfilePicture` (151, calls `GetCroppedPictureAsync`).
- **Correction (found by 62-RESEARCH.md, post-dates this CONTEXT.md's original "Consequence for planning" claim):** the write-path safety net is only **partially** built — the `hasNewOriginalUpload` signal protects the CROPPED image branch (fetches fresh via `GetCharacterCroppedPictureAsync`/`GetContactCroppedImageAsync` on "no new upload"), but the ORIGINAL image (`model.ProfilePicture`/`model.ContactImageData`) is still passed straight through from the round-tripped Domain model, which only stays populated today because `GetCharacterWithDetailsAsync`/`GetContactWithDetailsAsync` still eagerly loads it. Once this phase removes that eager-load, `CharacterService.UpdateAsync`/`ContactService.UpdateAsync`'s "no new upload" branch **must** also re-fetch the original bytes fresh via `GetCharacterOriginalPictureAsync`/`GetContactOriginalImageAsync`, or every edit that doesn't touch the photo silently wipes the stored image. This IS in scope for Phase 62's planner (see 62-RESEARCH.md Pitfall 1) — implemented as plan 62-02.

### Why the write-path mattered (historical background only — fully resolved by D-01, kept here so the risk isn't rediscovered from scratch by a future reader)
- **D-03 [informational] (RESOLVED):** Naively removing `.Include(x => x.ProfileImage)` from the read-side methods without also fixing the write path would have reproduced a real bug: `CharacterService.UpdateAsync`/`ContactService.UpdateAsync` used to unconditionally call `repository.UpdateProfileImageAsync(model.Id, model.ProfilePicture, token)` — passing `null` wiped the stored image. Phase 45-02 fixed this with the `hasNewOriginalUpload` signal described in D-01 — for the cropped-image branch only (see the D-01 correction above for the original-image gap this phase must still close).
- **D-04 [informational] (confirmed, unchanged):** `DungeonMasterProfileService.UpsertProfileAsync` (lines ~17-46) is **still the correct reference pattern** — it only touches the image when the caller explicitly passes new bytes or `removeImage: true`, never relying on a round-tripped read value. `GetProfileByUserIdAsync`'s eager-load remains pure waste with zero write-path risk — safe to fix.

### Contact scope — apply the same treatment to the single-entity fetch too
- **D-02 (unchanged, still valid):** `ContactRepository.GetContactWithDetailsAsync` (Contact's single-entity fetch — powers Details/Edit/ToggleReveal) gets the identical eager-load removal as `GetAllContactsWithDetailsAsync`, even though ROADMAP.md's text only explicitly named the list method. Confirmed via user's stated broader principle (see `<domain>`) — this isn't scope creep, it's the same bug the roadmap's own goal statement already describes.

### ViewModel shape — replace the byte[] display properties with the boolean the roadmap calls for
- **D-05 (unchanged, still valid — re-verified against current code):** `CharacterViewModel.ProfilePicture` (byte[]?, still at line 35) and `ContactViewModel.ContactImage` (byte[]?, still at line 22) — used **only** for `!= null` checks in every view that references them — become `HasProfilePicture: bool` / `HasContactImage: bool` on the display path. `EditDMProfileViewModel.ProfilePicture` (byte[]?, still at line 13) gets the same treatment for consistency with `DMProfileViewModel.HasProfilePicture` (bool, still at line 8 — confirmed already correct, still the pattern to mirror).
  - **New confirmation from this sanity check:** Phase 46 already repointed every affected view's `<img src>` to the dedicated cropped-image endpoints (e.g. `Views/Characters/Index.cshtml:38` now renders `Url.Action("GetCroppedPicture", ...)`, `Views/Contacts/Details.cshtml:15` renders `Url.Action("GetContactImage", ...)`) — the views were already NOT rendering raw bytes even before this phase. This makes D-05's conversion **more mechanical and lower-risk than originally assessed**: the byte[] ViewModel properties are now used *exclusively* for `!= null` existence checks, with zero remaining code path that reads the byte content itself. Re-verify exact current line numbers for each listed view file at plan time (Phase 46 touched these files and line numbers may have shifted from what's listed in `<canonical_refs>` below).
  - **Important nuance the planner must account for (unchanged):** the *Domain model* layer (`Character.ProfilePicture`, `Contact.ContactImageData`) is NOT being replaced — it genuinely carries real upload bytes on the write path. Only the **ViewModel's read/display use** of these properties is being replaced with a bool; the Create/Edit POST code paths that currently stash uploaded bytes onto the ViewModel property should instead use a local variable and set the mapped domain object's byte[] property directly post-map.

### Claude's Discretion
- Exact mechanism for projecting the boolean at the query level (e.g. `.Select(c => new { Entity = c, HasProfilePicture = c.ProfileImage != null })` translated to a SQL `EXISTS`/join, vs. some other EF Core projection shape) — implementation detail, planner/researcher's call. The one hard constraint: the generated SQL must not select the image byte columns.
- Whether the boolean is computed in the repository (returned as part of a projection), the service layer, or the controller (mirroring how DM's current `HasProfilePicture = profile?.ProfilePicture?.Length > 0` is computed in the controller today, confirmed still true at `DungeonMasterController.cs:43`) — planner's call, but should land consistently across all three entities rather than three different layers for three near-identical cases.
- Whether `Character`/`Contact`/`DungeonMasterProfile` Domain models gain a `HasProfilePicture`/`HasContactImage` property of their own, or whether that's purely a ViewModel-layer concern computed from whatever the repository returns — planner's call; no existing precedent forces either way (DM's existing bool lives only at the ViewModel/controller layer today).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 45/46 — formerly a blocking dependency, now fully resolved (D-01)
- `.planning/phases/45-dual-image-storage-backend/45-CONTEXT.md` — full context for the dual-image storage work; read for the `OriginalImageData`/`CroppedImageData` column shape and its rationale
- `.planning/phases/45-dual-image-storage-backend/45-02-SUMMARY.md`, `45-03-SUMMARY.md` — **confirmed executed.** 45-02 shipped the `hasNewOriginalUpload` write-path signal now live in `CharacterService`/`ContactService`; 45-03 shipped the `GetCharacterOriginalPictureAsync`/`GetCharacterCroppedPictureAsync`/`GetContactOriginalImageAsync`/`GetContactCroppedImageAsync` renames and the controller repoints. Both verified directly against current source on 2026-07-07 — no further action needed on the write path for Phase 62.
- `.planning/phases/45-dual-image-storage-backend/45-PATTERNS.md` — established patterns for the dual-image work, useful precedent for how this phase's own read-side changes should be structured
- `.planning/phases/46-client-side-crop-ui/` — **now exists and is fully executed** (8 sub-plans, all with SUMMARY.md). Its work repointed view `<img>` tags to the cropped-image endpoints (see D-05 update above) — read `46-CONTEXT.md`/`46-UI-SPEC.md` if the planner needs the final cropped-vs-original display shape, though the read-query changes this phase makes shouldn't conflict with it.
- `.planning/ROADMAP.md` — Phase 62 entry; dependency note should be updated from "Phase 46" (still correct, just no longer *blocking* — see STATE.md/roadmap for current phase status)

### Reference pattern already solved (Quest/QuestLog)
- `QuestBoard.Repository/QuestRepository.cs:335-354` — `ProjectWithoutCharacterImages` — confirmed unchanged, still the existing precedent this phase's goal explicitly names: omits `.ThenInclude(c => c!.ProfileImage)` entirely when loading `PlayerSignup.Character` for Quest/QuestLog pages. Character/Contact/DM's own list pages DO need a has-image boolean (unlike Quest), so the exact mechanism differs, but the "don't eager-load the byte[] at all" principle is the same.

### Repositories to change (re-verified current line numbers, 2026-07-07)
- `QuestBoard.Repository/CharacterRepository.cs` — `GetAllCharactersWithDetailsAsync` (12-23), `GetCharactersByOwnerIdAsync` (26-38), `GetCharacterWithDetailsAsync` (41-49) — remove `.Include(c => c.ProfileImage)`, project `HasProfilePicture` instead. (Note: `GetMainCharacterForUserAsync` at 52-59 has the same pattern but stays out of scope — still dead code.)
- `QuestBoard.Repository/ContactRepository.cs` — `GetAllContactsWithDetailsAsync` (12-30), `GetContactWithDetailsAsync` (33-45, per D-02) — remove `.Include(c => c.ProfileImage)`, project `HasContactImage` instead. (Note: `GetContactOriginalImageAsync`/`GetContactCroppedImageAsync` now live at 48-67 immediately below — leave those untouched, they're the correct on-demand fetch methods this phase routes display through.)
- `QuestBoard.Repository/DungeonMasterProfileRepository.cs` — `GetProfileByUserIdAsync` (31-36) — remove `.Include(p => p.ProfileImage)`, project `HasProfilePicture` instead.

### Services (write-path — CONFIRMED RESOLVED by Phase 45-02, no action needed here)
- `QuestBoard.Domain/Services/CharacterService.cs:67-106` — `UpdateAsync` (3 overloads) — already uses the explicit `hasNewOriginalUpload` signal; the D-03 hazard this originally described no longer exists.
- `QuestBoard.Domain/Services/ContactService.cs:21-60` — same pattern, already resolved.
- `QuestBoard.Domain/Services/DungeonMasterProfileService.cs:17-46` — `UpsertProfileAsync` — the already-correct reference pattern (D-04), unchanged.

### ViewModels / AutoMapper (D-05, line numbers re-confirmed 2026-07-07)
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs:35` — `ProfilePicture` byte[]? → `HasProfilePicture` bool
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs:22` — `ContactImage` byte[]? → `HasContactImage` bool
- `QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs:13` — `ProfilePicture` byte[]? → `HasProfilePicture` bool (for consistency with `DMProfileViewModel.HasProfilePicture`, already correct)
- `QuestBoard.Service/ViewModels/DungeonMasterViewModels/DMProfileViewModel.cs:8` — `HasProfilePicture` bool — the existing correct precedent to mirror for the other two view models
- `QuestBoard.Repository/Automapper/EntityProfile.cs` (Entity ↔ Domain, note: NOT `QuestBoard.Domain/Automapper/` as CLAUDE.md states — that reference is stale) — `CharacterEntity ↔ Character`, `ContactEntity ↔ Contact` (includes the `OriginalImageData`/`ContactImageData` mapping), `DungeonMasterProfileEntity ↔ DungeonMasterProfile`
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` — `Character ↔ CharacterViewModel`, `Contact ↔ ContactViewModel` (includes the `ContactImageData ↔ ContactImage` explicit `ForMember` mapping) — exact line numbers shifted since Phase 46, re-check at plan time

### Views affected by the D-05 ViewModel property rename
Confirmed Phase 46 already repointed these to render via image endpoints (`GetCroppedPicture`/`GetContactImage`/`GetCroppedContactImage`/`GetDMProfilePicture`) rather than raw bytes — they only need the `!= null` → bool-property-name update, not a rendering change. **Re-verify exact current line numbers at plan time** (Phase 46 touched all of these; numbers below are pre-46 and will have shifted):
- `QuestBoard.Service/Views/Characters/Index.cshtml`, `Index.Mobile.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`
- `QuestBoard.Service/Views/Contacts/Index.cshtml`, `Index.Mobile.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`
- `QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml`, `EditProfile.Mobile.cshtml` (Profile.cshtml/.Mobile.cshtml already use the bool)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `DMProfileViewModel.HasProfilePicture` + `DungeonMasterController.Profile`'s `HasProfilePicture = profile?.ProfilePicture?.Length > 0` (confirmed still at `DungeonMasterController.cs:43`) — the exact target end-state pattern already proven for one of the three entities; mirror this shape for Character/Contact and for `EditDMProfileViewModel`.
- `QuestRepository.ProjectWithoutCharacterImages` — proof this codebase already has precedent for "omit the image include entirely from a query used for display."

### Established Patterns
- Explicit boolean-signal parameters over implicit round-tripped state: `UpdateQuestPropertiesWithNotificationsAsync(..., updateProposedDates: bool, ...)` (Phase 61) and **Phase 45-02's `hasNewOriginalUpload` signal (now shipped, not just planned)** are the same idiom. Phase 62's own read-side change doesn't need to invent a new write-path idiom — it can reuse the shipped one as-is since it isn't touching the write path at all.
- All three image entities (`CharacterImageEntity`, `ContactImageEntity`, `DungeonMasterProfileImageEntity`) are dual-column (`OriginalImageData` required, `CroppedImageData` nullable), confirmed unchanged since Phase 45-01 — any new query projection this phase writes must account for both columns existing, not a single `ImageData` column.

### Integration Points
- Three repositories, ~4 ViewModels, ~13 view files (desktop + mobile pairs) — see `<canonical_refs>` for the full file list. **Services are no longer in scope** (write-path already fixed by Phase 45-02) — this narrows the integration surface versus the original assessment.
- No EF Core migration needed — this phase only changes query projections and mapping shapes, not the schema (which Phase 45-01 already changed).

</code_context>

<specifics>
## Specific Ideas

User's own words on the actual intent behind the roadmap's goal text: "The essence of what I'm trying to do, is that all images are only retrieved on demand, and not by querying the related entities like a Quest for instance. This drops the load on the DB and shows pages faster. Only when the page is done loading, retrieve the images needed for that specific page." Treat ROADMAP.md's enumerated method list as this session's best-effort capture of that principle, not a hard boundary — D-02 (Contact single-entity scope) follows directly from this.

</specifics>

<deferred>
## Deferred Ideas

- Removing `CharacterRepository.GetMainCharacterForUserAsync` / `ICharacterService.GetMainCharacterForUserAsync` entirely as dead code (zero production callers, reconfirmed via grep across the whole solution on 2026-07-07) — noticed during the original session's investigation but explicitly left alone as out-of-scope dead-code cleanup, not part of this phase's goal. Not deferred to a specific future phase, just flagged here in case a future cleanup pass wants it.

### Reviewed Todos (not folded)
None — `gsd-sdk query todo.match-phase 62` returned zero matches.

</deferred>

---

*Phase: 62-never-load-image-bytes-as-part-of-entity-list-queries-fetch-*
*Context gathered: 2026-07-07*
*Sanity-checked against current code: 2026-07-07 (post Phase 45/46 completion)*
