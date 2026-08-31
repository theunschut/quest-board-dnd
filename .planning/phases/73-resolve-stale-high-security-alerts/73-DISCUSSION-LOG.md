# Phase 73: Resolve Stale HIGH Security Alerts - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-26
**Phase:** 73-resolve-stale-high-security-alerts
**Areas discussed:** Graph refresh method, Dismissal reason + comment, Where evidence lives, Go / no-go rule

---

## Pre-discussion finding

Before the first question, read-only queries against GitHub's own APIs materially changed the phase's premise. ROADMAP.md's theory — "GitHub is minting alerts off a stale cached snapshot of a deleted manifest" — proved true but understated.

`GET /repos/{owner}/{repo}/dependency-graph/sbom` and GraphQL `repository.dependencyGraphManifests` showed GitHub's graph for `main` carrying a **ghost dependency set** (`Microsoft.AspNetCore.Identity` 2.3.1, `Identity.EntityFrameworkCore` 8.0.11, `Identity.UI` 8.0.11, `System.Security.Cryptography.Xml` 8.0.3) *alongside* the live 10.0.9 set, and still listing all five deleted `EuphoriaInn.*` manifests eight weeks after `a477ab9`.

Git archaeology pinned the ghost snapshot to a state predating `978d3f6` (2026-04-22). The decisive consequence: the alerted version **8.0.3** was upgraded to 10.0.9 on 2026-04-22 — four months before the alerts were minted on 2026-08-10 — so when the manifest was deleted it pinned a *non-vulnerable* version. This became D-02 and reshaped every area that followed.

---

## Graph refresh method

| Option | Description | Selected |
|--------|-------------|----------|
| Repair the graph, then dismiss | Evict the ghost manifests (alerts toggle, or a manifest-touching commit on `main`), re-query to prove they're gone, then dismiss survivors | |
| Evidence-only, no mutation | Treat the SBOM + `dependencyGraphManifests` queries as SECALERT-02's GitHub-sourced re-check; mutate nothing but the five dismissals | |
| Dismiss now, repair separately | Close the five on existing evidence, log ghost-manifest repair as its own follow-up phase | |
| You decide | Delegate to Claude | ✓ |

**User's choice:** You decide.

**Claude's call:** Evidence-only, no mutation (→ D-04). Reasoning: the evidence was already complete and GitHub-sourced, so dismissals needn't wait on a repair; the destructive lever was disqualified (below); and the safe lever collides with CLAUDE.md's never-commit-to-`main` rule.

### Follow-up: the destructive toggle

A read-only check found three pre-existing dismissals — **#7**, **#8** (`AutoMapper`) and **#11** (`SQLitePCLRaw.lib.e_sqlite3`), all by `cryptic96` — whose audit trail `DELETE`/`PUT /vulnerability-alerts` would put at risk.

| Option | Description | Selected |
|--------|-------------|----------|
| Confirm — don't touch it | Never toggle Dependabot alerts off/on in this repo; document the root cause instead | ✓ |
| Toggle is fine if history survives | Allow it, but export #7/#8/#11 into the phase record first | |
| Open a graph-repair phase | Confirm the veto and log a dedicated follow-up phase for eviction | |

**User's choice:** Confirm — don't touch it.
**Notes:** → D-05. Trading real audit trail for a cosmetic cache fix contradicts the phase's own purpose. Accepted consequence recorded as D-06: the ghost manifests survive, so a sixth alert is expected. This directly motivated D-13 (ongoing log rather than one-off record).

---

## Dismissal reason + comment

