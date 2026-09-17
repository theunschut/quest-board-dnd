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

Task IDs are assigned by the planner. Keyed by real Phase 84 task ids once PLAN.md files exist.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| `84-02-T3` | `84-02` | 1 | CALFEED-05, CALFEED-06 | T-84-01 | Anonymous caller with no cookie and no active board gets a valid `text/calendar` response scoped to their own signups across every board they belong to | integration, end-to-end | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ W0 | ⬜ pending |
| `84-03-T1` | `84-03` | 2 | CALFEED-10, CALFEED-11 | T-84-16, T-84-17 | A timed event emits a one-hour floating-local-time block and a null-start event emits a true all-day entry, both marked transparent | unit (exact-byte) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ❌ W0 | ⬜ pending |
| `84-03-T2` | `84-03` | 2 | CALFEED-09 | T-84-16 | Entry title is `[Board] Title` with `(maybe)`/`(declined)` appended only when the owner answered that way | unit (exact-byte) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ❌ W0 | ⬜ pending |
| `84-03-T3` | `84-03` | 2 | CALFEED-12 | T-84-15 | Entry UID is stable across repeated fetches of the same occurrence and namespaced by source | unit (invariant) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ❌ W0 | ⬜ pending |
| `84-04-T1` | `84-04` | 2 | CALFEED-13 | T-84-21 | Rolling window bounds are read from configuration with no code change required | unit (options validation) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedOptionsValidationTests` | ❌ W0 | ⬜ pending |
| `84-04-T2` | `84-04` | 2 | CALFEED-13, CALFEED-16 | T-84-06 | The feed window is recomputed on every fetch and last-fetched write is throttled to at most once per interval | unit (repository, throttle) | `dotnet test QuestBoard.UnitTests --filter CalendarSubscriptionRepositoryTests` | ❌ W0 | ⬜ pending |
| `84-04-T3` | `84-04` | 2 | CALFEED-04, CALFEED-15 | T-84-02, T-84-05 | A revoked subscription answers 410 Gone, an unknown address answers 404 Not Found, and the feed endpoint is rate limited per address | build + integration | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ W0 | ⬜ pending |
| `84-05-T1` | `84-05` | 3 | CALFEED-15 | T-84-02, T-84-05 | No application log line ever contains a full subscription address, proven under captured logs, and rate limiting is exercised | integration (captured logs, rate limit) | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ W0 | ⬜ pending |
| `84-05-T2` | `84-05` | 3 | CALFEED-06, CALFEED-07 | T-84-01, T-84-23 | The feed carries only events the owner holds a signup row on across their own boards; a row surviving the board predicate but outside membership is dropped and logged as an error | integration, two-group | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ W0 | ⬜ pending |
| `84-05-T3` | `84-05` | 3 | CALFEED-04, CALFEED-08, CALFEED-13, CALFEED-16 | T-84-04, T-84-06 | A cancelled event never appears, the window's bounds are enforced at their edges, and the fetch-time throttle holds under a hammered address | integration | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ W0 | ⬜ pending |
| `84-06-T3` | `84-06` | 3 | CALFEED-01, CALFEED-02, CALFEED-03 | T-84-07, T-84-26 | Add mints exactly one new 256-bit random subscription; multiple subscriptions stay independently named, addressed and revocable | build + full suite | `dotnet test` | ❌ W0 | ⬜ pending |
| `84-08-T1` | `84-08` | 5 | CALFEED-03, CALFEED-14 | T-84-30, T-84-33 | Every subscription row's name, address, created date, last-fetched timestamp, copy control, webcal link and QR code render on both Profile layouts | integration (markup + round trip, both layouts) | `dotnet test QuestBoard.IntegrationTests --filter ProfileCalendarSubscriptionTests` | ❌ W0 | ⬜ pending |
| `84-08-T2` | `84-08` | 5 | CALFEED-14 | T-84-32, T-84-35, T-84-36 | The Calendar Subscription section renders under a real mobile User-Agent, not just a narrow desktop viewport | integration (static guard) | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionStaticGuardTests` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/CalendarFeedWriterTests.cs` — exact-byte assertions for timed and all-day VEVENT output, escaped TEXT values, folding at 75 octets, CRLF line endings
- [ ] `QuestBoard.IntegrationTests/CalendarSubscriptionFeedTests.cs` — two-group tenant isolation, D-08 status codes, D-12 cancellation exclusion, D-17 signup-row gating, D-07 throttled fetch timestamp
- [ ] `QuestBoard.IntegrationTests/ProfileCalendarSubscriptionTests.cs` — add/rename/revoke round trip, both-layout markup under a real mobile User-Agent (`MobileDetectionMiddleware` test convention)
- [ ] `QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs` — configurable rolling-window bounds validation (CALFEED-13), not anticipated by the research pass
- [ ] `QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs` — throttled last-fetched write (CALFEED-16), not anticipated by the research pass
- [ ] `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs` — both-layout static guard (CALFEED-14), not anticipated by the research pass
- [ ] `QuestBoard.IntegrationTests/Helpers/CapturingLoggerProvider.cs` — log-capture harness the address-free-logging fact (CALFEED-15) depends on, not anticipated by the research pass
- [ ] No framework install needed — xUnit v3 and FluentAssertions are already solution-wide dependencies

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| A real phone subscribes to the address and renders the events | Phase goal | No automated harness can drive Apple/Google's own poller or renderer | Mint a subscription on Profile, add the `webcal://` address to iOS Calendar and to Google Calendar's "From URL", confirm the calendar name, the timed block, the all-day entry, and that entries do not mark the reader busy |
| RFC 5545 conformance beyond the golden files | D-01..D-13 | The validator found (`icalendar.dev/validator/`) appears browser-based with no confirmed scriptable interface — not a CI gate | Paste a live feed response into the validator before ship; treat failures as bugs in the writer |
| A change reaches a device, and how fast | Open research item 2 | Client poll cadence is vendor-chosen and not observable from the server | Edit an event, note the time, watch both clients until the change appears; record the observed latency rather than promising one in UI copy |

All three manual verifications above are discharged by task `84-08-T3`, a blocking `checkpoint:human-verify` in plan `84-08`, so the manual gate has an owner rather than floating.

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
