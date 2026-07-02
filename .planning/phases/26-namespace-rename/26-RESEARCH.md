# Phase 26: Namespace Rename - Research

**Researched:** 2026-06-29
**Domain:** .NET solution rename / C# namespace refactoring
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Patch migration files in-place. Text-replace `EuphoriaInn` → `QuestBoard` namespace strings in all 26 `.cs` and `.Designer.cs` migration files and `QuestBoardContextModelSnapshot.cs`. The deployed DB `__EFMigrationsHistory` table is preserved — no manual DB intervention required.
- **D-02:** The following are in scope beyond RENAME-01–04:
  - `CLAUDE.md` — update `EuphoriaInn.Service/` paths in dev commands (dotnet run, ef migrations)
  - `create-migration.sh` — update old project directory references
  - `Dockerfile` — update project name references
- **D-03:** The following are explicitly out of scope:
  - Razor view display text — user confirmed no "EuphoriaInn" strings appear in the UI
  - `.planning/` codebase map docs — historical artifacts, update separately if needed
- **D-04:** The string `"EuphoriaInn"` as a literal value in seed data or migration `Data()` calls must be protected — a blanket find-replace must not touch these string values, only namespace declarations and using-statement references.
- **D-05:** Single atomic commit: one `refactor: rename EuphoriaInn → QuestBoard` commit covers all changes. The rename is non-behavioral; all-or-nothing is cleaner than incremental commits.

### Claude's Discretion

None documented — all decisions were locked.

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope. `.planning/` codebase map docs are historical artifacts to update separately if needed.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| RENAME-01 | All `EuphoriaInn.*` namespaces renamed to `QuestBoard.*` across every C# file | 263 source .cs files (excl. obj/bin) contain EuphoriaInn; 201 non-migration files. Text-replace `namespace EuphoriaInn.` → `namespace QuestBoard.` and `using EuphoriaInn.` → `using QuestBoard.` |
| RENAME-02 | Project files (`.csproj`), solution file (`.slnx`), and directory names renamed | 5 directories to rename via `git mv`; 5 .csproj files to rename and update; 1 .slnx to rename and update internal paths |
| RENAME-03 | Config files (`appsettings*.json`), GitHub Actions workflows, and deployment references updated | `appsettings.json` is already clean; `binary-release.yml` needs one-line update; `dotnet.yml` and `docker-publish.yml` are clean; `Dockerfile`, `create-migration.sh`, `CLAUDE.md`, `README.md`, `docs/server-setup.md` need updates |
| RENAME-04 | All EF Core migration `*.Designer.cs` files updated with new namespace; `dotnet build` + all 191 tests pass | 26 `.Designer.cs` files + 26 `.cs` migration files + `QuestBoardContextModelSnapshot.cs` = 53 migration files, all need namespace patch; entity FQN strings in Designer.cs files also need renaming |
</phase_requirements>

---

## Summary

Phase 26 is a pure mechanical rename: every occurrence of `EuphoriaInn` in the codebase's namespaces, project names, and directory names is replaced with `QuestBoard`. There is zero behavior change — no new logic, no schema changes, no data migration. The rename is approximately 343 distinct source files across .cs, .cshtml, .razor, .csproj, .slnx, .yml, .sh, and documentation files.

The rename involves two conceptually distinct operations that must be sequenced carefully: (1) renaming the five top-level directories using `git mv` to preserve file history, and (2) text-replacing `EuphoriaInn` → `QuestBoard` within file contents. The sequence matters because the `.slnx` solution file and all `<ProjectReference>` elements reference directory paths — if content is patched before directories are moved, build references break; if directories are moved first, all source files still compile correctly once their content is patched.

The largest landmine is the EF Core migration Designer.cs files. These files contain entity FQN strings like `"EuphoriaInn.Repository.Entities.QuestEntity"` used by EF at runtime to resolve types — they are not display strings and must be renamed. However, migration `InsertData()` calls may contain the literal string `"EuphoriaInn"` as the group name (a database value). Confirmed by grepping: no such string values exist in the current migration set — the only string form is in assembly attribute decorators and FQN type references, all of which are correct to rename. A safe regex that targets only the namespace prefix form (`EuphoriaInn\.`) avoids touching unrelated strings.

