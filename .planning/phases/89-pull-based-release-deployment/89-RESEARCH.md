# Phase 89: Pull-Based Release Deployment - Research

**Researched:** 2026-10-04
**Domain:** Release engineering and server-side deployment (GitHub Actions provenance attestation, bash installer under systemd, EF Core 10 migration atomicity on SQL Server, SMTP outcome reporting)
**Confidence:** HIGH for the EF Core, `gh attestation`, SMTP and server-environment findings (each verified this session by reading source, running the tool, or inspecting the CT read-only). MEDIUM for the systemd-sandbox interplay and the SQL-CT backup behaviour (reasoned or documented, not exercised on the real hosts).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

## Implementation Decisions

### Production facts, verified 2026-10-04 over SSH

These were read off the App CT (Proxmox CT 102 `QuestBoard`, `192.168.6.12`) with an unprivileged
account that cannot read the env file. They override anything in older docs.

- **The env file is `/etc/questboard/env`, with no leading dot.** It is mode `600` and owned by
  `questboard:questboard`. `docs/server-setup.md` is correct. ROADMAP.md (Phase 89 scope notes) and
  PROJECT.md line 163 say `/etc/questboard/.env`, which is wrong; correct PROJECT.md as part of this
  phase.
- **`questboard.service`** lives at `/etc/systemd/system/questboard.service`. It runs as
  `User=questboard` with `WorkingDirectory=/opt/questboard`, executes
  `ExecStart=/usr/bin/dotnet /opt/questboard/QuestBoard.Service.dll`, and loads
  `EnvironmentFile=/etc/questboard/env`.
- **`/opt/questboard` is flat:** 77 entries owned by `questboard`.
- **The runner is live:** unit `actions.runner.theunschut-dnd-quest-board.QuestBoard.service` is
  active. It is registered under the repo's *old* name, `dnd-quest-board`.
  `/etc/sudoers.d/questboard` exists. `/home/questboard` is not listable by the inspection
  account, which matches the documented `deploy.sh` and `actions-runner/` living there.
- **Tools:** `curl`, `wget`, `unzip`, `/usr/sbin/sendmail` and `python3` are present. **`gh` and
  `jq` are not installed. `sqlcmd` is not installed.**
- **Runtime:** `Microsoft.AspNetCore.App` and `Microsoft.NETCore.App` 10.0.9. Ubuntu 24.04.4 with
  2 cores, 4 GB RAM, and a 7.8 GB root disk with 4.0 GB free.
- **Health:** `curl localhost:5000/health` returns `Healthy` with HTTP 200. The CT reaches
  `api.github.com/repos/theunschut/quest-board-dnd/releases/latest` (HTTP 200, unauthenticated).
- **Mail:** a local Postfix is active on the App CT with an **empty `relayhost`**. The app's own
  SMTP settings sit in the env file and were not read. Mail to Resend normally goes through the
  **Postfix CT (CT 105, `192.168.6.13`)**.
- **Neighbours:** SQL Server is CT 103 at `192.168.6.10`. Traefik is CT 101 at `192.168.6.8`.

### Release pipeline (GitHub side)

- **D-01: Provenance attestation plus checksum.** The release build runs on a GitHub-hosted runner
  and runs `actions/attest-build-provenance` on the release zip. The release carries three assets:
  the zip, a `.sha256` and a `.sigstore.json` bundle. The installer checks the checksum first,
  which is a cheap corruption check. It then runs `gh attestation verify --bundle` pinned to:
  - this repo,
  - the release workflow file as signer,
  - `refs/tags/<tag>` as source ref,
  - `--deny-self-hosted-runners`.

  The installer refuses on any failure. There is **no bypass flag, environment variable or
  fallback**. A verification service it cannot reach counts the same as a failed verification.
- **D-02: Draft release plus a `deploy` environment approval gate.** A tag push produces a
  **draft** release. Approving the `deploy` environment runs a publish job that re-downloads the
  draft, re-checks the checksum and the attestation, and publishes the release with `--latest`.
  The server polls `releases/latest`, which never returns drafts, so nothing installs before
  approval. The operator writes the release title and notes on the draft before approving; they
  hand-write them today, e.g. "v5.3.3 - Rescheduled game nights update in your calendar". Creating
  the `deploy` environment with the operator as required reviewer is an operator step for the
  handover.
- **D-03: Tag validation refuses:**
  - anything that isn't exactly `vX.Y.Z` (no leading zeros, no `-rc`/`+build` suffix),
  - a tag whose commit isn't reachable from `main`,
  - overwriting an already-published release.

  **Lightweight tags stay allowed.** Every release so far (v5.3.0–v5.3.3) is a lightweight tag on
  a `main` merge commit, so this matches current practice.
- **D-04: The release job runs the full `dotnet test` before it attests.** Integration tests use
  EF InMemory and need no database. The attested zip is therefore the exact build that passed.
- **D-05: The push path is removed completely.** Delete the `deploy` job and the
  `workflow_dispatch` tag input from `binary-release.yml`, or replace the file outright. After
  this phase no workflow in the repo targets `self-hosted`. A manual redeploy becomes
  `install <tag>` on the server. `docker-publish.yml` is untouched; it still publishes the GHCR
  image on every tag push regardless of approval, and that is acceptable.

### Migrations: a dedicated migrator, applied atomically before start

- **D-06: Add a new `QuestBoard.Migrator` console project, shipped inside every release zip.**
  - It references `QuestBoard.Repository`. It must **not** add its own EF Core
    `PackageReference`s; the project rule is that EF packages live only in Repository, and
    reaching them transitively is fine.
  - It reads the same configuration key as the app: `ConnectionStrings:DefaultConnection`,
    supplied as `ConnectionStrings__DefaultConnection` from `/etc/questboard/env`.
  - `QuestBoardContext` takes `IActiveGroupContext`, so the migrator supplies a stub.
  - Commands:
    - **`status`** lists applied and pending migration ids. It exits non-zero when the database
      holds a migration this build does not know, i.e. the database is ahead of the code.
    - **`apply`** applies all pending migrations (see D-07).
    - **Backup** (D-09) runs through the same connection, as part of `apply` or as its own
      command — planner's call.

  Each release carries its own migrator. Asking "can I roll back to X?" therefore means running
  X's own `status`; no separate migration manifest is needed. **Rejected:** an EF migration bundle,
  because it can only apply and cannot report pending or applied state, so the rollback decision
  would need `sqlcmd` plus `sa` credentials on the App CT or parsing log output. Also rejected: a
  `--migrate-only` mode in the app, which would put a command-line branch in `Program.cs` and
  build the full service graph, Hangfire included, just to migrate.
- **D-07: All pending migrations in a release apply in one transaction, all or nothing.**
  - **EF Core 10, which this project uses (10.0.9), applies each migration in its own
    transaction.** EF Core 9 applied the whole batch in one transaction; EF 10 reverted that. So
    stock `Migrate()` can leave a release half-applied.
  - The migrator opens its own transaction, applies, and commits. On failure everything rolls
    back.
  - It suppresses `RelationalEventId.MigrationsUserTransactionWarning` **in the migrator only**.
  - Supplying our own transaction bypasses EF's lock against concurrent migrations. That is safe
    here because the app is stopped and the installer holds its own lock.
  - **The migrator refuses to apply when any pending migration contains a non-transactional
    operation**: `suppressTransaction: true`, full-text index or catalog, `ALTER DATABASE`, or
    memory-optimized tables. Otherwise the guarantee could silently weaken. None of the 44
    current migrations contain one.
  - This must be proven against a real SQL Server, because InMemory cannot run migrations. See
    research items.
- **D-08: The startup `context.Database.Migrate()` in
  `QuestBoard.Repository/Extensions/ServiceExtensions.cs` stays unchanged.** On the server it is
  a no-op, because nothing is pending by the time the app starts. Docker and dev keep migrating on
  startup with no new setup step, which satisfies the self-hosting constraint.
- **D-09: Back up the database before migrating, only when migrations are pending.** The
  migrator runs `BACKUP DATABASE … WITH COPY_ONLY` on the SQL CT, named after the release and a
  UTC timestamp. `COPY_ONLY` leaves any existing backup chain alone. **If the backup fails, the
  install aborts before the running app is touched.** Keep the last few pre-migration backups.
  How pruning works (the files sit on the SQL CT's disk, not the App CT's) is a research item;
  if there is no clean way, document manual pruning.
- **D-10: Install order:**
  - With migrations pending: `status` → backup → **stop the app** → `apply` (atomic) → switch
    `current` → start → health check.
  - With nothing pending: stop → switch → start → health check.

  The old code never runs against a schema it doesn't know, and no Hangfire job fires
  mid-migration. Seconds to a minute of downtime is acceptable; today's stop, unzip and start
  has the same.
