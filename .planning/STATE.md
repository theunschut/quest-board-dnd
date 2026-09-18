---
gsd_state_version: 1.0
milestone: v9.0
milestone_name: Rolling Improvements
current_phase: 85
status: completed
stopped_at: Phase 85 context gathered
last_updated: "2026-09-18T17:28:26.970Z"
last_activity: 2026-09-18
last_activity_desc: Phase 84 execution resumed (wave continue)
progress:
  total_phases: 14
  completed_phases: 12
  total_plans: 97
  completed_plans: 88
  percent: 86
current_phase_name: one-shot-quests-in-the-calendar-feed
state_head: 593a8cdabc723218783c1a4b5b8ff9b64eeca830
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-08-25 — v9.0 milestone start)

**Core value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.
**Current focus:** Phase 85 — one-shot-quests-in-the-calendar-feed

## Current Position

Phase: 85
Plan: Not started
Status: All phases complete
Last activity: 2026-09-18 — Phase 85 complete

Also planned, not started: Phase 78 — Link Preview Foundation and Quest Cards (9 plans)

## Performance Metrics

**Velocity:**

- Total plans completed (v8.0): 26/26 across 7 phases (65–71)
- Timeline: ~2 days (2026-07-09 → 2026-07-11)

**Recent Trend:**

