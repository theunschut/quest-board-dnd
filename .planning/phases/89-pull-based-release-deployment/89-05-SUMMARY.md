---
phase: 89-pull-based-release-deployment
plan: 05
subsystem: deploy
tags: [bash, sigstore, gh-attestation, systemd-run, release-staging, health-check]

requires:
  - phase: 89-03
    provides: common.sh helpers (questboard_http_fetch, questboard_log, questboard_die, version checks) and the host-guard test harness
  - phase: 89-01
    provides: release artifact contract and migrator CLI (status, backup, apply)
provides:
  - deploy/lib/verify.sh with checksum, pinned attestation, main-ancestry, latest-tag and asset download functions
  - deploy/lib/release.sh with staging, tree hardening, manifest and JSON helpers, migrator invocation, health wait
  - offline PATH-stub tests for both libraries
affects: [89-06, 89-07, 89-08, 89-11]

tech-stack:
  added: []
  patterns:
    - "gh runs under env -u token variables with a private mktemp dir for config, cache and state"
    - "migrator launched through systemd-run with EnvironmentFile so the root installer never reads the secret"
    - "extract into releases/.staging-VERSION, validate, harden, then mv -T into place"

key-files:
  created:
    - deploy/lib/verify.sh
    - deploy/lib/release.sh
    - deploy/tests/verify-logic-test.sh
    - deploy/tests/release-logic-test.sh
  modified: []

key-decisions:
  - "run_migrator returns 64 for a refused argument, outside the migrator's own 0..6 exit codes, so callers can tell the two apart"
  - "secure_tree uses chmod go-w,a+r on files so the application user can always read its code, in addition to stripping group/other write"
  - "stage_release checks for an active release before extracting, failing fast instead of after the unzip"
  - "json_get prints nothing for a missing or null key and only returns 1 for invalid JSON or a non-object; json_list_length counts a missing key as 0"

patterns-established:
  - "Every verification failure is a refusal; no bypass flag, variable or fallback exists, enforced by a grep gate"
  - "Library functions validate repo, ref, asset and path arguments against strict regexes before building URLs or argument lists"

requirements-completed: [MH-4, MH-6]

duration: 35min
completed: 2026-10-04
status: complete
actuals:
  tokens: 13400
  tasks: 2
  commits: 2
---

# Phase 89 Plan 05: Verification and Release Libraries Summary

**Sourced shell libraries that decide release trust (sha256, pinned gh attestation in an isolated environment, main ancestry) and handle release content (hardened staging, systemd-run migrator hand-off, version-confirming health wait), each with offline tests.**

## Performance

- **Tasks:** 2 of 2
- **Files created:** 4
- **Test result:** `deploy/tests/run-all.sh` passes (4 test files: common, deploy logic, verify, release)

## Accomplishments

- `verify.sh`: `questboard_verify_checksum` requires a single-line `.sha256` naming the exact zip; `questboard_verify_attestation` runs `gh attestation verify` with the pinned repo, signer workflow, tag ref and self-hosted-runner denial, with all token variables unset and config, cache and state in a private temp directory removed on every path, printing the 40-hex attested commit; `questboard_commit_on_branch` accepts only compare status `identical` or `behind`; `questboard_fetch_latest_tag` maps 200/404/other to 0/3/1; `questboard_download_asset` maps 200/404/other to 0/2/1 and never leaves a partial file.
- `release.sh`: `questboard_stage_release` checks disk (twice the uncompressed size), entry names (absolute, parent segment, backslash), symlinks, manifest version and `healthVersionHeader`, and required files before hardening and an atomic `mv -T`; `questboard_run_migrator` builds the documented systemd-run property set; `questboard_wait_for_health` requires HTTP 200, a Healthy or Degraded body and, when asked, an exactly matching `X-QuestBoard-Version` header.
- 58 checks in `verify-logic-test.sh` and 109 in `release-logic-test.sh`, all offline.

## Task Commits

1. **Task 1: verification library** - `1112871c` (feat)
2. **Task 2: release staging, migrator invocation and health wait** - `dc1679a5` (feat)

## Deviations from Plan

### Environment-directed

**1. [User decision - sibling repo rule] Reference project files read, not copied**
- The plan cites files in the sibling dashboard repository as references. They were not opened for copying and no code was taken from them; both libraries and both tests were authored from the plan's behavior and action text and the phase research examples.

### Auto-fixed Issues

**2. [Rule 2 - Missing critical functionality] Argument validation beyond the plan text**
- **Issue:** URLs and the systemd-run argument list are built from caller-supplied repo, branch, tag, asset, release-dir and env-file values.
- **Fix:** strict regexes reject anything unexpected (`verify.sh`: repo, ref name, asset and tag; `release.sh`: absolute safe-character paths) before any request or process is started. `run_migrator` additionally refuses a label on non-backup subcommands and a missing label on backup.
- **Files modified:** deploy/lib/verify.sh, deploy/lib/release.sh
- **Commit:** 1112871c, dc1679a5

**3. [Rule 2 - Missing critical functionality] Files stay readable after hardening**
- **Issue:** stripping group/other write alone would leave a zip entry with mode 600 unreadable by the application user.
- **Fix:** `questboard_secure_tree` uses `chmod go-w,a+r` on files.
- **Commit:** dc1679a5

**4. [Design choice] Active-release check moved before extraction**
- The plan lists it after hardening; doing it first refuses the same cases without unpacking and leaves the same end state.

**5. [Test hardening] Raw-name archive fixtures are otherwise complete releases**
- The first draft of the odd-entry fixtures would have been refused for missing files regardless of the entry name. They now carry a complete valid tree, plus a sanity case proving the fixture is accepted with a harmless extra entry, so only the entry name can cause the refusal.

## Plan Behavior Notes

- `stage_release` requires `healthVersionHeader` to be true, per the plan. A release adopted from the pre-existing flat install (manifest `healthVersionHeader: false`) is created by setup rather than staged from a zip, so it never passes through this function.
- The health wait sleeps a real 2 seconds between attempts; the release test suite takes about 15 seconds as a result.
- The libraries are mode 644 like `common.sh` (sourced, not executed); the tests are executable.

## Issues Encountered

None. Real-network behaviour of `verify.sh` and the systemd-run sandbox interplay (`ProtectHome=yes`, bus access from the sandboxed unit) remain assumptions that are proven only on the CT, as the plan states.

## Known Stubs

None.

## Threat Flags

None. All new surface (outbound GitHub calls, systemd-run hand-off, archive extraction) is covered by the plan's threat register.

## Self-Check: PASSED

- deploy/lib/verify.sh, deploy/lib/release.sh, deploy/tests/verify-logic-test.sh, deploy/tests/release-logic-test.sh: present
- Commits 1112871c and dc1679a5: present
- Acceptance greps: `--deny-self-hosted-runners` 1, `XDG_CACHE_HOME` 1, `-u GH_TOKEN` 1, bypass/credential words 0, `EnvironmentFile=` 1, `RestrictAddressFamilies` 1, `RuntimeMaxSec=1800` 1, env-file sourcing 0, planning references 0
