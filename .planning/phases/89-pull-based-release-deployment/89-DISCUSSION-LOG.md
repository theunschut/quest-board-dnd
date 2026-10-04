# Phase 89: Pull-Based Release Deployment - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-04
**Phase:** 89-pull-based-release-deployment
**Areas discussed:** Migration runner, Release trust & gate, Poll cadence & reporting, Rest of migration/rollback, Server layout & cutover

---

## Migration runner

The operator answered the area-selection prompt with a question instead of picking areas: "Isn't
there a way to run the migration first without starting the app? Perhaps an EF command or a very
small dedicated dotnet program to just do that?" Claude laid out three approaches, then asked.

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated migrator | New QuestBoard.Migrator console app with `status`/`apply`, shipped in each release; installer knows exactly what is pending/applied; startup Migrate() stays as a no-op | ✓ |
| EF migration bundle | `dotnet ef migrations bundle`; standard tooling, but cannot report pending/applied state; needs sqlcmd + sa creds or log parsing for the rollback decision | |
| --migrate-only app mode | No new project; CLI branch in Program.cs; builds full DI (Hangfire etc.) just to migrate | |

**User's choice:** Dedicated migrator.

---

## Release trust & gate

| Option | Description | Selected |
|--------|-------------|----------|
| Attestation + checksum | attest-build-provenance in CI; installer checks sha256 then `gh attestation verify --bundle` pinned to repo/workflow/tag, deny self-hosted; no bypass | ✓ |
| Checksum only | Catches transfer corruption only; forgeable by anyone able to upload release assets | |

| Option | Description | Selected |
|--------|-------------|----------|
| Draft + approval | Tag → draft release; approving the `deploy` environment re-verifies and publishes; operator writes title/notes on the draft | ✓ |
| Push a tag, it ships | Published immediately; server installs within one poll interval | |

| Option | Description | Selected |
|--------|-------------|----------|
| Strict semver + on main | Exact vX.Y.Z, commit reachable from main, no overwrite of a published release; lightweight tags allowed | ✓ |
| Strict semver only | Shape + no overwrite; any branch | |
| Keep today's filter | Only the `v*.*.*` trigger glob | |

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, gate on tests | Release job runs `dotnet test` before attesting | ✓ |
| No, rely on main CI | Tags are on main merge commits that already ran dotnet.yml | |

**Notes:** Claude observed that `dotnet.yml` sets up .NET 8.0.x for a .NET 10 solution — recorded as an observation, not scope.

---

## Poll cadence & reporting

| Option | Description | Selected |
|--------|-------------|----------|
| Every 5 min | 2 min after boot, then 5 min with ~30 s jitter; 12 calls/hour | ✓ |
| Every 15 min | Gentler on the shared 60/hour unauthenticated limit | |
| Every 2 min | 30 calls/hour; ~42/60 with ing-dashboard on the same IP | |

| Option | Description | Selected |
|--------|-------------|----------|
| Every install outcome | One mail per install attempt; idle polls and GitHub-unreachable to journal only | ✓ |
| Failures only | Silent on success | |
| Journal only | No email at all | |

| Option | Description | Selected |
|--------|-------------|----------|
| Remember & skip it | State file records tag + outcome; later polls skip it until a newer tag or manual `install <tag>`; one mail per bad release | ✓ |
| Retry with backoff | A few spaced retries, then remember | |
| Halt all polling | Any failure disables the timer | |

| Option | Description | Selected |
|--------|-------------|----------|
| No — email + journal | Nothing scrapes the CT today | ✓ |
| Yes, textfile metrics | ing-dashboard's gauges into a textfile-collector dir | |

---

## Rest of migration/rollback

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, COPY_ONLY first | Backup only when migrations pending; failure aborts before touching the app; keep last few | ✓ |
| No, existing backups suffice | Installer doesn't touch backups | |

| Option | Description | Selected |
|--------|-------------|----------|
| Stop first, then migrate | Brief downtime; old code never runs against an unknown schema | ✓ |
| Migrate while old runs | Near-zero downtime; only safe for backward-compatible migrations | |

Partial-migration question — options presented: "Leave it stopped" / "Start the old version anyway".
**User's response (free text):** "Is it possible the migration is run in a transaction somehow? When
it fails it rolls back, and it's safe to launch the previous version right? Or is that not possible?"
Claude checked the EF Core docs: EF Core 9 applied all pending migrations in one transaction, EF Core
10 (this project, 10.0.9) reverted to per-migration transactions; the dedicated migrator can opt back
in with its own transaction. None of the 44 existing migrations contain non-transactional steps.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, one transaction | Migrator wraps all pending migrations in one transaction; refuses non-transactional steps; partial state can't occur | ✓ |
| No, stock EF 10 behaviour | Per-migration transactions; partial failure leaves the app stopped | |

