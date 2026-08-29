---
phase: 81
slug: contact-tags-and-filtering
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-30
---

# Phase 81 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3.2.2 + FluentAssertions v8.10.0 + NSubstitute v5.3.0; EF Core InMemory v10.0.9 for integration tests |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` — serial execution (`parallelizeAssembly: false`, `parallelizeTestCollections: false`); required because tests share one in-memory database per factory |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~ContactsControllerIntegrationTests|FullyQualifiedName~ContactRepositoryTests|FullyQualifiedName~ContactServiceTests"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30 seconds quick / ~3 minutes full |

**Build note:** if `dotnet build` or `dotnet test` fails on locked output files, Visual Studio is running the app under the debugger — ask the user to stop it (Shift+F5) before retrying. Do not work around it.

---

## Sampling Rate

- **After every task commit:** Run the quick command above
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

Phase 81 has no `CONTACTTAG-*` requirement IDs in REQUIREMENTS.md (ROADMAP.md says `Requirements: TBD`). Requirement column therefore references the locked CONTEXT.md decisions, which CONTEXT.md names as the requirement source for this phase. If the planner mints a `CONTACTTAG-*` family as plan 01 (a Claude's-Discretion item), replace the Requirement column with those IDs and keep the Decision reference alongside.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | 1 | D-23 | T-81-01 | Group A's tags never appear in group B's index, filter list, or tag suggestions; a POST attaching a contact to a foreign tag id is refused, not silently accepted | integration | `dotnet test --filter "FullyQualifiedName~ContactsControllerIntegrationTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1 | D-05 | T-81-01 | `ContactTag` carries a fail-closed `HasQueryFilter` dereferencing `activeGroupContext` inline; a null active group returns zero tags | integration | same as above | ❌ W0 | ⬜ pending |
| TBD | TBD | 1 | D-04 | — | `(GroupId, Name)` unique index treats "Shopkeeper" and "shopkeeper" as the same row | integration | same as above | ❌ W0 | ⬜ pending |
| TBD | TBD | 1 | D-28 | — | Removing a tag from its last contact deletes the row — asserted against `QuestBoardContext`, not inferred from the UI; re-adding mints a fresh id | unit (repository) | `dotnet test --filter "FullyQualifiedName~ContactRepositoryTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 2 | D-24 | T-81-02 | A player receives zero tag surfaces (no chips, no filter, no tag markup) on both index views and both Details views; a DM-tier viewer receives all of them | integration | `dotnet test --filter "FullyQualifiedName~ContactsControllerIntegrationTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 2 | D-25 | T-81-03 | A tag filter cannot surface a contact `IsVisibleTo` excluded — including an unrevealed contact carrying the filtered tag with Show Hidden off | integration | same as above | ❌ W0 | ⬜ pending |
| TBD | TBD | 2 | D-27 | T-81-03 | A tag borne only by unrevealed contacts is absent from the filter list for a viewer who cannot see them, and present for a DM with Show Hidden on | integration | same as above | ❌ W0 | ⬜ pending |
| TBD | TBD | 2 | D-08 / D-26 | — | Two selected tags return the union, not the intersection; category headings survive an active filter and empty ones are suppressed | integration | same as above | ❌ W0 | ⬜ pending |
| TBD | TBD | 2 | D-06 | — | An unknown, deleted, or foreign tag id in the query string silently matches nothing — never 404, never a thrown error | integration | same as above | ❌ W0 | ⬜ pending |
| TBD | TBD | 2 | D-30 / D-13 | — | `ToggleShowHidden`'s redirect carries the selected tag ids rather than dropping the query string | integration | same as above | ❌ W0 | ⬜ pending |
| TBD | TBD | 3 | D-29 / D-16 | T-81-04 | POSTing the plain comma-separated tag value with no JS tags the contact correctly, dedupes case-insensitively, and reuses existing rows per D-04 | integration | same as above | ❌ W0 | ⬜ pending |
| TBD | TBD | 3 | D-22 | — | Mobile markup (chips, tag input, filter offcanvas) renders only under a real mobile User-Agent | integration | `dotnet test --filter "FullyQualifiedName~ContactsTagsMobile"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

**Planner must replace every `TBD` Task ID / Plan / Wave cell** with the real task identifiers once PLAN.md files exist. The Requirement, Secure Behavior, Test Type, and Command columns are already fixed by CONTEXT.md and should not be renegotiated.

---

## Wave 0 Requirements

- [ ] Cross-group tag isolation tests in `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` — pattern precedent at `ContactsControllerIntegrationTests.cs:488` (`Details_ContactInDifferentGroup_ReturnsNotFound`, seeding via `TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2)`). No new fixture needed.
- [ ] `TestDataHelper.CreateTestContactTagAsync(...)` — analogous to the existing `CreateTestContactAsync` / `CreateTestContactNoteAsync` at `TestDataHelper.cs:166-219`, so tests can seed tags directly instead of routing every case through the controller POST.
- [ ] A real-mobile-User-Agent test class for the tag chips, tag input, and filter offcanvas — copy the `MobileUserAgent` / `DesktopUserAgent` constants and the `request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent)` shape from `QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs:10-60`. No new test infrastructure — a new test class following that pattern.
- [ ] Repository-level orphan-prune assertion (D-28) in `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` — mirrors the existing file's shape; no new fixture.

Test framework is already installed and configured — Wave 0 is new test files and helpers only, not infrastructure setup.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Tagify chips render, accept typed input, suggest from the viewer-scoped vocabulary, and remove on backspace | D-14, D-15 | The CDN script does not execute in the integration-test host; automated tests can only assert the underlying `<input>`, its `data-` attributes, and the `<script>` tags with their SRI attributes | Run the app, open a contact's Edit page as a DM on desktop and on a real mobile device, type a new tag and pick an existing one from suggestions, save, and confirm both persist |
| Tagify's styling matches the app theme on both platforms | D-15 | Visual only | Compare the chip field against surrounding form controls on desktop and mobile, in the app's own theme |
| CDN-blocked degradation to a plain comma-separated input | D-16 | Requires blocking the CDN at the network level, which the test host cannot simulate | Block `cdn.jsdelivr.net` in devtools or hosts file, reload the Edit page, confirm the field is a usable plain text input and that saving still tags correctly |
| Mobile filter offcanvas opens, applies, and clears | D-20 | Bootstrap offcanvas behaviour is client-side; integration tests assert markup presence only | On a real mobile device (not devtools emulation — `MobileDetectionMiddleware` selects on User-Agent), open the Contacts index as a DM, open the filter drawer, tick two tags, apply, then clear |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] Every `TBD` Task ID / Plan / Wave cell replaced with real values from PLAN.md
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
