---
phase: 45
slug: dual-image-storage-backend
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-07
---

# Phase 45 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (3.2.2) + FluentAssertions 8.10.0 |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Image"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~10s quick / ~60s full suite |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Image"`
- **After every plan wave:** Run `dotnet test` (full suite, both Unit and Integration test projects)
- **Before `/gsd:verify-work`:** Full suite must be green, plus a manual `dotnet ef database update` dry run confirming the hand-edited `RenameColumn` migration applies cleanly against a database with existing rows in all three tables (not automatable — the InMemory test provider builds schema from the current model rather than executing real migrations)
- **Max feedback latency:** ~60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 45-01-XX | 01 | 0 | IMAGE-03 | — | Uploading a photo persists `OriginalImageData` unmodified | unit | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests.UpdateProfileImageAsync_SetsOriginalImageData` | ❌ W0 | ⬜ pending |
| 45-01-XX | 01 | 0 | IMAGE-03 | — | Row with no cropped upload yet falls back to original on read | unit | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests.GetCharacterCroppedPictureAsync_FallsBackToOriginal_WhenCroppedIsNull` | ❌ W0 | ⬜ pending |
| 45-01-XX | 01 | 0 | Success Criterion #2 | — | Re-upload replaces both columns atomically, no partial-update state | unit | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests.UpdateProfileImageAsync_ReplacesBothColumnsAtomically` | ❌ W0 | ⬜ pending |
| 45-01-XX | 01 | 0 | Success Criterion #2 | — | Editing an unrelated field (e.g., Level) does not null out `CroppedImageData` | unit | `dotnet test --filter FullyQualifiedName~CharacterServiceTests.UpdateAsync_UnrelatedFieldEdit_PreservesExistingImages` | ❌ W0 | ⬜ pending |
| 45-01-XX | 01 | 0 | Success Criterion #4 | — | `GetXOriginalPictureAsync` and `GetXCroppedPictureAsync` return distinct values when both are set | unit | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests.GetCharacterOriginalAndCroppedPictureAsync_ReturnDistinctValues` | ❌ W0 | ⬜ pending |
| 45-01-XX | 01 | 0 | D-03 | V5 Input Validation | `IImageValidationService` rejects oversized/wrong-MIME/wrong-extension files consistently | unit | `dotnet test --filter FullyQualifiedName~ImageValidationServiceTests` | ❌ W0 | ⬜ pending |
| 45-01-XX | 01 | 0 | Research Pitfall 5 | — | Re-uploading a new original with no new crop clears the stale `CroppedImageData` | unit | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests.UpdateProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped` | ❌ W0 | ⬜ pending |
| 45-01-XX | 01 | 0 | IMAGE-02 | — | No server-side image-processing library referenced anywhere in the solution | manual/static | grep `*.csproj` for SkiaSharp/ImageSharp/Magick.NET — none should exist | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Extend `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` — widened `UpdateProfileImageAsync`, `GetCharacterOriginalPictureAsync`, `GetCharacterCroppedPictureAsync`
- [ ] Extend `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` — same, for Contact
- [ ] New `QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs` — confirm during planning whether this file already exists; create it if absent
- [ ] New `QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs` — MIME allowlist, extension allowlist, size limit, pair-validation with one/both/neither file present
- [ ] Manual/staging dry-run verification of the hand-edited `RenameColumn` migration — not automatable via the InMemory provider, must be an explicit plan task

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Hand-edited `RenameColumn` migration preserves existing photo data | Success Criterion #2 | InMemory test provider builds schema from the current model rather than executing real migration SQL — it cannot catch a scaffolder-generated `DropColumn`+`AddColumn` regression | Run `dotnet ef database update` (or apply via app startup) against a database copy containing existing character/DM/contact photos; confirm row counts and byte-for-byte content in `OriginalImageData` match pre-migration `ImageData` for a sample of rows |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
