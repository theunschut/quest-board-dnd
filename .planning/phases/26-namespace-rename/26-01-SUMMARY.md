---
phase: 26-namespace-rename
plan: "01"
subsystem: project-structure
tags: [rename, namespace, dotnet, git-mv, mechanical]
status: complete

dependency_graph:
  requires: []
  provides:
    - QuestBoard.* directory structure (5 project directories)
    - QuestBoard.slnx solution file
    - QuestBoard.*.csproj project files (5)
    - All source/config/CI/docs files with QuestBoard namespace tokens
    - dotnet build green gate
  affects:
    - All 5 project directories
    - EF migration files (53)
    - CI workflows, Dockerfile, docs

tech_stack:
  added: []
  patterns:
    - git mv for history-preserving directory rename
    - git ls-files + sed -i for tracked-files-only bulk token replacement

key_files:
  created: []
  modified:
    - QuestBoard.slnx (was EuphoriaInn.slnx)
    - QuestBoard.Domain/QuestBoard.Domain.csproj (was EuphoriaInn.Domain.csproj)
    - QuestBoard.Repository/QuestBoard.Repository.csproj (was EuphoriaInn.Repository.csproj)
    - QuestBoard.Service/QuestBoard.Service.csproj (was EuphoriaInn.Service.csproj)
    - QuestBoard.UnitTests/QuestBoard.UnitTests.csproj (was EuphoriaInn.UnitTests.csproj)
    - QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj (was EuphoriaInn.IntegrationTests.csproj)
    - QuestBoard.Domain/Properties/AssemblyInfo.cs (InternalsVisibleTo updated)
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs (53 migration files patched)
    - Dockerfile (COPY/ENTRYPOINT paths)
    - .github/workflows/binary-release.yml (publish path)
    - CLAUDE.md (dev command paths)
    - README.md (run command)
    - docs/server-setup.md (ExecStart DLL name)

decisions:
  - "D-01: Patch migration files in-place (text-replace) — no regeneration, deployed DB __EFMigrationsHistory preserved"
  - "D-05: No commit in plan 01 — single atomic commit owned by plan 02 after full test gate"

metrics:
  duration_minutes: 7
  completed_date: "2026-06-29"
  tasks_completed: 3
  tasks_total: 3
  files_renamed: 416
  files_content_patched: 343
  build_result: "succeeded: 0 errors, 23 warnings (all pre-existing)"
---

# Phase 26 Plan 01: Rename Directories, Bulk Replace, Build Gate Summary

**One-liner:** `git mv` renamed 5 project directories + solution + 5 .csproj files; `sed` replaced `EuphoriaInn`→`QuestBoard` across all 343 tracked non-planning files; `dotnet build QuestBoard.slnx --no-incremental` succeeded with 0 errors.

---

## What Was Done

### Task 1: Directory, Solution, and Project File Renames (git mv)

All five top-level project directories renamed using `git mv` to preserve full git history:

| Old Name | New Name |
|---|---|
| `EuphoriaInn.Domain/` | `QuestBoard.Domain/` |
| `EuphoriaInn.Repository/` | `QuestBoard.Repository/` |
| `EuphoriaInn.Service/` | `QuestBoard.Service/` |
| `EuphoriaInn.UnitTests/` | `QuestBoard.UnitTests/` |
| `EuphoriaInn.IntegrationTests/` | `QuestBoard.IntegrationTests/` |

Solution file renamed: `EuphoriaInn.slnx` → `QuestBoard.slnx`

Five .csproj files renamed within their (now-renamed) directories:
- `QuestBoard.Domain/QuestBoard.Domain.csproj`
- `QuestBoard.Repository/QuestBoard.Repository.csproj`
- `QuestBoard.Service/QuestBoard.Service.csproj`
- `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`
- `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj`

**Git status:** 410 R (rename) entries — history preserved for all files in all 5 directories.

**Verify result:** `RENAMED_OK`

### Task 2: Bulk Token Replacement

Command used:
```bash
git ls-files | grep -v '\.planning' | xargs grep -l 'EuphoriaInn' | xargs -d '\n' sed -i 's/EuphoriaInn/QuestBoard/g'
```

- 343 tracked non-planning files contained `EuphoriaInn` before replacement
- After replacement: **0 tracked non-planning files contain EuphoriaInn**
- Scope: `git ls-files` naturally excluded `obj/`, `bin/`, `.vs/`, `.git/` build artifacts

**Forms replaced (all in one pass):**
- `namespace QuestBoard.*` declarations in all .cs files
- `using QuestBoard.*` and `@using QuestBoard.*` directives
- FQN type references in .cshtml (e.g. `List<QuestBoard.Domain.Models.Character>`)
- EF Designer.cs entity FQN strings (53 migration files including ModelSnapshot)
- `InternalsVisibleTo("QuestBoard.UnitTests")` assembly attribute
- `<ProjectReference>` paths in all .csproj files
- `<Project Path="...">` entries in `QuestBoard.slnx`
- Dockerfile COPY/restore/build/publish/ENTRYPOINT paths
- `binary-release.yml` dotnet publish path
- Integration test directory path string literals
- CLAUDE.md dev command paths, README.md run command, docs/server-setup.md ExecStart DLL name

