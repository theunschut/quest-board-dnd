# Phase 59: Add a rewards field to quests - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-06
**Phase:** 59-add-a-rewards-field-to-quests-an-open-text-field-between-des
**Areas discussed:** Required vs optional + board type, Where else it shows, Details page block style, Empty state

---

## Required vs optional + board type

| Option | Description | Selected |
|--------|-------------|----------|
| Optional (like Recap) | Mirrors Recap's pattern (nullable string, no [Required]) — a DM can create a quest before deciding on rewards, or leave it blank for quests with no tangible reward | ✓ |
| Required (like Description) | Every quest must state its reward before it can be created/saved | |

**User's choice:** Optional (like Recap)

| Option | Description | Selected |
|--------|-------------|----------|
| Show for both board types | Matches Description's own treatment — not wrapped in the Campaign-hiding conditional | ✓ |
| OneShot only | Bundle Rewards inside the same @if (boardType != BoardType.Campaign) block as Challenge Rating | |

**User's choice:** Show for both board types
**Notes:** This resolves the placement question — Rewards renders unconditionally right after Description, before the OneShot-only conditional block, naturally satisfying "between Description and Challenge Rating" on OneShot boards.

---

## Where else it shows

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, show it there too (QuestLog) | Once a quest is done, players/DM likely still want to see what the promised reward was, alongside the recap | ✓ |
| No, Quest Details only | Keep the QuestLog completed-quest page unchanged | |

**User's choice:** Yes, show it on QuestLog/Details too (read-only)

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, add it there too (Follow-Up form) | Consistency with the same field-order convention used on Create/Edit | ✓ |
| No, skip Follow-Up form | Leave FollowUpQuestViewModel/CreateFollowUp.cshtml untouched | |

**User's choice:** Yes, add it to the Follow-Up Quest form

| Option | Description | Selected |
|--------|-------------|----------|
| Start blank | A follow-up quest ("Part 2") is a new session with its own reward, not a copy of the last one | ✓ |
| Pre-fill from original | Copy Rewards forward verbatim, same as Title/Description/CR/TotalPlayerCount | |

**User's choice:** Start blank (not pre-filled)

| Option | Description | Selected |
|--------|-------------|----------|
| Leave board list unchanged | The card is already dense; Rewards stays a Details-page-only feature | ✓ |
| Add a small indicator | New scope beyond original request — coin/treasure icon on the card | |

**User's choice:** Leave the quest board list card (`_QuestCard.cshtml`) unchanged

---

## Details page block style

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse existing box style exactly | Visual consistency with the established .quest-description-box/.recap-display-box pattern | ✓ |
| Distinct accent for Rewards | Same shape/padding, different border/heading color to set Rewards apart | |

**User's choice:** Reuse existing box style exactly

| Option | Description | Selected |
|--------|-------------|----------|
| "Rewards" with fa-coins | Standard FontAwesome treasure/currency icon, matches existing gold/warning accents | ✓ |
| "Rewards" with fa-gem | Alternate gem/loot framing | |
| No heading, just the box | Minimal, implied by placement below Description | |

**User's choice:** "Rewards" heading with fa-coins icon

---

## Empty state

| Option | Description | Selected |
|--------|-------------|----------|
| Hide the block entirely | Cleanest for the common case — no block, no heading, nothing rendered | ✓ |
| Show muted placeholder | Always render with "No rewards listed yet", matching Recap's empty-state pattern | |

**User's choice:** Hide the block entirely when empty

| Option | Description | Selected |
|--------|-------------|----------|
| No limit (match Description) | Description has no [StringLength] anywhere in the stack — unbounded nvarchar(max) | ✓ |
| Cap it (e.g. 2000 chars) | Matches Recap's/Character's/Contact's StringLength(2000) convention | |

**User's choice:** No character limit

---

## Claude's Discretion

- Exact placeholder copy for the Rewards textarea
- Exact CSS class name for the new box on `QuestLog/Details.cshtml` (reuse `.quest-description-box` directly vs. a new same-shaped class)
- Exact placement order of the new Rewards box on `QuestLog/Details.cshtml` relative to existing sections
- Migration structure (own dedicated migration, nullable string, same shape as `Recap`)

## Deferred Ideas

- A small rewards indicator/icon on the quest board list card (`_QuestCard.cshtml`) — considered and explicitly declined (not deferred to a specific future phase, just decided against for now).
