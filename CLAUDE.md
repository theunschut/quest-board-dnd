# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Environment

**Platform**: Development is done on **Windows**. Use Windows-style paths and line endings (CRLF) when creating or editing files. Avoid Unix-only shell syntax.

**Important**: SQL Server runs on the Windows host, not in WSL. Use `localhost` in the connection string for local development; Docker uses the `sqlserver` service name.

## Branching

**Never commit directly to `main`.** Main has branch protection rules. All work — including planning docs, migrations, and feature code — must go on a feature branch.

- Milestone work: `milestone/v<N>-<name>` (e.g. `milestone/v5-multi-tenancy`)
- Feature work: `feature/<short-description>`

If you realize commits have landed on `main` by mistake: create the branch from current `main`, then `git reset --hard <pre-commit-sha>` on `main` to remove them.

## Development Commands

**Build failures due to locked files**: If `dotnet build` or `dotnet test` fails because output files are in use, Visual Studio is most likely running the app under the debugger. Ask the user to stop the debugger (Shift+F5) before retrying the build.

```bash
# Build and run
dotnet build
dotnet run --project QuestBoard.Service

# Docker
docker-compose up -d
docker-compose logs -f questboard
```

Migrations are **auto-applied on startup** via `context.Database.Migrate()` — no manual `database update` needed in dev.

```bash
# Add/remove migrations (run from QuestBoard.Service/)
dotnet ef migrations add MigrationName --project ../QuestBoard.Repository
dotnet ef migrations remove --project ../QuestBoard.Repository
```

## Architecture

Three-layer clean architecture: **Service → Domain → Repository** (strict one-way dependency).

- `QuestBoard.Service` — MVC controllers, Razor views, ViewModels, authorization handlers
- `QuestBoard.Domain` — business logic, domain models, service interfaces
- `QuestBoard.Repository` — EF Core entities, repositories, `QuestBoardContext`, migrations

AutoMapper runs at two boundaries:
- Entity ↔ DomainModel: `QuestBoard.Domain/Automapper/EntityProfile.cs`
- DomainModel ↔ ViewModel: `QuestBoard.Service/Automapper/ViewModelProfile.cs`

Authorization policies: `"DungeonMasterOnly"` (DungeonMaster or Admin role), `"AdminOnly"` (Admin role only).

## Entity Framework

**IMPORTANT**: EF packages belong only in `QuestBoard.Repository` — never add them to the Service project.

## Code Comments

**Never embed GSD planning/tracking references in source code** — no requirement IDs (`D-06`, `TENANT-03`, `EMAIL-04`), phase/plan numbers (`Phase 28`, `31-01`), or review-finding IDs (`WR-03`, `31-REVIEW`) in comments, XML doc comments, or string literals. These references go stale the moment a phase closes and become dead noise that a future cleanup phase has to hunt down and strip (see Phase 34). Write comments that explain the *why* in plain language that stays true independent of which phase touched the code — e.g. `// Backfill LockoutEnabled for existing users so the lockout policy applies retroactively`, not `// SEC-02: backfill LockoutEnabled...`. Planning/tracking context belongs in `.planning/`, not in source. This does not apply to git commit messages, which are expected to reference phase/plan IDs for traceability.

## Code Navigation — RIP MCP

If the `rip` MCP server is available (tools prefixed `mcp__rip__`), **always prefer it over reading files** for any symbol-navigation question. It has the full codebase indexed.

| Goal | Tool |
|---|---|
| Find where a symbol is defined | `FindDefinition` |
| Find a symbol by name (partial or exact) | `FindSymbol` |
| Find every usage of a symbol across the codebase | `FindReferences` |
| Read the source body of a function/class | `GetSymbolBody` |
| List all fields and methods of a class | `GetClassMembers` |
| List all values of an enum | `GetEnumValues` |
| Who calls a function | `FindCallers` |
| What does a function call | `FindCallees` |
| Subclasses / implementors of a base | `FindImplementations` |
| Full inheritance chain | `FindInheritanceTree` |
| Trace a dependency path between two symbols | `FindDependencyPath` |
| High-level subsystem dependency map | `GetArchitectureSummary` |

### RIP Lookup Protocol

When a user asks about a feature, system, or concept by name — even if the term is not obviously a symbol (e.g. "sota system", "payment flow") — follow this sequence:

1. **`GetArchitectureSummary`** — identify which namespaces/subsystems relate to the term
2. **`FindSymbol`** — try PascalCase variants: `sota` → `SotaHandler`, `Sota`, `SotaRequest`; try the plural, the base class name, the interface name
3. **`GetClassMembers`** on each found type — get structure without reading files
4. **`GetSymbolBody`** for specific methods of interest
5. **`FindCallers` / `FindCallees`** to trace integrations
6. **`FindImplementations`** for interfaces or base classes

**Only after all of the above yield nothing:** use `Grep` with `output_mode: files_with_matches` to find file paths, then apply RIP tools (`GetSymbolBody`, `GetClassMembers`) to symbols found in those files. **Never `Read` a whole file** when RIP can answer the question.

One failed `FindSymbol` query is not a reason to fall back — try at least 3 symbol-name variants before giving up on RIP.

**When RIP is insufficient**, before falling back to file reads, output a short notice in this exact format so Thomas can improve the index:

```
⚠ RIP gap send to Thomas
Query   : <tool name> / <symbol or query used>
Reason  : <one sentence: why RIP couldn't answer — e.g. "symbol not indexed", "enum values missing", "FindCallers returned empty for X">
Fallback: <what you are doing instead>
```

Then continue with the fallback. Do not block on this — emit the notice and proceed.

## UI/UX Design Guidelines

All new views must use the modern card pattern with these CSS classes: `modern-card`, `modern-card-header`, `modern-card-body`.

```html
<div class="card-header modern-card-header">
    <h2 class="mb-0">
        <i class="fas fa-icon-name text-color me-2"></i>
        Page Title
    </h2>
</div>
```

- Always include `<hr>` before the button section
- Use filled colored buttons (not outline), FontAwesome icons with `me-2` spacing
- Button layout: `d-flex justify-content-between` — secondary (cancel) left, primary (submit) right

## Project

**D&D Quest Board — Milestone 4: Email Notifications**

A D&D campaign management web application for a group of players and Dungeon Masters. It handles quest creation and scheduling, player signup with date voting, a character/guild system, a shop with gold economy, and email notifications. Built with ASP.NET Core 10 MVC, SQL Server, and Docker — deployed as a single container to a self-hosted environment.

**Core Value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.

### Constraints

- **Compatibility:** No user-facing functionality may be removed or broken — all existing flows must work after the refactor
- **Tech stack:** Stay on ASP.NET Core 10 MVC + SQL Server + EF Core — no framework changes
- **Deployment:** Must remain deployable via `docker-compose up` with no additional setup steps
- **Database:** All schema changes require EF Core migrations; auto-applied on startup

## Reference Docs

Read these on demand when needed — not loaded by default:

- **Architecture** — `.planning/codebase/ARCHITECTURE.md` — layer structure, dependency direction, data flow, key abstractions
- **Conventions** — `.planning/codebase/CONVENTIONS.md` — naming patterns, code style, AutoMapper patterns
- **Tech Stack** — `.planning/codebase/STACK.md` — full dependency list, versions, configuration details
- **Roadmap** — `.planning/ROADMAP.md` — planned phases and milestones
