---
phase: 89-pull-based-release-deployment
plan: 08
subsystem: ci-verification
tags: [sigstore, gh-attestation, github-actions, shellcheck, actionlint, zizmor]
requires:
  - phase: 89-02
    provides: dotnet.yml triggers and the migrator-sql job
  - phase: 89-04
    provides: release.yml and the release-workflow-test.sh structural guard
  - phase: 89-05
    provides: deploy/lib/verify.sh trust functions
  - phase: 89-06
    provides: install-flow-test.sh with QUESTBOARD_TEST_PACKAGED_ZIP
provides:
  - Real-network proof that the installer's attestation check refuses tampering and mismatches
  - build/verify-published-release.sh workstation check that ends with the zip sha256
  - deploy-scripts, workflow-lint and network-verify CI jobs
  - Script-existence, SHA-pin, image-digest and self-hosted guards in release-workflow-test.sh
affects: [89-09, 89-10]
tech-stack:
  added: []
  patterns:
    - "Lint tools run only in CI from digest-pinned container images"
    - "Network tests are opt-in via QUESTBOARD_TEST_NETWORK=1 and SKIP otherwise"
key-files:
  created:
    - deploy/tests/verify-rejects-tampered-artifact-network-test.sh
    - deploy/tests/fixtures/public-attested-artifact.env
    - deploy/tests/fixtures/public-attested-artifact.sigstore.jsonl
    - build/verify-published-release.sh
  modified:
    - .github/workflows/dotnet.yml
    - build/tests/release-workflow-test.sh
key-decisions:
  - "verify-published-release.sh falls back to the origin remote when gh repo view cannot resolve the repository, so a workstation with no gh login still works"
  - "PyYAML is installed from the Ubuntu archive in CI only when python3 cannot already import it"
  - "The packaged zip path passed to the install-flow test is absolute"
status: complete
metrics:
  tasks: 2
  commits: 2
actuals:
  tokens: 10000
  tasks: 2
  commits: 2
---

# Phase 89 Plan 08: CI verification loop Summary

Verification is now proven against real attested bytes, the operator has a credential-free
pre-install check that prints the zip's sha256, and CI runs every script test, the packaged
installer flow, pinned lint of the release workflow and the network refusal proof on every PR.

## What was built

- **Network refusal test** (`deploy/tests/verify-rejects-tampered-artifact-network-test.sh`):
  SKIPs unless `QUESTBOARD_TEST_NETWORK=1`. With it, downloads a real public attested `.deb`,
  checks its recorded sha256, and asserts through `questboard_verify_attestation` that the genuine
  artifact passes and reports the recorded source digest, while a one-byte-flipped copy, a wrong
  repository, a wrong signer workflow and a wrong source ref are all refused. A second pass exports
  bogus `GH_TOKEN`/`GITHUB_TOKEN`, puts a logging `gh` wrapper first on PATH, and shows the wrapper
  only ever saw `unset` while the genuine verification still passed. Ran locally against the real
  network: all 9 checks pass.
- **Fixtures**: the `.sigstore.jsonl` bundle is a byte-for-byte copy (`cmp` clean); the `.env` was
  recreated with a plain-language header and the same six values. The user-granted copy exception
  covered both files; the `.env` was authored fresh as the plan specifies, so the reference
  header wording ("offline attestation verification", "the plan's action block") never entered
  the repository and no grep gate tripped.
- **`build/verify-published-release.sh vX.Y.Z`**: strict tag check, repository resolution with
  tokens stripped, credential-free curl download of the zip, `.sha256` and `.sigstore.json`,
  then `questboard_verify_checksum`, `questboard_verify_attestation` (release.yml, `refs/tags/TAG`),
  `questboard_commit_on_branch ... main`, a one-byte-tamper refusal proof, `PASS:`/`FAIL:` per
  step with exit 1 on the first failure, and a final line `sha256 questboard-vX.Y.Z.zip <hex>`.
  `v5.3.3` exits non-zero with a `FAIL:` line (it has a zip but no `.sha256` asset); `v1.2` exits
  non-zero as not a strict tag.
