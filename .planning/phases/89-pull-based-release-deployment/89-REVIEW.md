---
phase: 89-pull-based-release-deployment
reviewed: 2026-10-05T00:00:00Z
depth: standard
files_reviewed: 50
files_reviewed_list:
  - .github/workflows/dotnet.yml
  - .github/workflows/release.yml
  - .github/zizmor.yml
  - .gitignore
  - QuestBoard.IntegrationTests/Controllers/HealthVersionHeaderTests.cs
  - QuestBoard.IntegrationTests/Migrator/MigrationRunnerSqlServerTests.cs
  - QuestBoard.IntegrationTests/Migrator/MigratorSqlServer.cs
  - QuestBoard.IntegrationTests/Migrator/SqlProbeMigrations.cs
  - QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj
  - QuestBoard.Migrator/MigrationRunner.cs
  - QuestBoard.Migrator/MigratorCli.cs
  - QuestBoard.Migrator/Program.cs
  - QuestBoard.Migrator/QuestBoard.Migrator.csproj
  - QuestBoard.Service/Program.cs
  - QuestBoard.UnitTests/Migrator/MigrationPreflightTests.cs
  - QuestBoard.UnitTests/Migrator/MigratorBackupAndApplyCliTests.cs
  - QuestBoard.UnitTests/Migrator/MigratorCliTests.cs
  - QuestBoard.UnitTests/Migrator/ProbeMigrations.cs
  - QuestBoard.UnitTests/QuestBoard.UnitTests.csproj
  - QuestBoard.slnx
  - build/check-github-settings.sh
  - build/package-release.sh
  - build/tests/package-release-smoke-test.sh
  - build/tests/release-workflow-test.sh
  - build/tests/validate-release-tag-test.sh
  - build/validate-release-tag.sh
  - build/verify-published-release.sh
  - deploy/bin/questboard-deploy
  - deploy/deploy.conf.example
  - deploy/lib/common.sh
  - deploy/lib/deploy.sh
  - deploy/lib/release.sh
  - deploy/lib/setup.sh
  - deploy/lib/verify.sh
  - deploy/sql-ct/prune-premigration-backups.sh
  - deploy/systemd/questboard-deploy-poll.service
  - deploy/systemd/questboard-deploy-poll.timer
  - deploy/systemd/questboard.service.d/10-release-layout.conf
  - deploy/tests/common-logic-test.sh
  - deploy/tests/fixtures/public-attested-artifact.env
  - deploy/tests/install-flow-test.sh
  - deploy/tests/lib/host-guard.sh
  - deploy/tests/prune-premigration-backups-test.sh
  - deploy/tests/questboard-deploy-logic-test.sh
  - deploy/tests/release-logic-test.sh
  - deploy/tests/run-all.sh
  - deploy/tests/sandboxing-test.sh
  - deploy/tests/setup-logic-test.sh
  - deploy/tests/verify-logic-test.sh
  - deploy/tests/verify-rejects-tampered-artifact-network-test.sh
findings:
  critical: 0
  warning: 7
  info: 8
  total: 15
status: issues_found
---

# Phase 89: Code Review Report

**Reviewed:** 2026-10-05
**Depth:** standard
**Files Reviewed:** 50
**Status:** issues_found

## Summary

The trust chain holds up. Checksum, `gh attestation verify --bundle` pinned to repo, signer workflow, `refs/tags/<tag>` and `--deny-self-hosted-runners`, then main ancestry of the attested commit. No path installs an unverified artifact. No token reaches `gh`. The migrator's connection details never reach stdout, stderr or mail. The migrator transaction is proven against a real SQL Server (rolled-back schema and history). The release workflow uses SHA-pinned actions, no `${{ }}` inside `run:`, `persist-credentials: false`, and per-job minimal permissions. The outcome matrix in `questboard_decide_outcome` matches the design table, and its install order is covered by `install-flow-test.sh`. No source comment carries planning IDs.

I found no BLOCKER-class defect. The weaknesses are mostly robustness issues in the installer:
- an unbounded external call that can wedge the whole pipeline;
- a success path that can be mis-parsed into a permanent "refused";
- a transient-failure path that permanently parks a good release;
- `set -e` being silently disabled in the staging function that applies the hardening;
- a backup guard that fails open.

