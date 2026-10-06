---
phase: 89-pull-based-release-deployment
verified: 2026-10-05T18:10:00Z
status: passed
score: 16/16 must-haves verified
behavior_unverified: 0
overrides_applied: 0
re_verification: false
gaps: []
deferred: []
human_verification:

  - test: "Confirm the WR-03 decision: a transient failure of the main-ancestry compare call (transport error, HTTP 403/429/5xx) is now a quiet retry (no mail, nothing remembered), while 404 and other 4xx stay definitive refusals"
    expected: "You accept that a rate-limited or flaky compare call never parks a good release, and that an unreadable 200 body is still a refusal"
    why_human: "Decision logic that changes which outcome a failure maps to (89-REVIEW-FIX.md marks it 'requires human verification'). Offline tests prove the code does what it says, not that the policy is the one you want."

  - test: "Confirm the WR-05 decision: the pre-migration backup is skipped only when migrator status says databaseExists is exactly false; a missing or non-boolean value ends the attempt as 'failed (database could not be reached)' before the app is stopped"
    expected: "You prefer refusing over backing up blind, and are content that the mail reason reuses database_unreachable"
    why_human: "Fail-closed policy choice made by the fixer, flagged for human sign-off in 89-REVIEW-FIX.md."

  - test: "Confirm the WR-06 deviation: after a non-zero 'apply', the installer re-reads status; unchanged pending count means restart the previous release, fewer pending means carry on as an applied install (installed or halted), and a database that cannot be read after 3 tries keeps the old behaviour (previous release restarted, 'failed, rolled back')"
    expected: "You accept leaving the double-fault case (commit acknowledgement lost AND database unreadable for about 6 s) as it was, rather than the review's suggested 'treat as halted'"
    why_human: "Deliberate deviation from the review's suggestion, with a residual risk that only the operator can weigh."

  - test: "Confirm the T-89-31 vocabulary choice: a manual rollback that switches and restarts but fails its health check records the old release as 'abandoned' and the target as 'failed', with no new mail reason; manual runs now also write journal lines (logger, skipped under JOURNAL_STREAM)"
    expected: "You accept reusing existing outcome words and journal-duplicate suppression"
    why_human: "Outcome-vocabulary choice flagged 'requires human verification' in 89-REVIEW-FIX.md."

  - test: "Ship the review/audit hardening: cut the next release from PR #158, then run 'setup' from that release on CT 102, and re-check sha256 of /usr/local/sbin/questboard-deploy and /usr/local/lib/questboard-deploy/*.sh against the new zip"
    expected: "The installed installer matches the new release's deploy/ files; the next poll still reports 'nothing newer' and mails nothing"
    why_human: "The installer never rewrites itself. CT 102 currently runs the v5.4.1 installer (byte-identical to the v5.4.1 zip, verified here), which does NOT contain WR-01..WR-07 or T-89-31. Needs an operator-run release and a root setup; verifier has no write access."

  - test: "Delete /root/setup-adopt.log and /root/setup-final.log on CT 102"
    expected: "Files gone (they hold apt/installer output, no secrets)"
    why_human: "Follow-up recorded in 89-11-SUMMARY.md; needs root, and the unprivileged verifier account cannot see /root."

  - test: "Optional: exercise an unhealthy-release rollback or halted path on the real CT (for example a deliberately broken test tag), if you want production evidence beyond the offline stubs"
    expected: "Switch-back restarts the previous release and sends one 'rolled back' mail; the tag is remembered and later polls only journal"
    why_human: "In production only the 'installed' path has run. Rollback, halted, refused and failed rows are proven by the offline install-flow tests with stubbed host commands and by real-SQL atomicity tests in CI, not on the live host. Risky to induce, so left to the operator's judgement."
---

# Phase 89: Pull-Based Release Deployment Verification Report

**Phase Goal:** A tagged release reaches production because the server goes and fetches it, not because GitHub pushes it there. The server checks for new releases on a timer, verifies the download, installs it, and confirms the app came back healthy. GitHub does not need a runner, credential or any other link to the production box.
**Verified:** 2026-10-05
**Status:** human_needed
**Re-verification:** No, initial verification
**Code verified at:** HEAD f3cf9458 on milestone/v9-rolling-improvements, including the 89-REVIEW-FIX.md fixes.

