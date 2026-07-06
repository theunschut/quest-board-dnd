# Phase 58: Rename the Guild Members feature to Characters everywhere - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-06
**Phase:** 58-rename-the-guild-members-feature-to-characters-everywhere-co
**Areas discussed:** Rename depth, Players overlap, Old URLs

---

## Rename depth

| Option | Description | Selected |
|--------|-------------|----------|
| Full rename | Rename controller class (→ CharactersController, route → /Characters), views folder, ViewModel folder, CSS files, CSS class names, integration test file, and all UI copy/nav text | ✓ |
| UI text only | Only change nav label/page titles/button copy; leave controller class, routes, folder names, CSS files/classes, and test names as GuildMembers* internally | |
| Full rename, but keep CSS class names | Rename controller/route/views/ViewModels/tests/copy, but leave .guild-member-row/.guild-roster CSS class names alone | |

**User's choice:** Full rename (recommended option)
**Notes:** Finishes the job the Domain layer already did (Entity/Service/Repository are already "Character"-named) — no split naming left behind.

---

## Players overlap

| Option | Description | Selected |
|--------|-------------|----------|
| Rename it too | Rename GuildMembersIndexViewModel → PlayersIndexViewModel (or similar), move out of GuildMembersViewModels folder, fix "guild registry" copy | ✓ |
| Leave it alone | Out of scope — flag as separate follow-up cleanup instead of expanding this phase's diff | |

**User's choice:** Rename it too (recommended option)
**Notes:** Found via codebase scouting, not something the user had flagged originally — `ViewModels/GuildMembersViewModels/GuildMembersIndexViewModel.cs` is actually used by the unrelated `PlayersController` (DM/Player user directory), not the character roster. User confirmed folding this fix into the phase since leaving it would mean a stray "GuildMembers"-named file survives the rename for an unrelated feature.

---

## Old URLs

| Option | Description | Selected |
|--------|-------------|----------|
| Clean break | No redirect — small trusted group (~17 members), nav-driven usage, stale bookmarks just re-navigate | ✓ |
| Add a redirect | Keep a thin route alias/middleware redirect from old /GuildMembers paths to new /Characters paths | |

**User's choice:** Clean break (recommended option)
**Notes:** Consistent with PROJECT.md's established small-trusted-group context.

---

## Claude's Discretion

- Exact new class name for the renamed `GuildMembersIndexViewModel` (e.g. `PlayersIndexViewModel`)
- Exact reworded copy for "guild registry" and "the guild awaits other adventurers" / "join the guild" empty-state text
- Whether "Guild Roster" (second section heading on Characters Index) gets renamed and to what
- Exact new CSS class naming scheme (just needs internal consistency)
- Whether the now-empty `ViewModels/GuildMembersViewModels/` folder is deleted after its one file moves out

## Deferred Ideas

- Renaming other D&D-flavored terminology (Quest, DM, Session, Guild in other contexts) — explicitly out of scope, this phase is "Guild Members" → "Characters" only
- A redirect/alias for old `/GuildMembers` URLs — explicitly decided against (clean break)