- **D-11: Outcome matrix.** Every row sends one email (D-14) and records the tag (D-15).

  | Situation | Action | Reported as |
  |---|---|---|
  | Checksum or attestation fails | Nothing on disk or in the service changes | refused |
  | Database holds migrations unknown to this release | Refuse install | refused |
  | Backup fails | Abort before stopping the app | failed |
  | `apply` fails | Transaction rolled back, database unchanged; restart the previous release | failed, rolled back |
  | No migrations; new release unhealthy | Switch `current` back, restart previous, confirm healthy | rolled back |
  | Migrations committed; new release unhealthy | **Leave the new release active** (systemd keeps retrying it, in case it's transient); no automatic restore | halted — migrations applied, backup `<name>` |
- **D-12: Manual `rollback <version>`** works only to a release still on disk. It is refused when
  that release's own migrator `status` says the database holds migrations it doesn't know.
  Restoring a backup stays a **documented manual SQL step**. There is no automated restore
  command.

### Polling and reporting

- **D-13: Poll timer:** `OnBootSec=2min`, `OnUnitActiveSec=5min`, `RandomizedDelaySec=30`, the
  same as ing-dashboard. The poll reads the public `releases/latest` endpoint **without a token**.
  The unauthenticated limit is 60 requests an hour per public IP, possibly shared with the Ledger
  CT (CT 108, `192.168.6.16`), which polls the same way. 12 per hour each fits.
- **D-14: Email on every install outcome**: installed, rolled back, halted, refused, failed. The
  body holds the version, the result and timestamps only: **no paths, connection strings or key
  material**. Idle polls ("nothing newer") and "GitHub unreachable" go to the journal only. The
  Resend relay's 100-per-day limit is shared with the whole board, so the poll must never mail
  on its own.
- **D-15: Remember and skip.** The installer records each attempted tag and its outcome in a
  state file. Later polls skip a tag that was refused, failed, rolled back or halted, writing one
  journal line and **no email**, until a newer tag appears or the operator runs
  `install <tag>` by hand. Each bad release costs exactly one email.
- **D-16: No metrics.** Email plus journal only. Nothing scrapes this CT today.
- **D-17: Mail goes through the same relay the app uses.** Most likely that is the Postfix CT at
  `192.168.6.13`, which forwards to Resend. The installer speaks SMTP to it; `curl` is available
  for this, while `msmtp` and `mail` are not. **Do not rely on the App CT's local `sendmail`**:
  its Postfix has an empty `relayhost`, so it would attempt direct MX delivery. Relay host, port,
  sender and recipient live in the installer's config file. The handover confirms the relay value
  with the operator, because the app's SMTP settings are inside the env file Claude cannot read.

### Server layout, installer identity, cutover

- **D-18: Versioned layout inside the existing root:** `/opt/questboard/releases/<version>/`,
  plus a `/opt/questboard/current` symlink switched atomically.
  - `questboard.service` changes `WorkingDirectory` and `ExecStart` to point through `current`.
  - The service name, `User=questboard` and `EnvironmentFile=/etc/questboard/env` stay as they
    are.
  - Keep the active release, the previous one and a few more. There is 4.0 GB free, so roughly
    5 total is comfortable.
  - **Rejected:** a flat directory plus one `.previous` copy, which allows only one rollback
    step and swaps non-atomically.
- **D-19: The installer runs as root, sandboxed.**
  - It is a root-owned script in `/usr/local/sbin`, plus a library directory if needed.
  - A root systemd oneshot runs it, with `NoNewPrivileges`, `ProtectSystem=strict` plus
    `ReadWritePaths`, `PrivateTmp` and the rest of ing-dashboard's hardening set.
  - **Release directories are root-owned and read-only to the `questboard` app user**, so a
    compromised app cannot rewrite its own code or the installer.
  - The app keeps running as `questboard`.
  - `/etc/sudoers.d/questboard` and `/home/questboard/deploy.sh` are no longer needed.
- **D-20: Subcommands:**
  - `poll`: what the timer runs.
  - `install <tag>`: manual install or redeploy. It is also the override for a remembered tag
    (D-15).
  - `rollback <version>`
  - `verify --artifact --bundle --tag`: verification only, installs nothing.
  - `setup`: the one-time bootstrap, and the way to apply a newer installer or newer units.
- **D-21: No self-update.** The installer never rewrites itself. After a healthy install it
  compares the active release's `deploy/` files with the installed copies. If they differ, the
  outcome email says "installer update available — run `setup` from `<version>`".
- **D-22: Cutover adopts the running install.** `setup` installs its prerequisites:
  - `gh` from GitHub's official apt repository,
  - `jq` only if the design needs it (`python3` is already present).

  It then moves the current flat `/opt/questboard` contents into
  `/opt/questboard/releases/<running version>`. **It reads the running version from the
  installed assembly rather than assuming 5.3.3.** It points `current` at that directory, rewrites
  the unit and restarts, with no version change at that moment. That release predates
  attestation, so it is recorded as **"adopted, not verified"**, which is the same trust as
  today. The first pull-based install is then the release that ships this phase, and it has a
  real previous release to roll back to automatically.
- **D-23: Remove the runner immediately after the first pull-based install reports healthy.**
  Explicit operator handover checklist:
  1. Deregister runner `theunschut-dnd-quest-board.QuestBoard` in GitHub → Settings → Actions →
     Runners.
  2. Stop and uninstall its service (`svc.sh uninstall`).
  3. Delete `/home/questboard/actions-runner`.
  4. Remove `/etc/sudoers.d/questboard` and `/home/questboard/deploy.sh`.

  Verification confirms the runner is gone **from both GitHub** (the repo has zero registered
  runners) **and the CT** (no `actions.runner*` unit). While the runner stays registered, a later
  workflow could still target it.

### Documentation

- **D-24: Rewrite the deploy-related parts of `docs/server-setup.md`.** The roadmap said
  "sections 3–5", but in the actual file §3 is Traefik and §4 is DNS, neither of which is
  deploy-related. The parts that really change are:
  - the intro and architecture diagram ("runner lives on the App CT"),
  - §1's "Create the deploy script", "Allow questboard to restart the service", "Create the
    systemd service" and "Install the GitHub Actions runner",
  - §5 "Deploying",
  - "Checking logs", which has a runner-logs line.

  Also document how to cut a release and how a deploy behaves: manual commands, the outcome
  matrix, and why rollback sometimes stops. Model this on ing-dashboard's `docs/releasing.md`
  and `docs/deploy.md`; whether that becomes new docs or sections in `server-setup.md` is
  Claude's call. Fix the `.env` → `env` path in PROJECT.md.

### Claude's Discretion

- **Version check:** how the installer confirms the **new** version is the one answering, not
  merely that `/health` says `Healthy`. Today `/health` doesn't expose the version, while
  `AppVersion.Current` does. Adding the version to the health response or a response header is
  acceptable. The footer already shows the version publicly, so this leaks nothing new.
- **Names and locations:**
  - script and unit names (e.g. `questboard-deploy`, `questboard-deploy-poll.{service,timer}`),
  - config file location (e.g. `/etc/questboard/deploy.conf`, root-owned `600`),
  - state directory (e.g. `/var/lib/questboard-deploy`),
  - the health timeout and the exact retention count,
  - release asset names. Today's asset is `questboard-<tag>.zip`, and older releases (≤ v5.3.3)
    have no attestation, so the installer must never be asked to auto-install one.
- **Running the migrator:** how it gets the connection string without spreading the secret. For
  example, run it as `questboard` via `systemd-run`/`runuser` with `EnvironmentFile=`, rather
  than having the root installer parse the env file.
- **Shell vs Python:** bash like ing-dashboard is the default. Parse the GitHub API JSON with
  `python3`, or install `jq` in `setup`.
- **Tests:** the test strategy for the installer logic. ing-dashboard's
  `deploy/tests/*-logic-test.sh` and `verify-rejects-tampered-artifact-network-test.sh` pattern is
  the reference. A tampered-artifact refusal test is expected.
- **Workflow file:** replace `binary-release.yml` in place, or add a new `release.yml` and delete
  it.

### Deferred Ideas (OUT OF SCOPE)

- **Deploy metrics / Prometheus textfile gauges.** Declined for now (D-16). Revisit if this CT
  ever joins the monitoring stack.
- **Automated restore from the pre-migration backup.** Declined as too risky to automate (D-11,
  D-12). It stays a documented manual step.
- **Observation, not scope:** `.github/workflows/dotnet.yml` sets up .NET **8.0.x** for a .NET 10
  solution. It probably works only because runner images ship newer SDKs.
- **Observation, not scope:** `.config/dotnet-tools.json` pins `dotnet-ef` 9.0.6 while EF Core is
  10.0.9. This only affects developer migration commands. The migrator makes an EF bundle
  unnecessary.
- **Observation, not scope:** the old `server-setup.md` env example still shows Gmail SMTP
  settings. It gets rewritten here only where it touches deployment.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs are mapped to this phase (ROADMAP: "Requirements: TBD"). The must-haves below are derived from the phase goal and the locked decisions. They are planning handles only (MH-n), not REQUIREMENTS.md IDs.

| ID | Description | Research Support |
|----|-------------|------------------|
| MH-1 | Hosted-runner release workflow: validate tag, build, full test, package, attest, draft; `deploy`-environment publish job re-verifies and publishes with `--latest`; no `self-hosted` anywhere (D-01..D-05) | Section "Release workflow", Code Examples 8-9, Pitfalls 9-11 |
| MH-2 | `QuestBoard.Migrator` with `status`, `backup`, `apply`; atomic apply in a caller transaction; refuse non-transactional operations; refuse database-ahead (D-06, D-07, D-09) | EF 10.0.9 source findings, Code Examples 1-3, Pitfalls 1-4 |
| MH-3 | Version-confirming health check: `/health` exposes `AppVersion.Current` via a response header (Claude's discretion item) | Code Example 4, Pitfall 12 |
| MH-4 | Root-owned sandboxed installer: poll / install / rollback / verify / setup with the D-11 outcome matrix, D-15 remember-and-skip, D-14 email-on-outcome only (D-13..D-21) | Architecture Patterns, Code Examples 5-7, Pitfalls 5-8 |
| MH-5 | Cutover: `setup` adopts the running install into `releases/<version>/`, installs `gh`, units, config; runner removal handover with two-sided verification (D-22, D-23) | Bootstrap sequence, Runtime State Inventory, Pitfall 13 |
| MH-6 | Verification evidence: DB-less migrator guard test, gated real-SQL atomicity test, installer logic tests, tampered-artifact refusal test, workflow lint, CT UAT (Validation Architecture) | Validation Architecture |
| MH-7 | Docs: deploy-related parts of `docs/server-setup.md` rewritten, new deploy/release docs, PROJECT.md `.env` -> `env` fix (D-24) | Standard Stack, Open Questions |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Branching:** never commit to `main`; work stays on `milestone/v9-rolling-improvements` or a feature branch. [VERIFIED: CLAUDE.md "Branching"]
- **Local database contract:** SQL Server is expected at `localhost:1433`; do not install, start or provision it; integration tests use EF InMemory and need no database. This session confirmed `localhost:1433` refuses connections, so a database-backed test must be skipped by default and may only run where a server is supplied (CI service container). [VERIFIED: CLAUDE.md "Local database"; `/dev/tcp/localhost/1433` refused]
- **EF packages only in `QuestBoard.Repository`:** `QuestBoard.Migrator` must not declare any EF `PackageReference`; reaching EF through the Repository `ProjectReference` is allowed. [VERIFIED: `.claude/architecture.md`, CONTEXT D-06]
- **No planning/tracking references in source:** no `D-07`, `Phase 89`, `MH-n`, `RESEARCH.md` in comments, XML docs or string literals of any source file, which includes `deploy/**` shell scripts, systemd units, workflow YAML and migrator code. Commit messages may carry them. [VERIFIED: CLAUDE.md "Code Comments"]
- **Migrations auto-apply on startup stays** (`context.Database.Migrate()` in `ServiceExtensions.ConfigureDatabase`). Docker and dev keep working with no new setup step. [VERIFIED: `.claude/architecture.md`, `ServiceExtensions.cs`]
- **Writes to `Events`/`Quests` go through the change tracker** (feed revision guard). The migrator does not write those tables; existing data-backfill migrations are unaffected. [VERIFIED: `.claude/architecture.md`]
- **Time:** server-side "what day is it" reads go through `IBoardClock`. The migrator's UTC timestamp for a backup name is a real instant, not a board date; `AmbientClockSeamTests` guards a closed list of named files and does not scan a new project. Prefer `TimeProvider.System.GetUtcNow()` over `DateTime.UtcNow` anyway. [VERIFIED: `AmbientClockSeamTests.cs` lines 1-60]
- **Compatibility / Docker:** `Dockerfile`, `docker-compose.yml`, `docker-publish.yml` stay as they are. The Dockerfile copies three project files explicitly and restores `QuestBoard.Service.csproj` only, so adding a Migrator project to `QuestBoard.slnx` does not touch the image build. [VERIFIED: `Dockerfile`]
- **Build/test commands:** `dotnet build`, `dotnet test`. On Linux never set `DOTNET_GCHeapHardLimit`; `Fatal error. Internal CLR error. (0x80131506)` is a flake, re-run once. [VERIFIED: CLAUDE.md]
- **Production access:** read-only SSH as `claude`; use `systemctl show -p`, never `systemctl cat`; do not change anything on the server. Anything privileged goes to the operator as a script. [VERIFIED: CONTEXT specifics]

## Summary

The phase is a port of ing-dashboard's pull-based deployment with three genuine departures: EF migrations run through a dedicated migrator instead of an EF bundle, the migrator wraps the whole pending batch in one caller-owned transaction, and the installer mails only on install outcomes. Research confirmed the premise behind each departure and found five facts that change the plan.

**Confirmed from source and docs.** EF Core 10.0.9's `Migrator.Migrate` opens a transaction up front but passes `commitTransaction: useTransaction` into the command executor, so it commits after every migration; EF 9.0.0 passed `commitTransaction: false` and committed once. With a caller-supplied transaction (`useTransaction == false`) EF logs `MigrationsUserTransactionWarning`, skips the database lock, and runs every migration's commands inside the caller's transaction; if any command is `TransactionSuppressed` (or the execution strategy retries) EF throws `NotSupportedException`. SQL Server's generator marks `CreateDatabase`, `DropDatabase`, `AlterDatabase`, memory-optimized table operations and `migrationBuilder.Sql(..., suppressTransaction: true)` as suppressed. So the D-07 guarantee cannot silently weaken: EF itself refuses. A preflight in the migrator is still worth having so a bad release is refused before the backup and before the app is stopped. [VERIFIED: EF Core v10.0.9 and v9.0.0 `Migrator.cs`, `MigrationCommandExecutor.cs`, `SqlServerMigrationsSqlGenerator.cs`, `SqlServerHistoryRepository.cs`; CITED: EF 9 and EF 10 breaking-changes pages]

**Corrections and new facts the planner must carry.**

1. There are **43** migration classes, not 44. A compiled spike over the real `QuestBoard.Repository` enumerated 43 migrations, generated 164 SQL commands for SQL Server, and found none transaction-suppressed and none matching `ALTER DATABASE`, `FULLTEXT` or `MEMORY_OPTIMIZED`. `HasPendingModelChanges()` was false. [VERIFIED: spike run this session]
2. `gh attestation verify --bundle` is **not** offline by default. With a fresh cache and no network it fails with `no valid Sigstore verifiers could be initialized`; with `--custom-trusted-root trusted_root.jsonl` it verifies fully offline with no token. It also needs a **writable cache directory**: `GH_CONFIG_DIR` does not relocate the TUF cache, `XDG_CACHE_HOME` does, and an unwritable cache fails the same way. ing-dashboard's docs claim "fully offline" and its wrapper sets only `GH_CONFIG_DIR`, which is wrong under `ProtectHome=read-only`. The CT reaches both TUF hosts (`tuf-repo-cdn.sigstore.dev`, `tuf-repo.github.com`). [VERIFIED: ran `gh 2.102.0` against the ing-dashboard fixture; SSH curl from the CT]
3. `BACKUP` is **not allowed inside a transaction** (documented), which fits D-10 (backup, then stop, then open the apply transaction). It is also the reason `backup` must be its own migrator command and not part of `apply`: the app must keep running between the two. [CITED: learn.microsoft.com BACKUP (Transact-SQL)]
4. There is **no clean in-database way to prune** old `.bak` files. `xp_delete_file` is undocumented and header-checks the files; `msdb` queries only find files, they cannot delete them. The SQL CT is Ubuntu Linux, so pruning is a four-line operator script on that host. [CITED: SQL Server on Linux docs, community sources on `xp_delete_file`; VERIFIED: `environment.md` says CT 103 is Ubuntu 22.04]
5. The app writes **nothing** under its content root, so root-owned read-only release directories are safe. Sessions, Hangfire and the distributed cache are in SQL; logging is console only; Data Protection keys use the default location under the `questboard` home (`/home/questboard/.aspnet/...`), which is why the runner-removal step must delete only `/home/questboard/actions-runner`, never the home directory. [VERIFIED: grep over all `*.cs` in Service/Domain/Repository for file, directory, stream, content-root, web-root, temp-path and Data Protection APIs returned nothing; `Program.cs` read]

**Bootstrap ordering is a planning hazard.** `setup` ships inside the release zip, but `gh` and the installer do not exist on the CT until `setup` runs. The first release must be fetched out of band by the operator (verified on a workstation with `gh attestation verify`), unpacked in a temporary directory, and `setup` run from there. See "Bootstrap sequence".

**Primary recommendation:** Build the migrator as a thin console over a public, `DbContext`-generic `MigrationRunner` (status, preflight, backup, atomic apply) so the DB-less preflight guard and the gated real-SQL atomicity test can drive it; run it from the root installer through `systemd-run --pipe --wait` with `EnvironmentFile=/etc/questboard/env`; port ing-dashboard's installer with the D-11 matrix expressed as one pure decision function that bash logic tests cover row by row; and treat the first real release as the end-to-end rehearsal, stopped at the draft until the operator approves.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Build, test, package, attest the release zip | GitHub-hosted runner (CI) | — | D-01/D-04: the attested zip must come from a hosted runner; no production handle |
| Approval gate and publish | GitHub (`deploy` environment) | Operator | D-02: nothing installs until a human approves; the server only sees published releases |
| Release discovery (poll) | App CT installer (root oneshot, timer) | GitHub REST API (public, unauthenticated) | D-13: server pulls; GitHub holds no credential for the box |
| Artifact trust decision | App CT installer (`gh attestation verify` + checksum + main-ancestry) | Sigstore TUF CDN | D-01: refuse on any failure, no bypass |
| Schema change | Migrator (runs as `questboard` on the App CT) | SQL CT | D-06/D-07: atomic, before app start; connection string stays in the env file |
| Pre-migration backup | SQL Server on the SQL CT (`BACKUP DATABASE`, triggered by the migrator) | SQL CT filesystem (retention) | D-09: files land on the SQL CT's disk, so pruning is a SQL-CT-side concern |
| Activation (symlink switch, stop/start) | App CT installer + systemd | — | D-18/D-10: atomic `current` switch |
| Health and version confirmation | App (`/health` + version header) | Installer (poll loop) | Claude's-discretion item: confirm the new version answers |
| Outcome reporting | App CT installer via SMTP to the Postfix CT | Resend (behind Postfix) | D-14/D-17: one mail per outcome, no secrets |
| App runtime | App CT (`questboard.service`, user `questboard`) | — | Unchanged; code directory becomes root-owned read-only |
| Runner removal | Operator (GitHub UI + CT shell) | — | D-23: handover, verified from both sides |

## Standard Stack

### Core
| Library / Tool | Version | Purpose | Why Standard |
|----------------|---------|---------|--------------|
| .NET SDK / runtime | SDK 10.0.x (local 10.0.112); CT runtime 10.0.9 | Build and run the migrator and the app | Project is on .NET 10; CT has `Microsoft.NETCore.App` 10.0.9 [VERIFIED: ssh `dotnet --list-runtimes`] |
| EF Core + SqlServer provider | 10.0.9 (reached transitively from Repository) | Migrations, history table, `IMigrator` | Pinned in `QuestBoard.Repository.csproj`; migrator adds no EF package [VERIFIED: csproj] |
| `gh` CLI | 2.102.0 current (2026-09-30); minimum 2.49.0 for `attestation verify` | Offline-capable bundle verification | Official verifier for `actions/attest-build-provenance` output; ing-dashboard pins min 2.49.0 [VERIFIED: `gh --version`; `ing-dashboard/deploy/versions.env`] |
| `actions/checkout` | v7.0.1 = `3d3c42e5aac5ba805825da76410c181273ba90b1` | Hosted checkout | SHA resolved via `gh api` this session and equal to ing-dashboard's pin [VERIFIED: gh api] |
| `actions/setup-dotnet` | v6.0.0 = `a98b56852c35b8e3190ac28c8c2271da59106c68` | .NET 10 on the runner | same [VERIFIED: gh api] |
| `actions/attest-build-provenance` | v4.2.2 = `4d101475d8b20a2381f78447822ac1eab6504dd8` | Provenance attestation | D-01 names it; v4 is a wrapper over `actions/attest` (also 4.2.2), either works [VERIFIED: gh api release notes] |
| bash, `curl`, `unzip`, `python3`, `flock`, `systemd-run` | CT: curl 8.5.0, python3 3.12.3, systemd 255 | Installer runtime | All present on the CT; no new packages except `gh` [VERIFIED: ssh] |
| xUnit v3 + FluentAssertions | xunit.v3 3.2.2, FluentAssertions 8.10.0 | Migrator tests | Existing test stack; `Assert.Skip/SkipUnless` exist in xunit.v3.assert 3.2.2 [VERIFIED: csproj, package XML docs] |

### Supporting
| Library / Tool | Version | Purpose | When to Use |
|----------------|---------|---------|-------------|
| `shellcheck` | runner-provided | Lint `deploy/**/*.sh`, `build/*.sh` | CI lint step; not installed locally [ASSUMED: preinstalled on `ubuntu-24.04`] |
| `actionlint` / `zizmor` | latest | Workflow lint (hash-pinning, expression injection) | CI lint step; neither is installed locally [ASSUMED: obtainable on the runner] |
| SQL Server service container | `mcr.microsoft.com/mssql/server:2022-latest` | Real-SQL atomicity test in CI only | Gated test; never started locally [ASSUMED: image and tools path] |
| ing-dashboard fixture `public-attested-artifact.*` | `cli/cli` v2.101.0 `.deb` + bundle | Tampered-artifact refusal test | Verified to pass with gh 2.102.0; copy the two fixture files [VERIFIED: ran it] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Online TUF fetch at verify time (default `gh`) | `--custom-trusted-root` captured by `gh attestation trusted-root` at `setup` | Fully offline, but a Sigstore key rotation makes verification fail until `setup` is re-run. Not recommended; D-01 already treats an unreachable service as failure. |
| `systemd-run --pipe --wait` for the migrator | `runuser -u questboard -- bash -c '. /etc/questboard/env; ...'` | Sourcing is wrong: systemd `EnvironmentFile` syntax is not shell syntax (`$`, `!`, `#` in a connection string). |
| Filesystem prune script on the SQL CT | `xp_delete_file` from the migrator | Undocumented, header-checking, sysadmin-only; not acceptable for production code. |
| `jq` on the CT | `python3` (present) | Avoids a second `setup` prerequisite; D-22 says add `jq` only if needed. It is not needed. |
| Replace `binary-release.yml` in place | New `release.yml`, delete the old one | New file keeps the pinned signer path stable and matches ing-dashboard; README badge URL must change. |

**Installation (CT, performed by `setup` as root):**
```bash
# GitHub CLI from the official apt repository (key fingerprint pinned; see Pitfall 14)
curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg -o /tmp/ghkey.gpg
# verify the ACTIVE primary key fingerprint equals 7F38BBB59D064DBCB3D84D725612B36462313325, then:
install -m 644 /tmp/ghkey.gpg /usr/share/keyrings/githubcli.gpg
echo "deb [signed-by=/usr/share/keyrings/githubcli.gpg] https://cli.github.com/packages stable main" \
  > /etc/apt/sources.list.d/github-cli.list
apt-get update -qq && apt-get install -y gh
```

**Version verification:** no new NuGet, npm, PyPI or crates packages are introduced. `gh` 2.102.0 confirmed locally; the keyring fetched 2026-10-04 holds two primary keys, an expired `2C6106...` and a valid `7F38BBB59D064DBCB3D84D725612B36462313325`.

## Package Legitimacy Audit

No package from an ecosystem the `package-legitimacy` seam covers (npm, PyPI, crates) is introduced by this phase.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| `gh` (GitHub CLI) | apt: `cli.github.com/packages` | 5+ yrs | n/a | github.com/cli/cli | n/a (seam does not cover apt) | Approved: keyring fingerprint `7F38BBB59D064DBCB3D84D725612B36462313325` verified against the fetched key this session; installed only by the operator-run `setup` |
| `actions/checkout`, `actions/setup-dotnet`, `actions/attest-build-provenance` | GitHub Actions | official `actions/*` org | n/a | github.com/actions/* | n/a | Approved: pinned by full commit SHA, resolved from release tags this session |
| NuGet | — | — | — | — | n/a | None added. Migrator needs no EF `PackageReference`; test code reuses existing packages |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
 GitHub (hosted) ------------------------------------------------------------------
  tag vX.Y.Z pushed
      |
      v
  [release.yml: build job]  validate tag -> build -c Release -> dotnet test --no-build
      |                      -> publish app + migrator -> package zip (+ deploy/, manifest)
      |                      -> sha256 -> attest-build-provenance -> bundle
      v
  DRAFT release: questboard-<tag>.zip, .zip.sha256, .zip.sigstore.json
      |   (operator edits title + notes on the draft)
      v
  [publish job, environment "deploy", reviewer approves]
      re-download draft -> sha256 -c -> gh attestation verify -> gh release edit --draft=false --latest
      |
 ----------------------------------------------------------------- public ---------
      |  releases/latest (unauthenticated, 12/h)            Sigstore TUF CDN
      v                                                      ^
 App CT (192.168.6.12) ------------------------------------- | -------------------
  questboard-deploy-poll.timer (2min boot, 5min, +-30s)      |
      -> questboard-deploy-poll.service  (root oneshot, sandboxed, flock)
           poll: latest tag > active? not remembered-bad? else journal-only exit
           install <tag>:
             download zip+sha+bundle -> /var/lib/questboard-deploy/downloads/<tag>/
             sha256 -c  -> gh attestation verify (pinned repo/workflow/ref, deny self-hosted)
                        -> attested commit reachable from main (compare API)   [refuse on any fail]
             unzip -> releases/.staging-<ver> -> manifest.version == tag -> mv -> releases/<ver> (root-owned)
             systemd-run (User=questboard, EnvironmentFile=/etc/questboard/env)
                 migrator status --json  ---------------------------------------> SQL CT :1433
                    unknown applied  -> REFUSED
                    non-transactional pending -> REFUSED
                    pending>0: migrator backup --label <tag> ---------------------> BACKUP ... COPY_ONLY
                                  fail -> FAILED (app untouched)
             systemctl stop questboard
                    pending>0: migrator apply  (one caller transaction) ----------> COMMIT or ROLLBACK
                                  fail -> FAILED, rolled back; restart previous
             switch current -> releases/<ver>  (ln -s + mv -T)
             systemctl start questboard
             health loop: GET 127.0.0.1:5000/health  200 + Healthy|Degraded + X-header == version
                    ok -> INSTALLED (+ "installer update available?" compare deploy/)
                    bad & no migration  -> switch back, restart, confirm -> ROLLED BACK
                    bad & migrated      -> leave new release active -> HALTED (backup name in mail)
           record tag+outcome in state file; ONE email via curl smtp:// ------> Postfix CT :25 -> Resend
 SQL CT (192.168.6.10): operator-run prune script keeps the newest N pre-migration .bak files
```

### Recommended Project Structure
```
.github/workflows/
├── release.yml                  # replaces binary-release.yml; no self-hosted anywhere
├── dotnet.yml                   # add: deploy-script tests + shellcheck + workflow lint jobs
└── docker-publish.yml           # untouched
build/
├── validate-release-tag.sh      # strict vX.Y.Z, tag == GITHUB_SHA, ancestor of origin/main
├── package-release.sh           # publish app + migrator, deploy/ (minus tests/), manifest, zip, sha256
├── verify-published-release.sh  # workstation-side proof that a published release verifies like the CT does
└── tests/validate-release-tag-test.sh
deploy/
├── bin/questboard-deploy        # installed to /usr/local/sbin
├── lib/{common.sh,deploy.sh}    # installed to /usr/local/lib/questboard-deploy
├── systemd/{questboard.service,questboard-deploy-poll.service,questboard-deploy-poll.timer}
├── deploy.conf.example          # installed (edited) to /etc/questboard/deploy.conf, root 600
├── sql-ct/prune-premigration-backups.sh   # operator installs on the SQL CT, not on the App CT
└── tests/{run-all.sh,lib/host-guard.sh,fixtures/,*-test.sh,*-network-test.sh}
QuestBoard.Migrator/
├── QuestBoard.Migrator.csproj   # ProjectReference -> Repository only; no EF PackageReference
├── Program.cs                   # arg parsing, exit codes, JSON on stdout, logs on stderr
└── MigrationRunner.cs           # public, DbContext-generic: Status, Preflight, Backup, ApplyAtomically
QuestBoard.UnitTests/Migrator/   # DB-less preflight guard over all shipped migrations
QuestBoard.IntegrationTests/Migrator/  # gated real-SQL atomicity + backup tests
docs/{deploy.md,releasing.md}    # new; server-setup.md keeps CT creation and links here
```
Zip layout (new releases): `app/`, `migrator/`, `deploy/`, `release-manifest.json` (`{ "version", "commit", "healthVersionHeader": true }`). The adopted release is laid out identically (`releases/<ver>/app/`), so `questboard.service` has one path: `/opt/questboard/current/app`.

### Bootstrap sequence (cutover ordering)
`setup` is inside the release zip, but the CT has no installer, no `gh` and no `deploy/` until `setup` has run. Order:
1. Merge the phase to `main`. From then on no workflow deploys anything (the old runner is idle but still registered).
2. Tag the release. The hosted build produces the draft; the operator edits title and notes; approves `deploy`; the release is published but **not installed**.
3. Operator, on a workstation: `build/verify-published-release.sh <tag>` (trust on first use is bridged by verifying the zip with `gh` before it goes near the CT). Copy the zip to the CT and unzip it to a temporary directory.
4. Operator, on the CT as root: `<tmp>/deploy/bin/questboard-deploy setup`. It installs `gh` and the installer, units and config, reads the running version from `/opt/questboard/QuestBoard.Service.dll`, stops the app, moves the 77 flat entries into `releases/<running>/app/`, makes `/opt/questboard` root-owned, creates `current`, installs the unit change, starts the app, records "adopted, not verified", enables the timer.
5. The first pull-based install is that same published release: the timer or `questboard-deploy install <tag>` fetches it, verifies it itself, and installs it with the adopted release as the automatic rollback target.
6. After a healthy report: runner removal handover (D-23), then two-sided verification (zero registered runners via `gh api repos/{owner}/{repo}/actions/runners`; no `actions.runner*` unit on the CT).

### Pattern 1: Migrator as a thin console over a public, context-generic runner
**What:** All logic lives in `MigrationRunner(DbContext db)`; `Program.cs` only parses arguments, builds `QuestBoardContext` with a null `IActiveGroupContext`, calls the runner and maps results to exit codes. Tests drive the runner with a hand-written test `DbContext` and migrations.
**When to use:** Always. It is what makes D-07 provable without a production database.
**Exit-code contract (recommended):** `0` ok; `1` unexpected error or bad usage; `2` database ahead of this build (unknown applied migrations); `3` pending migration contains a non-transactional operation; `4` cannot connect or database missing; `5` backup failed; `6` apply failed and rolled back. `status` prints one JSON document on stdout (`applied`, `pending`, `unknown`, `nonTransactional`, `canBackup`), everything else goes to stderr, and no output ever contains a connection string or host.

### Pattern 2: One pure decision function for the outcome matrix
**What:** `qb_decide_outcome` takes facts (verify result, unknown-applied, backup result, apply result, health result, migrated flag) and returns one of `refused | failed | failed_rolled_back | rolled_back | halted | installed`, plus the action. The orchestration calls it; the logic test feeds it every D-11 row. ing-dashboard's `ledger_rollback_decision` is the seed.
**When to use:** the installer core, so the matrix is testable without root, systemd or a database.

### Pattern 3: Relocatable root for tests
**What:** Every path in the installer derives from `QUESTBOARD_DEPLOY_ROOT` (default empty). Tests run the real scripts against a temp root with `systemctl`, `systemd-run`, `gh` and `curl` replaced by PATH stubs that record calls (ing-dashboard's `host-guard.sh` makes `systemctl`/`pkexec` fail and log, so no test can touch the host).
**When to use:** all installer logic and flow tests.

### Pattern 4: Release immutability and atomic activation
**What:** Extract to `releases/.staging-<ver>`, validate the manifest version equals the tag, `mv -T` into `releases/<ver>`, `chown -R root:root`, `chmod -R go-w`; switch with `ln -s <target> current.tmp && mv -T current.tmp current`. Record the previous version in the state directory before switching. Prune oldest first, never the active or previous release.
**When to use:** install, rollback, setup.

### Anti-Patterns to Avoid
- **Calling stock `Database.Migrate()` from the migrator and calling that atomic.** EF 10 commits per migration (Pitfall 1).
- **Running `BACKUP` inside the apply transaction.** Not allowed; it runs before the transaction opens.
- **Sourcing `/etc/questboard/env` in bash.** Use `systemd-run -p EnvironmentFile=`.
- **Setting only `GH_CONFIG_DIR` for `gh`.** Also set `XDG_CACHE_HOME` and `XDG_STATE_HOME` (Pitfall 2).
- **Letting the poll mail.** The poll writes journal lines only; mail is sent from the install path, exactly once per outcome.
- **Interpolating `${{ github.ref_name }}` into `run:` scripts.** Pass it through `env:` (the current workflow does the opposite).
- **Trusting `releases/latest` to be the highest version.** Compare semver against the active release.
- **Embedding D-xx / Phase 89 / MH-n in scripts, units, workflows or C#.**

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Artifact provenance verification | Custom signature or certificate checks | `gh attestation verify --bundle ... --repo ... --signer-workflow ... --source-ref ... --deny-self-hosted-runners` | Sigstore bundle, certificate identity and transparency-log checks; the trusted root rotates via TUF |
| Atomic multi-migration apply | A SQL script runner (`migrations script` plus `sqlcmd`) | EF `Migrate()` inside `Database.BeginTransaction()` with `MigrationsUserTransactionWarning` ignored | Keeps the history-table inserts inside the same transaction and uses the provider's own SQL generation |
| Detecting non-transactional operations | Regex-only scanning of migration source files | `IMigrationsSqlGenerator.Generate(migration.UpOperations, ...)` then `MigrationCommand.TransactionSuppressed`, plus a text scan of `CommandText` as a second net | The generator is the source of truth for what EF will run |
| Loading `/etc/questboard/env` for the migrator | A shell or C# parser of the file | `systemd-run --pipe --wait --collect -p User=questboard -p EnvironmentFile=...` | Identical parsing to the app's own unit; secret never leaves systemd; special characters preserved (verified) |
| Sending the outcome email | Local `sendmail`, `msmtp`, a mail library | `curl --url smtp://relay:25/... --mail-from --mail-rcpt --upload-file` | Present on the CT; verified against an SMTP sink; the local Postfix has an empty `relayhost` |
| JSON handling in bash | Regex over JSON | `python3` one-liners (present on the CT) | No `jq` prerequisite |
| Single-instance protection | PID files | `flock -n` on a file under the unit's `RuntimeDirectory` | Same pattern as the reference installer |
| Symlink switching | `rm` then `ln` | `ln -s target tmp && mv -T tmp current` | Atomic rename |
| Backup retention | `xp_delete_file` or SQL Agent jobs | A root-owned script on the SQL CT using `find`/`ls` ordering | The files belong to the SQL CT's filesystem; no database privilege needed |
| Config parsing | `source deploy.conf` | The allow-list `KEY=VALUE` loader from ing-dashboard's `common.sh` (owner and mode checks) | A root installer must never source a file |

**Key insight:** every piece of this phase already exists as a hardened tool (`gh`, EF's migrator, systemd, curl, flock). The risk is in the seams: environment variables for `gh` under a sandbox, the transaction boundary around `BACKUP`, and the order of the install steps. Hand-rolled glue should be limited to the decision function and the orchestration.

## Runtime State Inventory

This phase migrates a live server layout and removes a live runner, so the inventory applies.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | No renamed keys. Sessions (`AspNetSessionState`), Hangfire and `__EFMigrationsHistory` live in SQL and are untouched. ASP.NET Data Protection keys sit under `/home/questboard/.aspnet/...` by default (no `AddDataProtection` call in the code) [ASSUMED location, not readable by the inspection account] | None for data. **Do not delete `/home/questboard`**: removing it logs everyone out and invalidates outstanding password-reset and email-change tokens. The runner handover deletes only `/home/questboard/actions-runner` |
| Live service config | GitHub: repo has a self-hosted runner registered as `theunschut-dnd-quest-board.QuestBoard` (old repo name); the `deploy` environment and a `v*` tag ruleset do not exist yet. Traefik routes to `:5000` and is unaffected. Postfix CT (192.168.6.13) must accept mail from the App CT: port 25 reachable and banner `220 Postfix.localdomain ESMTP Postfix (Debian/GNU)` [VERIFIED: ssh] | Operator: deregister runner, create `deploy` environment (required reviewer, tag policy `v*.*.*`), add tag ruleset; confirm the app's real relay and sender in the env file |
| OS-registered state | `questboard.service` in `/etc/systemd/system` (`ProtectSystem=no`, `WorkingDirectory=/opt/questboard`, `ExecStart=/usr/bin/dotnet /opt/questboard/QuestBoard.Service.dll`) [VERIFIED: `systemctl show`]; `actions.runner.theunschut-dnd-quest-board.QuestBoard.service`; `/etc/sudoers.d/questboard`; no deploy timers exist today [VERIFIED: `systemctl list-timers`] | `setup`: change unit paths (prefer a drop-in, see Open Question 4), `daemon-reload`, install poll service and timer, restart. Operator: uninstall runner service, remove sudoers file |
| Secrets / env vars | `/etc/questboard/env` (600, `questboard:questboard`) holds the connection string and mail settings. The migrator and the app read the same key `ConnectionStrings__DefaultConnection`. New `deploy.conf` (root, 600) holds relay host, port, sender, recipient: no secrets. Runner credentials live under `/home/questboard/actions-runner` | None for the env file (key name unchanged). Deleting the runner directory removes the runner credentials; GitHub-side deregistration revokes the registration |
| Build artifacts / installed packages | `/opt/questboard` is a flat directory of 77 entries owned by `questboard` [VERIFIED]; `/home/questboard/deploy.sh`; `gh` not installed | `setup` moves the flat contents under `releases/<running>/app/`, makes `/opt/questboard` root-owned, installs `gh`. Delete `deploy.sh` in the runner handover |

**Canonical question answered:** after every file in the repo is updated, the CT still holds the flat `/opt/questboard`, the old unit paths, the live runner and its sudoers file, and GitHub still holds the runner registration. All five are covered by `setup` or the D-23 handover; none can be done from a workflow.

## Common Pitfalls

### Pitfall 1: Stock `Migrate()` is per-migration, not atomic, on EF Core 10
**What goes wrong:** A release with three pending migrations fails on the second; the first stays committed and the history table says so. The previous release now runs against a schema it does not know.
**Why it happens:** In EF Core 10.0.9 `Migrator.MigrateImplementation` begins a transaction and then calls the executor with `commitTransaction: useTransaction`, which commits at the end of each migration's command list. EF 9.0.0 passed `commitTransaction: false` and committed once at the end. [VERIFIED: raw `Migrator.cs` at tags v9.0.0 and v10.0.9; CITED: EF Core 9 breaking changes "All pending migrations are applied in a single transaction ... This behavior was reverted in EF Core 10"]
**How to avoid:** The migrator opens `Database.BeginTransaction()` itself, calls `Migrate()`, then commits. With a caller transaction `useTransaction` is false, so the executor neither begins nor commits anything and every migration (and its history insert) runs inside the caller's transaction. Add `ConfigureWarnings(w => w.Ignore(RelationalEventId.MigrationsUserTransactionWarning))` to the migrator's options only. [VERIFIED: source; CITED: EF 9 breaking-changes mitigation text]
**Warning signs:** A test that applies two migrations where the second fails and then finds the first migration's table or history row still present.

### Pitfall 2: `gh attestation verify` needs a writable cache and, by default, the network
**What goes wrong:** Verification fails with `no valid Sigstore verifiers could be initialized` inside the sandboxed unit, and the installer refuses every release.
**Why it happens:** `gh` initialises a TUF client and caches it under `$XDG_CACHE_HOME/gh/.sigstore` (default `$HOME/.cache/gh`), and writes a device id under `$XDG_STATE_HOME/gh`. `GH_CONFIG_DIR` does not move the cache. Under `ProtectHome=read-only` with `HOME=/root` the cache is unwritable. With a writable empty cache and no network the same error appears. [VERIFIED: ran gh 2.102.0 with a read-only HOME (fail), a proxy to nowhere and a fresh cache (fail), `--custom-trusted-root` with the same dead proxy (pass, no token)]
**How to avoid:** Wrap every call in `env -u GH_TOKEN -u GITHUB_TOKEN -u GH_ENTERPRISE_TOKEN GH_CONFIG_DIR=$t XDG_CACHE_HOME=$t/cache XDG_STATE_HOME=$t/state GH_TELEMETRY=false GH_NO_UPDATE_NOTIFIER=1 GH_PROMPT_DISABLED=1` with `t=$(mktemp -d)` (the unit's `PrivateTmp` makes it private). A token is not needed with `--bundle`. Document verification as "needs Sigstore TUF reachable", not "offline". Both TUF hosts answered from the CT. [VERIFIED]
**Warning signs:** The installer refusing a release that verifies fine on a workstation.

### Pitfall 3: `BACKUP` cannot run in a transaction, and backup-then-stop leaves a small write window
**What goes wrong:** `BACKUP` inside the apply transaction throws; or a restore later loses writes made between backup and app stop.
**Why it happens:** "The BACKUP statement isn't allowed in an explicit or implicit transaction." D-10 orders backup, then stop, then apply, which is the right shape, but the app is still serving between the backup and the stop. [CITED: learn.microsoft.com/en-us/sql/t-sql/statements/backup-transact-sql]
**How to avoid:** `backup` is its own migrator command, run before the app is stopped, outside any transaction, with a generous command timeout. Document in the restore runbook that a restore discards writes made after the backup began. Parameterise database name, file name and backup-set name (`BACKUP` accepts variables for all three).
**Warning signs:** Backup code placed inside `ApplyAtomically`.

### Pitfall 4: Retrying execution strategy or a suppressed command inside the user transaction throws
**What goes wrong:** `NotSupportedException: User transaction is not supported with a TransactionSuppressed migrations or a retrying execution strategy.`
**Why it happens:** `Migrator.ValidateMigrations` and `MigrationCommandExecutor.ExecuteNonQuery` both throw that when a user transaction is present and either the execution strategy retries or any command in the batch is `TransactionSuppressed`. The migrator therefore must not call `EnableRetryOnFailure`. [VERIFIED: source; message text from `RelationalStrings.resx`]
**How to avoid:** Build options with `UseSqlServer(connectionString)` only plus the warning suppression. Run the preflight first so the refusal happens before backup and before the app stops; keep EF's own throw as the second net (the transaction is disposed, so it rolls back).
**Warning signs:** The failure appearing only after the app was already stopped.

### Pitfall 5: `systemd-run` from inside a sandboxed root oneshot is untested
**What goes wrong:** The migrator cannot be started from the poll unit although it works from a root shell.
**Why it happens:** `systemd-run` talks to PID 1 over a Unix socket; `ProtectSystem=strict` makes `/run` read-only, which should not block `connect()`, and `NoNewPrivileges` does not constrain PID 1. ing-dashboard uses `systemd-run` only from an unsandboxed root shell (`ledger-apikey`) and `runuser` from the sandboxed unit. [ASSUMED: sandbox interplay; VERIFIED locally: `systemd-run --user --pipe --wait --collect -p EnvironmentFile=...` propagates the exit code (`rc=7`), keeps stdout separate from stderr, preserves `$`, `!`, `#` in a connection-string value, and `-p RuntimeMaxSec=1` terminates a long run]
**How to avoid:** The first UAT step on the CT runs `questboard-deploy` under the real unit and calls `migrator status` through `systemd-run`. If it fails, fallback is to run the migrator from a second unit `questboard-migrate@.service` started with `systemctl start --wait`, passing mode through a root-owned file under `RuntimeDirectory`.
**Warning signs:** `Failed to connect to bus` in the journal.

### Pitfall 6: Release-directory ownership and readability
**What goes wrong:** The app cannot read its own code, or can write to it.
**Why it happens:** The installer runs as root with whatever umask systemd gives; zip entry modes decide file modes.
**How to avoid:** `umask 022` in the installer, `chown -R root:root`, `find -type d -exec chmod 755`, `find -type f -exec chmod go-w`, then assert a few `dll` files are `644`. `questboard` needs read+execute on directories only. After unzip, refuse a tree containing symlinks (`find -type l`). Confirm `/opt/questboard` itself is `root:root 755` after `setup` so the app user cannot swap `current`. [VERIFIED: the app writes nothing under the content root, see Summary item 5]
**Warning signs:** `UnauthorizedAccessException` reading `wwwroot`, or `questboard` able to `touch` inside a release.

### Pitfall 7: Polling facts that bite
**What goes wrong:** Mail floods, rate-limit exhaustion, or a downgrade.
**Why it happens:** `releases/latest` returned `v5.3.3` with one asset `questboard-v5.3.3.zip` and `x-ratelimit-limit: 60`, `x-ratelimit-used: 21` already this hour from the shared public IP. GitHub orders "latest" by release metadata, not semver. Old releases (<= v5.3.3) have no `.sha256` or `.sigstore.json`. [VERIFIED: ssh curl from the CT]
**How to avoid:** One API call per poll and none for the compare check except on install. Install only when `semver(latest) > semver(active)` and the tag is not remembered as refused/failed/rolled back/halted. A missing bundle asset is a verification failure (refused, one mail, remembered), never a fallback. Idle, unreachable and "remembered tag" outcomes log one journal line and exit 0 so `systemctl --failed` shows only real failures.
**Warning signs:** `X-RateLimit-Remaining` trending to zero; a poll that emails.

### Pitfall 8: Email format and failure semantics
**What goes wrong:** Postfix rejects or mangles the message, or a mail failure changes the deploy result.
**Why it happens:** Bare-LF bodies, non-ASCII subjects, no `Date`/`Message-ID`, or letting `curl`'s exit code propagate. curl derives the SMTP `EHLO` name from the URL path; with no path it announced the upload file name in the sink test.
**How to avoid:** Generate CRLF, ASCII-only headers, `From: noreply@theunschut.com` (the committed `EmailSettings:FromEmail`), `Date`, `Message-ID`, `Content-Type: text/plain; charset=UTF-8`; use `smtp://192.168.6.13:25/<helo-name>`; body is exactly version, result, timestamps (and the backup *name* for the halted row, never a path). A failed send logs one journal line and never changes the exit code. [VERIFIED: curl 8.18 against a local SMTP sink; committed defaults `SmtpServer 192.168.6.13`, `SmtpPort 25`, `EnableSsl false` read from `appsettings.json`; port 25 and banner verified from the CT]
**Warning signs:** More than one message per outcome; paths or hostnames in a body.

### Pitfall 9: Workflow injection and permissions
**What goes wrong:** Tag-controlled text executes in a shell, or a token has more power than it needs.
**Why it happens:** The existing workflow interpolates `${{ steps.version.outputs.version }}` and `${{ github.ref_name }}` into `run:` lines, and its top-level job grants `contents: write` broadly.
**How to avoid:** `permissions: {}` at workflow level; job-level `contents: write`, `id-token: write`, `attestations: write` only on the build job; `contents: write` only on publish. Pass `github.ref_name` through `env: TAG:`. Pin every action by full SHA (resolved this session). Add `concurrency: release-${{ github.ref }}` with `cancel-in-progress: false`. Run `zizmor` and `actionlint` in CI. [CITED: ing-dashboard `release.yml`, `.github/zizmor.yml`]

### Pitfall 10: A tag on a branch can ride the same signer workflow
**What goes wrong:** Someone with push rights tags a branch commit whose `release.yml` skips validation; the attestation still names the right workflow path and `refs/tags/<tag>`.
**Why it happens:** `--signer-workflow` pins the workflow path, not its content; the certificate's `buildSignerURI` carries `@refs/...` but is not pinned by that flag. The certificate does carry `sourceRepositoryDigest` (the commit) and `runnerEnvironment`. [VERIFIED: printed certificate fields from the fixture verification]
**How to avoid:** Keep ing-dashboard's second check: take `sourceRepositoryDigest` from `gh attestation verify --format json` and require that commit be `identical` or `behind` `main` via the unauthenticated compare API. Also create the `v*` tag ruleset and a tag-only deployment policy on the `deploy` environment (commands in ing-dashboard `docs/github-repository-settings.md`); `validate-release-tag.sh` needs `fetch-depth: 0` and `origin/main`.

### Pitfall 11: Test what you ship
**What goes wrong:** The attested zip is not the build that passed, or the release job builds the solution three times.
**Why it happens:** ing-dashboard packages first and tests afterwards; the current workflow publishes with no tests. `dotnet.yml` sets up .NET 8.0.x for a net10.0 solution.
**How to avoid:** One restore, `dotnet build -c Release -p:Version=<ver>`, `dotnet test -c Release --no-build`, then `dotnet publish -c Release --no-build` for the app and the migrator. The Dockerfile already uses build-then-`publish --no-build`. Use `dotnet-version: '10.0.x'` in the release workflow. A full local `dotnet test` took 94 s for 805 unit and 951 integration tests, all passing, so a 30-minute job timeout is generous. [VERIFIED: local run this session]

### Pitfall 12: Version confirmation edge cases
**What goes wrong:** The installer accepts the wrong version, rejects a good one, or cannot verify a rollback target.
**Why it happens:** `AppVersion.Current` strips the `+sha` suffix and returns `dev` when no informational version is present. A publish with `-p:Version=5.3.3` produced the informational version `5.3.3+<40-hex>` in the DLL. The adopted release (and any future rollback to it) has no version header. `/health` returns HTTP 200 for both `Healthy` and `Degraded` (board time zone unresolved), by design. [VERIFIED: `AppVersion.cs`, `BoardTimeZoneHealthCheck.cs`, test publish]
**How to avoid:** Compare the header to the numeric tag without `v`. Treat HTTP 200 with body `Healthy` or `Degraded` as up (log a warning on `Degraded`). Enforce the header only when the target release's manifest says `"healthVersionHeader": true`; releases created by `setup` adoption write `false`.

### Pitfall 13: The adopted release has no migrator and no manifest
**What goes wrong:** `rollback <adopted>` cannot run the target's own `status` (D-12), and the first install's automatic rollback target is unverified code.
**Why it happens:** The pre-phase release predates the migrator and attestation (D-22).
**How to avoid:** Automatic rollback (D-11 rows 4 and 5) never needs `status`: it only happens when no migration was committed. Manual `rollback <adopted>` should be refused with a clear message ("adopted release has no migrator; restore a backup by hand") unless the planner chooses to let the newest release's migrator compare the database against the adopted release's `QuestBoard.Repository.dll` migration list. Recommend refusing. Also: `setup` must stop the app before moving files, be re-runnable if interrupted, and read the running version from the DLL with a regex for `\d+\.\d+\.\d+(\+[0-9a-f]{40})?` (verified on a test publish: `5.3.3+0f947c3b...` appears as UTF-8 in `QuestBoard.Service.dll`), then ask the operator to confirm it.

### Pitfall 14: GitHub CLI apt key has two primary keys
**What goes wrong:** Pinning logic that expects exactly one key fails, or pins the expired one.
**Why it happens:** The fetched keyring contains an expired primary key (`2C6106201985B60E6C7AC87323F3D4EA75716059`, expiry flag `e`) and an active one (`7F38BBB59D064DBCB3D84D725612B36462313325`). [VERIFIED: `gpg --show-keys` on the keyring downloaded 2026-10-04]
**How to avoid:** Select the active (non-expired) primary key by `gpg --with-colons`, compare its fingerprint to the pin, then install the keyring with a `signed-by` source entry. Enforce `gh >= 2.49.0` after install.

### Pitfall 15: Repository hygiene traps
`.gitignore` ignores any directory named `releases` or `Release` (`[Rr]eleases/`); do not commit fixtures under such a path. The README workflow badge points at `binary-release.yml`; renaming the workflow means updating it. The attestation certificate records the repository URI at build time, so renaming the GitHub repository later breaks verification of existing releases against a config that pins the new name; keep `QUESTBOARD_GITHUB_REPO` equal to the name current at build time and the signer path `.github/workflows/release.yml` stable. [VERIFIED: `.gitignore`, `README.md` line 1]

### Pitfall 16: "Halted" means systemd keeps restarting the new release
`questboard.service` has `Restart=always` and `RestartSec=10` today [VERIFIED: `systemctl show`]. An explicit `systemctl stop` does not restart it, so the install window is safe. After a committed migration with an unhealthy release the installer must leave the unit enabled and running so a transient fault heals itself (D-11), and must still send exactly one `halted` mail naming the backup.

### Pitfall 17: First timer fire and re-run safety
`OnBootSec` is already in the past when the timer is enabled after boot, so first activation timing is not guaranteed by the timer alone. Have `setup` end by printing the exact `questboard-deploy install <tag>` command and let the operator run the first install deliberately. [ASSUMED: systemd timer semantics; verify on the CT]

### Pitfall 18: `install <tag>` for the already-active tag
ing-dashboard refuses a tag that is not newer than the active release. D-20 also calls `install` a redeploy. Recommended semantics: older than active is refused (use `rollback`); equal to active performs restart plus health check only, with no download; a remembered-bad tag is overridden by an explicit `install`.

## Code Examples

Patterns below were compiled or executed where marked. All C# uses only types reachable through `QuestBoard.Repository`'s package references.

### 1. Status and preflight (verified: compiled and run against the real `QuestBoard.Repository`)
```csharp
// QuestBoardContext(DbContextOptions<QuestBoardContext> options,
//                   IActiveGroupContext activeGroupContext, TimeProvider? timeProvider = null)
// IActiveGroupContext exposes: int? ActiveGroupId { get; }
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

var options = new DbContextOptionsBuilder<QuestBoardContext>()
    .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"))
    .ConfigureWarnings(w => w.Ignore(RelationalEventId.MigrationsUserTransactionWarning))
    .Options;
using var db = new QuestBoardContext(options, new NullGroupContext());

var assembly    = db.GetService<IMigrationsAssembly>();
var generator   = db.GetService<IMigrationsSqlGenerator>();
var initializer = db.GetService<IModelRuntimeInitializer>();
var forbidden   = new Regex(@"ALTER\s+DATABASE|FULLTEXT|MEMORY_OPTIMIZED", RegexOptions.IgnoreCase);

foreach (var (id, type) in assembly.Migrations.OrderBy(m => m.Key, StringComparer.Ordinal))
{
    var migration = assembly.CreateMigration(type, db.Database.ProviderName!);
    var model = migration.TargetModel is null ? null : initializer.Initialize(migration.TargetModel);
    foreach (var command in generator.Generate(migration.UpOperations, model))
        if (command.TransactionSuppressed || forbidden.IsMatch(command.CommandText)) { /* flag id */ }
}
// Real run: migrations=43 commands=164 flagged=0; HasPendingModelChanges()=false.
// Last migration id: 20260930161610_AddFeedEntryRevisions
sealed class NullGroupContext : IActiveGroupContext { public int? ActiveGroupId => null; }
```
For `status`: `db.Database.GetAppliedMigrations()`, `GetMigrations()`, `GetPendingMigrations()`; `unknown = applied except known` using `OrdinalIgnoreCase` (EF compares applied ids that way) [pattern; compile-check in the plan, these facade methods need a database]. With no connection the preflight half runs unchanged, which is what the DB-less regression test uses.

### 2. Atomic apply (source-verified semantics; exercised by the gated test)
```csharp
public void ApplyAtomically()
{
    var findings = Preflight();                       // refuse before touching anything
    if (findings.Count > 0) throw new NonTransactionalMigrationException(findings);

    db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
    using var tx = db.Database.BeginTransaction();    // caller transaction => useTransaction == false
    db.Database.Migrate();                            // history inserts run inside tx too
    tx.Commit();                                      // an exception skips this; Dispose rolls back
}
// Options: UseSqlServer(...) + ConfigureWarnings(Ignore MigrationsUserTransactionWarning); NO EnableRetryOnFailure.
```

### 3. Pre-migration backup (pattern; the gated CI test runs it for real)
```csharp
// File name only: SQL Server resolves it against its default backup directory.
var file = $"QuestBoard_pre-{label}_{TimeProvider.System.GetUtcNow():yyyyMMddTHHmmssZ}.bak";
db.Database.SetCommandTimeout(TimeSpan.FromMinutes(30));
db.Database.ExecuteSql(
    $"BACKUP DATABASE {databaseName} TO DISK = {file} WITH COPY_ONLY, CHECKSUM, INIT, NAME = {file}, STATS = 10");
