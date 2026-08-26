# Phase 73 Evidence Capture — Working Document

CHECK_DATE: 2026-08-26
Repo: `theunschut/quest-board-dnd` (all GraphQL/REST calls below use this real name; `theunschut/quest-board` resolves to it by redirect)

This is a working capture file for plan 73-01, Tasks 1 and 2. It is copied into
`.planning/SECURITY-TRIAGE.md` (the durable record) and archived with the phase
directory — it is not itself the durable record and is never linked to from it.

All commands below are reads. No PATCH, no DELETE, no vulnerability-alerts toggle.

---

## Step 1 — Open alert set confirmation

Command:
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts -X GET -f state=open --paginate --jq '.[] | "\(.number) \(.state) \(.security_vulnerability.severity) \(.dependency.package.name) \(.dependency.manifest_path)"'
```

Run: 2026-08-26

Output:
```
21 open high System.Security.Cryptography.Xml EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
20 open high System.Security.Cryptography.Xml EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
19 open high System.Security.Cryptography.Xml EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
18 open high System.Security.Cryptography.Xml EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
17 open high System.Security.Cryptography.Xml EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
```

**Result: the open set is exactly {17, 18, 19, 20, 21}.** No extra alert, no missing alert, none pre-dismissed. Matches RESEARCH.md's snapshot exactly. Nothing to log per D-19.

---

## Alert #17

Command: `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 -X GET`

Run: 2026-08-26. Full JSON captured; key fields:

| Field | Value |
|---|---|
| `number` | 17 |
| `state` | open |
| `dependency.package.ecosystem` | nuget |
| `dependency.package.name` | System.Security.Cryptography.Xml |
| `dependency.manifest_path` | `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` |
| `dependency.scope` | runtime |
| `dependency.relationship` | direct |
| `security_vulnerability.severity` | high |
| `security_vulnerability.vulnerable_version_range` | `>= 8.0.0, <= 8.0.3` |
| `security_vulnerability.first_patched_version.identifier` | 8.0.4 |
| `created_at` | 2026-08-10T20:34:08Z |
| `html_url` | https://github.com/theunschut/quest-board-dnd/security/dependabot/17 |
| `dismissed_at` | null |
| `dismissed_reason` | null |

Full advisory range set (`security_advisory.vulnerabilities[]`, filtered to the target package):
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 --jq '{ghsa: .security_advisory.ghsa_id, cve: .security_advisory.cve_id, cvss: .security_advisory.cvss.score, ranges: [.security_advisory.vulnerabilities[] | select(.package.name=="System.Security.Cryptography.Xml") | {pkg: .package.name, range: .vulnerable_version_range, patched: .first_patched_version.identifier}]}'
```
```json
{"cve":"CVE-2026-47304","cvss":8.1,"ghsa":"GHSA-g8r8-53c2-pm3f","ranges":[
  {"pkg":"System.Security.Cryptography.Xml","range":">= 10.0.0, <= 10.0.9","patched":"10.0.10"},
  {"pkg":"System.Security.Cryptography.Xml","range":">= 9.0.0, <= 9.0.17","patched":"9.0.18"},
  {"pkg":"System.Security.Cryptography.Xml","range":">= 8.0.0, <= 8.0.3","patched":"8.0.4"}
]}
```
GHSA-g8r8-53c2-pm3f / CVE-2026-47304 / CVSS 8.1 / Security Feature Bypass (CWE-345, CWE-347). Matches RESEARCH.md's snapshot exactly — no delta.

---

## Alert #18

Command: `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/18 -X GET`

Run: 2026-08-26.