Items marked "verified" below were reproduced locally with a small bash script. Nothing was run against the network or the production hosts.

## Warnings

### WR-01: `gh attestation verify` has no timeout, so a stalled network call wedges every later poll and manual install

**File:** `deploy/lib/verify.sh:102-114` (also `deploy/systemd/questboard-deploy-poll.service`)
**Issue:** Every other external call is bounded. curl uses `--max-time`, the migrator has `RuntimeMaxSec`, and health uses a deadline. `gh attestation verify` is not bounded. It fetches the TUF trusted root and the attestation material over the network. The poll service is `Type=oneshot`, and a oneshot unit has no start timeout by default. If `gh` stalls, the poll unit stays active forever while holding the `flock`. The consequences:
- `OnUnitActiveSec` never re-arms the timer, so there are no more polls and no mail.
- Every manual `install`, `rollback` and `setup` dies with "another questboard-deploy invocation is already running".

Nothing alerts the operator.
**Fix:**
```bash
# verify.sh
output="$(env -u GH_TOKEN ... timeout --kill-after=10 180 gh attestation verify ... 2>"${work}/stderr")" || rc=$?
```
Treat rc 124 or 137 like the "services unreachable" result (return 3). Optionally also set `TimeoutStartSec=30min` on the poll service as a backstop.

### WR-02: `gh` stderr is merged into the JSON that is parsed, so any warning turns a valid release into a permanent "refused"

**File:** `deploy/lib/verify.sh:114` and `:128-135`
**Issue:** The command ends with `--format json 2>&1`, and the combined text is fed to `json.load`. If `gh` writes anything to stderr on a successful verification, the JSON parse fails. Examples: a deprecation notice, an upgrade notice other than the one suppressed, or a TUF refresh message after `setup` or unattended-upgrades bumps `gh`. The function then returns 1 (not 3). The installer records `refused` with the reason "release signature check failed". It emails once and then skips that tag until an operator intervenes, even though the artifact was good. Verified locally: JSON plus one stderr line gives `JSONDecodeError: Extra data`.
**Fix:** Capture the streams separately. Parse stdout only, and log stderr only on failure:
```bash
output="$(env ... gh attestation verify ... --format json 2>"${work}/stderr")" || rc=$?
errtext="$(cat "${work}/stderr" 2>/dev/null || true)"
```

### WR-03: A transient failure of the main-ancestry API call permanently parks a good release, unlike the equivalent attestation outage

**File:** `deploy/bin/questboard-deploy:282-284`; `deploy/lib/verify.sh:145-174`
**Issue:** `questboard_commit_on_branch` returns 1 for any transport error, 5xx or non-200, including a 403 or 429 from the unauthenticated rate limit. `cmd_install` maps all of these to `stop_attempt verify fail not_on_main`. That records `refused`, so later polls skip the tag, and it sends a mail saying "the release was not built from the main branch". The design notes say the unauthenticated limit is 60 per hour per public IP, shared with another CT, so a 403 is plausible. The same release would be retried safely if the TUF endpoints were down (the QUESTBOARD_VERIFY_UNREACHABLE path), but a compare-API blip is treated as a final verdict. The test `compareunreachable` pins this, so it is deliberate, but it is inconsistent and the mail text is misleading.
**Fix:** Have `questboard_commit_on_branch` return a distinct "no verdict" status for transport failures, 403, 429 and 5xx. Handle that status like `QUESTBOARD_VERIFY_UNREACHABLE`: `rm -rf "$dl"`, log, `exit 0` on poll, nothing remembered. Reserve `not_on_main` for a 200 response whose status is not identical or behind, or for a 404.

### WR-04: `set -e` is silently disabled inside `questboard_stage_release`, so a failed hardening step is ignored and the release is still activated

**File:** `deploy/bin/questboard-deploy:287` calling `deploy/lib/release.sh:140-215`
**Issue:** The function is invoked as `questboard_stage_release ... || rc=$?`. In bash, `errexit` is ignored for the entire body of a function that is the left side of `||`. Verified locally: a `false` inside such a function does not stop it. As a result:
- `questboard_secure_tree "$staging"` (the root `chown -R`, `chmod 755`, `go-w`) can fail without effect. The function goes on to `mv -T` and returns 0, and the tree is activated without the read-only, root-owned guarantee that protects the app from rewriting its own code.
- `mkdir -p "$releases_dir"` and `rm -rf "$final"` failures are also swallowed.
- If `mv -T` is the command that fails, its non-zero status surfaces as rc 1 and is reported as `insufficient_disk`, which is a misleading reason.

