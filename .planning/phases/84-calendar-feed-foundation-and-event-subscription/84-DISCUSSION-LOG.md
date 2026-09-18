# Phase 84: Calendar Feed Foundation and Event Subscription - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-09-17
**Phase:** 84-calendar-feed-foundation-and-event-subscription
**Areas discussed:** Time / duration / all-day, Token lifecycle and revocation, What each entry says, Feed window and subscribing, Which events reach the feed (raised by the operator at the wrap-up gate)

---

## Time, duration and all-day entries

### What 19:00 means to a phone

| Option | Description | Selected |
|--------|-------------|----------|
| Europe/Amsterdam TZID (recommended) | `TZID` plus a `VTIMEZONE` carrying EU daylight-saving rules; correct anywhere. Becomes the app's first written-down timezone. | |
| Floating local time | No zone at all; the phone renders 19:00 in its own zone. Smallest change, matches the naive model. | ✓ |
| Convert to UTC | Always unambiguous, but needs a source zone anyway, and DST errors surface as every event being an hour wrong. | |

**User's choice:** Floating local time
**Notes:** Accepted cost — a reader abroad sees the wrong hour.

### Event duration

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed 1-hour block (recommended) | Renders reliably without overclaiming; events are informational, so a long block would swallow an evening for a one-line notice. | ✓ |
| Zero-length pin | Honest, since the schema does not know — but renders as an easy-to-miss sliver and clients handle a missing `DTEND` inconsistently. | |
| Fixed evening block (3–4h) | Answers "is my Friday busy?" at a glance; badly wrong for short informational entries. | |

**User's choice:** Fixed 1-hour block
**Notes:** The hour is invented; nothing in the schema supports it.

### Free/busy marking

| Option | Description | Selected |
|--------|-------------|----------|
| Free — `TRANSP:TRANSPARENT` (recommended) | Shows on the calendar without marking the reader busy on an invented duration. | ✓ |
| Busy — `TRANSP:OPAQUE` | Board events defend your evenings; but marks you busy on invented data and blacks out whole days for all-day entries. | |
| Split — timed busy, all-day free | Most accurate of the three; one more branch, and the hour is still invented. | |

**User's choice:** Free — `TRANSP:TRANSPARENT`

### Reminders

| Option | Description | Selected |
|--------|-------------|----------|
| No `VALARM` (recommended) | Both iOS and Google let the reader set a default alert per subscribed calendar, so the choice is theirs once. | ✓ |
| One reminder the day before | Closest to the existing email reminder job; nobody can turn it off per-entry. | |
| You decide | Leave it to research on actual client behaviour. | |

**User's choice:** No `VALARM`
**Notes:** Flagged for research — client handling of alarms in subscribed feeds varies.

---

## Token lifecycle and revocation

### Where the token lives

| Option | Description | Selected |
|--------|-------------|----------|
| Random column on `UserEntity` (recommended) | One URL per person; rotating is one overwrite but kills every device at once. | |
| Dedicated token table | Many named, independently revocable subscriptions, with a last-used timestamp. Roughly doubles the UI surface. | ✓ |
| Signed payload, no storage | No migration, but makes Phase 78's unshipped key-ring persistence a hard prerequisite and still needs stored state to revoke. | |

**User's choice:** Dedicated token table

### Whether the address stays visible

| Option | Description | Selected |
|--------|-------------|----------|
| Keep showing it (recommended) | Stored raw, re-copyable any time; a database dump exposes every live address. | ✓ |
| Show it once, then hide it | Only a hash stored, so a stolen dump yields nothing; no re-copying, no second device, no way to verify the phone's address. | |
| Keep it, hidden behind a click | Masked with a reveal toggle; same database exposure, guards only the screen. | |

**User's choice:** Keep showing it
**Notes:** First asking was dismissed as too jargon-heavy. Re-put in plain language — that the random string in the address *is* the password and there is no login — after which the trade-off was clear.

### What each row offers

