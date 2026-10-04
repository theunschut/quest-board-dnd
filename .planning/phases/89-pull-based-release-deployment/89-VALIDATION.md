---
phase: 89
slug: pull-based-release-deployment
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-04
---

# Phase 89 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `89-RESEARCH.md` § Validation Architecture. No requirement IDs are mapped to this phase;
> MH-1..MH-7 are the must-have handles derived in RESEARCH.md.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 3.2.2 + FluentAssertions 8.10.0 (C#); plain bash `check()` harness for the installer and build scripts |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/xunit.runner.json`, `deploy/tests/run-all.sh` (new, Wave 0) |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Migrator"` and `bash deploy/tests/run-all.sh` |
| **Full suite command** | `dotnet test` and `bash deploy/tests/run-all.sh` |
| **Estimated runtime** | ~100 seconds (dotnet suite measured at 94 s) |

---

## Sampling Rate

- **After every task commit:** Run the quick command for the area touched (migrator unit filter, or `bash deploy/tests/run-all.sh`, or `bash build/tests/validate-release-tag-test.sh`)
- **After every plan wave:** Run `dotnet test` and `bash deploy/tests/run-all.sh`
- **Before `/gsd-verify-work`:** Full suite green, plus CI lint and the gated real-SQL migrator test green in a hosted run
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

Task IDs are filled in by the planner/executor; rows below are keyed by must-have.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | MH-2 | — | Every shipped migration is transactional; regression guard for future migrations | unit (no DB) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Migrator"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MH-2 | — | Preflight flags a synthetic `suppressTransaction: true` migration; status computes applied/pending/unknown; exit-code mapping | unit | same | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MH-2 | — | Failing second migration leaves history and schema unchanged; non-transactional pending refused | integration, real SQL, gated | `QUESTBOARD_MIGRATOR_TEST_CONNECTION=... dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Migrator"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MH-3 | — | `/health` 200 with version header equal to `AppVersion.Current` | integration (InMemory) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardTimeZoneHealthCheck"` | partial | ⬜ pending |
| TBD | TBD | TBD | MH-4 | — | Semver gate, remember-and-skip, all six outcome-matrix rows, activation/prune, secret-free mail body, config owner/mode checks | bash logic | `bash deploy/tests/questboard-deploy-logic-test.sh` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MH-4 | — | Full install flow with stubbed `systemctl`/`systemd-run`/`gh`/`curl`; nothing changes on refusal | bash flow | `bash deploy/tests/install-flow-test.sh` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MH-1/4 | — | Tampered byte, wrong repo, wrong signer, wrong source ref all refused; no ambient token reaches `gh` | bash network | `QUESTBOARD_TEST_NETWORK=1 bash deploy/tests/verify-rejects-tampered-artifact-network-test.sh` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MH-4 | — | Poll unit carries the hardening set and write allow-list | bash static | `bash deploy/tests/sandboxing-test.sh` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MH-1 | — | Tag validation accepts `v1.2.3`, refuses `v01.2.3`, `v1.2.3-rc.1`, `v1.2`, off-main tags | bash | `bash build/tests/validate-release-tag-test.sh` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MH-1 | — | Hash-pinned actions, no `${{ }}` in `run:`, no `self-hosted` | lint | `actionlint` + `zizmor` in CI; `! grep -rn self-hosted .github/workflows` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MH-5/7 | — | Docs no longer reference runner, `deploy.sh` or `/etc/questboard/.env` outside the removal handover | grep | `! grep -rnE "deploy\.sh|actions-runner|/etc/questboard/\.env" docs .planning/PROJECT.md` (handover section excepted) | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.Migrator/` project added to `QuestBoard.slnx`, plus `QuestBoard.UnitTests/Migrator/` (MH-2)
- [ ] `QuestBoard.IntegrationTests/Migrator/` gated real-SQL tests with a test `DbContext` and two hand-written migrations (MH-2)
- [ ] `deploy/tests/` harness: `run-all.sh`, `lib/host-guard.sh`, fixtures, logic/flow/sandboxing/network tests (MH-4, MH-1)
- [ ] `build/tests/validate-release-tag-test.sh` (MH-1)
- [ ] CI job in `dotnet.yml`: `shellcheck`, `bash -n`, `run-all.sh`, workflow lint; SQL Server service container for the gated migrator tests
- [ ] `ProjectReference` from the test projects to `QuestBoard.Migrator` (no new framework installs)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Migrator runs via `systemd-run` inside the sandboxed poll unit; `gh attestation verify` initialises its cache | MH-4 | Needs the real CT, root and systemd | `systemctl start questboard-deploy-poll.service`; inspect journal |
| `migrator status` against production: applied = 43 + newer, no unknown, `canBackup` true | MH-2 | Production DB, read-only | Run `status` as `questboard` via the installer path |
| Backup rehearsal and prune | MH-2 | Files land on the SQL CT disk | `migrator backup --label rehearsal`; run prune script with `keep=5` on the SQL CT |
| First pull-based install: one outcome mail, version header, `current` symlink, adopted release kept | MH-4/5 | Real release + approval | Approve `deploy` env; watch poll |
| Idle poll and remembered-bad-tag send no mail | MH-4 | Real timer + relay | Observe journal and inbox across two polls |
| Runner removed from GitHub and CT | MH-5 | Operator-owned credentials | `gh api repos/{owner}/{repo}/actions/runners` → 0; `systemctl list-units 'actions.runner*'` empty |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
