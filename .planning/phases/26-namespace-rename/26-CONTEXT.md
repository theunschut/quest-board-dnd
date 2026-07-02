# Phase 26: Namespace Rename - Context

**Gathered:** 2026-06-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Rename all `EuphoriaInn.*` identifiers to `QuestBoard.*` across C# namespaces, project files, directory names, config, CI, and developer scripts — with zero behavior change and all 191 tests passing. This is a pure mechanical rename.

**Explicitly NOT renamed:** the string `"EuphoriaInn"` as the group name in seed data and the database. The UI has no visible "EuphoriaInn" display strings — only namespaces are affected.

</domain>

<decisions>
## Implementation Decisions

### Migration File Handling
- **D-01:** Patch migration files in-place. Text-replace `EuphoriaInn` → `QuestBoard` namespace strings in all 26 `.cs` and `.Designer.cs` migration files and the `QuestBoardContext.ModelSnapshot.cs` file. The deployed DB `__EFMigrationsHistory` table is preserved — no manual DB intervention required.

### Rename Scope
- **D-02:** The following are in scope beyond RENAME-01–04:
  - `CLAUDE.md` — update `EuphoriaInn.Service/` paths in dev commands (dotnet run, ef migrations)
  - `create-migration.sh` — update old project directory references
  - `Dockerfile` — update project name references
- **D-03:** The following are explicitly out of scope:
  - Razor view display text — user confirmed no "EuphoriaInn" strings appear in the UI
  - `.planning/` codebase map docs — historical artifacts, update separately if needed
- **D-04:** The string `"EuphoriaInn"` as a literal value in seed data or migration `Data()` calls must be protected — a blanket find-replace must not touch these string values, only namespace declarations and using-statement references.

### Commit Strategy
- **D-05:** Single atomic commit: one `refactor: rename EuphoriaInn → QuestBoard` commit covers all changes (C# namespaces, project files, directory renames, config, CI, scripts). The rename is non-behavioral; all-or-nothing is cleaner than incremental commits.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Scope
- `.planning/REQUIREMENTS.md` §Rename — RENAME-01 through RENAME-04: exact requirements and acceptance criteria
- `.planning/ROADMAP.md` §Phase 26 — phase goal and success criteria (build passes, 191 tests pass)
- `.planning/PROJECT.md` §Current Milestone — constraint: "EuphoriaInn group name in data is unchanged"

### Files to Rename (entry points for the planner)
- `EuphoriaInn.slnx` — solution file (rename to `QuestBoard.slnx`, update project paths inside)
- `EuphoriaInn.Service/EuphoriaInn.Service.csproj` — web app entry point
- `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`
- `EuphoriaInn.Repository/EuphoriaInn.Repository.csproj`
- `EuphoriaInn.UnitTests/EuphoriaInn.UnitTests.csproj`
- `EuphoriaInn.IntegrationTests/EuphoriaInn.IntegrationTests.csproj`

### CI / Scripts (must update — references hardcoded project paths)
- `.github/workflows/binary-release.yml` — hardcodes `EuphoriaInn.Service/EuphoriaInn.Service.csproj` in the publish step
- `.github/workflows/dotnet.yml` — no EuphoriaInn references, likely no change needed (verify)
- `.github/workflows/docker-publish.yml` — verify for project path references
- `create-migration.sh` — references old project directory names
- `Dockerfile` — references EuphoriaInn project name
- `CLAUDE.md` — dev commands reference `EuphoriaInn.Service/` paths

### Migration Files (patch in-place)
- `EuphoriaInn.Repository/Migrations/` — 26 files (13 × `.cs` + `.Designer.cs`) with embedded namespace strings
- `EuphoriaInn.Repository/Migrations/QuestBoardContext.ModelSnapshot.cs` — model snapshot, also needs namespace patching

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- No reusable code assets — this phase is pure rename, no new logic introduced

### Established Patterns
- **Namespace convention:** `EuphoriaInn.{Layer}.{Subfolder}` → becomes `QuestBoard.{Layer}.{Subfolder}` — inner subfolder names (e.g., `Models.QuestBoard`, `Controllers.QuestBoard`) are unchanged; only the root prefix changes
- **ProjectReference paths in .csproj:** use relative `../{ProjectDir}/{ProjectDir}.csproj` form — all 5 will need both the directory path AND the filename updated
- **`git mv` for directories:** use `git mv EuphoriaInn.Domain QuestBoard.Domain` etc. to preserve file history across the directory rename (5 top-level directories)

### Integration Points
- `EuphoriaInn.slnx` references all 5 project paths — must be updated when directories are renamed
- All `<ProjectReference>` elements across .csproj files reference sibling directory names — will break until directory rename is complete
- `binary-release.yml` publish step: `dotnet publish EuphoriaInn.Service/EuphoriaInn.Service.csproj` → path must match new directory structure

### Known Landmines
- Migration Designer.cs files contain type-qualified names like `EuphoriaInn.Repository.Entities.QuestBoardContext` — these are namespace-qualified, not display strings, so replacing `EuphoriaInn.Repository` → `QuestBoard.Repository` is correct
- Migration seed data calls like `migrationBuilder.InsertData(... "EuphoriaInn" ...)` (if any) must NOT be renamed — the string value is the group name, not a namespace
- `WebApplicationFactory<Program>` in IntegrationTests references the Service project's `Program` class — the namespace change must propagate consistently or integration test build will break

</code_context>

<specifics>
## Specific Ideas

- The rename is entirely mechanical — no architectural decisions needed beyond the ones above
- Planner should verify `dotnet.yml` and `docker-publish.yml` for EuphoriaInn references before assuming they're clean

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 26-Namespace-Rename*
*Context gathered: 2026-06-29*