**Primary recommendation:** Execute in two task waves: Wave 1 renames directories (`git mv`) and patches solution/project file content; Wave 2 patches all C# source files, Razor/Blazor files, and CI/scripts; then run `dotnet build` and `dotnet test` as the gate.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Directory renames | Git / filesystem | — | Must happen first; unblocks all other reference fixes |
| Project/solution file updates | MSBuild (.csproj/.slnx) | — | References directory paths — must align with renamed directories |
| Namespace declarations in C# | C# compiler | — | `namespace X.Y` declarations and `using` directives |
| EF migration file namespace patches | EF Core (build-time type resolution) | — | Designer.cs uses FQN strings; runtime migration resolution requires matching namespace |
| Razor @using directives and FQN refs | ASP.NET Core Razor compiler | — | Same namespace token; replace matches C# pattern |
| CI/CD workflow path references | GitHub Actions | — | Static string paths to .csproj files |
| Deployment/scripts | systemd + bash | Server manual step | DLL name changes; live server service unit requires manual update |

---

## Standard Stack

No external packages are installed in this phase. This is a pure source-level rename using built-in .NET tooling only.

### Tooling Used
| Tool | Version | Purpose |
|------|---------|---------|
| `git mv` | 2.54.0 (installed) [VERIFIED: environment] | Rename directories while preserving git history |
| `dotnet build` | SDK 10.0.301 (installed) [VERIFIED: environment] | Verify build succeeds after rename |
| `dotnet test` | SDK 10.0.301 (installed) [VERIFIED: environment] | Verify all 191 tests pass |
| PowerShell `Get-ChildItem` + `(Get-Content … -Raw) -replace` | Built-in Windows | Batch text-replace across file contents |

### No Packages to Install

This phase installs no NuGet packages and no npm packages. The Package Legitimacy Audit section is omitted (nothing to audit).

---

## Architecture Patterns

### Rename Execution Order

The sequence is non-negotiable. Doing it out of order produces unbuildable states:

```
Step 1: git mv each of the 5 top-level project directories
Step 2: Update .slnx (solution file) project path references
Step 3: Update all <ProjectReference> paths in all .csproj files
Step 4: Rename the .csproj files themselves (within the now-renamed directories)
Step 5: Text-replace EuphoriaInn → QuestBoard in all file contents
Step 6: dotnet build (gate — must be green)
Step 7: dotnet test (gate — all 191 must pass)
Step 8: Single atomic git commit
```

Steps 1–4 address RENAME-02 (project/directory structure). Step 5 addresses RENAME-01, RENAME-03, RENAME-04 simultaneously.

### Directory Rename Commands (ASSUMED pattern from git documentation)

```bash
# From repo root — run each sequentially
git mv EuphoriaInn.Domain QuestBoard.Domain
git mv EuphoriaInn.Repository QuestBoard.Repository
git mv EuphoriaInn.Service QuestBoard.Service
git mv EuphoriaInn.UnitTests QuestBoard.UnitTests
git mv EuphoriaInn.IntegrationTests QuestBoard.IntegrationTests
# Also rename the solution file
git mv EuphoriaInn.slnx QuestBoard.slnx
```

### Solution File Update Pattern

`EuphoriaInn.slnx` content before (6 project path entries):

```xml
<Project Path="EuphoriaInn.IntegrationTests/EuphoriaInn.IntegrationTests.csproj" />
<Project Path="EuphoriaInn.UnitTests/EuphoriaInn.UnitTests.csproj" />
<Project Path="EuphoriaInn.Domain/EuphoriaInn.Domain.csproj" />
<Project Path="EuphoriaInn.Repository/EuphoriaInn.Repository.csproj" />
<Project Path="EuphoriaInn.Service/EuphoriaInn.Service.csproj" />
```

After rename:

