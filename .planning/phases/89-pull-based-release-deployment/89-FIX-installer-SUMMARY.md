# Phase 89 Ad-hoc Fix: Installer rollback stickiness and verification outage Summary

Two installer defects found during documentation, fixed with offline test coverage.

## Fix 1: manual rollback now sticks

- `cmd_rollback` records the release rolled back FROM as `abandoned` (then the target as
  `rolled_back_manual`, so the target stays the last attempts line) once the rollback is healthy.
- `abandoned` is added to the accepted outcomes in `questboard_record_attempt` and to the skip list in
  `questboard_remembered_outcome`. Polls log `skipping vA: abandoned earlier; run questboard-deploy
  install vA to try it again`. It is not in the mail vocabulary because it is never mailed.
- Not recorded when: rollback is refused or fails (unknown migrations, pending, adopted target, not on
  disk, already active, health failure), or the active release is not a plain version.
- `install vA` records `installed` and clears the memory; a newer tag is not blocked.
- Tests: `install-flow-test.sh` (rollback then poll skips, install by hand, newer tag installs,
  refused/unhealthy rollbacks record nothing), `questboard-deploy-logic-test.sh` (outcome vocabulary).
- Docs: `docs/deploy.md` (rollback command, outcomes table, troubleshooting); the stop-the-timer
  workaround is removed.
- Commit: 816223bd

## Fix 2: verification outage is retried, not refused

- `questboard_verify_attestation` keeps its arguments and returns 1 for any rejection. It returns the
  new `QUESTBOARD_VERIFY_UNREACHABLE` (3) only when `gh` failed AND `questboard_verification_services_unreachable`
  then found a transport failure (no answer at all, any HTTP status counts as reachable) reaching
  tuf-repo-cdn.sigstore.dev, tuf-repo.github.com or api.github.com. Endpoints are fixed constants, no
  credential is sent. Successful `gh` never probes; a tampered artifact with the network up yields 1.
- `cmd_install`: status 3 removes the download, logs and exits 0 under poll, or dies with
  `verification services unreachable; nothing changed, try again later` on a manual install. No
  staging, no app stop, no attempt recorded, no mail. `cmd_verify` dies with a clear unreachable message.
- Interface change: new return value 3 and two new functions/constants in `deploy/lib/verify.sh`. The
  outcome matrix is unchanged (no row needed; unknown input still exits 2).
- Tests: `verify-logic-test.sh` (return codes per reachability case, each endpoint down alone, no
  probe on success, no credential), `install-flow-test.sh` (outage install/poll retry, next poll installs,
  real rejection still refused and remembered).
- Docs: `docs/deploy.md` (verification, outcomes, troubleshooting), `docs/releasing.md` (outage is not a rejection).
- Commit: d011be97

## Coordinator addition

- `docs/releasing.md`: workstation verification prerequisites (`gh`, `curl`, `python3`, `sha256sum`, run from
  a checkout) and the `all checks passed` line. Commit: 6c0f93aa

## Verification

`bash deploy/tests/run-all.sh`: 8 passed, 0 failed; `bash -n` clean on all touched scripts.
The network test file and the other files owned by the sibling executor were not touched.