The goal is achieved in the codebase and on the live production host. All 16 must-have truths verified, with no gaps and no blockers. The status is `human_needed` rather than `passed` because the review-fix pass left four decision-logic changes that its own report marks "requires human verification". The hardened installer is also merged but not yet running on the CT.

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | The server checks for new releases on a timer (D-13) | VERIFIED | `deploy/systemd/questboard-deploy-poll.timer`: `OnBootSec=2min`, `OnUnitActiveSec=5min`, `RandomizedDelaySec=30`. The service is a root oneshot running `/usr/local/sbin/questboard-deploy poll`. Live on CT 102: the timer is `ActiveState=active` and the last poll service `Result=success`. `cmd_poll` reads `releases/latest` unauthenticated and installs only a strictly newer strict-semver tag. |
| 2 | The download is verified, with no bypass (D-01, D-03) | VERIFIED | `deploy/lib/verify.sh` and `cmd_install` run in order: checksum, then `gh attestation verify` pinned to repo, signer workflow, `refs/tags/<tag>` and `--deny-self-hosted-runners`, with token variables unset under a private config dir, then compare-API main ancestry. All run before staging. There is no bypass flag or variable, and the config loader is allow-list only. A service outage returns status 3: nothing is installed and nothing mailed or remembered, so it is fail-closed. This is a documented deviation from D-01's "counts as failed" wording (89-FIX-installer-SUMMARY, `docs/deploy.md`). Real-byte refusal test: `deploy/tests/verify-rejects-tampered-artifact-network-test.sh`, CI `network-verify` green. `build/verify-published-release.sh` passed for v5.4.0 and v5.4.1 (89-10, 89-11). |
| 3 | The release installs into a versioned directory with an atomic switch (D-18) | VERIFIED | Live: `current -> /opt/questboard/releases/5.4.1`, releases `5.3.3` (adopted) and `5.4.1`, all `root:root 755`. `questboard.service` has `WorkingDirectory=/opt/questboard/current/app` through the `10-release-layout.conf` drop-in. `questboard_activate_release` does a temp-link `mv -T`, and staging uses `mv -T` after the hardening pass. |
| 4 | The installer confirms the app is healthy and acts on the outcome matrix (D-10, D-11) | VERIFIED | `cmd_install` implements every matrix row through `questboard_decide_outcome`. Install order is status, backup, stop, apply, switch, start, health. Health must be 200 with body Healthy or Degraded and the matching `X-QuestBoard-Version`. Live: `/health` is 200 with `X-QuestBoard-Version: 5.4.1`. `bash deploy/tests/run-all.sh` ran here: 8 passed, 0 failed. `install-flow-test.sh` exercises each row. |
| 5 | Migrations apply atomically, with a backup first and a refusal when unsafe (D-06, D-07, D-09) | VERIFIED | `QuestBoard.Migrator` has a `ProjectReference` to Repository only and no EF `PackageReference`. `MigrationRunner` does `BeginTransaction`, `Migrate` and one `Commit`. It has a non-transactional preflight, a `COPY_ONLY` backup with a label regex and SQL parameters, and exits 2/3/4/5/6. Local: 33 migrator unit tests pass. The 11 real-SQL tests skip locally without a server, but CI `migrator-sql` runs with `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` (skips fail the job) and is green on HEAD (run 37350945081). 89-10 records 11/11 executed. |
| 6 | GitHub has no runner, credential or other link to production (D-05, D-23) | VERIFIED | `gh api .../actions/runners --jq .total_count` returned `0`. `build/check-github-settings.sh` returned 4 PASS. No `actions.runner*` unit on the CT. `/etc/sudoers.d` holds only the stock `README`. `git ls-files` finds no `binary-release.yml`. The only `runs-on` values in workflows are hosted `ubuntu` (grep for non-ubuntu is empty), and `release-workflow-test.sh` passes. The CT holds no GitHub token; `gh` runs with tokens unset. |
| 7 | The release pipeline builds, tests, attests and gates publish (D-01, D-02, D-04) | VERIFIED | `release.yml`: `permissions: {}`, SHA-pinned actions, tag validation, full `dotnet test` with the SQL service container required, deploy and script tests, package smoke, `actions/attest-build-provenance`, then a draft. A `publish` job with `environment: deploy` re-downloads, re-verifies and runs `gh release edit --draft=false --latest`. Live: v5.4.1 is published with exactly the zip, `.sha256` and `.sigstore.json`. The release run for v5.4.1 succeeded (37337860144). The check-github-settings controls pass. |
| 8 | Only install outcomes mail; idle polls stay silent (D-14) | VERIFIED | `finish()` is the single mail path. Idle, unreachable, no-release, remembered and no-verdict paths only log. Mail is closed-vocabulary with no paths or secrets, sent by curl SMTP to the configured relay. Production evidence in 89-12: three idle polls, no further mail. Live: the poll service is `Result=success`. |
| 9 | Failed tags are remembered and skipped (D-15) | VERIFIED | `questboard_record_attempt` and `questboard_remembered_outcome`; `cmd_poll` skips remembered tags with a journal line and no mail; a manual `install` overrides. Covered in `install-flow-test.sh`. |
| 10 | The installer runs as root, sandboxed, and releases are root-owned (D-19) | VERIFIED | The poll unit has `NoNewPrivileges`, `PrivateTmp`, `ProtectSystem=strict`, `ReadWritePaths=/opt/questboard /var/lib/questboard-deploy`, `ProtectHome=read-only` and more. The migrator runs through `systemd-run` as `questboard` with `EnvironmentFile`, so root never reads the secret file. Live: `/opt/questboard`, `releases` and both release trees are `root:root 755`. `sandboxing-test.sh` passes. |
| 11 | Cutover adopted the running install without a version change (D-22) | VERIFIED | `setup` detects the version from the DLL and requires `--confirm-adopt-version`. Live: `5.3.3` is kept on disk as the adopted release, `5.4.1` is active, and `/health` returns 200. Cutover steps are recorded in 89-11. The gh key fingerprint `7F38BBB5...3325` is pinned in `deploy/lib/setup.sh`. |
| 12 | Manual `rollback` is safe and DB-checked (D-12) | VERIFIED | `cmd_rollback`: the target must be on disk with its own migrator, `status` must show nothing unknown and nothing pending, and the adopted target is refused with a pointer to the manual restore. A failed health check now records the outcome and exits non-zero. Tests are in `install-flow-test.sh`. |
| 13 | The installer never self-updates, and reports when its files differ (D-21) | VERIFIED | `questboard_deploy_files_differ` feeds `--update-from` in the mail. Nothing rewrites `/usr/local/sbin`. Live: the CT files are byte-identical to the v5.4.1 zip, checked by sha256 here. |
| 14 | The release artifact contract holds (D-06, D-18) | VERIFIED | `build/package-release.sh` was run here with version 0.0.0. The zip holds app, migrator, deploy without `tests`, and the manifest. `package-release-smoke-test.sh` printed "All checks passed", including `X-QuestBoard-Version` equal to the packaged version and a static file served through `current`. |
| 15 | The Docker path and startup `Migrate()` are untouched (D-08) | VERIFIED | `context.Database.Migrate()` is still at `ServiceExtensions.cs:47`. `git log --since=2026-10-04` shows no commits to `Dockerfile`, `docker-compose.yml` or `docker-publish.yml`. |
| 16 | Docs and planning state match the new design (D-24) | VERIFIED | `docs/deploy.md`, `docs/releasing.md` and `docs/server-setup.md` describe the pull deploy. server-setup has no runner, sudoers or `deploy.sh` instructions apart from the retirement context. `.planning/PROJECT.md` line 163 uses `/etc/questboard/env` without a dot. |

