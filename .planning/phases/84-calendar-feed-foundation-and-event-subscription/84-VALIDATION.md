---
phase: 84
slug: calendar-feed-foundation-and-event-subscription
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-17
---

# Phase 84 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Seeded from `84-RESEARCH.md` `## Validation Architecture`. The per-task map is filled once PLAN.md task IDs exist.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`xunit.v3` 3.2.2) + FluentAssertions 8.10.0 — already solution-wide |
| **Config file** | none dedicated — standard `dotnet test` per-project convention |
| **Quick run command** | `dotnet test QuestBoard.UnitTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30s quick / full suite dominated by `QuestBoard.IntegrationTests` |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green, plus one manual `curl` or real-device check against at least one real calendar client (Apple / Google / Outlook) — unit and markup tests cannot prove a phone renders the feed correctly
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

Task IDs are assigned by the planner. Until PLAN.md exists, the map below is keyed by CONTEXT.md decision; `/gsd-validate-phase` converts these rows to task IDs.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | D-01/D-02/D-03/D-13 | — | N/A | unit (golden-file / exact-byte) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-17 | — | Feed excludes a one-shot event the viewer holds no signup row on; includes a campaign auto-row | integration, two-group | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-08 | — | Revoked token → 410 Gone; unknown token → 404 Not Found; live token → 200 `text/calendar` | integration | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | Tenant isolation (second-layer re-check) | — | A dropped membership predicate is caught in-memory and `LogError`'d, never emitted to a subscriber | integration, two-group | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-12 | — | A cancelled event with a live signup row does not appear in the feed | integration | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-07 | — | Rename/revoke round-trip works; last-fetched write is throttled so a hammered address cannot become a write storm | integration + unit | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-15/D-16 | — | Subscription section renders on both Profile layouts; nothing is minted until Add is pressed | integration (markup, both desktop and real mobile User-Agent) | `dotnet test QuestBoard.IntegrationTests --filter ProfileCalendarSubscriptionTests` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/CalendarFeedWriterTests.cs` — exact-byte assertions for timed and all-day VEVENT output, escaped TEXT values, folding at 75 octets, CRLF line endings
- [ ] `QuestBoard.IntegrationTests/CalendarSubscriptionFeedTests.cs` — two-group tenant isolation, D-08 status codes, D-12 cancellation exclusion, D-17 signup-row gating, D-07 throttled fetch timestamp
- [ ] `QuestBoard.IntegrationTests/ProfileCalendarSubscriptionTests.cs` — add/rename/revoke round trip, both-layout markup under a real mobile User-Agent (`MobileDetectionMiddleware` test convention)
- [ ] No framework install needed — xUnit v3 and FluentAssertions are already solution-wide dependencies

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| A real phone subscribes to the address and renders the events | Phase goal | No automated harness can drive Apple/Google's own poller or renderer | Mint a subscription on Profile, add the `webcal://` address to iOS Calendar and to Google Calendar's "From URL", confirm the calendar name, the timed block, the all-day entry, and that entries do not mark the reader busy |
| RFC 5545 conformance beyond the golden files | D-01..D-13 | The validator found (`icalendar.dev/validator/`) appears browser-based with no confirmed scriptable interface — not a CI gate | Paste a live feed response into the validator before ship; treat failures as bugs in the writer |
| A change reaches a device, and how fast | Open research item 2 | Client poll cadence is vendor-chosen and not observable from the server | Edit an event, note the time, watch both clients until the change appears; record the observed latency rather than promising one in UI copy |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
