---
phase: 89-pull-based-release-deployment
plan: 06
subsystem: deploy
tags: [bash, installer, flock, outcome-matrix, rollback, systemd]
status: complete

requires:
  - phase: 89-01
    provides: packaged release contract (zip, manifest, migrator, deploy/ tree)
  - phase: 89-03
    provides: common.sh, deploy.sh decision/bookkeeping functions, host-guard test harness
  - phase: 89-05
    provides: verify.sh and release.sh libraries wired together here
provides:
  - deploy/bin/questboard-deploy dispatcher (poll, install, rollback, verify, setup)
  - install order with and without pending migrations and the full outcome matrix
  - remember-and-skip polling and one-mail-per-outcome reporting
  - manual rollback guarded by the target's own migrator status
  - offline end-to-end flow test, including an optional real packaged-zip run
affects: [89-07, 89-08, 89-11]

tech-stack:
  added: []
  patterns:
    - "single finish() exit point records, mails once, logs one line, cleans downloads"
    - "every failure path asks questboard_decide_outcome; the tested table is the only source of outcome names"
    - "EXIT trap restarts the app if the script dies between stop and start"
    - "flow test stubs curl/gh/systemctl/systemd-run and tags each logged call with the release the current link names, so switch order is provable"

key-files:
  created:
    - deploy/bin/questboard-deploy
    - deploy/tests/install-flow-test.sh
  modified:
    - .gitignore

key-decisions:
  - "Manual rollback sends no mail; it prints, logs and records rolled_back_manual"
  - "Rollback to the adopted release (adopted manifest or no migrator) is refused with a pointer to docs/deploy.md"
  - "Install of the active tag is a restart plus health check with no download; an older tag dies with 'use rollback' and writes nothing"
  - "Download transport failure is quiet (exit 0, no mail, not remembered) only when invoked by poll; by hand it is an error"
  - "A release found unhealthy and switched back is removed from disk, as are refused and apply-failed ones"
  - "finish exits 1 for every non-installed outcome, so a poll that refuses a tag shows as a failed timer run once"

patterns-established:
  - "Verification order download -> checksum -> attestation -> main ancestry before staging, no bypass; grep gate proves no skip flag exists"
  - "Configuration values are assigned unconditionally before the validated loader runs, so environment variables never reach the installer"

requirements-completed: [MH-4, MH-6]

duration: n/a
completed: 2026-10-04

actuals:
  tokens: 12000
  tasks: 2
  commits: 2
---

# Phase 89 Plan 06: Root installer Summary

**`questboard-deploy` wires the verified libraries into poll/install/rollback/verify/setup, with the decided migration-aware install order, every outcome-matrix row, remember-and-skip and one mail per outcome, proven by 181 offline flow checks plus a run of a real packaged zip through its own shipped installer.**

## Tasks

| Task | Name | Commit | Type |
| ---- | ---- | ------ | ---- |
| 1 | Dispatcher with poll, install, verify and the full outcome matrix | 3165ab76 | auto (tdd) |
| 2 | Manual rollback, redeploy, installer-update notice and packaged-zip run | e27aa431 | auto (tdd) |

## Accomplishments