**Fix:** Check the steps explicitly instead of relying on `errexit`:
```bash
questboard_secure_tree "$staging" || { questboard__stage_fail "$staging" 3; return; }
rm -rf "$final" || { questboard__stage_fail "$staging" 3; return; }
mv -T "$staging" "$final" || { questboard__stage_fail "$staging" 3; return; }
```
Make `questboard_secure_tree` return non-zero if any of its three commands fails.

### WR-05: The pre-migration backup fails open when `databaseExists` is anything other than the literal `true`

**File:** `deploy/bin/questboard-deploy:305-319`
**Issue:** The backup runs only when `[ "$pending" -gt 0 ] && [ "$db_exists" = "true" ]`. `db_exists` comes from `questboard_json_get ... || db_exists=""`. If the key is renamed, missing or unparsable while `pending` still parses, the backup is skipped silently and `apply` proceeds against a database that may hold production data. A skipped safety net should be loud. The intent is only to skip the backup on a brand-new host.
**Fix:** Invert the test so only an explicit `false` skips the backup:
```bash
if [ "$pending" -gt 0 ] && [ "$db_exists" != "false" ]; then
  run_migrator ... backup --label "$TAG"
  ...
```
Also treat an empty `db_exists` as `stop_attempt status error database_unreachable`.

### WR-06: Apply failures lose the non-SQL cause and are always reported as "rolled back", even when the commit outcome is ambiguous

**File:** `QuestBoard.Migrator/MigrationRunner.cs:224-238`; `deploy/bin/questboard-deploy:329-345`
**Issue:** There are two related gaps.
1. Diagnostics. The catch-all wraps every exception into `MigratorApplyException(sql?.Number, sql?.Message)`. For a non-SQL failure the CLI prints only "apply failed and was rolled back" and the type is dropped. Examples: the EF pending-model-changes `InvalidOperationException`, or a `CreateMigration` failure. The operator gets a "failed, rolled back" mail with nothing in the journal to diagnose it. The CLI deliberately prints only type names elsewhere, so the same safe treatment fits here.
2. Ambiguity. `transaction.Commit()` sits inside the same try. If the commit acknowledgement is lost (connection drop at commit), the exception is reported as "rolled back" even though the server may have committed. The installer treats every non-zero `apply` exit as "database unchanged" and restarts the previous release on a possibly migrated schema. The same applies if the migrator process dies after commit but before exit.

**Fix:** Print `ex.GetType().Name` in `MigratorApplyException` when there is no SQL error. In the installer, after any non-zero `apply`, re-run `status` and trust "rolled back" only if `pending` is unchanged. Otherwise treat the install as `halted` (the migrated branch) and do not restart the old release.

### WR-07: Test-only environment seams are honoured by the production installer and weaken the root checks

**File:** `deploy/bin/questboard-deploy:18-24` and `:67-72`; `deploy/lib/common.sh:125-127`
**Issue:** `QUESTBOARD_DEPLOY_ROOT` and `QUESTBOARD_DEPLOY_CONF` are read straight from the caller's environment. When `QUESTBOARD_DEPLOY_ROOT` is set:
- `require_root` returns success for a non-root caller.
- The config owner check switches from uid 0 to "the current user".
- Every path (state, lock, releases, env file) is redirected.

The unit does not set them, so there is no direct exploit. But the installer is normally run through `sudo`, and any `env_keep`, `SETENV` or `sudo -E` configuration turns this into a way to make a root run trust a user-owned config, state tree and env-file path. The `ENV_FILE` is also handed to `systemd-run` as the migrator's `EnvironmentFile`.
**Fix:** Make the seams unreachable in production. For example, source them only from a file that exists solely in test trees, or ignore both variables unless a sentinel file under the proposed root is present and root-owned-or-test-owned. At minimum, do not let the seam skip the uid-0 owner check when the real uid is 0. Add `env -i` or an explicit allow-list to the sudo instructions in the docs.

## Info

