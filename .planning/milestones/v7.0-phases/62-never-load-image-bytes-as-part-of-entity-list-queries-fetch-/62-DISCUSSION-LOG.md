# Phase 62: Stop eagerly loading image bytes in list/entity queries - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-07
**Phase:** 62-never-load-image-bytes-as-part-of-entity-list-queries-fetch-
**Areas discussed:** Image-safety fix (write-path regression risk), Contact scope, ViewModel shape, Phase sequencing, final check

---

## Image-safety fix (write-path regression risk)

Discovered during codebase scouting (not a pre-planned discussion area): naively removing `.Include(ProfileImage)` from the roadmap-named query methods would cause `CharacterService.UpdateAsync`/`ContactService.UpdateAsync` to wipe existing profile pictures on any edit that doesn't re-upload a new photo, because those methods unconditionally re-persist whatever byte[] the (now-unpopulated) model carries.

| Option | Description | Selected |
|--------|-------------|----------|
| Decouple image writes | Change UpdateAsync so it only touches the image when the caller explicitly provides new bytes or a remove signal, mirroring DungeonMasterProfileService.UpsertProfileAsync's already-correct pattern | ✓ |
| Keep the round-trip, re-fetch bytes just before save | Leave UpdateAsync's unconditional re-persist as-is; controllers re-fetch just the image bytes via the existing lightweight single-purpose endpoints right before saving | |
| Let me think about it / propose something else | | |

**User's choice:** Decouple image writes (recommended).
**Notes:** Superseded in practice by the later Phase sequencing decision — Phase 45-02/45-03 (not yet executed) already build this exact fix for a related reason (preserving CroppedImageData). Once Phase 62 is sequenced after 45/46, this decision's *intent* still holds but the *mechanism* will already exist rather than needing to be built by Phase 62 itself. See CONTEXT.md D-01/D-03/D-04.

---

## Contact scope — GetContactWithDetailsAsync

