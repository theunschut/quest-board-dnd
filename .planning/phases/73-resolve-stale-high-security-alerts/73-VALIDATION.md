---
phase: 73
slug: resolve-stale-high-security-alerts
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-26
---

# Phase 73 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

**This phase produces no code and no automated test suite.** Validation here means re-runnable,
observable **state assertions** against GitHub and the local repo — not unit or integration tests.
Every success criterion below is a command whose output is checked, not assumed. Nyquist compliance
is satisfied by that property, not by test coverage. Do not let a downstream agent restate these as
"tests"; they are state assertions and the record must say so.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | none — no code under test. Assertion tools: `gh` 2.89.0, `dotnet` SDK 10.0.400, `git`, `grep` |
| **Config file** | none |
| **Quick run command** | `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/{n}` (per-alert state read) |
| **Full suite command** | see **Phase Gate Assertion** below |
| **Estimated runtime** | ~5 seconds per read; ~60 seconds for a full `dotnet list package --include-transitive` sweep |

**Note:** `dotnet build` / `dotnet test` (the project's configured build and test commands) are **not**
part of this phase's validation. No source file changes, so a green build proves nothing about this
phase's goal. Running them is not forbidden, but a green build is not evidence for any requirement here.

---

## Sampling Rate

- **Per alert action (D-16 / D-17):** re-run the two-source gate immediately before *that alert's* PATCH —
  not once for all five up front. D-17 requires per-alert independence, so a single up-front sweep would
  break the contract it is meant to enforce.
- **After each PATCH:** re-read that alert and assert `state`, `dismissed_reason`, `dismissed_by.login`,
  and that `dismissed_comment` matches the operator-approved draft verbatim.
- **Phase gate:** the SECALERT-05 assertion below, once, after all approved PATCHes complete.
- **Max feedback latency:** ~5 seconds (single API read per assertion).
- **Freshness rule:** RESEARCH.md's captured JSON is a 2026-08-26 snapshot. Re-run the alert list and the
  GraphQL manifest query live before the D-11 approval gate — do not assert against the snapshot.

---

## Per-Requirement Verification Map

Task IDs are filled in once plans exist; the assertions themselves are fixed by research and do not
depend on how the planner splits the work.

| Req ID | Behavior | Type | Re-runnable Command | Expected Result |
|--------|----------|------|---------------------|-----------------|
| SECALERT-01 | Each of #17–#21 opened individually; manifest path + detection timestamp recorded; branch scope acknowledged | state assertion (read) | `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/{n}` for n in 17..21 (5 separate calls) | Each returns `dependency.manifest_path` matching an `EuphoriaInn.*` path and `created_at` on `2026-08-10`; all five appear individually in `.planning/SECURITY-TRIAGE.md` |
| SECALERT-02 | GitHub's own server-side view re-checked (SBOM + GraphQL), no force-refresh, no mutation | state assertion (read) | SBOM filter + GraphQL `dependencyGraphManifests` query (verbatim commands in RESEARCH.md §Research Question 3) | SBOM contains exactly one `System.Security.Cryptography.Xml` entry, at `8.0.3`, reachable only via `Microsoft.AspNetCore.Identity 2.3.1`; GraphQL still lists the five ghost `EuphoriaInn.*` manifests |
| SECALERT-03 | Each alert dismissed individually with `dismissed_reason=inaccurate` and its own evidence-citing comment | state assertion (read, post-PATCH) | `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/{n}` per alert | `state == "dismissed"`, `dismissed_reason == "inaccurate"`, `dismissed_by.login == "cryptic96"`, `dismissed_comment` byte-identical to the approved draft for that alert |
| SECALERT-04 | Investigation and outcome recorded in `.planning/PROJECT.md` | state assertion (file content) | `grep -n "SECURITY-TRIAGE" .planning/PROJECT.md` | 2 matches — one in the `## Context` → *Known issues / tech debt* list, one in the `## Key Decisions` table |
| SECALERT-05 | No open HIGH alert this phase was scoped to handle remains open | state assertion (read) | Phase Gate Assertion below | `0`, per the D-19 reading |

---

## Phase Gate Assertion

```bash
gh api repos/theunschut/quest-board/dependabot/alerts \
  --jq '[.[] | select(.state=="open" and .security_vulnerability.severity=="high")] | length'
```

Expected: `0`.

**D-19 qualifier — the honest reading.** This asserts *"no HIGH alert this phase was scoped to handle
remains open."* If a **new** HIGH alert (not #17–#21) has appeared since 2026-08-26, this command returns
non-zero and the phase still closes: D-06 guarantees the ghost manifests keep minting alerts, so a literal
"must return 0" gate would make closure hostage to GitHub's advisory feed. In that case the required
outcome is that the new alert is logged as its own dated entry in `.planning/SECURITY-TRIAGE.md` and
`select(.number | IN(17,18,19,20,21))` returns empty. Record which reading was used in the phase summary
rather than silently accepting a non-zero result.

Note the URL uses the **old** repo name `quest-board` — success criterion 5 quotes it literally and it
still resolves via GitHub's rename redirect. The GraphQL queries need the real name `quest-board-dnd`.

---

## Wave 0 Requirements

None. Every assertion above runs today with tools already installed (`gh`, `dotnet`, `git`, `grep`).
No test framework, fixture, or harness setup is required — this phase produces no code.

---

## Pre-Flight Assertions (before any mutation)

These are not requirement checks; they are the go/no-go conditions that must hold before the first PATCH.
They belong to the D-11 operator gate.

| Check | Command | Required result |
|-------|---------|-----------------|
| Active `gh` account is `cryptic96` (D-10) | `gh auth status` | active account `cryptic96` |
| Token carries write scope for the endpoint (RQ-2) | `gh api repos/theunschut/quest-board-dnd/dependabot/alerts -i` and read `X-OAuth-Scopes` / `X-Accepted-Oauth-Scopes` | active token's scopes satisfy the accepted set; no dismissal attempted to find out |
| Alert list still matches the plan | `gh api .../dependabot/alerts --jq '[.[] \| select(.state=="open")] \| .[].number'` | `17,18,19,20,21` — investigate before proceeding if the set differs |
| D-16 gate part (a), per alert | `gh api .../dependabot/alerts/{n} --jq '.dependency.manifest_path'` | an `EuphoriaInn.*` path |
| D-16 gate part (b), per alert, two-source | fresh `dotnet list package --include-transitive` across all five `QuestBoard.*.csproj`, **plus** the SBOM filter | zero matches locally; SBOM's single match fully ghost-attributed |

A failure on part (a) or (b) for one alert stops **that alert only** (D-17). Per D-18, patching is out of
scope regardless of what the gate reveals — a genuine reachability finding is written up for the operator,
not fixed here.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Instructions |
|----------|-------------|------------|--------------|
| Operator approves all five dismissal comments in one review (D-11) | SECALERT-03 | Irreversible outward-facing action on a public repo; permanent audit metadata. Cannot be automated by design — automating it *is* the rubber stamp the phase exists to avoid. | Present all five drafted comments and their per-alert verification lines together; operator gives one explicit approval; only then do the five PATCH calls fire. No dismissal is posted without it. |
| Durable record reads as evidence, not as a summary | SECALERT-04 | "A sceptic six months out can re-run this" is a judgement a command cannot assert | Confirm `.planning/SECURITY-TRIAGE.md` contains the conclusion, the per-alert table, and the appendix of exact commands with their filtered captured output and timestamps (D-15) |

---

## Validation Sign-Off

- [ ] Every requirement above has a re-runnable command with a stated expected result
- [ ] Pre-flight assertions run and pass before the first PATCH
- [ ] D-16 gate re-run per alert immediately before that alert's PATCH, not batched
- [ ] Post-PATCH read-back performed for each dismissed alert
- [ ] Phase gate assertion run once after all PATCHes, with the D-19 reading recorded explicitly
- [ ] No assertion in this phase is described as a "test" in any phase artifact
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
