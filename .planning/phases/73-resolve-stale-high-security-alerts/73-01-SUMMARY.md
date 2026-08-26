---
phase: 73-resolve-stale-high-security-alerts
plan: 01
subsystem: security
tags: [dependabot, github-api, sbom, graphql, security-triage, dependency-graph]

# Dependency graph
requires: []
provides:
  - "Live, re-verified per-alert evidence for Dependabot alerts #17-#21 (GHSA, CVE, CVSS, full advisory range set, manifest_path, created_at, html_url), captured 2026-08-26"
  - "Confirmed default-branch scope as an API property (no branch/ref dimension exists in the Dependabot Alerts API)"
  - "Live re-read of GitHub's SBOM and dependencyGraphManifests GraphQL query, corroborated by a fresh local dotnet list package --include-transitive sweep across all five QuestBoard.* manifests"
  - "Corrected timeline: real exposure to the advisory family ended 2026-06-29 (manifest deletion), not 2026-04-22 (version bump out of the alerted range only)"
  - ".planning/SECURITY-TRIAGE.md — new durable, append-only, evidence-carrying log, entry one for this incident"
affects: [73-02-dismiss-alerts, security-triage-log]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Durable security-triage log at .planning/ root (survives phase-directory archival), append-only, evidence copied in rather than linked"
    - "Two-independent-source verification for dependency-graph absence claims (GitHub SBOM + local dotnet list package --include-transitive), avoiding circular reliance on the subsystem already proven stale"

key-files:
  created:
    - ".planning/phases/73-resolve-stale-high-security-alerts/73-EVIDENCE-CAPTURE.md"
    - ".planning/SECURITY-TRIAGE.md"
  modified: []

key-decisions:
  - "Combined Task 1 and Task 2 into a single commit (both write incrementally to 73-EVIDENCE-CAPTURE.md and were completed in one uninterrupted work session) rather than two separate task commits"
  - "Recorded the branch/ref-key absence probe's literal result (6, all security_advisory.references[].url false positives) alongside the refined result (0) rather than silently reporting only the expected 0 — a transparency choice the plan's freshness/delta-flagging instruction calls for"

patterns-established: []

requirements-completed: [SECALERT-01, SECALERT-02]

coverage:
  - id: D1
    description: "Each of alerts #17-#21 investigated individually via its own API call, with manifest attribution, detection timestamp, full advisory range set, and confirmed default-branch scope recorded per alert"
    requirement: "SECALERT-01"
    verification:
      - kind: other
        ref: "gh api repos/theunschut/quest-board-dnd/dependabot/alerts/{17,18,19,20,21} -X GET (5 separate calls); grep -oE GHSA-... .planning/SECURITY-TRIAGE.md | sort -u | wc -l == 5"
        status: pass
    human_judgment: false
  - id: D2
    description: "GitHub's server-side dependency graph re-checked live via SBOM and GraphQL dependencyGraphManifests, no force-refresh, no mutation, corroborated by an independent local dotnet list package --include-transitive sweep"
    requirement: "SECALERT-02"
    verification:
      - kind: other
        ref: "gh api .../dependency-graph/sbom --jq (versionInfo join) == 8.0.3; gh api graphql dependencyGraphManifests totalCount == 13 with 5 ghost EuphoriaInn.* filenames; dotnet list package --include-transitive x5 QuestBoard.* manifests, zero matches"
        status: pass
    human_judgment: false
  - id: D3
    description: ".planning/SECURITY-TRIAGE.md created as an append-only durable log with entry one carrying the conclusion, a named correction to the original 2026-04-22 framing, per-alert table (dismissal columns pending), root-cause section with the D-05 veto, and a 12-command re-runnable appendix"
    verification:
      - kind: other
        ref: "grep -c '^## Entry 1' .planning/SECURITY-TRIAGE.md == 1; grep -cE '^\\| *#?(17|18|19|20|21) *\\|' == 5; grep -c 73-EVIDENCE-CAPTURE == 0"
        status: pass
    human_judgment: false

duration: 13min
completed: 2026-08-26
status: complete
---

# Phase 73 Plan 01: Evidence Capture and Durable Triage Record Summary

