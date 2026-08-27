---
gsd_state_version: 1.0
milestone: v9.0
milestone_name: Rolling Improvements
current_phase: 78
current_phase_name: Link Preview Foundation and Quest Cards
status: executing
stopped_at: Phase 75 context gathered
last_updated: "2026-08-27T09:17:23.026Z"
last_activity: 2026-08-27
last_activity_desc: Phase 74 complete, transitioned to Phase 78
progress:
  total_phases: 8
  completed_phases: 3
  total_plans: 24
  completed_plans: 15
  percent: 38
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-08-25 — v9.0 milestone start)

**Core value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.
**Current focus:** Phase 74 — event-schema-crud-and-calendar-display

## Current Position

Phase: 78 — Link Preview Foundation and Quest Cards
Plan: Not started
Status: Executing Phase 74
Last activity: 2026-08-27 — Phase 74 complete, transitioned to Phase 78

## Performance Metrics

**Velocity:**

- Total plans completed (v8.0): 26/26 across 7 phases (65–71)
- Timeline: ~2 days (2026-07-09 → 2026-07-11)

**Recent Trend:**

- v8.0 shipped in ~2 days across 7 phases, 26 plans — no scope growth beyond the original roadmapped phase set (unlike v7.0's 18 ad-hoc additions). A milestone-close audit found and fixed one cross-phase gap (QuestLog Description rendering raw) before shipping. See `.planning/milestones/v8.0-ROADMAP.md` and `.planning/milestones/v8.0-MILESTONE-AUDIT.md` for details.
- v7.0 shipped in ~3.1 days across 22 phases, 59 plans — largest milestone by phase count yet. See `.planning/RETROSPECTIVE.md` for the full cross-milestone trend view.

## Accumulated Context

### Decisions

v8.0's decision log has been archived — see `.planning/PROJECT.md` Key Decisions table and `.planning/milestones/v8.0-ROADMAP.md` Milestone Summary for the consolidated view. No open decisions carried forward.

### Roadmap Evolution

v8.0 shipped exactly as originally roadmapped: 7 phases (65–71), 26 plans, 100% requirement coverage (21/21), no orphans, no ad-hoc scope additions. Full evolution history archived in `.planning/milestones/v8.0-ROADMAP.md`.

- Phase 78 added 2026-08-26: Link Preview Foundation and Quest Cards — Open Graph / Twitter Card unfurls for quest links, gated behind signed share links.
- Phase 79 added 2026-08-26: Character and Contact Link Cards — extends the signed-link mechanism to characters and contacts, including portrait images and the `IsRevealed` spoiler gate.

### Pending Todos

None captured for v8.0. Two small deferred toolbar features (EDITOR-07/08/09 — strikethrough, horizontal rule, cheatsheet link) logged in `.planning/PROJECT.md` Requirements → Active for a future milestone to pick up if requested.

### Blockers/Concerns

None open for v8.0. Carried forward from prior milestones, still unresolved:

- `GroupSessionMiddleware` redirects on all HTTP verbs including POST — a POST-body data-loss risk if the session expires mid-submission; flagged by code review during Phase 31, not yet fixed.
- `Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` appears to be dead code (Platform area's `_ViewStart.cshtml` never selects it) — discovered during Phase 42 research, deliberately left unfixed as out-of-scope for that phase. See PROJECT.md Known Issues.
- `GuildMembersController.Edit` POST's `SetAsMainCharacterAsync` demotion guard can never be true (dead code, predates Phase 56) — found during Phase 56 verification, flagged as a separate follow-up task, not yet actioned. See PROJECT.md Known Issues.

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260713-js8 | Add re-crop trigger for existing profile images (Characters, Contacts, DM Profile) and fix backend gaps that would drop or wipe crop-only submissions | 2026-07-13 | d2f2f95 | [260713-js8-add-re-crop-trigger-for-existing-profile](./quick/260713-js8-add-re-crop-trigger-for-existing-profile/) |
| 260714-b0w | Waitlist table missing on quest details/manage pages when quest is finalized, or 'No' votes not showing in waitlist | 2026-07-14 | 79e76cb | [260714-b0w-waitlist-table-missing-on-quest-details-](./quick/260714-b0w-waitlist-table-missing-on-quest-details-/) |

## Deferred Items

Items acknowledged and carried forward across milestone closes.

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| requirement | EMAIL-04 — digest session reminder (multiple same-day quests → one email) | Still deferred — same-day quests have never occurred in over a year of operation | v4.0 close |
| requirement | REMIND-02 — combined reminder for multi-quest days | Still deferred — same as EMAIL-04 | v4.0 close |
| tech debt | `GroupSessionMiddleware` redirects on POST — data-loss risk if session expires mid-submission | Still deferred — flagged by code review in Phase 31, not yet fixed | v5.0 close |
| requirement | EMAILMD-02 — real Outlook desktop verification for all 3 quest email templates | Deferred — untestable without production access (real relay + real AppUrl); Gmail-confirmed via operator override for Quest Finalized directly, Session Reminder/Waitlist Promoted on shared-engine grounds | v8.0 close |

## Session Continuity

Last session: 2026-08-27T09:17:23.000Z
Stopped at: Phase 75 context gathered
Resume file: .planning/phases/75-event-availability-signups/75-CONTEXT.md

## Operator Next Steps

- Review `.planning/milestones/v8.0-ROADMAP.md` and `.planning/MILESTONES.md` for the shipped-milestone summary
- Run `/gsd-new-milestone` to begin questioning → research → requirements → roadmap for the next milestone
