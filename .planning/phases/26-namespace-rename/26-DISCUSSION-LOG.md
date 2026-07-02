# Phase 26: Namespace Rename - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-29
**Phase:** 26-namespace-rename
**Areas discussed:** Migration file handling, Rename scope beyond namespaces, Commit strategy

---

## Migration File Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Patch in-place | Text-replace EuphoriaInn → QuestBoard namespace strings in all existing migration files. Deployed DB migration history stays intact — no manual DB intervention needed. | ✓ |
| Squash into one migration | Delete all 13 migrations and regenerate a single InitialCreate from the current schema. Cleaner history, but requires manually clearing the __EFMigrationsHistory table on the deployed DB. | |

**User's choice:** Patch in-place
**Notes:** Safety-first choice — the deployed LXC DB has applied migration history that must not be disrupted.

---

## Rename Scope Beyond Namespaces

### CLAUDE.md / scripts / Dockerfile

| Option | Description | Selected |
|--------|-------------|----------|
| Update them now | CLAUDE.md commands would immediately reflect the renamed paths — keeps developer instructions accurate. | ✓ |
| Leave for a follow-up | Phase scope stays strictly namespaces + project files + config/CI. CLAUDE.md update is a separate commit. | |

**User's choice:** Update them now
**Notes:** Keeping CLAUDE.md accurate for developer usage is worth including in the rename phase.

### View display text

| Option | Description | Selected |
|--------|-------------|----------|
| Leave view display text as-is | 'Euphoria Inn' is the in-world tavern name — it stays. Phase 30 or later can rebrand display text per group. | ✓ |
| Rename display text to 'Quest Board' | Update visible 'Euphoria Inn' strings in views/layouts to 'Quest Board' as part of this rename phase. | |

**User's choice:** Not applicable — user confirmed there are no "EuphoriaInn" strings visible in the UI
**Notes:** "EuphoriaInn" is strictly a namespace convention, not a display name used in the interface.

---

## Commit Strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Atomic — single commit | One 'refactor: rename EuphoriaInn → QuestBoard' commit. Clean, unambiguous in git log. | ✓ |
| Per-concern — 3 commits | (1) C# namespace text replacements, (2) directory/project file renames, (3) config/CI/docs updates. Each commit must build independently. | |

**User's choice:** Atomic — single commit
**Notes:** The rename is non-behavioral; all-or-nothing is appropriate and easier to review.

---

## Claude's Discretion

None — all areas had a clear user preference.

## Deferred Ideas

None — discussion stayed within phase scope.
