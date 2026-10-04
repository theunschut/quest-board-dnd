# 89-10 Task 2 notes: hosted CI run and GitHub settings

Hosted run: https://github.com/theunschut/quest-board-dnd/actions/runs/37235279978 (head b1eb48c2, PR 155).
These notes are working notes for Task 3 and the plan SUMMARY; they are not the SUMMARY.

## Hosted run outcome

| Job | Id | Result |
| --- | -- | ------ |
| build | 111533028381 | success |
| migrator-sql | 111533028401 | success |
| workflow-lint | 111533028198 | success |
| network-verify | 111533028358 | success |
| deploy-scripts | 111533028414 | failure (step 6, shellcheck) |

In deploy-scripts, steps 1-5 passed (checkout, .NET, PyYAML, `bash -n` on every script). Steps 7-12 were
skipped because shellcheck failed first: offline deploy script tests, release tag validation test,
release workflow structure test, package a release zip, smoke test, install flow against the zip.

## migrator-sql evidence (job 111533028401)

The migrator tests ran against the real SQL Server service container, in required mode, and were not
skipped. `dotnet test` summary from the job log:

    Test Run Successful.
    Total tests: 11
         Passed: 11

No Failed or Skipped line is printed in the summary (so 0 and 0). All eleven are
`QuestBoard.IntegrationTests.Migrator.MigrationRunnerSqlServerTests.*` and each shows `Passed`, including
the atomic-apply, rollback, ahead-of-build refusal, backup (copy-only, documented name, missing database)
and `GetStatus_ForTheServerLogin_ReportsCanBackup` cases. The only "Failed" text in the log is SQL Server's
own container log ("Login failed for user 'sa'. Reason: Failed to open the explicitly specified database"),
emitted by the tests that deliberately probe a database that does not exist yet; it is expected noise from
the "Stop containers" step, not a test failure.

## workflow-lint and network-verify

Both green on the run: actionlint, zizmor, PyYAML guard and the release workflow structure test passed in
workflow-lint; the tampered/mismatched artifact refusal test passed in network-verify.

## check-github-settings.sh (operator's gh login)

    PASS: v* tag ruleset is active, guards creation, update and deletion, and only the admin role can bypass it
    PASS: deploy environment requires at least one reviewer
    PASS: deploy environment uses a v*.*.* tag deployment policy and no branch policy
    FAIL: no self-hosted runner is registered (1 registered)

Exactly one FAIL, about the registered runner, as expected until the runner is removed in the operator step.

## shellcheck findings and their real causes

The pinned shellcheck image is not available locally and was not pulled, so every fix was made by reading
the code, then checked with `bash -n` and the full offline suites.

| Finding | Cause | Fix |
| ------- | ----- | --- |
| SC2034 `QUESTBOARD_SMTP_HOST`/`PORT` in questboard-deploy | The dispatcher set them only so the sourced library could read them as hidden globals inside `questboard_send_mail`. | `questboard_send_mail` now takes `MSGFILE TO FROM HOST PORT`; the dispatcher passes all four values. The coupling is explicit and shellcheck sees the use. |
| SC2174 deploy.sh (x2), setup.sh | `mkdir -p -m 700` applies the mode to the leaf only, ignores the parents' mode, and does not fix a directory that already exists. | New `questboard_make_dir MODE DIR...` in common.sh: parents are created first with umask 022 (so always 755), then `install -d -m MODE` sets the leaf, tightening an existing directory too. deploy.sh and setup.sh use it. |
| SC1007 release-logic-test.sh:423 | `STUB_HEALTH_BODY= STUB_HEALTH_EXIT=7` | Empty value written as `''`. |
| SC2034 setup-logic-test.sh `ENV_FILE`/`LIB_DIR`/`LOCK_PATH` | Genuinely unused: no library or test reads them under the setup test. Copied from the dispatcher. | Removed. |
| SC2148 host-guard.sh | Sourced file with no shebang. | `# shellcheck shell=bash` on line 1. |

No shellcheck disable comments were added, and the shellcheck invocation in the workflow is unchanged.

Tests added or changed:
- common-logic-test.sh: send_mail cases now pass arguments; new case for a missing relay host (no curl call);
  new `questboard_make_dir` cases (leaf mode, two leaves, parents 755 under umask 077, existing 755 leaf
  tightened to 700, existing parent untouched).
- setup-logic-test.sh: the directory holding the state and download directories is 755 after setup.

## Sibling sweep

Searched every script under deploy/ and build/ for the same patterns:
- `mkdir -p -m`: only the three fixed sites existed. The remaining `mkdir -m 700 "$dl"` (no `-p`) and
  `mkdir -p` + `chmod 700` in the dispatcher are not SC2174.
- Assignments referenced only once in their file (script in the session scratchpad): the survivors are
  inline per-command environment prefixes, heredoc config text, or variables read by sourced libraries
  that shellcheck follows (`APP_SERVICE`); none matches a reported finding pattern.
- `VAR= cmd` (SC1007): none left (`IFS= read` is exempt).
- Missing shebang or directive: host-guard.sh was the only one; every other script has a shebang.

## Local run of the steps that never ran in CI

All against this worktree after the fix:
- `bash deploy/tests/run-all.sh`: 8 passed, 0 failed
- `bash build/tests/validate-release-tag-test.sh`: all cases passed
- `bash build/tests/release-workflow-test.sh`: passed
- `build/package-release.sh --version 0.0.0 --commit <sha> --output <scratch dir>`: built questboard-v0.0.0.zip
- `bash build/tests/package-release-smoke-test.sh <zip>`: all checks passed (migrator exits 4 without leaking,
  /health 200 with the version header, favicon through current)
- `QUESTBOARD_TEST_PACKAGED_ZIP=<zip> bash deploy/tests/install-flow-test.sh`: all install flow checks passed
  (including the packaged-zip install and outcome mail through the new send_mail arguments)
- `bash -n` on every touched script.

Residual risk: the shellcheck step itself can only be confirmed by a new hosted run, which was not triggered
here (not pushed).

## Commit

- c7966cd6 fix(89-10): clear the shellcheck findings the hosted run reported

## For Task 3

- The branch must be pushed and CI re-run green (all five jobs) before merging PR 155.
- The self-hosted runner is still registered; that is the one remaining check-github-settings.sh FAIL.
