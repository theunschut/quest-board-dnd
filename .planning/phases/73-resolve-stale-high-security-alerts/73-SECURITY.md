---
phase: 73-resolve-stale-high-security-alerts
audited: 2026-08-26
auditor: gsd-security-auditor
asvs_level: 1
block_on: high
threats_total: 14
threats_closed: 14
threats_open: 0
status: secured
record_corrections_applied: 1
---

# Phase 73 Security Audit - Resolve Stale HIGH Security Alerts

**Verdict: SECURED.** 14/14 declared threats verified closed. One declared mitigation was found
overstated in the durable record (`03/T-73-01`); the record has been corrected in place by this
audit and the threat now reads closed. No blocking finding. Two non-blocking follow-ups are listed
at the bottom.

Threat IDs repeat across the three plans, so they are namespaced `{plan}/{id}` here.

Scope note: this phase changed zero source files. Its "implementation" is documentation plus
external GitHub API state, so verification is live read-only API and `git` state assertion rather
than code grep. Every check below was re-run independently by this audit - none is copied from a
SUMMARY, VERIFICATION or plan claim.

## Threat Verification

| Threat ID | Category | Severity | Disposition | Status | Evidence |
|---|---|---|---|---|---|
| 01/T-73-01 | Repudiation | high | mitigate | CLOSED | `73-EVIDENCE-CAPTURE.md` carries `CHECK_DATE: 2026-08-26`, 5 per-alert subsections, 5 distinct GHSA + 5 distinct CVE ids, both sources captured (SBOM A5/A6, local `dotnet list package --include-transitive` A8) and the `main` HEAD drift check. Re-verified live by this audit: SBOM returns exactly one `System.Security.Cryptography.Xml` entry at `8.0.3`; local sweep returns 0 occurrences across all 5 `QuestBoard.*` manifests, with non-empty control output (10/64/103 package lines) proving the command actually ran. |
| 01/T-73-02 | Repudiation / Tampering | high | mitigate | CLOSED | Every `DELETE`/`PUT .../vulnerability-alerts` string in the phase corpus is a prohibition or a post-hoc assertion, never an executed command (11 matches, all plan/context/record prose). Live: `GET /repos/theunschut/quest-board-dnd/vulnerability-alerts` -> `204 No Content` (alerts still enabled); #7/#8/#11 read `fix_started` / `cryptic96` at `2026-06-25T15:49:52Z`, `2026-06-25T15:49:52Z`, `2026-06-29T14:30:23Z`. The veto and the three protected dismissals are named in `SECURITY-TRIAGE.md` "Root cause, and what is not fixed". |
| 01/T-73-03 | Information Disclosure | low | accept | CLOSED (accepted) | No `gho_` / `ghp_` / `github_pat_` / `Authorization:` string in any phase artifact or in `SECURITY-TRIAGE.md`. Scope headers are recorded as scope *names* (`admin:org, gist, repo`), not credential values. Logged under Accepted Risks below. |
| 01/T-73-04 | Tampering | medium | mitigate | CLOSED | No force-refresh and no mutating call in plan 73-01's corpus; appendix A12 records the post-task mutation check. Commit timeline confirms nothing mutated during wave 1: the alerts remained `open` from 07:33Z (evidence capture) until 09:14:50Z (first PATCH, wave 2). |
| 02/T-73-01 | Repudiation | high | mitigate | CLOSED (with recorded deviation - see Adjudication 1) | Gate (a) re-read live per alert immediately before that alert's own PATCH. Gate (b)'s independent local source re-run live inside the sending loop (once, repo-wide, 0 occurrences). Gate (b)'s GitHub SBOM half last ran live at 09:11Z (blocked-executor #17 gate, ~3.5 min before the first PATCH) and at 07:33Z, not inside the loop. D-11 approval recorded at 07:47Z, before any PATCH. D-17/D-18 stop-on-failure never fired (no failure) but is structurally intact: `git status --porcelain -- '*.csproj'` empty, no patch anywhere in the phase. |
| 02/T-73-02 | Repudiation / Tampering | high | mitigate | CLOSED | Same live evidence as 01/T-73-02, re-run after the PATCHes by this audit. #7/#8/#11 untouched with original June timestamps; no write touched any alert outside #17-#21 (zero open alerts, and only #17-#21 carry a 2026-08-26 `dismissed_at`). |
| 02/T-73-03 | Tampering | medium | mitigate | CLOSED | All five bodies exist as `dismissals/dismiss-{17..21}.json` with `state=dismissed`, `dismissed_reason=inaccurate`. This audit compared each file's `dismissed_comment` against the live stored comment: **5/5 exact byte match**, 253 chars each. Charset check on the live text: zero apostrophes, zero double quotes, zero newlines. |
| 02/T-73-04 | Elevation of Privilege | low | accept | CLOSED (accepted) | `73-DISMISSAL-DRAFTS.md` records `X-Oauth-Scopes` and `X-Accepted-Oauth-Scopes` verbatim from a live read-only `GET`, with an explicit honesty note that the PATCH-side carryover was untested. Live re-check: active token scopes `admin:org, gist, repo`; `repo` present in both lists. Logged under Accepted Risks below. |
| 02/T-73-05 | Spoofing | low | mitigate | CLOSED | `gh auth status` (re-run by this audit): active account `cryptic96`, `theunschut` present but inactive. Live read-back on all five alerts: `dismissed_by.login = cryptic96` on #17-#21, matching the actor on #7/#8/#11. |
| 03/T-73-01 | Repudiation | high | mitigate | CLOSED after correction (see Adjudication 2) | The per-alert gate (b) cells plus the trailing paragraph asserted that both sources ran live immediately before each PATCH. That overstated what ran. Corrected in place by this audit: `.planning/SECURITY-TRIAGE.md` now carries a dated, dagger-referenced correction stating exactly which source ran where and when. Diffability re-verified independently after the edit: all five live `dismissed_comment` values still appear verbatim in the record. |
| 03/T-73-02 | Repudiation / Tampering | high | mitigate | CLOSED | The alerts toggle appears in no executed command. The phase gate recorded both readings of success criterion 5 (`0` and `0`); this audit re-ran the open-alert count live: `0`. #7/#8/#11 re-asserted `fix_started` after the gate and again now. |
| 03/T-73-03 | Tampering | medium | mitigate | CLOSED | `git show --stat cc83807` (the hooks commit): `.planning/PROJECT.md \| 2 ++`, 2 insertions, 0 deletions - scoped `Edit`, no whole-file `Write`. The later phase-completion commit `f2b870b` touches PROJECT.md with 3 insertions / 2 deletions, also small and scoped. |
| 03/T-73-04 | Repudiation | medium | mitigate | CLOSED (residual noted) | Record lives at `.planning/` root; `grep -c "73-EVIDENCE-CAPTURE" .planning/SECURITY-TRIAGE.md` = 0; captured output is copied in, not linked; both PROJECT.md hooks state their reasoning in plain language and cite `.planning/SECURITY-TRIAGE.md`, citing no phase-local decision ID. Residual: the record still cites the bare filename `73-DISMISSAL-DRAFTS.md` once for the approval record, and still cites `D-02` / `D-19` / `D-06` (this audit removed the `D-16/D-17` citation as part of the correction). Hygiene, not a gap - see Follow-ups. |
| 03/T-73-05 | Denial of Service (process) | low | accept | CLOSED (accepted) | Accepted by design and documented: the record states that the ghost manifests survive the phase, that a sixth alert is expected, and that the log is append-only so entry two needs no restructuring. Live: GraphQL `dependencyGraphManifests` still returns `totalCount 13` with all five ghost `EuphoriaInn.*` manifests present - the accepted condition is real and unchanged. Logged under Accepted Risks below. |