| Field | Value |
|---|---|
| `number` | 18 |
| `state` | open |
| `dependency.manifest_path` | `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` |
| `dependency.scope` / `relationship` | runtime / direct |
| `security_vulnerability.vulnerable_version_range` | `>= 8.0.0, <= 8.0.3` |
| `security_vulnerability.first_patched_version.identifier` | 8.0.4 |
| `created_at` | 2026-08-10T20:34:08Z |
| `html_url` | https://github.com/theunschut/quest-board-dnd/security/dependabot/18 |
| `dismissed_at` / `dismissed_reason` | null / null |

Full advisory range set:
```json
{"cve":"CVE-2026-50525","cvss":7.5,"ghsa":"GHSA-8q5v-6pqq-x66h","ranges":[
  {"pkg":"System.Security.Cryptography.Xml","range":">= 10.0.0, <= 10.0.9","patched":"10.0.10"},
  {"pkg":"System.Security.Cryptography.Xml","range":">= 9.0.0, <= 9.0.17","patched":"9.0.18"},
  {"pkg":"System.Security.Cryptography.Xml","range":">= 8.0.0, <= 8.0.3","patched":"8.0.4"}
]}
```
GHSA-8q5v-6pqq-x66h / CVE-2026-50525 / CVSS 7.5 / Denial of Service (CWE-770).

---

## Alert #19

Command: `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/19 -X GET`

Run: 2026-08-26.

| Field | Value |
|---|---|
| `number` | 19 |
| `state` | open |
| `dependency.manifest_path` | `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` |
| `dependency.scope` / `relationship` | runtime / direct |
| `security_vulnerability.vulnerable_version_range` | `>= 8.0.0, <= 8.0.3` |
| `security_vulnerability.first_patched_version.identifier` | 8.0.4 |
| `created_at` | 2026-08-10T20:34:09Z |
| `html_url` | https://github.com/theunschut/quest-board-dnd/security/dependabot/19 |
| `dismissed_at` / `dismissed_reason` | null / null |