All five alerts share package, manifest, range `>= 8.0.0, <= 8.0.3` and patched version `8.0.4`. Only the CVE/GHSA, CVSS (8.1 for #17, 7.5 for the rest) and class (1 bypass, 4 DoS) differ.

### Reason code

| Option | Description | Selected |
|--------|-------------|----------|
| `inaccurate` | GitHub's attribution is factually wrong — alerting on a version absent since 2026-04-22 in a manifest absent since 2026-06-29 | ✓ |
| `not_used` (roadmap's note) | What the ROADMAP scope note named; means "package present, vulnerable path unreached" — a weaker and different claim | |
| Split by evidence | `inaccurate` by default, but confirmed per alert after opening each | |

**User's choice:** `inaccurate`.
**Notes:** → D-07. Deliberately overrides ROADMAP.md's scope note. `not_used` would understate the evidence and imply the package is actually present.

### Comment individuation

| Option | Description | Selected |
|--------|-------------|----------|
| Per-alert verification line | Each comment leads with what was checked for that alert — own CVE/GHSA, own range confirmed against the ghost manifest, own timestamp — then the shared conclusion and a pointer | ✓ |
| Shared evidence + own identifiers | One honest identical evidence sentence, each comment naming only its own CVE/GHSA | |
| Differentiate by CVE class | Give #17 (bypass) distinct reasoning from the four DoS alerts | |

**User's choice:** Per-alert verification line.
**Notes:** → D-08. The tension named openly during discussion: the evidence genuinely *is* identical, because it's evidence about the manifest rather than the CVE. Rewording five identical conclusions would fake independent analysis. The individuality had to live in verification that actually happened, not in prose. "Differentiate by CVE class" was rejected as decorative — vulnerability class has no bearing on the reachability argument.

### Acting identity

| Option | Description | Selected |
|--------|-------------|----------|
| `cryptic96` — match precedent | Active `gh` account and actor on all three existing dismissals | ✓ |
| `theunschut` — repo owner | More accountable signature, but breaks from existing history and needs an account switch | |
| You decide | Default to whichever account is active at execution | |

**User's choice:** `cryptic96` — match precedent. → D-10.

### Operator approval gate

| Option | Description | Selected |
|--------|-------------|----------|
| Show all five, approve once | All five drafted comments presented together, one approval, then all five PATCH calls fire | ✓ |
| Approve each alert separately | Five confirmations, matching per-alert reasoning with per-alert approval | |
| Approve after a dry run | Write all five into the phase record first, operator reads, then one go-ahead | |

**User's choice:** Show all five, approve once.
**Notes:** → D-11. Every comment is still read before anything posts; only the keystroke is shared. Five separate prompts were judged to risk approval fatigue — five reflexive yeses would be the rubber stamp arriving through the back door.

---

## Where evidence lives

Context: PROJECT.md is ~105KB and loaded every session; `/gsd-cleanup` archives phase directories at milestone close, so a phase-local pointer would break exactly when a reviewer needs it.

| Option | Description | Selected |
|--------|-------------|----------|
| Durable file + two PROJECT.md hooks | Full evidence in `.planning/SECURITY-TRIAGE.md`; PROJECT.md gets a Known Issues bullet and a Key Decisions row, both linking it | ✓ |
| All of it into PROJECT.md | Literal SECALERT-04 reading, immune to stale pointers, but bulks up a file already loaded as context every session | |
| Phase artifact + PROJECT.md summary | Standard GSD shape, but the pointer breaks when the phase dir is archived at v9.0 close | |

**User's choice:** Durable file + two PROJECT.md hooks. → D-12, D-14.

### Form of the evidence

| Option | Description | Selected |
|--------|-------------|----------|
| Conclusion + re-runnable appendix | Narrative and per-alert table up top; appendix of exact `gh` commands with filtered captured output and timestamps | ✓ |
| Narrative + facts table | Compact and skimmable, but findings must be taken on trust | |
| Raw output only | Maximally objective, but the SBOM alone is 438 packages — unfiltered dumps bury the three facts that matter | |

**User's choice:** Conclusion + re-runnable appendix.
**Notes:** → D-15. Identified as the artifact that structurally separates triage from a rubber stamp; without it SECALERT-04 is satisfied only in form.

### Log scope

| Option | Description | Selected |
|--------|-------------|----------|
| Ongoing log, this as entry one | Append-only dated log; a sixth alert is expected by design (D-06), so the next reviewer inherits precedent and queries | ✓ |
| This incident only | Simpler, nothing to maintain, but the next stale alert starts from zero | |
| You decide | Planner chooses based on how the appendix turns out | |

**User's choice:** Ongoing log, this as entry one. → D-13.

---

## Go / no-go rule

### The gate

| Option | Description | Selected |
|--------|-------------|----------|
| Two-part per-alert gate | Per alert: (a) `manifest_path` is one of the five ghost manifests, (b) the package at a version in that alert's range is absent from every `QuestBoard.*` manifest on `main` | ✓ |
| Re-run today's queries wholesale | Confirm the overall picture is unchanged, then dismiss all five — a batch check, so a single changed attribution slips through | |
| Evidence is sufficient, proceed | Fastest, but comments would cite evidence gathered at a different time than the action | |

**User's choice:** Two-part per-alert gate.
**Notes:** → D-16. The gate and the evidence are the same act — the check D-08's comment cites is performed at the moment of action rather than quoted from this discussion.

### Partial failure

| Option | Description | Selected |
|--------|-------------|----------|
| Dismiss the clean ones, escalate the failure | Passing alerts dismissed on their own evidence; failing one written up as an open finding | ✓ |
| Abort everything, escalate | Most conservative, but treats five independently-verified alerts as one unit | |
| Stop and ask in the moment | Maximum control, but leaves the executor unable to act unsupervised | |

**User's choice:** Dismiss the clean ones, escalate the failure.
**Notes:** → D-17. Per-alert reasoning implies per-alert outcomes; holding four defensible dismissals hostage to one anomaly would be batch thinking wearing a different hat.

### If the vulnerability is real

| Option | Description | Selected |
|--------|-------------|----------|
| Out of scope — stop and report | Triage succeeded at its job; the fix is a code change with build, tests and deploy — a different phase | ✓ |
| In scope — patch it here | Avoids a HIGH sitting open, but turns a documentation phase into a code phase mid-flight with no plan or tests | |
| Patch only if trivial | Pragmatic, but "trivial" is the judgement call that expands once someone is mid-change | |

**User's choice:** Out of scope — stop and report. → D-18.

### Closing criterion

| Option | Description | Selected |
|--------|-------------|----------|
| Close on the five, log the sixth | Read criterion #5 as "#17–#21 dismissed and no HIGH this phase was scoped to handle remains open" | ✓ |
| Literal reading — phase blocks | Honours the written criterion exactly, but makes closure hostage to GitHub's advisory feed | |
| Amend the criterion now | Rewrite ROADMAP.md criterion #5 to name #17–#21 explicitly | |

**User's choice:** Close on the five, log the sixth.
**Notes:** → D-19. ROADMAP text left unedited; the reading is recorded in CONTEXT.md instead. D-06 guarantees the feed will fire again, so the literal reading would make the phase permanently uncloseable.

---

## Claude's Discretion

- **Graph refresh framing** — user answered "you decide"; call recorded as D-01/D-04/D-06 with full reasoning.
- Exact wording of the five dismissal comments, within D-08's shape and D-09's character budget.
- The precise `jq`/`node` filter expressions captured in the D-15 appendix.
- Section structure and entry format of `.planning/SECURITY-TRIAGE.md`, and exact wording of the two PROJECT.md hooks.
- Whether the D-16 gate is a script run per alert or inline commands, and how output is captured into the appendix.
- Whether to backfill #7/#8/#11 into the new log as historical context.

## Deferred Ideas

- **Evict the ghost manifests from GitHub's dependency graph** — the actual root cause; blocked by the D-05 veto and the never-commit-to-`main` rule. Revisit when `milestone/v9-rolling-improvements` merges to `main` naturally.
- **Delete the leftover `EuphoriaInn.*` `bin/`/`obj/` directories** — gitignored, local-only, invisible to GitHub; ROADMAP already calls it optional.
- **Patching `System.Security.Cryptography.Xml`** — only real work if the D-16 gate fails part (b); D-18 routes it to its own phase.
- **Backfilling #7/#8/#11 into `.planning/SECURITY-TRIAGE.md`** — planner discretion, no requirement covers it.
