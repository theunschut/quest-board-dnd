---
phase: 98
slug: characters-contacts-dm-profile-and-players-ui-conversion
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
block_on: high
created: 2026-11-14
---

# Phase 98 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Register authored at plan time (23 PLAN `<threat_model>` blocks: 71 rows, 67 unique IDs; T-98-SC repeats in plans 01, 02, 03, 04, 06). Verified by gsd-security-auditor at ASVS L1 against HEAD 26aa8524.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Review tooling → operator machine / mail relay / disk | Scratch Playwright harness, forced email suppression, credentials and baseline copies under ~/qb-ui-review/phase-98 | Fixture credentials (mode 600), captures; no mail leaves (SMTP 127.0.0.1:1025) |
| Harness / baseline app → operator database | Fixtures created through the app's own forms on a DB that holds real member data | p98 fixture rows only |
| Scratch package → operator desktop / publication | Gate 1 and Gate 2 sheets (JPEG frames, index, README, answers) | Fixture data; frames may show review-account display names |
| Operator answers → later plans / release | gate1/gate2 answers.json and contrast-exceptions.json drive slices and release | Decisions with provenance |
| Repository → view | `HasProfilePicture` boolean per signup character, board-filtered | Boolean only (no image bytes) |
| Browser → POST endpoints | Category, contact, note, profile, character, delete and reorder forms | Form fields + antiforgery token; controllers unchanged |
| Member-written text → rendered page / data attributes | Names, tags, bios, notes, sheet links, confirm sentences, tag whitelist | Razor-encoded output; JSON in encoded attribute; no `Html.Raw` |
| User file → browser canvas / upload | Crop dialog module, 5 MB and type pre-check, server validators unchanged | Image file |
| Shared layout / components → other areas | Global sheet list removals, `.form-page` / `.form-actions` contract | CSS scope only |
| Stored wall-clock date → rendered text | `Html.WallClock`, never timezone-converted | Date text |
| Test suite → operator database | Migrator suite, one approved run | Scratch databases `QuestBoardMigratorProbe_{guid}` created and dropped |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation / evidence | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-98-SC | Tampering | npm packages (×5 rows) | high | mitigate | node_modules symlinked to the Phase 94 Playwright 1.64.1 install; no new manifest or lockfile in the repo diff | closed |
| T-98-01 | Info disclosure | email to real addresses | high | mitigate | run-app.mjs forces SuppressSending + SMTP 127.0.0.1:1025, refuses overrides, checks /proc environ | closed |
| T-98-02 | Info disclosure | credentials / state files | medium | mitigate | mode 600, outside the repo, no secrets in docs or diff, connection string masked in logs | closed |
| T-98-03 | Info disclosure | feed addresses in records | high | mitigate | /feeds/calendar/ aborted in capture tools, query strings stripped; no feed address in any record (see residual note) | closed |
| T-98-04 | DoS | operator database | medium | mitigate | tools never stop foreign processes; no docker/systemctl/sqlcmd calls | closed |
| T-98-05 | Info disclosure | welcome emails | high | mitigate | same forced suppression, verified before account creation | closed |
| T-98-06 | Tampering | fixtures on real-data DB | medium | mitigate | fixtures through app forms only, listed in fixtures.json | closed |
| T-98-07 | Info disclosure | generated passwords | medium | mitigate | randomBytes(18), chmod 600, never printed | closed |
| T-98-08 | Info disclosure | baseline copy / BEFORE captures | low | accept | committed files + fantasy fixtures only | closed (accepted) |
| T-98-09 | Tampering | mockup module patterns | medium | mitigate | no innerHTML/global key listeners in mockup or shipped module | closed |
| T-98-10 | Info disclosure | saved baseline pages | low | mitigate | scripts stripped, antiforgery values blanked | closed |
| T-98-11 | Info disclosure | picture-flag query across boards | high | mitigate | queries DbContext.Characters under the active-board filter; no IgnoreQueryFilters | closed |
| T-98-12 | DoS | portrait bytes per request | medium | mitigate | projects `ProfileImage != null`; unit test asserts no bytes loaded | closed |
| T-98-13 | Tampering | name in avatar alt | low | mitigate | Razor attribute encoding, no Html.Raw | closed |
| T-98-14 | Tampering | feed revision | high | mitigate | read-only query; feed files unchanged | closed |
| T-98-15 | Info disclosure | scraped page text | medium | mitigate | scraped as p98 accounts on P98 boards only | closed |
| T-98-16 | Spoofing | sheet link in mock | medium | mitigate | rel="noopener noreferrer" | closed |
| T-98-17 | Info disclosure | gate package content | medium | mitigate | allowed entries only, no scripts/links/credentials, text hygiene clean | closed |
| T-98-18 | Repudiation | Gate 1 answers | low | mitigate | per-item source and verbatim provenance in gate1/answers.json | closed |
| T-98-19 | Tampering | unapproved merge | medium | mitigate | merge recorded with explicitYes before the merge commits; 98-17 read the key | closed |
| T-98-20 | Tampering | category rename form | medium | mitigate | asp-action + antiforgery + validation kept; controller unchanged | closed |
| T-98-21 | EoP | category pages on phone UA | medium | mitigate | both-UA facts; controller unchanged | closed |
| T-98-22 | DoS | Quest/Events forms after promotion | medium | mitigate | promotion declined at Gate 1; rules scoped to new classes only | closed (not triggered) |
| T-98-23 | Tampering | crop status/dialog text | medium | mitigate | textContent only; guard test | closed |
| T-98-24 | Tampering | oversized / wrong-type upload | medium | mitigate | server MaxFileSize/AllowedExtensions unchanged; client pre-check | closed |
| T-98-25 | DoS | page-wide key handler | low | mitigate | keydown on the selection only; guard test | closed |
| T-98-26 | Info disclosure | re-crop original URL attribute | low | accept | Razor-encoded, same authenticated route, access checks unchanged | closed (accepted) |
| T-98-27 | Spoofing | reverse tabnabbing via sheet link | medium | mitigate | rel="noopener noreferrer", [Url] kept, render fact | closed |
| T-98-28 | Tampering | confirm sentence attribute | low | mitigate | fixed strings, confirm() plain text, guard test | closed |
| T-98-29 | EoP | details actions on phone UA | medium | mitigate | CanManageCharacterAsync untouched; both-UA facts | closed |
| T-98-30 | Tampering | hostile tag name in whitelist | high | mitigate | JsonSerializer in encoded attribute, JSON.parse + string filter, no Html.Raw | closed |
| T-98-31 | Info disclosure | other board's tags | medium | mitigate | own-board-only suggestion fact | closed |
| T-98-32 | Tampering | contact POST fields | medium | mitigate | all 23 POST forms carry antiforgery; controllers unchanged | closed |
| T-98-33 | Tampering | DOM XSS via note toggle | high | mitigate | contact-notes.js toggles hidden/focus only; guard test | closed |
| T-98-34 | EoP | note controls on phone UA | medium | mitigate | both-UA theories; controller unchanged | closed |
| T-98-35 | Tampering | cross-board note injection | high | mitigate | board check before insert; integration test | closed |
| T-98-36 | EoP | editing another DM's profile | high | mitigate | Forbid() unless owner/Admin on GET and POST; integration test | closed |
| T-98-37 | Tampering | validation summary output | low | mitigate | tag helper, no Html.Raw | closed |
| T-98-38 | Tampering | photo upload | medium | mitigate | field names, antiforgery, validators unchanged | closed |
| T-98-39 | Tampering | hostile category name in confirm | high | mitigate | sentence built in Razor, encoded attribute; XSS facts | closed |
| T-98-40 | EoP | category actions on phone UA | medium | mitigate | controller unchanged; phone Manage facts | closed |
| T-98-41 | Tampering | class rows built from strings | medium | mitigate | template clone, renames only name/id/for; guard test | closed |
| T-98-42 | Tampering | class list binding | medium | mitigate | Classes[i].* kept, renumbered from zero | closed |
| T-98-43 | EoP | editing another player's character | high | mitigate | CanManageCharacterAsync gates unchanged | closed |
| T-98-44 | Tampering | names in links/alt | low | mitigate | no Html.Raw | closed |
| T-98-45 | Info disclosure | roster across boards | medium | mitigate | controller/repository unchanged; board filter | closed |
| T-98-46 | Info disclosure | hidden contacts / others' tags | high | mitigate | controller unchanged; player-view theories on both UAs | closed |
| T-98-47 | Tampering | names in links and chips | low | mitigate | no Html.Raw | closed |
| T-98-48 | Info disclosure | link to switched-off DM page | medium | mitigate | link only when the page is on; render facts; BoardPageGateFilter kept | closed |
| T-98-49 | Tampering | names and bio | low | mitigate | no Html.Raw; Markdown pipeline unchanged | closed |
| T-98-50 | Tampering | wall-clock date converted | high | mitigate | Html.WallClock; helper unchanged; date guard | closed |
| T-98-51 | Tampering | feed revision and write paths | high | mitigate | no change under Domain, Migrator, feed files, controllers, Program.cs | closed |
| T-98-52 | Repudiation | silently weakened guards | medium | mitigate | tests-renamed.md accounts for every retargeted test; no test file deleted | closed |
| T-98-53 | EoP | exceptions treated as approved | medium | mitigate | all pending until the operator's Gate 2 answers | closed |
| T-98-54 | DoS | operator database (migrator suite) | low | mitigate | one approved run; scratch DBs created and dropped; no connection detail recorded | closed |
| T-98-55 | Info disclosure | gate package publication | medium | mitigate | plan expected "local only, never pushed"; the orchestrator published the approved v2 package as orphan branch `review/phase-98-gate2` (2ddb3c08) at the operator's request; unmasked frames, fixture data, text hygiene clean | open — below high threshold (non-blocking) |
| T-98-56 | Repudiation | exceptions shipped without sign-off | medium | mitigate | every exception answered with source in contrast-exceptions.json and gate2/answers.json | closed |
| T-98-57 | EoP | accidental push / tag / PR | high | mitigate | no tag since v6.2.0; no PR; milestone-branch pushes follow the standing rule | closed |
| T-98-58 | DoS | host calling removed initImageCrop | medium | mitigate | grep clean; render test pins absence | closed |
| T-98-59 | Tampering | markup from strings / global in crop module | medium | mitigate | guard test with scanner self-test | closed |
| T-98-60 | DoS | host unstyled after global link removal | low | mitigate | every host links the sheet; layout guard | closed |
| T-98-61 | DoS | form calling removed initContactTags | medium | mitigate | grep clean; absence asserted | closed |
| T-98-62 | EoP | merged forms on phone UA | medium | mitigate | both-UA facts; controller unchanged | closed |
| T-98-63 | Tampering | whitelist after merge | medium | mitigate | encoded attribute kept; guard | closed |
| T-98-64 | Tampering | member names in Players page | low | mitigate | no Html.Raw | closed |
| T-98-65 | Info disclosure | members of another board | medium | mitigate | controller/service/repository unchanged | closed |
| T-98-66 | Tampering | frozen baseline writing to operator DB | low | mitigate | port guard, forced suppression on 8311; five identical p98dm fixture re-saves disclosed and fixed in the harness | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

