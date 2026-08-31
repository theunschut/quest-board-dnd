# Phase 73: Resolve Stale HIGH Security Alerts - Context

**Gathered:** 2026-08-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Triage and close the five open HIGH Dependabot alerts (#17–#21, `System.Security.Cryptography.Xml`) on `theunschut/quest-board-dnd`, each on individually re-verified evidence, and preserve the investigation in a durable record a future reviewer can re-run.

Not a code phase. Not in this phase: patching any package, evicting the stale manifests from GitHub's dependency graph, deleting the leftover `EuphoriaInn.*` working-tree directories, or triaging any alert other than #17–#21.

</domain>

<decisions>
## Implementation Decisions

### Evidence and the dependency-graph re-check (SECALERT-01, SECALERT-02)

- **D-01: The premise in ROADMAP.md is understated and must be corrected during this phase.** The roadmap's theory is "GitHub is minting alerts off a stale cached snapshot of a deleted manifest." That is true but weaker than what the evidence actually shows. GitHub's dependency graph for `main` currently carries **two dependency sets simultaneously** — the live QuestBoard one *and* a ghost set from before the .NET 10 upgrade:

  | In GitHub's graph today | Real state on `main` |
  |---|---|
  | `Microsoft.AspNetCore.Identity` **2.3.1** | absent — bumped to 2.3.11 in `978d3f6` (2026-04-22), then deleted with the manifest |
  | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` **8.0.11** | `10.0.9` |
  | `Microsoft.AspNetCore.Identity.UI` **8.0.11** | `10.0.9` |
  | `System.Security.Cryptography.Xml` **8.0.3** | absent from every manifest |

  `System.Security.Cryptography.Xml 8.0.3` hangs off `Microsoft.AspNetCore.Identity 2.3.1` — the ghost branch of the graph, not anything reachable from a live manifest.

- **D-02: The strongest single fact is the version, not the deleted file.** The alerts' vulnerable range is `>= 8.0.0, <= 8.0.3`. When `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` was deleted in `a477ab9` (2026-06-29) it pinned `System.Security.Cryptography.Xml` at **10.0.9 — not vulnerable**. The version GitHub is alerting on was upgraded away in `978d3f6` on **2026-04-22**, four months before the alerts were minted on 2026-08-10. The dismissal argument rests on this, not on "the file is gone."

- **D-03: SECALERT-02 is satisfied by GitHub's own server-side APIs, with no mutating action.** Two read-only queries are GitHub's data, not local `dotnet list package` output, which is exactly what the requirement asks for:
  - `GET /repos/{owner}/{repo}/dependency-graph/sbom` — proves the package is present in the graph and traces the `DEPENDS_ON` chain to `Microsoft.AspNetCore.Identity 2.3.1`.
  - GraphQL `repository.dependencyGraphManifests` (needs `Accept: application/vnd.github.hawkgirl-preview+json`) — returns **13** manifests for `main`: the 8 real ones plus all **5 deleted `EuphoriaInn.*` `.csproj` files**, eight weeks after `a477ab9` and five weeks after `main`'s current HEAD `69f2ea0` (2026-07-17).

- **D-04: No force-refresh is attempted. Nothing on the repo is mutated except the five dismissals.** The evidence is already complete and GitHub-sourced, so the dismissals do not need to wait on a graph repair.

- **D-05: The Dependabot alerts off/on toggle (`DELETE`/`PUT /repos/{owner}/{repo}/vulnerability-alerts`) is vetoed outright — not just for this phase, for this repo.** It is the only lever that reliably evicts ghost manifests, but the repo already holds three dismissals whose audit trail it would put at risk: **#7 and #8** (`AutoMapper`, 2026-06-25) and **#11** (`SQLitePCLRaw.lib.e_sqlite3`, 2026-06-29), all dismissed by `cryptic96` with reason `fix_started`. Destroying real audit trail to fix a cosmetic cache is a bad trade in a phase whose entire purpose is defensible evidence. *Operator confirmed explicitly.*

- **D-06: Accepted consequence — the ghost manifests survive this phase, so GitHub will keep minting alerts off them.** Dismissing these five does not stop a sixth. This is a knowing trade for D-05, not an oversight, and it is the reason D-13 structures the record as an ongoing log. The only remaining non-destructive lever is a manifest-touching commit reaching `main`, which the `milestone/v9-rolling-improvements` branch will do naturally at merge — revisit the ghost-manifest question then, not inside this phase.

### Dismissal mechanics (SECALERT-03)

- **D-07: `dismissed_reason` is `inaccurate`, not the `not_used` named in the ROADMAP scope note.** `not_used` means "the package is present and the vulnerable code path isn't reached" — a weaker and factually different claim than what was found. GitHub's attribution is simply wrong: it is alerting on a version absent since 2026-04-22, in a manifest absent since 2026-06-29. `inaccurate` is the only reason code that states that truthfully, and it is permanent audit metadata. **This deliberately overrides the ROADMAP scope note.**

- **D-08: Each comment leads with a per-alert verification line, then the shared conclusion, then a pointer to the durable record.** The honest position is that the *evidence* is identical across all five, because it is evidence about the manifest, not about the CVE — all five share package, manifest, range `>= 8.0.0, <= 8.0.3` and patched version `8.0.4`. Rewording five near-identical conclusions would fake independent analysis. So the individuality lives in the **verification that actually happened** — each alert opened, its own CVE/GHSA named, its own range confirmed against the ghost manifest, its own check timestamp — not in the prose.

- **D-09: `dismissed_comment` is a short field — believed capped at 280 characters. The planner MUST verify the real limit against the API before drafting.** If the cap holds, full evidence cannot live in the comment, which independently forces the D-11 split (short comment + pointer to the durable record).

- **D-10: All five are posted as `cryptic96`.** It is the currently-active `gh` account and the actor on all three existing dismissals (#7, #8, #11), so the repo's dismissal history stays attributable to one consistent hand. The executor must confirm the active account before the first PATCH rather than assume it.

- **D-11: Operator gate — the executor presents all five drafted comments and verification lines together, the operator approves once, then all five PATCH calls fire.** Every individual comment is read before anything posts; only the keystroke is shared. Rejected: five separate confirmations (approval fatigue turns five prompts into five reflexive yeses, which is the rubber stamp arriving through the back door). **No dismissal is posted without this approval** — ROADMAP names this explicitly, and it is an outward-facing action on a public repo.

### Where the record lives (SECALERT-04)

- **D-12: Full evidence goes in a new durable file, `.planning/SECURITY-TRIAGE.md`, outside the phase directory.** `/gsd-cleanup` archives phase directories at milestone close, so a `73-EVIDENCE.md` pointer would break exactly when a future reviewer needs it. PROJECT.md is already ~105KB and loaded as project context every session, so the full evidence table does not belong inline.

- **D-13: `.planning/SECURITY-TRIAGE.md` is an append-only dated log, with this incident as entry one.** Because D-06 leaves the ghost manifests in place, a sixth alert is *expected*. The next reviewer then inherits the precedent and the reproduction queries instead of re-deriving them.

- **D-14: PROJECT.md gets two hooks, satisfying SECALERT-04 without bulk.**
  - a **Known issues / tech debt** bullet under `## Context` for the ghost-manifest root cause, carrying the re-runnable reproduction query and noting the D-05 veto as the reason it stays unfixed;
  - a **Key Decisions** table row for the dismiss-not-patch call.

  Both link `.planning/SECURITY-TRIAGE.md`.

- **D-15: The record is a conclusion plus a re-runnable appendix.** Narrative conclusion and per-alert table up top; below it, the exact `gh` commands with their *filtered* captured output and timestamps — the SBOM relationship query, the `dependencyGraphManifests` query, and the `git log -S` archaeology on `978d3f6`. Filtered, not raw: the SBOM alone is 438 packages and an unfiltered dump would bury the three facts that matter. A reviewer can re-run each command and diff. **This appendix is the artifact that structurally separates triage from a rubber stamp** — without it, SECALERT-04 is satisfied only in form.

### Go / no-go rule

- **D-16: Two-part per-alert gate, re-verified immediately before each PATCH.** For each alert independently: **(a)** its `manifest_path` resolves to one of the five `EuphoriaInn.*` ghost manifests, and **(b)** `System.Security.Cryptography.Xml` at a version inside that alert's range is absent from every `QuestBoard.*` manifest on `main`. Both must pass or that alert is not dismissed. The gate and the evidence are the same act — this is the check D-08's comment cites, performed at the moment of the action rather than quoted from this discussion.

- **D-17: A gate failure on one alert does not block the others.** The passing alerts are dismissed on their own evidence; the failing one stops and is written up as an open finding for the operator. Per-alert reasoning implies per-alert outcomes — holding four defensible dismissals hostage to one anomaly would be batch thinking wearing a different hat.

- **D-18: If gate part (b) fails, patching is OUT of scope.** Gate part (b) failing means the package is genuinely reachable from a `QuestBoard.*` manifest. That outcome means the triage succeeded at its actual job. The fix is a code change with a build, a test run and a deploy: a different phase with different risks, no plan, no research and no tests. The executor writes up the finding, dismisses nothing for that alert, and the operator scopes the follow-up. Explicitly rejected: "patch only if trivial" — *trivial* is the judgement call that expands once someone is already mid-change.

- **D-19: Success criterion #5 is read as "alerts #17–#21 are dismissed and no HIGH alert this phase was scoped to handle remains open."** A sixth, unrelated HIGH alert landing mid-phase does not block closure — it becomes entry two in `.planning/SECURITY-TRIAGE.md` and gets its own triage. The literal "zero open HIGH alerts" reading would make closure hostage to GitHub's advisory feed, which nobody controls, and which D-06 guarantees will fire again. The ROADMAP text is not edited; this reading is recorded here instead.

### Claude's Discretion

Operator answered "you decide" on the graph-refresh framing (D-01/D-04/D-06 record the call and its reasoning). Beyond that, the planner decides:

- Exact wording of the five dismissal comments, within D-08's shape and D-09's character budget.
- The precise `jq`/`node` filter expressions captured in the D-15 appendix.
- Section structure and entry format of `.planning/SECURITY-TRIAGE.md`, and the exact wording of the two PROJECT.md hooks.
- Whether the D-16 gate is a script the executor runs per alert or inline commands, and how its output is captured into the appendix.
- Whether to record the three pre-existing dismissals (#7, #8, #11) in the new log as historical context — useful for D-05's rationale, but not required by any requirement.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` — Phase 73 entry: goal, 5 success criteria, scope notes, "Evidence gathered so far", and the two named risks (rubber-stamping, losing the audit trail). **Note:** its `dismissed_reason=not_used` scope note is deliberately overridden by D-07, and its "Evidence gathered so far" is superseded by D-01/D-02.
- `.planning/REQUIREMENTS.md` — SECALERT-01 … SECALERT-05 in full

### Project constraints and conventions
- `.planning/PROJECT.md` — `## Context` → *Known issues / tech debt* bullet list and `## Key Decisions` table are the two insertion points for D-14
- `CLAUDE.md` — **Branching** rule (never commit to `main`; this phase makes no commits to `main` at all), **Code Comments** rule (no GSD tracking IDs in source — applies to the new `.planning/SECURITY-TRIAGE.md` only insofar as it is a planning doc, where IDs are fine)

### GitHub API surface this phase uses
- `GET /repos/{owner}/{repo}/dependabot/alerts` — list, with `state=open|dismissed|all`
- `GET /repos/{owner}/{repo}/dependabot/alerts/{alert_number}` — per-alert detail for the D-16 gate
- `PATCH /repos/{owner}/{repo}/dependabot/alerts/{alert_number}` — `state=dismissed`, `dismissed_reason=inaccurate`, `dismissed_comment=…`. No bulk endpoint exists, which conveniently forces the per-alert action SECALERT-03 requires.
- `GET /repos/{owner}/{repo}/dependency-graph/sbom` — GitHub's own current graph view
- GraphQL `repository.dependencyGraphManifests` with `Accept: application/vnd.github.hawkgirl-preview+json` — the decisive ghost-manifest query
- **Verify the `dismissed_comment` length cap before drafting comments (D-09).**

### To be created by this phase
- `.planning/SECURITY-TRIAGE.md` — does not exist yet; D-12/D-13/D-15 define it

</canonical_refs>

<code_context>
## Existing Code Insights

No application code is touched. The relevant "code context" is repository and GitHub state, confirmed 2026-08-26.

### Repository state
- Tracked manifests are exactly five: `QuestBoard.{Domain,Repository,Service,UnitTests,IntegrationTests}/*.csproj`. Identity packages are `Microsoft.AspNetCore.Identity.EntityFrameworkCore` **10.0.9** (Repository) and `Microsoft.AspNetCore.Identity.UI` **10.0.9** (Service). `Microsoft.AspNetCore.Identity` 2.3.1 and `System.Security.Cryptography.Xml` appear in **zero** tracked manifests.
- The five `EuphoriaInn.*` directories in the working tree contain **only** `bin/` and `obj/` build output — zero `.csproj`, zero `.cs` outside build output — and are matched by `.gitignore:25`/`:26`. Local-only; invisible to GitHub. Deleting them would not affect the graph.
- `main` HEAD: `69f2ea0` (2026-07-17). Manifest deletion: `a477ab9` (2026-06-29). Version bump that stranded the graph: `978d3f6` (2026-04-22).

### GitHub state
- Repo is **public**, renamed: `repos/theunschut/quest-board` resolves by redirect to **`theunschut/quest-board-dnd`**, which is what alert `html_url`s and the SBOM document name use. Success criterion #5's command works as written via the redirect; the GraphQL query needs the **real** name `quest-board-dnd`.
- `dependabot_security_updates` is **enabled**; secret scanning is disabled.
- Five open alerts, all created 2026-08-10, all `scope=runtime`, all range `>= 8.0.0, <= 8.0.3`, all patched at `8.0.4`:

  | Alert | CVE | GHSA | Class | CVSS |
  |---|---|---|---|---|
  | #17 | CVE-2026-47304 | `GHSA-g8r8-53c2-pm3f` | Security Feature Bypass | 8.1 |
  | #18 | CVE-2026-50525 | `GHSA-8q5v-6pqq-x66h` | Denial of Service | 7.5 |
  | #19 | CVE-2026-47302 | `GHSA-cvvh-rhrc-wg4q` | Denial of Service | 7.5 |
  | #20 | CVE-2026-50648 | `GHSA-23rf-6693-g89p` | Denial of Service | 7.5 |
  | #21 | CVE-2026-50527 | `GHSA-mmjf-rqrv-855v` | Denial of Service | 7.5 |

- Three pre-existing dismissals to protect (D-05): **#7**, **#8** (`AutoMapper`, 2026-06-25) and **#11** (`SQLitePCLRaw.lib.e_sqlite3`, 2026-06-29), all `fix_started`, all by `cryptic96`.

### Integration points
- `.planning/PROJECT.md` `## Context` → *Known issues / tech debt* list, and `## Key Decisions` table (D-14).
- `gh` CLI 2.89.0, authenticated for both `cryptic96` (active) and `theunschut`. Token scopes on the active account: `admin:org`, `gist`, `repo` — **the planner should confirm these suffice for `PATCH .../dependabot/alerts`** on a public repo before execution, rather than discovering it at the gate.

</code_context>

<specifics>
## Specific Ideas

- The three facts the record must lead with, in this order: **(1)** the alerted version 8.0.3 was upgraded away on 2026-04-22, four months before the alerts were minted; **(2)** the manifest itself was deleted on 2026-06-29; **(3)** GitHub still lists all five deleted manifests in `dependencyGraphManifests` today. Fact (1) is the strongest and is currently absent from ROADMAP.md entirely.
- The reviewer this record is written for is a sceptic six months out asking "did they actually check, or did they just click dismiss five times?" Every structural decision here — D-08's verification line, D-15's re-runnable appendix, D-16's gate-at-the-moment-of-action — exists to answer that specific person.

</specifics>

<deferred>
## Deferred Ideas

- **Evict the ghost manifests from GitHub's dependency graph.** The actual root cause. Blocked by the D-05 veto (the reliable lever is destructive) and by CLAUDE.md's never-commit-to-`main` rule (the safe lever needs a manifest-touching commit on `main`). Revisit when `milestone/v9-rolling-improvements` merges to `main` naturally — re-run the `dependencyGraphManifests` query afterwards and see whether the ghosts cleared on their own. Not a phase of its own yet.
- **Delete the leftover `EuphoriaInn.*` `bin/`/`obj/` directories in the working tree.** Gitignored, local-only, invisible to GitHub — cosmetic cleanup with zero bearing on the alerts. ROADMAP already calls it optional and not a deliverable.
- **Patching `System.Security.Cryptography.Xml`.** Only becomes real work if the D-16 gate fails part (b), in which case D-18 routes it to its own phase.
- **Backfilling `#7`/`#8`/`#11` into `.planning/SECURITY-TRIAGE.md` as historical entries.** Left to planner discretion; no requirement covers it.

</deferred>

---

*Phase: 73-resolve-stale-high-security-alerts*
*Context gathered: 2026-08-26*
