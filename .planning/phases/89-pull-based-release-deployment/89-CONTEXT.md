# Phase 89: Pull-Based Release Deployment - Context

**Gathered:** 2026-10-04
**Status:** Ready for planning

<domain>
## Phase Boundary

A tagged release reaches production because the App CT goes and fetches it. Today GitHub pushes it
there through a self-hosted runner. When this phase is done:

- A hosted runner builds, tests and attests the release.
- An approval publishes it.
- A systemd timer on the App CT notices the new release, verifies it, and installs it into a
  versioned directory. The installer runs any migrations as a separate atomic step, restarts the
  app, checks that the new version is healthy, and then either rolls back or stops for a person
  if something is wrong.
- GitHub has no runner, credential or other handle on the production box.

In scope:
- The release workflow.
- A new migrator console project.
- The installer, its systemd units and its config, all under a new `deploy/` directory.
- Rewriting the deploy parts of `docs/server-setup.md`.
- An explicit operator handover for the one-time server setup and for removing the runner.

Out of scope:
- The Docker path: `docker-publish.yml`, the `Dockerfile` and `docker-compose.yml` stay exactly
  as they are.
- Any change to application behaviour beyond what the installer needs to confirm which version
  is running.
- Metrics and monitoring infrastructure.
- Automated database restore.

</domain>

<decisions>
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
- **D-03: Tag validation refuses**:
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
- **D-08: The startup** `context.Database.Migrate()` in
  `QuestBoard.Repository/Extensions/ServiceExtensions.cs` stays unchanged. On the server it is
  a no-op, because nothing is pending by the time the app starts. Docker and dev keep migrating on
  startup with no new setup step, which satisfies the self-hosting constraint.
- **D-09: Back up the database before migrating, only when migrations are pending.** The
  migrator runs `BACKUP DATABASE … WITH COPY_ONLY` on the SQL CT, named after the release and a
  UTC timestamp. `COPY_ONLY` leaves any existing backup chain alone. **If the backup fails, the
  install aborts before the running app is touched.** Keep the last few pre-migration backups.
  How pruning works (the files sit on the SQL CT's disk, not the App CT's) is a research item;
  if there is no clean way, document manual pruning.
- **D-10: Install order**:
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

- **D-13: Poll timer**: `OnBootSec=2min`, `OnUnitActiveSec=5min`, `RandomizedDelaySec=30`, the
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

- **D-18: Versioned layout inside the existing root**: `/opt/questboard/releases/<version>/`,
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
- **D-20: Subcommands**:
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

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase definition
- `.planning/ROADMAP.md` § "Phase 89: Pull-Based Release Deployment": goal, current push path,
  reference model, scope notes. Its `/etc/questboard/.env` path is wrong; see the production facts
  above.
- `CLAUDE.md` and `.claude/architecture.md`: EF packages only in `QuestBoard.Repository`;
  migrations auto-applied on startup; the local DB contract (`localhost:1433`, SQL auth via
  user-secrets on Linux); don't provision environments; no planning IDs in source comments.

### Current push path (to replace)
- `.github/workflows/binary-release.yml`: the hosted `release` job plus the `self-hosted` `deploy`
  job and the `workflow_dispatch` input.
- `docs/server-setup.md`: §1 deploy script, sudoers, systemd unit and runner install; §5
  Deploying; "Checking logs".
- `.github/workflows/docker-publish.yml`: must stay untouched.
- `.github/workflows/dotnet.yml`: main/PR CI, for comparison with the release test gate.

