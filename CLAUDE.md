# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@.claude/project-overview.md
@.claude/architecture.md
@.claude/rip-navigation.md

## Branching

**Never commit directly to `main`.** Main has branch protection rules. All work — including planning docs, migrations, and feature code — must go on a feature branch.

- Milestone work: `milestone/v<N>-<name>` (e.g. `milestone/v5-multi-tenancy`)
- Feature work: `feature/<short-description>`

Branch from the current working branch, not from `main`, unless the work genuinely has no dependency on what is in flight — branching a follow-up off `main` silently excludes the milestone's own changes.

If you realize commits have landed on `main` by mistake: create the branch from current `main`, then `git reset --hard <pre-commit-sha>` on `main` to remove them.

## Local database

The app expects SQL Server at **`localhost:1433`** on every platform. How it got there is the user's business — a Windows host install, a Docker container, whatever. **Do not install, start, or provision it, and do not tear it down afterwards.** If it is unreachable, stop and tell the user what is missing.

The one platform difference is **auth mode**. The committed `appsettings.json` uses `Trusted_Connection=true` (Windows Integrated Auth):

- **Windows** — works as-is against a local install.
- **Linux / Docker** — cannot work; a container has no Windows auth. Override with SQL auth via `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`, never by editing `appsettings.json` (it is committed).

The failure symptom is misleading: `Cannot generate SSPI context` looks like a Kerberos or DNS problem, but it means the wrong auth mode for the platform. Do **not** "fix" it by deleting `Trusted_Connection=true` — that silently promotes the vestigial `User Id`/`Password` in the same string to being the live login.

Integration tests use EF Core InMemory and need no database at all.

## Development Commands

```bash
dotnet build
dotnet test
dotnet run --project QuestBoard.Service
```

**Build failures due to locked files**: If `dotnet build` or `dotnet test` fails because output files are in use, Visual Studio is most likely running the app under the debugger. Ask the user to stop the debugger (Shift+F5) before retrying.

**On Linux**: never set `DOTNET_GCHeapHardLimit` or other GC-constraining env vars — they crash the Roslyn analyzers and surface as a wall of bogus compile errors. A `Fatal error. Internal CLR error. (0x80131506)` is a transient flake; re-run the same command once before treating it as real.

Migration commands and the layer rules live in `.claude/architecture.md`, imported above.

## Code Comments

**Never embed GSD planning/tracking references in source code** — no requirement IDs (`D-06`, `TENANT-03`, `EMAIL-04`), phase/plan numbers (`Phase 28`, `31-01`), review-finding IDs (`WR-03`, `31-REVIEW`), or planning document names (`RESEARCH.md`, `UI-SPEC.md`, `CONTEXT.md`) in comments, XML doc comments, or string literals. These references go stale the moment a phase closes and become dead noise that a future cleanup phase has to hunt down and strip. Write comments that explain the *why* in plain language that stays true independent of which phase touched the code — e.g. `// Backfill LockoutEnabled for existing users so the lockout policy applies retroactively`, not `// SEC-02: backfill LockoutEnabled...`. Planning/tracking context belongs in `.planning/`, not in source. This does not apply to git commit messages, which are expected to reference phase/plan IDs for traceability.

## UI/UX

**Before creating or editing any Razor view, read `.claude/ui-guidelines.md`** — card pattern, button layout, `.Mobile.cshtml` twins, and the date-rendering helpers.

## Reference Docs

Read these on demand when needed — not loaded by default:

- **Project state** — `.planning/PROJECT.md` — current milestone, shipped features, known debt
- **Architecture** — `.planning/codebase/ARCHITECTURE.md` — layer structure, dependency direction, data flow, key abstractions
- **Conventions** — `.planning/codebase/CONVENTIONS.md` — naming patterns, code style, AutoMapper patterns
- **Tech Stack** — `.planning/codebase/STACK.md` — full dependency list, versions, configuration details
- **Roadmap** — `.planning/ROADMAP.md` — planned phases and milestones