// label must match ^[A-Za-z0-9._-]+$ ; print only the file NAME to stdout.
```
Permissions: `BACKUP DATABASE` defaults to `sysadmin`, `db_owner` and `db_backupoperator`, and the SQL Server service account needs write access to the backup folder [CITED: BACKUP docs]. `docs/server-setup.md` shows the app logging in as `sa`; the real login was not read [ASSUMED].

### 4. Version on the health response (pattern; Program.cs line 373 is `app.MapHealthChecks("/health");`)
```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
    {
        context.Response.Headers["X-QuestBoard-Version"] = AppVersion.Current;
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(report.Status.ToString());   // body stays "Healthy"/"Degraded"
    }
});
```
Existing tests only `Assert.Contains("Healthy", body)` / `"Degraded"`; add one integration test asserting the header equals `AppVersion.Current`.

### 5. Verification wrapper (verified behaviours; mirrors ing-dashboard with the cache fix)
```bash
qb_verify_attestation() {   # artifact bundle repo signer_workflow source_ref
  local t; t="$(mktemp -d)"
  local out
  if ! out=$(env -u GH_TOKEN -u GITHUB_TOKEN -u GH_ENTERPRISE_TOKEN \
        GH_CONFIG_DIR="$t" XDG_CACHE_HOME="$t/cache" XDG_STATE_HOME="$t/state" \
        GH_TELEMETRY=false GH_NO_UPDATE_NOTIFIER=1 GH_PROMPT_DISABLED=1 \
        gh attestation verify "$1" --bundle "$2" --repo "$3" \
          --signer-workflow "$3/$4" --source-ref "$5" \
          --deny-self-hosted-runners --format json 2>&1); then
    rm -rf "$t"; return 1
  fi
  rm -rf "$t"
  printf '%s' "$out" | python3 -c \
   'import sys,json; print(json.load(sys.stdin)[0]["verificationResult"]["signature"]["certificate"]["sourceRepositoryDigest"])'
}
```
Verified: fixture verifies with no token; tampered byte and wrong `--source-ref` are refused ("expected SourceRepositoryRef to be refs/tags/v0, got refs/heads/trunk").

### 6. Running the migrator as `questboard` without spreading the secret
```bash
systemd-run --quiet --pipe --wait --collect \
  --uid=questboard --gid=questboard \
  --property=EnvironmentFile=/etc/questboard/env \
  --property=NoNewPrivileges=yes --property=PrivateTmp=yes --property=ProtectSystem=strict \
  --property=ProtectHome=yes --property=ProtectKernelTunables=yes --property=ProtectKernelModules=yes \
  --property=ProtectControlGroups=yes --property=RestrictNamespaces=yes --property=LockPersonality=yes \
  --property=CapabilityBoundingSet= --property=RestrictAddressFamilies="AF_UNIX AF_INET AF_INET6" \
  --property=RuntimeMaxSec=1800 \
  --working-directory=/opt/questboard/current/migrator \
  /usr/bin/dotnet /opt/questboard/current/migrator/QuestBoard.Migrator.dll status