### Code the phase touches or depends on
- `QuestBoard.Repository/Extensions/ServiceExtensions.cs`: `ConfigureDatabase()`, the startup
  `Migrate()` that stays.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs`: constructor needs
  `DbContextOptions`, `IActiveGroupContext` and an optional `TimeProvider`.
- `QuestBoard.Repository/Migrations/`: 44 migrations, none non-transactional as of 2026-10-04.
- `QuestBoard.Service/Program.cs`: line 42 `AddHealthChecks()…board-timezone`; line 373
  `MapHealthChecks("/health")`; line ~376 runs `ConfigureDatabase()` before `app.Run()` except in
  the `Testing` environment.
- `QuestBoard.Service/Helpers/AppVersion.cs`: the informational version with the `+sha` suffix
  stripped, used by the footer.
- `QuestBoard.slnx`: the new migrator project gets added here.

### Reference implementation (port, not copy): `/mnt/Data/repos/ing-dashboard`
- `.github/workflows/release.yml`: build, test, attest, draft, then `deploy`-environment publish
  with re-verification.
- `build/validate-release-tag.sh`, `build/package-release.sh`,
  `build/verify-published-release.sh`, `build/check-github-settings.sh`.
- `deploy/bin/ledger-deploy`, `deploy/lib/deploy.sh` (verification, pending-migration
  computation, activation, rollback decision, health wait, outcome reporting),
  `deploy/lib/common.sh`.
- `deploy/systemd/ledger-deploy-poll.timer` and `ledger-deploy-poll.service`: timer cadence and
  sandboxing set.
- `deploy/deploy.conf.example`: config shape.
- `deploy/tests/ledger-deploy-logic-test.sh`,
  `deploy/tests/verify-rejects-tampered-artifact-network-test.sh`: test patterns.
- `docs/deploy.md`, `docs/releasing.md`: documentation shape. **Note the difference:**
  ing-dashboard's docs say it emails on every poll. This phase deliberately does not (D-14), and
  it runs migrations through a dedicated migrator rather than an EF bundle (D-06).

### External
- EF Core 9 breaking changes,
  https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/breaking-changes : "All
  pending migrations are applied in a single transaction" (reverted in EF 10), and "Exception is
  thrown when applying migrations in an explicit transaction" (`MigrationsUserTransactionWarning`
  and its suppression).
- `/mnt/Data/repos/brainfarts/environment.md` §2–§3: CT IDs and addresses (App CT 102 `.12`,
  MS-Sql 103 `.10`, Postfix 105 `.13`, Traefik 101 `.8`, Ledger 108 `.16`), verified
  2026-10-04.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AppVersion.Current`: already yields the running version string, which the installer's
  "new version is answering" check can use.
- The health checks pipeline (`AddHealthChecks()` / `MapHealthChecks("/health")`): the
  installer's health wait targets `http://127.0.0.1:5000/health` on the CT.
- `QuestBoard.Repository`'s `AddDbContext<QuestBoardContext>` registration: the migrator can
  reuse the same registration shape with a stub `IActiveGroupContext`.

### Established Patterns
- Migrations auto-apply on startup, and that is a project constraint. The migrator adds an
  earlier step and leaves the startup call in place.
- Configuration comes from environment variables in `/etc/questboard/env`, double-underscore
  form.
- No DataProtection key persistence is configured, so keys go to the `questboard` user's home.
  No upload handling writes to the content root (images are stored through the DB). **Research
  must confirm the app writes nothing under its content root**, because release directories
  become read-only to it.
- Every release so far is a lightweight tag on a `main` merge commit.

### Integration Points
- The release workflow, i.e. a new or rewritten `binary-release.yml`.
- `QuestBoard.slnx`, which gains the migrator project. The release zip must contain the app, the
  migrator and the `deploy/` directory.
- The App CT: `/opt/questboard`, `questboard.service`, `/etc/questboard/`, `/usr/local/sbin`, new
  systemd timer and service units, and SMTP to the relay.
- The SQL CT (`192.168.6.10`), reached only through the migrator's existing SQL connection, for
  `BACKUP DATABASE`.

</code_context>

<specifics>
## Specific Ideas

- The operator asked directly whether migrations could run without starting the app, and then
  whether they could run in one transaction so a failure leaves the database untouched. Both are
  now decisions (D-06, D-07). The goal behind them: **a failed migration must always leave
  production in a state where the previous version can safely come back up.**
- Port ing-dashboard's model but keep this project's own constraints: the Resend budget, the
  migrate-on-startup architecture, and the Docker self-host path.
- Production inspection uses the unprivileged `claude` account
  (`ssh -i ~/.ssh/questboard-lxc-claude -o IdentitiesOnly=yes claude@192.168.6.12`). It cannot
  read `/etc/questboard/env`. Use `systemctl show -p …`, never `systemctl cat`, in case of
  inline secrets. Anything privileged goes to the operator as a script.

### Research items for the researcher/planner
- Prove D-07 against a real SQL Server: a deliberately failing second migration leaves
  `__EFMigrationsHistory` and the schema unchanged. Decide whether that runs as a test gated on
  `localhost:1433` being reachable, or as a scripted manual verification step.
- Confirm EF 10's `Migrate()` executes inside a caller-supplied transaction once the warning is
  suppressed, and how to detect non-transactional operations up front. EF 9 added a warning for
  them, which might be configurable as an error inside the migrator.
- How `gh attestation verify --bundle` behaves offline on the CT: whether it needs the sigstore
  trusted root over the network, or a `--custom-trusted-root`. ing-dashboard treats an
  unreachable service as a failure.
- How to prune old pre-migration `.bak` files on the SQL CT (D-09), and the SQL CT's default
  backup directory and free disk space.
- The SMTP relay the app actually uses (D-17), which the handover confirms with the operator.

</specifics>

<deferred>
## Deferred Ideas

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

</deferred>

---

*Phase: 89-pull-based-release-deployment*
*Context gathered: 2026-10-04*
