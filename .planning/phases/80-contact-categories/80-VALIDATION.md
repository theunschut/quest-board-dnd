---
phase: 80
slug: contact-categories
status: ready
nyquist_compliant: true
wave_0_complete: false
created: 2026-08-30
---

# Phase 80 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `80-RESEARCH.md` § Validation Architecture.
>
> **Requirement IDs do not exist yet.** `.planning/REQUIREMENTS.md` carries no `CONTACTCAT-*`
> family and ROADMAP.md says `Requirements: TBD`. Rows below are therefore seeded by the
> **CONTEXT.md decision ID** (`D-04`, `D-17`, …) that each behaviour comes from. The first plan
> mints the `CONTACTCAT-*` family (Phase 82's `82-01-PLAN.md` is the structural precedent);
> once it does, re-key the Requirement column to those IDs and fill in the Task IDs.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5) — both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests` |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json`; host wiring in `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests` |
| **Scoped run command** | `dotnet test --filter "FullyQualifiedName~Contact"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | quick / scoped / full: to be measured on first run |

**Build note:** if `dotnet build` or `dotnet test` fails on locked output files, Visual Studio is
holding the binaries under the debugger — ask the user to stop it (Shift+F5) before retrying.
This is a build-environment failure, never a red test.

---

## Sampling Rate

- **After every task commit:** `dotnet test --filter "FullyQualifiedName~Contact"`
- **After every plan wave:** `dotnet test`
- **Before `/gsd-verify-work`:** full suite green **plus** the real-device check below, which the
  automated suite cannot satisfy
- **Max feedback latency:** scoped suite must stay under ~60s; split it rather than sample less often

---

## Per-Task Verification Map

Task IDs are assigned at planning. Rows are seeded by decision so no locked decision can be
silently dropped from a plan.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | 1 | D-17 (cross-group isolation) | Info Disclosure — the Phase 49/55 leak class | Group A's categories appear on no group-B index and in no group-B dropdown; a POST naming a foreign `CategoryId` is refused, not silently accepted | integration | `dotnet test --filter "FullyQualifiedName~ContactCategory_CrossGroup"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1 | D-02 (fail-closed filter) | Info Disclosure | A null `ActiveGroupId` returns **zero** categories, never every board's merged | integration | `dotnet test --filter "FullyQualifiedName~ContactCategory_CrossGroup"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 2 | D-13 / D-18 (empty-heading suppression) | Info Disclosure — a heading is itself a campaign spoiler | Heading absent for a player whose contacts under it are all unrevealed; the same heading **present** for a DM with Show Hidden on | integration | `dotnet test --filter "FullyQualifiedName~ContactCategory_EmptyHeadingSuppression"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 2 | D-19 (ordering + Ungrouped) | — | Categories render in `SortOrder`, contacts alphabetical within, Ungrouped pinned last, zero-category board renders today's flat list unchanged (D-10) | integration | `dotnet test --filter "FullyQualifiedName~ContactsIndex_CategoryOrdering"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1 | D-20 (delete orphans) | — | Deleting a non-empty category leaves its contacts alive with `CategoryId = null`; asserted by reading the DB directly, never inferred from the UI | integration | `dotnet test --filter "FullyQualifiedName~ContactCategory_DeleteOrphans"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1 | D-04 (unique name, case-insensitive) | — | A duplicate name differing only in case surfaces as a `ModelState` validation message, not a raw `DbUpdateException` 500 | integration | `dotnet test --filter "FullyQualifiedName~ContactCategory_DuplicateName"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 3 | D-08 (mobile management page) | — | `Manage.Mobile.cshtml` is actually selected and renders, proven with a real mobile `User-Agent` | integration (render) | `dotnet test --filter "FullyQualifiedName~ContactCategoryMobileRender"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 3 | D-09 / D-10 on mobile | — | Grouped headings and the Ungrouped block render on `Index.Mobile.cshtml`, same real-UA mechanism | integration (render) | `dotnet test --filter "FullyQualifiedName~ContactCategoryMobileRender"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1 | D-05 (DM-tier gate) | Elevation of Privilege | Every category write returns a redirect/403 for a plain player; `DungeonMasterOnly` sits at class level on the management controller | integration | `dotnet test --filter "FullyQualifiedName~ContactCategoryManagement"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1 | D-15 (plain-text rendering) | Stored XSS | A category name containing markup is HTML-escaped in the heading and never routed through `IMarkdownService`; length-capped at 60 | integration | `dotnet test --filter "FullyQualifiedName~ContactCategory_DuplicateName"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1 | D-01 / D-03 (model shape) | — | Nullable `CategoryId` + optional navigation; `SetNull` delete behaviour and the `NoAction` Group FK are what the migration actually emits | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~ContactCategoryRepository"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1 | D-16 (category on Details) | — | Category name reaches both Details views; the field must first be **added** to `Contact`, `ContactEntity` and `ContactViewModel` — see the conflict note below | unit + integration | `dotnet test --filter "FullyQualifiedName~ContactService"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `TestDataHelper.CreateTestContactCategoryAsync` — the seeding helper every test row above depends on. Build this first; nothing else can be written without it.
- [ ] New file `QuestBoard.IntegrationTests/Controllers/ContactCategoryManagementControllerIntegrationTests.cs` — D-04, D-05, D-06, D-08. CRUD, the authorization gate, reorder, and the mobile render.
- [ ] New `[Fact]` methods in `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` — D-13, D-14, D-16, D-18, D-19. Grouping and the visibility interplay on the existing Index/Details actions.
- [ ] Cross-group isolation and delete-orphan coverage — D-17 and D-20. Follow the two-group setup already used by `TenantIsolationTests.cs` / `ContactsControllerIntegrationTests.cs`.
- [ ] `QuestBoard.UnitTests/Repository/ContactCategoryRepositoryTests.cs` and `QuestBoard.UnitTests/Services/ContactCategoryServiceTests.cs` — new pair mirroring the existing `ContactServiceTests.cs` structure.
- [ ] Extend `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` for the `.Include(Category)` path.
- Framework install: **none** — xUnit v3 is already present and configured.