ROADMAP.md's goal text explicitly named `GetCharacterWithDetailsAsync` (Character's single-entity fetch) but only `GetAllContactsWithDetailsAsync` (Contact's list fetch) — not Contact's equivalent single-entity method.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, treat it the same as Character's | GetContactWithDetailsAsync loses its eager Include too, for symmetry with Character and because the write-path fix already has to touch ContactService.UpdateAsync regardless | (superseded, see notes) |
| No, leave it exactly as the roadmap text scoped it | Only touch GetAllContactsWithDetailsAsync for Contact | |

**User's choice:** Neither option directly — user clarified the roadmap's enumerated list may be an imprecise capture of a broader intent: "all images are only retrieved on demand, and not by querying the related entities... Only when the page is done loading, retrieve the images needed for that specific page."
**Notes:** This broader principle resolves the question affirmatively — GetContactWithDetailsAsync is in scope, same as if "Yes" had been selected, but grounded in the user's own stated goal rather than a symmetry argument. Captured as CONTEXT.md D-02.

---

## ViewModel shape — byte[] vs bool

Every current view only does a null/length check on CharacterViewModel.ProfilePicture / ContactViewModel.ContactImage — never renders the actual bytes.

| Option | Description | Selected |
|--------|-------------|----------|
| Replace with bool | Matches roadmap wording and the DMProfileViewModel precedent; removes a byte[] field that would otherwise always be null/empty on these paths | ✓ |
| Keep byte[], just leave it unpopulated | Smaller diff, but leaves a landmine field that looks meaningful but is always null | |

**User's choice:** Replace with bool (recommended).
**Notes:** Captured as CONTEXT.md D-05, including the nuance that the Domain-model layer (Character.ProfilePicture, Contact.ContactImageData) keeps its byte[] shape since it's genuinely used on the write/upload path — only the ViewModel display-side use is replaced.

---

## Phase sequencing — the critical finding

Investigation revealed Phase 45 (Dual-Image Storage Backend) is partially executed (45-01 landed, commit 075b5f0) but 45-02/45-03 are planned-not-executed, and those plans already touch the exact same CharacterService.UpdateAsync/ContactService.UpdateAsync/repository read methods this phase needs, including building the same write-safety fix from the "Image-safety fix" area above. ROADMAP.md listed Phase 62 as depending only on Phase 61, with no accounting for Phase 45/46.

| Option | Description | Selected |
|--------|-------------|----------|
| Run Phase 62 after Phase 45 finishes | Update ROADMAP.md so Phase 62 depends on Phase 45 completing; Phase 62 only needs the read-side change since Phase 45 already builds the write-safety net | (partially selected, see notes) |
| Fold this work into Phase 45 instead of a separate Phase 62 | Add the eager-load removal as a 45-04 plan rather than tracking it as its own phase | |
| Run Phase 62 now, independent of Phase 45 | Keep phase 62 depending only on Phase 61; build its own version of the write-safety fix now | |

**User's choice:** "phase 62 should run after 45 AND 46" — stronger than the first option (which only named Phase 45).
**Notes:** ROADMAP.md's Phase 62 entry updated this session: `Depends on` changed from Phase 61 to Phase 46 (which itself depends on Phase 45), with a rationale note explaining the correction. Captured as CONTEXT.md D-01.

---

## Claude's Discretion

- Exact mechanism for projecting the `HasProfilePicture`/`HasContactImage` boolean at the query level (EF Core projection shape) — planner/researcher's call, constrained only to "must not select the image byte columns."
- Which layer (repository projection, service, or controller) computes the boolean — planner's call, should be consistent across all three entities.
- Whether Character/Contact/DungeonMasterProfile Domain models gain their own `HasProfilePicture`/`HasContactImage` property, or whether it stays purely a ViewModel-layer concern — planner's call.

## Deferred Ideas

- Removing `CharacterRepository.GetMainCharacterForUserAsync` (confirmed dead code, zero production callers) — noticed during investigation, explicitly left alone as out-of-scope dead-code cleanup, not deferred to any specific future phase.

---

## Sanity check — 2026-07-07 (post Phase 45/46 completion)

User re-ran `/gsd-discuss-phase 62` and asked for a validity check of the original decisions now that Phase 45 (45-02, 45-03) and Phase 46 have both finished executing, rather than a fresh discussion.

Dispatched an Explore agent plus direct verification against current source to re-check every claim in CONTEXT.md. Findings:

| Area | Result |
|---|---|
| D-01 write-path fix (`hasNewOriginalUpload`) | **Confirmed shipped** — `CharacterService.UpdateAsync`/`ContactService.UpdateAsync` now have the 3-overload signal pattern; picture-read methods renamed (`GetCharacterOriginalPictureAsync` etc.); controllers repointed. No longer a Phase 62 concern. |
| D-02 Contact single-entity scope | Unaffected, still valid as originally decided. |
| D-03/D-04 write-path hazard background | **Resolved** by Phase 45-02; DM's `UpsertProfileAsync` remains the correct reference pattern, unchanged. |
| D-05 ViewModel byte[]→bool | Still valid and still needed (Character/Contact/EditDMProfileViewModel still carry byte[]; DM's `DMProfileViewModel.HasProfilePicture` still the correct precedent). New finding: Phase 46 already repointed all affected views to render via the cropped-image endpoints, so the byte[] properties are now used *only* for existence checks — lower-risk conversion than originally assessed. |
| Six read-side eager-loads (the actual bug) | **Confirmed still present** in all six methods — this is exactly what Phase 62 needs to fix. |
| Canonical refs / line numbers | Repository and service line numbers re-verified and corrected in CONTEXT.md. View file line numbers flagged as needing re-verification at plan time (Phase 46 touched all of them). |
| Dead code (`GetMainCharacterForUserAsync`) | Reconfirmed still dead, still out of scope. |

**Outcome:** CONTEXT.md rewritten in place to remove the "blocked" status, mark D-01/D-03/D-04 as resolved background rather than open risk, and correct all canonical-reference line numbers/method names to their current, post-Phase-45/46 state. No new gray areas were opened; this was a validation pass, not a new discussion.