# stdout: JSON; exit status: the migrator's exit code (propagated by --wait).
```
SQL needs network, hence `AF_INET`/`AF_INET6` (ing-dashboard's variant used `AF_UNIX` only because PostgreSQL is local).

### 7. Mail, poll parsing, adopted version
```bash
# Mail (verified against a local SMTP sink; CRLF body, ASCII headers)
curl --silent --show-error --max-time 30 --url "smtp://${QB_SMTP_HOST}:${QB_SMTP_PORT}/questboard-deploy" \
  --mail-from "$QB_MAIL_FROM" --mail-rcpt "$QB_MAIL_TO" --upload-file "$msgfile"

# Poll (python3, no jq)
curl -fsS --max-time 30 -H 'Accept: application/vnd.github+json' "$LATEST_URL" \
  | python3 -c 'import sys,json; print(json.load(sys.stdin).get("tag_name",""))'

# Running version of the flat install (verified on a test publish)
python3 - /opt/questboard/QuestBoard.Service.dll <<'PY'
import re,sys
m = re.search(rb'(\d+\.\d+\.\d+)(?:\+[0-9a-f]{7,40})?\x00', open(sys.argv[1],'rb').read())
print(m.group(1).decode() if m else "")
PY
```
(The regex must be tightened against the real DLL during implementation; the test publish contained `5.3.3`, `5.3.3.0` and `5.3.3+<sha>` as UTF-8.)

### 8. Release workflow, build job (skeleton)
```yaml
name: release
on:
  push:
    tags: ['v[0-9]+.[0-9]+.[0-9]+']
