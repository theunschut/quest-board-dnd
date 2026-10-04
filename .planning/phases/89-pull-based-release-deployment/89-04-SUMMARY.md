---
phase: 89-pull-based-release-deployment
plan: 04
subsystem: release-pipeline
tags: [github-actions, attestation, release, bash, supply-chain]
status: complete
requires:
  - phase: 89-01
    provides: build/package-release.sh and package-release-smoke-test.sh
provides:
  - hosted attested release workflow (build, test, package, attest, draft, approved publish)
  - strict release-tag validation
  - static guard over the workflow and every workflow's runs-on
  - read-only checker for deploy environment, tag ruleset and registered runners
affects: [89-05, 89-08, 89-10, 89-12]
tech-stack:
  added: []
  patterns: [every expression reaches shell only through env, actions pinned by full SHA, workflow path is the attestation signer identity]
key-files:
  created:
    - build/validate-release-tag.sh
    - build/tests/validate-release-tag-test.sh
    - .github/workflows/release.yml
    - .github/zizmor.yml
    - build/tests/release-workflow-test.sh
    - build/check-github-settings.sh
  modified:
    - README.md
  deleted:
    - .github/workflows/binary-release.yml
key-decisions:
  - "New release.yml rather than rewriting binary-release.yml, so the attestation signer path is a fresh stable name"
  - "Publish job takes the version from the tag (questboard-$TAG.zip) instead of a job output, since the asset names embed the v prefix"
duration: n/a
completed: 2026-10-04
actuals:
  tokens: 5000
  tasks: 3
  commits: 3
---

# Phase 89 Plan 04: Attested hosted release workflow Summary

A tag push now runs a hosted ubuntu-24.04 workflow that validates the tag, builds, runs the full test suite (SQL migrator tests required against an mssql service container), packages, attests with build provenance and drafts a release. A `deploy`-environment approval re-downloads the draft, re-checks the checksum and the attestation, then publishes. The old self-hosted push deploy is gone.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | Strict release-tag validation | 793edd99 |
| 2 | release.yml replaces the push-based workflow | eedf1921 |
| 3 | Read-only GitHub settings checker | 00803aaf |

## Verification

- `bash build/tests/validate-release-tag-test.sh`: all cases pass (lightweight and annotated tags, every rejection, SHA mismatch).
- `bash build/tests/release-workflow-test.sh`: all assertions pass, including that no workflow targets a self-hosted runner.
- `docker-publish.yml`, `Dockerfile` and `docker-compose.yml` are unchanged; `binary-release.yml` is deleted; README Release badge points at `theunschut/quest-board-dnd` `release.yml`; the other two badges are untouched.
- `check-github-settings.sh` passes `bash -n`, is executable, uses no write verbs and no system jq. It was not run: it needs the owner's gh login and the settings do not exist yet (expected to fail until the operator handover).
- actionlint and zizmor were not run locally (CI runs them in a later plan).

## Deviations from Plan

### Process deviation: files authored from the plan spec, not copied

The plan named ing-dashboard files as port sources. The attempt to copy the validator and its test from that repository was blocked by the permission system, and the user then chose "resume, write from spec". All six new files were therefore authored from the plan's behaviour and action text, with the sibling repository used at most as a reference. Behaviour matches the plan; code structure differs from the sibling in places (for example the test collects failures rather than exiting on the first).

### Auto-fixed

**1. [Rule 1 - Bug] Trailing-newline test case passed for the wrong reason**
- **Found during:** Task 1
- **Issue:** the case passed literal `$'v1.2.3\n'` text rather than a real newline.
- **Fix:** build the value in a variable with `$'v1.2.3\n'` first.
- **Commit:** 793edd99

**2. [Rule 1 - Bug] README badge image URL**
- **Found during:** Task 2
- **Issue:** the first sed pass left the badge image on the old `quest-board` repository path.
- **Fix:** both badge URLs now use `theunschut/quest-board-dnd`.
- **Commit:** eedf1921

### Notes

- The publish job's checksum and verification steps use `github.ref_name` for the asset name, since the assets are named `questboard-vX.Y.Z.zip` and the tag is `vX.Y.Z`. No `needs.build.outputs.version` is consumed there; the output stays declared on the build job.
- The workflow invokes `deploy/tests/run-all.sh`, `deploy/tests/install-flow-test.sh` and `deploy/tests/verify-rejects-tampered-artifact-network-test.sh`, which later plans create. They do not exist in this worktree yet, so a release must not be cut before those plans land.

## Known Stubs

None.

## Threat Flags

None beyond the plan's threat model.

## Self-Check: PASSED

All six created files exist, `binary-release.yml` is absent, and commits 793edd99, eedf1921 and 00803aaf are in the branch history.
