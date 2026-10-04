---
phase: 89-pull-based-release-deployment
plan: 03
subsystem: deploy-installer-core
tags: [bash, installer, outcome-matrix, config-loader, mail, activation, pruning]
status: complete

requires:
  - "89-01: release layout (releases/X.Y.Z, current symlink, release-manifest.json) and the systemd drop-in"
provides:
  - "deploy/lib/common.sh: logging, allow-list config loader, tag and semver checks, HTTP fetch helper, secret-free CRLF outcome mail sent by curl SMTP"
  - "deploy/lib/deploy.sh: questboard_decide_outcome (all twelve matrix rows), remember-and-skip attempt state, atomic activation, pruning, installer-update detection"
  - "deploy/tests: host-guard, run-all runner and two logic test files"
affects: [89-05, 89-06, 89-07]

tech-stack:
  added: []
  patterns:
    - "Config is parsed line by line against a key allow-list and per-key regex, assigned with printf -v, never run as shell code"
    - "Decision logic as pure case-table functions, tested row by row, so orchestration only wires them"
    - "Offline tests in a temp root with PATH stubs for host commands and curl"

key-files:
  created:
    - deploy/lib/common.sh
    - deploy/lib/deploy.sh
    - deploy/tests/lib/host-guard.sh
    - deploy/tests/run-all.sh
    - deploy/tests/common-logic-test.sh
    - deploy/tests/questboard-deploy-logic-test.sh
  modified: []

key-decisions:
  - "load_conf dies on any non-blank, non-comment line that is not an allow-listed KEY=VALUE (the reference loader silently ignored unknown lines)"
  - "Mode check looks only at the group and other write bits, using the last three mode digits"
  - "activate_release does not overwrite the recorded previous release when re-activating the already active one"
  - "prune only considers directories named like a plain version, leaving foreign directories and symlinks alone"
  - "render_mail prints the Backup line only for a halted outcome and ignores a supplied backup name otherwise"

requirements-completed: [MH-4, MH-6]

duration: n/a
completed: 2026-10-04

actuals:
  tokens: 13200
  tasks: 2
  commits: 2
---

# Phase 89 Plan 03: Installer core library and logic tests Summary

**The installer's decision-making core now exists as root-free, network-free bash: an allow-list config loader that never runs the file, closed-vocabulary CRLF outcome mail over curl SMTP, the full outcome matrix as one pure function, remember-and-skip state, atomic activation, safe pruning and installer-update detection, all proven by 103 plus 67 offline checks.**

## Tasks

| Task | Name | Commit | Type |
| ---- | ---- | ------ | ---- |
| 1 | Common library, offline harness and its logic tests | 748c54a3 | auto (tdd) |
| 2 | Outcome decision, state memory, activation, pruning and update detection | 61cdf03c | auto (tdd) |

### Task 1

- `common.sh` carries the load guard and the `questboard_` functions from the plan's interface list. No metrics code, no config sourcing, no dynamic evaluation, no local mail agent.
- `questboard_load_conf` checks owner (uid 0, or the current uid under `QUESTBOARD_DEPLOY_ROOT`), refuses group- or other-writable files, validates every value against its key's pattern (plus a 1-65535 port range and a six-digit cap before numeric comparison) and dies on anything unexpected. Tests prove a `$(touch ...)` value and a backtick value are rejected and never run.
- `questboard_render_mail` validates every input, emits CRLF on every line with ASCII headers, and builds the body only from version, result (label plus closed-vocabulary reason), UTC timestamps, a regex-checked backup name (halted only), previous-release health and the installer-update line. Tests assert no body line contains `/`, `=`, `Server`, `Password` or `Data Source`.
- `questboard_send_mail` uses `curl --url smtp://HOST:PORT/questboard-deploy` and always returns 0; an empty recipient makes no curl call and logs one line, a failing curl logs one line.
- `host-guard.sh` stubs `systemctl systemd-run pkexec apt-get`; `run-all.sh` runs every `*-test.sh` except `*-network-test.sh` from any cwd.

### Task 2

- `questboard_decide_outcome` is a `case` table over `STAGE:RESULT` with the migrated/previous split on the health-failure row; unknown stage, result or migrated flag returns 2. Both rationale comments (halted leaves the new release active; apply failure restarts the previous release) are in the function.
- `record_attempt` validates tag and outcome tokens before appending `TAG OUTCOME UTC`; `remembered_outcome` reads the latest line per tag with `awk` and `ENVIRON` (no escape processing) and prints it only for the five remembered-bad outcomes.
- `activate_release` records the previous version, then `ln -s` to a `current.XXXXXX` temp name and `mv -T` over the link. `prune_releases` uses `sort -V` and spares the active and previous releases. Tests include an adopted-style release in the set for activation, rollback and pruning.
- `deploy_files_differ` compares the dispatcher, library files, the two poll units and the drop-in with `cmp -s`, returns 0 on the first difference or missing installed file, 1 when all match or the release has no `deploy/` directory, and never copies anything.

## Verification

- `bash deploy/tests/common-logic-test.sh`: 103 checks passed, no `FAIL:` lines.
- `bash deploy/tests/questboard-deploy-logic-test.sh`: all checks passed, no `FAIL:` lines.
- `bash deploy/tests/run-all.sh`: 2 passed, 0 failed.
- Acceptance greps: config-source pattern 0, dynamic-evaluation word 0, metrics/prometheus/old-mail-agent words 0, `smtp://` present, `mv -T` present (2), exactly 8 `questboard_*()` definitions in `deploy.sh`, planning-reference and old-project-name grep over `deploy/lib` and `deploy/tests` empty.
- No test touched the host: the guard log of recorded `systemctl`/`systemd-run`/`pkexec`/`apt-get` calls is asserted empty.

## Deviations from Plan

None - plan executed as written. TDD note: each task's tests and implementation were committed together as one `feat(...)` commit rather than a separate RED commit, because the harness and function contracts were designed together; the tests were run against the implementation and a first-run test bug (a mutated expectation in the "never rewrites installed files" check) was fixed in the test itself before commit.

## Authentication Gates

None.

## Known Stubs

None.

## Threat Flags

None. The surfaces added (config file to root process, outcome mail, state file, symlink switch) are those the plan's threat model already covers.

## Self-Check: PASSED

- Files exist: deploy/lib/common.sh, deploy/lib/deploy.sh, deploy/tests/lib/host-guard.sh, deploy/tests/run-all.sh, deploy/tests/common-logic-test.sh, deploy/tests/questboard-deploy-logic-test.sh.
- Commits 748c54a3 and 61cdf03c present in `git log`.