- v8.0 shipped in ~2 days across 7 phases, 26 plans — no scope growth beyond the original roadmapped phase set (unlike v7.0's 18 ad-hoc additions). A milestone-close audit found and fixed one cross-phase gap (QuestLog Description rendering raw) before shipping. See `.planning/milestones/v8.0-ROADMAP.md` and `.planning/milestones/v8.0-MILESTONE-AUDIT.md` for details.
- v7.0 shipped in ~3.1 days across 22 phases, 59 plans — largest milestone by phase count yet. See `.planning/RETROSPECTIVE.md` for the full cross-milestone trend view.

**Per-Plan Metrics:**

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 84 P02 | 22 min | 3 tasks | 24 files |
| Phase 84 P03 | 16 min | 3 tasks | 2 files |
| Phase 84 P04 | 23min | 5 tasks | 11 files |
| Phase 84 P05 | 24min | 3 tasks | 3 files |
| Phase 84 P08 | 137min | 4 tasks | 10 files |

## Accumulated Context

### Decisions

v8.0's decision log has been archived — see `.planning/PROJECT.md` Key Decisions table and `.planning/milestones/v8.0-ROADMAP.md` Milestone Summary for the consolidated view. No open decisions carried forward.

- [Phase 83]: CalendarButtonStyleTests.cs written with post-write CRLF conversion (Write tool emitted LF; converted before running tests) to satisfy CLAUDE.md's Windows/CRLF convention
- [Phase 84]: 84-02's four one-way concretizations approved as specified (table shape with RevokedAt tombstone, 32-byte Base64Url address stored plain, `questboard-event-{eventId}` identifiers, `/feeds/calendar/{feedToken}.ics` on its own anonymous controller)
- [Phase 84]: Revoked-subscription tombstones get a bounded retention window (default 30 days) swept by a nightly Hangfire job. This made the `410` temporary, so CALFEED-04 was reworded and CALFEED-17 minted rather than dropping the sweep; 84-04 grew a fifth task
- [Phase 84]: 84-02: feed query rooted at EventSignups (third instance of the pinned-membership tenant-safety pattern), CalendarSubscriptions carries no GroupId/query filter by design, tracer proven over real anonymous HTTP with no active board.
- [Phase 84]: Vote-marker branches on Availability alone, never HasAnswered -- a campaign auto-created Yes row renders identically to a chosen Yes, an accepted cost (84-CONTEXT.md D-18).
- [Phase 84]: 84-04's amendment: revoked calendar subscriptions get a bounded retention window (RetentionDays, default 30) swept nightly by CalendarSubscriptionRetentionJob; past the window a retired address answers 404 like one that never existed
- [Phase 84]: 84-04: rate limiting on the calendar feed is partitioned by the feedToken route value, not client IP, so several members' clients behind one home network never share a budget
- [Phase 84]: Log-safety fact inlines its own harness smoke test (revoke-then-fetch) rather than depending on run order across facts — xUnit gives no ordering guarantee across facts, each of which clears the database independently
- [Phase 84]: Task 3's real-device subscription checkpoint was deferred to deployment by operator decision, not approved and not failed -- Outlook and Google Calendar fetch server-side and cannot reach a localhost or LAN address — Server-side coverage (104 test methods) and an external RFC 5545 validator pass (0 errors, 0 warnings) independently prove the document; client poll-and-render behaviour remains genuinely unverified and is tracked as an open WINDOWS.md unrun-verify item
- [Phase 84]: 84-08: three UAT-found UI defects fixed centrally during the Task 3 review window -- modals freed from a backdrop-filter stacking-context trap, the subscription row rebalanced to 68px, and the address stopped being displayed on screen (kept in DOM, readonly, revealed only by the clipboard-denied fallback) — CALFEED-03 and 84-UI-SPEC E3/E4 amended accordingly; address is a bearer credential with no expiry and should not be visible on a screen-shared or screenshotted page

### Roadmap Evolution

v8.0 shipped exactly as originally roadmapped: 7 phases (65–71), 26 plans, 100% requirement coverage (21/21), no orphans, no ad-hoc scope additions. Full evolution history archived in `.planning/milestones/v8.0-ROADMAP.md`.

- Phase 78 added 2026-08-26: Link Preview Foundation and Quest Cards — Open Graph / Twitter Card unfurls for quest links, gated behind signed share links.
- Phase 79 added 2026-08-26: Character and Contact Link Cards — extends the signed-link mechanism to characters and contacts, including portrait images and the `IsRevealed` spoiler gate.
- Phase 80 added 2026-08-27: Contact Categories — group NPCs under named headings on the Contacts index. From a board user's feature request relayed by the operator.
- Phase 81 added 2026-08-27: Contact Tags and Filtering — many-to-many free-form tags on contacts plus a filter on the index. Same request; the requester staged it after categories, so it is a separate phase and may stay unplanned.
- Phase 82 added 2026-08-29: Personal Cross-Board Event Agenda — every upcoming event across all boards a member belongs to, board named on every row. Raised during Phase 77's discuss pass and deliberately kept off that page.
- Phase 83 added 2026-08-30: Availability Surface Naming and Placement — rename the pair to "My Agenda" / "Board Availability" and move the board-scoped overview's nav entry under the Dungeon Master menu. Naming and discoverability only; a DM-only permission gate was considered and rejected because it would hide less than the agenda already shows.
- Phase 84 added 2026-09-17: Calendar Feed Foundation and Event Subscription — a personal, token-authenticated `text/calendar` feed carrying every event from every board the reader belongs to, subscribable from the Account Profile page on both layouts. Raised by the operator; one-way subscription only, deliberately not CalDAV.
- Phase 85 added 2026-09-17: One-Shot Quests in the Calendar Feed — adds the reader's quest sessions to the same feed, restricted to boards where `BoardType` is `OneShot` at the operator's instruction. Split from 84 because no cross-board quest read exists yet and the board-type predicate is independent of membership.

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
| Phase 83 P01 | 15min | 3 tasks | 4 files |
| Phase 83 P02 | 20min | 3 tasks | 5 files |
| 260831-hz9 | Fix cross-tenant note-injection in ContactsController.AddNote | 2026-08-31 | f732fb81 | [260831-hz9-fix-cross-tenant-note-injection-in-conta](./quick/260831-hz9-fix-cross-tenant-note-injection-in-conta/) |
| 260831-mcb | Contact tags: show tags only for owned contacts by default; non-owned contacts' tags hidden unless the existing ShowHidden toggle is on (universal rule for all DM-tier viewers) | 2026-08-31 | b8b4da56 | [260831-mcb-contact-tags-show-tags-only-for-owned-co](./quick/260831-mcb-contact-tags-show-tags-only-for-owned-co/) |
| 260831-fast | Hide the contact tag filter bar entirely when no tags are visible to the viewer (follow-up to 260831-mcb) | 2026-08-31 | 355e51d4 | — |

## Deferred Items

Items acknowledged and carried forward across milestone closes.

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| requirement | EMAIL-04 — digest session reminder (multiple same-day quests → one email) | Still deferred — same-day quests have never occurred in over a year of operation | v4.0 close |
| requirement | REMIND-02 — combined reminder for multi-quest days | Still deferred — same as EMAIL-04 | v4.0 close |
| tech debt | `GroupSessionMiddleware` redirects on POST — data-loss risk if session expires mid-submission | Still deferred — flagged by code review in Phase 31, not yet fixed | v5.0 close |
| requirement | EMAILMD-02 — real Outlook desktop verification for all 3 quest email templates | Deferred — untestable without production access (real relay + real AppUrl); Gmail-confirmed via operator override for Quest Finalized directly, Session Reminder/Waitlist Promoted on shared-engine grounds | v8.0 close |
| requirement | CALFEED real-device subscription check — a real phone actually subscribing via iOS Calendar, Google Calendar and Outlook, what iOS names the calendar, refresh latency, and a camera QR scan | Deferred to deployment — Outlook and Google fetch server-side from their own infrastructure, so no localhost or LAN address can satisfy them; needs a public tunnel or the deployed app. The document itself is proven (external RFC 5545 validator: 0 errors, 0 warnings) and the server contract has 29 end-to-end HTTP facts, but no client's poll-and-render behaviour has been observed | v9.0, Phase 84 |

## Session Continuity

Last session: 2026-09-18T14:17:56.808Z
Stopped at: Phase 85 context gathered
Resume file: .planning/phases/85-one-shot-quests-in-the-calendar-feed/85-CONTEXT.md

## Operator Next Steps

- Review `.planning/milestones/v8.0-ROADMAP.md` and `.planning/MILESTONES.md` for the shipped-milestone summary
- Run `/gsd-new-milestone` to begin questioning → research → requirements → roadmap for the next milestone