permissions: {}
concurrency: { group: "release-${{ github.ref }}", cancel-in-progress: false }
jobs:
  build:
    runs-on: ubuntu-24.04
    timeout-minutes: 30
    permissions: { contents: write, id-token: write, attestations: write }
    outputs: { version: "${{ steps.tag.outputs.version }}" }
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with: { fetch-depth: 0, persist-credentials: false }
      - id: tag
        env: { TAG: "${{ github.ref_name }}" }
        run: build/validate-release-tag.sh >> "$GITHUB_OUTPUT"
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with: { dotnet-version: '10.0.x' }
      - run: dotnet build -c Release -p:Version="${{ steps.tag.outputs.version }}"   # version comes from the validated step
      - run: dotnet test -c Release --no-build
      - run: bash deploy/tests/run-all.sh
      - run: build/package-release.sh --version "${{ steps.tag.outputs.version }}" --commit "$GITHUB_SHA" --output artifacts/release
      - id: attest
        uses: actions/attest-build-provenance@4d101475d8b20a2381f78447822ac1eab6504dd8 # v4.2.2
        with: { subject-path: "artifacts/release/questboard-${{ github.ref_name }}.zip" }
      # copy steps.attest.outputs.bundle-path to questboard-<tag>.zip.sigstore.json,
      # then gh release create --draft --verify-tag (or upload --clobber while still a draft;
      # refuse if the release already exists and is published)
