# Phase 26: Namespace Rename - Pattern Map

**Mapped:** 2026-06-29
**Files analyzed:** 343 files containing `EuphoriaInn` (source-level); 8 config/infra files with discrete edits
**Analogs found:** 0 / 0 — this phase has no new logic; no code analogs apply

---

## Overview

Phase 26 is a pure mechanical rename — no new files are created and no new logic is introduced. Every
modification is a text replacement of `EuphoriaInn` → `QuestBoard` across namespace declarations,
`using` directives, project file references, directory names, CI workflows, and deployment scripts.

**There are no code analogs to extract.** The "pattern" for this phase is the replacement rule itself,
the sequencing constraint, and the safe-replace boundaries documented below.

---

## File Classification

| File / File Set | Role | Data Flow | Operation | Closest Analog | Match Quality |
|---|---|---|---|---|---|
| `EuphoriaInn.slnx` → `QuestBoard.slnx` | config | — | `git mv` + content patch (5 project paths) | — | n/a |
| `EuphoriaInn.Domain/` → `QuestBoard.Domain/` | directory | — | `git mv` directory rename | — | n/a |
| `EuphoriaInn.Repository/` → `QuestBoard.Repository/` | directory | — | `git mv` directory rename | — | n/a |
| `EuphoriaInn.Service/` → `QuestBoard.Service/` | directory | — | `git mv` directory rename | — | n/a |
| `EuphoriaInn.UnitTests/` → `QuestBoard.UnitTests/` | directory | — | `git mv` directory rename | — | n/a |
| `EuphoriaInn.IntegrationTests/` → `QuestBoard.IntegrationTests/` | directory | — | `git mv` directory rename | — | n/a |
| `*.csproj` files (5 files) | config | — | rename file + patch `<ProjectReference>` paths | — | n/a |
| All `*.cs`, `*.cshtml`, `*.razor` source files (~201 non-migration) | source | — | bulk text replace | — | n/a |
| `EuphoriaInn.Repository/Migrations/` (53 files) | migration | — | in-place namespace patch (D-01) | — | n/a |
| `Dockerfile` | config | — | patch project path strings (lines 11–17, 20–22, 26, 30, 41) | — | n/a |
| `.github/workflows/binary-release.yml` | CI | — | patch one line (line 29: `dotnet publish` path) | — | n/a |
| `.github/workflows/dotnet.yml` | CI | — | verify only — no EuphoriaInn refs found | — | n/a |
| `.github/workflows/docker-publish.yml` | CI | — | verify only — no EuphoriaInn refs found | — | n/a |
| `create-migration.sh` | script | — | already partially updated (path at line 2 still has old form); verify full file | — | n/a |
| `CLAUDE.md` | docs | — | patch dev command paths (lines 25, 35–37, 44–46, 49–50, 56) | — | n/a |
| `README.md` | docs | — | patch `dotnet run --project EuphoriaInn.Service` (line 26) | — | n/a |
| `docs/server-setup.md` | docs | — | patch systemd ExecStart DLL path (line 120) | — | n/a |

---

## Pattern Assignments

### Wave 1: Directory and Project Structure Renames

**Operation:** `git mv` (must use Git Bash — PowerShell `Rename-Item` does not preserve git history)

**Exact commands** (run from repo root in Git Bash, sequentially):

```bash
git mv EuphoriaInn.Domain QuestBoard.Domain
git mv EuphoriaInn.Repository QuestBoard.Repository
git mv EuphoriaInn.Service QuestBoard.Service
git mv EuphoriaInn.UnitTests QuestBoard.UnitTests
git mv EuphoriaInn.IntegrationTests QuestBoard.IntegrationTests
git mv EuphoriaInn.slnx QuestBoard.slnx
```

**Then rename the 5 .csproj files** (within their now-renamed directories):

```bash
git mv QuestBoard.Domain/EuphoriaInn.Domain.csproj QuestBoard.Domain/QuestBoard.Domain.csproj
git mv QuestBoard.Repository/EuphoriaInn.Repository.csproj QuestBoard.Repository/QuestBoard.Repository.csproj
git mv QuestBoard.Service/EuphoriaInn.Service.csproj QuestBoard.Service/QuestBoard.Service.csproj
git mv QuestBoard.UnitTests/EuphoriaInn.UnitTests.csproj QuestBoard.UnitTests/QuestBoard.UnitTests.csproj
git mv QuestBoard.IntegrationTests/EuphoriaInn.IntegrationTests.csproj QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj
```

