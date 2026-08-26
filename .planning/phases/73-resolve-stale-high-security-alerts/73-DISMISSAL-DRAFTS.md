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