```
(Move step outputs into `env:` rather than inline `${{ }}` when finalising, to satisfy zizmor.)

### 9. Release workflow, publish job (skeleton)
```yaml
  publish:
    needs: build
    environment: deploy
    runs-on: ubuntu-24.04
    timeout-minutes: 15
    permissions: { contents: write }
    # steps: gh release download <tag> -> sha256sum -c -> gh attestation verify
    #        (--bundle, --repo, --signer-workflow .../release.yml, --source-ref refs/tags/<tag>,
    #         --deny-self-hosted-runners) -> gh release edit <tag> --draft=false --latest
```

### 10. Poll unit and timer (hardening set from ing-dashboard, paths adapted)
```ini
# questboard-deploy-poll.service
[Unit]
Description=Poll for a newer published release and hand it to the installer
After=network-online.target
Wants=network-online.target
[Service]
Type=oneshot
ExecStart=/usr/local/sbin/questboard-deploy poll
RuntimeDirectory=questboard-deploy
RuntimeDirectoryPreserve=yes
NoNewPrivileges=yes
PrivateTmp=yes
ProtectSystem=strict
ProtectHome=read-only
ProtectKernelTunables=yes
ProtectKernelModules=yes
ProtectControlGroups=yes
RestrictNamespaces=yes
LockPersonality=yes
RestrictRealtime=yes
RestrictSUIDSGID=yes
SystemCallArchitectures=native
ReadWritePaths=/opt/questboard /var/lib/questboard-deploy
# no CapabilityBoundingSet and no RestrictAddressFamilies: the installer is root, switches users and reaches GitHub

