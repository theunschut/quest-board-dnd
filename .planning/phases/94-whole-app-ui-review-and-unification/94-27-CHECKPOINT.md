# Decision needed: the seven follow-up phases after Phase 94

Nothing has been added to the roadmap yet. Appending only happens after you answer. It can be undone before a phase starts.

## The proposed phases, in order

| Phase | Name | Covers | Size | Why it is here |
|---|---|---|---|---|
| 95 | Quests and Quest Log | Quest board, quest details, create and edit forms, DM manage, quest log, move page, the shared Markdown editor, signup modal and vote buttons; also the desktop top bar fix | Large: 25 view files, 13 stylesheets | It is the core loop, and the shared pieces later phases reuse live here |
| 96 | Account and pickers | Sign in, profile, password pages, board picker, access-denied page, phone landing page | Small: 17 view files, 1 stylesheet | A keyboard user cannot get past the board picker today; small and quick to ship |
| 97 | Events, Series, Calendar, Agenda | Calendar (desktop month that fits without scrolling), agenda, events overview, event details, series, shared calendar pieces | Medium: 17 view files, 6 stylesheets | It reuses the calendar piece after phase 95 has patched its vote buttons |
| 98 | Characters, Contacts, DM profile, Players | Characters, contacts, contact categories, DM profile, players list, image crop dialog | Large: 26 view files, 13 stylesheets | Needs the editor fix from phase 95; settles the crop dialog |
| 99 | Shop and Shop management | Shop and its management pages in the navy theme, new spot for the floating merchant button, the broken Return, Sell and Preview controls | Medium: 11 view files, 6 stylesheets, but the messiest colour code | Needs the theme decided (done) and the item-create bug fixed first |
| 100 | Admin and Platform | Admin pages and the Platform area, readable badges and tables, three Platform twins kept | Medium: 25 view files, 6 stylesheets | Depends on the Platform layouts being proven, which they now are |
| 101 | Email templates | All 10 email templates restyled with inline styles; every message tested by a real send to your inbox | Small to medium: 10 templates | Independent of the web pages; needs you for the test sends, so it can run any time |

## Deliberately outside the cut

These are bug fixes, not look-and-feel work, so they are not phases:

- Shop item create fails (missing board on the new item). Fix it on its own before phase 99.
- The `WallClockUnmovedTests` flake that fails in the first minute after 20:00 UTC. A small test-only fix.

## Two things I changed or noticed while checking

- The desktop top bar fix (links wrapping between 992 and 1280 px) was approved in principle but had no owner. I put it in phase 95. Say so if you want it elsewhere.
- The phone landing page now sits with Account and pickers, not Quests; the cut text still listed it twice, and I fixed that.

## Coverage proof

Passed: 121 view files, 45 stylesheets and 10 email templates each belong to exactly one phase, zero missing, zero duplicated. The two big shared stylesheets are split by line range and cover every line.

## Release caveat to weigh when choosing the order

Each phase can be held at the draft-release approval step, which holds the production deployment. But the public container image is published on every tag push regardless, so a held release is already public as an image, and self-hosters pulling `latest` get it at once.

## Your options

1. **Approve as proposed.** Order chosen for the core loop first and the audit's findings. No downside beyond the order trade-offs above.
2. **Approve with changes (state the order, splits or merges).** You control the sequence. The coverage check is re-run on the changed cut before anything is appended.

Reply `approve`, or `approve with changes:` followed by the changes.

The full per-phase detail (scope, before and after for every approved relayout, merges) is in `~/qb-ui-review/phase-94/signoff/cut.md`.

---

## Traceability (for the record, not for the decision)

- Plan 94-27, Task 2 (checkpoint:decision, gate blocking). Task 1 (tracer) done and committed 1753ca09; Task 3 not started.
- Draft labels to numbers: Phase 1 -> 95, Phase 5a -> 96, Phase 2 -> 97, Phase 3 -> 98, Phase 4 -> 99, Phase 5b -> 100, Phase 6 -> 101. Phase 999.1 stays in the backlog.
- Decisions served: D-06 (cut approved before append), D-07 (release caveat), D-18 and D-19 (email phase), D-05, D-04 (before/after for relayouts: 31 relayout rows, 21 "not yet drawn", 10 with Direction A mockups).
- Edits to 94-AUDIT.md in Task 1: Phase 1 Views bullet (Home/Index removed, 10 pairs and 20 files, vote UI described as a scoped patch), Phase 3 Views bullet (no longer lists the signup modal or editor as its own), Phase 5a Views bullet (Home/Index owned, 17 files), the Phase 5a and Phase 4 Stylesheets bullets (stale `site.css` references now point at `legacy-pages.css`), adjustment 12 added (Q-127 to Phase 1), "eleven items" now "twelve".
- Coverage script: `~/qb-ui-review/phase-94/plans/94-27/coverage.py`; cut file generator: `make-cut.py` in the same folder.
- `home.mobile.css` stays under Phase 95: it is linked only by `Quest/Index.Mobile.cshtml`, not by the Home page (checked after approval).
- Out of this plan: Shop item create fix (`deferred-items.md`), WallClockUnmovedTests flake.