| Option | Description | Selected |
|--------|-------------|----------|
| Name, address, delete + last-fetched (recommended) | Enough to tell a live subscription from a dead one before revoking. | |
| Name, address, delete | Minimum that works; no way to tell what is still in use. | |
| Full management — add rename and created-date | Most complete picture; a rename form and its validation on top. | ✓ |

**User's choice:** Full management

### Answer to a revoked or unknown address

| Option | Description | Selected |
|--------|-------------|----------|
| `404` for both (recommended) | A prober learns nothing from the response. | |
| `410` for revoked, `404` for unknown | Tells a well-behaved client to stop asking; confirms to a prober that an address was once real. | ✓ |
| `200` with an empty calendar | Tidiest on the device; reads as "nothing scheduled" and keeps an unknown caller polling forever. | |

**User's choice:** `410` for revoked, `404` for unknown
**Notes:** Derived consequence surfaced at the time and accepted — distinguishing the two forces tombstone rows (a `RevokedAt` column), so there is no hard `DELETE` on this table.

---

## What each entry says

### Where the board name goes

| Option | Description | Selected |
|--------|-------------|----------|
| Prefix the title (recommended) | Survives the truncation a cramped day view applies; noise if you only belong to one board. | ✓ |
| Plain title, board in the description | Cleanest calendar; the board is invisible exactly where you glance. | |
| Plain title, board in the location field | Visible without eating the title; a board is not a place, and a client may try to map it. | |

**User's choice:** Prefix the title

### The description

| Option | Description | Selected |
|--------|-------------|----------|
| Markdown flattened to plain text, capped (recommended) | Markdig and AngleSharp are already dependencies; a new render target, not a new package. | |
| Raw Markdown, as stored | No new code; formatted text shows its raw syntax on the phone. | |
| No description at all | Keeps entries short and pushes detail to the board, where it renders properly. | ✓ |

**User's choice:** No description at all
**Notes:** Retires the ROADMAP's "Markdown leaking into `DESCRIPTION`" risk and removes any need to touch `IMarkdownService`.

### The link back

| Option | Description | Selected |
|--------|-------------|----------|
| My Agenda (recommended) | Always works — membership-scoped, no active board needed; lands on a list rather than the event. | |
| The event's own page | Exactly the thing you tapped — but 404s whenever that board is not active, which is the normal case. | |
| The event's page, falling back to the picker | `SelectGroup` already threads `returnUrl`; real new work on `Details` plus an extra screen. | |

**User's choice:** *(free text)* No link at all — *"people know the url of the website, but being member of multiple boards will clash if you're logged in to board 1, but the link opens something on board 2?"*
**Notes:** Not among the options offered. Confirmed against the code that `EventsController.Details` returns `NotFound()` for a non-active board, which supports the reasoning. Removes the cross-board deep-link problem from the phase rather than solving it.

### Cancelled events

| Option | Description | Selected |
|--------|-------------|----------|
| Mark cancelled, keep visible (recommended) | `STATUS:CANCELLED`; the reader actually learns the session is off. | |
| Remove it from the feed | Reuses the existing `CancelledAt == null` predicate verbatim; the entry vanishes silently. | ✓ |
| Mark cancelled in the title too | Most reliably visible; ugly title and doubled-up signalling. | |

**User's choice:** Remove it from the feed
**Notes:** Accepted cost — a cancellation reaches the subscriber only as a silent disappearance.

---

## Feed window and subscribing

### Which dates

| Option | Description | Selected |
|--------|-------------|----------|
| Rolling window — months back, a year ahead (recommended) | Bounded size regardless of how long the board runs; two numbers somebody picked. | ✓ |
| Upcoming only, from today | Smallest feed; the calendar can never answer "when did we last play?" | |
| Everything, no window | Complete record; the file grows forever and every device re-downloads all of it. | |

**User's choice:** Rolling window
**Notes:** Forces its own query — date range, no `Take`, and no roster include.

### Getting the address onto a phone