Full advisory range set (filtered to `System.Security.Cryptography.Xml`; this advisory's full
`vulnerabilities[]` array also lists many `Microsoft.NetCore.App.Runtime.*` platform packages —
irrelevant here since none of them appear in this repo's dependency graph):
```json
{"cve":"CVE-2026-47302","cvss":7.5,"ghsa":"GHSA-cvvh-rhrc-wg4q","ranges":[
  {"pkg":"System.Security.Cryptography.Xml","range":">= 10.0.0, <= 10.0.9","patched":"10.0.10"},
  {"pkg":"System.Security.Cryptography.Xml","range":">= 9.0.0, <= 9.0.17","patched":"9.0.18"},
  {"pkg":"System.Security.Cryptography.Xml","range":">= 8.0.0, <= 8.0.3","patched":"8.0.4"}
]}
```
GHSA-cvvh-rhrc-wg4q / CVE-2026-47302 / CVSS 7.5 / Denial of Service (CWE-770).

---

## Alert #20

Command: `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/20 -X GET`

Run: 2026-08-26.

| Field | Value |
|---|---|
| `number` | 20 |
| `state` | open |
| `dependency.manifest_path` | `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` |
| `dependency.scope` / `relationship` | runtime / direct |
| `security_vulnerability.vulnerable_version_range` | `>= 8.0.0, <= 8.0.3` |
| `security_vulnerability.first_patched_version.identifier` | 8.0.4 |
| `created_at` | 2026-08-10T20:34:09Z |
| `html_url` | https://github.com/theunschut/quest-board-dnd/security/dependabot/20 |
| `dismissed_at` / `dismissed_reason` | null / null |

Full advisory range set:
```json
{"cve":"CVE-2026-50648","cvss":7.5,"ghsa":"GHSA-23rf-6693-g89p","ranges":[
  {"pkg":"System.Security.Cryptography.Xml","range":">= 10.0.0, <= 10.0.9","patched":"10.0.10"},
  {"pkg":"System.Security.Cryptography.Xml","range":">= 9.0.0, <= 9.0.17","patched":"9.0.18"},
  {"pkg":"System.Security.Cryptography.Xml","range":">= 8.0.0, <= 8.0.3","patched":"8.0.4"}
]}
```
GHSA-23rf-6693-g89p / CVE-2026-50648 / CVSS 7.5 / Denial of Service (CWE-770).

---

## Alert #21

Command: `gh api repos/theunschut/quest-board-dnd/dependabot/alerts/21 -X GET`

Run: 2026-08-26.

| Field | Value |
|---|---|
| `number` | 21 |
| `state` | open |
| `dependency.manifest_path` | `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` |
| `dependency.scope` / `relationship` | runtime / direct |
| `security_vulnerability.vulnerable_version_range` | `>= 8.0.0, <= 8.0.3` |
| `security_vulnerability.first_patched_version.identifier` | 8.0.4 |
| `created_at` | 2026-08-10T20:34:09Z |
| `html_url` | https://github.com/theunschut/quest-board-dnd/security/dependabot/21 |
| `dismissed_at` / `dismissed_reason` | null / null |

Full advisory range set:
```json
{"cve":"CVE-2026-50527","cvss":7.5,"ghsa":"GHSA-mmjf-rqrv-855v","ranges":[
  {"pkg":"System.Security.Cryptography.Xml","range":">= 10.0.0, <= 10.0.9","patched":"10.0.10"},
  {"pkg":"System.Security.Cryptography.Xml","range":">= 9.0.0, <= 9.0.17","patched":"9.0.18"},
  {"pkg":"System.Security.Cryptography.Xml","range":">= 8.0.0, <= 8.0.3","patched":"8.0.4"}
]}
```
GHSA-mmjf-rqrv-855v / CVE-2026-50527 / CVSS 7.5 / Denial of Service (CWE-121).

**Cross-alert summary:** five distinct GHSA IDs, five distinct CVE IDs, one shared package
(`System.Security.Cryptography.Xml`), one shared manifest (`EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`),
one shared alerted range (`>= 8.0.0, <= 8.0.3` → patched `8.0.4`), one shared created_at day
(2026-08-10). Every alert independently confirms the same three-range advisory family
(`8.0.0-8.0.3`, `9.0.0-9.0.17`, `10.0.0-10.0.9`). No delta from RESEARCH.md's snapshot for any of the five.

---

## Step 4 — Branch/ref scope confirmation

Literal probe as specified in the plan:
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 --jq '[paths(scalars) | map(tostring) | join(".")] | map(select(test("ref|branch";"i"))) | length'
```
Result: **6** (not the `0` the plan's acceptance criterion names) — a delta from RESEARCH.md's
expectation, investigated below rather than asserted blindly.

Cause, inspected directly:
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 --jq '[paths(scalars) | map(tostring) | join(".")] | map(select(test("ref|branch";"i")))'
```
```json
["security_advisory.references.0.url","security_advisory.references.1.url","security_advisory.references.2.url","security_advisory.references.3.url","security_advisory.references.4.url","security_advisory.references.5.url"]
```
All six matches are `security_advisory.references[N].url` — the advisory's own citation links
(NVD, MSRC, dotnet/runtime issue, etc.). The substring `ref` inside the field name `references`
is what the `i`-flagged regex matches; these are not a branch or ref *dimension* on the alert.
The apparent full JSON payload today includes a `security_advisory` object with a `references[]`
array that RESEARCH.md's abbreviated example capture did not show (RESEARCH.md's RQ3(a) JSON
excerpt omitted `security_advisory` entirely for brevity) — so this is a query-scope artifact of
a richer payload, not a new branch/ref field appearing.

