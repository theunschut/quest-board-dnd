---
gsd_state_version: "1.0"
milestone: v9.0
milestone_name: Rolling Improvements
current_phase: 87
status: completed
stopped_at: Phase 87 complete — all phases complete
last_updated: "2026-09-22T06:51:38.481Z"
last_activity: 2026-09-22
last_activity_desc: Phase 87 complete
state_head: c79ae880606857c5990fb9b153a4e68c9c4d1ccc
progress:
  total_phases: 16
  completed_phases: 14
  total_plans: 108
  completed_plans: 99
  percent: 88
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-09-22 — after Phase 87)

**Core value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.
**Current focus:** None — Phase 87 closed. Next phase not chosen.

## Current Position

Phase: 87 (cross-board-deep-link-recovery) — COMPLETE
Plan: 4 of 4 — verified 21/21, security SECURED, review closed
Status: Between phases — three roadmapped phases remain unstarted
Last activity: 2026-09-22 — Phase 87 complete

Roadmapped and not started: Phase 78 — Link Preview Foundation and Quest Cards (9 plans written,
none executed) and Phase 79 — Character and Contact Link Cards (not yet planned). Every other
phase in the 72–87 range is complete with a verification report.

Note: `roadmap.analyze` reports `roadmap_complete: false` for all 16 phases, including ones
shipped milestones ago. This ROADMAP.md records completion as `**Plans:** N/N plans complete`
rather than a checkbox on the phase heading, which is what the analyzer reads — so the derived
phase counters understate progress. Pre-existing and consistent across every phase; not
introduced by Phase 87.

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

- [Phase 87]: D-12 kept -- a SuperAdmin following a deep link to a board they are not a member of gets the same 404 as anyone else. The operator hit it live, questioned it, then reaffirmed it. A SuperAdmin can already reach any board by picking it explicitly, so the 404 costs a click rather than a capability; auto-repointing by an attacker-influenceable link would be unbounded across every board for the highest-authority account; and a role branch would break the structural no-branch parity the oracle tests rest on
- [Phase 87]: The three mobile banner defects found at human verification were fixed inside 87-04 rather than deferred to gap closure -- contained CSS plus one button class, and the executor still held its worktree
- [Phase 87]: The picker-skip path was accepted on 9 integration facts instead of a live browser check, because the branch is gated on `!isSuperAdmin` and so is structurally unreachable from the operator's own account
- [Phase 87]: The security auditor was spawned rather than taking secure-phase's `threats_open: 0` short-circuit -- the short-circuit requires asserting the mitigations exist, which would have rubber-stamped the phase's own claims about a deliberate tenancy-filter bypass
- [Phase 87]: Code review's WR-01 was fixed rather than accepted. The registry documented itself as a table an action could never silently join, but its key omitted the MVC area; the guarantee is now true rather than nearly true. Both Info findings fixed too. `IgnoreQueryFilters()` on `UserGroups` was kept despite bypassing no filter today -- if membership ever gains a board-scoped filter, removing it would break cross-board resolution as a plausible-looking "not a member" rather than as an error
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
- Backlog 999.1 parked 2026-09-18: Voting from a Phone Calendar Entry — operator asked whether a vote could be cast in the phone calendar entry itself and flow back to the board. Answered no for the shipped subscription: an `.ics` URL subscription is one-way and every major client renders it read-only, so no feed property produces an RSVP button. The two routes that would work (iMIP email invitations, which need inbound mail this codebase does not have; or a token-linked tap-through page, which reverses 84 D-10 and makes a leaked feed address write-capable) were both judged disproportionate for now. Quest date voting is unreachable on any route because the feed carries finalized quests only.
- Phase 86 added 2026-09-18: Viewer-Local Times and Correct Job Scheduling — render real instants in the viewer's browser timezone (client-side `<time>` + `Intl`, operator's decision) and give the three Hangfire sweeps an explicit `TimeZoneInfo`. Raised from the UTC "Last fetched" timestamp on the Profile page; investigation also found the container sets no `TZ` and Hangfire cron defaults to UTC, so the sweeps never ran at the CET/CEST hour their comments claim. Both halves ship together because classifying every `DateTime` as a real instant or naive wall-clock is the shared bulk of the work.
- Phase 87 added 2026-09-21: Cross-Board Deep Link Recovery -- a member following a link to another board they belong to should land on the page instead of a 404. Raised by the operator from live two-board use, and explicitly a revision of the strict session-scoped tenancy decision rather than a bug against it. The 18 global query filters keyed on `ActiveGroupId` make a foreign-board read indistinguishable from a nonexistent one, so the controller cannot answer differently today. Phase 82's Agenda confirm-then-switch modal is the pattern to generalise; the non-negotiable constraint is that a non-member must see no new signal, so the recovery flow cannot become a board-membership oracle.

### Pending Todos

None captured for v8.0. Two small deferred toolbar features (EDITOR-07/08/09 — strikethrough, horizontal rule, cheatsheet link) logged in `.planning/PROJECT.md` Requirements → Active for a future milestone to pick up if requested.

### Blockers/Concerns

None open for v8.0. Carried forward from prior milestones, still unresolved:

- `GroupSessionMiddleware` redirects on all HTTP verbs including POST — a POST-body data-loss risk if the session expires mid-submission; flagged by code review during Phase 31, not yet fixed.
- `Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` appears to be dead code (Platform area's `_ViewStart.cshtml` never selects it) — discovered during Phase 42 research, deliberately left unfixed as out-of-scope for that phase. See PROJECT.md Known Issues.
- `GuildMembersController.Edit` POST's `SetAsMainCharacterAsync` demotion guard can never be true (dead code, predates Phase 56) — found during Phase 56 verification, flagged as a separate follow-up task, not yet actioned. See PROJECT.md Known Issues.

New as of 2026-09-22:

- Several unit tests pin a `FakeBoardClock` to a fixed date but build their fixtures from `DateTime.UtcNow`, so the gap they assert shrinks by a day for every real day that passes. `QuestServiceTests`' three completed-quest tests were fixed after one went red two days after being written; `EventSeriesMaterializationTests`, `GroupRepositoryTests`, `EmailConfirmationJobGuardTests`, `EventSeriesServiceTests` and `DailyReminderJobTests` still carry the construction. Not urgent, but each will fail on its own schedule.
- Phase 87's human verification carries three items the operator accepted conditionally rather than proved: the board-switch banner was checked with a Pixel 8 user-agent override at 375x812 rather than on a physical device; the picker-skip path was never exercised from a real non-SuperAdmin account; and the overall approval was "approved for now, tell me if anything is broken". If friction is reported later, the useful question is which part — the switch being unasked-for reopens a design decision, the banner is styling.

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

Last session: 2026-09-22T06:56:55.758Z
Stopped at: Phase 87 closed — gate tail finished (code review, review fixes, regression gate, verification, roadmap)
Resume file: none — no work in flight

## Operator Next Steps

- Use the board across two boards for a few minutes and say whether the automatic switch removes friction or is more surprising than the old 404 — Phase 87's approval was explicitly conditional on this
- Check the board-switch banner on a real phone; the mobile pass used a user-agent override, and this codebase selects mobile views by user agent rather than viewport
- Decide whether to stop the `mssql-dev` container left running from the previous session (named volume `mssql-data`, so data persists either way)
- Phase 78 (Link Preview Foundation and Quest Cards) is planned with 9 plans and ready to execute; Phases 79 and 81 are roadmapped but unplanned
- v9.0 has no fixed end state — run `/gsd-new-milestone` only when you decide to cut it