| Option | Description | Selected |
|--------|-------------|----------|
| Copy button plus a `webcal://` link (recommended) | No new packages; on a desktop the `webcal` link is useless to you. | |
| Copy, `webcal://` link, and a QR code | Directly solves the desktop-to-phone hop; needs a new package. | ✓ |
| Just the address and a copy button | Nothing to get wrong; every device is a manual paste. | |

**User's choice:** Copy, `webcal://` link, and a QR code
**Notes:** Adds the phase's only new dependency.

### Where it lives

| Option | Description | Selected |
|--------|-------------|----------|
| Its own page, linked from Profile (recommended) | Keeps Profile about name and email; one more page and its mobile twin. | |
| A section on the Profile page | Nothing to navigate to; Profile becomes mostly calendar subscription. | ✓ |
| Profile shows the address, a separate page manages them | Common case never leaves the page; the same information in two places. | |

**User's choice:** A section on the Profile page

### First use

| Option | Description | Selected |
|--------|-------------|----------|
| Empty until you click Add (recommended) | No live address minted for anyone who never wanted one. | ✓ |
| One minted automatically on first view | Lowest friction; every member holds a live bearer URL whether they use a calendar or not. | |
| Empty, but Add is one click with no form | Keeps the opt-in; default names pile up. | |

**User's choice:** Empty until you click Add

---

## Which events reach the feed

Raised by the operator at the wrap-up gate, after all four selected areas were complete. It changed the phase's central query, so it is recorded as its own area.

The operator initially stated a belief that My Agenda is already scoped to events the viewer is signed up for. It is not — Phase 82 D-01 scoped it by board membership and explicitly rejected row-scoping. The operator then observed, correctly, that the two rules coincide on campaign boards because joining backfills a `Yes` row, which narrowed the question to the one-shot case alone.

### One-shot board, event posted, not yet answered

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — show it (matches My Agenda) | Reuses Phase 82's rule verbatim; the one case where the phone tells you something you did not know. | |
| No — only once I've answered | Diverges from My Agenda, needs its own signup-scoped query; the calendar can never warn about a session you have not spotted. | ✓ |

**User's choice:** No — only once I've answered

### Declined events

| Option | Description | Selected |
|--------|-------------|----------|
| Exclude declines (recommended) | Your phone shows what you might attend. | |
| Any row counts | Simplest predicate; the calendar fills with sessions you said no to. | |
| Campaign auto-rows only | Rule reads differently per board type — what Phase 82 D-01 set out to avoid. | |

**User's choice:** *(free text)* — proposed showing every event with a row and carrying the vote in the title, and asked for advice.

**Advice given:** the proposal is sound because `TRANSP:TRANSPARENT` means a declined entry costs only clutter, but it needed two corrections — the marker must be a **suffix**, since `[Board]` already consumes the lead and truncation should sacrifice the marker before the event name; and it must read **four** states rather than three, because a campaign auto-row is `Yes` with `HasAnswered == false` and marking it "Yes" would assert an answer never given.

**Follow-up choice:** suffix marker, but skip `(no answer)` — so `(maybe)` and `(declined)` are marked and everything else renders as a plain title.
**Notes:** Accepted cost — a never-answered campaign event is indistinguishable from an accepted one.

---

## Claude's Discretion

- Absolute base URL from `EmailSettings:AppUrl`, following Phase 78 D-07. Flagged to the operator rather than asked; no objection raised.
- All-day `VALUE=DATE` mapping and its exclusive next-day `DTEND`.
- `UID` scheme — stable across polls, namespaced by source so Phase 85's quests cannot collide with events.
- Rate limiting on the anonymous endpoint.

## Deferred Ideas

- **Quests in the feed — Phase 85.** Operator's rules, stated during this discussion: one-shot boards only, only quests the viewer is signed up for, and only once finalized — never while date voting is running.
- A per-board feed URL, rather than one personal cross-board feed.
- Marking a never-answered campaign event, if the plain title turns out to mislead.
- Renaming `EmailSettings:AppUrl` — already deferred in Phase 78 D-07, and a calendar feature reading it makes the smell worse.