Refined probe, excluding the `references` false positives:
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 --jq '[paths(scalars) | map(tostring) | join(".")] | map(select(test("ref|branch";"i"))) | map(select(test("references")|not)) | length'
```
Result: **0**.

**Recorded finding:** confirmed default-branch-scoped (main); the Dependabot Alerts API exposes
no per-branch dimension, unlike the Code Scanning API's `instances[].ref`. No branch or ref field
was read from the payload as evidence of anything — the six raw hits are advisory citation URLs,
and the refined, references-excluded probe returns exactly 0, matching RESEARCH.md's underlying
claim. This delta between the literal plan-specified command (6) and its intended meaning (0) is
itself recorded here rather than silently resolved, per the freshness/delta-flagging instruction.

Post-check — nothing mutated by Task 1:
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts -X GET -f state=open --jq '[.[] | select(.number | IN(17,18,19,20,21))] | length'
```
Result: **5**. All five alerts still open after Task 1.

---

## Task 2 — Dependency graph re-check

### Step 1 — SBOM package filter (source A)

Command:
```
gh api repos/theunschut/quest-board-dnd/dependency-graph/sbom --jq '.sbom.packages[] | select((.name | contains("Cryptography.Xml")) or (.name | contains("AspNetCore.Identity"))) | "\(.name) \(.versionInfo)"'
```
Run: 2026-08-26.
```
Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0.11
Microsoft.AspNetCore.Identity.UI 8.0.11
Microsoft.AspNetCore.Identity 2.3.1
System.Security.Cryptography.Xml 8.0.3
Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.9
Microsoft.AspNetCore.Identity.UI 10.0.9
```
Exact ghost/live split D-01 describes. `System.Security.Cryptography.Xml` appears exactly once, at `8.0.3`.

Isolated single-value check:
```
gh api repos/theunschut/quest-board-dnd/dependency-graph/sbom --jq '[.sbom.packages[] | select(.name=="System.Security.Cryptography.Xml") | .versionInfo] | join(",")'
```
Result: **`8.0.3`** — one entry, one version, no `10.0.x` entry anywhere in the live graph.

