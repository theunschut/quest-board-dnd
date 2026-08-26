# Phase 73 Plan 02 — Dismissal Drafts (Operator Approval Material)

**Purpose:** This file is the single artifact the operator reviews to approve (or hold) the five
dismissals of Dependabot alerts #17–#21. Per D-11, all five comments and all five verification
lines are presented together, in one review, before any PATCH fires. Nothing in this file has
been posted to GitHub.

---

## Pre-flight assertions (Task 1, re-run live 2026-08-26)

| Check | Result |
|---|---|
| Active `gh` account (D-10) | `gh auth status` → active account **`cryptic96`** (also logged in: `theunschut`, inactive) |
| Token scopes on active account | `admin:org`, `gist`, `repo` |
| `X-Oauth-Scopes` (from live `GET .../dependabot/alerts -i` re-check, this session) | `admin:org, gist, repo` |
| `X-Accepted-Oauth-Scopes` (same live `GET`, this session) | `admin:repo_hook, delete_repo, read:repo_hook, repo, repo:invite, repo:status, repo_deployment, security_events, write:repo_hook` |
| Scope verdict | `repo` appears in both lists — **confirmed sufficient**. Read from the `GET` endpoint only; the `PATCH`-side carryover is untested because testing it is the mutation itself, and `X-Accepted-Oauth-Scopes` is documented as a per-resource (not per-verb) header in GitHub's REST implementation, so it is expected to carry over unchanged. |

## Freshness re-check (Task 1 step 3, live 2026-08-26 — same day as plan 73-01's evidence capture)

- **Open alert set:** `gh api .../dependabot/alerts -f state=open --jq '...number'` → `21, 20, 19, 18, 17`. **No delta** from `73-EVIDENCE-CAPTURE.md` / `SECURITY-TRIAGE.md` — exactly the five alerts this plan is scoped to.
- **GraphQL `dependencyGraphManifests`:** `totalCount: 13`. All five ghost `EuphoriaInn.*` manifests (`Domain`, `IntegrationTests`, `Repository`, `Service`, `UnitTests`) still present. **No delta** — none of the ghost manifests have cleared since plan 73-01.
- **Per-alert live re-read (GHSA, CVE, range, `manifest_path`, `state`):** re-fetched all five alerts individually this session. Every field matches `SECURITY-TRIAGE.md`'s per-alert table exactly — no drift in any GHSA, CVE, range, or manifest path.
- **Full advisory range set (spot-checked live on #17 and #21, identical across the family):**

  | Affected range | Patched at |
  |---|---|
  | `>= 8.0.0, <= 8.0.3` | `8.0.4` |
  | `>= 9.0.0, <= 9.0.17` | `9.0.18` |
  | `>= 10.0.0, <= 10.0.9` | `10.0.10` |

**Conclusion: zero material delta since plan 73-01's evidence capture.** Nothing new to flag.

## What approval authorizes

**Approving this material posts five irreversible dismissals on a public GitHub repository
(`theunschut/quest-board-dnd`). `dismissed_reason` is permanent audit metadata attached to each
alert forever.** After approval, each alert's two-part gate (manifest attribution + two-source
package absence) is re-run live immediately before that alert's own `PATCH` call (D-16) — an
alert whose evidence has changed since this file was written will stop rather than post, and a
held or failed alert does not block the other four (D-17).

---

## Alert #17

**html_url:** https://github.com/theunschut/quest-board-dnd/security/dependabot/17

**Comment (248 characters):**
```
Checked 2026-08-26: GHSA-g8r8-53c2-pm3f / CVE-2026-47304, range >=8.0.0 <=8.0.3, manifest EuphoriaInn.Domain.csproj deleted 2026-06-29 in a477ab9. Package absent from all 5 QuestBoard manifests; dotnet + SBOM agree. See .planning/SECURITY-TRIAGE.md
```

**Verification line:** `manifest_path` live-read as `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` — still one of the five ghost manifests in the GraphQL list re-checked above. Two-source absence result (from plan 73-01, re-affirmed): GitHub's own SBOM shows `System.Security.Cryptography.Xml` exactly once, at `8.0.3`, reachable only via the ghost `Microsoft.AspNetCore.Identity 2.3.1` node; a fresh local `dotnet list package --include-transitive` sweep across all five `QuestBoard.*` manifests shows zero occurrences of the package at any version. `dismissed_reason`: `inaccurate`, not `not_used`. Posting identity: `cryptic96`.

**GHSA / CVE:** `GHSA-g8r8-53c2-pm3f` / `CVE-2026-47304` — CVSS 8.1, Security Feature Bypass.

---

## Alert #18

**html_url:** https://github.com/theunschut/quest-board-dnd/security/dependabot/18