- **CI jobs** in `.github/workflows/dotnet.yml`: `deploy-scripts` (bash -n, pinned shellcheck,
  `run-all.sh`, tag and workflow tests, package a v0.0.0 zip, packaged-zip smoke, install flow with
  `QUESTBOARD_TEST_PACKAGED_ZIP`), `workflow-lint` (pinned actionlint over release.yml and
  dotnet.yml, pinned zizmor `--offline` over release.yml, with a comment on why the hash-pin policy
  covers release.yml only) and `network-verify`. All run on ubuntu-24.04 with `contents: read`,
  SHA-pinned actions, `persist-credentials: false`, no `${{ }}` in `run:`. The `build` and
  `migrator-sql` jobs and the triggers are unchanged.
- **Guard** in `release-workflow-test.sh`: five required dotnet.yml jobs, SHA pins on migrator-sql
  and the new jobs, ubuntu-24.04 plus `contents: read` plus `persist-credentials: false` on the new
  jobs, no `${{` in dotnet.yml runs, every `docker run` image digest-pinned, and every
  `build/`/`deploy/` script named in a `run:` of either workflow must exist (globs skipped).
  The existing no-self-hosted check covers every workflow.

## Verification

- `QUESTBOARD_TEST_NETWORK=1 bash deploy/tests/verify-rejects-tampered-artifact-network-test.sh`: all checks pass; without the variable it prints `SKIP:` and exits 0.
- `bash build/tests/release-workflow-test.sh`: all PASS, including the new assertions.
- `bash deploy/tests/run-all.sh`: 8 passed, 0 failed.
- `bash -n` over all 25 repository shell scripts: clean. shellcheck, actionlint and zizmor were
  not run locally (CI only, by decision); findings are for the plan 10 loop.
- `dotnet test`: 834 unit tests and 952 integration tests passed, 11 skipped (SQL-backed, no server here).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Origin-remote fallback for repository resolution**
- **Found during:** Task 1
- **Issue:** `gh repo view` needs a logged-in gh, but the operator docs say the workstation check needs no login or token.
- **Fix:** If `gh repo view` fails, derive `owner/name` from the `origin` remote and validate it against the repository pattern; a failure of both still fails the step. Tested with a failing gh stub.
- **Files modified:** build/verify-published-release.sh
- **Commit:** 1c00cca9

**2. [Rule 3 - Blocking] PyYAML availability on hosted runners**
- **Found during:** Task 2
- **Issue:** `release-workflow-test.sh` imports PyYAML, which hosted ubuntu-24.04 runners may not provide.
- **Fix:** `deploy-scripts` and `workflow-lint` install `python3-yaml` from the Ubuntu archive only when `import yaml` fails.
- **Files modified:** .github/workflows/dotnet.yml
- **Commit:** b44bc80f

## Interface notes versus the operator docs

`docs/deploy.md` and `docs/releasing.md` describe the script accurately: it downloads the three
assets, verifies the checksum and attestation, checks main ancestry, proves the tamper refusal and
ends with `sha256 questboard-vX.Y.Z.zip <hex>`. The only difference is additive: the script prints
`all checks passed for vX.Y.Z` on the line before the sha256 line, so the sha256 line is last. Both
docs say no login is needed, which now holds thanks to the origin-remote fallback. The docs say the
script needs only `gh`; it also needs `curl`, `python3`, `sha256sum` and a git checkout of this
repository (for the verification functions it sources).

## Known Stubs

None.

## Threat Flags

None. No new network endpoint, auth path or trust-boundary schema beyond the plan's threat model.

## Self-Check: PASSED

All four task-1 files and both task-2 files exist; commits 1c00cca9 and b44bc80f are on the branch.