### IN-01: Cutover `setup` stops the app outside the rescue trap

**File:** `deploy/lib/setup.sh:290` (and `:431`)
**Issue:** `questboard_setup_adopt` calls `systemctl stop` directly, so `APP_STOPPED` is never set. If any later step dies (for example `chown -R` or `questboard_secure_tree`), the app stays stopped and nothing restarts it. The resume path recovers on a rerun, but only if the operator notices.
**Fix:** Use `app_stop` (which sets the flag) from the dispatcher, or set `APP_STOPPED=1` after the stop and clear it after the final start.

### IN-02: The backup prune script is neither installed nor scheduled, and retries can displace the backup that matters

**File:** `deploy/sql-ct/prune-premigration-backups.sh:32-46`
**Issue:** `setup` does not install it, and nothing schedules it. The SQL CT disk will fill over time, and `BACKUP` failure then aborts every future migrating release as `backup_failed`. Also, "keep newest N by mtime" counts repeated retries of one release as separate backups. Five failed attempts of the same release can push out the backup that the halted release's mail points to.
**Fix:** Document and ship a cron entry or timer. Consider keeping at most N distinct tags rather than N files, and never pruning the file named in the most recent `halted` outcome.

### IN-03: The release job depends on a live third-party download

**File:** `.github/workflows/release.yml:84-85`; `deploy/tests/fixtures/public-attested-artifact.env`
**Issue:** The "Network verification test" step downloads a `cli/cli` release asset and reaches the TUF CDN before the attest step. An outage at either, or removal of that asset, blocks a release tag, including an emergency hotfix. The same test already runs on every push in the `network-verify` CI job.
**Fix:** Drop it from the release job, or make it non-blocking there (`continue-on-error: true`).

### IN-04: The original `build` job in `dotnet.yml` keeps tag-pinned actions and persisted credentials

**File:** `.github/workflows/dotnet.yml:21-26`
**Issue:** `actions/checkout@v4` and `actions/setup-dotnet@v4` are tag-pinned and the checkout persists the token. The comment explains the pin exception, and the token is read-only. The job is also set up for .NET 8 while the solution targets 10. The first two points are not exploitable here, but they are inconsistent with the other jobs in the same file.
**Fix:** Pin by SHA and add `persist-credentials: false`, and extend zizmor to `dotnet.yml`.

### IN-05: `Degraded` health is accepted as success without being reported

**File:** `deploy/lib/release.sh:301-330`
**Issue:** A new release that reports `Degraded` passes the health gate and produces an "installed" mail. The only trace is one journal line.
**Fix:** Carry a `degraded` flag into the mail (a fixed-vocabulary line) or treat it as not healthy.

### IN-06: Migrator exit codes collapse into "database could not be reached"

**File:** `deploy/bin/questboard-deploy:296-303`
**Issue:** Any `status` exit code other than 2 or 3 maps to `database_unreachable`. That includes exit 1 (missing `ConnectionStrings__DefaultConnection`, an unexpected exception, or a `systemd-run` failure). The mail therefore blames the database for configuration or sandbox faults.
**Fix:** Map 4 to `database_unreachable`. Add a generic reason such as `migrator_failed` to the closed vocabulary for the rest.

### IN-07: A poll that collides with a manual install marks the poll unit failed

**File:** `deploy/bin/questboard-deploy:74-78`
**Issue:** `flock -n 9 || questboard_die` exits 1 for a poll too, leaving `questboard-deploy-poll.service` in a failed state, which is noisy for a normal collision.
**Fix:** In `cmd_poll`, treat lock contention as a quiet "install in progress" and exit 0.

### IN-08: Temporary link name and archive-symlink handling could be tighter

**File:** `deploy/lib/deploy.sh:145-147`; `deploy/lib/release.sh:185-193`
**Issue:** `mktemp -u` only generates a name and does not reserve it. This is safe only because `/opt/questboard` is root-owned, and a crash leaves a stray `current.XXXXXX` link. Symlinks in the archive are also detected after extraction. The archive is attested first, so this is defence in depth only.
**Fix:** Use a fixed name such as `current.new` with `ln -sfn` followed by `mv -T`. Reject symlink entries with `zipinfo -l` before extracting.

---

_Reviewed: 2026-10-05_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
