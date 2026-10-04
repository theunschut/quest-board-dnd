---
phase: 89-pull-based-release-deployment
plan: 09
subsystem: docs
tags: [docs, deploy, release, runbook, server-setup]
status: complete

requires:
  - phase: 89-04
    provides: release workflow and build/check-github-settings.sh
  - phase: 89-06
    provides: questboard-deploy dispatcher behaviour
  - phase: 89-07
    provides: setup, poll units, deploy.conf and the SQL-host prune script
provides:
  - "docs/deploy.md: operator guide for the pull-based installer"
  - "docs/releasing.md: cutting, refusals, workstation verification, one-time GitHub settings"
  - "docs/server-setup.md: deploy parts rewritten without the runner"
  - "PROJECT.md deployment line with the correct env path"
affects: [89-10, 89-11, 89-12]

tech-stack:
  added: []
  patterns:
    - "Docs state only behaviour read from deploy/, build/ and the workflow, not the reference project's claims"

key-files:
  created:
    - docs/deploy.md
    - docs/releasing.md
  modified:
    - docs/server-setup.md
    - .planning/PROJECT.md

key-decisions:
  - "Two new docs rather than sections in server-setup.md; server-setup.md keeps CT creation, SQL, Traefik and DNS and links out"
  - "The old runner, its directory, service script, restart permission file and deploy script are mentioned only inside the cutover section of deploy.md"

requirements-completed: [MH-7]

duration: n/a
completed: 2026-10-04

actuals:
  tokens: 10300
  tasks: 2
  commits: 2
---

# Phase 89 Plan 09: Operator documentation Summary

**Three operator documents for the pull-based deploy: a server guide covering every installer subcommand, the outcome matrix with mail and memory columns, manual backup restore, SQL-host pruning, cutover and runner retirement; a release guide with the one-time GitHub settings; and a server-setup rewrite that no longer describes a runner.**

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | docs/deploy.md, operating the pull-based installer | ec98da15 |
| 2 | docs/releasing.md, server-setup rewrite, PROJECT.md path fix | 3640b355 |

## What was written

- **docs/deploy.md** (14 sections in the order the plan lists): how a release arrives, path table, the poll, install order with and without migrations, the verification chain (checksum, attestation pinned to repo, workflow, tag and hosted runners, attested commit on main) with the explicit statement that an unreachable Sigstore service refuses the install, a 12-row outcome table (situation, action, mail, remembered), notification mail, all five commands, why rollback stops, the manual restore runbook, SQL-CT pruning with a cron line, the migrator by hand with its exit codes, the nine config keys with defaults, cutover and runner retirement with two-sided verification and the Data Protection warning, and troubleshooting.
- **docs/releasing.md**: cutting a release, what the workflow refuses, assets, `build/verify-published-release.sh vX.Y.Z`, the `deploy` environment and `v*` tag ruleset `gh api` commands for `theunschut/quest-board-dnd`, `build/check-github-settings.sh`, the GHCR image note, and what to do when a release does not install.
- **docs/server-setup.md**: intro, diagram, `/opt/questboard` now `root:root`, base unit kept with a drop-in note, one "Install the deploy tooling" subsection, a new "Deploying" section, and the poll-unit log line. Sections 2 to 4 are byte-identical (checked with a zero-context diff).
- **PROJECT.md**: `/etc/questboard/env` and a one-sentence description of the pull-based deploy; nothing else changed.

## Verification

- Task 1 verify loop: all required strings present (the `check-github-settings.sh` string is covered by releasing.md; the gate in the plan's loop lists it for deploy.md, which also names it in the retirement section).
- No runner, runner-directory, restart-permission or old-deploy-script reference before the cutover heading; zero `fully offline` or `every poll sends/emails` matches; `halted` appears 9 times; `Data Protection` appears once.
- No planning references in any of the three docs.
- Task 2 gates: zero matches for the runner patterns in server-setup.md and releasing.md; zero `/etc/questboard/.env` and one `/etc/questboard/env` in PROJECT.md; server-setup.md links to both new docs.

## Deviations from Plan

### Auto-fixed Issues

None.

### Notes

**1. Fresh-server paragraph added to deploy.md after Task 1's commit.** Writing the server-setup link ("fresh CT or existing") showed deploy.md only described the adoption path. A short paragraph on a fresh CT (setup does not ask for a version, then `install vX.Y.Z`) went into the Task 2 commit.

**2. `build/verify-published-release.sh` documented from its interface.** The script is written by a parallel plan and did not exist in this worktree. Its steps and final `sha256 questboard-vX.Y.Z.zip <hex>` line are described as the sibling plan specifies; re-read the doc against the merged script.

**3. Migrator command carries `--working-directory`.** The plan's interface string omits it; the installer passes it, so the doc includes it to match real behaviour.

**4. Reference docs read for shape only.** Nothing was copied from the sibling repository; the `gh api` shapes for the environment and ruleset are API facts and match what `check-github-settings.sh` checks.

## Findings for follow-up (code, not docs)

- **A manual rollback does not hold.** `rollback` records only the target tag. The release you rolled back from stays remembered as `installed`, so the next poll sees a newer latest tag that is not remembered as bad and installs it again within minutes. deploy.md documents the workaround (stop the timer until a fixed release exists). A real fix would be to record the abandoned tag with an outcome the poll skips.
- **A Sigstore outage costs the release a mail and a memory.** An unreachable trust service surfaces as `attestation_failed`, which is `refused` and remembered, so the poll will not retry it. deploy.md tells the operator to run `install vX.Y.Z` once the service is back. Distinguishing "cannot verify" from "verified false" would avoid the manual step.

## Known Stubs

None.

## Threat Flags

None. The doc content follows the plan's threat register: runner retirement limits deletion to the runner directory and warns about the home directory (T-89-19), the restore runbook states the lost-writes window (T-89-18), no secrets or real connection strings appear (T-89-36), and the cutover requires workstation verification plus a sha256 comparison on the CT before `setup` (T-89-37).

## Self-Check: PASSED

- docs/deploy.md, docs/releasing.md, docs/server-setup.md and .planning/PROJECT.md exist and are committed.
- Commits ec98da15 and 3640b355 are in the branch history.