(Note: this same query transiently returned `{"message":"Failed to generate SBOM: Request timed out.","status":"500"}` on its first invocation this session — GitHub's SBOM generation endpoint is itself unreliable/slow, consistent with it being the stale/cached subsystem this phase is scrutinizing. Immediate retry succeeded with the result above. Not treated as a material finding, just an operational note for whoever re-runs this.)

**GitHub's own SBOM is itself the subsystem already proven stale (Pitfall 2) — this is source A only, corroborated by source B (local `dotnet`) below, not trusted alone.**

### Step 2 — SBOM DEPENDS_ON chain

Re-derive SPDXID fresh:
```
gh api repos/theunschut/quest-board-dnd/dependency-graph/sbom --jq '.sbom.packages[] | select(.name=="System.Security.Cryptography.Xml") | .SPDXID'
```
Result: `SPDXRef-nuget-System.Security.Cryptography.Xml-8.0.3-84ce5b` — identical to RESEARCH.md's captured value, confirming SPDXIDs are stable for this graph state.

Incoming edges:
```
gh api repos/theunschut/quest-board-dnd/dependency-graph/sbom --jq '.sbom.relationships[] | select(.relatedSpdxElement == "SPDXRef-nuget-System.Security.Cryptography.Xml-8.0.3-84ce5b") | .spdxElementId'
```
```
SPDXRef-nuget-Microsoft.AspNetCore.Identity-2.3.1-58ae84
SPDXRef-github-theunschut-quest-board-dnd-main-d23e00
```
Two incoming edges: the ghost `Microsoft.AspNetCore.Identity 2.3.1` node and the repo-level root
node. **Caveat:** the SBOM flattens the whole graph under one repo-level root and carries no
per-manifest attribution inside itself — the "which manifest" question is answered by each
alert's `manifest_path` (Task 1) plus the GraphQL query below, never by the SBOM alone.

### Step 3 — GraphQL `dependencyGraphManifests`

Command:
```
gh api graphql -H "Accept: application/vnd.github.hawkgirl-preview+json" -f query='query { repository(owner: "theunschut", name: "quest-board-dnd") { dependencyGraphManifests(first: 50) { totalCount nodes { filename blobPath parseable } } } }'
```
Run: 2026-08-26.

`totalCount`: **13**.

Ghost manifests (still present today, 5 of 13):
```
EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
EuphoriaInn.IntegrationTests/EuphoriaInn.IntegrationTests.csproj
EuphoriaInn.Repository/EuphoriaInn.Repository.csproj
EuphoriaInn.Service/EuphoriaInn.Service.csproj
EuphoriaInn.UnitTests/EuphoriaInn.UnitTests.csproj
```

Real manifests (8 of 13):
```
.github/workflows/binary-release.yml
.github/workflows/docker-publish.yml
.github/workflows/dotnet.yml
QuestBoard.Domain/QuestBoard.Domain.csproj
QuestBoard.Service/QuestBoard.Service.csproj
QuestBoard.UnitTests/QuestBoard.UnitTests.csproj
QuestBoard.Repository/QuestBoard.Repository.csproj
QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj
```

Matches RESEARCH.md's 13-manifest count exactly, ~8 weeks after the ghost manifests' deletion
commit (`a477ab9`, 2026-06-29). `dependenciesCount` deliberately NOT recorded as evidence —
per RESEARCH.md and Pitfall-adjacent guidance, it is a known preview-API artifact returning `0`
for every node including populated live manifests.

### Step 4 — Local ground truth (source B, independent of GitHub's cache)

Per-manifest `dotnet list <csproj> package --include-transitive`, filtered for
`Cryptography.Xml` and `Identity`, run 2026-08-26 against the local working tree
(fresh, already-restored graph):

**`QuestBoard.Domain/QuestBoard.Domain.csproj`:**
```
Microsoft.IdentityModel.Abstractions       8.14.0
Microsoft.IdentityModel.JsonWebTokens      8.14.0
Microsoft.IdentityModel.Logging            8.14.0
Microsoft.IdentityModel.Tokens             8.14.0
```
No `Cryptography.Xml` match. No base `Microsoft.AspNetCore.Identity` (2.x) match — only unrelated `Microsoft.IdentityModel.*` (JWT/OIDC) packages.

**`QuestBoard.Repository/QuestBoard.Repository.csproj`:**
```
Microsoft.AspNetCore.Identity.EntityFrameworkCore      10.0.9   10.0.9
Azure.Identity                                         1.14.2
Microsoft.Extensions.Identity.Core                     10.0.9
Microsoft.Extensions.Identity.Stores                   10.0.9
Microsoft.Identity.Client                              4.73.1
Microsoft.Identity.Client.Extensions.Msal              4.73.1
Microsoft.IdentityModel.Abstractions                   8.14.0
Microsoft.IdentityModel.JsonWebTokens                  8.14.0
Microsoft.IdentityModel.Logging                        8.14.0
Microsoft.IdentityModel.Protocols                      7.7.1
Microsoft.IdentityModel.Protocols.OpenIdConnect        7.7.1
Microsoft.IdentityModel.Tokens                         8.14.0
System.IdentityModel.Tokens.Jwt                        7.7.1
```
No `Cryptography.Xml` match. No base `Microsoft.AspNetCore.Identity` (2.x) match — only its
`.EntityFrameworkCore` sibling at `10.0.9` and unrelated Identity/IdentityModel families.

**`QuestBoard.Service/QuestBoard.Service.csproj`:**
```
Microsoft.AspNetCore.Identity.UI                 10.0.9   10.0.9
Azure.Identity                                         1.14.2
Microsoft.AspNetCore.Identity.EntityFrameworkCore      10.0.9
Microsoft.Identity.Client                              4.73.1
Microsoft.Identity.Client.Extensions.Msal              4.73.1
Microsoft.IdentityModel.Abstractions                   8.14.0
Microsoft.IdentityModel.JsonWebTokens                  8.14.0
Microsoft.IdentityModel.Logging                        8.14.0
Microsoft.IdentityModel.Protocols                      7.7.1
Microsoft.IdentityModel.Protocols.OpenIdConnect        7.7.1
Microsoft.IdentityModel.Tokens                         8.14.0
System.IdentityModel.Tokens.Jwt                        7.7.1
```
No `Cryptography.Xml` match. No base `Microsoft.AspNetCore.Identity` (2.x) match — only its
`.UI`/`.EntityFrameworkCore` siblings at `10.0.9`.

**`QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`:**
```
Azure.Identity                                             1.14.2
Microsoft.AspNetCore.Identity.EntityFrameworkCore          10.0.9
Microsoft.AspNetCore.Identity.UI                           10.0.9
Microsoft.Extensions.Identity.Core                         10.0.9
Microsoft.Extensions.Identity.Stores                       10.0.9
Microsoft.Identity.Client                                  4.73.1
Microsoft.Identity.Client.Extensions.Msal                  4.73.1
Microsoft.IdentityModel.Abstractions                       8.14.0
Microsoft.IdentityModel.JsonWebTokens                      8.14.0
Microsoft.IdentityModel.Logging                            8.14.0
Microsoft.IdentityModel.Protocols                          7.7.1
Microsoft.IdentityModel.Protocols.OpenIdConnect            7.7.1
Microsoft.IdentityModel.Tokens                             8.14.0
System.IdentityModel.Tokens.Jwt                            7.7.1
```
No `Cryptography.Xml` match. No base `Microsoft.AspNetCore.Identity` (2.x) match (test project references the app projects, inheriting the same 10.0.9 siblings).

**`QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj`:**
```
Azure.Identity                                               1.14.2
Microsoft.AspNetCore.Identity.EntityFrameworkCore            10.0.9
Microsoft.AspNetCore.Identity.UI                             10.0.9
Microsoft.Extensions.Identity.Core                           10.0.9
Microsoft.Extensions.Identity.Stores                         10.0.9
Microsoft.Identity.Client                                    4.73.1
Microsoft.Identity.Client.Extensions.Msal                    4.73.1
Microsoft.IdentityModel.Abstractions                         8.14.0
Microsoft.IdentityModel.JsonWebTokens                        8.14.0
Microsoft.IdentityModel.Logging                              8.14.0
Microsoft.IdentityModel.Protocols                            7.7.1
Microsoft.IdentityModel.Protocols.OpenIdConnect              7.7.1
Microsoft.IdentityModel.Tokens                               8.14.0
System.IdentityModel.Tokens.Jwt                              7.7.1
```
No `Cryptography.Xml` match. No base `Microsoft.AspNetCore.Identity` (2.x) match.

**Result: zero occurrences of `System.Security.Cryptography.Xml`, at any version, and zero
occurrences of the base `Microsoft.AspNetCore.Identity` 2.x package, anywhere in the live,
freshly-restored transitive graph of all five tracked `QuestBoard.*` manifests.** Concordant
with the GitHub SBOM (source A) — the two independent sources agree, avoiding the circular
reasoning Pitfall 2 warns against.

### Step 5 — Git archaeology

```
git log -S "System.Security.Cryptography.Xml" --oneline --all -- '*.csproj'
```
```
785cd29 fix(33): eliminate all build warnings
691911f Bump the nuget group with 1 update
978d3f6 net10 update and package upgrades
```
(`691911f` predates the string's introduction into any tracked `.csproj` — matched only because
`git log -S` reports the commit that changed the *occurrence count* of the string, which includes
its very first appearance further back; not itself evidentially relevant here. `785cd29` is a
later, unrelated warnings-cleanup commit. The load-bearing commit is `978d3f6`.)

```
git show 978d3f6 -- EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
```
Filtered diff (2026-04-22, author Theun Schut, "net10 update and package upgrades"):
```diff
-    <PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.3.1" />
+    <PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.3.9" />
-  <ItemGroup>
-    <PackageReference Include="System.Security.Cryptography.Xml" Version="8.0.3" />
-  </ItemGroup>
+    <PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.7" />
```
Confirms `978d3f6` bumped `System.Security.Cryptography.Xml` straight from `8.0.3` to `10.0.7` —
never revisiting the alerted `8.0.0-8.0.3` range again.

```
git show a477ab9~1:EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
```
Last live content before deletion:
```xml
<PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.9" />
```
Last live value: **`10.0.9`** — inside the advisory's `10.0.0-10.0.9` range (patched at `10.0.10`).

```
git log --diff-filter=D --oneline --all -- 'EuphoriaInn.Domain/EuphoriaInn.Domain.csproj'
```
```
a477ab9 refactor: rename EuphoriaInn -> QuestBoard
```
Confirms `a477ab9` (Mon Jun 29 2026 23:00:50 +0200) is the deletion commit for
`EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`.

**Corrected timeline (per RESEARCH.md's third finding, D-02 correction):** the 2026-04-22 bump
(`978d3f6`) ended exposure to the alerted `8.0.0-8.0.3` range only. The same advisory family also
covers `9.0.0-9.0.17` and `10.0.0-10.0.9`, and the manifest's last live value was `10.0.9` — inside
that third band. Real exposure to the full advisory family ended at the 2026-06-29 deletion
(`a477ab9`), not the 2026-04-22 bump. Nothing here implies the 2026-04-22 upgrade eliminated the
vulnerability outright.

### Step 6 — `main` HEAD drift re-check

```
git fetch origin main
```
```
ok fetched (1 new refs)
```
```
git rev-parse origin/main
```
```
89a8cb662ecd4ef4705645506b21e46b048ef87e
```
```
git diff --stat 89a8cb6 origin/main -- '*.csproj'
```
Output: **(empty)**.

**Interpretation:** `origin/main` is still at `89a8cb6` — the same HEAD RESEARCH.md observed on
2026-08-26 after Phase 72's merge. Empty diff means D-06's premise still holds: no manifest-touching
commit has reached `main` since, so the ghost manifests have had no opportunity to clear on their own.

Post-check — nothing mutated by Task 2:
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts -X GET -f state=open --jq 'length'
```
Result: **5**. All five alerts still open after Task 2.

---

## Summary of Task 1 + Task 2 findings for Task 3

1. All five alerts (#17-21) independently confirmed: same manifest (`EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`), same alerted range (`>= 8.0.0, <= 8.0.3` → patched `8.0.4`), same `created_at` day (2026-08-10), distinct GHSA/CVE per alert, all still `open`.
2. Default-branch scope confirmed as an API property (no branch/ref dimension exists in this API), not extracted as a field.
3. GitHub's SBOM shows exactly one `System.Security.Cryptography.Xml` entry, at `8.0.3`, reachable only via the ghost `Microsoft.AspNetCore.Identity 2.3.1` node.
4. GraphQL `dependencyGraphManifests` still lists all 5 ghost `EuphoriaInn.*` manifests today (`totalCount` 13).
5. Local `dotnet list package --include-transitive` across all 5 `QuestBoard.*` manifests independently confirms zero occurrences of the vulnerable package at any version — concordant with the SBOM, avoiding circular reasoning.
6. Git archaeology confirms: `978d3f6` (2026-04-22) ended exposure to the alerted `8.0.0-8.0.3` range; the manifest's last live value (`10.0.9`, before its 2026-06-29 deletion in `a477ab9`) was itself inside the advisory family's `10.0.0-10.0.9` band — the corrected timeline.
7. `main` HEAD unchanged since RESEARCH.md's snapshot (`89a8cb6`); D-06's premise still holds.
8. Nothing mutated: 5 alerts open before, during, and after both tasks.
