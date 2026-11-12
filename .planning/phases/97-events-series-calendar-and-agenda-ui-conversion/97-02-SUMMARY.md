---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 02
subsystem: review-environment
tags: [ui-conversion, fixtures, playwright, baseline, before-capture]
requires: ["97-01"]
provides:
  - "fixtures.json (39 keys), manifest.json (46 rows, 6 afterOnly), contrast-exceptions.json, titles.json under ~/qb-ui-review/phase-97"
  - "tools: fixtures.mjs (--refresh-today), manifest.mjs, probes.mjs (fit fields), sweep.mjs (WIDTHxHEIGHT sizes), date-lines.mjs, register97.mjs, before-summary.py"
  - "frozen baseline (BASELINE_SHA 14fcdefb77ff0aaa8484059c4f0bbc3b70ad38f3) serving on port 8301"
  - "BEFORE capture: before/report.json (224 records), before/summary.md, before/date-lines.json, tests-before.txt"
affects: [97-03, 97-05]
key-files:
  created: []
  modified: []
decisions:
  - "The two new fixture boards joined the existing linked set 'Audit Pair of Lanterns' (the Audit Board is already in it; a board belongs to one set only)"
  - "Phase-97 accounts p97player, p97dm2, quiet (and role alias p97agenda) replace player and dm2 wherever fixtures need them"
metrics:
  completed: 2026-11-12
status: complete
actuals:
  tokens: 0
  tasks: 3
  commits: 1
---

# Phase 97 Plan 02: Fixtures, tools, frozen baseline and BEFORE capture Summary

Every fixture the Events, Series, Calendar and Agenda captures need now exists (made only through the app's own forms), the
sweep, probe, manifest and date-line tools are in place, a frozen copy of the starting commit answers on port 8301, the test
inventory is recorded and BEFORE is captured for 40 manifest rows under both User-Agents. No repository source file changed;
all artifacts live in `~/qb-ui-review/phase-97`.

## Fixtures (form used per key)

Second full run of `fixtures.mjs`: **created 0** (fixtures.json: 39 keys, 35 with `created: true`). The crowded-day counts
were re-verified as p97player: day 9=1, 11=2, 12=7, 13=3, 14=4, 18=5, 20=6.

| Keys | Form |
|---|---|
| user.p97player, user.p97dm2, user.quiet (accounts) | Admin > Create user, Admin > Reset password (`register97.mjs`); p97dm2 building key via Admin > Edit user (previous value false) |
| board.linked-member (Emberfall Wardens), board.linked-inert (Saltmarsh Concord), board.quiet (Quiet Hollow) | Platform > Group > Create (One-Shot) |
| memberships (p97player Player on Emberfall; p97dm2 DM on Emberfall and Saltmarsh; quiet Player on Quiet Hollow, removed from the Audit Board) | Platform > Group > Members add / remove |
| link.set | Platform > Linked boards > Add board |
| day.crowded7, event.allday, event.timed, 5 fillers, event.today, event.linked-near, month.fiveweek events, event.oneshot-answered's event | Events/Create |
| series.weekly (Sundays, no end), series.short (Saltmarsh, ends a week after its first occurrence), linked-member series, series.empty | Events/Create with the recurrence box |
| event.cancelled and the two linked cancelled occurrences | Events/Details > Cancel Occurrence |
| quest.voting (p97player votes Yes, Maybe, No), quest.finalized (p97dm2, key, p97player selected), quest.nokey (dm, no key, finalized), 4 linked quests | Quest/Create, Quest/Details signup, Quest/Manage finalize |
| event.oneshot-answered (p97player Yes) | Events/Details Yes button |
| month.crowded, month.fiveweek, month.empty, day.crowded1..6 | computed positions in the crowded month; verified by fetching the calendar |

Months: crowded **May 2027** (six weeks), five-week **January 2027**, empty **January 2029** (verified empty as p97player).
Day counts: 1 = May 9, 2 = May 11, 3 = May 13, 4 = May 14, 5 = May 18, 6 = May 20, 7 = May 12.
`series.empty` is **available** (id 7): the form accepted a series anchored and ended in the past; its page reads
"No occurrences have been generated yet."

Linked set (see deviation 2): "Audit Pair of Lanterns" holds Audit Board, **Emberfall Wardens**, **Saltmarsh Concord**,
The Hollow Vault of Nehru, The Quiet Hollow of Orrin. Quiet Hollow is in no set.

## Baseline, tests, BEFORE

- BASELINE_SHA `14fcdefb77ff0aaa8484059c4f0bbc3b70ad38f3` (ancestry check against c781090c passed); extracted with
  `git archive` into `baseline-src/` and served on 8301 (a second `run-app` of the repository tree on 8301 exits 1).
  The working tree serves on 8300. Both apps are left running.
- Test inventory (`dotnet test --list-tests`, tests-before.txt): UnitTests **1378** entries (1038 distinct methods),
  IntegrationTests **1857** entries (1424 distinct methods).
- Fixture title lengths (titles.json, 34 titles): average 22.9, median 22, longest 39, shortest 10.
- BEFORE: 224 records, 0 not 200, no failed or unevaluated asserts, all from the baseline on 8301. It already shows the
  v6.0.2 card surface (stated at the top of before/summary.md). Headline numbers: 1796 contrast failures (all inside main,
  default conservative scoring), Events/Details phone overflow at 390 is scrollWidth 394 (research measured 398), calendar
  `pageScrolls` is true at 1280x720 (+223px) and 1440x900 (+43px) for cal-crowded and cal-sixweek, with cellOverflow days
  12, 14, 18, 20 at both sizes. The 18 date lines are recorded in before/date-lines.json.

## Deviations from Plan

1. **[Orchestrator override] Ports 8300/8301 and role substitution.** Phase 96 was capturing concurrently, so p97player
   replaces `player` (votes, answers, Emberfall membership, manifest viewer), p97dm2 replaces `dm2` (linked-board content,
   runway alert), `quiet` as planned. The agenda-filtered-out row uses `p97agenda`, a second role key of p97player's account, so
   the session-held filter has its own login state (the plan used player2). No existing qb-audit-* account was changed.
   Existing accounts (dm, player2, admin, noboard, superadmin) are only signed in as, plus `dm` creating Audit Board content.
2. **[Rule 3] Linked set.** The Audit Board already belongs to a phase-94 set; a board belongs to one set and a set holds
   one board type, so the plan's three-board set and the "opposite board type" idea were impossible. The two new One-Shot
   boards were added to that existing set (five boards now). The Audit Board is itself One-Shot, so the one-shot answer
   fixture lives on it.
3. **Building-key roles swapped.** The existing dm and dm2 hold no key and may not be changed, so p97dm2 holds the key
   (finalized quest by p97dm2 shows the check) and the no-key quest is by the keyless existing `dm`.
4. **Tooling base.** probes.mjs and sweep.mjs were rebuilt from the phase-96 revisions (they already carry the text-spacing
   probe and the afterOnly handling) and extended with the fit fields, `WIDTHxHEIGHT` sizes, `{key:prop}` path placeholders
   and an `uncheckAll` action; `controlBorders` came along from phase 96.
5. **BEFORE port.** The tracer swept the working tree on 8300 (identical commit); the full BEFORE sweep ran on the frozen
   baseline on 8301.
6. **Source quest ids.** Fixture quest ids are the app's own (about 992049 and up).

## Known limits

- `--stack` (glass-stack contrast scoring from phase 96) was not used; the contrast count is the conservative default.
- afterOnly rows carry paths and roles only; the slices that create those states fill in their actions.

No repository file is added or changed by this plan. No auth gates. No stubs. No new threat surface.

## Self-Check: PASSED

fixtures.json, manifest.json, contrast-exceptions.json, titles.json, tests-before.txt, baseline-src/BASELINE_SHA,
before/report.json, before/summary.md, before/date-lines.json exist; all three tasks' automated verifies printed `ok`;
second fixtures run created 0; `git status --porcelain` empty before this file; STATE.md and ROADMAP.md untouched.

## Resync after phase 96 merge

feature/phase-97 was fast-forwarded to 73f6db87 (phase 96 and v6.1.0 merged, which also carries phase 97's own earlier slices 97-04
and 97-06). The scratch environment was re-based on it so BEFORE/AFTER comparisons isolate phase 97's relayouts. Nothing in the
repository changed except this section.

**New baseline.** BASELINE_SHA `73f6db87bc8d7103356dbf63fc0a741c7eb7dc54` (`git merge-base --is-ancestor c781090c` passed). The 8301 app
(ours) was stopped, `baseline-src` moved to `baseline-src.14fcdefb` (kept), re-created with `git archive HEAD`, and started on 8301
(`/health` 200). `before/` moved to `before.14fcdefb/` and the full BEFORE sweep re-run exactly as in Task 3 (`sweep.mjs --pages phase
--before` against 8301: 40 rows, 224 captures, both User-Agents, 0 not 200, no failed or unevaluated asserts; `date-lines.mjs`: 18
keys; `before-summary.py`, whose opening paragraph was updated to name the new baseline, the phase 96 and 97-04/97-06 content it
already shows, and the kept old copy). The 8300 app is stopped.

**Fixtures.** `fixtures.mjs` second run (against 8301, because 8300 is stopped; same database and forms): `created this run: 0`,
39 keys, fixtures.json unchanged, crowded-day counts 1/2/7/3/4/5/6 as p97player, five-week 5 entries, empty month 0, runway alert
still shown to p97dm2. Phase 96's changes broke no form the tool uses.

**BEFORE headline numbers, 14fcdefb vs 73f6db87.**

| measure | 14fcdefb | 73f6db87 |
|---|---|---|
| captures / not 200 / failed asserts | 224 / 0 / 0 | 224 / 0 / 0 |
| contrast failures (all inside main) | 1796 | 1876 |
| Events/Details phone overflow at 390 / 412 (4 rows) | scrollWidth 394 / 416, overflow true | 390 / 412, overflow false |
| other overflow records | cal-dm and cal-dm-alert at 320 (378), ev-details-oneshot desktop 320 (370) | same three, unchanged |
| cal-crowded and cal-sixweek pageScrolls, 1280x720 / 1440x900 | true, +223 / +43 | true, +223 / +43 (1280x700: +243, same) |
| cellOverflow days at those sizes | 12, 14, 18, 20 | 12, 14, 18, 20 |
| date-lines.json | 18 keys | 18 keys, identical text |

The +80 contrast failures are all in the four agenda rows (+10 per phone and desktop capture each): the wall-clock helper (97-06)
now wraps the date in its own `time.wall-clock` element that is scored next to the unchanged `small.text-muted` parent (same
3.94 ratio, same colour); no colour changed. The Events/Details overflow disappeared because phase 96's `.mobile-layout main > .row`
gutter rule reaches its bare `.row`.

**Test counts on the synced tree** (`QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`, user-secrets connection, no GC variables, no CLR flake):
UnitTests 1412 passed / 0 failed / 0 skipped; IntegrationTests 1928 passed / 0 failed / 0 skipped. Recorded in
`~/qb-ui-review/phase-97/tests-resync.txt` (the 97-02 inventory had 1378 and 1857 listed entries at 14fcdefb).

**phase96-landed.md** was re-recorded against 73f6db87 (the PROVISIONAL marker is gone). Verdict: 97-08 verifies the phone `.row`
gutter rule (`shell.mobile.css:40-42`, `.mobile-layout main > .row`, reaches `Events/Details.cshtml:42`); it does not add one.
Later slices must also respect: `account.css` is the last entry in `UiFoundationLayerGuardTests` `LayeredFiles` (line 76) and
`NoLiteralFiles` (new sheets of this area are appended there); the area views already render dates through `Html.WallClock` and
the board's today through `ViewBag.BoardToday` (97-06, 97-04), pinned by `EventsConversionGuardTests` (its `AreaFiles` list grows with
new views), `EventsDateRenderTests`, `CalendarBoardClockTests` and `WallClockAreaStylesTests`; `MobileViewsTests` Calendar facts moved
to lines 296-408 and 1589-1727.

**Deviation.** The new baseline therefore already contains 97-04 and 97-06 (phase 97's own code), not only phase 96; AFTER
comparisons for those two slices cannot be measured against this BEFORE (their BEFORE is `before.14fcdefb/`).