**Comment (248 characters):**
```
Checked 2026-08-26: GHSA-8q5v-6pqq-x66h / CVE-2026-50525, range >=8.0.0 <=8.0.3, manifest EuphoriaInn.Domain.csproj deleted 2026-06-29 in a477ab9. Package absent from all 5 QuestBoard manifests; dotnet + SBOM agree. See .planning/SECURITY-TRIAGE.md
```

**Verification line:** `manifest_path` live-read as `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` — still one of the five ghost manifests in the GraphQL list re-checked above. Two-source absence result (from plan 73-01, re-affirmed): GitHub's own SBOM shows `System.Security.Cryptography.Xml` exactly once, at `8.0.3`, reachable only via the ghost `Microsoft.AspNetCore.Identity 2.3.1` node; a fresh local `dotnet list package --include-transitive` sweep across all five `QuestBoard.*` manifests shows zero occurrences of the package at any version. `dismissed_reason`: `inaccurate`, not `not_used`. Posting identity: `cryptic96`.

**GHSA / CVE:** `GHSA-8q5v-6pqq-x66h` / `CVE-2026-50525` — CVSS 7.5, Denial of Service.

---

## Alert #19

**html_url:** https://github.com/theunschut/quest-board-dnd/security/dependabot/19

**Comment (248 characters):**
```
Checked 2026-08-26: GHSA-cvvh-rhrc-wg4q / CVE-2026-47302, range >=8.0.0 <=8.0.3, manifest EuphoriaInn.Domain.csproj deleted 2026-06-29 in a477ab9. Package absent from all 5 QuestBoard manifests; dotnet + SBOM agree. See .planning/SECURITY-TRIAGE.md
```

**Verification line:** `manifest_path` live-read as `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` — still one of the five ghost manifests in the GraphQL list re-checked above. Two-source absence result (from plan 73-01, re-affirmed): GitHub's own SBOM shows `System.Security.Cryptography.Xml` exactly once, at `8.0.3`, reachable only via the ghost `Microsoft.AspNetCore.Identity 2.3.1` node; a fresh local `dotnet list package --include-transitive` sweep across all five `QuestBoard.*` manifests shows zero occurrences of the package at any version. `dismissed_reason`: `inaccurate`, not `not_used`. Posting identity: `cryptic96`.

**GHSA / CVE:** `GHSA-cvvh-rhrc-wg4q` / `CVE-2026-47302` — CVSS 7.5, Denial of Service.

---

## Alert #20

**html_url:** https://github.com/theunschut/quest-board-dnd/security/dependabot/20

**Comment (248 characters):**
```
Checked 2026-08-26: GHSA-23rf-6693-g89p / CVE-2026-50648, range >=8.0.0 <=8.0.3, manifest EuphoriaInn.Domain.csproj deleted 2026-06-29 in a477ab9. Package absent from all 5 QuestBoard manifests; dotnet + SBOM agree. See .planning/SECURITY-TRIAGE.md
```

**Verification line:** `manifest_path` live-read as `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` — still one of the five ghost manifests in the GraphQL list re-checked above. Two-source absence result (from plan 73-01, re-affirmed): GitHub's own SBOM shows `System.Security.Cryptography.Xml` exactly once, at `8.0.3`, reachable only via the ghost `Microsoft.AspNetCore.Identity 2.3.1` node; a fresh local `dotnet list package --include-transitive` sweep across all five `QuestBoard.*` manifests shows zero occurrences of the package at any version. `dismissed_reason`: `inaccurate`, not `not_used`. Posting identity: `cryptic96`.

**GHSA / CVE:** `GHSA-23rf-6693-g89p` / `CVE-2026-50648` — CVSS 7.5, Denial of Service.

---

## Alert #21

**html_url:** https://github.com/theunschut/quest-board-dnd/security/dependabot/21

**Comment (248 characters):**
```
Checked 2026-08-26: GHSA-mmjf-rqrv-855v / CVE-2026-50527, range >=8.0.0 <=8.0.3, manifest EuphoriaInn.Domain.csproj deleted 2026-06-29 in a477ab9. Package absent from all 5 QuestBoard manifests; dotnet + SBOM agree. See .planning/SECURITY-TRIAGE.md
```

**Verification line:** `manifest_path` live-read as `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` — still one of the five ghost manifests in the GraphQL list re-checked above. Two-source absence result (from plan 73-01, re-affirmed): GitHub's own SBOM shows `System.Security.Cryptography.Xml` exactly once, at `8.0.3`, reachable only via the ghost `Microsoft.AspNetCore.Identity 2.3.1` node; a fresh local `dotnet list package --include-transitive` sweep across all five `QuestBoard.*` manifests shows zero occurrences of the package at any version. `dismissed_reason`: `inaccurate`, not `not_used`. Posting identity: `cryptic96`.

**GHSA / CVE:** `GHSA-mmjf-rqrv-855v` / `CVE-2026-50527` — CVSS 7.5, Denial of Service.

---

