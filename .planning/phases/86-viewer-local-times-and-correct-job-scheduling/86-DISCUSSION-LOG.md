# Phase 86: Viewer-Local Times and Correct Job Scheduling - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-09-20
**Phase:** 86-viewer-local-times-and-correct-job-scheduling
**Areas discussed:** Mixed-zone legibility, Sweep scheduling zone, Server-rendered times, Which instants convert

---

## Mixed-zone legibility

| Option | Description | Selected |
|--------|-------------|----------|
| Nothing — accept the mix | No marker, no label. Game nights stay bare wall-clock, instants convert silently. Matches Phase 84 D-01's already-accepted cost. | ✓ |
| Label game nights with the board zone | Session times render with an explicit zone so a wall-clock value announces what it means. | |
| Mark converted instants instead | Leave game nights bare; give converted timestamps a tooltip revealing the source UTC. | |
| Label both explicitly | Every rendered time carries a zone. Unambiguous, maximum noise. | |

**User's choice:** Asked for a recommendation first ("What's your advise?"), then accepted the advised position — accept the mix, leave game-night rendering out of scope entirely, and add a `title` attribute carrying the source instant on converted values.

**Notes:** The advice turned on four points: the dangerous value is the game night, not the instant, so if anything were labelled it would be the game nights rather than the converted timestamps; game nights were never broken and were not what the operator reported; leaving them untouched makes "no game night moved" structurally true rather than test-proven; and labelling on the board while the feed silently cannot label would make the two surfaces disagree about how careful they are. The `title` attribute was folded in from the third option as a cheap diagnostic aid.

---

## Sweep scheduling zone

**Q1 — Which timezone should the three nightly sweeps run in?**

| Option | Description | Selected |
|--------|-------------|----------|
| Configurable, Europe/Amsterdam default | Options class with a code default, following the `CalendarFeedOptions` idiom. Job's internal clock reads the same zone. | ✓ |
| Hardcoded Europe/Amsterdam | `TimeZoneInfo` passed directly at each registration, no configuration surface. | |
| Fix the reminder only, leave maintenance on UTC | Only the user-visible sweep gets a zone; the other two keep UTC with corrected comments. | |

**Notes:** One option was ruled out before the question was asked — shifting the cron hours to compensate (`0 7 * * *` to land at 09:00) cannot track DST and would be an hour wrong for half the year. Also separated during framing: only `daily-session-reminders` has user-visible timing; the top-up and retention sweeps need only stay off-peak and distinct.

**Q2 — If the container cannot resolve the zone at startup, what should happen?**

| Option | Description | Selected |
|--------|-------------|----------|
| Fail fast at startup | Refuse to boot, matching `CalendarFeedOptions.IsValid()` and the Production email-config guard. *(Recommended)* | |
| Fall back to UTC and log a warning | Boot anyway, log loudly, keep current behaviour. | ✓ |
| Pin tzdata in the image | Install tzdata in the Dockerfile so resolution cannot fail. | |

**Notes:** Operator chose availability over strictness, against the recommendation. The stated concern with this choice — that today's bug survived precisely because nobody reads log lines — was then addressed by Q3 rather than relitigated.

**Q3 — Should the UTC fallback be observable somewhere other than a log line?**

| Option | Description | Selected |
|--------|-------------|----------|
| Degraded health check | Named health check reporting `Degraded` when the fallback is active; still returns 200 so the docker healthcheck does not restart-loop. | ✓ |
| Log line only | Nothing further to build or test. | |
| Log plus a startup banner | Visible on deploy, invisible once the container has been running. | |

**Notes:** Grounded in the existing bare `AddHealthChecks()`/`MapHealthChecks("/health")` at `Program.cs:41, 363`, which carries no custom checks today and is already polled by `docker-compose.yml`.

**Q4 — Should the phase also fix the ambient "today" reads, or only the job schedule?**

| Option | Description | Selected |
|--------|-------------|----------|
| All of them — one board clock | Every `DateTime.Today`/`Now` compared against a board-local date reads the board zone through one seam. | ✓ |
| Only the reminder job | Smallest change satisfying the goal as written; knowingly leaves a real off-by-one. | |
| All of them, and drop ambient clocks entirely | Same plus clock-by-injection everywhere, making the midnight boundary testable. | |

**Notes:** This question was added mid-area after inspecting the call sites revealed a genuine correctness bug rather than a cosmetic one: `EventSeriesService`, `GroupRepository` and others compute `today` in UTC and compare it against board-local `DateOnly` values, so between midnight and 02:00 board time they operate on yesterday.

---

## Server-rendered times

**Resolved by evidence rather than asked — the email half.** All four transactional templates (`QuestFinalized`, `QuestDateChanged`, `SessionReminder`, `WaitlistPromoted`) render only a quest date formatted `"dddd, MMMM d"` with no time component at all. Every one is a wall-clock game night, out of scope under D-01, with no clock value to convert and no zone to label. ROADMAP.md's "do emails need an explicit zone label" question is moot rather than answered.

**Q1 — What should the server render for an instant before the browser rewrites it?**

| Option | Description | Selected |
|--------|-------------|----------|
| Board zone, then JS swaps | Server renders in the configured board zone; identical to the browser value for a local reader, so no flash. No-JS reader gets board time, consistent with the game nights beside it. | ✓ |
| UTC with an explicit label | Honest but flashes on every page load, and leaves a no-JS reader doing arithmetic. | |
| Render nothing until JS fills it | Never shows a wrong value; no time at all without JS. | |

**Notes:** This option only became available because the sweep-zone decision gives the server a configured board zone to render in — it would not have been on the table before that area was settled.

---

## Which instants convert

**Q1 — Which rendered instants should convert to the viewer's zone?**

| Option | Description | Selected |
|--------|-------------|----------|
| Every rendered instant | Date-only renders included. Rule is a property of the value, not the format string. | ✓ |
| Only where a time-of-day is shown | Smallest diff, targets the reported symptom; leaves a format-string-dependent rule. | |
| Every instant, plus relative rendering | Adds "2 hours ago" for recency-style timestamps. | |

**Notes:** Framed with four grounding findings from the views: the `FinalizedDate ?? ClosedDate` coalesce that mixes both categories through one format string; `DateTime.UtcNow - TransactionDate` arithmetic that must stay in UTC; `SignupTime` appearing only as an `OrderBy` key and needing nothing; and most instant renders being date-only, which is wrong only between 22:00 board time and midnight. Choosing the first option over the third also declines relative rendering.

Four `datetime-local` form round-trip values (`ProposedDates` rendered `"yyyy-MM-ddTHH:mm"`) were identified after this question as explicitly never-touch — wall-clock *and* form values that a blanket sweep would corrupt.

---

## Claude's Discretion

- The shape of the board-clock seam (`TimeProvider` wrapper, dedicated interface, or options-backed service), subject to one seam serving both the cron registrations and the ambient reads.
- The shape of the client-side formatting pass — script location, element marking, `Intl` options — subject to the first-paint and `title`-attribute decisions.
- Which options class carries the zone, or whether a new one is minted.

## Deferred Ideas

- Labelling game-night times with an explicit board-zone marker — revisit if a player relocates outside the board's zone.
- Relative rendering ("2 hours ago") for recency-style timestamps — considered and declined.
- Expressing the instant-versus-wall-clock distinction in the type system — a schema-and-model change, its own phase.
- Per-user timezone preference — not wanted; the browser's resolved zone is the whole mechanism.