**Live re-verification of all five open Dependabot alerts (#17-#21) plus GitHub's SBOM/GraphQL dependency-graph state, corroborated by a fresh local `dotnet list package` sweep, written into a new durable `.planning/SECURITY-TRIAGE.md` log — zero mutations, all five alerts still open.**

## Performance

- **Duration:** ~13 min
- **Started:** 2026-08-26T09:23:06+02:00 (approx, plan-load time)
- **Completed:** 2026-08-26T09:35:34+02:00
- **Tasks:** 3 (executed as 2 commits — Tasks 1+2 shared one commit, Task 3 its own)
- **Files modified:** 2 (both new)

## Accomplishments

- Fetched all five alerts (#17-#21) individually via five separate `gh api` calls; recorded per-alert GHSA, CVE, CVSS, full three-range advisory family (`8.0.0-8.0.3`, `9.0.0-9.0.17`, `10.0.0-10.0.9`), `manifest_path`, `created_at`, `html_url` — no delta from RESEARCH.md's 2026-08-26 snapshot for any of the five.
- Confirmed default-branch scope as a genuine API property (no branch/ref dimension exists in the Dependabot Alerts API), investigating and explaining a literal-probe false positive (6 matches, all `security_advisory.references[].url`) rather than silently reporting the expected `0`.
- Re-read GitHub's SBOM (`System.Security.Cryptography.Xml` present exactly once, at `8.0.3`, `DEPENDS_ON` chain traced to the ghost `Microsoft.AspNetCore.Identity 2.3.1` node) and the `dependencyGraphManifests` GraphQL query (`totalCount: 13`, all 5 ghost `EuphoriaInn.*` manifests still listed) — both live, no force-refresh, no mutation.
- Corroborated GitHub's graph with a fresh local `dotnet list package --include-transitive` sweep across all five `QuestBoard.*` manifests: zero occurrences of the vulnerable package or the base `Microsoft.AspNetCore.Identity` 2.x line, at any version, anywhere.
- Captured git archaeology (`978d3f6` version bump, `a477ab9` manifest deletion) confirming the corrected timeline: the alerted `8.0.0-8.0.3` range was abandoned 2026-04-22, but the manifest's last live value (`10.0.9`, before its 2026-06-29 deletion) sat inside the same advisory family's `10.0.0-10.0.9` band — real exposure ended at deletion, not the earlier bump.
- Confirmed `main` HEAD drift is empty since RESEARCH.md's snapshot (`89a8cb6` → `89a8cb6`) — D-06's premise still holds.
- Created `.planning/SECURITY-TRIAGE.md`, a new durable append-only log at `.planning/` root, entry one carrying the conclusion, the named correction, the per-alert table (dismissal columns pending), the root-cause section (D-05 veto, dismissals #7/#8/#11 named), and a 12-command re-runnable appendix.
- Verified zero mutation throughout: all five alerts remained `open` before, during, and after both tasks.

## Task Commits

Tasks 1 and 2 both write incrementally to the same working file (`73-EVIDENCE-CAPTURE.md`) and were executed in one uninterrupted pass, so they were committed together rather than as two separate commits:

1. **Tasks 1+2: Open all five alerts individually and capture per-alert evidence; re-check GitHub's server-side dependency graph and corroborate with local ground truth** - `9996b06` (docs)
2. **Task 3: Write `.planning/SECURITY-TRIAGE.md` entry one** - `c6123be` (docs)

No separate plan-metadata commit in this run (worktree mode — STATE.md/ROADMAP.md are excluded; SUMMARY.md commit follows this summary's creation per the worktree protocol).

## Files Created/Modified

- `.planning/phases/73-resolve-stale-high-security-alerts/73-EVIDENCE-CAPTURE.md` - Working capture: five per-alert sections, branch-scope finding, SBOM/GraphQL/local-sweep/git-archaeology/HEAD-drift evidence
- `.planning/SECURITY-TRIAGE.md` - New durable append-only log, entry one for this incident

## Decisions Made

- **Combined Task 1 and Task 2 into one commit.** Both tasks append to the same working file and were completed back-to-back without an intervening stopping point; splitting them post-hoc into two commits would have added no traceability value. Task 3's commit is separate since it targets a different file (`SECURITY-TRIAGE.md`).
- **Recorded the literal branch/ref probe result (6) alongside the refined result (0), with the cause explained (`security_advisory.references[].url` substring false positives), rather than silently asserting the plan's expected `0`.** This is exactly the kind of delta the plan's freshness instructions require flagging rather than resolving invisibly.
- **Noted the transient SBOM-endpoint `500 Request timed out` and its successful retry as an operational footnote, not a material finding** — GitHub's SBOM-generation endpoint itself being slow/unreliable is consistent with (not contradictory to) this phase's premise that GitHub's dependency-graph subsystem is stale/cached, but does not change any recorded fact once the retry succeeded with the expected result.

## Deviations from Plan

None - plan executed exactly as written. The literal `-jq` probe returning 6 instead of the acceptance criterion's expected `0` was investigated and explained (Step 4 in the capture file and Appendix A4 in the durable log) rather than "fixed" — no code or command needed correction, only the interpretation needed to be made explicit, which the plan's own delta-flagging instructions call for.

## Issues Encountered

- The Bash tool's worktree-safety guard rejected a `for`-loop-based multi-call GitHub API command as "too complex to verify it stays inside the worktree," even though it contained no git operations. Resolved by splitting into five separate single-alert `gh api` calls (no functional change, same evidence gathered).
- GitHub's `dependency-graph/sbom` endpoint returned a transient `500 Failed to generate SBOM: Request timed out` on its first invocation this session. An immediate retry succeeded with the expected result (`8.0.3`, one entry). Recorded as an operational footnote in both the capture file and the durable log's appendix, not treated as a material finding.

## User Setup Required

None - no external service configuration required. This plan performed only GitHub API reads and local git/dotnet reads.

## Next Phase Readiness

- `.planning/SECURITY-TRIAGE.md` entry one is complete and ready for plan 73-02 to fill in the Gate (a)/(b) columns and the dismissal timestamps/`dismissed_reason` once the operator approves the drafted comments.
- All five alerts (#17-#21) remain `open` — confirmed by the final phase-gate check (`gh api .../dependabot/alerts -X GET -f state=open --jq 'length'` returns `5`) — so plan 73-02 starts from a clean, unmutated state.
- No blockers. The D-16 gate's two-source absence check (GitHub SBOM + local `dotnet list package`) already passes cleanly for the shared package/manifest footprint across all five alerts, which plan 73-02 can re-run per-alert immediately before each PATCH per D-17.

---
*Phase: 73-resolve-stale-high-security-alerts*
*Completed: 2026-08-26*
