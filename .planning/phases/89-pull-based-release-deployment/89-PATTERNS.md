# Phase 89: Pull-Based Release Deployment - Pattern Map

**Mapped:** 2026-10-04
**Files analyzed:** 30 (new or modified)
**Analogs found:** 28 / 30 (most are cross-repo ports from `/mnt/Data/repos/ing-dashboard`, abbreviated `ING:`)

Convention: `ING:` = `/mnt/Data/repos/ing-dashboard`. Port, do not copy: rename `ledger`/`LEDGER_` to `questboard`/`QUESTBOARD_`, and never put planning IDs (D-xx, Phase 89, MH-n) in any file below (scripts, units, YAML, C#). ING's `ledger.service` and `ledger_*` function prefix become `questboard_*`.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `.github/workflows/release.yml` (replaces `binary-release.yml`, delete it) | config (CI) | batch + approval gate | `ING:.github/workflows/release.yml` | exact (adapt) |
| `.github/workflows/dotnet.yml` (add shell test, shellcheck, gated SQL jobs) | config (CI) | batch | itself, `ING:.github/workflows/ci.yml` | role-match |
| `build/validate-release-tag.sh` | utility | transform | `ING:build/validate-release-tag.sh` | exact |
| `build/package-release.sh` | utility | file-I/O | `ING:build/package-release.sh` | role-match (no efbundle, adds migrator and `deploy/`) |
| `build/verify-published-release.sh` | utility | request-response | `ING:build/verify-published-release.sh` | exact |
| `build/tests/validate-release-tag-test.sh` | test | transform | `ING:build/tests/` | exact |
| `deploy/bin/questboard-deploy` | controller (CLI dispatcher) | request-response | `ING:deploy/bin/ledger-deploy` | exact (adds `setup`) |
| `deploy/lib/common.sh` | utility | transform | `ING:deploy/lib/common.sh` | exact (mail via curl SMTP, no metrics) |
| `deploy/lib/deploy.sh` | service | event-driven, file-I/O | `ING:deploy/lib/deploy.sh` | role-match (migrator, matrix differs) |
| `deploy/systemd/questboard-deploy-poll.service` | config | event-driven | `ING:deploy/systemd/ledger-deploy-poll.service` | exact |
| `deploy/systemd/questboard-deploy-poll.timer` | config | event-driven | `ING:deploy/systemd/ledger-deploy-poll.timer` | exact |
| `deploy/systemd/questboard.service` (path change, ideally a drop-in) | config | n/a | live unit (read via `systemctl show -p`), `docs/server-setup.md` lines 110-133 | partial |
| `deploy/deploy.conf.example` | config | n/a | `ING:deploy/deploy.conf.example` | exact |
| `deploy/sql-ct/prune-premigration-backups.sh` | utility | batch | none in repo | no analog |
| `deploy/tests/run-all.sh`, `lib/host-guard.sh` | test | n/a | `ING:deploy/tests/lib/host-guard.sh` | exact |
| `deploy/tests/questboard-deploy-logic-test.sh` | test | transform | `ING:deploy/tests/ledger-deploy-logic-test.sh` | exact |
| `deploy/tests/verify-rejects-tampered-artifact-network-test.sh` + `fixtures/` | test | request-response | `ING:deploy/tests/verify-rejects-tampered-artifact-network-test.sh` | exact |
| `QuestBoard.Migrator/QuestBoard.Migrator.csproj` | config | n/a | `QuestBoard.Repository/QuestBoard.Repository.csproj`, `QuestBoard.Service.csproj` | role-match |
| `QuestBoard.Migrator/Program.cs` | controller (CLI) | request-response | `QuestBoard.Service/Program.cs` (config), `ServiceExtensions.cs` | partial |
| `QuestBoard.Migrator/MigrationRunner.cs` | service | CRUD (schema) | `ServiceExtensions.ConfigureDatabase` | partial |
| `QuestBoard.slnx` (add Migrator project) | config | n/a | itself | exact |
| `QuestBoard.Service/Program.cs` (health version header) | middleware | request-response | itself line 42, 373 | exact |
| `QuestBoard.Service/Helpers/AppVersion.cs` (consumer only) | utility | transform | itself | exact |
| `QuestBoard.UnitTests/Migrator/*Tests.cs` | test | transform | `QuestBoard.UnitTests` conventions | role-match |
| `QuestBoard.IntegrationTests/Migrator/*Tests.cs` (gated real SQL) | test | CRUD | `BoardTimeZoneHealthCheckTests.cs` conventions | partial |
| `QuestBoard.IntegrationTests/Controllers/*HealthVersion*Tests.cs` | test | request-response | `BoardTimeZoneHealthCheckTests.cs` | exact |
| `docs/deploy.md`, `docs/releasing.md` | docs | n/a | `ING:docs/deploy.md`, `ING:docs/releasing.md` | exact |
| `docs/server-setup.md` (rewrite deploy parts) | docs | n/a | itself | exact |
| `.planning/PROJECT.md` line 163 (`.env` to `env`) | docs | n/a | itself | exact |

## Pattern Assignments

### `.github/workflows/release.yml` (CI, batch + approval)

**Analog:** `ING:.github/workflows/release.yml` (152 lines). Copy structure: `permissions: {}` at top, `concurrency` group per ref with `cancel-in-progress: false`, `build` job (contents:write, id-token:write, attestations:write), `publish` job with `environment: deploy`.

**Trigger and pinned actions** (ING lines 1-48):
```yaml
on:
  push:
    tags:
      - 'v[0-9]+.[0-9]+.[0-9]+'
permissions: {}
...
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with: { fetch-depth: 0, persist-credentials: false }
      - name: Validate release tag
        id: tag
        env:
          TAG: ${{ github.ref_name }}
        run: build/validate-release-tag.sh >> "$GITHUB_OUTPUT"
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
```
Adapt: QuestBoard has no `global.json` check it; else `dotnet-version: '10.0.x'` (current `binary-release.yml` uses that). Drop the postgres service. Test step becomes `dotnet test --solution QuestBoard.slnx` (or plain `dotnet test`, as `dotnet.yml` does). Package step runs `build/package-release.sh`, then tests (D-04: tests run before attest), then:
```yaml
      - uses: actions/attest-build-provenance@4d101475d8b20a2381f78447822ac1eab6504dd8 # v4.2.2
        with:
          subject-path: artifacts/release/questboard-${{ steps.tag.outputs.version }}.zip
```
**Asset naming warning:** current asset is `questboard-<tag>.zip` (with `v`); ING uses `ledger-<version>.zip` (no `v`). Pick one and keep installer, workflow and docs consistent.

**Draft create/upload with overwrite refusal** (ING lines 76-100): copy verbatim (`gh release view --json isDraft`, `--clobber` only on drafts, `gh release create --draft --verify-tag`). Replace the generic notes with text telling the operator to write title and notes before approving.

**Publish job re-verification** (ING lines 102-152): copy verbatim; this is exactly the pinned form to require:
```yaml
gh attestation verify "release/ledger-$VERSION.zip" \
  --bundle "release/ledger-$VERSION.zip.sigstore.json" \
  --repo "$GITHUB_REPOSITORY" \
  --signer-workflow "$GITHUB_REPOSITORY/.github/workflows/release.yml" \
  --source-ref "refs/tags/$TAG" \
  --deny-self-hosted-runners
...
gh release edit "$TAG" --repo "$GITHUB_REPOSITORY" --draft=false --latest
```
**Fix vs current repo workflow** (`.github/workflows/binary-release.yml`): it interpolates `${{ github.ref_name }}` into `run:`; pass through `env:` instead (as ING does). It uses unpinned `@v4`/`@v2` actions; pin by SHA like ING. Remove `workflow_dispatch` and the `runs-on: self-hosted` `deploy` job entirely. Replacing the file changes the signer-workflow path to `release.yml`; update any README badge URL.

---

### `build/validate-release-tag.sh` (utility, transform)

**Analog:** `ING:build/validate-release-tag.sh` (41 lines). Copy as is. Key excerpt:
```bash
if ! [[ "$TAG" =~ ^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "::error::Tag is not a strict semver tag ..." >&2; exit 1
fi
TAG_COMMIT="$(git rev-parse --verify --quiet "refs/tags/${TAG}^{commit}")" ...
git merge-base --is-ancestor "$TAG_COMMIT" "$MAIN_REF"
printf 'version=%s\n' "$VERSION"
```
Lightweight tags are fine (`refs/tags/X^{commit}` handles both). Use `fetch-depth: 0` in the workflow so `origin/main` exists.

### `build/package-release.sh` (utility, file-I/O)

**Analog:** `ING:build/package-release.sh` lines 1-60 (arg parsing `--version/--commit/--output`, strict semver and 40-hex validation, `STAGE_DIR`, `dotnet publish ... -p:Version -p:SourceRevisionId -p:ContinuousIntegrationBuild=true`).

**Adapt:**
- Publish `QuestBoard.Service/QuestBoard.Service.csproj` to `stage/app` and `QuestBoard.Migrator` to `stage/migrator`, both `-c Release -r linux-x64 --self-contained false` (CT has the shared runtime 10.0.9).
- Delete the `dotnet ef migrations bundle` and `migrations list` blocks and `dotnet tool restore` (D-06 rejects the bundle; `.config/dotnet-tools.json` pins dotnet-ef 9.0.6 anyway).
- Copy `deploy/` into the stage minus `deploy/tests/`; write `release-manifest.json` with `version`, `commit`, `healthVersionHeader`.
- `-p:Version` is what feeds `AppVersion.Current` (`AssemblyInformationalVersion` with `+sha` stripped); the version header and installer comparison depend on it.

### `build/verify-published-release.sh`, `build/tests/validate-release-tag-test.sh`

**Analogs:** `ING:build/verify-published-release.sh` (110 lines), `ING:build/tests/`. Port with renamed assets. Needed for the bootstrap (operator verifies first zip on a workstation before `setup`).

---

### `deploy/bin/questboard-deploy` (CLI dispatcher)

**Analog:** `ING:deploy/bin/ledger-deploy` (252 lines).

**Library location and relocatable root** (ING lines 12-31):
```bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -f "${SCRIPT_DIR}/../lib/common.sh" ]; then LIB_DIR="$(cd "${SCRIPT_DIR}/../lib" && pwd)"
else LIB_DIR="/usr/local/lib/ledger"; fi
source "${LIB_DIR}/common.sh"; source "${LIB_DIR}/deploy.sh"
DEPLOY_ROOT="${LEDGER_DEPLOY_ROOT:-}"
LOCK_PATH="${DEPLOY_ROOT}/run/ledger-deploy/deploy.lock"
STATE_DIR="${DEPLOY_ROOT}/var/lib/ledger-deploy/state"
RELEASES_DIR="${DEPLOY_ROOT}/opt/ledger/releases"
CURRENT_LINK="${DEPLOY_ROOT}/opt/ledger/current"
CONF_PATH="${LEDGER_DEPLOY_CONF:-${DEPLOY_ROOT}/etc/ledger/deploy.conf}"
```
Adapt: `/usr/local/lib/questboard-deploy`, `/opt/questboard/releases`, `/etc/questboard/deploy.conf`, `/var/lib/questboard-deploy`. `setup` runs from an unpacked zip, so the `${SCRIPT_DIR}/../lib` branch is the live path there.

**Usage and `require_root`** (ING lines 33-52): keep `poll`, `install`, `rollback`, `verify`; add `setup`. `install` must also be the override for a remembered tag.

**Differences from ING to implement:** no textfile metrics; poll never emails; add skip-remembered-tag state (poll writes one journal line); `--from-dir` may be dropped. Adopt/`setup` has no ING analog (see No Analog).

### `deploy/lib/common.sh` (utility)

**Analog:** `ING:deploy/lib/common.sh` (186 lines). Reuse: `*_log` (stderr, UTC timestamp, lines 22-24), `*_die` (27-30), `*_load_conf` allow-list loader with owner and mode checks (41-97; never `source` the file; under a test root the expected owner is the current uid), `*_semver_gt` (170-186). Drop `ledger_write_textfile_metrics` (no metrics).

**Replace `ledger_notify_email`** (lines 143-166 pipe to `msmtp -t`, which is absent on the CT). Keep its shape (empty recipient skips, failure logs and returns 0, body secret-free) but send via curl:
```bash
{ printf 'From: %s\nTo: %s\nSubject: [questboard-deploy] %s\n\n%s\n' ...; } > "$msg"
curl --silent --show-error --max-time 30 --url "smtp://${HOST}:${PORT}" \
  --mail-from "$FROM" --mail-rcpt "$TO" --upload-file "$msg" || questboard_log "sending notification email failed"
```
Config keys: relay host, port, sender, recipient.

### `deploy/lib/deploy.sh` (service)

**Analog:** `ING:deploy/lib/deploy.sh` (605 lines). Function map to port:

| ING function (lines) | Use |
|---|---|
| `ledger_verify_attestation` (17-53) | port, but apply the sandbox fix below |
| `ledger_commit_on_branch` (55-80) | port (attested commit reachable from main via compare API) |
| `ledger_fetch_latest_tag` (82-96) | port; unauthenticated `releases/latest`, parse with `python3` (no `jq`); compare semver against active, not trust "latest" |
| `ledger_activate_release` (250-276) | port as is |
| `ledger_rollback_decision` (278-286) | extend into one pure `decide_outcome` covering the whole matrix |
| `ledger_prune_releases` (291-336) | port; never prune active or previous |
| `ledger_wait_for_health` (338-376) | adapt |
| `ledger_install_verified_release` (416-556), `ledger_rollback_release` (558-) | port orchestration; insert migrator steps |
| `ledger_pending_migrations`, `ledger_read_applied_migrations` (169-248) | drop; migrator `status --json` replaces them |
| metrics and provisioning functions (98-148, 378-415) | drop |

**Attestation call to fix** (ING lines 24-41). ING sets only `GH_CONFIG_DIR`; research shows `gh` also needs a writable cache under `ProtectHome=read-only`:
```bash
t="$(mktemp -d)"
env -u GH_TOKEN -u GITHUB_TOKEN -u GH_ENTERPRISE_TOKEN \
  GH_CONFIG_DIR="$t" XDG_CACHE_HOME="$t/cache" XDG_STATE_HOME="$t/state" \
  GH_TELEMETRY=false GH_NO_UPDATE_NOTIFIER=1 GH_PROMPT_DISABLED=1 \
  gh attestation verify "$artifact" --bundle "$bundle" --repo "$repo" \
    --signer-workflow "${repo}/${signer_workflow}" --source-ref "$source_ref" \
    --deny-self-hosted-runners --format json
```
Any failure (including unreachable TUF) is a refusal; no bypass.

**Health wait** (ING 338-376 checks `/health` body equals `Healthy` and a metrics build-info line matching the version). Adapt: `curl -D-` `http://127.0.0.1:5000/health`, accept 200 with body `Healthy` (decide on `Degraded`, which the app returns 200 for when the board zone falls back to UTC) AND the version response header equal to the installed version.

**Rollback decision** (ING 278-286 is binary on `migrated`). Required matrix rows (outcomes: refused, failed, failed_rolled_back, rolled_back, halted, installed) come from the context's outcome table; make the function pure so the logic test covers every row.

**Migrator invocation:** `systemd-run --pipe --wait --collect -p User=questboard -p EnvironmentFile=/etc/questboard/env dotnet <release>/migrator/QuestBoard.Migrator.dll status|backup|apply`. Never `source` the env file.

### `deploy/systemd/questboard-deploy-poll.service` and `.timer`

**Analogs:** `ING:deploy/systemd/ledger-deploy-poll.service` and `.timer`. Copy:
```ini
[Service]
Type=oneshot
ExecStart=/usr/local/sbin/ledger-deploy poll
RuntimeDirectory=ledger-deploy
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
ReadWritePaths=/opt/ledger ... /var/lib/ledger-deploy
```
```ini
[Timer]
OnBootSec=2min
OnUnitActiveSec=5min
RandomizedDelaySec=30
[Install]
WantedBy=timers.target
```
Adapt `ReadWritePaths=/opt/questboard /var/lib/questboard-deploy` (and `/etc/systemd/system` plus `/usr/local/sbin /usr/local/lib` only if `setup` runs under this unit; `setup` is run by the operator in a shell, so probably not). Drop the grafana, prometheus and msmtp log paths. Caution: `systemd-run` and `systemctl stop/start` from inside a `ProtectSystem=strict` unit need the system bus reachable; verify in UAT.

### `deploy/systemd/questboard.service`

No in-repo copy. Source of truth is the live unit (`User=questboard`, `WorkingDirectory=/opt/questboard`, `ExecStart=/usr/bin/dotnet /opt/questboard/QuestBoard.Service.dll`, `EnvironmentFile=/etc/questboard/env`) and `docs/server-setup.md` lines 110-133. New paths: `/opt/questboard/current/app` and `.../QuestBoard.Service.dll`. Research prefers a drop-in over rewriting the unit.

### `deploy/deploy.conf.example`

**Analog:** `ING:deploy/deploy.conf.example` (29 lines): comment header (copy to `/etc/.../deploy.conf`, root-owned, not group/world-writable) then `KEY=value` lines. Keep `*_GITHUB_REPO`, `*_SIGNER_WORKFLOW=.github/workflows/release.yml`, `*_NOTIFY_EMAIL`, `*_KEEP_RELEASES=5`, `*_HEALTH_TIMEOUT_SECONDS`; replace `OPS_URL` with `http://127.0.0.1:5000`; drop `TEXTFILE_DIR`; add SMTP host, port, sender. Placeholders only; no secrets.

### `deploy/tests/*`

**Analogs:** `ING:deploy/tests/lib/host-guard.sh` (copy verbatim; stubs `systemctl` and `pkexec` to log and fail, prepends to PATH; extend the stub list with `systemd-run`).
`ING:deploy/tests/ledger-deploy-logic-test.sh` skeleton (lines 1-40):
```bash
set -euo pipefail
export LEDGER_DEPLOY_ROOT; LEDGER_DEPLOY_ROOT="$(mktemp -d)"
trap 'rm -rf "${LEDGER_DEPLOY_ROOT}"' EXIT
source "${SCRIPT_DIR}/lib/host-guard.sh"; host_guard_install "$LEDGER_DEPLOY_ROOT"
source "${REPO_ROOT}/deploy/lib/common.sh"; source "${REPO_ROOT}/deploy/lib/deploy.sh"
FAILURES=0
check() { local description="$1" expected="$2" actual="$3"; ... PASS/FAIL, FAILURES++ ; }
```
Cases to write: semver, outcome decision per matrix row, activation atomicity, prune keeps active and previous, remembered-tag skip, email body contains no paths or connection strings.
`ING:deploy/tests/verify-rejects-tampered-artifact-network-test.sh` plus `fixtures/public-attested-artifact.{env,sigstore.jsonl}`: copy the two fixture files and port the test (downloads one public attested asset, checks sha, expects pass for the genuine artifact and refusal for tampered bytes, wrong repo, wrong signer workflow, wrong source ref). Research confirmed the fixture verifies with gh 2.102.0. Needs the network; keep out of the default offline run.

---

### `QuestBoard.Migrator/QuestBoard.Migrator.csproj`

**Analog:** `QuestBoard.Repository/QuestBoard.Repository.csproj` (lines 1-8 property group) and the test csproj ProjectReference form.
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\QuestBoard.Repository\QuestBoard.Repository.csproj" />
  </ItemGroup>
</Project>
```
**No EF `PackageReference`** (they come through Repository). Add `<Project Path="QuestBoard.Migrator/QuestBoard.Migrator.csproj" />` to `QuestBoard.slnx` next to the other three non-test projects (flat `<Project>` entries after the `/Tests/` folder). The Dockerfile restores `QuestBoard.Service.csproj` only, so the image build is unaffected. For configuration reading add `Microsoft.Extensions.Configuration.EnvironmentVariables` only if not already transitive; check before adding any package.

### `QuestBoard.Migrator/MigrationRunner.cs` and `Program.cs`

**Analog (DbContext construction):** `QuestBoard.Repository/Extensions/ServiceExtensions.cs` lines 14-17, 53-60:
```csharp
services.AddDbContext<QuestBoardContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
...
var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
context.Database.Migrate();
```
**Constructor requirement** (`QuestBoard.Repository/Entities/QuestBoardContext.cs` lines 8-12):
```csharp
public class QuestBoardContext(
    DbContextOptions<QuestBoardContext> options,
    IActiveGroupContext activeGroupContext,
    TimeProvider? timeProvider = null)
```
`IActiveGroupContext` (`QuestBoard.Domain/Interfaces/IActiveGroupContext.cs`) has one member, `int? ActiveGroupId { get; }`; the stub returns null.

**Migrator build pattern (new, from research):** `new DbContextOptionsBuilder<QuestBoardContext>().UseSqlServer(cs).ConfigureWarnings(w => w.Ignore(RelationalEventId.MigrationsUserTransactionWarning))`; then `using var tx = db.Database.BeginTransaction(); db.Database.Migrate(); tx.Commit();`. Note: do not enable `EnableRetryOnFailure` (retrying strategy makes EF throw). Preflight non-transactional detection via `IMigrationsSqlGenerator.Generate(migration.UpOperations, ...)` and `MigrationCommand.TransactionSuppressed`. Make `MigrationRunner` public and `DbContext`-generic so tests can drive it. Exit-code contract and JSON-on-stdout/logs-on-stderr in RESEARCH Pattern 1. Never print the connection string or host. Use `TimeProvider.System.GetUtcNow()` for the backup timestamp; `BACKUP` cannot be inside the transaction, so it is its own command run before the app is stopped.

**Config key:** `ConnectionStrings:DefaultConnection`, i.e. env var `ConnectionStrings__DefaultConnection`, same as the app.

### `QuestBoard.Service/Program.cs` (health version header)

**Analog:** itself. Existing pieces:
- line 42: `builder.Services.AddHealthChecks().AddCheck<BoardTimeZoneHealthCheck>("board-timezone");`
- line 373: `app.MapHealthChecks("/health");`
- lines 375-378: migrations run via `app.Services.ConfigureDatabase()` unless `Testing` (leave unchanged).

Add a response header (e.g. `X-App-Version`) carrying `AppVersion.Current` only on `/health`, using the project's existing middleware style in Program.cs, or `MapHealthChecks("/health").Add(b => ...)` endpoint convention. `AppVersion.Current` (`QuestBoard.Service/Helpers/AppVersion.cs`) reads `AssemblyInformationalVersion` and strips `+sha`; in a `Testing` host it may be `dev`, so tests should assert header equals `AppVersion.Current` rather than a literal. Keep the body `Healthy`/`Degraded` unchanged so existing tests still pass.

### Tests

**Health header test analog:** `QuestBoard.IntegrationTests/Controllers/BoardTimeZoneHealthCheckTests.cs`:
```csharp
public class BoardTimeZoneHealthCheckTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task Health_WithResolvableZone_ReturnsOkAndHealthy()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```
Arrange/Act/Assert comments, xUnit v3 `TestContext.Current.CancellationToken`, class-fixture factory.

**Migrator tests:** `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` uses `net10.0`, FluentAssertions 8.10.0, NSubstitute 5.3.0, `xunit.v3` 3.2.2, `<Using Include="Xunit" />`, ProjectReferences to Domain, Repository and Service. Put a DB-less preflight guard test over all shipped migrations (generate SQL for `QuestBoardContext` with a SQL Server provider and no connection; the spike found 43 migrations, 164 commands, none suppressed). Gated real-SQL atomicity test goes in IntegrationTests (InMemory cannot run migrations); gate with `Assert.SkipUnless`/`Assert.Skip` (present in xunit.v3.assert 3.2.2) on `localhost:1433` reachability; per CLAUDE.md never provision it, skip when absent. If the Migrator project reference is needed from test projects, add a ProjectReference.

---

### `docs/server-setup.md`, `docs/deploy.md`, `docs/releasing.md`, `.planning/PROJECT.md`

**Analogs:** `ING:docs/deploy.md`, `ING:docs/releasing.md`. Existing file sections (from headings): intro and Architecture (line 5), `### Create the deploy script` (72), `### Allow questboard to restart the service` (102), `### Create the systemd service` (110), `### Install the GitHub Actions runner` (134), `## 5. Deploying` (229-255), `## Checking logs` (256; runner logs line 262). Sections 3 (Traefik) and 4 (DNS) are untouched. Do not copy ING's claims that verification is "fully offline" or that every poll emails. Fix `/etc/questboard/.env` to `/etc/questboard/env` in PROJECT.md line 163 (and ROADMAP scope notes if desired).

## Shared Patterns

### Relocatable root and PATH stubs for tests
**Source:** `ING:deploy/bin/ledger-deploy` lines 20-26, `ING:deploy/tests/lib/host-guard.sh`
**Apply to:** all `deploy/bin` and `deploy/lib` code, all `deploy/tests/*`. Every path derives from an env root (default empty); tests run with `systemctl`, `systemd-run`, `gh`, `curl` stubs.

### Never source config files; allow-list loader
**Source:** `ING:deploy/lib/common.sh` `ledger_load_conf` (41-97)
**Apply to:** installer config. Root installer must also never parse `/etc/questboard/env`.

### Single-instance lock
**Source:** `ING:deploy/bin/ledger-deploy` `LOCK_PATH` (`flock -n` under the unit's `RuntimeDirectory`).
**Apply to:** every state-changing subcommand.

### Atomic symlink switch
**Source:** `ING:deploy/lib/deploy.sh` lines 250-276 (`ln -s target tmp && mv -T tmp current`, record previous in state dir).
**Apply to:** install, rollback, setup.

### Comment hygiene
**Source:** `/mnt/Data/repos/quest-board-dnd/CLAUDE.md` "Code Comments"
**Apply to:** everything listed. ING's scripts contain no such IDs, but do not copy ING comments that mention ledger-specific features. Plain-language why-comments only.

### EF only in Repository; startup `Migrate()` stays
**Source:** `.claude/architecture.md`, `ServiceExtensions.ConfigureDatabase`
**Apply to:** Migrator csproj (no EF PackageReference) and Service (no change to `ConfigureDatabase`).

## No Analog Found

| File / feature | Role | Data Flow | Reason |
|---|---|---|---|
| `setup` subcommand (adopt flat `/opt/questboard` into `releases/<ver>/app`, install `gh` from apt, install units and config, record "adopted, not verified") | CLI | file-I/O | ING provisions via `deploy/provision.sh` and `provision.d/*.sh`; closest partial analogs are `ING:deploy/provision.d/10-packages.sh` and `40-services.sh`. Use RESEARCH "Bootstrap sequence" and its `gh` apt install block (fingerprint `7F38BBB59D064DBCB3D84D725612B36462313325`). |
| `deploy/sql-ct/prune-premigration-backups.sh` | utility (runs on the SQL CT) | batch | nothing similar in either repo; four-line `find`/`ls` retention script per RESEARCH |
| Migrator `MigrationRunner` atomic apply and preflight | service | CRUD | repo only has stock `Migrate()`; use RESEARCH Code Examples 1-3 |
| Existing `QuestBoard.IntegrationTests` real-SQL gated test | test | CRUD | all existing integration tests use InMemory; no skip-if-no-database precedent |

## Metadata

**Analog search scope:** `/mnt/Data/repos/quest-board-dnd` (`.github/workflows`, `QuestBoard.Repository`, `QuestBoard.Service`, `QuestBoard.UnitTests`, `QuestBoard.IntegrationTests`, `docs`), `/mnt/Data/repos/ing-dashboard` (`.github`, `build`, `deploy/bin|lib|systemd|tests`)
**Files scanned:** about 35 (ING deploy.sh and docs only partially read; re-read the specific function ranges cited above when implementing)
**Pattern extraction date:** 2026-10-04
**Notes for planner:** RESEARCH.md lines 561-1065 (Pitfalls 4+, Code Examples, Validation Architecture, Open Questions) were not read by this mapper; consult them for the migrator code examples and test plan. RESEARCH says 43 migrations, CONTEXT says 44.
