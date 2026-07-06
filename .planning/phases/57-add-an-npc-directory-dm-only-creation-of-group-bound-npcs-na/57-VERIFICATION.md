---
phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
verified: 2026-07-06T23:30:00Z
status: passed
score: 9/9 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 57: Add an NPC Directory (Contacts) Verification Report

**Phase Goal:** A DM-tier user can create, edit, reveal/hide, and delete group-bound "Contacts" (name, image, description, town/city, optional sub-location), every group member can view revealed Contacts and collaboratively add/edit/delete freeform authored+timestamped notes on the Details page, and hidden Contacts stay invisible (list-filtered + 404) to everyone except their creator and DM-tier viewers who flip a per-group session "Show Hidden" toggle — with full desktop + mobile parity, mirroring the Characters feature.

**Verified:** 2026-07-06
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DM-tier user can Create/Edit/Delete a Contact (name, image, description, town/city, sub-location) | VERIFIED | `ContactsController.cs:77-224` — Create/Edit/Delete all gated `[Authorize(Policy = "DungeonMasterOnly")]`; `ContactViewModel.cs` carries Name/ContactImage/Description/TownCity/SubLocation; `ContactEntity.cs` persists all fields |
| 2 | DM-tier user can reveal/hide a Contact via a toggle | VERIFIED | `ContactsController.ToggleReveal` (lines 226-242) flips `IsRevealed`; `Details.cshtml:56-70` renders "Reveal Contact"(`btn-success`)/"Hide Contact"(`btn-secondary`) form |
| 3 | New Contacts default to hidden | VERIFIED | `ContactsController.Create` POST (line 128): `contact.IsRevealed = false;` |
| 4 | Every group member can view revealed Contacts on Index/Details | VERIFIED | `Index`/`Details` actions use plain `[Authorize]` (no DM policy); `IsVisibleTo` returns true when `contact.IsRevealed` |
| 5 | Every group member can add/edit/delete freeform authored+timestamped notes on Details, fully collaborative (no ownership guard) | VERIFIED | `AddNote`/`EditNote`/`DeleteNote` (lines 258-315) carry plain `[Authorize]` only, no ownership check; integration test `EditNote_DifferentGroupMember_CanEditNoteAuthoredByAnotherUser` passes; `Details.cshtml:106-156` renders add-note form + inline edit-in-place, ungated on `CanManage` |
| 6 | Hidden Contacts are list-filtered on Index and 404 on Details for viewers outside the visibility rule | VERIFIED | `Index` (line 33): `.Where(c => IsVisibleTo(...))`; `Details` (lines 64-67): returns `NotFound()` when `IsVisibleTo` fails |
| 7 | Hidden Contact stays invisible to everyone except (a) its creator and (b) DM-tier viewers with the per-group "Show Hidden" session toggle on | VERIFIED | `IsVisibleTo` (lines 368-381): revealed OR creator OR (DM-tier AND toggle); `SessionKeys.ShowHiddenContactsKey(groupId)` scopes toggle per group via `RequireActiveGroupId()` |
| 8 | Hidden-Contact protection extends to the image endpoint (no IDOR leak via GetContactImage) | VERIFIED (post-review fix confirmed in codebase) | `ContactsController.GetContactImage` (lines 317-342) now loads the contact, computes `viewerIsDmTier`/`includeHidden`, and calls `IsVisibleTo(...)` returning `NotFound()` on failure — matches 57-REVIEW.md CR-01's proposed fix verbatim. Regression tests `GetContactImage_HiddenContact_PlayerGetsNotFound` and `GetContactImage_HiddenContact_CreatorCanFetchOwnImage` both pass (ran directly: 2/2 passed) |
| 9 | Full desktop + mobile parity across Index/Details/Edit/Create | VERIFIED | All 8 view files exist: `Index/Details/Edit/Create.cshtml` + `Index/Details/Edit/Create.Mobile.cshtml`; mobile views reproduce toggle/badge/notes/reveal UI per 57-06-SUMMARY.md and source inspection |