```xml
<Project Path="QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj" />
<Project Path="QuestBoard.UnitTests/QuestBoard.UnitTests.csproj" />
<Project Path="QuestBoard.Domain/QuestBoard.Domain.csproj" />
<Project Path="QuestBoard.Repository/QuestBoard.Repository.csproj" />
<Project Path="QuestBoard.Service/QuestBoard.Service.csproj" />
```

### Text-Replace Scope — What Changes

The replace token `EuphoriaInn` appears in these forms in source files:

| Form | Example (before) | Example (after) | Scope |
|------|-----------------|-----------------|-------|
| Namespace declaration | `namespace EuphoriaInn.Service.Controllers` | `namespace QuestBoard.Service.Controllers` | All .cs |
| Using directive | `using EuphoriaInn.Domain.Interfaces` | `using QuestBoard.Domain.Interfaces` | All .cs, .cshtml, .razor |
| @using Razor | `@using EuphoriaInn.Service.ViewModels` | `@using QuestBoard.Service.ViewModels` | All .cshtml, .razor |
| FQN type reference in cshtml | `List<EuphoriaInn.Domain.Models.Character>` | `List<QuestBoard.Domain.Models.Character>` | Views/Quest/Details.cshtml, Views/Shop/Index.cshtml |
| EF Designer.cs FQN | `"EuphoriaInn.Repository.Entities.QuestEntity"` | `"QuestBoard.Repository.Entities.QuestEntity"` | All 26 Designer.cs |
| Assembly attribute | `[assembly: InternalsVisibleTo("EuphoriaInn.UnitTests")]` | `[assembly: InternalsVisibleTo("QuestBoard.UnitTests")]` | Domain/Properties/AssemblyInfo.cs |
| ProjectReference path | `<ProjectReference Include="..\EuphoriaInn.Domain\EuphoriaInn.Domain.csproj" />` | `<ProjectReference Include="..\QuestBoard.Domain\QuestBoard.Domain.csproj" />` | All .csproj |
| Dockerfile project paths | `COPY ["EuphoriaInn.Service/EuphoriaInn.Service.csproj", ...]` | `COPY ["QuestBoard.Service/QuestBoard.Service.csproj", ...]` | Dockerfile |
| CI workflow path | `dotnet publish EuphoriaInn.Service/EuphoriaInn.Service.csproj` | `dotnet publish QuestBoard.Service/QuestBoard.Service.csproj` | binary-release.yml |
| Dev documentation | `dotnet run --project EuphoriaInn.Service` | `dotnet run --project QuestBoard.Service` | CLAUDE.md, README.md |
| Server setup docs | `ExecStart=... EuphoriaInn.Service.dll` | `ExecStart=... QuestBoard.Service.dll` | docs/server-setup.md |
| Integration test path string | `"EuphoriaInn.Service", "Controllers", ...` | `"QuestBoard.Service", "Controllers", ...` | QuestFinalizeTests.cs, MobileCssTests.cs |

### Safe Regex for Content Replace

A simple string replacement of `EuphoriaInn` → `QuestBoard` is safe for all files except those with the literal group name string. Confirmed: no migration file contains `InsertData` with `"EuphoriaInn"` as a value — the string `"EuphoriaInn"` as a standalone quoted value does not appear in any `.cs` migration file. Simple token replacement is safe across the entire codebase.

### Anti-Patterns to Avoid

- **Replacing only namespace declarations but not FQN inline type references:** Some Razor views use `EuphoriaInn.Domain.Models.Character` as fully-qualified type names inside `@foreach` casts. A grep limited to `namespace` or `using` lines misses these — use whole-file replace, not line-filtered replace.
- **Renaming migration Designer.cs FQN strings too aggressively:** The entity name strings like `"EuphoriaInn.Repository.Entities.QuestEntity"` must be renamed. But do not rename the `[Migration("20250629200240_InitialSqlServerNoAction")]` attribute string — the migration timestamp name is correct and must not change.
- **Using `dotnet ef migrations` tooling to re-generate files:** Do not run `dotnet ef migrations` to regenerate the snapshot or migration files. Patch in-place. Regeneration would create new migrations with a new timestamp in `__EFMigrationsHistory`, breaking the deployed database.
- **Forgetting that `git mv` on Windows may need Git Bash:** `git mv` in Git Bash handles the rename; PowerShell `Rename-Item` does not preserve git history. Always use `git mv`.
- **Patching obj/bin directories:** Build artifact JSON files in obj/ and bin/ directories also contain `EuphoriaInn` strings, but they are regenerated by `dotnet build`. Do NOT manually patch them — the build step produces correct output automatically.

