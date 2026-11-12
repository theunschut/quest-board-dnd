---
status: testing
phase: 97-events-series-calendar-and-agenda-ui-conversion
source: [97-VERIFICATION.md]
started: 2026-11-13T00:09:22+01:00
updated: 2026-11-13T00:09:22+01:00
deferred_until: after deploy
---

## Current Test

number: 1
name: Real-phone pass over Calendar, Agenda, Events/Index, Events/Details and Series
expected: |
  Phone layout served on each page; every control at least 44px (except the known 22px inline series link in the runway alert); nothing scrolls sideways at 390; the day list and Agenda read in order
awaiting: user response (deferred until after deploy)

## Tests

All items are deferred until after the v6.2.0 deploy: they need a real phone, a screen reader or production. They do not block the release.

### 1. Real-phone pass over Calendar, Agenda, Events/Index, Events/Details and Series
expected: Phone layout served on each page; every control at least 44px (except the known 22px inline series link in the runway alert); nothing scrolls sideways at 390; the day list and Agenda read in order
result: [pending, deferred until after deploy]

### 2. Screen-reader pass over the month: an entry, a "+N more" button, the open day panel and an inert linked entry
expected: Each entry announces its full name (status, time, DM, key, own vote); "+N more" announces the hidden count; the panel announces its heading and returns focus to "+N more" on close; the inert linked entry reads its board and time and is not focusable
result: [pending, deferred until after deploy]

### 3. Production: moved date lines read as before deploy
expected: The date lines moved onto the wall-clock helper read the same on production as before deploy (they format with the host culture, which was never inspected on production)
result: [pending, deferred until after deploy]

### 4. Desktop browser: "+N more" day panel and detail card behaviour
expected: Focus moves into the panel, Tab loops inside it, Esc, Close and an outside click return focus to "+N more"; the detail card opens on hover and on first tap, and the second tap follows the link
result: [pending, deferred until after deploy; scratch Playwright run passed 211/211 before deploy]

### 5. Desktop browser: the four scripts that replaced inline code (event-details, event-series-form, agenda, row-link)
expected: Each page behaves as before: Events/Details answers, the series form's recurrence controls, the Agenda board filter, and whole-row links on Events/Index
result: [pending, deferred until after deploy; scratch sweeps passed with zero console errors]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps
