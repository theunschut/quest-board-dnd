# Phase 94 Plan 24: Checkpoint (Task 3, operator review of the full readability sweep)

Status: Tasks 1 and 2 done, Task 3 waiting for the operator. No SUMMARY yet; STATE.md, ROADMAP.md and WINDOWS.md untouched.

Review page: `~/qb-ui-review/phase-94/plans/94-24/review/index.html` (self-contained, relative image paths, 118 pages x both User-Agents; decisions first, then the classification table, then flagged pages, then the rest).

## Commits (branch `worktree-agent-a9ce42caabc3ae550`, base `b61869b1`)

| Commit | What |
|--------|------|
| `5b1ca9a0` | Cream element rules removed from modern-card.css (stopped run, kept unchanged) |
| `ba750d65` | modern-card.css deleted and unlinked from the four layouts; PlatformAreaLayoutTests, ContactCategoryContrastGuardTests, LinkedBoardStaticGuardTests re-pinned; contacts.css comment rewritten (stopped run, kept unchanged) |
| `2b32dab6` | Held heading and strong type scale landed in base.css with the guard (stopped run, kept unchanged) |
| `fab1f3fe` | Gap: `BaseCss_MapsHeadingScale` threw because `body` is declared by two rules; the guard now reads every rule of a selector |
| `95ec8190` | Phone card-title step in components.css (`.mobile-layout .modern-card-header :is(h1, h2)` at 20px); the stopgap `h1-h6 { font-weight: bold }` removed from mobile.css |
| `a8de5cf7` | Real regression of the scale on unconverted phone cards: new token `--qb-fg-heading` (#fff3c9, 5:1 on plain glass) and its channel triple, used by the page sheets' heading colour rules; `.shop-mgmt-*-card-mobile h5.text-info` maps its info channel to it; shop item card gained the scrim; account title bold stopgap removed; guard table row added |
| `4a5fdc3e` | `.form-control-plaintext { color: inherit }` in components.css; `.cr-badge` text uses the heading token (legacy-pages.css, quests.css) |

Check of the stopped run against Tasks 1 and 2: all acceptance criteria hold. `modern-card.css` is gone, `grep -rn 'modern-card\.css'` over Views, Areas and wwwroot/css prints nothing (the only test mention is the toast guard asserting its absence), no layout links it, the three tests keep their facts and add the contrast assertions, and the contacts.css comment names neither file nor rule. Its `.toast` restatement went with the file, and so did the important blue focus shadow.

## Test result

`dotnet build`: 0 errors. Full `dotnet test`: UnitTests 1,300 passed; IntegrationTests 1,750 passed, 11 skipped (SQL Server migrator facts), 0 failed. No flake hit. Port 8024 is stopped; ports 8000 and 8001 were not touched.

## Sweep `final` against `before` (120 records, 0 non-200, both User-Agents)

`readability-compare.py before final` flags 5 rows, none a real regression:

| Row | Flag | Class | Evidence |
|-----|------|-------|----------|
| Agenda-Index mobile, Events-Index desktop and mobile | interactiveLost (past event link, Show players, Show More Events), one new Agenda contrast row | date drift | Weekly Lantern Rounds (Tue Nov 3) is in the past. Swept the frozen baseline today (`before-today`, port 8001, these ids only): `readability-compare.py before-today final` passes all three rows. |
| Agenda-Index desktop | interactiveLost: five `Switch & Open` buttons | active-board difference | `before-today` renders the five cross-board entries as `Switch & Open` buttons (the 8001 session's active board), `final` renders the same five as `Open Event` links: 23 vs 24 controls, same elements. The remaining lost controls are the date drift above. |
| ShopManagement-Index desktop | new contrast failure `h4.text-secondary` 3.92:1 | probe false positive | Card is glass over scrim; the shared probe ignores the scrim layer (WINDOWS 8). The corrected probe (`mockups/package/probe-lib.mjs`) shows no h4 failure on either side (46 failures on the live baseline, 5 on final for that page). |

The first full sweep (after the phone step) flagged 47 rows. Four were the date/active-board rows above, one the probe false positive, and **42 phone rows were one real regression**: with the heading scale in force, h4 to h6 card titles are 16px at 600 (normal text, 4.5:1) and the unconverted phone cards sit on plain `rgba(255,255,255,.15)` glass without the scrim, where the parchment measures 4.34:1 (before they were 20px bold, which counts as large text). Fixed per the plan on the page sheets with a lighter heading token; the scale was not touched. Two follow-ups were found by comparing the ratios of failures present in both reports (the comparison keys them by selector and sample, so a failure that got worse is not flagged):

- `p.form-control-plaintext` (Account-Profile, Characters-Details, desktop) fell 4.34 to **1.04:1**: Bootstrap's dark body ink on the dark card once the card-wide cream rule was gone. Real; fixed in components.css. Screenshot confirmed the values were nearly invisible before the fix.
- `.cr-badge` text (Quest-Details, Quest-Details voting, Quest-Manage, QuestLog-Details, desktop) 4.46 to 4.0:1: its own gold colour had been hidden by the cream span rule. Real; fixed.

Totals over the whole manifest, shared probe: 2,435 failure rows BEFORE, 838 final; 487 failures improved, 335 unchanged, 14 lower (listed below). Corrected multi-layer probe over every page: 2,438 against the live baseline on 8001, 623 final; its only "new" keys are Move-Index mobile and Admin-EmailStats, where the 8001 baseline page is a different page (the 8001 session lands on the board picker) or the same element at a better ratio under another key.

## WINDOWS entries

- **11 (heading and strong scale): resolved.** Landed in `2b32dab6` with a guard, inside this plan's sweep; headings measure on the new surfaces and the page-sheet fix above covers the unconverted phone cards.
- **13 (important blue focus shadow): resolved.** The rule left with modern-card.css. Confirmed on a focused card input (Account/Edit, desktop and phone): outline `2px solid rgb(26, 15, 8)` plus the gold inner band `rgb(255, 193, 7) 0 0 0 2px`, no blue.
- **15 / 26 (`.toast` restatement): resolved.** The restatement is gone; toasts keep their opaque surface (`plans/94-24/toasts.mjs`, all checks pass desktop and phone: overlay brown `rgb(47, 27, 14)`, parchment text, no cover, stack below the navbar).
- **16 (phone 20px card title): resolved** (`95ec8190`), with no new failure on the converted `modern-card-header` titles.
- **17 and 26 (dark-panel alert): measured, decision 1 below.** A dark overlay alert heading would pass (12.98:1 in a trial).
- **12 (ten status-text failures): partly resolved.** On desktop the lower ratios were the multi-layer probe artefact: the corrected probe shows no Quest/Manage success text, text-info heading, char-count or small-text failure on final (7 rows of the 14 "lower" ones). On the phone, the required-field asterisk (`span.text-danger`, Characters-Create/Edit, Contacts-Create/Edit/Details) stays at 3.36:1 (4.34 in BEFORE, the drop dates from 94-19) because those cards are still plain glass without the scrim. Not new, and no page-level fix exists that keeps the red. It resolves when those cards move to the scrimmed card surface in their area phases; keep entry 12 open for those 7 phone rows.
- **18, 21, 24, 25:** shown as decisions 2, 3, 5 and 4 in the review; no change made.

## Decisions to confirm (each has one screenshot in the review)

1. Alerts as parchment panels. A dark-panel alert heading would now pass (nothing colours `.alert-heading`; trial 12.98:1). Keep parchment or go back to the dark alert?
2. Status words as ink on paper surfaces (icon and word carry the status). Enough at a glance?
3. Desktop menu collapses to the hamburger below 1200px (compact link step 1200 to 1399px).
4. Phone backdrop is the 900px WebP scaled about 1.4x on tall phones; slightly soft.
5. Page links under 44px (six quest titles at 27px on /quests; 43 email links at 22px on /Platform/Users) are left to the Quests and Platform area phases.

## Awaiting

Operator review of `review/index.html`. Reply "approved", or list the pages and what is unreadable or unreachable. Then the SUMMARY, STATE and ROADMAP updates and the ledger changes above follow.