- Install order: status, backup, stop, apply, switch, start, health when migrations are pending; status, stop, switch, start, health otherwise. The flow test records each host call together with the release the `current` link names at that moment, so "apply before switch" and "switch between stop and start" are asserted, not assumed.
- Outcome matrix rows proven: checksum mismatch, attestation failure, commit off main (diverged and unreachable), missing bundle or zip asset, database ahead (status 2), non-transactional (3), database unreachable (4), backup failure (5, app never stopped), apply failure (6, previous release restarted and confirmed healthy), unhealthy without migration (switched back, previous confirmed healthy, earlier `previous` value restored), unhealthy after apply (stays active, halted, backup name in the mail, no further stop).
- Poll: idle, unreachable, no release, remembered, newer-than-remembered, download transport failure; none of the quiet paths mail or record. An explicit install retries a remembered tag.
- Lock: a second invocation while the flock is held exits non-zero and touches nothing.
- Manual rollback: succeeds only to an on-disk release with its own migrator whose status shows nothing unknown and nothing pending; adopted, missing, active and malformed targets are refused; no mail.
- Installer-update notice appears only when shipped deploy files differ from the installed copies (a missing installed file counts), and installed files are verified byte-identical after the install.
- Packaged zip: built with `build/package-release.sh --version 0.0.2`, extracted `deploy/bin` and `deploy/lib` from that zip ran the install into a temp root (current names 0.0.2, one installed mail). Without `QUESTBOARD_TEST_PACKAGED_ZIP` the case prints `SKIP: packaged zip not supplied` and passes.
- `.gitignore` re-includes `/deploy/bin/`; project `bin/` folders stay ignored.

## Deviations from Plan

### Environment-directed

**1. [User decision - sibling repo rule] Reference project files not copied**
- The plan names the sibling dashboard repository's installer and library as port sources. They were not opened for copying and no code was taken from them; the dispatcher and the flow test were authored from the plan's behavior and action text, the phase libraries and their tests.

### Auto-fixed Issues

**2. [Rule 2 - Missing critical functionality] Exit trap restarts a stopped application**
- **Issue:** with `set -e`, an unexpected failure between `systemctl stop` and `systemctl start` would leave the board down.
- **Fix:** `app_stop`/`app_start` track state and an EXIT trap starts the service again if the script dies while it is stopped. It never fires on the tested paths.
- **Files modified:** deploy/bin/questboard-deploy
- **Commit:** 3165ab76

**3. [Rule 2 - Missing critical functionality] A failed stop is an outcome, not a crash**
- **Issue:** `systemctl stop` failing would abort under `set -e` with nothing recorded or mailed.
- **Fix:** it starts the service again, removes the staged release and finishes `failed restart_failed`. This branch is outside the decided matrix and is not covered by a flow test.
- **Commit:** 3165ab76

**4. [Design choice] Mail composition guarded on a configured recipient**
- `finish` only renders and hands off a message when `QUESTBOARD_NOTIFY_EMAIL` is set (the renderer rejects an empty recipient). The attempt is still recorded and logged.

**5. [Design choice] Rollback code landed with Task 1**
- The whole dispatcher, including rollback and redeploy, was written in one pass and committed with Task 1; Task 2's commit carries the tests that prove those paths.

## Plan Behavior Notes

- Redeploy does not add the installer-update notice; the plan scopes that to the healthy install path.
- If a manual rollback's health check fails the command exits non-zero with the target left active and records nothing. Switching forward again is safe in that case (nothing unknown or pending was verified) but is left to the operator.
- The flow test takes about 27 s because the health wait sleeps a real 2 s between attempts and four cases wait out the 10 s minimum timeout.
- The verification command in the plan runs `build/package-release.sh --commit "$(git rev-parse HEAD)"`; the sandbox refused that construct, so the same packaging was run with a fixed 40-hex commit. The commit value only lands in the manifest and does not change what is installed.

## Issues Encountered

None. Real-host behaviour (systemd-run sandboxing, sudo-less root run, real Sigstore and GitHub calls, the shared lock under `/run`) is proven only on the CT, as in earlier plans.

## Known Stubs

None.

## Threat Flags

None. All new surface (root installer acting on GitHub input, service stop/start, operator overrides) is in the plan's threat register; T-89-01, -03, -07, -08, -09, -30 and -31 are exercised by the flow test.

## Self-Check: PASSED

- deploy/bin/questboard-deploy: found, executable, tracked (not ignored)
- deploy/tests/install-flow-test.sh: found, `deploy/tests/run-all.sh` 5 passed 0 failed
- commits 3165ab76 and e27aa431 present
- acceptance greps: one non-comment `questboard_send_mail`, zero bypass wording, zero planning references, `rolled_back_manual` and `docs/deploy.md` present
