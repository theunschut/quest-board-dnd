---
phase: 85
slug: one-shot-quests-in-the-calendar-feed
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-09-18
---

# Phase 85 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

One-shot quest sessions were added as a second source to a calendar feed that previously
carried only board events. The feed answers at a cookie-less, token-addressed anonymous URL —
no session, no active board — so the scoping rules below are a confidentiality boundary rather
than a convenience. A leak on this surface has no human reader to notice it. The repository
read deliberately bypasses the ambient tenant query filter, on the strength of a board-id set
pinned by the domain service from a fresh per-request membership read.

Register authored at plan time across the six `*-PLAN.md` `<threat_model>` blocks.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| anonymous calendar client to feed address | An unauthenticated machine reader holding only a bearer address. Sends no cookie, holds no session, has no active board. | Session titles, dates and times for quests and events the reader is part of |
| domain service to repository | The service supplies the pinned one-shot board-id set; the repository bypasses the ambient tenant filter on the strength of it. Safe only because the set is narrower than the filter, never wider. | Board-id set, subscription owner id |
| board membership to board type | Two independent scoping rules over the same boards. Collapsing them into one is the roadmap's own named risk. | Board identifiers and their `BoardType` |
| a subscriber's device over time | A calendar client caches what it was last served and refetches on its own schedule. | Previously served entries still held on-device |
| application copy to member | What the interface asserts as fact. The invented session length is not something the board knows. | Displayed session-length copy |
| ledger to future audit | A requirement marked complete is read by a later milestone audit as evidence. | Requirement completion state |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-85-01 | Information Disclosure | `QuestRepository.GetFeedQuestsForUserAsync` + quest branch of `CalendarSubscriptionService.GetFeedAsync` — the second cross-board read in this codebase | high | mitigate | Board set derived solely from the fresh per-request membership read (`CalendarSubscriptionService.cs:67-75`), passed as the only board predicate (`:120-122`, `QuestRepository.cs:292`), every returned row re-checked in memory with `LogError` carrying both counts (`:126-133`). Guarded by the non-member-board and leaving-a-board integration facts and all four `CalendarSubscriptionQuestRecheckTests` unit facts. | closed |
| T-85-02 | Information Disclosure | board-type scoping of the quest read | medium | mitigate | The single derived set carries the board-type narrowing too (`CalendarSubscriptionService.cs:75`), so board type cannot be satisfied without membership or the reverse. Argument-capture unit fact asserts the set is exactly the one-shot board, never the campaign board. | closed |
| T-85-03 | Elevation of Privilege | the Dungeon Master branch of the quest predicate | high | mitigate | Board containment and the seat-or-DM disjunction sit inside a single `Where` with the disjunction parenthesised (`QuestRepository.cs:292-296`). Guarded behaviourally by the campaign-board exclusion run through the DM route, and by the 85-03 mutation check (re-shaping the read into two merged lists turns the single-entry fact red). See Note 1. | closed |
| T-85-04 | Tampering | VEVENT identifier collision between an event and a quest sharing a numeric id | low | mitigate | `CalendarFeedWriter.BuildUid` namespaces by source member name (`CalendarFeedWriter.cs:134-141`); facts emit both sources at one numeric id and assert two distinct identifiers, plus an occurrence count of exactly one. | closed |
| T-85-05 | Information Disclosure | honouring a seat on a quest flagged as a Dungeon Master session | low | **accept** | Reasoned acceptance, not an oversight — see Accepted Risks Log. Premise re-verified in code during this audit. | closed |
| T-85-06 | Denial of Service | omitting the ambient-filter bypass on the new read | medium | mitigate | `IgnoreQueryFilters()` present with its fail-closed rationale (`QuestRepository.cs:286-291`); the tracer fact seeds a genuinely qualifying quest and asserts it appears — the only check that distinguishes "bypass missing" from "no quests qualify". See Note 1. | closed |
| T-85-07 | Tampering | whole-file rewrite of `.planning/ROADMAP.md` / `.planning/REQUIREMENTS.md` | medium | mitigate | Scoped replacements only; phase diff is 3 hunks in ROADMAP.md and 2 in REQUIREMENTS.md, +106/-15. `### Phase ` heading count 14 before and after. | closed |
| T-85-08 | Repudiation | a minted requirement that no plan claims | low | mitigate | All 18 ids `QUESTFEED-01`–`18` verified present in at least one plan's `requirements` frontmatter. | closed |
| T-85-09 | Information Disclosure | a predicate degenerating to board scope, putting every finalized quest on the reader's own one-shot boards into the feed | medium | mitigate | Negative control seeds an unseated finalized quest on the reader's own board in the same fetch as a seated one, asserting absence plus an event count of one. | closed |
| T-85-10 | Information Disclosure | a quest that stopped qualifying still being served | medium | mitigate | Four before/after transitions against the same address — unfinalize, delete, seat withdrawn, moved out of window — each asserting zero entries after. | closed |
| T-85-11 | Tampering | an identifier that changes between fetches for one unchanged quest | medium | mitigate | The reschedule fact captures the UID line from the first body and asserts byte equality against the second. | closed |
| T-85-12 | Denial of Service | a window predicate lost entirely, serving every finalized quest ever | low | mitigate | Both window facts assert an entry count alongside the absence, so a degenerate "no window" predicate fails on the count. | closed |
| T-85-13 | Information Disclosure | events silently narrowed to one-shot boards as a side effect of narrowing quests | high | mitigate | The event branch remains scoped to the full member board set (`CalendarSubscriptionService.cs:85-91`). One fact asserts the campaign board's event is present in the same body where its quest is absent; another asserts an events-only document is byte-identical in body **and** ETag. | closed |
| T-85-14 | Repudiation | a requirement marked complete with nothing behind it | medium | mitigate | All 18 ticked ids map to green rows in `85-VALIDATION.md`'s per-task verification map; no id ticked without a row. | closed |
| T-85-15 | Spoofing | application copy presenting the invented session length as a fact the board knows | low | mitigate | Five new forbidden-claim cases added to the shipped copy guard theory, with a mutation check proving they trip (red at 18/19, green at 19/19). Preventive: no UI ships in this phase. | closed |
| T-85-16 | Repudiation | a deferred gap silently disappearing from the record | medium | mitigate | Both open gaps restated as open across `85-VALIDATION.md`, `85-06-SUMMARY.md` and `85-VERIFICATION.md`, and carried as `.planning/WINDOWS.md` entries 5 and 6. Neither is described as closed, resolved, verified or observed. | closed |
| T-85-SC | Tampering | package-manager installs | n/a | **accept** | No package was introduced — the phase diff contains zero `PackageReference` changes and no `*.csproj` / `Directory.Packages.props` edits. See Accepted Risks Log. | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-85-01 | T-85-05 | Honouring a confirmed seat on a quest flagged as a Dungeon Master session. In this codebase that flag hides a quest from the board *listing*, but the quest details read applies no gate at all — re-verified during this audit: `QuestRepository.GetQuestWithDetailsAsync` filters on quest id alone, while the listing read `GetQuestsWithSignupsForRoleAsync` does apply the flag. It is a discoverability hide, not an access control, so honouring a granted seat widens nothing already unreachable. Stated cost: a Dungeon Master who flips the flag after signups leaves those readers' phones carrying a title the board no longer lists for them. | Operator (locked decision D-04, `85-CONTEXT.md`) | 2026-09-18 |
| AR-85-02 | T-85-SC | Package-legitimacy gate not applicable — this phase adds no external package. Confirmed by audit: the phase diff touches no `*.csproj` or `Directory.Packages.props` and contains zero `PackageReference` lines; all 13 changed source files sit in existing projects. | Operator (`85-RESEARCH.md` Package Legitimacy Audit) | 2026-09-18 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-09-18 | 17 | 17 | 0 | gsd-security-auditor (ASVS L1, block_on high) |