## Common facts across all five (shared evidence, individual verification)

All five alerts share one package (`System.Security.Cryptography.Xml`), one manifest
(`EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`), one alerted range (`>= 8.0.0, <= 8.0.3`,
patched at `8.0.4`), and one advisory family (three ranges total, see table above) — because the
evidence is about the manifest, not the CVE. What is individual to each alert, and independently
re-verified live in this task, is: its own GHSA id, its own CVE id, its own `manifest_path`
re-read fresh, and its own confirmed `open` state immediately before this draft was written.

**Root cause (from `.planning/SECURITY-TRIAGE.md` entry one):** `978d3f6` (2026-04-22) bumped
the package's version out of the alerted `8.0.0–8.0.3` range; `a477ab9` (2026-06-29) deleted the
manifest entirely. GitHub's `dependencyGraphManifests` query still lists the deleted manifest
today because the reliable eviction lever (`DELETE`/`PUT
/repos/{owner}/{repo}/vulnerability-alerts`) is permanently vetoed for this repo (D-05) — it would
put the audit trail on the three pre-existing dismissals (#7, #8, #11) at risk. Full narrative,
per-alert table, and re-runnable appendix live in `.planning/SECURITY-TRIAGE.md`.

---

## Approval outcome

**Approved: 2026-08-26.** The operator was shown all five comments, their GHSA/CVE pairs,
severities, the exact `dismissed_reason=inaccurate`, and the verbatim comment text, and was told
explicitly that this is irreversible audit metadata on a public repo. Decision recorded by the
orchestrator that relayed this plan's continuation: **approve all five, exactly as drafted
(#17, #18, #19, #20, #21), no wording changes**, posting identity `cryptic96` (the currently
active `gh` account — no `gh auth switch` to be run). No alert held. This satisfies D-11: one
review, one explicit decision, covering all five comments and verification lines together.

All five alerts confirmed still `open` at the moment this approval was received and acted on
(re-checked live immediately before Task 3 began):
`gh api repos/theunschut/quest-board-dnd/dependabot/alerts -X GET -f state=open --jq '[.[] | select(.number | IN(17,18,19,20,21))] | length'` → `5`.

## Task 3 outcome — BLOCKED before any PATCH could be sent

**2026-08-26, live gate re-run for alert #17 (first in processing order):**

- **Gate (a) — manifest attribution:** `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 --jq '{state, manifest_path: .dependency.manifest_path, range: .security_vulnerability.vulnerable_version_range}'` → `state: open`, `manifest_path: EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` (one of the five ghost manifests), `range: >= 8.0.0, <= 8.0.3` (matches the approved comment). **PASS.**
- **Gate (b) — two-source absence:** GitHub SBOM re-read live: `System.Security.Cryptography.Xml` present exactly once, at `8.0.3` (unchanged from plan 73-01). Local `dotnet list package --include-transitive` re-run live across all five `QuestBoard.*` manifests (`Domain`, `Repository`, `Service`, `UnitTests`, `IntegrationTests`): zero matches for `Cryptography.Xml` in every manifest. **PASS.**
- **PATCH attempt:** `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 -X PATCH --input .../dismiss-17.json` was **denied by the Claude Code harness's Bash auto-mode permission classifier** ("Blocked by classifier") before it reached the network — this is a runtime/tool-permission boundary, not a GitHub-side rejection, not a 422, and not a gate failure. No HTTP request was sent; no state changed.
- **Post-denial read-back attempt:** an immediate read-only `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 --jq '.state'`, intended only to reconfirm alert #17's unmutated state, was **also denied by the same classifier**. Per the tool's own guidance ("you should not attempt to work around this denial... if you believe this capability is essential, STOP and explain to the user"), further calls against this endpoint were not attempted, to avoid the appearance of probing around a deliberate safety block.

**Because #17's PATCH never fired, alerts #18–#21 were not attempted** — the four remaining PATCH calls are structurally identical (`gh api .../dependabot/alerts/{n} -X PATCH --input dismiss-{n}.json`) and would be expected to hit the identical classifier denial. Per D-17 this is not a per-alert evidence failure (gate (a) and (b) both passed cleanly for #17) — it is a single categorical tool-permission block that applies uniformly to all five approved PATCH calls, so there was no basis to expect a different outcome by attempting the other four.

**Result: zero PATCH calls succeeded. All five alerts (#17–#21) remain `open`, unmutated.** The last successful live read (gate (a) above) confirmed #17 `open`; no evidence exists that any alert's state changed after that point, since no mutating call reached GitHub.

**What is required to proceed:** the user (not this agent) needs to grant Bash permission for the mutating command `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/{n} -X PATCH --input <file>` (for `n` in 17–21) — for example via a Bash permission rule in Claude Code settings — after which Task 3 can resume exactly where it stopped: re-run each alert's own gate live, PATCH, read back, starting with #17.
