---
phase: 89-pull-based-release-deployment
fixed_at: 2026-10-05T18:00:00Z
review_path: .planning/phases/89-pull-based-release-deployment/89-REVIEW.md
iteration: 1
findings_in_scope: 8
fixed: 8
skipped: 0
status: all_fixed
---

# Phase 89: Code Review Fix Report

**Fixed at:** 2026-10-05
**Source review:** `.planning/phases/89-pull-based-release-deployment/89-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 8 (WR-01 to WR-07, plus the security-audit gap T-89-31)
- Fixed: 8
- Skipped: 0
- Info findings (IN-01 to IN-08) were out of scope and are untouched.

Every fix keeps the fail-safe invariant: nothing unverified is staged or activated, and no
refusal became weaker. Three of the fixes (WR-03, WR-05, WR-06) change a decision, not just
plumbing, and are marked "requires human verification" below.

**Where verification ran:** in the isolated agent worktree (not in the main checkout), offline, with
PATH stand-ins (`deploy/tests/lib/host-guard.sh`). Nothing touched a production host, the network,
sudo, real systemctl/systemd-run/gh/curl or mail. The numbers below are reproducible from the
branch after merge.

- `bash deploy/tests/run-all.sh`: 8 passed, 0 failed.
- `bash build/tests/release-workflow-test.sh` and `bash build/tests/validate-release-tag-test.sh`: pass.
- `bash -n` clean on every touched script.
- `dotnet build`: 0 errors. `dotnet test`: 838 unit tests pass; 952 integration tests pass, 11 skipped
  (the SQL Server gated ones, as designed).
- Packaged zip: `build/package-release.sh --version 0.0.0 ...`, then
  `build/tests/package-release-smoke-test.sh <zip>` ("All checks passed") and
  `QUESTBOARD_TEST_PACKAGED_ZIP=<zip> bash deploy/tests/install-flow-test.sh` pass (the shipped
  installer installs the packaged release).
- Regression proof: the new `install-flow-test.sh` was also run against the base commit's installer
  and libraries; it fails 93 checks there (all the new cases), and passes on the fixed code. The
  individual test files for WR-01 to WR-05 were additionally seen failing before their fix was
  written.

## Fixed Issues

### WR-02: `gh` stderr is merged into the JSON that is parsed

**Files modified:** `deploy/lib/verify.sh`, `deploy/tests/verify-logic-test.sh`, `deploy/tests/install-flow-test.sh`
**Commit:** 6ac518ad
**Applied fix:** stdout and stderr of `gh attestation verify` go to separate files. Only stdout is
parsed. On failure stderr (first 4000 bytes) is logged.
**Tests:** `verify-logic-test.sh`: gh stub printing a notice on stderr plus valid JSON verifies and
yields the commit; the same notice with non-JSON stdout still refuses. `install-flow-test.sh`
(`gh-notice`): a full install succeeds with a stderr notice.

### WR-01: `gh attestation verify` has no timeout

**Files modified:** `deploy/lib/verify.sh`, `deploy/lib/setup.sh`, `deploy/tests/verify-logic-test.sh`, `deploy/tests/install-flow-test.sh`, `docs/deploy.md`
**Commit:** cf6936f6
**Applied fix:** `gh` runs under `timeout --kill-after=10 120` (constants
`QUESTBOARD_GH_VERIFY_TIMEOUT_SECONDS` and `QUESTBOARD_GH_VERIFY_KILL_AFTER_SECONDS`, assigned
unconditionally so neither the environment nor the config file can change them). A cut-off run (rc 124 or
137) is not special-cased in the verdict: it falls into the existing path, so it is a quiet retry
only when the connectivity probe also finds a service unreachable, and a refusal otherwise.
`setup` now requires `timeout`.
**Judgement calls:**
- Every `curl` in `deploy/` already carries `--max-time` (`questboard_http_fetch`, the mail send, the
  `setup` key download), so no further bounding was needed in the poll path.
- No `TimeoutStartSec` backstop was added to the poll unit. With every external call bounded, a unit
  timeout would only add a way to kill an install between "app stopped" and "app started". Changing
  the unit would also need a `setup` run on the live CT.
- A stall while the services still answer is a refusal (remembered, one mail), per the brief. It is
  the stricter choice; an operator can retry with `install`.
**Tests:** unit tests with a hanging gh stub and the bound lowered in-process (3 for unreachable, 1
for reachable, and still finishes in seconds); flow tests with a `timeout` stand-in that expires
(outage then recovery; reachable then refused) and a check that gh is always run under the bound.

### WR-03: A transient failure of the main-ancestry call permanently parks a good release

**Files modified:** `deploy/lib/verify.sh`, `deploy/bin/questboard-deploy`, `deploy/tests/verify-logic-test.sh`, `deploy/tests/install-flow-test.sh`, `docs/deploy.md`
**Commit:** d159a5f9
**Status:** fixed: requires human verification (decision logic)
**Applied fix:** `questboard_commit_on_branch` returns 3 (`QUESTBOARD_COMMIT_CHECK_NO_VERDICT`) for a
transport failure, HTTP 403, 429 or any 5xx. The dispatcher handles it through a new
`end_without_verdict` helper shared with the verification outage (download removed, journal line,
poll exits 0, manual run exits non-zero, nothing mailed or remembered). `cmd_verify` reports it
distinctly. No extra request is made.
**Judgement calls:**
- A 404 (GitHub does not know the commit in this repository) and other 4xx stay definitive refusals,
  as do a 200 with status ahead/diverged and a 200 whose body cannot be read. The last one is not on
  the list in the brief, so it keeps today's fail-safe behaviour.
- Each retry re-downloads the zip before reaching the compare call, as the existing verification
  outage path already does. Reordering would need the commit from the attestation first.
**Tests:** unit tests for 403/429/500/502/503/504/transport (3), 404/422/ahead/diverged/unreadable (1);
flow tests for install by hand, polls under 403/429/503 and the following successful poll, and a 404
refusal. The old test that pinned "unreachable is refused" was replaced.

### WR-04: `set -e` is silently disabled inside `questboard_stage_release`

**Files modified:** `deploy/lib/release.sh`, `deploy/lib/verify.sh`, `deploy/lib/deploy.sh`, `deploy/lib/common.sh`, `deploy/bin/questboard-deploy`, `docs/deploy.md`, `deploy/tests/release-logic-test.sh`, `deploy/tests/install-flow-test.sh`, `deploy/tests/questboard-deploy-logic-test.sh`, `deploy/tests/common-logic-test.sh`
**Commit:** 997fc3fc
**Applied fix:** `questboard_secure_tree` returns non-zero if any of its three steps fails. Every step of
`questboard_stage_release` (`mkdir -p`, clearing a stale staging directory, the hardening pass,
removing a leftover release, `mv -T`, and the symlink scan) checks its own status; a failure removes the
staging directory and returns the new status 5. The dispatcher maps 5 to a new `content`/`error`
outcome row (`failed keep`) with a new fixed mail reason `staging_failed` ("the release could not be put
in place"), so a failed move is no longer reported as `insufficient_disk`. The same pattern was fixed in
`questboard_verify_checksum` (it is called through `if !`).
**Other places checked:** every other function reached through `||`, `if` or `$(...)` in `deploy/`
(download, fetch, commit check, migrator run, health wait, mail, activation, prune, setup) already
returns explicitly; nothing else needed changing. `setup` calls `questboard_secure_tree` as a plain
command, where its new non-zero status stops setup under `set -e`.
**Tests:** `release-logic-test.sh` with failing `chmod` and `mv` stand-ins (status 5, no release
directory, no staging directory), a locked leftover release directory (non-root only), an uncreatable
releases directory, and `questboard_secure_tree` itself; flow test with a failing hardening step (mail,
record, nothing touched); outcome table and reason vocabulary checks.

### WR-05: The pre-migration backup fails open

**Files modified:** `deploy/bin/questboard-deploy`, `deploy/tests/install-flow-test.sh`, `docs/deploy.md`
**Commit:** 56ca5a19
**Status:** fixed: requires human verification (decision logic)
**Applied fix:** the status must report `databaseExists` as exactly `true` or `false`; anything else
(missing key, other value, unparsable) ends the attempt as `failed (database could not be reached)`
before the app is stopped. The backup is skipped only for an explicit `false` (a fresh host).
**Judgement call:** I chose to refuse rather than back up. A backup of an unreadable state cannot be
requested safely (`backup` itself needs a database), and refusing is the option that never migrates
unprotected. The check applies even when nothing is pending, because a status without the key means
the migrator output cannot be trusted at all. The reason reuses `database_unreachable`, as the
existing unparsable-`pending` branch does.
**Tests:** flow tests for a fresh host (no backup, apply runs), a status without the key, and a
non-boolean value.

### WR-06: Apply failures lose the non-SQL cause and are always reported as "rolled back"

**Files modified:** `QuestBoard.Migrator/MigrationRunner.cs`, `QuestBoard.Migrator/MigratorCli.cs`, `QuestBoard.UnitTests/Migrator/MigratorBackupAndApplyCliTests.cs` (commit 033f5302); `deploy/bin/questboard-deploy`, `deploy/tests/install-flow-test.sh`, `docs/deploy.md` (commit 3c8a26d1)
**Commits:** 033f5302 (migrator), 3c8a26d1 (installer)
**Status:** fixed: requires human verification (decision logic)
**Applied fix, migrator:** `MigratorApplyException` carries `FailureTypeName` (set only when the failure
was not a SQL error) and `CommitOutcomeUnknown` (set when the exception came from `Commit`). The CLI line
is built by the new public `MigratorCli.DescribeApplyFailure`: type name only, never the message; "apply
failed while committing; whether it was kept is unknown" for a commit failure. Exit code stays 6.
**Applied fix, installer:** after any non-zero `apply` the installer re-runs `status` with the new
release's migrator (up to 3 reads, 2 seconds apart, only while the answer is unreadable) and compares the
pending count with the count before the apply:
- unchanged: rollback as before (previous release restarted, `failed, rolled back`);
- fewer pending (some or all committed): carry on as an applied install. The new release is activated
  and started; healthy gives `installed`, unhealthy gives `halted - migrations applied` with the backup
  name. The previous release is never started on the migrated schema.
**Judgement call (deviation):** if the database cannot be read at all after the retries, the installer
keeps today's behaviour (previous release restarted, mail `failed, rolled back`) and logs that nothing
shows the schema changed. The review suggested "otherwise treat as halted". I did not, because
activating the new release against a schema that most likely is not migrated (the usual cause of an
unreadable database after a failed apply is a database that is down) would leave the board on a release
that cannot run, and leaving the app stopped would turn a likely outage into a certain one. The
double fault (commit acknowledgement lost and the database unreadable for 6 seconds) is the only case
left uncovered, and it is no worse than before.
**Tests:** unit tests for the type-name line, no message leakage, SQL error unchanged and the commit
wording. Flow tests for: applied-despite-failure and healthy (installed), the same and unhealthy
(halted, backup named, old release never started), partly committed, unchanged (rollback, with the
extra status call), and unreadable (three reads, previous release restarted). The SQL-gated
integration tests were not runnable here (no SQL Server); the new unit tests do not need one.

### WR-07: Test-only environment seams are honoured by the production installer

**Files modified:** `deploy/bin/questboard-deploy`, `deploy/lib/common.sh`, `deploy/tests/lib/host-guard.sh`, `deploy/tests/install-flow-test.sh`, `deploy/tests/common-logic-test.sh`, `deploy/tests/sandboxing-test.sh`, `docs/deploy.md`
**Commit:** 00e7cd1e
**Applied fix:** `QUESTBOARD_DEPLOY_ROOT` and `QUESTBOARD_DEPLOY_CONF` are honoured only when the root is a real
(non-symlink) directory holding a `.questboard-test-root` marker (non-symlink), both owned by the user
running the installer. Otherwise the run exits 1 with `refusing QUESTBOARD_DEPLOY_ROOT / QUESTBOARD_DEPLOY_CONF: not a test tree owned by this user`
before anything is read, written or sourced. The check runs before the library directory is chosen,
which also closes a larger hole than the review named: the library fallback path was built from the
same variable, so an inherited value could have made a root run `source` libraries from a
caller-owned tree. `DEPLOY_ROOT` is non-empty only after the check, and `questboard_load_conf` now keys
its "expected owner" off that variable instead of reading the environment.
**Why this design:** a root run needs a root-owned marker, which only root can create, so a variable
inherited through `env_keep`, `SETENV` or `sudo -E` cannot relax anything. The test harness keeps
working with one line per test tree (`host_guard_mark_test_root`); tests that source the libraries
directly set `DEPLOY_ROOT` themselves. A refusal (rather than a silent ignore) was chosen so a stray
variable is noticed. The docs show `sudo env -i PATH=... questboard-deploy ...`.
**Tests:** flow tests for a root without a marker, a symlinked marker, a configuration path without a
test tree, a configuration path inside a test tree, and a non-root caller with no relocation (still
`must be run as root`); a unit test that an environment `QUESTBOARD_DEPLOY_ROOT` no longer relaxes
the owner check.

### T-89-31: Manual runs left no journal trace; a failed manual rollback recorded nothing

**Files modified:** `deploy/lib/common.sh`, `deploy/bin/questboard-deploy`, `deploy/tests/lib/host-guard.sh`, `deploy/tests/install-flow-test.sh`, `deploy/tests/common-logic-test.sh`, `deploy/tests/sandboxing-test.sh`, `docs/deploy.md`
**Commit:** 6a5c613b
**Status:** fixed: requires human verification (outcome vocabulary choice)
**Applied fix (1):** `questboard_log` and `questboard_die` also write to the journal with
`logger -t questboard-deploy -p daemon.info|daemon.err`, except when `JOURNAL_STREAM` is set (systemd
sets it for a unit whose stderr is the journal), so poll runs get no duplicate lines. A missing or
failing `logger` changes nothing. The test harness got a recording `logger` stand-in so no test writes to a
real journal.
**Applied fix (2):** a manual rollback that switched `current` and restarted the app but failed the
health check now records the release rolled away from as `abandoned` and the target as `failed`
(logged as `outcome=failed ... reason=unhealthy`), then still exits non-zero with the same message. No
new outcome word or mail reason was needed. `abandoned` is recorded before the health wait on purpose:
otherwise the next poll would reinstall the release the operator just left. The "no record" expectation
in `install-flow-test.sh` was replaced, including a poll that must not reinstall the abandoned
release.
**Tests:** journal lines for a manual install, a manual refusal (error priority), a rollback, and the
absence of duplicates under `JOURNAL_STREAM`; unit tests for the log helper; the failed-rollback flow case.

## Skipped Issues

None.

## Not done, on purpose

- Info findings IN-01 to IN-08 (out of scope for this pass).
- `TimeoutStartSec` on the poll unit (see WR-01).
- No change to the CT or any production host; the orchestrator merges and releases. Because the fixes
  change the installer's libraries and dispatcher, they reach production only through a `setup` run
  from the release that carries them (the installer never rewrites itself).

---

_Fixed: 2026-10-05_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
