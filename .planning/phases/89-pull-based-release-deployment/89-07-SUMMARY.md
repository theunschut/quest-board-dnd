---
phase: 89-pull-based-release-deployment
plan: 07
subsystem: deploy-server-install
tags: [bash, systemd, setup, adoption, gh-install, backup-retention]
status: complete

requires:
  - "89-01: versioned release layout and the questboard.service drop-in"
  - "89-03: common.sh and deploy.sh (config loader, semver, activation, attempts)"
  - "89-05: release.sh (secure_tree, wait_for_health, manifest_get, migrator property set)"
provides:
  - "deploy/systemd/questboard-deploy-poll.{service,timer}: sandboxed root oneshot and its cadence"
  - "deploy/deploy.conf.example: config template matching the loader's allow-list"
  - "deploy/lib/setup.sh: questboard_setup_main, install_gh, install_files, detect_flat_version, active_key_fingerprints, adopt"
  - "deploy/sql-ct/prune-premigration-backups.sh: SQL-host retention for pre-migration backups"
  - "deploy/tests: sandboxing-test.sh, setup-logic-test.sh, prune-premigration-backups-test.sh"
affects: [89-06, 89-11, 89-12]

tech-stack:
  added: []
  patterns:
    - "Decide the layout action (and refuse) read-only before any host change, so an unconfirmed adoption leaves the server exactly as it was"
    - "Resumable move: everything but the main assembly first, assembly last; an adopted manifest marks an unfinished adoption"
    - "Offline tests with recording PATH stubs, including a gh stub that is absent until the apt stub installs it"

key-files:
  created:
    - deploy/systemd/questboard-deploy-poll.service
    - deploy/systemd/questboard-deploy-poll.timer
    - deploy/deploy.conf.example
    - deploy/lib/setup.sh
    - deploy/sql-ct/prune-premigration-backups.sh
    - deploy/tests/sandboxing-test.sh
    - deploy/tests/setup-logic-test.sh
    - deploy/tests/prune-premigration-backups-test.sh
  modified: []

key-decisions:
  - "setup decides the layout action before gh install or any file copy; an unconfirmed or mismatched version exits 2 having changed nothing at all (stricter than the plan, which only required /opt untouched)"
  - "The main assembly is moved last and the adopted manifest is written first, so a run cut off at any point resumes; a cut-off after the last move resumes without asking again because the earlier run was already confirmed"
  - "/opt/questboard and releases are made root-owned 755 by a separate idempotent step that runs for every layout, not inside install_files, so the app user keeps access until the app is stopped"
  - "Health is only re-checked when setup started or restarted the app; a no-change rerun does not touch the app"
  - "Prune script treats an explicitly empty KEEP as invalid instead of silently using the default"

requirements-completed: [MH-4, MH-5, MH-6]

duration: n/a
completed: 2026-10-04

actuals:
  tokens: 21000
  tasks: 3
  commits: 3
---

# Phase 89 Plan 07: Server install surface Summary

**Sandboxed poll unit and timer, the config template, a re-runnable `setup` that installs the pinned gh, the installer and its units and adopts the running flat install only after the operator repeats its version, and a SQL-host retention script, all proven by three offline test files (99 checks for setup alone) with no host access.**

## Tasks

| Task | Name | Commit | Type |
| ---- | ---- | ------ | ---- |
| 1 | Poll unit, timer, config template and static sandboxing test | 2dc53a1e | auto |
| 2 | setup: install, adopt the flat install, enable the timer | 68581469 | auto (tdd) |
| 3 | SQL-host retention for pre-migration backups | 5bb7850c | auto (tdd) |

### Task 1
- Service: root oneshot running `/usr/local/sbin/questboard-deploy poll`, the full hardening set, `ReadWritePaths=/opt/questboard /var/lib/questboard-deploy`, and a comment on why capabilities and address families are not restricted.
- Timer: `OnBootSec=2min`, `OnUnitActiveSec=5min`, `RandomizedDelaySec=30`.
- `deploy.conf.example` has the nine allow-listed keys; the recipient is the `operator@example.com` placeholder. The test loads a mode-600 copy through the real loader and pins the migrator's `--property=` set in `release.sh`, plus the drop-in shape and that no installer file sources an env or deploy.conf path (files that do not exist yet in this worktree are skipped).