**Score:** 16/16 truths verified, 0 behavior-unverified. The behavior-dependent truths (the transitions in 4, 5, 9 and 12) each have a passing test that exercises the transition. Their evidence is the offline `install-flow-test.sh` run here and the real-SQL CI job.

### Required Artifacts

| Artifact | Status | Details |
|----------|--------|---------|
| `QuestBoard.Migrator/MigrationRunner.cs`, `MigratorCli.cs` | VERIFIED | Substantive and wired into the release zip. Unit tests pass. |
| `build/package-release.sh`, `build/tests/package-release-smoke-test.sh` | VERIFIED | Ran here, all checks passed. |
| `deploy/bin/questboard-deploy` (676 lines), `deploy/lib/{common,deploy,release,setup,verify}.sh` | VERIFIED | Full implementation, no stubs. Installed byte-identical on the CT (v5.4.1). |
| `deploy/systemd/*` (poll service and timer, drop-in) | VERIFIED | Present, installed on the CT. |
| `deploy/sql-ct/prune-premigration-backups.sh`, `deploy/deploy.conf.example` | VERIFIED | Present. The prune script is installed and scheduled per 89-11. |
| `.github/workflows/release.yml`, `build/validate-release-tag.sh`, `build/check-github-settings.sh`, `build/verify-published-release.sh` | VERIFIED | All run or tested here. |
| `QuestBoard.IntegrationTests/Migrator/*SqlServer*` | VERIFIED | 11 tests, skipped locally by design, required in CI. |
| `docs/deploy.md`, `docs/releasing.md`, `docs/server-setup.md`, `.planning/PROJECT.md` | VERIFIED | Content as described in truth 16. |

### Key Link Verification