**Score:** 9/9 truths verified (0 present-but-behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Repository/Entities/ContactEntity.cs` | Contact EF entity | VERIFIED | Name/Description/TownCity/SubLocation/IsRevealed/CreatedByUserId/Notes/GroupId all present, no Character-only fields carried over |
| `QuestBoard.Repository/Entities/ContactImageEntity.cs` | 1:1 FK-as-PK image table | VERIFIED | Exists, referenced by `ContactEntity.ProfileImage` |
| `QuestBoard.Repository/Entities/ContactNoteEntity.cs` | authored+timestamped note table | VERIFIED | ContactId FK, Text, AuthorUserId, CreatedAt, UpdatedAt (nullable) all present |
| `QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs` | migration creating 3 tables | VERIFIED | `CreateTable` for Contacts/ContactImages/ContactNotes; `ContactNotes→Contacts` uses `ReferentialAction.Cascade`, `ContactNotes→AspNetUsers` FK omits `onDelete` (defaults to Restrict/NoAction) — no cascade cycle |
| `QuestBoard.Domain/Models/Contact.cs` | Contact + ContactNote domain models | VERIFIED | Matches entity shape; `ContactImageData` byte[]? projection field present |
| `QuestBoard.Repository/ContactRepository.cs` | data access incl. note methods | VERIFIED | Dedicated `AddNoteAsync`/`UpdateNoteAsync`/`DeleteNoteAsync` operate directly on `ContactNoteEntity` DbSet; `UpdateNoteAsync` verifies `entity.ContactId != note.ContactId` before updating (WR-03 fix, confirmed in code at line 126) |
| `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` | full HTTP surface | VERIFIED | All 11 actions present: Index/Details/Create/Edit/Delete/ToggleReveal/ToggleShowHidden/AddNote/EditNote/DeleteNote/GetContactImage |
| `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` | form/detail ViewModel | VERIFIED | Fields match spec; reused MaxFileSize/AllowedExtensions validators present |
| 8 view files (4 desktop + 4 mobile) | full UI surface | VERIFIED | All 8 files present under `QuestBoard.Service/Views/Contacts/` |
| `QuestBoard.Service/Automapper/ViewModelProfile.cs` | Domain↔ViewModel mapping | VERIFIED | `CreateMap<Contact, ContactViewModel>`/reverse both carry explicit `.ForMember(ContactImage ↔ ContactImageData)` (the human-verify-found AutoMapper bug fix, confirmed present at lines 78-88) |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `ContactsController` | `IContactService`/`IContactRepository` | constructor injection | WIRED | DI registered in both `QuestBoard.Domain/Extensions/ServiceExtensions.cs:21` and `QuestBoard.Repository/Extensions/ServiceExtensions.cs:25` |
| `QuestBoardContext` | `ContactEntity`/`ContactImageEntity`/`ContactNoteEntity` | `HasQueryFilter` fail-closed on GroupId | WIRED | Confirmed at `QuestBoardContext.cs:350-363` — no SuperAdmin bypass, comment explicitly documents the deliberate omission |
| `_Layout.cshtml` / `_Layout.Mobile.cshtml` | `ContactsController.Index` | nav `<li>` | WIRED | Both layouts render the Contacts nav link unconditionally (outside the `@if (activeBoardType == BoardType.OneShot)` block) |
| `Details.cshtml` | `ContactsController` note actions | `asp-action="AddNote"/"EditNote"/"DeleteNote"` POST forms | WIRED | Confirmed in view source; matching controller actions exist and are ungated (D-09) |
| `ContactsController.GetContactImage` | `IsVisibleTo` visibility check | direct call before serving image bytes | WIRED (post-review fix) | Confirmed present; this was the CR-01 finding, now closed |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full test suite passes | `dotnet test` (run once, full solution) | 191 unit + 353 integration = 544/544 passed | PASS |
| CR-01 regression tests pass | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GetContactImage_HiddenContact"` | 2/2 passed | PASS |
| No `CanManageCharacterAsync`-style guard leaked into ContactsController | `grep -n "CanManageCharacterAsync" ContactsController.cs` | no matches | PASS |
| Every `[HttpPost]` action carries `[ValidateAntiForgeryToken]` | count comparison | 8 HttpPost / 8 ValidateAntiForgeryToken | PASS |
| No debt markers (TBD/FIXME/XXX/TODO/HACK/PLACEHOLDER) in Contact feature files | grep scan across controller/viewmodels/domain/repository/entities/views | no matches (only legitimate HTML `placeholder="..."` attributes and CSS placeholder classes) | PASS |

### Requirements Coverage

Not applicable — Phase 57 is an ad-hoc backlog phase with no `requirements:` field populated in any PLAN frontmatter (all empty `[]`) and no REQUIREMENTS.md entries mapped to Phase 57. Confirmed REQUIREMENTS.md only tracks MOBILE-01/02, VOTE-01–07, IMAGE-01–05 (Phases 43-46), none of which reference Phase 57. This matches the phase's stated context ("ad-hoc backlog phase — no REQUIREMENTS.md mapping; source of truth is 57-CONTEXT.md decisions D-01 through D-20"). No orphaned requirements found.

### Anti-Patterns Found

None. Scanned all Contact-feature controller/ViewModel/domain/repository/entity/view files for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER|not yet implemented|coming soon` — zero matches beyond legitimate UI placeholder text/CSS class names.

### Post-Review Fix Verification (per verification task instructions)

All four fixes claimed in SUMMARY.md/57-REVIEW.md were independently confirmed present and correct in the current codebase (not just claimed):

1. **AutoMapper ContactImage/ContactImageData mismatch** (found during 57-06 human-verify) — confirmed fixed: `ViewModelProfile.cs:78-88` carries explicit `.ForMember` mappings both directions.
2. **CR-01 (critical) — GetContactImage bypassed hidden-Contact visibility check** — confirmed fixed: `ContactsController.cs:317-342` now applies `IsVisibleTo` before serving image bytes; both new regression tests pass.
3. **WR-01 — missing ModelState validation on AddNote/EditNote** — confirmed fixed: both actions check `ModelState.IsValid` (lines 268, 290) before proceeding.
4. **WR-03 — EditNote didn't verify contactId match** — confirmed fixed, but note the fix landed in the repository layer rather than the controller: `ContactRepository.UpdateNoteAsync` (line 126) checks `entity.ContactId != note.ContactId` and no-ops on mismatch, which achieves the same protection the review's controller-level suggestion aimed for.

WR-02 (redundant image-row rewrite on every Edit) and the two Info findings (IN-01 duplicate validation, IN-02 MIME sniffing fallback) were deliberately left unresolved per the phase's own decision — confirmed these are non-blocking code-quality items, not correctness or security defects, consistent with 57-REVIEW.md's classification (Warning/Info, not Critical).

### Human Verification Required

None. The phase's own Plan 06 Task 2 (`checkpoint:human-verify`, blocking gate) was already executed and approved by the user during phase execution — 57-06-SUMMARY.md documents the full 10-step manual verification (nav visibility, create/default-hidden, three-branch visibility including as Player, per-group toggle + session reset, reveal, collaborative notes, edit/delete, mobile parity, image validation, UI-SPEC copy/style conformance) was approved after the AutoMapper bug was found and fixed live. No further human verification items were identified during this goal-backward pass.

### Gaps Summary

No gaps found. All 9 derived observable truths are verified against the current codebase (not SUMMARY.md claims alone). All 3 code-review findings (1 critical, 2 warnings) and the 1 human-verify-discovered bug are confirmed fixed in the current source, with the critical fix additionally covered by 2 passing regression tests. The full test suite passes 544/544, matching the reported count. Phase goal is achieved.

---

_Verified: 2026-07-06_
_Verifier: Claude (gsd-verifier)_
