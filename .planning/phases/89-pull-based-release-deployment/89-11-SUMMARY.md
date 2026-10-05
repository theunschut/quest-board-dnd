---
phase: 89-pull-based-release-deployment
plan: 11
subsystem: deploy
tags: [cutover, production, adoption, migrator, backup]
requires: [89-10]
provides: [versioned-layout-in-production, first-pull-based-install, rehearsal-backup]
key-files:
  created: []
  modified:
    - deploy/lib/setup.sh
    - deploy/tests/setup-logic-test.sh
duration: operator handover session
completed: 2026-10-05
---

# Phase 89 Plan 11: Production cutover Summary

Production (CT 102 `QuestBoard`) moved from the flat, push-deployed install to the versioned,
pull-based layout. The adopted 5.3.3 is kept as the rollback target, and the first attested
release, **v5.4.1**, installed itself through the sandboxed poll unit.

## How the privileged steps ran

The plan assigns every privileged step to the operator. On the operator's request the operator
granted a temporary root SSH login on CT 102 (the existing `claude` key, `from="192.168.1.140"`,
`expiry-time` 23:59 the same day, marked `claude-temp-root-89`). The orchestrator ran the App CT
steps as root, paused for the operator's confirmation before adopting, never printed
`/etc/questboard/env` beyond the three relay keys, then removed the grant line and proved a root
login is refused while the unprivileged `claude` account still works. The SQL CT (CT 103) steps
were run by the operator.

## Task 1 — workstation verification and pre-cutover snapshot

- `build/verify-published-release.sh v5.4.1`: all checks passed, attested commit `c4785470` on
  main, tampered copy refused.
  `sha256 questboard-v5.4.1.zip 9f3f52d1ae84c301f4a07121029f501f9c8a40ad91e0aeea5f051632c618c7b9`
- Snapshot before cutover: `questboard.service` active, `WorkingDirectory=/opt/questboard`,
  `ExecStart=/usr/bin/dotnet /opt/questboard/QuestBoard.Service.dll`, no drop-ins; `/health` 200
  without a version header; 77 entries in the flat `/opt/questboard` (owned `questboard`); 4.0 GB
  free; runner unit active; no `gh`; site footer `v5.3.3`.
- No user data under `/opt/questboard`: images live in the database, nothing written there since
  the 30 Sep deploy.

## Task 2 — cutover

- v5.4.0 setup could not adopt: `could not read exactly one running version` (nothing changed).
  Cause and fix below; the cutover used **v5.4.1** instead.
- v5.4.1 `setup` (detect): `Running version detected: 5.3.3`, exit 2, nothing changed. Matches the
  footer locally and publicly.
- `setup --confirm-adopt-version 5.3.3` first died installing gnupg (stale apt lists from
  2026-06-23 → 404), before changing anything. After `apt-get update` on the CT it succeeded:
  installed gh 2.102.0 (gnupg pulled in 12 packages and upgraded `gpgv`), created
  `/etc/questboard/deploy.conf`, adopted the flat install as release 5.3.3, app healthy after
  ~5 s; exit 3 (config edit requested).
- `deploy.conf`: recipient `theunschut@gmail.com`, sender `questboard@theunschut.com` (the app's
  own sender), relay `192.168.6.13:25`, as read from the app's env.
- Re-running setup enabled `questboard-deploy-poll.timer`, which fired at once:
  `outcome=installed tag=v5.4.1 reason=none` at 16:17:40Z through the sandboxed unit (~4 s app
  restart). This proves `systemd-run` from the sandbox and gh verification on the CT.
- Migrator `status`: 43 applied, `pending` [], `unknown` [], `canBackup: true`.
- Backup rehearsal: `questboard-premigration-rehearsal-20261005T162117Z.bak` written; on the SQL CT
  it is present (70 MB, `mssql:mssql`) in `/var/opt/mssql/data` (no `defaultbackupdir` set, 27 GB
  free). Prune script installed at `/usr/local/sbin/prune-premigration-backups.sh`, ran with exit 0
  (nothing to prune), weekly cron `/etc/cron.d/questboard-prune-backups` added.
- Mail: the operator received "[questboard-deploy] installed v5.4.1".

## Task 3 — post-cutover verification

| Check | Result |
|---|---|
| `readlink /opt/questboard/current` | `/opt/questboard/releases/5.4.1`; releases: `5.3.3`, `5.4.1`; `state/previous` = 5.3.3 |
| Ownership | `/opt/questboard`, `releases`, both release trees, `5.4.1/app`: `root:root 755`; nothing group/other-writable |
| `deploy.conf` / installer | `root 600` / `root 755` |
| `questboard.service` | active, `WorkingDirectory=/opt/questboard/current/app`, `DropInPaths=…/10-release-layout.conf` |
| `/health` | `HTTP/1.1 200 OK`, `X-QuestBoard-Version: 5.4.1` |
| Poll timer / last poll | active + enabled / `Result=success` |
| attempts | `v5.3.3 adopted`, `v5.4.1 installed` |
| gh | 2.102.0 |

## Deviations

1. **[Rule 1] Flat-version detection never matched a real build.** The informational version
   `5.3.3+<40 hex>` is 46 characters, so its attribute record's length byte is 0x2E ("."), which
   the detector's lookbehind rejected. Fixed to match the attribute record structurally (prolog,
   length byte equal to the string length, two zero bytes); tests now use the real layout.
   PR theunschut/quest-board-dnd#157, released as **v5.4.1**.
2. **[Rule 1] Stale apt lists broke the gnupg install.** setup refreshed lists before installing
   gh but not before gnupg. Fixed in `questboard_setup_install_gh` with a regression test that hides
   any real gpg (commit `7d1424ba`, ships with the next release). On production the lists were
   refreshed by hand first.
3. **Privileged steps run by the orchestrator under a temporary, operator-granted root login**
   (see above) instead of by the operator, at the operator's request.
4. The poll timer fired on enable, so the first install came from the timer rather than a manual
   `systemctl start`; same unit, same sandbox.

## Follow-ups

- `/root/setup-adopt.log` and `/root/setup-final.log` remain on CT 102 (apt and installer output, no
  secrets) for the operator to delete.

## Self-Check: PASSED