**Key spot checks:**
- `QuestBoard.Domain/Properties/AssemblyInfo.cs`: contains `InternalsVisibleTo("QuestBoard.UnitTests")` ✓
- `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs`: contains `QuestBoard.Repository.Entities` ✓
- `QuestBoard.slnx`: all 5 Project Path entries reference `QuestBoard.*/QuestBoard.*.csproj` ✓
- `Dockerfile ENTRYPOINT`: `["dotnet", "QuestBoard.Service.dll"]` ✓
- `docs/server-setup.md ExecStart`: `/opt/questboard/QuestBoard.Service.dll` ✓
- `.github/workflows/binary-release.yml`: `dotnet publish QuestBoard.Service/QuestBoard.Service.csproj` ✓

**Protected (untouched):**
- Migration timestamp attribute strings (e.g. `20250629200240_InitialSqlServerNoAction`) — no `EuphoriaInn` in them, unchanged ✓
- D-04: no `InsertData`/`HasData` call contained `"EuphoriaInn"` as a data value (confirmed by prior research) ✓
- `.planning/` docs — excluded from replace pipeline ✓

**Verify result:** `REPLACE_OK`

### Task 3: Build Gate

```
dotnet build QuestBoard.slnx --no-incremental
```

**Result:** Build succeeded — 0 errors, 23 warnings (all pre-existing)

**Projects built successfully:**
- `QuestBoard.Domain → bin/Debug/net10.0/QuestBoard.Domain.dll`
- `QuestBoard.Repository → bin/Debug/net10.0/QuestBoard.Repository.dll`
- `QuestBoard.Service → bin/Debug/net10.0/QuestBoard.Service.dll`
- `QuestBoard.UnitTests → bin/Debug/net10.0/QuestBoard.UnitTests.dll`
- `QuestBoard.IntegrationTests → bin/Debug/net10.0/QuestBoard.IntegrationTests.dll`

**Pre-existing warnings (not introduced by this rename):**
- NU1510: PackageReference pruning suggestions in QuestBoard.Domain.csproj (4 warnings)
- CS9113: Unread parameter `logger` in 2 job files
- xUnit1051: CancellationToken usage suggestions in test files (17 warnings)

---

## Deviations from Plan

None — plan executed exactly as written.

The LF→CRLF line ending warnings from `git diff --stat` are expected on Windows — the files were written by `sed` (which outputs LF) and git's `.gitattributes` will convert to CRLF on the next commit. This is cosmetic and not an error.

---

## Commit Status

**NO COMMIT WAS MADE IN THIS PLAN.**

Per decision D-05 and the plan's explicit instructions, the single atomic commit `refactor: rename EuphoriaInn → QuestBoard` is owned by **plan 02** and will only be made after the full test suite (191 tests) passes.

Current working tree state:
- 410 staged R (rename) entries from `git mv` operations
- 343 files with content modifications (not yet staged for commit — `sed -i` modifies working tree, `git mv` stages renames only)
- All changes are unstaged content modifications

Plan 02 will stage all changes and create the single atomic commit after running `dotnet test QuestBoard.slnx --verbosity normal`.

---

## Phase Requirement Satisfaction

| Req ID | Status | Evidence |
|--------|--------|----------|
| RENAME-01 | Satisfied | Zero `EuphoriaInn` in tracked non-planning C# files; `REPLACE_OK` |
| RENAME-02 | Satisfied | All 5 dirs + solution + 5 .csproj renamed; `dotnet build` exits 0; `RENAMED_OK` |
| RENAME-03 | Satisfied | CLAUDE.md, README.md, Dockerfile, binary-release.yml, docs/server-setup.md all patched |
| RENAME-04 | Partially satisfied | 53 migration files patched in-place (D-01); build succeeded; full test gate (191 tests) runs in plan 02 |

---

## Self-Check: PASSED

**Files exist:**
- [x] `QuestBoard.slnx` — exists
- [x] `QuestBoard.Domain/QuestBoard.Domain.csproj` — exists
- [x] `QuestBoard.Repository/QuestBoard.Repository.csproj` — exists
- [x] `QuestBoard.Service/QuestBoard.Service.csproj` — exists
- [x] `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` — exists
- [x] `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` — exists
- [x] `QuestBoard.Domain/Properties/AssemblyInfo.cs` — exists, contains `InternalsVisibleTo("QuestBoard.UnitTests")`
- [x] `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs` — exists, contains `QuestBoard.Repository.Entities`

**No EuphoriaInn in tracked non-planning files:** verified (empty result from grep)

**Build gate:** `dotnet build QuestBoard.slnx --no-incremental` — Build succeeded, 0 errors

**No commit made:** confirmed (plan 02 owns the atomic commit per D-05)