# questboard-deploy-poll.timer
[Timer]
OnBootSec=2min
OnUnitActiveSec=5min
RandomizedDelaySec=30
[Install]
WantedBy=timers.target
```
`setup` runs unsandboxed from a root shell, so the poll unit needs no write access to `/etc/systemd/system`. Manual `install`/`rollback` run unsandboxed too and share the lock file under `/run/questboard-deploy/`.

### 11. Pruning on the SQL CT (operator-installed; no database privilege)
```bash
#!/usr/bin/env bash
# keep the newest N pre-migration backups; run as root on the SQL host
set -euo pipefail
dir="${1:-/var/opt/mssql/data}"; keep="${2:-5}"
ls -1t "$dir"/QuestBoard_pre-*.bak 2>/dev/null | tail -n +"$((keep + 1))" | xargs -r rm -f --
```
Default Linux backup directory is `/var/opt/mssql/data` unless `filelocation.defaultbackupdir` was changed [CITED: SQL Server on Linux `mssql-conf` docs; the SQL CT's setting is unverified].

### 12. Gated real-SQL atomicity test (sketch)
```csharp
// Skips unless a connection string is supplied (CI service container only).
var cs = Environment.GetEnvironmentVariable("QUESTBOARD_MIGRATOR_TEST_CONNECTION");
Assert.SkipUnless(!string.IsNullOrEmpty(cs), "no SQL Server supplied");
// Create a uniquely named scratch database, run MigrationRunner.ApplyAtomically against a test DbContext whose
// assembly holds: 001 (creates table A) and 002 (creates table B, then migrationBuilder.Sql("SELECT 1/0")).
// Assert: exception thrown; __EFMigrationsHistory has no rows for either id; neither table exists.
// Second case: a migration with suppressTransaction: true => preflight refuses, history unchanged.
// Third case: the backup command produces a file name and msdb.dbo.backupset has a copy_only row. Drop the database in finally.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| EF 9: one transaction for all pending migrations | EF 10: one transaction per migration | EF Core 10 | Stock `Migrate()` no longer gives all-or-nothing; the migrator must supply the transaction |
| EF 8 and earlier: silent `Migrate()` after model drift | EF 9+: `PendingModelChangesWarning` throws | EF Core 9 | `HasPendingModelChanges()` was false for this repo this session, so the migrator will not throw it |
| `dotnet ef migrations bundle` as the deploy primitive | Dedicated migrator project with `status`/`backup`/`apply` | This phase (D-06) | Can report applied and pending state; no `sqlcmd`/`sa` on the App CT |
| Self-hosted runner pushes to production | Pull-based installer with attestation | This phase | GitHub holds no handle on the box |
| `actions/attest-build-provenance` as a standalone action | v4 is a thin wrapper over `actions/attest` | v4 | Either is valid; D-01 names the former |

**Deprecated/outdated:**
- The `workflow_dispatch` redeploy input and `/home/questboard/deploy.sh`: removed with the push path.
- ing-dashboard's claim that attestation verification is fully offline: only true with `--custom-trusted-root`.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `BACKUP ... TO DISK = '<file name only>'` resolves to the SQL instance's default backup directory (`/var/opt/mssql/data` on Linux) | Code Example 3, Pitfall 3 | Backup lands somewhere unexpected or fails; the gated test covers a Linux container, UAT must confirm on the real SQL CT |
| A2 | `systemd-run` works from inside the sandboxed root oneshot (`ProtectSystem=strict`, `NoNewPrivileges`) | Pitfall 5 | Migrator cannot be launched; fallback is a templated unit |
| A3 | The app's production login on the SQL CT has `BACKUP DATABASE` rights (docs example uses `sa`) | Code Example 3, Open Question 1 | Every release with pending migrations fails at backup (safe, but blocks deploys); `status` should report `canBackup` |
| A4 | GitHub-hosted `ubuntu-24.04` has `shellcheck`, `gh`, `python3`; `actionlint` and `zizmor` are obtainable | Standard Stack | CI lint steps need an install step |
| A5 | `mcr.microsoft.com/mssql/server:2022-latest` works as a service container with a `sqlcmd` health check | Validation Architecture | Gated test cannot run in CI until the service definition is fixed |
| A6 | Data Protection keys are at `/home/questboard/.aspnet/DataProtection-Keys` (default, not read) | Runtime State Inventory | If they are elsewhere, the runner cleanup caution is moot; if home is deleted anyway, sessions and tokens are invalidated |
| A7 | The production env file's SMTP settings equal the committed defaults (`192.168.6.13:25`, no TLS, `noreply@theunschut.com`) | Pitfall 8, Open Question 2 | Mail goes to the wrong relay; handover confirms |
| A8 | A systemd timer enabled after boot may not fire immediately | Pitfall 17 | Cosmetic; setup prints the manual install command anyway |
| A9 | `xp_delete_file` needs sysadmin and inspects file headers | Alternatives Considered | None if we never use it |
| A10 | `HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'BACKUP DATABASE')` is a valid permission probe for `status.canBackup` | Pattern 1 | Wrong function usage; verify in the gated test |
| A11 | Health wait of about 120 s is enough on 2 cores / 4 GB | Open Question 5 | False "unhealthy" on a slow start; make it a `deploy.conf` key |
| A12 | The SQL CT has enough free disk for several pre-migration backups | Open Question 1 | Backup fails (safe: install aborts before the app is touched) |
| A13 | Unauthenticated `releases/latest` ordering is by release creation, not semver | Pitfall 7 | None: the installer compares semver regardless |

## Open Questions

1. **Which SQL login does the app use, and how much disk does the SQL CT have?**
   - What we know: `docs/server-setup.md` shows `User Id=sa`; the env file is unreadable to the inspection account; SQL CT 103 is Ubuntu Linux; port 1433 is open from the App CT. [VERIFIED: ssh]
   - What's unclear: login privileges, backup directory, free space, database size.
   - Recommendation: add `canBackup` to `status`, and put `df -h /var/opt/mssql`, the database size query and a `RESTORE HEADERONLY` smoke test in the operator pre-flight checklist. If the login is not privileged, grant `db_backupoperator`.
2. **Which relay and sender does the app really use?**
   - What we know: committed defaults point at `192.168.6.13:25`, plain, `noreply@theunschut.com`; port 25 answers with a Postfix banner from the App CT.
   - What's unclear: whether the env file overrides them.
   - Recommendation: handover asks the operator to confirm; `deploy.conf.example` ships those defaults; a manual `questboard-deploy` test-mail subcommand is not in the decisions, so confirm by the first real outcome mail.
3. **Manual `rollback` to the adopted release.**
   - Recommendation: refuse (no migrator to run `status`); document manual steps. Automatic rollback to it works.
4. **Rewrite `questboard.service` or add a drop-in?**
   - What we know: the unit's inline content beyond the properties read via `systemctl show` is unknown; D-18 says `WorkingDirectory` and `ExecStart` change.
   - Recommendation: `setup` writes `/etc/systemd/system/questboard.service.d/10-release-layout.conf` (`WorkingDirectory=`, empty `ExecStart=` then the new one) and leaves the base file alone; the docs show the full unit for fresh installs. Planner decides.
5. **Health timeout default.** Recommend 120 s, configurable.
6. **Header name.** Recommend `X-QuestBoard-Version`.
7. **Where to run the tampered-artifact network test in CI.** Recommend `dotnet.yml` (every push) and the release build job; skip by default locally unless `QUESTBOARD_TEST_NETWORK=1`.
8. **Same-tag install semantics** (Pitfall 18): recommend restart plus health check only.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK (dev machine) | migrator build, tests | yes | 10.0.112 | — |
| `gh` (dev machine) | fixture and verify tests, workstation verification | yes | 2.102.0 | — |
| `shellcheck`, `actionlint`, `zizmor` (dev machine) | lint | no | — | Run in CI (hosted runner) |
| SQL Server at `localhost:1433` (dev machine) | gated atomicity test | no (connection refused) | — | Skip by default; CI service container; do not provision locally |
| `sqlcmd` (dev machine and CT) | none required | no | — | Not needed: the migrator does all SQL |
| App CT: curl, unzip, python3, systemd | installer | yes | curl 8.5.0, python3 3.12.3, systemd 255 | — |
| App CT: `gh` | attestation verify | no | — | `setup` installs from the official apt repo |
| App CT: `jq` | JSON | no | — | Not needed; use `python3` |
| App CT: .NET runtime | migrator and app | yes | Microsoft.NETCore.App / AspNetCore.App 10.0.9 | — |
| App CT: local `sendmail` | none | present but unusable | empty `relayhost` | Use `curl smtp://` to 192.168.6.13:25 |
| App CT -> `api.github.com`, release downloads, `tuf-repo-cdn.sigstore.dev`, `tuf-repo.github.com` | poll, install, verify | yes (HTTP 200/206 from the CT) | rate limit 60/h, 21 used | — |
| App CT -> Postfix CT :25, SQL CT :1433 | mail, migrator | yes (banner `220 Postfix.localdomain ESMTP Postfix (Debian/GNU)`; 1433 open) | — | — |
| Disk on App CT | releases | 4.0 GB free; zip 44 MB, unpacked about 73 MB per app | — | Keep 5 releases (about 0.5 GB with the migrator) |

