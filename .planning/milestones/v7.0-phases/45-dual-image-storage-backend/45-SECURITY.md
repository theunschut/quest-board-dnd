---
phase: 45
slug: dual-image-storage-backend
status: verified
threats_open: 0
asvs_level: 1
created: 2026-07-07
---

# Phase 45 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| migration → production database | A data-destroying scaffolded migration would silently delete every existing photo across three tables | Existing `CharacterImages`/`DungeonMasterProfileImages`/`ContactImages` byte data |
| client (browser) → upload controller action | Untrusted file upload crosses here; client-supplied Content-Type/extension/size must be re-validated server-side | Uploaded photo bytes, MIME type, filename |
| controller Edit POST → service new-upload signal | The "a new original was uploaded this request" fact is only knowable at the controller boundary; the Edit POST must pass it explicitly, not let the service infer it from never-null round-tripped model bytes | `hasNewOriginalUpload` boolean |
| Service edit path → image columns | An edit to an unrelated field must not silently mutate stored image data | `OriginalImageData`/`CroppedImageData` byte arrays |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-45-01 | Tampering / Denial of Service | `RenameImageColumnsAddCropped` migration | mitigate | Rename via `RenameColumn` (not scaffolded `DropColumn`+`AddColumn`) — confirmed zero `DropColumn` calls in `Up()`. Human dry-run against the real dev database verified all 21 existing photos byte-for-byte identical (SHA-256 hash match) before/after migration. | closed |
| T-45-02 | Tampering | `CharacterService`/`ContactService`/`DungeonMasterProfileService` update paths | mitigate | Explicit `hasNewOriginalUpload` signal threaded from controller; service fetches-and-preserves the stored crop on no new upload, clears it on a genuine new upload. Verified: `CharacterServiceTests`/`ContactServiceTests`/`DungeonMasterProfileServiceTests` assert both directions (Pitfall 4 preserve, Pitfall 5 clear) and pass. | closed |
| T-45-03 | Information Disclosure | Cropped/original-read query projections (`CharacterRepository`, `ContactRepository`, `DungeonMasterProfileRepository`) | mitigate | Verified directly in source: `GetCharacterOriginalPictureAsync`/`GetCharacterCroppedPictureAsync` and the Contact equivalents are rooted at the group-filtered `Characters`/`Contacts` DbSet (not `CharacterImages`/`ContactImages` directly), so a cross-group id returns null. DM read matches its existing no-group-filter shape — no new access-control surface introduced. | closed |
| T-45-04 | Tampering | Upload actions (`CharactersController`, `ContactsController`, `DungeonMasterController` — 5 call sites) | mitigate | `IImageValidationService.ValidateImagePair` — MIME allowlist (`image/jpeg`, `image/png`, `image/gif`) + extension allowlist + 5 MB size limit, applied to both original and (when present) cropped file. Verified all 5 upload call sites invoke it (grep-confirmed). | closed |
| T-45-05 | Denial of Service | Oversized upload → large `byte[]` allocation | mitigate | 5 MB size limit (`MaxFileSizeBytes`) enforced server-side in the shared validator, confirmed present in `ImageValidationService.cs`. | closed |
| T-45-06 | Tampering (residual) | Client-forged `Content-Type` header on a non-image byte payload | accept | Magic-byte upload-side validation intentionally out of scope (small trusted-group internal tool; not part of D-03's deduplication scope). Residual risk is bounded: the existing `DetectImageMimeType` magic-byte check on the *serving* path (confirmed present in `CharactersController`/`ContactsController`) already forces an `image/*` response Content-Type regardless of what was actually uploaded, defending against content-sniffing-based stored XSS. | closed |
| T-45-07 | Tampering (integrity) | `CharactersController.Edit` / `ContactsController.Edit` → service | mitigate | Edit POST actions thread the real `hasNewOriginalUpload` signal into the widened `UpdateAsync` overload, so a new-original re-upload actually clears a stale crop through the real HTTP path — not just in an isolated unit test. Independently grep-verified (`hasNewOriginalUpload` present in both controllers, sole 3-arg `UpdateAsync` call at each Edit action) and covered by controller-level integration tests (`ClearsStaleCropped`/`PreservesStoredCroppedImage`). This exact wiring gap was caught missing by the plan-checker twice during planning before execution began. | closed |
| T-45-SC | Tampering (supply chain) | NuGet package installs | accept | No new packages added this phase (IMAGE-02 / success criterion #3) — Package Legitimacy Gate not applicable. Confirmed via repo-wide grep: zero references to SkiaSharp/ImageSharp/Magick.NET in any `.csproj`. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-45-01 | T-45-06 | Server-side magic-byte validation on *upload* (as opposed to the existing magic-byte check on *serving*) was flagged by project-level research as a possible hardening step, but explicitly scoped out — D-03 is about deduplicating existing MIME/size validation, not adding a new validation layer, and this is a small trusted-group internal tool (per PROJECT.md's stated risk tolerance). The existing serving-side `DetectImageMimeType` check already neutralizes the most likely exploit path (stored XSS via content-sniffing). | Theun Schut (project owner, via RESEARCH.md scope note + 45-03-PLAN.md T-45-06 disposition) | 2026-07-07 |
| AR-45-02 | T-45-SC | No new NuGet packages introduced this phase — the Package Legitimacy Gate has nothing to audit. | Theun Schut (project owner, via phase plans) | 2026-07-07 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-07-07 | 8 | 8 | 0 | Orchestrator (direct source verification — short-circuited per register_authored_at_plan_time=true, threats_open=0; no auditor agent spawn needed) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-07-07
