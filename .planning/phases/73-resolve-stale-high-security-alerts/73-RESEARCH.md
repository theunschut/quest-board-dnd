# Phase 73: Resolve Stale HIGH Security Alerts - Research

**Researched:** 2026-08-26
**Domain:** GitHub Dependabot Alerts REST/GraphQL API — evidence-based alert triage and dismissal (not a code phase)
**Confidence:** HIGH (every claim below was either executed live against `theunschut/quest-board-dnd` in this session, or cited from GitHub's own documentation/changelog — see per-question provenance)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** The ROADMAP premise is understated. GitHub's dependency graph for `main` carries two dependency sets simultaneously — the live QuestBoard one and a ghost set from before the .NET 10 upgrade. `System.Security.Cryptography.Xml 8.0.3` hangs off `Microsoft.AspNetCore.Identity 2.3.1` — the ghost branch, not anything reachable from a live manifest.
- **D-02:** The strongest single fact is the version, not the deleted file. The alerts' vulnerable range is `>= 8.0.0, <= 8.0.3`. The manifest pinned `System.Security.Cryptography.Xml` at 10.0.9 — not in that range — before deletion. The version GitHub is alerting on was upgraded away on 2026-04-22, four months before the alerts were minted on 2026-08-10.
- **D-03:** SECALERT-02 is satisfied by GitHub's own server-side APIs, no mutating action: `GET .../dependency-graph/sbom` and GraphQL `repository.dependencyGraphManifests` (needs `Accept: application/vnd.github.hawkgirl-preview+json`), returning 13 manifests (8 real + 5 ghost `EuphoriaInn.*` `.csproj` files).
- **D-04:** No force-refresh is attempted. Nothing on the repo is mutated except the five dismissals.
- **D-05:** The Dependabot alerts off/on toggle (`DELETE`/`PUT /repos/{owner}/{repo}/vulnerability-alerts`) is vetoed outright — not just for this phase, for this repo. It would evict ghost manifests but puts the existing #7/#8/#11 dismissal audit trail at risk. Operator confirmed explicitly.
- **D-06:** Accepted consequence — the ghost manifests survive this phase; GitHub will keep minting alerts off them. Revisit when `milestone/v9-rolling-improvements` merges to `main` naturally (a manifest-touching commit is the only remaining non-destructive lever).
- **D-07:** `dismissed_reason` is `inaccurate`, not `not_used` (deliberately overrides the ROADMAP scope note). GitHub's attribution is factually wrong: alerting on a version absent since 2026-04-22, in a manifest absent since 2026-06-29.
- **D-08:** Each comment leads with a per-alert verification line, then the shared conclusion, then a pointer to the durable record. Individuality lives in the verification that happened (own CVE/GHSA, own range confirmed, own check timestamp), not in reworded prose.
- **D-09:** `dismissed_comment` is a short field — believed capped at 280 characters. **The planner MUST verify the real limit against the API before drafting** (see Research Question 1 below — now verified).
- **D-10:** All five are posted as `cryptic96` (currently active `gh` account, actor on all three existing dismissals). Executor must confirm the active account before the first PATCH.
- **D-11:** Operator gate — the executor presents all five drafted comments and verification lines together, the operator approves once, then all five PATCH calls fire. No dismissal is posted without this approval.
- **D-12:** Full evidence goes in a new durable file, `.planning/SECURITY-TRIAGE.md`, outside the phase directory (survives `/gsd-cleanup` archival; PROJECT.md is already ~105KB).
- **D-13:** `.planning/SECURITY-TRIAGE.md` is an append-only dated log, this incident as entry one. A sixth alert is expected (D-06), so the log format anticipates future entries.
- **D-14:** PROJECT.md gets two hooks: a **Known issues / tech debt** bullet under `## Context` for the ghost-manifest root cause (with re-runnable reproduction query, noting the D-05 veto), and a **Key Decisions** table row for the dismiss-not-patch call. Both link `.planning/SECURITY-TRIAGE.md`.
- **D-15:** The record is a conclusion plus a re-runnable appendix. Narrative + per-alert table up top; exact `gh` commands with filtered captured output and timestamps below (SBOM relationship query, `dependencyGraphManifests` query, `git log -S` archaeology). Filtered, not raw — the SBOM alone is 438 packages.
- **D-16:** Two-part per-alert gate, re-verified immediately before each PATCH: (a) `manifest_path` resolves to one of the five `EuphoriaInn.*` ghost manifests, and (b) `System.Security.Cryptography.Xml` at a version inside that alert's range is absent from every `QuestBoard.*` manifest on `main`. Both must pass or that alert is not dismissed.
- **D-17:** A gate failure on one alert does not block the others. Per-alert reasoning implies per-alert outcomes.
- **D-18:** If gate part (b) fails — package genuinely reachable from a `QuestBoard.*` manifest — patching is OUT of scope. Executor writes up the finding, dismisses nothing for that alert, operator scopes the follow-up.
- **D-19:** Success criterion #5 is read as "alerts #17–#21 are dismissed and no HIGH alert this phase was scoped to handle remains open." A sixth unrelated HIGH alert landing mid-phase does not block closure — it becomes entry two in `.planning/SECURITY-TRIAGE.md`.

### Claude's Discretion

- Exact wording of the five dismissal comments, within D-08's shape and D-09's character budget.
- The precise `jq`/`node` filter expressions captured in the D-15 appendix.
- Section structure and entry format of `.planning/SECURITY-TRIAGE.md`, and the exact wording of the two PROJECT.md hooks.
- Whether the D-16 gate is a script the executor runs per alert or inline commands, and how its output is captured into the appendix.
- Whether to record the three pre-existing dismissals (#7, #8, #11) in the new log as historical context.

### Deferred Ideas (OUT OF SCOPE)

- Evict the ghost manifests from GitHub's dependency graph (blocked by D-05 veto and the never-commit-to-`main` rule; revisit when `milestone/v9-rolling-improvements` merges).
- Delete the leftover `EuphoriaInn.*` `bin/`/`obj/` directories in the working tree (gitignored, cosmetic, zero bearing on alerts).
- Patching `System.Security.Cryptography.Xml` (only in scope if D-16 gate part (b) fails, routed to its own phase per D-18).
- Backfilling `#7`/`#8`/`#11` into `.planning/SECURITY-TRIAGE.md` as historical entries (discretionary, no requirement covers it).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SECALERT-01 | Investigate each of #17–#21 individually — branch scope, manifest attribution, dependency-graph freshness confirmed per alert, before any closure | See Research Question 3 (`GET .../alerts/{n}` verified field set) and Research Question 4 (all 5 confirmed open, live). **Important scope correction:** the alert payload carries no branch field at all — see "Branch attribution" finding below. Manifest attribution and per-alert CVE/range are fully available and verified. |
| SECALERT-02 | Dependency graph re-checked via GitHub's own server-side APIs (no force-refresh, per D-04) | SBOM query and GraphQL `dependencyGraphManifests` query both executed live in this session — see Research Questions 3, 6. Both confirm D-01/D-03's claims exactly. |
| SECALERT-03 | Each alert closed individually with `dismissed_reason=inaccurate` and evidence-citing comment, no bulk action | Exact `PATCH` command shape given (Research Question 3); `dismissed_comment` 280-char limit verified (Research Question 1); token scope preflight verified (Research Question 2). |
| SECALERT-04 | Investigation and outcome recorded in `.planning/PROJECT.md` | Exact insertion points (line numbers, current heading text) captured in Research Question 7; `.planning/SECURITY-TRIAGE.md` confirmed not to exist yet; house style precedent identified. |
| SECALERT-05 | `gh api repos/theunschut/quest-board/dependabot/alerts` returns no open HIGH alerts once phase closes | Command verified to work today via the `quest-board` → `quest-board-dnd` redirect (Research Question 4); current state is exactly 5 open HIGH alerts, all `System.Security.Cryptography.Xml`, all attributable to #17–#21. |
</phase_requirements>

## Summary

All read-only evidence CONTEXT.md's D-01 through D-19 rely on was independently re-verified live against `theunschut/quest-board-dnd` in this research session — nothing in CONTEXT.md's factual claims about GitHub state, git history, or package manifests failed to reproduce. Beyond confirming CONTEXT.md, this session surfaced two findings CONTEXT.md did not have and the planner should carry forward:

1. **`dismissed_comment`'s 280-character cap is real but is documented only in GitHub's 2022-08-22 changelog announcement, not restated in the current REST API reference page's parameter table.** Treat it as CITED (not directly machine-verifiable without a forbidden mutating call) and draft comments with defensive margin under 280, not right up against it.
2. **The alert payload carries no branch/ref field whatsoever** — confirmed by fetching alert #17 in full. Dependabot alerts are a default-branch-only construct; there is no per-branch dimension to attribute. Success criterion 1's "branch attribution" cannot be literally satisfied by a payload field because no such field exists in this API — it can only be satisfied by the fact (documented in GitHub's own docs) that Dependabot alerts are scoped exclusively to the default branch (`main`), which the alert's `html_url`/`manifest_path` confirm. The planner should scope the SC1 task around "confirmed to be default-branch-scoped" rather than "extracted a branch field," so no task is written against a field that cannot exist.

A third finding sharpens D-02's evidence without contradicting it: **the same five GHSA advisories affect not just the `8.0.0–8.0.3` range GitHub alerted on, but also `9.0.0–9.0.17` and `10.0.0–10.0.9`** — and the manifest's actual last live value before deletion was `10.0.9`, which sits inside that third vulnerable band (patched only at `10.0.10`). This means the 2026-04-22 upgrade away from `8.0.3` did **not**, by itself, take the package out of a vulnerable version for this CVE family — the real end of exposure was the 2026-06-29 manifest deletion. D-02's dismissal argument ("the alerted version was upgraded away") is still literally true and sufficient (GitHub is alerting on the specific `8.0.0–8.0.3` range, which the manifest genuinely never touched after 2026-04-22), but the durable record should state the fuller, more accurate timeline rather than implying the 2026-04-22 upgrade eliminated the vulnerability outright. Both local `dotnet list package --include-transitive` (fresh, ground-truth, immune to GitHub's own staleness) and GitHub's SBOM independently confirm the package is entirely absent from the current live graph today, at any version — so the D-16 gate passes cleanly regardless of which version-range framing is used.

**Primary recommendation:** Run the D-16 gate as a two-source check (local `dotnet list package --include-transitive` as ground truth + GitHub SBOM as corroboration) rather than trusting GitHub's SBOM alone, since GitHub's own graph is the thing already proven stale in this phase. All other CONTEXT.md decisions are directly executable as written; the exact commands and their real output are captured below so the plan/executor can act without re-deriving anything.

## Architectural Responsibility Map

This phase touches no application tier. It is a GitHub-API-and-planning-docs phase.

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Alert investigation (read) | External service (GitHub REST/GraphQL API) | — | All evidence-gathering is `GET`/GraphQL queries against GitHub, not local app code |
| Alert dismissal (write) | External service (GitHub REST API) | — | `PATCH .../dependabot/alerts/{n}`, no application code involved |
| Local ground-truth cross-check | Build tooling (`dotnet` CLI) | — | `dotnet list package --include-transitive` reads the local restored graph, independent of GitHub's cache |
| Durable record | Repository / Storage (`.planning/` docs) | — | New file `.planning/SECURITY-TRIAGE.md` plus two `PROJECT.md` hooks — plain markdown, no database, no migration |

## Standard Stack

Not applicable — this phase installs no packages and writes no application code. The only "stack" is the `gh` CLI (already installed, v2.89.0) and the .NET SDK (already installed, `10.0.400`) for the `dotnet list package` ground-truth check.

## Package Legitimacy Audit

Not applicable — this phase installs zero external packages. Skip per the Package Legitimacy Gate protocol's own scope condition ("whenever this phase installs external packages").

## Architecture Patterns

Not an application-architecture phase. The relevant "pattern" is the investigation-and-dismissal procedure itself, which decomposes into four stages per alert, run identically for #17–#21:

### Procedure Flow Diagram

```
 [GitHub Dependabot Alert #N]
          |
          v
 (1) GET /dependabot/alerts/{N} ---------> manifest_path, vulnerable_version_range,
          |                                first_patched_version, created_at, html_url
          v
 (2) D-16 Gate part (a): manifest_path in {5 EuphoriaInn.* ghost manifests}?
          |                         \
         yes                        no --> STOP, not this phase's alert (shouldn't happen for #17-21)
          v
 (3) D-16 Gate part (b), TWO independent checks:
          |
          +--> dotnet list package --include-transitive (5x QuestBoard.*.csproj)
          |         --> package absent at any version?  (ground truth, local, fresh)
          |
          +--> GET /dependency-graph/sbom + GraphQL dependencyGraphManifests
                    --> package's only DEPENDS_ON edge is from the ghost Identity 2.3.1
                        node, and the ghost manifest still appears in the 13-manifest list?
                        (GitHub's own corroboration — NOT sole evidence, since GitHub's
                        graph is the thing already proven stale)
          |
         both pass?
          |                         \
         yes                        no --> STOP for this alert (D-17), write up as open
          v                              finding (D-18), do not dismiss, no patch (out of scope)
 (4) Draft per-alert comment (D-08 shape, <280 chars, D-09) --> present all 5 + verification
     lines together --> single operator approval (D-11) --> PATCH state=dismissed,
     dismissed_reason=inaccurate, dismissed_comment=... for cryptic96 (D-10)
          |
          v
 (5) Append entry to .planning/SECURITY-TRIAGE.md (D-12/13/15) + 2 PROJECT.md hooks (D-14)
```

### Recommended file layout for this phase's deliverables

```
.planning/
├── SECURITY-TRIAGE.md          # NEW — D-12/13/15, append-only dated log, entry one = this incident
├── PROJECT.md                   # EDIT — 2 hooks: Known issues bullet (~line 161) + Key Decisions row (~line 238)
└── phases/73-.../73-RESEARCH.md # this file
```

### Anti-Patterns to Avoid

- **Trusting GitHub's SBOM as the sole source for the D-16 gate's absence check.** The whole premise of this phase is that GitHub's dependency graph can be stale. Using only the SBOM to prove absence would be circular reasoning — corroborate it with a fresh local `dotnet list package --include-transitive` run.
- **Writing a task that expects a "branch" field in the Dependabot alert JSON.** It does not exist (verified below). Scope SC1's "branch attribution" language around "confirmed default-branch scope," not a field extraction.
- **Treating the 2026-04-22 version bump as having eliminated the vulnerability.** It only moved the package out of the *specific* `8.0.0–8.0.3` range the alert cites; the same advisory family continued to cover the package (at `10.0.7`, later `10.0.9`) until the 2026-06-29 manifest deletion. State this precisely in the durable record.

## Don't Hand-Roll

Not applicable — no code, no libraries, no reimplementation risk. The one thing to explicitly *not* build: a custom force-refresh/cache-bust mechanism for GitHub's dependency graph. D-04 vetoes this; GitHub's own read-only APIs already supply sufficient evidence without it.

## Common Pitfalls

### Pitfall 1: Assuming the alert's version-range field is the only vulnerable range for that CVE
**What goes wrong:** Concluding "the package was upgraded away from the vulnerable version on 2026-04-22" without checking whether the *later* version it was upgraded to (`10.0.7`, then `10.0.9`) is also inside a vulnerable range for the same advisory.
**Why it happens:** The alert's top-level `security_vulnerability.vulnerable_version_range` field only shows the range matching the .NET target framework GitHub detected in the (stale) manifest — `8.0.0–8.0.3`. The full advisory (`security_advisory.vulnerabilities[]`, plural) lists all affected ranges across .NET 8/9/10.
**How to avoid:** Always inspect the full `vulnerabilities[]` array, not just the alert's single `security_vulnerability` object, before writing the dismissal narrative. Verified in this session — see Research Question 6 below.
**Warning signs:** A dismissal comment or durable-record entry that implies "upgrading the version fixed it" without checking the destination version against the full advisory.

### Pitfall 2: Relying only on GitHub's SBOM to prove D-16 gate part (b)
**What goes wrong:** The SBOM endpoint is itself generated from GitHub's dependency-graph cache — the same subsystem already shown to be stale (D-01/D-06). Treating its absence-of-a-version as proof risks a circular argument a skeptical future reviewer (per CONTEXT.md's own framing) would catch immediately.
**How to avoid:** Pair the SBOM check with a fresh local `dotnet restore` + `dotnet list package --include-transitive` across all 5 `QuestBoard.*.csproj` files. Both were run in this session with concordant results (see Research Question 5).
**Warning signs:** A gate script or appendix entry that cites only the GitHub SBOM/GraphQL output.