---

### Wave 2: Bulk Content Replace

**Operation:** Text replace `EuphoriaInn` → `QuestBoard` across all tracked source files.

**Recommended command** (Git Bash, from repo root):

```bash
git ls-files | grep -v '\.planning' | xargs grep -l 'EuphoriaInn' | \
  xargs -d '\n' sed -i 's/EuphoriaInn/QuestBoard/g'
```

`git ls-files` automatically excludes `obj/`, `bin/`, `.git/`, `.vs/`. The `-d '\n'` flag handles
filenames with spaces correctly.

**Alternative (PowerShell):**

```powershell
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

Note: On Windows PowerShell 5.1, `-Encoding UTF8` writes a BOM. Verify Designer.cs encoding first;
use Git Bash `sed` fallback if BOM behavior differs from existing files.

---

## Shared Patterns (Replace Targets)

These are the distinct forms of `EuphoriaInn` that the bulk replace must cover. All are handled by the
single token replace — listed here so the planner can write targeted verification steps.

### Namespace declarations (all `.cs` files)

```csharp
// Before
namespace EuphoriaInn.Service.Controllers
namespace EuphoriaInn.Domain.Interfaces
namespace EuphoriaInn.Repository.Migrations

// After
namespace QuestBoard.Service.Controllers
namespace QuestBoard.Domain.Interfaces
namespace QuestBoard.Repository.Migrations
```

### Using directives (all `.cs`, `.cshtml`, `.razor` files)

```csharp
// Before
using EuphoriaInn.Domain.Interfaces;
@using EuphoriaInn.Service.ViewModels

