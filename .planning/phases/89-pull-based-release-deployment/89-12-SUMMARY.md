---
phase: 89-pull-based-release-deployment
plan: 12
subsystem: deploy
tags: [runner-retirement, github, verification, mail-budget]
requires: [89-11]
provides: [no-github-handle-on-production, idle-poll-silence-confirmed]
key-files:
  created: []
  modified: []
duration: operator handover session
completed: 2026-10-05
---

# Phase 89 Plan 12: Retire the push path Summary

GitHub no longer holds any runner, credential or workflow that can reach the production box,
confirmed from GitHub, the CT and the repository, and the poll is silent when idle.

## Task 1 — retire the runner and the old deploy pieces

Run by the orchestrator under the temporary root grant (see 89-11), with the operator's go-ahead.

1. GitHub: runner id 21 `QuestBoard` (labels `self-hosted,Linux,X64`, online, not busy) removed
   with `gh api --method DELETE repos/theunschut/quest-board-dnd/actions/runners/21`; registered
   runners: 0.
2. CT: `./svc.sh stop` and `./svc.sh uninstall` from `/home/questboard/actions-runner`
   (unit `actions.runner.theunschut-dnd-quest-board.QuestBoard.service` stopped and removed).
3. `rm -rf /home/questboard/actions-runner` (2.2 GB); `/home/questboard` and its `.aspnet` Data
   Protection keys kept.
4. `rm /etc/sudoers.d/questboard /home/questboard/deploy.sh`; `visudo -c`: `/etc/sudoers: parsed OK`.

Also cancelled run 29313719417 (`.NET CI`, main), queued since 2026-07-14 with no jobs, which the
operator saw as GitHub still waiting for a runner; nothing is queued or waiting afterwards.

## Task 2 — two-sided verification

| Side | Command | Output |
|---|---|---|
| GitHub | `gh api …/actions/runners --jq .total_count` | `0` |
| GitHub | `bash build/check-github-settings.sh` | 4× PASS (tag ruleset, reviewer, tag policy, no self-hosted runner), exit 0 |
| CT | `systemctl list-units --all --no-legend 'actions.runner*' \| wc -l` | `0` |
| CT | `systemctl list-unit-files --no-legend 'actions.runner*' \| wc -l` | `0` |
| CT | `test -d /home/questboard && echo home-present` | `home-present` |
| CT | `systemctl show -p ActiveState questboard.service` | `ActiveState=active` |
| CT | `curl … /health` | `200` |
| Repo | `git grep -nE '^\s*(runs-on:.*\|-\s*)self-hosted\b' origin/main -- .github/workflows` | no output |
| Repo | `git cat-file -e origin/main:.github/workflows/binary-release.yml` | fails (absent) |

CT checks ran as the unprivileged `claude` account. Org-level runners could not be listed (the
operator's token lacks `admin:org`); the retired runner was repository-scoped.

## Task 3 — mail budget over idle polls

- Journal since the install, three timer runs:
  `16:22:35Z nothing newer than 5.4.1`, `16:28:40Z nothing newer than 5.4.1`,
  `16:34:25Z nothing newer than 5.4.1`; no outcome line; attempts unchanged; `Result=success`.
- Next run scheduled about five minutes ahead.
- Inbox: the operator received the single "[questboard-deploy] installed v5.4.1" mail.

## Deviations

- Task 1 ran under the temporary root grant instead of by the operator (operator's request); the
  grant was removed afterwards and a root login shown to be refused.
- The stale queued run was cancelled (not in the plan; operator asked that GitHub stop waiting on a
  runner).

## Self-Check: PASSED