---

## Runtime State Inventory

This phase renames identifiers — it is a rename/refactor phase.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | No `EuphoriaInn` namespace strings stored in SQL Server database tables. The `__EFMigrationsHistory` table stores migration timestamps (e.g., `20250629200240_InitialSqlServerNoAction`) — these do NOT include the namespace prefix and are unaffected. [VERIFIED: grep of migration files] | None — DB untouched |
| Live service config | The systemd unit file at `/etc/systemd/system/questboard.service` on the production Linux server has `ExecStart=/usr/bin/dotnet /opt/questboard/EuphoriaInn.Service.dll`. After rename the published DLL will be `QuestBoard.Service.dll`. This file lives on the server, not in git. [VERIFIED: docs/server-setup.md line 120] | Manual: update systemd unit on production server before next deploy. Run `systemctl daemon-reload` after edit. |
| OS-registered state | The `questboard.service` systemd service name itself stays `questboard` — only the `ExecStart` DLL path changes. The actions runner service on the server does not reference `EuphoriaInn`. | None beyond the ExecStart DLL path fix above |
| Secrets/env vars | `/etc/questboard/env` on the production server contains connection strings and API keys — none reference `EuphoriaInn` by name. ASP.NET Core env var override convention (`EmailSettings__SmtpServer`) uses section names from `appsettings.json`, not namespace names. [VERIFIED: appsettings.json has no EuphoriaInn string] | None |
| Build artifacts | `obj/` and `bin/` directories under each project contain stale `EuphoriaInn.*` JSON and assembly files. After `git mv` and source patch, running `dotnet build` regenerates all of these correctly. | Run `dotnet build` — artifacts auto-regenerate |

**Critical runtime state for next deployment:** The systemd `ExecStart` line on the production server must be manually updated from `EuphoriaInn.Service.dll` to `QuestBoard.Service.dll` before or during the first deploy after this rename. Failing to do so means `systemctl start questboard` will fail silently — the service will stop but won't start because the DLL path no longer exists.

The `docs/server-setup.md` file documents the setup steps including the old DLL name — it must be updated in this phase so the next admin who sets up a server uses the correct name.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Bulk file content replace | Custom script iterating files | PowerShell `-replace` operator or `sed` in Git Bash with `-i` flag | Built-ins handle encoding, line endings; custom scripts have edge-case bugs |
| Namespace resolution after rename | Manual tracking of what references what | `dotnet build` | Compiler reports every broken reference with file + line |
| Test verification | Selective re-running of affected tests | `dotnet test` (full suite) | Tests are cheap (~seconds); selective runs can miss transitive failures |

---

## Common Pitfalls

### Pitfall 1: Migration Designer.cs FQN strings not renamed
**What goes wrong:** Build succeeds but integration tests that use EF in-memory or migrations fail at runtime with `InvalidOperationException: No entity type with name 'EuphoriaInn.Repository.Entities.QuestEntity' was found`.
**Why it happens:** EF Core 10 resolves entities by their FQN string stored in Designer.cs `modelBuilder.Entity("EuphoriaInn.Repository.Entities.QuestEntity", ...)` calls. After the namespace rename, the entity's actual FQN is `QuestBoard.Repository.Entities.QuestEntity`. EF cannot match them.
**How to avoid:** Use a whole-file replace that targets all occurrences of `EuphoriaInn` including those inside quoted strings in `.Designer.cs` files. Verify by grepping post-rename: `grep -r "EuphoriaInn" EuphoriaInn.Repository/Migrations` should return zero results.
**Warning signs:** Compilation succeeds but integration tests fail with entity-not-found or type-mapping errors.