// After
using QuestBoard.Domain.Interfaces;
@using QuestBoard.Service.ViewModels
```

### EF Designer.cs FQN strings (53 migration files in `QuestBoard.Repository/Migrations/`)

```csharp
// Before — entity FQN string inside modelBuilder calls
modelBuilder.Entity("EuphoriaInn.Repository.Entities.QuestEntity", b =>

// After
modelBuilder.Entity("QuestBoard.Repository.Entities.QuestEntity", b =>
```

These ARE namespace-qualified type names, not display data — correct to rename.
Confirmed: no `InsertData()`/`HasData()` call contains `"EuphoriaInn"` as a value string (D-04 safe).

### InternalsVisibleTo assembly attribute

**Source file:** `QuestBoard.Domain/Properties/AssemblyInfo.cs`

```csharp
// Before
[assembly: InternalsVisibleTo("EuphoriaInn.UnitTests")]

// After
[assembly: InternalsVisibleTo("QuestBoard.UnitTests")]
```

### ProjectReference paths (all `.csproj` files)

```xml
<!-- Before -->
<ProjectReference Include="..\EuphoriaInn.Domain\EuphoriaInn.Domain.csproj" />

<!-- After -->
<ProjectReference Include="..\QuestBoard.Domain\QuestBoard.Domain.csproj" />
```

### Solution file paths

**Source file:** `QuestBoard.slnx` (after `git mv`)

```xml
<!-- Before (inside EuphoriaInn.slnx) -->
<Project Path="EuphoriaInn.Domain/EuphoriaInn.Domain.csproj" />
<Project Path="EuphoriaInn.Repository/EuphoriaInn.Repository.csproj" />
<Project Path="EuphoriaInn.Service/EuphoriaInn.Service.csproj" />
<Project Path="EuphoriaInn.UnitTests/EuphoriaInn.UnitTests.csproj" />
<Project Path="EuphoriaInn.IntegrationTests/EuphoriaInn.IntegrationTests.csproj" />

<!-- After -->
<Project Path="QuestBoard.Domain/QuestBoard.Domain.csproj" />
<Project Path="QuestBoard.Repository/QuestBoard.Repository.csproj" />
<Project Path="QuestBoard.Service/QuestBoard.Service.csproj" />
<Project Path="QuestBoard.UnitTests/QuestBoard.UnitTests.csproj" />
<Project Path="QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj" />
```

### Dockerfile project path strings

**Source file:** `Dockerfile`

Current lines requiring change (lines 11–17, 20–22, 26, 30, 41):
- `COPY ["EuphoriaInn.Domain/EuphoriaInn.Domain.csproj", ...]`
- `COPY ["EuphoriaInn.Repository/EuphoriaInn.Repository.csproj", ...]`
- `COPY ["EuphoriaInn.Service/EuphoriaInn.Service.csproj", ...]`
- `dotnet restore "EuphoriaInn.Service/EuphoriaInn.Service.csproj"`
- `dotnet build "EuphoriaInn.Service/EuphoriaInn.Service.csproj" ...`
- `dotnet publish "EuphoriaInn.Service/EuphoriaInn.Service.csproj" ...`
- `COPY ["EuphoriaInn.Domain/", ...]`, etc.
- `ENTRYPOINT ["dotnet", "EuphoriaInn.Service.dll"]`

All handled by bulk replace.

### CI workflow (binary-release.yml line 29)

```yaml
# Before
run: dotnet publish EuphoriaInn.Service/EuphoriaInn.Service.csproj -c Release -o ./publish

# After
run: dotnet publish QuestBoard.Service/QuestBoard.Service.csproj -c Release -o ./publish
```

`dotnet.yml` and `docker-publish.yml` — verified clean, no EuphoriaInn refs.

### Integration test hardcoded path strings

**Source files:** `QuestBoard.IntegrationTests/MobileCssTests.cs` (line 50), `QuestBoard.IntegrationTests/QuestFinalizeTests.cs` (line 27)

```csharp
// Before
"EuphoriaInn.Service", "Controllers", ...

// After
"QuestBoard.Service", "Controllers", ...
```

Handled by bulk replace (these are directory path strings, not display data).

### Documentation strings (CLAUDE.md, README.md, docs/server-setup.md)

```
# CLAUDE.md (lines 25, 35-37, 44-46, 49-50, 56)
dotnet run --project EuphoriaInn.Service
→ dotnet run --project QuestBoard.Service

dotnet ef migrations add MigrationName --project ../EuphoriaInn.Repository
→ dotnet ef migrations add MigrationName --project ../QuestBoard.Repository

EuphoriaInn.Service — MVC controllers ...
→ QuestBoard.Service — MVC controllers ...

# README.md (line 26)
dotnet run --project EuphoriaInn.Service
→ dotnet run --project QuestBoard.Service

# docs/server-setup.md (line 120)
ExecStart=/usr/bin/dotnet /opt/questboard/EuphoriaInn.Service.dll
→ ExecStart=/usr/bin/dotnet /opt/questboard/QuestBoard.Service.dll
```

---

## No Analog Found

This entire phase has no analogs — it introduces no new logic, no new file roles, and no new data flows.
All modifications are mechanical text replacements and filesystem renames.

| File Set | Role | Data Flow | Reason |
|---|---|---|---|
| All 343 files | rename target | — | Pure token replacement; no code pattern applies |

---

## Post-Replace Verification Commands

The planner must include these as explicit task steps after the bulk replace:

```bash
# 1. Verify no EuphoriaInn remains in source (should return empty)
grep -r "EuphoriaInn" QuestBoard.Domain QuestBoard.Repository QuestBoard.Service \
  QuestBoard.UnitTests QuestBoard.IntegrationTests \
  --include="*.cs" --include="*.cshtml" --include="*.razor"

# 2. Verify CI, scripts, docs are clean
grep -r "EuphoriaInn" .github Dockerfile create-migration.sh CLAUDE.md README.md docs

# 3. Build gate — must exit 0
dotnet build QuestBoard.slnx --no-incremental

# 4. Full test gate — must show 0 failures (191 passing)
dotnet test QuestBoard.slnx --verbosity normal
```

---

## Critical Boundary: What Must NOT Be Replaced

Per decision D-04: the literal string `"EuphoriaInn"` as a group name value in seed data.
Research confirmed (A3): no migration file contains `InsertData()` with `"EuphoriaInn"` as a value.
Simple token replacement is safe across the entire codebase.

---

## Manual Step (Out-of-Band — Production Server)

This is not a code change — it is a server admin task that must be completed before the first deploy
after this rename is merged:

**On production server:**
```bash
sudo sed -i 's/EuphoriaInn.Service.dll/QuestBoard.Service.dll/' \
  /etc/systemd/system/questboard.service
sudo systemctl daemon-reload
```

The plan must include this as an explicit manual gate before the first `git tag` + deploy.

---

## Metadata

**Analog search scope:** Not applicable — pure rename phase
**Files scanned:** `EuphoriaInn.slnx`, `Dockerfile`, `create-migration.sh`, `binary-release.yml`,
  `dotnet.yml`, `docker-publish.yml`, `CLAUDE.md`, `README.md`, `docs/server-setup.md`
**Pattern extraction date:** 2026-06-29