**Blocking-open count (`severity >= high`): 0.** No threat is open at or above the `high` block
threshold, and none is open below it.

## Adjudication 1 - 02/T-73-01, gate (b)'s two-source requirement

**Ruling: the mitigation is satisfied in substance; the deviation is procedural and is now on the
record.**

What the mitigation demanded: package-absence proven by two independent sources, re-run live
immediately before each individual PATCH, so that GitHub's own (proven-stale) dependency graph is
never its own corroboration.

What ran in the loop that sent the five PATCH calls (09:14:50Z-09:14:58Z, orchestrator-executed
after the executor was permission-blocked):

- Gate (a): five live per-alert reads (`state`, `manifest_path`, package and range), one
  immediately before each PATCH. Fully as declared.
- Gate (b), local `dotnet list package --include-transitive`: run live in that loop, once,
  repo-wide across all five `QuestBoard.*` manifests, returning 0 occurrences. Not repeated per
  alert.
- Gate (b), GitHub SBOM: not re-run inside the loop. Last live reads at 09:11Z (in the gate the
  permission-blocked executor ran for #17, ~3.5 minutes before the first PATCH) and 07:33Z (wave 1
  evidence capture).

Why this satisfies rather than fails the mitigation:

1. **The source that ran live in the loop is the independent one.** The two-source rule exists to
   stop GitHub's graph corroborating itself. The local restore-graph read carries that burden, and
   it ran. The omitted source is the suspect one.
