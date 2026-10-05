---
phase: 89-pull-based-release-deployment
plan: 10
subsystem: release
tags: [ci, github-settings, release, attestation, handover]
requires: [89-08, 89-09]
provides: [hosted-ci-proof, deploy-environment, tag-ruleset, published-release-v5.4.0]
key-files:
  created:
    - .planning/phases/89-pull-based-release-deployment/89-10-TASK2-NOTES.md
  modified:
    - docs/releasing.md
    - deploy/lib/common.sh
    - deploy/lib/deploy.sh
    - deploy/lib/setup.sh
    - deploy/bin/questboard-deploy
    - build/tests/package-release-smoke-test.sh
duration: operator handover session
completed: 2026-10-04
---

# Phase 89 Plan 10: Hosted proof and first attested release Summary

Hosted CI is green with the real-SQL atomicity tests executed, the GitHub-side gates exist, and
**v5.4.0** is published, attested and latest.

## Task 1 — push, PR, GitHub gates (operator checkpoint, actions run by the orchestrator on the operator's instruction)

- Push was first declined by GitHub email privacy. With the operator's approval the 59 unpushed
  commits were re-authored to `14213103+cryptic96@users.noreply.github.com` (tree byte-identical,
  merges preserved; local backup branch `backup/pre-email-rewrite-89`), and the repo-local
  `user.email` set to the same address. Commit hashes quoted in 89-01..89-09 summaries predate
  this rewrite and no longer resolve on the branch.
- PR theunschut/quest-board-dnd#155 opened from `milestone/v9-rolling-improvements`.
- `deploy` environment created: required reviewer `cryptic96`, `prevent_self_review=false`,
  custom tag policy `v*.*.*`, no branch policy.
- Tag ruleset `release tags` (id 24469258): active, `refs/tags/v*`, creation/update/deletion,
  bypass RepositoryRole 5. The documented form-field command failed with HTTP 422
  (`conditions.ref_name.exclude` is required); created via JSON and `docs/releasing.md` fixed.

## Task 2 — hosted CI and settings verification

- First hosted run (37235279978): build, migrator-sql, workflow-lint, network-verify green;
  deploy-scripts failed at shellcheck (SC2174 ×3, SC2034 ×5, SC1007, SC2148). Fixed at cause
  (new `questboard_make_dir`, explicit mail relay arguments, shell directive, test cleanups); no
  gate weakened. Details in 89-10-TASK2-NOTES.md.
- migrator-sql (job 111533028401) ran in required mode against the SQL Server service container:
  Total 11, Passed 11, none skipped.
- Re-run on 95e72ee8 (37236241131): all 9 checks green.
- `build/check-github-settings.sh`: 3 PASS, exactly 1 FAIL — the still-registered self-hosted
  runner (retired in 89-12).

## Task 3 — merge, tag, publish

- PR #155 merged (merge commit `358ff71f`); `binary-release.yml` no longer on main.
- `v5.4.0` tagged on `358ff71f` (validated with `build/validate-release-tag.sh`), pushed under the
  tag ruleset's admin bypass. Release run 37237203725: build green, `deploy` approved by the
  operator, publish green. Release is latest with exactly `questboard-v5.4.0.zip`,
  `.zip.sha256`, `.zip.sigstore.json`.
- Independent workstation check `build/verify-published-release.sh v5.4.0`: all checks passed,
  attested commit `358ff71f` on main, tampered copy refused.
  `sha256 questboard-v5.4.0.zip 00a5ef36bee96765895020ab0af63b93c3ec220c320486071e0d6ce778f0e57b`

## Deviations

- **Flaky smoke check on main (Rule 1):** after the merge, main's deploy-scripts failed in the
  packaged-zip smoke test: `printf | grep -q` under `pipefail` reported a present entry missing
  (SIGPIPE race). Fixed with a here-string; the same pattern in setup's `gh --version | head -n 1`
  was fixed too. Merged via PR #156 (`66ea5a6f`). v5.4.0 was built before the fix and still ships
  the old setup probe; its worst case is a needless gh reinstall. The operator chose to publish
  v5.4.0 as built rather than re-tag; the fix ships with the next release.
- Main's merged branch is auto-deleted by the repository; the milestone branch was fast-forwarded
  to main and re-pushed after each merge.

## Self-Check: PASSED