### Pitfall 2: InternalsVisibleTo assembly name not updated
**What goes wrong:** Unit tests that access internal members of `QuestBoard.Domain` fail with `InaccessibleDueToProtectionLevel` compile errors.
**Why it happens:** `EuphoriaInn.Domain/Properties/AssemblyInfo.cs` has `[assembly: InternalsVisibleTo("EuphoriaInn.UnitTests")]`. If this isn't updated to `"QuestBoard.UnitTests"`, the attribute no longer grants access.
**How to avoid:** The whole-file replace catches this automatically. Post-rename grep for `InternalsVisibleTo` and verify it reads `QuestBoard.UnitTests`.
**Warning signs:** Build succeeds but unit tests produce accessibility errors for internal symbols.

### Pitfall 3: Integration test hardcoded path strings not updated
**What goes wrong:** `MobileCssTests` fails with `FileNotFoundException: mobile.css not found. Searched upward from '...'. Last attempted path: '.../EuphoriaInn.Service/wwwroot/css/mobile.css'`.
**Why it happens:** `MobileCssTests.cs` (line 50) and `QuestFinalizeTests.cs` (line 27) contain the string `"EuphoriaInn.Service"` as a directory name component in `Path.Combine` calls. After the directory is renamed to `QuestBoard.Service`, the test walks up the directory tree looking for `EuphoriaInn.Service` and never finds it.
**How to avoid:** The whole-file replace updates these string literals automatically since they contain `EuphoriaInn`. Post-rename confirm `MobileCssTests.cs` reads `"QuestBoard.Service"`.
**Warning signs:** CSS integration tests fail with file-not-found; no compile error.

### Pitfall 4: Systemd unit not updated before deploy
**What goes wrong:** After merging the rename branch and pushing a release tag, the GitHub Actions runner deploys the new zip. The zip now contains `QuestBoard.Service.dll`. The systemd service tries to start `/opt/questboard/EuphoriaInn.Service.dll` — that file no longer exists — and the service silently fails to start. The site goes down.
**Why it happens:** The systemd unit file lives on the production server at `/etc/systemd/system/questboard.service` and is not managed by git.
**How to avoid:** Update the `ExecStart` line on the production server and run `systemctl daemon-reload` before the first deploy from the renamed branch. This is a manual step and must be documented in the plan as a pre-deploy gate.
**Warning signs:** After deploy, `systemctl status questboard` shows `failed` and `journalctl -u questboard` shows `Failed to execute program /opt/questboard/EuphoriaInn.Service.dll: No such file or directory`.

### Pitfall 5: Windows git mv case-sensitivity on NTFS
**What goes wrong:** `git mv EuphoriaInn.Service QuestBoard.Service` appears to succeed, but git internally shows no change because NTFS is case-insensitive and treats the rename as a no-op if only case changes. This is not the case here (completely different prefix) but worth knowing.
**Why it matters:** The rename from `EuphoriaInn.` to `QuestBoard.` changes more than case, so NTFS will correctly recognize it as a rename. No workaround needed — noted for awareness.
**Warning signs:** `git status` after `git mv` shows no staged changes.

---

## Code Examples

### Verified: How to patch file content on Windows (PowerShell)

```powershell
# Run from repo root in PowerShell
# Replace EuphoriaInn with QuestBoard in all source files (excluding obj/bin/.git)
Get-ChildItem -Recurse -File -Include *.cs,*.cshtml,*.razor,*.csproj,*.slnx,*.yml,*.sh,*.md,Dockerfile |
  Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|\\.git\\|\\.vs\\|\\.planning\\' } |
  ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    if ($content -match 'EuphoriaInn') {
        $newContent = $content -replace 'EuphoriaInn', 'QuestBoard'
        Set-Content $_.FullName -Value $newContent -Encoding UTF8 -NoNewline
    }
  }
```