### Live test evidence captured during the audit

- `QuestBoard.UnitTests` calendar-feed suites (`CalendarSubscriptionQuestRecheckTests`, `CalendarFeedWriterTests`, `CalendarFeedOptionsValidationTests`) — 59/59 passed
- `QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` — 19/19 passed
- Forbidden-claim copy guard — 19/19 passed
- `CalendarSubscriptionStaticGuardTests` — 43/44; the single failure is the pre-existing Linux-only path-resolution bug inherited from Phase 84 (`WINDOWS.md` entry 6). It touches no mitigation above.

---

## Notes

**Note 1 — one declared guard for T-85-03 / T-85-06 is not a standing assertion.**
Both threats named "source assertion of exactly one filter bypass in the repository class"
among their guards. That was a plan-time acceptance criterion (`grep -c 'IgnoreQueryFilters'`
outputs 1), not a test, and it was found during execution not to hold — it outputs 3, because
the pre-existing `GetQuestsForTomorrowAllGroupsAsync` (the daily reminder job's system-wide
sweep) already bypasses the filter. It was resolved by written note rather than by a corrected
standing assertion, and no test anywhere reads `QuestRepository.cs` as source or asserts on its
bypass count. The substantive mitigations are verified present in the code, and two of the three
named guards for T-85-03 exist and are green. But this register must not be read as claiming a
structural regression in bypass count would be caught automatically — it would not.

**Note 2 — code review WR-01 is inside T-85-01's declared scope, not outside it.**
The second-layer re-check validates board scope only and cannot catch a regression in the
seat-or-DM predicate. That is exactly what T-85-01's mitigation declared, so it is not an absent
mitigation; the seat/DM predicate's protection is the query itself plus the T-85-09 negative
control and its mutation check — test-time, not runtime. WR-01's minimum recommended fix
(extending the doc comment to state which predicate the re-check does and does not cover) has
not been applied; the comment still reads only "Second-layer re-check, mirroring the event
branch above." Worth tracking; opens no threat.

**Note 3 — no `## Threat Flags` section exists in any of the six SUMMARY files.**
The section is absent rather than empty, so executor-reported attack surface was unavailable as
an independent audit input. The register was verified against the code directly instead, and
every production file changed in the phase maps to a registered threat surface. No unmapped new
attack surface was found.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-09-18
