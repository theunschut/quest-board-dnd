# Phase 46: Client-Side Crop UI - Context

**Gathered:** 2026-07-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire an interactive, client-side crop widget (Cropper.js v2.1.1) into every photo-upload form in the app — Character photo (Create/Edit), DM profile photo (Edit), and Contact photo (Create/Edit) — so the user frames a square crop before saving, and both the original and cropped bytes submit in one ordinary form POST. Covers requirements IMAGE-01, IMAGE-04, IMAGE-05, plus the Contact-image parity Phase 45's D-01 already locked in (Contacts didn't exist when those requirements were written).

This phase also decides, and rewires, which of the many places a photo is displayed across the app reads the cropped column vs. the original column — the dual-image backend (Phase 45) is fully built and already exposes both, but every current `<img>` tag still points at the original-image endpoints.

**Not in scope:** Any further backend/schema work (Phase 45 already shipped it), rotate/flip controls (IMAGE-06, deferred to v2), multiple aspect-ratio presets (IMAGE-07, deferred to v2).

</domain>

<decisions>
## Implementation Decisions

### Crop Aspect Ratio
- **D-01:** All three photo fields — Character, Contact, DM profile — crop to one locked **square (1:1)** aspect ratio in the Cropper.js widget. Supersedes the initial research assumption that Character/Contact (landscape list-card) and DM (circle) might need different locked ratios. A square crop still displays as a circle anywhere `border-radius: 50%` is applied (confirmed against the existing DM `rounded-circle` pattern) — no functional conflict with DM's circular presentation.

### List-Card Display Shape
- **D-02:** Because a square crop shown inside the current landscape list-card box (`.character-image`/`.contact-image` in `characters.css`/`contacts.css` — currently `width: 100%; height: 200px`, fluid width) would get re-cropped top/bottom by `object-fit: cover` to fill the wider box, undoing the user's square framing — the Character and Contact list-card image containers change from a landscape box to a **square aspect-ratio box**. This is a deliberate visual layout change to the Characters and Contacts grid pages, not just a backend rewire. DM's existing circle CSS (`rounded-circle` over a 128×128 box in `dm-profile.css`) needs no change.

### Cropped vs. Original — Display Rule
- **D-03:** The **original** image displays ONLY on two pages: **Character Details** and **Contact Details** (desktop + mobile both). The **cropped** image displays literally everywhere else a photo appears, including:
  - Characters index / Contacts index (list-card thumbnails)
  - **DM Profile page** (desktop + mobile) — this explicitly **supersedes REQUIREMENTS.md's literal IMAGE-04 text**, which groups DM with Character as "displays the original." The user confirmed DM Profile should show the cropped image, not the original, leaving Character/Contact Details as the only two original-only exceptions in the entire app.
  - Quest Details participant list (selected + waitlist sections)
  - Quest Manage participant roster
  - QuestLog Details recap page (per-participant avatar)
  - `_QuestCard` partial (quest-board card inline avatar)
  - Character/Contact Create+Edit forms' own "current photo" preview thumbnail

  **Downstream note:** Treat this decision as the source of truth for which endpoint each view calls — do not implement IMAGE-04 from its literal REQUIREMENTS.md wording.

### Crop Interaction Pattern
- **D-04:** Selecting a file opens a **modal** with the Cropper.js widget over the photo (not an inline-in-form widget). If the user submits without ever touching the crop frame, a **centered default crop at the locked 1:1 ratio auto-saves** — the modal never blocks form submission waiting for an explicit "confirm crop" click.