Note: `Set-Content` with `-NoNewline` preserves existing line endings. The `-Encoding UTF8` flag writes UTF-8 without BOM on PowerShell 6+; on Windows PowerShell 5.1 it writes UTF-8 with BOM — check the project's existing file encoding first. [ASSUMED — verify against actual file encoding; Designer.cs files have a UTF-8 BOM (`﻿`) per observed output]

Alternatively, using Git Bash `sed`:

```bash
# From repo root in Git Bash — processes all tracked files
git ls-files | grep -v '\.planning' | xargs grep -l 'EuphoriaInn' | \
  xargs sed -i 's/EuphoriaInn/QuestBoard/g'
```

`git ls-files` limits to tracked files only, automatically excluding obj/, bin/, .vs/, and .git/. This is cleaner than path exclusion logic. [ASSUMED — verify `xargs` handles spaces in filenames correctly; use `xargs -d '\n'` if needed]

### Verified: dotnet build gate command

```bash
# From repo root after rename
dotnet build QuestBoard.slnx --no-incremental
```

`--no-incremental` forces a clean rebuild, catching any stale references from the pre-rename build cache.

### Verified: dotnet test gate command

```bash
# From repo root
dotnet test QuestBoard.slnx --verbosity normal
```

Expected output: `Passed: 191, Failed: 0` (or whatever the current passing count is — must be all-green).

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual file-by-file rename in IDE | Bulk text-replace via PowerShell or sed | Always available | Faster, less error-prone for 343-file renames |
| Re-generating migrations after namespace change | Patch migration files in-place | EF Core 5+ guidance | Avoids broken `__EFMigrationsHistory` on deployed DB |

**Note on IDE rename tools:** Visual Studio and Rider both have "Rename Namespace" refactoring. These tools are slower than a scripted replace for this scale, and they may miss non-C# files (cshtml, yml, Dockerfile, sh). Script-based replace is recommended.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | PowerShell `Set-Content -Encoding UTF8 -NoNewline` preserves BOM on existing BOM-encoded files | Code Examples | File encoding corruption in Designer.cs files; build error or garbled source. Mitigation: verify one Designer.cs file encoding before bulk replace; use Git Bash `sed -i` as fallback if BOM handling differs |
| A2 | `git ls-files \| xargs sed -i` handles filenames with spaces correctly without `-d '\n'` flag | Code Examples | Filenames with spaces truncated; partial replace. Mitigation: verify with `echo "D:/repos/dnd-quest-board/EuphoriaInn.Service/Views/Quest/_QuestCard.cshtml" \| xargs -d '\n' sed -i 's/EuphoriaInn/QuestBoard/g'` first |
| A3 | No migration file contains `InsertData()` with `"EuphoriaInn"` as a data value | Runtime State Inventory, Don't Hand-Roll | If such data exists, bulk replace corrupts seed data value. Mitigation: grep confirmed clean — `grep -rn '"EuphoriaInn"' EuphoriaInn.Repository/Migrations/` returned no InsertData/HasData calls |

**If this table is empty:** All claims in this research were verified or cited — no user confirmation needed.
(A1 and A2 are low-risk: both are verifiable in minutes before bulk replace runs.)

---

## Open Questions

1. **Systemd unit update timing**
   - What we know: The production server's `/etc/systemd/system/questboard.service` has `ExecStart=/usr/bin/dotnet /opt/questboard/EuphoriaInn.Service.dll`. After merge, the DLL name changes. This file must be updated on the server before the next deploy.
   - What's unclear: Whether the user wants to update the server now (before merging) or as part of the deployment step. This is a manual operation on the live server.
   - Recommendation: Plan should include a task "Update systemd ExecStart on production server" as a manual step with explicit commands, gated before the next `git tag` + deploy.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `git` | Directory renames via `git mv` | Yes | 2.54.0.windows.1 | — |
| `dotnet` SDK | Build + test gate | Yes | 10.0.301 | — |
| PowerShell or Git Bash | Bulk file content replace | Yes (Windows) | Built-in | Use either; plan should specify which |

