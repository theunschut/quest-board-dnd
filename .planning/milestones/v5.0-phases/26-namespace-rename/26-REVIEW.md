---
phase: 26-namespace-rename
reviewed: 2026-06-29T00:00:00Z
depth: standard
files_reviewed: 9
files_reviewed_list:
  - .github/workflows/binary-release.yml
  - QuestBoard.Domain/Properties/AssemblyInfo.cs
  - QuestBoard.Domain/QuestBoard.Domain.csproj
  - QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
  - QuestBoard.Repository/QuestBoard.Repository.csproj
  - QuestBoard.Service/QuestBoard.Service.csproj
  - QuestBoard.UnitTests/QuestBoard.UnitTests.csproj
  - docs/server-setup.md
findings:
  critical: 0
  warning: 1
  info: 1
  total: 2
status: issues_found
---

# Phase 26: Code Review Report

**Reviewed:** 2026-06-29
**Depth:** standard
**Files Reviewed:** 9
**Status:** issues_found

## Summary

This phase was a pure mechanical rename from `EuphoriaInn.*` to `QuestBoard.*` across all namespaces, project files, CI config, migrations, and docs. The rename was executed correctly on every directly verifiable surface:

- No residual `EuphoriaInn` tokens exist in any `.cs`, `.csproj`, `.yml`, `.cshtml`, `.razor`, or `.json` source file. (Hits in `.planning/` and `.vs/` are historical planning docs and IDE local state — not source code.)
- `QuestBoard.slnx` was created correctly; `EuphoriaInn.slnx` was deleted.
- All old `EuphoriaInn.*` source directories are gone from the working tree.
- All `ProjectReference` paths in `.csproj` files resolve correctly to `QuestBoard.*` equivalents.
- `AssemblyInfo.cs` `InternalsVisibleTo` was correctly updated from `EuphoriaInn.UnitTests` to `QuestBoard.UnitTests`. The unit test project has a direct `ProjectReference` to `QuestBoard.Domain`, so the attribute is effective.
- `QuestBoardContextModelSnapshot.cs` uses only `QuestBoard.Repository.*` FQNs throughout — no residual old namespace strings.
- `docs/server-setup.md` is clean of `EuphoriaInn` and the artifact download URL (`questboard-$TAG.zip`) matches exactly what the CI pipeline produces (`questboard-${{ github.ref_name }}.zip`).
- The CI `deploy` job's `always()` + `workflow_dispatch` condition is intentional and correct — on `workflow_dispatch` the `release` job is skipped (not failed), and the `deploy` job correctly runs via the first branch of the `||` condition.

One pre-existing policy violation was carried forward unchanged, and one informational item is noted.

## Warnings

### WR-01: `Microsoft.EntityFrameworkCore.Tools` in Service Project Violates Architecture Policy

**File:** `QuestBoard.Service/QuestBoard.Service.csproj:13`
**Issue:** `Microsoft.EntityFrameworkCore.Tools` is referenced directly in `QuestBoard.Service.csproj`. `CLAUDE.md` states: "EF packages belong only in `EuphoriaInn.Repository` — never add them to the Service project." This was carried forward unchanged from `EuphoriaInn.Service.csproj` — this rename phase did not introduce it, but it was not cleaned up either.

The package is declared with `<PrivateAssets>all</PrivateAssets>` and `<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>`, which means it is a build-time-only tooling reference used to enable `dotnet ef` commands when the Service project is the startup project. It does not add EF runtime binaries to the published output. The functional impact is low, but it contradicts the stated architecture policy and risks confusion as future contributors may assume EF usage in Service is acceptable.

**Fix:** Move the `EF.Tools` reference to `QuestBoard.Repository.csproj` where it logically belongs. The `dotnet ef migrations add` command in `CLAUDE.md` already uses `--project ../EuphoriaInn.Repository` (now `../QuestBoard.Repository`), which is the correct pattern:

```xml
<!-- QuestBoard.Repository/QuestBoard.Repository.csproj -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.9">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

Remove the corresponding block from `QuestBoard.Service/QuestBoard.Service.csproj`. Migration commands should continue to work because `QuestBoard.Repository` is now the owner and `QuestBoard.Service` is only referenced as the startup project via `--startup-project`.

## Info

### IN-01: `_ViewImports.cshtml` Namespace Not in Reviewed File List

**File:** `QuestBoard.Service/Views/_ViewImports.cshtml:12`
**Issue:** `_ViewImports.cshtml` was not listed in the review scope (it is not a `.csproj`, `AssemblyInfo.cs`, CI yml, ModelSnapshot, or docs file), but it was renamed as part of this phase and its `@namespace QuestBoard.Service.Pages` directive is now consistent with the new namespace. Confirmed clean — no `EuphoriaInn` tokens. Noting for completeness since the `_ViewImports.cshtml` references are the most common place namespace renames are missed in Razor projects.

**Fix:** No action needed. The file is clean.

---

_Reviewed: 2026-06-29_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