2. **The omitted source could not have flipped the answer in the direction that matters.** The
   SBOM's actual value throughout is a *positive* hit - one `8.0.3` entry - explained by the ghost
   `Microsoft.AspNetCore.Identity 2.3.1` edge. It has never been the source that could reveal the
   package reachable from a live manifest; only the local sweep can, and it said 0.
3. **Per-alert repetition of gate (b) is degenerate by construction.** Gate (b) is package-scoped
   and identical for all five alerts; plan 73-02 itself directs implementing it once and calling it
   per alert. Running it once for the loop weakens the ritual, not the finding.
4. **The gap is now closed outright by independent re-run.** This audit re-ran both sources
   read-only: local sweep 0 occurrences across all five manifests (with non-empty control output);
   SBOM exactly one entry at `8.0.3`,
   `SPDXRef-nuget-System.Security.Cryptography.Xml-8.0.3-84ce5b`, whose only incoming `DEPENDS_ON`
   edges are the ghost `Microsoft.AspNetCore.Identity 2.3.1` node and the repository root node.
   Both sources agree, at a third point in time, checked by a party that did not perform the
   dismissals.

What remains true and is not glossed: for #18-#21 no GitHub-sourced corroboration was taken inside
the sending window. That is a real, if narrow, departure from the declared procedure, and the
durable record now says so.

## Adjudication 2 - 03/T-73-01, did the durable record overstate?

**Ruling: yes, it overstated - narrowly but materially. Corrected.**

The per-alert table's gate (b) cell read `PASS - local dotnet list package --include-transitive (0
matches across all 5 QuestBoard.* manifests) + GitHub SBOM (1 entry, 8.0.3, ...)` on all five rows,
and the paragraph immediately under the table said:

> Every gate (a)/(b) pair above was re-run live in plan 73-02, immediately before that alert's own
> `PATCH`, per D-16/D-17 - not copied forward from plan 73-01's earlier evidence capture.

Read together, that asserts both sources ran per alert inside the PATCH window, and explicitly
denies carry-forward. For gate (b)'s SBOM half on #18-#21 that denial is the opposite of what
happened: the SBOM result *was* carried forward (from 09:11Z and 07:33Z). The 73-03 executor
reconstructed the cell from 73-02's log rather than from the orchestrator's actual loop, and that
log's own "Task 3 FINAL" section defines gate (b) as "two-source ... the local transitive package
sweep ... returned 0" - naming one source while calling it two, which is where the overstatement
entered.

This threat exists so the record matches reality and is diffable. An unchallenged sentence claiming
a check ran that did not run is precisely the failure mode it names.

**Minimal correction, applied to `.planning/SECURITY-TRIAGE.md`:**

- Each of the five gate (b) cells now carries a `†` marker.
- The overstating paragraph is replaced by a dated, explicitly-labelled correction note
  (`† Correction to this table, added 2026-08-26 by the phase-73 security audit`) stating, per
  source: gate (a) ran five times, one per alert, immediately before each PATCH; gate (b)'s local
  source ran live inside the loop once, repo-wide; gate (b)'s SBOM half did not run inside the loop
  and last ran at 09:11Z and 07:33Z. It also records this audit's independent re-run of both sources
  and their agreement.
- The correction is additive and labelled, matching the file's established practice (plan 73-03
  already corrected "deleted" to "renamed away" the same way) rather than silently rewriting a cell.
- Incidental benefit: the bare `per D-16/D-17` citation is gone, replaced by the rule in plain
  language, so that sentence survives `/gsd-cleanup` archiving `73-CONTEXT.md`.

Verified after the edit: entry-1 heading count `1`, five per-alert data rows, five distinct GHSA
ids, zero `73-EVIDENCE-CAPTURE` references, and all five live `dismissed_comment` values still
present verbatim in the record.

## Accepted Risks Log

| ID | Risk | Severity | Rationale | Verified |
|---|---|---|---|---|
| 01/T-73-03 | Filtered rather than raw API capture in a repo-tracked file could omit context or carry a secret | low | Source data is public on a public repository; filtering is for reviewability (438 SBOM packages), not confidentiality | No `gho_` / `ghp_` / `github_pat_` / `Authorization:` string in any phase artifact or in the durable record; only scope *names* recorded |
| 02/T-73-04 | The `cryptic96` token carries `repo` (and extraneous `admin:org`), broader than the documented `public_repo` minimum for this endpoint | low | `repo` is a superset of the minimum; narrowing the token is out of phase scope and would not change the blast radius of five approved calls | Scopes re-checked read-only pre-gate and again by this audit: `admin:org, gist, repo`; `repo` present in `X-Accepted-Oauth-Scopes` |
| 03/T-73-05 | Phase closure could be held hostage by a new advisory landing on the surviving ghost manifests | low | By design: the ghost manifests survive the phase, so a new out-of-scope alert becomes entry two in the append-only log rather than a blocker | Live: `dependencyGraphManifests` `totalCount 13`, all five ghost `EuphoriaInn.*` manifests still listed; zero open alerts at audit time |

## Unregistered Flags

None of the three plan summaries carries a `## Threat Flags` section, so the register was never
extended during implementation. The following surfaced during execution, map to no threat ID, and
are recorded here as informational. None is blocking.