### Pitfall 3: Windows/PowerShell quoting breaking the `dismissed_comment` PATCH body
**What goes wrong:** `gh api ... -f dismissed_comment="..."` with embedded apostrophes (e.g. "GitHub's cache") or multi-line text can break PowerShell's quoting, silently truncating or mis-escaping the comment.
**How to avoid:** Pass the comment via a JSON input file (`gh api ... --input file.json`) rather than inline `-f` flags, or use `--field` with a heredoc-free single-line string with apostrophes avoided/escaped. See the exact command shape in Research Question 3 below — this project develops on Windows (per CLAUDE.md), so this is a real risk, not a hypothetical.
**Warning signs:** A PATCH that "succeeds" (200 OK) but the resulting `dismissed_comment` on GitHub is truncated or garbled compared to the draft — always re-`GET` the alert after dismissal to confirm the comment landed intact.

### Pitfall 4: Treating `main` HEAD drift during this phase as invalidating D-06's "no manifest-touching commit yet" premise
**What goes wrong:** Between CONTEXT.md being gathered (2026-08-26, `main` at `69f2ea0`) and this research running (same day, `main` now at `89a8cb6` after Phase 72's merge PR #145), `main`'s HEAD advanced. A hasty read could assume this invalidates D-06.
**Why it happens:** `main` moves independently of this phase via other merged work.
**How to avoid:** Diff the two commits for `*.csproj` changes before assuming anything changed — verified in this session: `git diff --stat 69f2ea0 89a8cb6 -- '*.csproj'` returns empty. No manifest touched; D-06's premise still holds. Re-run this diff at execution time in case `main` moves again before the phase closes.
**Warning signs:** Skipping this recheck and asserting the ghost-manifest situation "as of 2026-08-26" without re-confirming against the `main` HEAD current at execution time.

## Code Examples

All commands below were executed live against `theunschut/quest-board-dnd` in this research session; output is the actual response, filtered where huge. None of these are mutating calls.

### Research Question 1 — `dismissed_comment` length cap [ANSWERED]

**Documented limit: 280 characters.**

Source: GitHub's official changelog announcement introducing the field — [github.blog/changelog/2022-08-22-dependabot-alerts-optional-dismissal-comment-2](https://github.blog/changelog/2022-08-22-dependabot-alerts-optional-dismissal-comment-2/) — quoted verbatim: *"These comments (maximum 280 characters) are viewable in the alert timeline."*

**Caveat (why this is `[CITED]` not `[VERIFIED]`):** The current live REST API reference page ([docs.github.com/en/rest/dependabot/alerts#update-a-dependabot-alert](https://docs.github.com/en/rest/dependabot/alerts?apiVersion=2022-11-28#update-a-dependabot-alert)) does **not** restate a `maxLength` constraint in its `dismissed_comment` parameter row — it just says "An optional comment associated with dismissing the alert." The 280-char cap comes from the original 2022 feature-announcement changelog post, not the current parameter schema table. There is no read-only way to re-confirm the exact numeric limit without attempting a write (forbidden in this session).

**Recommendation for the planner:** Treat 280 as real (it is GitHub's own public statement and has not been retracted), but draft every comment with a safety margin — target **≤ 260 characters**, not flush against 280, since GitHub's exact enforcement point (before/after trimming whitespace, unicode handling, etc.) is unverified. This independently confirms D-09's "if the cap holds, full evidence cannot live in the comment" premise — the D-11 split (short comment + pointer to `.planning/SECURITY-TRIAGE.md`) is required regardless of the exact number, since even 280 characters cannot hold a CVE ID, GHSA ID, manifest path, range, and conclusion in readable prose.

**Safe confirmation path for the executor (no mutation):** None exists beyond re-checking this citation. If the operator wants harder confirmation before the real PATCH run, the only fully safe method is the D-11 gate itself — GitHub will return `422 Unprocessable Entity` on an oversized comment at the moment of the real (approved, non-hypothetical) PATCH call, and the phase's per-alert independence (D-17) means one length-related 422 does not block the other four.

### Research Question 2 — Token scope pre-flight for `PATCH .../dependabot/alerts/{n}` [ANSWERED]

**Documented requirement** (docs.github.com/en/rest/dependabot/alerts, "Update a Dependabot alert"): *"OAuth app tokens and personal access tokens (classic) need the `security_events` scope to use this endpoint. If this endpoint is only used with public repositories, the token can use the `public_repo` scope instead."*

**`theunschut/quest-board-dnd` is public** (confirmed via CONTEXT.md and reconfirmed by the fact all `gh api` calls in this session succeeded without an org/private-repo scope requirement) — so `public_repo` is sufficient, and `repo` (a strict superset of `public_repo`) definitely qualifies.

**Live pre-flight check — read-only, run against `GET` (same endpoint family), zero mutation risk:**

```bash
gh api repos/theunschut/quest-board-dnd/dependabot/alerts -i -X GET -f state=open -f severity=high
```

Actual response headers observed in this session:
```
X-Accepted-Oauth-Scopes: admin:repo_hook, delete_repo, read:repo_hook, repo, repo:invite, repo:status, repo_deployment, security_events, write:repo_hook
X-Oauth-Scopes: admin:org, gist, repo
```

`repo` appears in **both** lists — the active token (`cryptic96`) already holds a scope GitHub explicitly accepts for this endpoint family. **CONFIRMED SUFFICIENT.** `admin:org` and `gist` are extraneous to this task but harmless.

**Caveat:** This header check was run against the `GET` list endpoint (mutating calls are forbidden this session), not the `PATCH` endpoint itself. GitHub's own docs state the identical scope requirement for both read and write Dependabot-alert endpoints, and `X-Accepted-Oauth-Scopes` is a per-resource (not per-verb) header in GitHub's REST implementation, so this is expected to carry over unchanged. **Recommend the executor re-run this exact `-i` header check immediately before the D-11 operator-approval gate** as a final, zero-risk confirmation, rather than assuming it silently.

### Research Question 3 — Verified command syntax

**(a) `GET /repos/theunschut/quest-board-dnd/dependabot/alerts/{n}`** — confirmed fields (alert #17, full JSON captured):

```bash
gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 -X GET
```

```json
{
  "number": 17,
  "state": "open",
  "dependency": {
    "package": { "ecosystem": "nuget", "name": "System.Security.Cryptography.Xml" },
    "manifest_path": "EuphoriaInn.Domain/EuphoriaInn.Domain.csproj",
    "scope": "runtime",
    "relationship": "direct"
  },
  "security_vulnerability": {
    "severity": "high",
    "vulnerable_version_range": ">= 8.0.0, <= 8.0.3",
    "first_patched_version": { "identifier": "8.0.4" }
  },
  "url": "https://api.github.com/repos/theunschut/quest-board-dnd/dependabot/alerts/17",
  "html_url": "https://github.com/theunschut/quest-board-dnd/security/dependabot/17",
  "created_at": "2026-08-10T20:34:08Z",
  "updated_at": "2026-08-10T20:34:08Z",
  "dismissal_request": null,
  "assignees": [],
  "dismissed_at": null,
  "dismissed_by": null,
  "dismissed_reason": null,
  "dismissed_comment": null,
  "fixed_at": null,
  "auto_dismissed_at": null
}
```

All the D-16-relevant fields are present and confirmed: `dependency.manifest_path`, `security_vulnerability.vulnerable_version_range`, `security_vulnerability.first_patched_version`, `created_at`, `html_url`, `state`.

**No branch/ref field exists in this payload — confirmed by exhaustive inspection of the full JSON above.** Unlike GitHub's Code Scanning alert API (which has per-`ref` `.instances[]`), the Dependabot Alerts API has no branch dimension at all — it operates purely against the default branch's cached dependency graph. GitHub's own docs corroborate ("Dependabot scans your repository's default branch"). **SC1's "branch attribution" cannot be satisfied by extracting a field that does not exist.** The planner should scope this as: confirm (and record) that the alert is inherently default-branch-scoped (no per-branch alerting exists in this API), rather than writing a task that expects a `ref`/`branch` JSON key.

**(b) `GET /repos/theunschut/quest-board-dnd/dependency-graph/sbom`** — confirmed returns 438 packages, 698 relationships. Filtered extraction isolating the target package and Identity chain:

```bash
gh api repos/theunschut/quest-board-dnd/dependency-graph/sbom --jq \
  '.sbom.packages[] | select((.name | contains("Cryptography.Xml")) or (.name | contains("AspNetCore.Identity"))) | "\(.name) \(.versionInfo)"'
```

Actual output:
```
Microsoft.AspNetCore.Identity.UI 8.0.11
System.Security.Cryptography.Xml 8.0.3
Microsoft.AspNetCore.Identity 2.3.1
Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0.11
Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.9
Microsoft.AspNetCore.Identity.UI 10.0.9
```

This is the exact 4-row ghost/live split D-01's table describes, machine-confirmed. **`System.Security.Cryptography.Xml` appears exactly once, only at `8.0.3` — no `10.0.x` entry exists anywhere in the live graph.**

DEPENDS_ON relationship chain (Python used here for readability of the join; a jq-only equivalent is noted below):

```python
import json
data = json.load(open("sbom.json"))  # from: gh api .../dependency-graph/sbom > sbom.json
sbom = data['sbom']
pkgs = {p['SPDXID']: p for p in sbom['packages']}
target = 'SPDXRef-nuget-System.Security.Cryptography.Xml-8.0.3-84ce5b'
for r in sbom['relationships']:
    if r.get('relatedSpdxElement') == target:
        src = pkgs.get(r['spdxElementId'], {})
        print(r['spdxElementId'], '->', r['relationshipType'], '| name:', src.get('name'), src.get('versionInfo'))
```

Actual output:
```
SPDXRef-nuget-Microsoft.AspNetCore.Identity-2.3.1-58ae84 -> DEPENDS_ON | name: Microsoft.AspNetCore.Identity 2.3.1
SPDXRef-github-theunschut-quest-board-dnd-main-d23e00 -> DEPENDS_ON | name: com.github.theunschut/quest-board-dnd main
```

This proves the exact chain D-01 asserts: `Microsoft.AspNetCore.Identity 2.3.1` (ghost) `DEPENDS_ON` `System.Security.Cryptography.Xml 8.0.3` (ghost). The second row (root repo package directly `DEPENDS_ON` the Xml package too) is expected — GitHub's SBOM flattens the whole graph under one repo-level root node; it does **not** carry per-manifest attribution inside the SBOM itself. Manifest attribution comes only from the Dependabot alert's own `manifest_path` field (part (a) above) and the `dependencyGraphManifests` GraphQL query (part (c) below) — cross-reference those two, not the SBOM, for the "which manifest" question.

jq-only equivalent of the same relationship filter (no python dependency):
```bash
gh api repos/theunschut/quest-board-dnd/dependency-graph/sbom --jq \
  '.sbom.relationships[] | select(.relatedSpdxElement == "SPDXRef-nuget-System.Security.Cryptography.Xml-8.0.3-84ce5b" or .spdxElementId == "SPDXRef-nuget-Microsoft.AspNetCore.Identity-2.3.1-58ae84")'
```
(SPDXIDs are stable across runs against the same graph state — confirmed identical between two separate fetches in this session. Re-derive the target SPDXID fresh each run via the package-name filter above rather than hardcoding it, in case GitHub regenerates the hash suffix.)

**(c) GraphQL `repository.dependencyGraphManifests`** — confirmed working, returns **13** manifests right now:

```bash
gh api graphql -H "Accept: application/vnd.github.hawkgirl-preview+json" -f query='
query {
  repository(owner: "theunschut", name: "quest-board-dnd") {
    dependencyGraphManifests(first: 50) {
      totalCount
      nodes { filename blobPath exceedsMaxSize parseable dependenciesCount }
    }
  }
}'
```

Actual `totalCount`: **13**. `filename` list observed (5 ghost `EuphoriaInn.*` + 5 real `QuestBoard.*` + 3 GitHub Actions workflow YAMLs = 8 non-ghost):

Ghost (still present today, matching D-03's claim exactly):
- `EuphoriaInn.UnitTests/EuphoriaInn.UnitTests.csproj`
- `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`
- `EuphoriaInn.IntegrationTests/EuphoriaInn.IntegrationTests.csproj`
- `EuphoriaInn.Repository/EuphoriaInn.Repository.csproj`
- `EuphoriaInn.Service/EuphoriaInn.Service.csproj`

Real:
- `.github/workflows/binary-release.yml`, `.github/workflows/docker-publish.yml`, `.github/workflows/dotnet.yml`
- `QuestBoard.Domain/QuestBoard.Domain.csproj`, `QuestBoard.Service/QuestBoard.Service.csproj`, `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.Repository/QuestBoard.Repository.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj`

**CONFIRMED right now (this session, not "as of 2026-08-26" from CONTEXT.md's earlier check): all 5 ghost `EuphoriaInn.*` manifests are still present**, ~8 weeks after their deletion commit (`a477ab9`) and after the additional `main` HEAD advance to `89a8cb6` (Phase 72's merge) that happened after CONTEXT.md was gathered — confirming D-06's premise still holds (no manifest-touching commit has landed on `main` yet; verified via `git diff --stat 69f2ea0 89a8cb6 -- '*.csproj'` returning empty).

Minor observed quirk worth flagging (not blocking): `dependenciesCount` returns `0` for every node including the real, populated `QuestBoard.*.csproj` manifests — this is a known preview-API artifact of the `hawkgirl-preview` GraphQL fields, not a signal that those manifests are actually empty (the SBOM query above proves they are not). Do not use `dependenciesCount` as evidence of anything in the durable record.

**(d) Exact `PATCH` shape (NOT executed — documented only):**

```bash
# Safer than -f for text containing apostrophes/newlines on Windows/PowerShell:
# write the body to a JSON file first, then reference it with --input.
cat > dismiss-17.json <<'JSON'
{
  "state": "dismissed",
  "dismissed_reason": "inaccurate",
  "dismissed_comment": "Verified 2026-08-26: GHSA-g8r8-53c2-pm3f/CVE-2026-47304, range 8.0.0-8.0.3. Pkg absent from all QuestBoard.* manifests (dotnet + GitHub SBOM cross-checked). Ghost manifest EuphoriaInn.Domain.csproj (deleted a477ab9, 2026-06-29). Full evidence: .planning/SECURITY-TRIAGE.md#entry-1"
}
JSON
gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 -X PATCH --input dismiss-17.json
```

**Windows/PowerShell quoting pitfalls flagged:**
- `-f dismissed_comment="It's ..."` inline (a single-quoted or double-quoted `-f`/`--field` argument containing an apostrophe) is the most likely breakage point in PowerShell — PowerShell's own quote parsing can interact badly with `gh`'s argument parsing before it ever reaches curl/HTTP. **Avoid apostrophes in the comment text entirely** (write "GitHub's" as "GitHub" + possessive-free phrasing, e.g. "GitHub cache" or "the cached graph") — cheaper than fighting quoting.
- Prefer `--input <file>.json` (as above) over inline `-f`/`--field` for any comment with punctuation, since the JSON file sidesteps shell quoting entirely — only the file's own JSON-string-escaping rules apply (escape any literal `"` inside the comment as `\"`).
- Multi-line comments are unnecessary here anyway (280-char single-paragraph budget), so this mostly matters for apostrophes and double quotes, not literal newlines.
- After each real PATCH, `GET` the alert back and diff `dismissed_comment` against the draft to catch any silent truncation/mis-escaping — cheap, zero-risk, and closes the loop D-11 opens.

### Research Question 4 — Current alert state [ANSWERED, LIVE]

```bash
gh api repos/theunschut/quest-board-dnd/dependabot/alerts -X GET -f state=open --paginate
```

Actual result: **5 open alerts total, all severity `high`, all package `System.Security.Cryptography.Xml`** — #17, #18, #19, #20, #21. **No sixth HIGH alert has appeared.** D-19's anticipated scenario has not (yet) occurred; SC5's literal reading and D-19's fallback reading currently coincide.

```bash
gh api repos/theunschut/quest-board/dependabot/alerts -X GET -f state=open -f severity=high --jq 'length'
# -> 5   (confirms the old repo-name redirect used by SC5's literal command still works today)
```

### Research Question 5 — D-16 gate part (b) mechanics [ANSWERED]

**Recommendation: run BOTH a local ground-truth check and a GitHub-sourced corroboration check, not GitHub's SBOM alone** (see Pitfall 2 above for why). Both were executed for all 5 `QuestBoard.*` manifests in this session with concordant, clean results.

**Local ground-truth (run this per-alert or once for all 5 — the check is identical across all 5 alerts since they share one package/version footprint):**

```bash
cd "C:/Repos/quest-board"
for p in QuestBoard.Domain/QuestBoard.Domain.csproj QuestBoard.Repository/QuestBoard.Repository.csproj \
         QuestBoard.Service/QuestBoard.Service.csproj QuestBoard.UnitTests/QuestBoard.UnitTests.csproj \
         QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj; do
  echo "=== $p ==="
  dotnet list "$p" package --include-transitive 2>&1 | grep -i "Cryptography.Xml\|Identity" || echo "  (no match)"
done
```

Actual output (session, fresh restore, `.NET 10.0.400` SDK):
- `QuestBoard.Domain` — only `Microsoft.IdentityModel.*` (unrelated JWT/token-validation packages, not `Microsoft.AspNetCore.Identity`). No `Cryptography.Xml` match.
- `QuestBoard.Repository` — `Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.9` plus its own dependents (`Microsoft.Extensions.Identity.Core/Stores 10.0.9`, `Microsoft.Identity.Client*`, `Microsoft.IdentityModel.*`). No `Cryptography.Xml` match, no base `Microsoft.AspNetCore.Identity` (2.x) match.
- `QuestBoard.Service` — `Microsoft.AspNetCore.Identity.UI 10.0.9` plus the same dependent family. No `Cryptography.Xml` match, no base `Microsoft.AspNetCore.Identity` (2.x) match.
- `QuestBoard.UnitTests` / `QuestBoard.IntegrationTests` — same dependent family (test projects reference the app projects). No `Cryptography.Xml` match, no base `Microsoft.AspNetCore.Identity` (2.x) match.

**Zero occurrences of `System.Security.Cryptography.Xml`, at any version, anywhere in the live, freshly-restored transitive graph of all 5 tracked manifests.** Zero occurrences of the base `Microsoft.AspNetCore.Identity` package (the 2.3.x line the ghost depends on) either — only its unrelated `.EntityFrameworkCore`/`.UI` siblings at `10.0.9`, and the unrelated `Microsoft.IdentityModel.*`/`Microsoft.Extensions.Identity.*` families (JWT/OIDC token handling, not the vulnerable package's dependency chain).

**GitHub-sourced corroboration:** the SBOM filter in Research Question 3(b) above already shows this — `System.Security.Cryptography.Xml` has exactly one entry in the entire 438-package graph, at `8.0.3`, with its only incoming edge from the ghost `Microsoft.AspNetCore.Identity 2.3.1` node.

**Both sources agree: gate part (b) passes for all five alerts.** Recommend the planner make this a single reusable script (not 5 separate inline command blocks) since the check is identical across all 5 alerts — only the per-alert `manifest_path`/CVE/range values (part (a) and the comment draft) actually vary per alert.

### Research Question 6 — `git log -S` archaeology [ANSWERED]

**Version bump commit, 2026-04-22:**

```bash
git log -S "System.Security.Cryptography.Xml" --oneline --all -- '*.csproj'
```
Output: `978d3f6 net10 update and package upgrades` (plus one unrelated earlier match, `691911f Bump the nuget group with 1 update`, from before the string was introduced — not relevant to this archaeology).

```bash
git show 978d3f6 -- EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
```
Actual diff (filtered to the relevant `<ItemGroup>`):
```diff
-    <PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.3.1" />
+    <PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.3.9" />
-  <ItemGroup>
-    <PackageReference Include="System.Security.Cryptography.Xml" Version="8.0.3" />
-  </ItemGroup>
+    <PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.7" />
```
Confirms: `978d3f6` (Wed Apr 22 2026, author Theun Schut) is exactly where `8.0.3` was abandoned — bumped straight to `10.0.7` in the same commit, never passing through any version inside the alert's `8.0.0–8.0.3` range again.

**Manifest deletion commit, 2026-06-29:**

```bash
git show a477ab9~1:EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
```
Actual last-known content before deletion:
```xml
<PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.9" />
```

```bash
git show a477ab9 --stat | grep -i EuphoriaInn
```
Confirms `a477ab9` ("refactor: rename EuphoriaInn -> QuestBoard") deletes `EuphoriaInn.Domain/*` (and siblings) entirely — `EuphoriaInn.Domain.csproj` is not in the post-commit tree.

**Important correction to D-02's framing (see Summary):** the manifest's last live value (`10.0.9`) was itself inside the `>= 10.0.0, <= 10.0.9` vulnerable range documented in the same advisory family (confirmed via `.security_advisory.vulnerabilities[]` on all 5 alerts — every one lists three affected ranges: `8.0.0-8.0.3`→`8.0.4`, `9.0.0-9.0.17`→`9.0.18`, `10.0.0-10.0.9`→`10.0.10`). The actual end of exposure was the 2026-06-29 deletion, not the 2026-04-22 bump. The 2026-04-22 date remains the correct answer to "when did the specific `8.0.0-8.0.3` range GitHub is alerting on stop applying," which is what D-07's `inaccurate` dismissal reason is about (GitHub is attributing to a range that has not applied since that date) — just don't conflate it with "when did the vulnerability class stop applying" in the durable record's prose.

## Runtime State Inventory

Not applicable — this is not a rename/refactor/migration phase. No stored data, live service config, OS-registered state, secrets, or build artifacts change as a result of this phase. (The `EuphoriaInn.*` `bin`/`obj` leftovers are pre-existing, gitignored, and explicitly out of scope per CONTEXT.md's Deferred Ideas — not touched by this phase.)

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `dismissed_comment` is hard-capped at exactly 280 characters | Research Question 1 / Code Examples | Low — the recommendation already builds in a defensive margin (≤260) and the D-11 per-alert independence (D-17) means a single 422 on one alert doesn't block the other four; worst case is a retry with a shorter comment |
| A2 | `X-Accepted-Oauth-Scopes` is identical for `GET` and `PATCH` on the same alert resource (not separately verified against `PATCH` since that call is forbidden this session) | Research Question 2 | Low — GitHub's docs state the scope requirement applies to "this endpoint" (Update a Dependabot alert) directly with the same `security_events`/`public_repo` language used for the read-side docs; recommend the executor re-run the header check immediately before D-11's approval gate as a final, zero-risk confirmation |

**All other claims in this document were executed live in this session (`[VERIFIED]`) or sourced from GitHub's official documentation/changelog (`[CITED]`)** — no other claims rest on unverified training knowledge.

## Open Questions

1. **Will `main` advance further (with a manifest-touching commit) before this phase executes?**
   - What we know: `main` moved from `69f2ea0` to `89a8cb6` between CONTEXT.md's gathering and this research session, with zero `.csproj` changes in that range.
   - What's unclear: whether it will move again, and whether a future move touches a manifest (which would partially satisfy D-06's deferred ghost-manifest fix ahead of schedule).
   - Recommendation: re-run `git diff --stat <old-HEAD> <new-HEAD> -- '*.csproj'` and the GraphQL `dependencyGraphManifests` query immediately before executing the D-16 gate, not just trust this research session's snapshot.

2. **Does the `public_repo`/`repo` scope requirement documented for "Update a Dependabot alert" apply identically to the org-owned vs. personal-account token distinction?**
   - What we know: `cryptic96`'s token has `repo` (broader than the documented minimum `public_repo`), confirmed sufficient by direct header inspection on the read endpoint.
   - What's unclear: GitHub's docs don't distinguish token-owner-type nuances for this specific endpoint beyond the public/private repo split, and no PATCH was attempted to fully confirm.
   - Recommendation: the executor's pre-PATCH header re-check (Research Question 2) is the practical resolution — if it ever shows `repo` missing from `X-Oauth-Scopes` at execution time, stop before the first PATCH and re-auth.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `gh` CLI | All GitHub API interaction (GET/GraphQL/PATCH) | ✓ | 2.89.0 | — |
| `gh` auth (`cryptic96`, active) | D-10's single-actor requirement | ✓ | scopes: `admin:org`, `gist`, `repo` | — |
| `.NET SDK` | D-16 gate's local ground-truth check | ✓ | 10.0.400 | — |
| `git` | D-15's `git log -S` archaeology appendix | ✓ | (repo already a working clone) | — |
| `python3` | Optional — JSON relationship-graph filtering for the SBOM appendix (jq equivalent provided) | ✓ (used in this session) | — | jq-only equivalent given in Research Question 3(b); no hard dependency on python |

No missing dependencies, no blockers.

## Validation Architecture

This phase produces no code and no automated test suite — "validation" here means re-runnable, observable **state assertions** against GitHub and the local repo, not unit/integration tests. Nyquist validation is honored by making every success criterion below a command whose output is checked, not assumed.

### Phase Requirements → Validation Map

| Req ID | Behavior | Validation Type | Re-runnable Command | Expected Result |
|--------|----------|-----------------|----------------------|-----------------|
| SECALERT-01 | Each of #17–#21 opened individually, manifest/timestamp recorded, branch-scope acknowledged | state assertion (read) | `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/{17,18,19,20,21}` (5 calls) | Each returns `manifest_path` == an `EuphoriaInn.*` path, `created_at` == `2026-08-10T20:34:0{8,8,9,9,9}Z`, and the durable record cites all 5 |
| SECALERT-02 | GitHub server-side re-check (SBOM + GraphQL), no force-refresh | state assertion (read) | SBOM filter command + GraphQL `dependencyGraphManifests` query (both given above) | SBOM shows exactly 1 `System.Security.Cryptography.Xml` entry at `8.0.3`; GraphQL shows the 5 ghost `EuphoriaInn.*` manifests still present |
| SECALERT-03 | Each alert dismissed individually, `dismissed_reason=inaccurate`, evidence-citing comment, single operator approval | state assertion (read, post-PATCH) | `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/{n}` per alert | `state == "dismissed"`, `dismissed_by.login == "cryptic96"`, `dismissed_reason == "inaccurate"`, `dismissed_comment` matches the approved draft (diff check) |
| SECALERT-04 | Investigation and outcome recorded in `.planning/PROJECT.md` | state assertion (file content) | `grep -n "SECURITY-TRIAGE" .planning/PROJECT.md` | 2 matches — one in the Known issues bullet list, one in the Key Decisions table row |
| SECALERT-05 | Zero open HIGH alerts once phase closes | state assertion (read) | `gh api repos/theunschut/quest-board/dependabot/alerts --jq '[.[] | select(.state=="open" and (.security_vulnerability.severity=="high" or .severity=="high"))] | length'` | `0` (or, per D-19, any remaining open HIGH is a newly-appeared alert not #17–#21, logged as a new `.planning/SECURITY-TRIAGE.md` entry rather than blocking) |

### Sampling Rate

- **Per alert action:** re-run the D-16 gate (local `dotnet list package` + GitHub SBOM cross-check) immediately before that alert's PATCH — not once for all five up front, since D-17/D-18 require per-alert independence.
- **Phase gate:** the SECALERT-05 command above, run once after all approved PATCHes complete.

### Wave 0 Gaps

None — every validation command above is executable today with tools already installed (`gh`, `dotnet`, `git`). No test framework or fixture setup is needed since this phase produces no code.

## Security Domain

This phase's entire subject matter *is* security-alert triage, but it does not touch application code, authentication, session management, or any ASVS-style code control — it is GitHub-API-and-documentation work. The standard ASVS categories mostly do not apply; the one real security-relevant control in this phase is token/credential scope handling, covered fully in Research Question 2 above.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | No application authentication surface touched |
| V3 Session Management | no | No application session surface touched |
| V4 Access Control | no | No application authorization surface touched |
| V5 Input Validation | no | No user input processed; this phase's own inputs are dismissal comment drafts under operator review (D-11), not untrusted external input |
| V6 Cryptography | no | The vulnerable package itself is a cryptography library, but this phase does not use, configure, or patch it — it verifies the package is absent from the live app entirely |

### Known Threat Patterns for this phase

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Rubber-stamp dismissal of a still-reachable real vulnerability | Repudiation (false audit claim) | D-16's two-source gate (local + GitHub-sourced) before every dismissal; D-17/D-18 stop-and-escalate on any gate failure rather than proceeding |
| Silent audit-trail loss (e.g. via the vulnerability-alerts toggle) | Repudiation / Tampering | D-05 permanently vetoes the toggle; the three pre-existing dismissals (#7/#8/#11) remain untouched by this phase |
| Over-broad token scope used for a narrow write action | Elevation of privilege (defense-in-depth concern, not an active exploit here) | Research Question 2's pre-flight confirms the minimum documented scope (`public_repo`) is satisfied by the actually-used token (`repo`), not relying on an overprivileged `admin:org` scope for this action |

## Sources

### Primary (HIGH confidence — executed live in this session)

- `gh api repos/theunschut/quest-board-dnd/dependabot/alerts[/{n}]` — all 5 open alerts + individual detail for #17, plus #7/#8/#11 historical dismissals
- `gh api repos/theunschut/quest-board-dnd/dependency-graph/sbom` — 438-package SBOM, filtered for Identity/Xml chain
- `gh api graphql` with `dependencyGraphManifests` — live 13-manifest count, ghost list confirmed
- `git show`/`git log -S` against `978d3f6`, `a477ab9`, `69f2ea0`, `89a8cb6` in the local `quest-board` clone
- `dotnet list package --include-transitive` against all 5 `QuestBoard.*.csproj` (fresh restore, SDK 10.0.400)
- `gh auth status` — confirmed active account (`cryptic96`) and scopes
- `gh api ... -i` header inspection — `X-OAuth-Scopes` / `X-Accepted-Oauth-Scopes` on the alerts list endpoint

### Secondary (MEDIUM confidence — official docs, not independently re-derivable this session)

- [docs.github.com/en/rest/dependabot/alerts — "Update a Dependabot alert"](https://docs.github.com/en/rest/dependabot/alerts?apiVersion=2022-11-28#update-a-dependabot-alert) — scope requirement (`security_events`/`public_repo`), `dismissed_reason` enum values
- [github.blog/changelog/2022-08-22-dependabot-alerts-optional-dismissal-comment-2](https://github.blog/changelog/2022-08-22-dependabot-alerts-optional-dismissal-comment-2/) — 280-character `dismissed_comment` limit (not restated in the current live reference page)

### Tertiary (LOW confidence)

None — every claim in this document is either primary (executed) or secondary (official GitHub source).

## Metadata

**Confidence breakdown:**
- Alert/manifest/SBOM/GraphQL state: HIGH — every figure was fetched live in this session against the real repo
- `dismissed_comment` 280-char cap: MEDIUM — official GitHub source, but not restated in the current live API reference and not independently re-verifiable without a forbidden mutating call
- Token scope sufficiency: HIGH for the read-side confirmation; MEDIUM for the untested PATCH-side carryover (recommend re-check immediately before the D-11 gate)
- Git archaeology (`978d3f6`, `a477ab9`): HIGH — exact commit diffs captured directly

**Research date:** 2026-08-26
**Valid until:** This research is a point-in-time snapshot of live GitHub state (alert list, manifest count, `main` HEAD). Re-verify Research Questions 3(c) and 4 (GraphQL manifest count, open-alert list) immediately before executing the D-16 gate and before the D-11 approval — do not treat this document's captured JSON as still-current at execution time without a fresh re-run. The API mechanics (scope requirements, comment cap, PATCH shape) are stable and do not need re-verification within the life of this milestone.