**T-98-55 resolution:** the operator either deletes `review/phase-98-gate2` (origin and local), as was done for v1, or accepts it as an exposure of fixture-only frames. Either closes it; it is the orchestrator's branch, so this session leaves it untouched.

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-98-01 | T-98-08 | Baseline copy and BEFORE captures contain only committed files and fantasy fixtures; the connection string stays in user-secrets | Plan 98-03 (accept disposition) | 2026-11-13 |
| AR-98-02 | T-98-26 | Re-crop original URL in a Razor-encoded data attribute; same authenticated route the old inline script exposed; access checks unchanged | Plan 98-09 (accept disposition) | 2026-11-13 |

Gate 2 accessibility exceptions (e1, e3, e4, e5, e6, e8) and decisions (d1, d3) are recorded in `~/qb-ui-review/phase-98/contrast-exceptions.json`; they are not security acceptances.

---

## Residual notes (non-blocking)

- **T-98-03:** thirteen phase-authored ad hoc probes install no feed abort; they visit pages that never request `/feeds/calendar/`, and three record only the fixed confirm sentence. A shared context factory would remove the per-script dependence.
- **T-98-30:** no test seeds a hostile tag name into the whitelist; the mitigation is present in code. Test-depth note only.
- **Gate 1 items 7 and 10:** recorded as explicit yes after the operator's direct reply "uh yes?" to a confirmation question; both replies are stored verbatim.
- **WR-01** (stale crop after a refused pick) fixed in 3aa6d4fd with a guard test.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-14 | 67 | 66 | 1 (T-98-55, medium, non-blocking) | gsd-security-auditor (ASVS L1), recorded by the executing session |

## Security Audit 2026-11-14

| Metric | Count |
|--------|-------|
| Threats found | 67 |
| Closed | 66 |
| Open | 1 (below block threshold) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [ ] T-98-55 resolved (delete the review branch or accept) — operator's choice