| Option | Description | Selected |
|--------|-------------|----------|
| Leave new release, halt | Keep new release active, email with backup name, operator decides | ✓ |
| Stop the app, halt | Same, but site down until acted on | |
| Auto-restore backup + roll back | Self-healing but riskiest to automate | |

**Notes:** Manual `rollback <version>` default accepted without further questions — only to an on-disk release whose own migrator confirms the DB holds no unknown migrations; restore stays a manual SQL step.

---

## Server layout & cutover

| Option | Description | Selected |
|--------|-------------|----------|
| releases/<v> + current link | Atomic switch, multi-step rollback; unit paths change to /opt/questboard/current | ✓ |
| Flat dir + one previous copy | No unit change; one rollback step; non-atomic swap | |

Env file question — options: `/etc/questboard/env` / `/etc/questboard/.env` / "Not sure — plan checks".
**User's response (free text):** asked Claude to find the location itself over SSH, without reading
the file because of passwords. Claude proposed an unprivileged `claude` account (no sudo, not in the
`questboard` group, `restrict` key) so the kernel denies reading the 600 env file. The operator
named the key to match existing ones (`~/.ssh/questboard-lxc-claude`, comment
`claude@questboard-lxc`) and used the remote user `claude` like the other CTs. The operator also
asked to record every CT's address in `/mnt/Data/repos/brainfarts/environment.md`; Claude filled it
from the operator's `pct list` output and committed it there. Lookup result: `/etc/questboard/env`
(no dot) — the docs were right, the roadmap and PROJECT.md wrong. The same lookup found the runner
still active (registered under the old repo name), `gh`/`jq`/`sqlcmd` absent, 4.0 GB free disk, and
a local Postfix with an empty relayhost.

| Option | Description | Selected |
|--------|-------------|----------|
| Root, sandboxed | Root-owned installer, root systemd oneshot with hardening; release dirs read-only to the app user; sudoers entry removed | ✓ |
| questboard user + sudoers | App user owns its code and the installer | |

(This question was first dismissed by the operator mid-SSH-setup, then re-asked and answered.)

| Option | Description | Selected |
|--------|-------------|----------|
| Manual setup, with a nudge | Never self-rewrites; outcome email flags when the active release's deploy/ differs | ✓ |
| Auto self-update after healthy install | Zero manual steps; a buggy installer breaks later automatic deploys | |

| Option | Description | Selected |
|--------|-------------|----------|
| Adopt it as releases/5.3.3 | Running flat install becomes the first release dir ("adopted, not verified") so the first pull install has a rollback target | ✓ |
| Fresh install, keep a cold copy | Every on-disk release attested; no auto-rollback target on day one | |

| Option | Description | Selected |
|--------|-------------|----------|
| Right after first pull install | Handover checklist after the first healthy pull install; verify gone from GitHub and CT | ✓ |
| Before the first pull install | Closes exposure sooner; no push fallback if pull fails on day one | |

| Option | Description | Selected |
|--------|-------------|----------|
| Same relay as the app | SMTP to the relay the app uses (likely Postfix CT .13); configurable; handover confirms | ✓ |
| Local sendmail on the App CT | Local Postfix has an empty relayhost — would try direct MX | |
| Not sure — handover finds out | Configurable, checked at handover | |

---

## Claude's Discretion

- How the installer confirms the *new* version answers (e.g. version in `/health` or a header)
- Script/unit/config/state names and locations, retention count, health timeout, asset names
- How the migrator receives the connection string without spreading the secret
- Bash vs Python helpers; JSON parsing via `python3` or `jq` installed by `setup`
- Installer test strategy (ing-dashboard's logic-test and tampered-artifact patterns as reference)
- Replace `binary-release.yml` in place vs a new `release.yml`
- New `docs/deploy.md`/`docs/releasing.md` vs sections in `server-setup.md`

## Deferred Ideas

- Deploy metrics / Prometheus textfile gauges
- Automated restore from the pre-migration backup
- Observation: `dotnet.yml` uses .NET 8.0.x setup for a .NET 10 solution
- Observation: `dotnet-ef` tool manifest pinned to 9.0.6 vs EF Core 10.0.9
- Observation: `server-setup.md` env example still shows Gmail SMTP settings
