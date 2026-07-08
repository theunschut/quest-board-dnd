---
phase: 46
slug: client-side-crop-ui
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-07
---

# Phase 46 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5) — confirmed via `QuestBoard.UnitTests.csproj` |
| **Config file** | Standard `dotnet test` — no custom `xunit.runner.json`; project-level `.csproj` settings only |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~ImageValidationService\|FullyQualifiedName~CharacterService\|FullyQualifiedName~ContactService\|FullyQualifiedName~DungeonMasterProfileService"` |
| **Full suite command** | `dotnet test` (runs both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`) |
| **Estimated runtime** | ~30-60 seconds (integration suite spins up `WebApplicationFactory`) |

---

## Sampling Rate

- **After every task commit:** Run the quick run command (unit tests only, no `WebApplicationFactory` startup cost)
- **After every plan wave:** Run the full suite command
- **Before `/gsd:verify-work`:** Full suite must be green, plus the real-device manual checklist (blocked pending device-access confirmation per CONTEXT.md's Pre-Execution Blocker)
- **Max feedback latency:** ~60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD-service-signature | TBD | 1 | IMAGE-01/D-01 | V5 | `UpdateAsync`/`UpsertProfileAsync` accept a caller-supplied cropped byte[] instead of hardcoding null | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~CharacterServiceTests\|FullyQualifiedName~ContactServiceTests\|FullyQualifiedName~DungeonMasterProfileServiceTests` | ❌ Wave 0 — extend existing service test files | ⬜ pending |
| TBD-cropped-read-endpoints | TBD | TBD | IMAGE-04/D-03 | V4 | New cropped-read actions return `CroppedImageData ?? OriginalImageData`, replicating sibling original-read endpoint's auth/visibility checks | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CharactersControllerIntegrationTests\|FullyQualifiedName~ContactsControllerIntegrationTests` | ❌ Wave 0 — actions don't exist yet | ⬜ pending |
| TBD-dm-repoint | TBD | TBD | IMAGE-04/D-03 | V4 | `GetDMProfilePicture` returns cropped-or-fallback bytes after repoint | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~DungeonMasterControllerIntegrationTests` | Check at plan time — confirm whether this test file exists yet | ⬜ pending |
| TBD-validation-wiring | TBD | TBD | IMAGE-05 | V5 | `ValidateImagePair` is called with a real (non-null) cropped `ImageFileInput` once the crop UI submits a genuine cropped file | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~ImageValidationServiceTests` | ✅ exists from Phase 45 — extend, don't replace | ⬜ pending |
| TBD-crop-modal-ui | TBD | TBD | IMAGE-01 | — | Client-side drag/resize/zoom crop frame exists and produces a cropped Blob | manual-only | N/A — manual verification per D-04's modal flow | N/A | ⬜ pending |
| TBD-real-device-checks | TBD | TBD | Success Criterion #4 | — | EXIF orientation, canvas-memory ceiling, touch-drag precision, all on a real device | manual-only, real-device required | N/A | N/A — gated behind Pre-Execution Blocker; do not attempt to automate | ⬜ pending |

*Task IDs are TBD — the planner assigns concrete plan/task numbers. This table's rows are the required coverage; the planner must map each to an actual task ID.*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Services/CharacterServiceTests.cs` (or equivalent) — extend with coverage for the widened `UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData, token)` signature's three branches (new crop supplied / new original with no crop / no new file at all)
- [ ] `QuestBoard.UnitTests/Services/ContactServiceTests.cs` / `DungeonMasterProfileServiceTests.cs` — same three-branch coverage for the other two services
- [ ] `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs` — extend with a case posting a real `CroppedPictureFile` alongside `ProfilePictureFile`, asserting `CroppedImageData` persists as the submitted crop bytes (not null, not the fallback) — mirrors the existing `Edit_NewOriginalPhotoUpload_ClearsStaleCroppedImage` pattern
- [ ] Equivalent extension to `ContactsControllerIntegrationTests.cs`
- [ ] New integration coverage for the `GetCroppedPicture`/equivalent actions once added (no test exists today because the actions don't exist yet)
- [ ] Confirm whether `DungeonMasterControllerIntegrationTests.cs` exists at all at plan time — if not, this phase's DM repoint work is the first to need one

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|--------------------|
| Interactive crop frame (drag/resize/zoom) | IMAGE-01 | No automated DOM/canvas test exists in this codebase's stack; Cropper.js interaction is inherently visual | Open each of the 6 crop-enabled forms (Character Create/Edit, Contact Create/Edit, DM EditProfile — desktop+mobile), select a photo, confirm the modal opens with a draggable/resizable/zoomable square frame |
| EXIF orientation correction | Success Criterion #4 | Requires an actual phone-camera photo with EXIF orientation metadata; desktop test images and devtools emulation do not reproduce this | Upload a real portrait photo taken directly from a phone camera (not a pre-rotated sample image); confirm it saves right-side-up, not sideways/upside-down |
| iOS Safari canvas-memory ceiling | Success Criterion #4 | iOS Safari's 16.7M-pixel canvas ceiling is invisible in desktop Chrome or devtools "mobile" emulation | Upload a full-resolution (12MP+) photo from an iPhone camera on real iOS Safari; confirm the crop canvas does not crash or blank |
| Touch-drag/pinch crop precision | Success Criterion #4 | Synthesized compatibility touch events in devtools emulation mask real touchscreen behavior | On a real touchscreen device, drag and pinch-resize the crop frame; confirm smooth, precise response with no missed gestures |

**Blocker:** All four manual verifications above require real-device access, which per 46-CONTEXT.md's Pre-Execution Blocker is **not yet confirmed**. Do not schedule these as a plan's verification step until device access is confirmed with the user.

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
