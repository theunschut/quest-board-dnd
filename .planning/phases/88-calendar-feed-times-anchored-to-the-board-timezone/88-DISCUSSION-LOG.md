# Phase 88: Calendar Feed Times Anchored to the Board Timezone - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md. This log preserves the alternatives considered.

**Date:** 2026-09-30
**Phase:** 88-calendar-feed-times-anchored-to-the-board-timezone
**Areas discussed:** Scope (raised by operator), The two phones, How the zone is written, Zone fell back to UTC, Fixing existing entries

---

## Scope (per-board timezone, raised by the operator mid-discussion)

The operator proposed a timezone setting on each board (default Amsterdam), used by the calendar
subscription for that board's events and quests.

| Option | Description | Selected |
|--------|-------------|----------|
| Own phase after 88 (Recommended) | 88 fixes the feed and writes the zone on each entry from the one board zone; a new phase makes the clock per-board everywhere | |
| Fold into 88 | Board timezone field and migration, per-board clock across about 15 call sites, per-zone reminder scheduling | |
| Backlog it | Park as a 999.x item; 88 still writes the zone on each entry | |

**User's choice:** Freeform. The operator does not want or need the automatic jobs to change zone.
The feed just needs to write the zone onto each quest and event time so the subscription shows the
right time.
**Notes:** Recorded as one resolved board zone on every timed entry, with per-board zones
deferred. The operator also asked why the app falls back to UTC. Answered: only when the
configured id cannot be resolved, per Phase 86 D-04, which was the operator's own choice. The
operator checked production `/health`, which reported `Healthy`.

---

## The two phones

| Option | Description | Selected |
|--------|-------------|----------|
| iPhone, Apple Calendar | Subscribed directly; Apple fetches | ✓ (operator) |
| Google Calendar | Google's servers fetch | ✓ (friend) |
| Outlook | Microsoft's servers fetch | |
| Not sure / Samsung Calendar | Unknown or a different route | |

| Option | Description | Selected |
|--------|-------------|----------|
| Same quest, before 25 Oct | One summer-time entry | |
| Same quest, after 25 Oct | One winter-time entry | |
| Different quests/dates | Two separate entries | ✓ |
| Not sure | Unconfirmed | |

**Notes:** The operator then gave the actual state. Event: iPhone 18:00 (correct), Google 20:00
(wrong). Quest: iPhone reportedly 19:00, Google unknown. That suggested a second bug in the quest
path. Code tracing showed the quest time is copied without conversion at every step, so the
operator was asked to fetch the live feed (a PowerShell `curl.exe` command, run by the operator,
with only the `DTSTART` line pasted back). The bytes read `DTSTART:20261002T180000`, and the
operator confirmed they had misread the iPhone. Conclusion: Apple is correct, and only Google is
wrong.

| Option | Description | Selected |
|--------|-------------|----------|
| Google + iPhone (Recommended) | The broken route plus a guard on the working one, both checked on production | ✓ |
| Google + iPhone + Outlook | Adds an app nobody reported using | |
| Google only | Apple inferred from the bytes | |

---

## How the zone is written

| Option | Description | Selected |
|--------|-------------|----------|
| UTC with a trailing Z (Recommended) | Converted per entry date; no VTIMEZONE; the zone name is not in the file | |
| TZID + generated VTIMEZONE | The standard route; zone name per entry; rules differ between Windows and Linux | leaned |
| X-WR-TIMEZONE header only | One line; non-standard; applies to the whole calendar | |

**User's choice:** Leaned toward TZID and asked what the Windows/Linux difference is.
**Notes:** Explained that both platforms agree on the offset at a moment but describe the rules
in different shapes, so a translated block differs by platform. Offered a fixed-date variant that
only asks for offset-at-a-moment. The operator then asked whether a board timezone would simplify
it. Answered: no. The zone is already known and the rules are the hard part; per-board zones
would add blocks. Hard-coding Amsterdam is the only real shortcut, and it breaks self-hosters.

| Option | Description | Selected |
|--------|-------------|----------|
| TZID + fixed-date changes (Recommended) | Generated from the configured zone; same bytes on both platforms | ✓ |
| TZID + hard-coded Amsterdam | Least code; wrong for other zones | |
| TZID + yearly rule | Compact; platform-shaped | |
| UTC with Z | Simplest output; no zone name | |

| Option | Description | Selected |
|--------|-------------|----------|
| No X-WR-TIMEZONE (Recommended) | Nothing zoneless left for it to apply to | |
| Yes, as a hint | One line; Google may use it as the display zone | ✓ |

---

## Zone fell back to UTC

| Option | Description | Selected |
|--------|-------------|----------|
| Zoneless, as today (Recommended) | Apple stays right, Google stays wrong; never claims a zone it does not have | |
| Declare UTC | Matches the clock; every phone shows 18:00 as 20:00 in summer | ✓ |
| Refuse to serve (503) | Clients keep their last copy; blocks correct updates | |
| Refuse to start instead | Reverses 86 D-04; a crash loop under docker-compose | |

**User's choice:** Declare UTC.
**Notes:** Recorded as "always declare exactly the resolved zone, no degraded branch". The cost
was stated back to the operator: game nights show one to two hours late on every phone while the
zone is broken.

---

## Fixing existing entries

| Option | Description | Selected |
|--------|-------------|----------|
| Bump SEQUENCE to 1 (Recommended) | One literal; helps clients that compare revisions | ✓ |
| Change nothing | Trust replacement by UID; each retry costs up to a day on Google | |
| New UIDs, once | The cancellation path; per-entry alerts lost; duplicate risk | |

| Option | Description | Selected |
|--------|-------------|----------|
| Rotate UIDs, then re-check (Recommended) | Existing entries must self-correct; re-subscribing is not a fix | |
| Re-subscribing is fine | Tell the group to re-add the subscription once if old entries stick | ✓ |

---

## Claude's Discretion

- How the zone reaches the writer (per entry or per document), with every zone field sourced
  from `IBoardClock.TimeZone`.
- `TZID` string form (IANA preferred; Windows-id conversion) and the UTC-fallback id.
- `VTIMEZONE` span, the leading observance, and whether a feed with no timed entries emits it.
- `X-WR-TIMEZONE` placement among the calendar headers.

## Deferred Ideas

- Per-board timezone setting, with a board-aware clock (see CONTEXT.md `<deferred>`).
