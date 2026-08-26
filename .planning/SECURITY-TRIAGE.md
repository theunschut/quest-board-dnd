# Security Alert Triage Log

This file is an **append-only, dated log** of Dependabot security-alert triage incidents for
`theunschut/quest-board-dnd`. Newest entries go last, at the bottom of the file. Nothing here is
edited or removed once written — if a later entry needs to correct an earlier one, it does so
explicitly, by reference, not by rewriting history.

This file lives at `.planning/` root, deliberately outside any `phases/` directory, because
`/gsd-cleanup` archives phase directories at milestone close. A pointer into a phase directory
would break exactly when a future reviewer needs it. Everything a reviewer needs is copied in
below, not linked to a phase artifact.

---

## Entry 1 - 2026-08-26 - Dependabot alerts #17-#21, System.Security.Cryptography.Xml (HIGH)

**Outcome:** Pending - dismissals gated on operator approval (see plan 73-02)

### Conclusion

Three facts, in the order that matters most:

1. The alerted version `8.0.3` was upgraded away on **2026-04-22** in commit `978d3f6`, four
   months before these alerts were minted on 2026-08-10.
2. The manifest that carried the package, `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`, was
   **deleted outright** on **2026-06-29** in commit `a477ab9` ("refactor: rename EuphoriaInn ->
   QuestBoard").
3. GitHub's own `dependencyGraphManifests` query still lists all five deleted `EuphoriaInn.*`
   manifests today, 2026-08-26 — nearly two months after their deletion.

**This supports one conclusion: GitHub is attributing a vulnerability to a package/version
combination that exists in no live manifest on `main`.** The attribution is factually wrong, not
merely stale. `dismissed_reason=inaccurate` (not `not_used`) is the reason code that states this
truthfully — `not_used` would imply the package is present but its vulnerable code path is
unreached, which is a different and weaker claim than what was actually found.

### Correction to the original framing

The original framing (captured in the phase's `73-CONTEXT.md`, decision D-02) held that the
2026-04-22 upgrade was the single strongest fact, because it moved the package's version out of
the specific range GitHub is alerting on (`>= 8.0.0, <= 8.0.3`). That is still literally true and
still sufficient on its own — but it is not the whole timeline, and stating it without the rest
would leave a hole a reviewer checking the GHSA advisory would find immediately.

All five advisories in this incident (`GHSA-g8r8-53c2-pm3f`, `GHSA-8q5v-6pqq-x66h`,
`GHSA-cvvh-rhrc-wg4q`, `GHSA-23rf-6693-g89p`, `GHSA-mmjf-rqrv-855v`) cover **three** affected
ranges for `System.Security.Cryptography.Xml`, not just the one GitHub alerted on:

| Affected range | Patched at |
|---|---|
| `>= 8.0.0, <= 8.0.3` | `8.0.4` |
| `>= 9.0.0, <= 9.0.17` | `9.0.18` |
| `>= 10.0.0, <= 10.0.9` | `10.0.10` |

The manifest's **last live value before deletion was `10.0.9`** (confirmed via
`git show a477ab9~1:EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`) — inside the third,
`10.0.0-10.0.9` band, patched only at `10.0.10`. So the 2026-04-22 bump ended exposure to the
*specific* `8.0.0-8.0.3` range GitHub alerted on, and nothing more; the vulnerability class
(across all three ranges of the same advisory family) continued to apply to the manifest all the
way through `10.0.7` and then `10.0.9`, right up until the manifest itself was deleted on
2026-06-29. **Real end of exposure: 2026-06-29 (`a477ab9`), not 2026-04-22.**

Nothing in this record claims the 2026-04-22 upgrade eliminated the vulnerability. It sharpens
the dismissal case rather than weakening it: the package is confirmed absent from every live
manifest, at any version, today — corroborated by two independent sources (see Root Cause below)
— regardless of which version-range framing is used.

### Per-alert table

| Alert | CVE | GHSA | CVSS | Class | `manifest_path` | `created_at` | Branch scope | Gate (a) | Gate (b) | Dismissed at | `dismissed_reason` |
|---|---|---|---|---|---|---|---|---|---|---|---|
| #17 | CVE-2026-47304 | GHSA-g8r8-53c2-pm3f | 8.1 | Security Feature Bypass | `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` | 2026-08-10T20:34:08Z | default branch (main) - API has no per-branch dimension | *(pending 73-02)* | *(pending 73-02)* | | |
| #18 | CVE-2026-50525 | GHSA-8q5v-6pqq-x66h | 7.5 | Denial of Service | `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` | 2026-08-10T20:34:08Z | default branch (main) - API has no per-branch dimension | *(pending 73-02)* | *(pending 73-02)* | | |
| #19 | CVE-2026-47302 | GHSA-cvvh-rhrc-wg4q | 7.5 | Denial of Service | `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` | 2026-08-10T20:34:09Z | default branch (main) - API has no per-branch dimension | *(pending 73-02)* | *(pending 73-02)* | | |
| #20 | CVE-2026-50648 | GHSA-23rf-6693-g89p | 7.5 | Denial of Service | `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` | 2026-08-10T20:34:09Z | default branch (main) - API has no per-branch dimension | *(pending 73-02)* | *(pending 73-02)* | | |
| #21 | CVE-2026-50527 | GHSA-mmjf-rqrv-855v | 7.5 | Denial of Service | `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` | 2026-08-10T20:34:09Z | default branch (main) - API has no per-branch dimension | *(pending 73-02)* | *(pending 73-02)* | | |

All five share the same package, manifest, alerted range (`>= 8.0.0, <= 8.0.3` → patched
`8.0.4`), and detection day — because the evidence is about the manifest, not about the CVE. The
individuality is in the verification: each alert was opened by its own API call, with its own
CVE/GHSA/CVSS confirmed separately (see appendix).

### Root cause, and what is not fixed

GitHub's dependency graph for `main` currently carries **two dependency sets simultaneously** —
the live QuestBoard one, and a ghost set left over from before the .NET 10 upgrade:

| In GitHub's graph today | Real state on `main` |
|---|---|
| `Microsoft.AspNetCore.Identity` **2.3.1** | absent - bumped to `2.3.9` in `978d3f6` (2026-04-22), then to `2.3.11`, then deleted with the manifest in `a477ab9` (2026-06-29) |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` **8.0.11** | `10.0.9` |
| `Microsoft.AspNetCore.Identity.UI` **8.0.11** | `10.0.9` |
| `System.Security.Cryptography.Xml` **8.0.3** | absent from every manifest, at any version |

`System.Security.Cryptography.Xml 8.0.3` hangs off `Microsoft.AspNetCore.Identity 2.3.1` — the
ghost branch of the graph, not anything reachable from a live manifest. Confirmed two
independent ways: GitHub's own SBOM (`DEPENDS_ON` chain traced directly to the ghost Identity
node) and a fresh local `dotnet list package --include-transitive` sweep across all five live
`QuestBoard.*` manifests (zero matches for the vulnerable package or the ghost Identity 2.x line,
at any version).

**Accepted trade, not an oversight: the ghost manifests survive this phase.** GitHub will keep
minting alerts off them — a sixth alert (or more) is expected, which is why this log is
structured to grow. The only lever that reliably evicts ghost manifests from GitHub's dependency
graph is toggling Dependabot alerts off and on (`DELETE`/`PUT
/repos/{owner}/{repo}/vulnerability-alerts`), and that lever is **permanently vetoed for this
repo**: it would put the audit trail on the three existing dismissals at risk —
**#7** and **#8** (`AutoMapper`, dismissed 2026-06-25) and **#11** (`SQLitePCLRaw.lib.e_sqlite3`,
dismissed 2026-06-29), all `dismissed_reason=fix_started`, all dismissed by `cryptic96`.
Destroying real audit trail to fix a cosmetic cache staleness issue is not a trade this repo
makes.

The one remaining **non-destructive** lever: a manifest-touching commit reaching `main`. The
`milestone/v9-rolling-improvements` branch will do this naturally when it merges. Re-run the
GraphQL `dependencyGraphManifests` query after that merge and check whether the ghost manifests
finally cleared. As of this entry (`main` at `89a8cb6`), no manifest-touching commit has reached
`main` since Phase 72's merge.

### Appendix - re-runnable commands

Every command below is a read. None mutates anything. Run 2026-08-26 unless noted.

**A1 — Open alert set confirmation:**
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts -X GET -f state=open --paginate --jq '.[] | "\(.number) \(.state) \(.security_vulnerability.severity) \(.dependency.package.name) \(.dependency.manifest_path)"'
```
```
21 open high System.Security.Cryptography.Xml EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
20 open high System.Security.Cryptography.Xml EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
19 open high System.Security.Cryptography.Xml EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
18 open high System.Security.Cryptography.Xml EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
17 open high System.Security.Cryptography.Xml EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
```
Confirms the open set is exactly {17,18,19,20,21} — no extra, missing, or pre-dismissed alert.

**A2 — Per-alert detail fetch (one of five, alert #17; same shape for #18-#21):**
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 -X GET
```
Key fields: `manifest_path=EuphoriaInn.Domain/EuphoriaInn.Domain.csproj`,
`vulnerable_version_range=>= 8.0.0, <= 8.0.3`, `first_patched_version=8.0.4`,
`created_at=2026-08-10T20:34:08Z`, `state=open`, `dismissed_at=null`.

**A3 — Full advisory range fetch (alert #17; ranges identical across all five since they share
one advisory family):**
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

**A4 — Branch/ref-key absence probe:**
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 --jq '[paths(scalars) | map(tostring) | join(".")] | map(select(test("ref|branch";"i"))) | length'
```
Literal result: `6`. Investigated: all six are `security_advisory.references[N].url` (the
advisory's own citation URLs) — a substring match on `ref` inside the field name `references`,
not a branch/ref dimension. Refined, excluding that field:
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts/17 --jq '[paths(scalars) | map(tostring) | join(".")] | map(select(test("ref|branch";"i"))) | map(select(test("references")|not)) | length'
```
Result: `0`. **Confirmed default-branch-scoped (main); the Dependabot Alerts API exposes no
per-branch dimension whatsoever** — unlike the Code Scanning API's `instances[].ref`. No branch
or ref field was extracted from any payload as evidence.

**A5 — SBOM package filter (source A; GitHub's own graph is the subsystem this incident proves
stale, so this is corroboration, not the sole proof):**
```
gh api repos/theunschut/quest-board-dnd/dependency-graph/sbom --jq '[.sbom.packages[] | select(.name=="System.Security.Cryptography.Xml") | .versionInfo] | join(",")'
```
Result: `8.0.3` — exactly one entry, one version, no `10.0.x` anywhere in the live graph.
(Caveat: this SBOM-generation endpoint transiently returned a `500 Request timed out` on its
first call this session; an immediate retry succeeded with the result above — an operational
note for whoever re-runs this, not a material finding.)

**A6 — SBOM `DEPENDS_ON` chain:**
```
gh api repos/theunschut/quest-board-dnd/dependency-graph/sbom --jq '.sbom.packages[] | select(.name=="System.Security.Cryptography.Xml") | .SPDXID'
```
```
SPDXRef-nuget-System.Security.Cryptography.Xml-8.0.3-84ce5b
```
(Re-derive this SPDXID fresh each run rather than hardcoding it, in case GitHub regenerates the
hash suffix — it was stable across every fetch in this session.)
```
gh api repos/theunschut/quest-board-dnd/dependency-graph/sbom --jq '.sbom.relationships[] | select(.relatedSpdxElement == "SPDXRef-nuget-System.Security.Cryptography.Xml-8.0.3-84ce5b") | .spdxElementId'
```
```
SPDXRef-nuget-Microsoft.AspNetCore.Identity-2.3.1-58ae84
SPDXRef-github-theunschut-quest-board-dnd-main-d23e00
```
Two incoming edges: the ghost `Microsoft.AspNetCore.Identity 2.3.1` node, and the repo-level root
node (the SBOM flattens the whole graph under one root and carries no per-manifest attribution
itself — the "which manifest" question is answered by A2's `manifest_path` and A7 below).

**A7 — GraphQL `dependencyGraphManifests`:**
```
gh api graphql -H "Accept: application/vnd.github.hawkgirl-preview+json" -f query='query { repository(owner: "theunschut", name: "quest-board-dnd") { dependencyGraphManifests(first: 50) { totalCount nodes { filename blobPath parseable } } } }'
```
`totalCount: 13`. Ghost manifests present (5 of 13):
```
EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
EuphoriaInn.IntegrationTests/EuphoriaInn.IntegrationTests.csproj
EuphoriaInn.Repository/EuphoriaInn.Repository.csproj
EuphoriaInn.Service/EuphoriaInn.Service.csproj
EuphoriaInn.UnitTests/EuphoriaInn.UnitTests.csproj
```
Real manifests present (8 of 13): `.github/workflows/{binary-release,docker-publish,dotnet}.yml`,
`QuestBoard.{Domain,Service,UnitTests,Repository,IntegrationTests}/QuestBoard.*.csproj`.
(`dependenciesCount` is deliberately not cited anywhere as evidence — it is a known
`hawkgirl-preview` artifact returning `0` for every node, including populated live manifests.)

**A8 — Local `dotnet list package --include-transitive` sweep (source B, independent of GitHub's
cache), run against each of the five tracked `QuestBoard.*` manifests, filtered for
`Cryptography.Xml`/`Identity`:**
```
dotnet list QuestBoard.Domain/QuestBoard.Domain.csproj package --include-transitive
dotnet list QuestBoard.Repository/QuestBoard.Repository.csproj package --include-transitive
dotnet list QuestBoard.Service/QuestBoard.Service.csproj package --include-transitive
dotnet list QuestBoard.UnitTests/QuestBoard.UnitTests.csproj package --include-transitive
dotnet list QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj package --include-transitive
```
Result across all five: zero occurrences of `System.Security.Cryptography.Xml` at any version;
zero occurrences of the base `Microsoft.AspNetCore.Identity` 2.x package. Only its unrelated
`.EntityFrameworkCore`/`.UI` siblings at `10.0.9`, and the unrelated `Microsoft.IdentityModel.*`
(JWT/OIDC) family, appear. Concordant with A5/A6 — two independent sources agree.

**A9 — Git archaeology, version-bump commit:**
```
git log -S "System.Security.Cryptography.Xml" --oneline --all -- '*.csproj'
```
```
785cd29 fix(33): eliminate all build warnings
691911f Bump the nuget group with 1 update
978d3f6 net10 update and package upgrades
```
```
git show 978d3f6 -- EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
```
```diff
-    <PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.3.1" />
+    <PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.3.9" />
-  <ItemGroup>
-    <PackageReference Include="System.Security.Cryptography.Xml" Version="8.0.3" />
-  </ItemGroup>
+    <PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.7" />
```
`978d3f6` (2026-04-22, Theun Schut, "net10 update and package upgrades") bumped
`System.Security.Cryptography.Xml` straight from `8.0.3` to `10.0.7`, never revisiting the
alerted range again.

**A10 — Git archaeology, manifest deletion commit:**
```
git show a477ab9~1:EuphoriaInn.Domain/EuphoriaInn.Domain.csproj
```
```xml
<PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.9" />
```
```
git log --diff-filter=D --oneline --all -- 'EuphoriaInn.Domain/EuphoriaInn.Domain.csproj'
```
```
a477ab9 refactor: rename EuphoriaInn -> QuestBoard
```
`a477ab9` (2026-06-29 23:00:50 +0200) deleted the manifest. Its last live value, `10.0.9`, sits
inside the advisory family's `10.0.0-10.0.9` band (patched only at `10.0.10`) — the fact behind
this entry's Correction section above.

**A11 — `main` HEAD drift re-check:**
```
git fetch origin main
git rev-parse origin/main
git diff --stat 89a8cb6 origin/main -- '*.csproj'
```
`origin/main` = `89a8cb662ecd4ef4705645506b21e46b048ef87e`. Diff output: empty. D-06's premise
still holds — no manifest-touching commit has reached `main` since this incident's research
snapshot.

**A12 — Post-task mutation check (run after each of Tasks 1 and 2):**
```
gh api repos/theunschut/quest-board-dnd/dependabot/alerts -X GET -f state=open --jq 'length'
```
Result both times: `5`. No dismissal occurred while gathering this evidence.