### Task 2
- `questboard_setup_main` exit codes: 0 complete, 2 awaiting adoption confirmation, 3 awaiting a deploy.conf edit, 1 error.
- gh is installed only when the keyring's single active (not expired, revoked, invalid or disabled) primary key equals the pin; floor 2.49.0 re-checked after install; no jq.
- Adoption: stop app, create `releases/X/app`, write the adopted manifest with python3, move entries (assembly last), secure the tree, lock down `/opt/questboard` and `releases`, activate, record `vX adopted`.
- 99 checks: key filtering, version detection, every gh path, every setup scenario in the plan's behavior list (flat without and with wrong and right confirmation, rerun after the recipient edit, changed drop-in, two interruption points, fresh host, missing base unit, missing dotnet, bad source, bad argument, existing config).

### Task 3
- `find ... -printf '%T@ %f\0'`, NUL-sorted newest first, removes beyond KEEP, prints `removed NAME`. KEEP must match `^[1-9][0-9]*$`; DIR must exist. Only regular files named `questboard-premigration-*.bak` are candidates.

## Verification

- `bash deploy/tests/run-all.sh`: 7 passed, 0 failed (includes the three new tests).
- Acceptance greps from the plan: pin appears once in setup.sh, no `jq`/runner/sudoers references, no planning references in any new file, prune script executable.

## Deviations from Plan

### Auto-fixed Issues

None needed beyond the items below.

### Process deviations

**1. [Instruction] Reference files authored, not copied**
- The plan cites ing-dashboard files as port sources. Per the orchestrator's instruction every file was written from the plan's behavior text; that repository was not read for content or copied.

**2. [Ordering] Layout decision moved ahead of gh install and file copy**
- The plan listed gh install and file install before the layout step. Setup now reads the layout and handles the confirmation refusal first, so exit 2 changes nothing on the machine (not even apt sources, the drop-in or deploy.conf). Reason: a drop-in on disk whose layout is not in place yet is a hazard if anything reloads systemd and restarts the app in between. All plan behaviors still hold.

**3. [Rule 2 - Missing critical functionality] Resume path for a run cut off after the last move**
- If the assembly has moved but `current` does not exist, a naive layout check would call the host fresh. An adopted-marked release with an app and no `current` is now recognised and finished.

**4. [Rule 1 - Bug] Empty KEEP in prune script**
- `${2:-5}` silently turned an explicit empty KEEP into the default; changed to `${2-5}` so it is refused. Found by the test's refusal loop.

**5. [TDD] Tests and implementation committed together**
- Tasks 2 and 3 are marked tdd but each landed as a single commit containing code and its tests rather than separate test/feat commits.

## Auth gates
None.

## Known Stubs
None. The dispatcher `deploy/bin/questboard-deploy` is written by a parallel plan; the setup test uses a stand-in for it and `setup` only copies it.

## Notes for the integration step
- `setup.sh` assumes the dispatcher globals are fully resolved paths under `DEPLOY_ROOT` (`RELEASES_DIR`, `CURRENT_LINK`, `STATE_DIR`, `DOWNLOAD_DIR`, `CONF_PATH`) and sets `APP_SERVICE`; installed library and binary destinations are built from `DEPLOY_ROOT` directly, not from `LIB_DIR`, because `LIB_DIR` may point at an unpacked release during setup.
- `sandboxing-test.sh` skips `deploy/bin/*` when absent; after the wave merges it will also scan the real dispatcher.

## Threat Flags
None beyond the plan's threat register.

## Self-Check: PASSED

- Files present: poll service, timer, deploy.conf.example, setup.sh, prune script, three test files (verified by `git status` clean after commits and by the passing run-all).
- Commits present: 2dc53a1e, 68581469, 5bb7850c.