**Mobile-render mechanism (reuse verbatim, do not reinvent):** `AgendaMobileRenderTests.GetMobileAsync`
from Phase 82 overrides the request `User-Agent` header. That is the only thing that selects a
`.Mobile.cshtml` view. Devtools emulation does not select mobile views at all, so any check that
relies on it proves nothing — this repo has already shipped mobile markup that was never rendered.

**`IgnoreQueryFilters()` scope note:** D-17 forbids it on every *application* path. D-20's
delete-orphan assertion needs it to read the orphaned rows back from the DB. That is test
infrastructure, not an application path, and the two are not in conflict. Keep it out of every
controller, service, and repository method in this phase.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Grouped index and the Manage Categories page are legible and usable on a real handset | D-08, D-09 | The integration suite proves the mobile view is *selected and renders*; it cannot judge layout, tap targets, or whether a long category name breaks the heading. Phase 77 was reopened for exactly this gap. | On a real phone (not devtools): open Contacts, confirm headings render and are readable; open Manage Categories from the index button; add, rename, reorder and delete a category; confirm the up/down buttons are tappable and the delete confirmation names the contact count. |
| First-run discovery path | D-07 | Depends on a board that genuinely has zero categories, and on whether the disabled-dropdown hint actually reads as an invitation. | On a board with no categories: open Contacts → Create. Confirm the category select is disabled with helper text linking to Manage Categories, and that the index shows **no** headings at all until the first category exists. |

---

## Conflict Carried Forward From Research

`80-CONTEXT.md` D-16 states the category field "is already on the mapped `ContactViewModel`".
**It is not.** `Contact`, `ContactEntity`, and `ContactViewModel` have no category-shaped field
today. The planner must scope the field additions across all three layers plus both AutoMapper
profiles; D-16 cannot be treated as a view-only change.

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s on the scoped suite
- [ ] Requirement column re-keyed from `D-*` to `CONTACTCAT-*` once the minting plan lands
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