| From | To | Status |
|------|----|--------|
| `release.yml` build job to `package-release.sh --version ... --commit "$GITHUB_SHA"` | WIRED |
| `release.yml` publish job to `gh attestation verify ... --deny-self-hosted-runners` and `environment: deploy` | WIRED |
| `cmd_install` to `questboard_decide_outcome`, `questboard_stage_release`, `questboard_run_migrator`, `questboard_wait_for_health` | WIRED |
| `cmd_poll` to `questboard_remembered_outcome` and then `cmd_install` | WIRED |
| `cmd_setup` to `questboard_setup_main` | WIRED |
| poll service `ExecStart` to `/usr/local/sbin/questboard-deploy poll` | WIRED, live on the CT |
| drop-in to `current/app/QuestBoard.Service.dll` | WIRED, live (`WorkingDirectory` confirmed) |
| `/health` to `AppVersion.Current` (`Program.cs:380`) | WIRED, live header `5.4.1` |
| `dotnet.yml` `migrator-sql` to the gated tests (`QUESTBOARD_MIGRATOR_TEST_REQUIRED`) | WIRED |

### Data-Flow Trace (Level 4)

There is no UI rendering in this phase. The data that matters is the version and status chain. The `/health` header is built from the assembly informational version set by `-p:Version=`. The smoke test proves it equals the packaged version and the live CT returns `5.4.1`. The migrator status JSON is parsed by the installer with real `python3` JSON parsing. Status: FLOWING.

### Behavioral Spot-Checks

| Behavior | Command | Result |
|----------|---------|--------|
| Offline installer suite | `bash deploy/tests/run-all.sh` | 8 passed, 0 failed |
| Workflow structure guard | `bash build/tests/release-workflow-test.sh` | PASS |
| Tag validation | `bash build/tests/validate-release-tag-test.sh` | All cases passed |
| Solution build | `dotnet build -c Release` | 0 errors |
| Migrator unit tests | `dotnet test QuestBoard.UnitTests --filter Migrator` | 33 passed |
| Package and smoke | `package-release.sh --version 0.0.0` then smoke test | All checks passed |
| Runners registered | `gh api .../actions/runners --jq .total_count` | 0 |
| Settings controls | `bash build/check-github-settings.sh` | 4 PASS |
| Published release | `gh release view` | v5.4.1, not a draft, 3 assets. Downloaded zip sha256 is `9f3f52d1...c618c7b9`, matching the `.sha256` asset and 89-11. |
| Live health | CT `curl localhost:5000/health` | 200, `X-QuestBoard-Version: 5.4.1` |
| Installer byte-identity | CT `sha256sum` against the unzipped v5.4.1 release | Installer and 5 libraries all identical |
| HEAD CI | run 37350945081 | build, migrator-sql, workflow-lint, network-verify and deploy-scripts all success |

### Probe Execution

No `probe-*.sh` scripts exist or are declared by the plans. Not applicable.

### Requirements Coverage

The phase has no requirement IDs, and `REQUIREMENTS.md` maps none to it. There are no orphaned requirements. Must-haves came from the ROADMAP goal and decisions D-01..D-23.

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| `deploy/`, `build/`, `QuestBoard.Migrator`, `.github/workflows` | `TBD`, `FIXME`, `XXX` | None found | Debt-marker gate is clear |
| Same trees plus docs | GSD IDs in source (`D-nn`, `Phase 89`, `WR-`, `T-89`) | None found | Respects the CLAUDE.md code-comment rule |
| Info findings IN-01..IN-08 in `89-REVIEW.md` | Not addressed (out of scope for the fix pass) | Info | Robustness polish only: IN-01 setup stop outside the rescue trap, IN-03 live third-party download in the release job, IN-04, IN-05 Degraded accepted silently, IN-06 exit-code collapse, IN-07 poll/manual lock collision, IN-08 link naming. IN-02 (prune script not installed or scheduled) was resolved in 89-11, which installed it and added a weekly cron. |

### Human Verification Required

See the `human_verification` list in the frontmatter. In short:

1. Sign off the four decision-logic changes from the review-fix pass: WR-03, WR-05, WR-06 and T-89-31.
2. Cut the next release from PR #158 and run `setup` on the CT so the hardened installer actually runs there. It does not today.
3. Delete the two leftover `/root/setup-*.log` files on CT 102.
4. Optionally exercise a failure path on the real host.

### Gaps Summary

There are no gaps. Every mechanism the goal names exists, is wired and is proven:

- The timer polls.
- The installer verifies the checksum, then the pinned attestation, then main ancestry, with no bypass.
- It installs into a versioned directory with an atomic symlink switch.
- It checks health and decides rollback or halt from the outcome matrix.
- Migrations are atomic, proven against a real SQL Server in CI.
- GitHub holds no runner, credential or workflow that can reach the box. This was confirmed from both sides: 0 registered runners, no `actions.runner*` unit, no sudoers rule and no self-hosted target.
- The first pull-based install (v5.4.1) ran in production through the sandboxed unit and came up healthy.

The residual items above are decisions and rollout steps, not missing goal behaviour.

---

_Verified: 2026-10-05_
_Verifier: Claude (gsd-verifier)_