### Claude's Discretion
- Exact new controller-action/service-method names for the "read cropped" endpoints (e.g. `GetCroppedPicture`, `GetCharacterCroppedPictureAsync`) — should mirror the naming Phase 45 already established for the renamed original-read methods (`GetCharacterOriginalPictureAsync`, etc.).
- Whether DM's single existing `GetDMProfilePicture` action/`GetProfilePictureAsync` service method is simply repointed to read `CroppedImageData ?? OriginalImageData` (per Phase 45's D-02a fallback) in place, or whether a separate original-read method is also added for future-proofing even though nothing currently displays it. No UI currently needs a DM "original" endpoint given D-03.
- EXIF-orientation-correction snippet sourcing (no npm package — needs a specifically-vetted vendored snippet or hand-rolled reader, flagged HIGH-severity in `.planning/research/PITFALLS.md`) and the canvas-downscale-before-crop implementation (~2000–2500px max dimension bound before any full-resolution canvas work, to avoid iOS Safari's 16.7M-pixel canvas ceiling) — both flagged in STATE.md as needing a dedicated `--research-phase` pass during planning, not resolved by this discussion.
- Exact modal markup/styling (Bootstrap modal vs. a custom overlay) to match this app's existing UI conventions.
- Cropped-output resolution (resized to a fixed pixel size vs. native resolution of the selected crop box, up to the downscaled source bound) — not raised as a user-facing concern; pick whatever keeps output file size reasonable.

### Pre-Execution Blocker (not resolved by this discussion)
- **Real-device verification access is NOT YET CONFIRMED** for this phase. Phase 43 verified its iOS Safari fix on a physical iPhone over LAN with no device-cloud service — Phase 46 has its own real-device-only checks (EXIF orientation from an actual phone-camera photo, iOS Safari's canvas-memory ceiling, touch-drag/pinch precision on a real touchscreen) per `.planning/research/SUMMARY.md`. Do not schedule/attempt the verification checkpoint until device access (the same iPhone, and optionally an Android device for a second touch-gesture platform) is explicitly confirmed with the user.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Roadmap
- `.planning/ROADMAP.md` §"Phase 46: Client-Side Crop UI" — phase goal, success criteria, requirements mapping, Phase 45 dependency (now satisfied — Phase 45 is fully complete across all 3 plans, though `ROADMAP.md`/`STATE.md`'s own checkboxes/progress-percent are stale and still show it in-progress as of this session; a `/gsd-progress` bookkeeping pass should refresh them separately)
- `.planning/REQUIREMENTS.md` §"Character & Profile Image Cropping" — IMAGE-01, IMAGE-04, IMAGE-05 full text. **IMAGE-04 is stale relative to D-03 above** — its literal "guild-member list / character/DM details" split undersells both the Contact-scope expansion (Phase 45 D-01) and this phase's actual cropped-vs-original rule (only 2 pages show original; DM Profile shows cropped).

### Project-Level Research (pre-dates this discussion's specific decisions — read with the overrides below in mind)
- `.planning/research/SUMMARY.md` §"Phase 3: Client-side crop UI" — Cropper.js v2.1.1 vendoring plan, build-order rationale, Pitfalls 1–3 summary
- `.planning/research/FEATURES.md` — crop-UX must-haves (fixed aspect ratio, live preview, zoom/pan) and explicitly-rejected anti-patterns (free-form/unconstrained crop, multiple aspect-ratio presets). **Its aspect-ratio guidance ("matched to this app's existing avatar display shape," implying per-field ratios) is superseded by D-01's single unified square ratio.**
- `.planning/research/PITFALLS.md` — full detail on the three real-device-only pitfalls (EXIF orientation, iOS Safari canvas memory ceiling, touch-drag precision) referenced in the Claude's Discretion and Pre-Execution Blocker sections above
- `.planning/research/STACK.md` — Cropper.js version decision detail, vendoring approach (no npm/bundler, matches this app's no-bundler `site.js` convention)

### Related Phase Context (Phase 46 builds directly on top of this)
- `.planning/phases/45-dual-image-storage-backend/45-CONTEXT.md` — D-01 (Contact-image scope expansion), D-02/D-02a (`OriginalImageData`/`CroppedImageData` column naming + `CroppedImageData ?? OriginalImageData` fallback for un-migrated rows), D-03 (shared `IImageValidationService`)
- `.planning/phases/45-dual-image-storage-backend/45-03-SUMMARY.md` — current as-built state: `IImageValidationService` validates original+cropped pairs across all 5 upload actions; `CharactersController.Edit`/`ContactsController.Edit` already call the `hasNewOriginalUpload`-aware `UpdateAsync` overload; **all existing `GetProfilePicture`/`GetContactImage`/`GetDMProfilePicture` actions currently read the ORIGINAL column only** — none of them read `CroppedImageData` yet, confirming this phase must add the new cropped-read endpoints from scratch, not just repoint views to something that already exists

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CharactersController.GetProfilePicture` (`QuestBoard.Service/Controllers/Characters/CharactersController.cs:364`) — calls `characterService.GetCharacterOriginalPictureAsync`. Keep as-is for Character Details; add a sibling cropped-read action for every other Character consumer.
- `ContactsController.GetContactImage` (`QuestBoard.Service/Controllers/Contacts/ContactsController.cs:318-346`) — existing magic-byte content-type sniffing pattern to replicate for the new cropped-read action(s).
- `DungeonMasterController.GetDMProfilePicture` (`QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs:135`) — calls `dmProfileService.GetProfilePictureAsync`. Per D-03, this single action can likely just be repointed to the `Cropped ?? Original` read (DM has no page needing the original anymore).
- `IImageValidationService`/`ImageValidationService` (`QuestBoard.Domain/Interfaces/IImageValidationService.cs`, `QuestBoard.Domain/Services/ImageValidationService.cs`, added Phase 45-03) — validates an `ImageFileInput` original+optional-cropped pair; the crop UI's second (cropped) file submission is exactly the second half of this existing contract.
- `ICharacterService`/`IContactService`'s `UpdateAsync(model, hasNewOriginalUpload, token)` overload (added Phase 45-02/03) — already threads the new-upload signal needed to correctly clear a stale crop on re-upload; the crop UI's submission just needs to populate both file fields correctly on the `ViewModel`.

### Established Patterns
- All photo-serving actions today (`GetProfilePicture`, `GetContactImage`, `GetDMProfilePicture`) share the same magic-byte MIME-sniffing-into-`File()`-result pattern — new cropped-read actions should mirror this exactly, not introduce a new response pattern.
- `.character-image`/`.contact-image` (`characters.css:112-142`, `contacts.css:91-121`) are near-identical landscape box definitions (`width: 100%; height: 200px; object-fit: cover`) — both need the same square-box change per D-02, in parallel.
- `.dm-profile-photo`/`.dm-profile-photo-mobile` (`dm-profile.css`, `dm-profile.mobile.css`) already render as a 128×128 (or mobile-equivalent) circle via `rounded-circle` — no CSS change needed here per D-01/D-02.

### Integration Points — Views Needing Repoint to the New Cropped-Read Endpoint
- `Views/Characters/Index.cshtml`, `Index.Mobile.cshtml` (list thumbnails)
- `Views/Characters/Create.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml` ("current photo" preview)
- `Views/Contacts/Index.cshtml`, `Index.Mobile.cshtml` (list thumbnails)
- `Views/Contacts/Edit.cshtml`, `Edit.Mobile.cshtml` ("current photo" preview)
- `Views/DungeonMaster/Profile.cshtml`, `Profile.Mobile.cshtml`, `EditProfile.cshtml`, `EditProfile.Mobile.cshtml` (via repointing the shared `GetDMProfilePicture` action itself — no view markup change needed)
- `Views/Quest/Details.cshtml` (2 occurrences: selected + waitlist participant avatars)
- `Views/Quest/Manage.cshtml` (participant roster avatar)
- `Views/Quest/_QuestCard.cshtml` (inline quest-board card avatar)
- `Views/QuestLog/Details.cshtml`, `Details.Mobile.cshtml` (recap participant avatar)

### Views Staying on the Original-Read Endpoint (no change)
- `Views/Characters/Details.cshtml`, `Details.Mobile.cshtml`
- `Views/Contacts/Details.cshtml`, `Details.Mobile.cshtml`

### Views Needing the New Crop-Modal UI Itself (file-select → Cropper.js modal → dual submission)
- `Views/Characters/Create.cshtml`, `Edit.cshtml` + their `.Mobile.cshtml` equivalents
- `Views/Contacts/Create.cshtml`, `Edit.cshtml` + their `.Mobile.cshtml` equivalents
- `Views/DungeonMaster/EditProfile.cshtml`, `EditProfile.Mobile.cshtml`

</code_context>

<specifics>
## Specific Ideas

- The user explicitly walked through the square-crop-as-circle CSS mechanics before committing to D-01 — confirming a 1:1 stored crop displays correctly as DM's existing circle via `border-radius: 50%`, with no functional conflict.
- The cropped-vs-original display rule (D-03) was derived interactively, location by location, rather than accepted as REQUIREMENTS.md's literal IMAGE-04 text — the end state is meaningfully broader (cropped is the default nearly everywhere; original is the rare exception, only on Character/Contact Details) than the original requirement wording implied.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. Rotate/flip (IMAGE-06) and multiple aspect-ratio presets (IMAGE-07) are already documented v2 deferrals in REQUIREMENTS.md, not new ideas from this session.

</deferred>

---

*Phase: 46-Client-Side Crop UI*
*Context gathered: 2026-07-07*