**Missing dependencies with no fallback:** none for the App CT. The SQL CT's backup directory, free space and login privileges are unverified (Open Question 1).
**Missing dependencies with fallback:** `gh` (installed by `setup`); lint tools (CI); local SQL Server (skip gated tests, CI container).

## Validation Architecture

> `workflow.nyquist_validation` is `true` in `.planning/config.json`.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.2 (+ FluentAssertions 8.10.0) for C#; plain bash `check()` harness for the installer |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/xunit.runner.json`; `deploy/tests/run-all.sh` (new) |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Migrator"` and `bash deploy/tests/run-all.sh` |
| Full suite command | `dotnet test` (805 unit + 951 integration, 94 s measured) |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MH-2 | Every shipped migration is transactional (no `TransactionSuppressed`, no `ALTER DATABASE`/`FULLTEXT`/`MEMORY_OPTIMIZED`); regression guard for all future migrations | unit, no DB | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Migrator"` | no, Wave 0 |
| MH-2 | Preflight flags a synthetic `suppressTransaction: true` migration; status computes applied/pending/unknown; backup label and exit-code mapping | unit | same | no, Wave 0 |
| MH-2 | A failing second migration leaves history and schema unchanged; non-transactional pending is refused; backup file is created | integration, real SQL, gated | `QUESTBOARD_MIGRATOR_TEST_CONNECTION=... dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Migrator"` | no, Wave 0 |
| MH-3 | `/health` returns 200, body `Healthy`/`Degraded`, header equals `AppVersion.Current` | integration (InMemory) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardTimeZoneHealthCheck"` | partially (file exists, header test is new) |
| MH-4 | Semver gate, state remember-and-skip, outcome decision for all six D-11 rows, activation/previous/prune, secret-free mail body, config loader owner/mode checks | bash logic | `bash deploy/tests/questboard-deploy-logic-test.sh` | no, Wave 0 |
| MH-4 | Full install flow with stubbed `systemctl`, `systemd-run`, `gh`, `curl` for each matrix row; nothing changes on refusal | bash flow | `bash deploy/tests/install-flow-test.sh` | no, Wave 0 |
| MH-1/4 | Genuine artifact verifies; one flipped byte, wrong repo, wrong signer workflow, wrong source ref all refused; no ambient token reaches `gh`; refused install leaves no release or staging directory | bash network | `QUESTBOARD_TEST_NETWORK=1 bash deploy/tests/verify-rejects-tampered-artifact-network-test.sh` | no, Wave 0 (reuse ing-dashboard fixture) |
| MH-4 | Poll unit carries the hardening set and write allow-list; installer unit file shape | bash static | `bash deploy/tests/sandboxing-test.sh` | no, Wave 0 |
| MH-1 | Tag validation accepts `v1.2.3`, refuses `v01.2.3`, `v1.2.3-rc.1`, `v1.2`, off-main tags | bash | `bash build/tests/validate-release-tag-test.sh` | no, Wave 0 |
| MH-1 | Workflow hygiene: hash-pinned actions, no `${{ }}` in `run:`, no `self-hosted` | lint | `actionlint` + `zizmor` in CI; `! grep -rn self-hosted .github/workflows` | no, Wave 0 |
| MH-5/7 | Docs no longer mention the runner, `deploy.sh` or `/etc/questboard/.env` outside the removal handover | grep | `! grep -rnE "deploy\.sh|actions-runner|/etc/questboard/\.env" docs .planning/PROJECT.md` (allow the handover section) | no, Wave 0 |
| MH-5 | `setup` adoption, first install, runner removal | manual UAT on the CT | operator checklist below | n/a |

### Manual operator UAT (CT, after the first real release is published)
1. As root, run the installer once under the real poll unit (`systemctl start questboard-deploy-poll.service`) and confirm `systemd-run` of `migrator status` works inside the sandbox (Pitfall 5) and `gh attestation verify` initialises its cache.
2. `migrator status` against the production database: applied count equals 43 plus any newer, no unknown, `canBackup` true. Read-only.
3. Backup rehearsal: run `migrator backup --label rehearsal` once and confirm the file lands in the SQL CT backup directory; run the prune script with `keep=5`.
4. First install of the published release: outcome mail arrives once; `/health` carries the version header; `current` points at `releases/<ver>`; the adopted release remains.
5. Idle poll: journal line only, no mail. Remembered-bad-tag: one journal line, no mail.
6. Tampered rehearsal on a workstation: `build/verify-published-release.sh <tag>` ends with the one-byte-modified copy refused.
7. Runner removal (D-23) then two-sided check: `gh api repos/{owner}/{repo}/actions/runners` shows zero; `systemctl list-units 'actions.runner*'` empty.

The first real release is also the workflow rehearsal: the draft proves build, test, package and attest; nothing deploys until the `deploy` approval, so it is safe to push the tag and inspect the draft before approving.

### Sampling Rate
- **Per task commit:** the quick commands above for the area touched.
- **Per wave merge:** `dotnet test` and `bash deploy/tests/run-all.sh`.
- **Phase gate:** full suite green plus CI lint and the gated SQL test green in a hosted run before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `QuestBoard.Migrator/` project, added to `QuestBoard.slnx`, plus `QuestBoard.UnitTests/Migrator/` (covers MH-2).
- [ ] `QuestBoard.IntegrationTests/Migrator/` gated tests with a test `DbContext` and two hand-written migrations (covers MH-2 proof).
- [ ] `deploy/tests/` harness: `run-all.sh`, `lib/host-guard.sh`, fixtures copied from ing-dashboard, logic/flow/sandboxing/network tests (covers MH-4, MH-1).
- [ ] `build/tests/validate-release-tag-test.sh` (MH-1).
- [ ] CI: a job in `dotnet.yml` for `shellcheck`, `bash -n`, `run-all.sh`, and workflow lint; a SQL Server service container on the job that runs the gated tests.
- [ ] Framework installs: none (xUnit v3 already referenced); add a `ProjectReference` from the test projects to `QuestBoard.Migrator`.

## Security Domain

> `security_enforcement` is not set to false in `.planning/config.json`, so this section applies.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture (trust boundaries) | yes | Hosted-runner build, attestation, `deploy` approval, root installer, root-owned immutable release directories |
| V2 Authentication / V3 Session / V4 Access Control | no (app behaviour unchanged) | — |
| V5 Input Validation | yes | Strict `^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$` on tags; backup label `^[A-Za-z0-9._-]+$`; parameterised `BACKUP`; allow-list config loader; never `eval` or `source` data |
| V6 Cryptography | yes | Never hand-rolled: `sha256sum` for corruption, Sigstore via `gh` for authenticity |
| V7 Error Handling and Logging | yes | Journal for every poll outcome; mail bodies carry version, result, timestamps only |
| V8 Data Protection | yes | Connection string only in `/etc/questboard/env` (600) and in the transient migrator unit's environment; never on a command line, in mail or in logs |
| V10 Malicious Code / supply chain | yes | Pinned action SHAs, `--deny-self-hosted-runners`, signer workflow, source ref and main-ancestry pins, tag ruleset |
| V12 Files and Resources | yes | Verify before unzip; stage then `mv -T`; refuse symlinks in the tree; free-space check before unzip |
| V14 Configuration | yes | systemd hardening set; `deploy.conf` root-owned, not group/world writable |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Tampered or substituted release asset | Tampering | Checksum then `gh attestation verify` pinned to repo, workflow, ref; refuse on any failure, including an unreachable verifier |
| Tag created off `main` or workflow modified on a branch | Spoofing / Elevation | Tag validation in the workflow, attested commit must be reachable from `main`, tag ruleset, environment tag policy |
| Replay of an older attested release | Tampering | Install only a strictly newer semver; `rollback` is explicit and local |
| Compromised app rewrites its own code or the installer | Elevation | Release directories and `/opt/questboard` root-owned; installer and libs in `/usr/local`; `NoNewPrivileges` |
| Secret leakage via mail, logs, process list | Information disclosure | `systemd-run EnvironmentFile`; no secret arguments; fixed mail body; migrator prints no connection details |
| SQL injection through tag or label into `BACKUP` | Tampering | Label regex plus parameters |
| Concurrent installs or a concurrent migration | Tampering / DoS | `flock` on a runtime-directory file; app stopped during apply (EF's own lock is bypassed by design) |
| Partial migration | Tampering | One caller transaction, preflight refusal of non-transactional operations, gated proof test |
| Mail flood or rate-limit exhaustion | DoS | Remember-and-skip, journal-only idle polls, 12 polls/hour |
| Malicious zip contents | Tampering | Zip is attested before extraction; staged extraction; symlink refusal; manifest version must equal the tag |
| Stale or wrong state file | Tampering | State directory root-only under `ReadWritePaths`; an explicit `install <tag>` overrides |

## Sources

### Primary (HIGH confidence)
- EF Core source at tags v10.0.9 and v9.0.0 (raw.githubusercontent.com/dotnet/efcore): `Migrator.cs`, `MigrationCommandExecutor.cs`, `SqlServerMigrationsSqlGenerator.cs`, `SqlServerHistoryRepository.cs`, `RelationalOptionsExtension.cs`, `RelationalEventId.cs`, `RelationalStrings.resx` - read in full where cited.
- Installed package XML docs, `~/.nuget/packages/microsoft.entityframeworkcore.relational/10.0.9` and `microsoft.entityframeworkcore/10.0.9` - public API surface (`IMigrationsAssembly`, `IMigrationsSqlGenerator`, `IMigrator`, `IModelRuntimeInitializer`, `MigrationCommand`); xunit.v3.assert 3.2.2 (`Assert.Skip`, `SkipUnless`).
- Compiled spike over `QuestBoard.Repository` (43 migrations, 164 commands, 0 flagged) and a real `dotnet publish` of the Service project (77 entries, 73 MB, zip 44 MB).
- `gh 2.102.0` runs against the ing-dashboard fixture (online, no network, custom trusted root, tampered, wrong ref, unwritable cache).
- Read-only SSH to the App CT (versions, unit properties, reachability of GitHub, TUF hosts, Postfix CT, SQL CT, rate-limit headers).
- ing-dashboard `release.yml`, `build/*.sh`, `deploy/bin`, `deploy/lib`, `deploy/systemd`, `deploy/tests`, `docs/deploy.md`, `docs/releasing.md`, `docs/github-repository-settings.md`.
- Repo files: `Program.cs`, `AppVersion.cs`, `ServiceExtensions.cs`, `QuestBoardContext.cs`, `appsettings.json`, `Dockerfile`, workflows, `docs/server-setup.md`, `AmbientClockSeamTests.cs`.
- A full local `dotnet test` run (805 + 951 passing).

### Secondary (MEDIUM confidence)
- learn.microsoft.com EF Core 9 breaking changes (single transaction; explicit-transaction exception and mitigation) and EF Core 10 breaking changes (no mention of migrations; the revert is stated on the EF 9 page).
- learn.microsoft.com BACKUP (Transact-SQL) (transaction restriction; permissions; COPY_ONLY) and SERVERPROPERTY (`InstanceDefaultBackupPath` is SQL Server 2019 and later).
- GitHub CLI manual text via `gh attestation verify --help` and `gh attestation trusted-root --help`.

### Tertiary (LOW confidence)
- Community sources on `xp_delete_file` behaviour and the SQL Server on Linux default backup directory (web search; marked [ASSUMED] where used).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - every version and SHA was resolved this session.
- Architecture: HIGH for the migrator, verification and mail paths; MEDIUM for `systemd-run` inside the sandbox and for SQL-CT backup placement.
- Pitfalls: HIGH for items 1-4, 6-8, 10-12, 14; MEDIUM for 5, 13, 17.

**Research date:** 2026-10-04
**Valid until:** 2026-11-03 for the stack; re-check `gh` and action SHAs at implementation time (fast-moving), and re-read EF `Migrator.cs` if EF is bumped past 10.0.9.