| Flag | What happened | Why it is not a gap |
|---|---|---|
| UF-1: mutating capability changed hands mid-phase | The harness Bash permission classifier denied the mutating PATCH to the executor subagent; the executor correctly refused to route around it; the orchestrator then executed the five approved calls directly. The register assumed an executor would hold the write capability and models no threat for that capability moving | The declared control was D-11 human approval, not the harness classifier. Approval is recorded at 07:47Z, before the 09:14Z PATCHes. Outcome independently verified: `dismissed_by=cryptic96`, comments byte-identical to the approved bodies, no write outside #17-#21 |
| UF-2: approved text was edited after the D-11 gate | All five comment bodies were changed from "deleted" to "renamed away" after approval, on the `a477ab9` R100-rename finding, and re-approved before sending | The corrected text is what landed (verified live, 5/5 byte match against the JSON bodies), the change is documented with its evidence, and the dismissal's substance is unchanged. Plan 73-02 anticipated exactly this path and required re-approval |
| UF-3: the D-11 approval artifact is a paraphrase, not a verbatim quote | Plan 73-02 Task 2 required appending the operator's *verbatim* response. `73-DISMISSAL-DRAFTS.md` records "Decision recorded by the orchestrator that relayed this plan's continuation: approve all five, exactly as drafted" - a paraphrase. The second (wording-correction) approval is likewise recorded in prose, inside the Task 3 section rather than as a second `## Approval outcome` block | The approval's existence, scope and timing are recorded and precede the PATCHes, and the operator can confirm or deny directly. This weakens the artifact's independence, not the outcome. Flagged for future phases: quote the operator verbatim when the artifact *is* the audit trail |
| UF-4: five `EuphoriaInn.*` directories still on disk | They look like they contradict the absence claim | Investigated before anything was sent and pre-empted by name in the durable record: gitignored `bin`/`obj` residue only, zero `.csproj`, zero tracked files (`git ls-files -- 'EuphoriaInn.*'` empty) |

## Follow-ups (non-blocking)

1. **`.planning/PROJECT.md` line 116 carries the same overstatement this audit corrected in the
   durable record.** Its phase-73 evolution entry says wave 2 "re-ran each alert's own two-part
   evidence gate immediately before that alert's own `PATCH`". PROJECT.md is permanently-loaded
   project context - exactly the boundary 03/T-73-03 names - so the imprecise clause outlives the
   phase. This audit does not edit PROJECT.md (outside its write scope). Suggested minimal edit:
   change that clause to "re-ran each alert's own manifest-attribution gate immediately before that
   alert's own `PATCH`, with the independent local package-absence sweep re-run live in the same
   sending loop", leaving the rest untouched.
2. **Dangling-reference hygiene in `.planning/SECURITY-TRIAGE.md`.** One bare
   `73-DISMISSAL-DRAFTS.md` filename citation and the `D-02` / `D-19` / `D-06` decision IDs will
   stop resolving once `/gsd-cleanup` archives the phase directory. Each is glossed or
   non-load-bearing today, so this is hygiene rather than a gap - worth a one-line plain-language
   gloss next time the file is appended to.

## Audit Method

Every assertion in this report was re-run read-only by the auditor on 2026-08-26. No mutating call
of any kind was issued: no PATCH, no PUT, no DELETE. Commands used: `gh auth status`;
`gh api .../dependabot/alerts` (list and per-alert GET); `gh api .../dependency-graph/sbom` (package
filter, SPDXID lookup, relationship trace); `gh api graphql dependencyGraphManifests`;
`gh api .../vulnerability-alerts` (GET only, returned `204`); `dotnet list <csproj> package
--include-transitive` across all five `QuestBoard.*` manifests; `git show --stat`, `git log`,
`git status --porcelain -- '*.csproj'`; and byte comparison of the live `dismissed_comment` values
against both the sent JSON bodies and the durable record.