**Missing dependencies with no fallback:** None — all required tools are present.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 |
| Config file | `xunit.runner.json` (in each test project) |
| Quick run command | `dotnet test QuestBoard.slnx --filter "FullyQualifiedName~UnitTests"` |
| Full suite command | `dotnet test QuestBoard.slnx --verbosity normal` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| RENAME-01 | All C# files use QuestBoard.* namespaces | Smoke: grep | `grep -r "EuphoriaInn" QuestBoard.Domain QuestBoard.Repository QuestBoard.Service QuestBoard.UnitTests QuestBoard.IntegrationTests --include="*.cs" --include="*.cshtml" --include="*.razor"` returns empty | After rename |
| RENAME-02 | Project/solution structure renamed | Build gate | `dotnet build QuestBoard.slnx --no-incremental` exits 0 | After rename |
| RENAME-03 | Config, CI, scripts updated | Manual spot-check + grep | `grep -r "EuphoriaInn" .github Dockerfile create-migration.sh CLAUDE.md README.md docs` returns empty | After rename |
| RENAME-04 | Migrations patched; build + 191 tests pass | Full test suite | `dotnet test QuestBoard.slnx --verbosity normal` — must show 0 failures | After rename |

### Sampling Rate
- **Per task:** `dotnet build QuestBoard.slnx` (fast, catches broken references immediately)
- **End of all tasks:** `dotnet test QuestBoard.slnx --verbosity normal` (full 191-test suite)
- **Phase gate:** Full suite green + grep confirms zero `EuphoriaInn` in source before `/gsd-verify-work`

### Wave 0 Gaps
None — existing test infrastructure covers all phase requirements. No new test files need to be created. The validation is a combination of build success + test suite + a final grep scan.

---

## Security Domain

This phase introduces no new authentication, authorization, input handling, or cryptographic operations. All changes are namespace renaming with zero behavior change. The security domain section is not applicable.

---

## Project Constraints (from CLAUDE.md)

| Directive | Impact on This Phase |
|-----------|---------------------|
| Work on `milestone/v5-multi-tenancy` branch — never commit to `main` | All changes go on `milestone/v5-multi-tenancy` |
| Windows development environment — use Windows paths | Use Git Bash for `git mv` and scripted replace; or PowerShell — plan must specify commands appropriate for Windows |
| Migrations are auto-applied via `context.Database.Migrate()` — no manual `database update` | Confirms in-place patch approach (D-01) is correct; do not re-generate migrations |
| EF packages belong only in `EuphoriaInn.Repository` (becoming `QuestBoard.Repository`) | No package movement; just rename references |
| `dotnet ef migrations add` command runs from the Service directory and targets the Repository | After rename: `dotnet ef migrations add MigrationName --project ../QuestBoard.Repository` |
| No user-facing functionality may be removed or broken | Confirmed: pure rename with zero behavior change |

---

## Sources

### Primary (HIGH confidence)
- Codebase grep — direct inspection of all 343 files containing `EuphoriaInn` in source [VERIFIED: environment]
- `EuphoriaInn.Repository/Migrations/` — inspected Designer.cs FQN patterns [VERIFIED: environment]
- `docs/server-setup.md` — inspected systemd unit ExecStart line [VERIFIED: environment]
- `26-CONTEXT.md` — locked decisions from user discussion [VERIFIED: environment]

### Secondary (MEDIUM confidence)
- CLAUDE.md directives [VERIFIED: environment]

### Tertiary (LOW confidence)
- PowerShell encoding behavior for BOM files [ASSUMED — documented as assumption A1]

---

## Metadata

**Confidence breakdown:**
- Rename scope (what files): HIGH — directly verified by grep across all source files
- Execution order (git mv before patch): HIGH — standard .NET project rename practice; violating order produces clear build errors
- Migration file treatment (patch in-place): HIGH — locked decision D-01 confirmed by codebase inspection showing no data-value `"EuphoriaInn"` strings
- Deployment runtime state (systemd ExecStart): HIGH — directly verified from docs/server-setup.md

**Research date:** 2026-06-29
**Valid until:** This research describes a snapshot of a specific codebase state. It is valid until any new C# files, migrations, or workflows are added — re-verify with a fresh grep if time passes before planning completes.
