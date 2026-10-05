# Operating the pull-based deploy

This is the guide for the person sitting on the App CT when a release is installing, has
failed, or needs to be put back. How to cut and publish a release is in
[releasing.md](releasing.md); how the CTs themselves are built is in
[server-setup.md](server-setup.md).

## How a release reaches the server

The server fetches releases; nothing pushes to it.

1. A release is built, tested and attested on GitHub's hosted runners, then published after an
   approval (see [releasing.md](releasing.md)).
2. A systemd timer on the App CT runs `questboard-deploy poll` every few minutes. It asks GitHub
   for the latest published release.
3. If that release is newer than the one running and has not already been tried and rejected, the
   installer downloads it, verifies it, installs it into its own directory, runs any database
   migrations, restarts the app and checks that the new version is the one answering.
4. Whatever happens, an install attempt ends with exactly one outcome and one mail.

GitHub has no runner, no credential and no inbound connection to the App CT. Every connection is
outbound from the CT. The installer reads public endpoints without a token, and the tool that
verifies the signature is started with every GitHub token variable removed.

## Layout on the App CT

| Path | What it is |
|---|---|
| `/opt/questboard/releases/<X.Y.Z>/` | One directory per installed release, owned by root and read-only to the app |
| `/opt/questboard/releases/<X.Y.Z>/app/` | The published web app |
| `/opt/questboard/releases/<X.Y.Z>/migrator/` | That release's own database migrator |
| `/opt/questboard/releases/<X.Y.Z>/deploy/` | The installer files shipped with that release |
| `/opt/questboard/releases/<X.Y.Z>/release-manifest.json` | Version, commit and whether the release reports its version in a response header |
| `/opt/questboard/current` | Symlink to the active release, switched atomically |
| `/usr/local/sbin/questboard-deploy` | The installer |
| `/usr/local/lib/questboard-deploy/` | The installer's libraries |
| `/etc/questboard/deploy.conf` | Installer settings, owned by root, mode 600 |
| `/etc/questboard/env` | The app's own environment file, unchanged by any of this |
| `/var/lib/questboard-deploy/state/attempts` | One line per attempt: tag, outcome, UTC time |
| `/var/lib/questboard-deploy/state/previous` | The release that was active before the current one |
| `/var/lib/questboard-deploy/downloads/` | Scratch space for a download in progress |
| `/run/questboard-deploy/deploy.lock` | The lock that stops two installer runs overlapping |
| `/etc/systemd/system/questboard-deploy-poll.service` | The sandboxed oneshot that runs `poll` |
| `/etc/systemd/system/questboard-deploy-poll.timer` | Starts it: 2 minutes after boot, then every 5 minutes, with up to 30 seconds of random delay |
| `/etc/systemd/system/questboard.service.d/10-release-layout.conf` | Points the existing `questboard.service` at `/opt/questboard/current/app` |

The installer keeps `QUESTBOARD_KEEP_RELEASES` releases on disk (5 by default) and never deletes
the active release or the one before it.

## The poll

The timer starts `questboard-deploy poll` two minutes after boot and every five minutes after
that. Each run makes one unauthenticated request to GitHub's latest-release endpoint, about 12
requests an hour. GitHub allows 60 unauthenticated requests an hour per public address, and that
allowance is shared with anything else on the network that polls GitHub the same way, so the
cadence should not be shortened.

A poll that finds nothing to do says so in the journal and exits successfully:

- nothing newer than the active release;
- GitHub unreachable, or no release published yet;
- the verification services unreachable, or GitHub's main-branch check giving no usable answer,
  while checking a new release (the next poll retries it);
- the newest release is one the installer already tried and rejected (see "Outcomes").

None of these send mail. A poll also installs nothing that is not strictly newer than what is
running, so a release that GitHub lists as latest but that is older is never installed.

## What an install does

Both orders start the same way: download the three release files, verify them, and unpack the
release into a staging directory that is moved into place only when complete.

With migrations pending:

1. ask the release's own migrator for the database status;
2. take a copy-only backup of the database, named `questboard-premigration-<tag>-<UTC>.bak`, on
   the SQL CT;
3. stop the app;
4. apply every pending migration in a single transaction, all or nothing;
5. switch `current` to the new release;
6. start the app and wait for the health check.

With nothing pending: status, stop the app, switch `current`, start, health check. No backup is
taken.

The backup is skipped only when the status says outright that the database does not exist yet,
which is a fresh host with nothing to protect. A status that leaves that field out or reports it
as anything other than true or false ends the attempt as `failed` (the database could not be
read) before the app is stopped, rather than migrating without a backup.

The health check polls `http://127.0.0.1:5000/health` until it gets HTTP 200 with a body of
`Healthy` or `Degraded` and an `X-QuestBoard-Version` header equal to the new version, so an old
process that is still answering is never mistaken for the new release. The wait lasts
`QUESTBOARD_HEALTH_TIMEOUT_SECONDS`.

After a healthy install the installer prunes old releases and, if the installer files shipped in
the new release differ from the installed copies, adds a line to the outcome mail saying so (see
"Commands", `setup`).

### Verification

A release is installed only if every check passes. There is no flag, variable or fallback that
skips one.

1. **Checksum.** The `.sha256` file must be a single line naming the zip, and the zip must match.
2. **Build provenance.** `gh attestation verify` is run on the zip with the downloaded
   `.sigstore.json` bundle, pinned to this repository, to the signing workflow
   `.github/workflows/release.yml`, to the source ref `refs/tags/<tag>`, and with
   `--deny-self-hosted-runners` so only a GitHub-hosted build is accepted.
3. **Attested commit.** The commit the attestation names must be identical to, or behind, `main`,
   asked of GitHub's public compare endpoint. Only a definite answer is a verdict: a commit that is
   ahead of or diverged from `main`, or one GitHub does not know, is refused. No answer at all, or
   an answer that says nothing about the commit (the unauthenticated rate limit, HTTP 403 or 429,
   or a 5xx), is treated like an unreachable verification service: nothing changes, nothing is
   mailed or remembered, and the next poll retries.
4. **Content.** The unpacked release must have a manifest whose version equals the tag, must
   contain the app, the migrator and the installer files, must contain no symlinks and no path
   that escapes the directory, and the disk must have room for it.

Verification needs the Sigstore and GitHub trust services to be reachable from the CT, because
`gh` fetches the current trusted roots over the network. When `gh` fails, the installer asks each
of those services (and the GitHub API) for an answer, without any credential. If at least one gives
no answer at all, no verdict on the release exists: nothing is staged, stopped or remembered, no
mail is sent, and the next poll retries the same tag. A manual
`questboard-deploy install` in that situation exits non-zero with `verification services
unreachable; nothing changed, try again later`. This never lets an unverified release through: the
release is simply not installed until verification has run. If every service answers and `gh`
still fails, the release is refused as described below.

The `gh` check is cut off after 120 seconds (then killed 10 seconds later if it ignores the
stop), so a stalled network call can never hold the installer's lock and silence every later
poll. A run that was cut off is judged exactly like any other failed run: services that give no
answer mean a quiet retry, services that answer mean the release is refused.

## Outcomes

| Situation | What the installer does | Mail | Remembered by the poll |
|---|---|---|---|
| Healthy after the install | Release stays active; old releases pruned | `installed` | no (an earlier memory of this tag is cleared) |
| Checksum, attestation or main-ancestry check fails; a release file is missing; the content is invalid | Nothing on disk or in the service changes; the staged release is removed | `refused` | yes |
| The database holds migrations this release does not know | Install refused before anything changes | `refused` | yes |
| A pending migration cannot run inside a transaction | Install refused before anything changes | `refused` | yes |
| The pre-migration backup fails, the database cannot be reached, there is not enough disk, or the release cannot be put in place (a hardening, clean-up or move step failed) | Abort before the app is stopped; the running release is untouched and nothing half-installed is left behind | `failed` | yes |
| Applying the migrations fails | The transaction rolls back and the database is unchanged; the installer reads the database again to confirm it, then the previous release is started again | `failed, rolled back` | yes |
| The migrator reports a failed apply, but the database turns out to hold the migrations (for example a commit whose acknowledgement was lost) | Treated as an applied install: the previous release is not started on the migrated schema, the new release is activated and checked like any other | `installed` if healthy, otherwise `halted - migrations applied` | no if installed, otherwise yes |
| No migrations, and the new release is not healthy | `current` is switched back, the previous release is restarted and confirmed healthy | `rolled back` | yes |
| Migrations committed, and the new release is not healthy | The new release is left active, systemd keeps retrying it, nothing is restored automatically | `halted - migrations applied`, naming the backup | yes |
| Nothing newer than the active release | Nothing | none, journal only | no |
| GitHub unreachable, or no release published | Nothing; the next poll retries | none, journal only | no |
| The verification services (Sigstore, GitHub) cannot be reached, or the main-branch check gets no usable answer (no response, HTTP 403, 429 or 5xx) | Nothing is staged or stopped; the next poll retries | none, journal only | no |
| A remembered tag turns up again | Skipped, with one journal line | none, journal only | already remembered |
| `questboard-deploy rollback` by hand | Switches to the chosen release | none, terminal and journal only | the release rolled back from is (as `abandoned`) |
| `questboard-deploy rollback` by hand, and the chosen release does not become healthy | `current` has already moved and the app was restarted on it; the command exits with an error | none, terminal and journal only | the release rolled back from is (as `abandoned`), and the chosen release is recorded as `failed` |

A tag that was refused, failed, rolled back, halted or abandoned by a manual rollback is
remembered: later polls skip it without mailing, until a newer tag is published or you run
`questboard-deploy install <tag>` by hand. Each bad release therefore costs exactly one mail.

A release that fails its health check with no earlier release to go back to (the very first
install on a fresh server) is reported as `failed` and left in place.

An outcome other than `installed` makes the poll run exit with an error, so
`systemctl status questboard-deploy-poll.service` shows `failed` once for that release.

## Notification mail

Each install outcome sends one mail through the SMTP relay named in `deploy.conf`, which should be
the relay the app itself uses. The server's own local mail system is not used. The subject reads
`[questboard-deploy] <result> v<version>`. The body holds only the version, the result with a
short fixed reason, the start and finish times in UTC, and, for a halted install, the name of the
backup file. It carries no paths, host names, connection strings or key material. A mail that
cannot be sent is logged and never changes what the installer does.

Polls never send mail on their own. The relay shares a daily sending budget with the rest of the
board, so a quiet poll must stay quiet.

When the shipped installer files differ from the installed ones, the mail also says: `Installer
update available: run setup from release <version>`.

## Commands

All of these run as root on the App CT.

The installer takes its settings from `/etc/questboard/deploy.conf` and never from the
environment. The two variables the offline tests use to run it against a temporary directory,
`QUESTBOARD_DEPLOY_ROOT` and `QUESTBOARD_DEPLOY_CONF`, are refused (the run stops with `not a
test tree owned by this user`) unless they name a directory that carries the test marker and is
owned by the same user as the installer process, which a directory made by anyone else never is
for a root run. If you run the installer through `sudo`, keep sudo's default environment
handling: do not add `env_keep`, `SETENV` or `sudo -E` for it. To be certain, start it with a
clean environment:

```bash
sudo env -i PATH=/usr/local/sbin:/usr/sbin:/usr/bin:/sbin:/bin questboard-deploy install vX.Y.Z
```

### `questboard-deploy poll`

What the timer runs. It is also safe to run by hand, or to start through the unit so the sandbox
is exercised:

```bash
systemctl start questboard-deploy-poll.service
```

### `questboard-deploy install vX.Y.Z`

Installs or redeploys one release by hand. The behaviour depends on the tag:

- **Newer than the active release:** a full install, exactly as the poll would do it.
- **The active release:** restart the app and run the health check. Nothing is downloaded.
- **Older than the active release:** refused. Use `rollback`.
- **A tag the poll is skipping:** installed anyway. This is the override for a remembered tag, and
  a successful install clears the memory.

### `questboard-deploy rollback X.Y.Z`

Switches to a release that is still on disk, without the `v`. It works only when that release's
own migrator reports that the database holds nothing the release does not know and nothing is
pending. It is refused when:

- the release is not on disk, or is already active;
- the database holds migrations that release does not know;
- the database is behind that release (install it instead);
- the release is the adopted one, which has no migrator to ask (see "Moving an existing push-based
  install over").

It sends no mail. The tag you roll back to is recorded as `rolled_back_manual`, and the release
you rolled back from is recorded as `abandoned` and remembered, so the poll does not install it
again: it logs `skipping vX.Y.Z: abandoned earlier; run questboard-deploy install vX.Y.Z to try it
again` and moves on. A release published later than the abandoned one is not held back, so the
next poll installs it as usual. To go forward to the abandoned release again, run
`questboard-deploy install vX.Y.Z` by hand; that records it as installed and clears the memory. A
rollback that is refused before anything changes records nothing. A rollback that switched
`current` and restarted the app but then saw the target fail its health check is recorded all the
same: the target as `failed`, and the release rolled back from as `abandoned`, so the poll does not
install it again behind your back. Fix or replace the target and install it by hand.

### `questboard-deploy verify --artifact FILE --bundle FILE --tag vX.Y.Z`

Runs the attestation and main-ancestry checks on files you already have, installs nothing, and
prints the attested commit on success.

### `questboard-deploy setup [--from DIR] [--confirm-adopt-version X.Y.Z]`

The one-time bootstrap, and also how an installer update is applied. It installs the GitHub CLI
from GitHub's apt repository (only after checking the repository key against a pinned
fingerprint), the installer, its libraries, the poll units and the `questboard.service` drop-in;
creates `deploy.conf` from the example when it does not exist; makes `/opt/questboard` root-owned;
and enables the poll timer once `deploy.conf` has a real recipient. It can be run again at any
time. Exit codes: 0 complete, 2 waiting for the running version to be confirmed (nothing was
changed), 3 waiting for `deploy.conf` to be edited, 1 error.

The installer never rewrites itself. When an outcome mail says an installer update is available,
apply it from the release it names:

```bash
/opt/questboard/releases/<version>/deploy/bin/questboard-deploy setup
```

## Why rollback sometimes stops

Database migrations only go forward. The installer runs a migration in one transaction, so a
failed apply leaves the database exactly as it was and the previous release can simply start
again: that is `failed, rolled back`. A non-zero exit from the migrator is not taken on trust,
though: the installer reads the database status again (up to three times, two seconds apart, if
the first reads get no answer) and starts the previous release only when everything that was
pending still is. If fewer migrations are pending, the schema has moved on, and the install
continues as an applied one. If the database cannot be read at all, nothing shows that the schema
changed, and the previous release is started as before; the journal says so. The migrator's own
error line names the exception type of a failure that was not a SQL error, and says the outcome is
unknown when the failure came while committing.

Once a migration has committed, the previous release's code may no longer match the schema, so
starting it again could do damage. If the new release then fails its health check, the installer
does not go back. It reports `halted - migrations applied`, leaves the new release active, and
`questboard.service` keeps restarting it, because the fault may be transient. The mail names the
backup taken before the migration.

What to do when you get a halted mail:

1. Read `journalctl -u questboard.service -n 100` and find out why the new release is not healthy.
   If it recovers on its own, you are done.
2. Otherwise, fix forward: publish a corrected release. The poll installs it because it is newer.
3. If you need the old version back now, restore the backup (next section) and roll back.

Manual `rollback` stops for the same reason: it refuses when the database holds migrations the
target release does not know, because that is a database the target cannot safely run against.

## Restoring a pre-migration backup

There is no automated restore. A restore is a deliberate, manual step because it discards data.

A restore discards every write made after the backup began. The app keeps serving between the
backup and the moment it is stopped for the migration, so signups and other changes made in that
window are lost too. Prefer fixing forward once the service is back.

1. Stop the app and keep it stopped. A halted install leaves systemd retrying the new release, so
   this matters:

   ```bash
   systemctl stop questboard
   ```

2. On the SQL CT, restore the backup over the live database. The file sits in the SQL Server
   default backup directory, `/var/opt/mssql/data` unless you changed it, and is named in the
   halted mail:

   ```sql
   RESTORE DATABASE [QuestBoard]
     FROM DISK = N'/var/opt/mssql/data/questboard-premigration-<tag>-<UTC>.bak'
     WITH REPLACE;
   ```

   Run it with `sqlcmd` or any SQL client logged in as an administrator. Use the database name
   from the connection string in `/etc/questboard/env` if it is not `QuestBoard`.

3. Put the previous release back.

   - If the previous release was installed by the pull-based installer, run:

     ```bash
     questboard-deploy rollback <previous version>
     ```

     It starts the app and checks health. The release you rolled back from stays remembered as
     rejected after a halted install, so the poll leaves it alone.

   - If the previous release is the adopted one, `rollback` refuses it. Repoint `current` with an
     atomic rename and start the app:

     ```bash
     ln -s /opt/questboard/releases/<previous version> /opt/questboard/current.tmp
     mv -T /opt/questboard/current.tmp /opt/questboard/current
     systemctl start questboard
     ```

4. Confirm the site, then publish a fixed release.

## Backups on the SQL CT

SQL Server writes each pre-migration backup to its own disk, so trimming them is a job on the SQL
CT, not the App CT. A backup is a full copy of the database. Before a release that carries a
migration, check that the backup directory has room:

```bash
df -h /var/opt/mssql/data
```

Every release ships the pruning script. Copy it from an installed release on the App CT to the SQL
CT and install it:

```bash
install -m 0755 prune-premigration-backups.sh /usr/local/sbin/prune-premigration-backups.sh
```

Run it as root on the SQL CT, by hand after a release that migrated, or weekly from cron:

```bash
# prune-premigration-backups.sh [DIR] [KEEP]   (DIR default /var/opt/mssql/data, KEEP default 5)
/usr/local/sbin/prune-premigration-backups.sh /var/opt/mssql/data 5
```

```
# /etc/cron.d/questboard-prune-backups
17 3 * * 0  root  /usr/local/sbin/prune-premigration-backups.sh /var/opt/mssql/data 5
```

It keeps the newest `KEEP` files named `questboard-premigration-*.bak`, prints `removed <name>` for
each one it deletes, and touches nothing else in the directory.

## Running the migrator by hand

Each release carries its own migrator. To read the database status or take a backup yourself, run
it the way the installer does: through `systemd-run`, which reads `/etc/questboard/env` itself and
starts the process as the app user. Never `source` the env file in a shell; it holds the database
password.

```bash
systemd-run --pipe --wait --collect --uid=questboard --gid=questboard \
  --working-directory=/opt/questboard/current/migrator \
  --property=EnvironmentFile=/etc/questboard/env \
  /usr/bin/dotnet /opt/questboard/current/migrator/QuestBoard.Migrator.dll status
```

`status` prints one JSON line listing applied and pending migrations. A backup works the same way
with `backup --label NAME` in place of `status`; the label may use letters, digits, `.`, `_` and
`-`, up to 64 characters, and the command prints the backup file name. Neither prints a connection
string.

| Exit code | Meaning |
|---|---|
| 0 | Success |
| 1 | Error or bad usage |
| 2 | The database is ahead of this release (it holds migrations the release does not know) |
| 3 | A pending migration contains an operation that cannot run inside a transaction |
| 4 | Cannot connect to the database |
| 5 | The backup failed |
| 6 | Applying failed and was rolled back |

## Configuration

`/etc/questboard/deploy.conf` is read line by line as `KEY=VALUE`; it is never run as shell code.
It must be owned by root and not writable by group or others, and a key that is not in this table
is an error. Environment variables with the same names are ignored.

| Key | Default | Meaning |
|---|---|---|
| `QUESTBOARD_GITHUB_REPO` | none, required | `owner/name` the releases are fetched and verified against; must be the name the repository had when the release was built |
| `QUESTBOARD_SIGNER_WORKFLOW` | `.github/workflows/release.yml` | The workflow path an attestation must name |
| `QUESTBOARD_NOTIFY_EMAIL` | none | Recipient of outcome mail. The poll timer is only enabled once this is a real address |
| `QUESTBOARD_MAIL_FROM` | none | Sender address of outcome mail |
| `QUESTBOARD_SMTP_HOST` | none | Relay host; use the relay the app itself sends through |
| `QUESTBOARD_SMTP_PORT` | `25` | Relay port |
| `QUESTBOARD_KEEP_RELEASES` | `5` | Releases kept on disk, at least 2 |
| `QUESTBOARD_HEALTH_TIMEOUT_SECONDS` | `120` | How long a new release has to become healthy, at least 10 |
| `QUESTBOARD_HEALTH_URL` | `http://127.0.0.1:5000/health` | Health endpoint; must be plain HTTP on this machine |

## Moving an existing push-based install over

This is for a server that is still running the old push-based deploy: a flat `/opt/questboard`, a
GitHub Actions runner on the box, and a deploy script in the `questboard` home directory. The
first pull-based install adopts the running version, so nothing changes for users at that moment.

The installer is delivered inside a release, but the CT has no installer, no `gh` and no `deploy/`
directory until `setup` has run once. So the first release is fetched and checked by hand:

1. Merge to `main`, tag a release, approve the `deploy` environment and let it publish, as in
   [releasing.md](releasing.md). The server does not install it yet.
2. On a workstation, verify the published release:

   ```bash
   build/verify-published-release.sh vX.Y.Z
   ```

   It checks the release exactly as the server will, confirms a tampered copy would be refused,
   and ends with a line `sha256 questboard-vX.Y.Z.zip <hex>`. Keep that hash.
3. On the App CT, fetch the zip and compare its hash to the one from step 2:

   ```bash
   cd "$(mktemp -d)"
   curl -fLO https://github.com/theunschut/quest-board-dnd/releases/download/vX.Y.Z/questboard-vX.Y.Z.zip
   sha256sum questboard-vX.Y.Z.zip
   ```

   Stop if the hashes differ.
4. Unzip to a temporary directory and run `setup` from there as root:

   ```bash
   mkdir release && unzip -q questboard-vX.Y.Z.zip -d release
   release/deploy/bin/questboard-deploy setup
   ```

   With the old flat layout in place it reads the running version from
   `/opt/questboard/QuestBoard.Service.dll`, prints it, and stops with exit code 2 without
   changing anything.
5. Compare the printed version with the site footer, then confirm it:

   ```bash
   release/deploy/bin/questboard-deploy setup --confirm-adopt-version X.Y.Z
   ```

   This installs `gh` and the installer files, creates `/etc/questboard/deploy.conf`, stops the
   app, moves the flat install into `/opt/questboard/releases/X.Y.Z/app`, makes `/opt/questboard`
   root-owned, points `current` at it, starts the app and waits for it to be healthy. That
   release is recorded as adopted, not verified: it carries the same trust it had before. Setup
   ends with exit code 3 and asks you to edit the configuration.
6. Edit `/etc/questboard/deploy.conf`: the recipient, sender, relay host and port. Then run setup
   again, which enables the poll timer:

   ```bash
   release/deploy/bin/questboard-deploy setup
   ```

7. Trigger the first install through the unit, so the sandboxed path is exercised rather than a
   root shell:

   ```bash
   systemctl start questboard-deploy-poll.service
   journalctl -u questboard-deploy-poll.service -n 50 --no-pager
   ```

   The adopted release is now the automatic rollback target for that install. Expect an
   `installed` mail.

On a fresh CT with nothing installed yet, `setup` has no running version to adopt and does not
ask for one. Run it as in step 4, edit `deploy.conf` as in step 6 and run `setup` again, then run
`questboard-deploy install vX.Y.Z` for the first release.

### Retiring the push-based deploy

Do this once the first pull-based install has reported healthy. While the old runner stays
registered, a later workflow could still target it.

1. Deregister the runner `theunschut-dnd-quest-board.QuestBoard`: GitHub, Settings, Actions,
   Runners, then remove it. From a workstation with the owner's `gh` login, list it and remove it
   by id:

   ```bash
   gh api repos/theunschut/quest-board-dnd/actions/runners --jq '.runners[] | {id, name, status}'
   gh api --method DELETE repos/theunschut/quest-board-dnd/actions/runners/<id>
   ```

2. On the App CT, stop and uninstall the runner's service:

   ```bash
   cd /home/questboard/actions-runner
   ./svc.sh stop
   ./svc.sh uninstall
   ```

3. Delete the runner directory:

   ```bash
   rm -rf /home/questboard/actions-runner
   ```

4. Remove the old restart permission and deploy script:

   ```bash
   rm /etc/sudoers.d/questboard /home/questboard/deploy.sh
   ```

Then verify both sides:

- **GitHub:** the repository has zero registered runners. `build/check-github-settings.sh` fails
  while any is registered, or check directly:

  ```bash
  gh api repos/theunschut/quest-board-dnd/actions/runners --jq .total_count
  ```

  The answer must be `0`.
- **The CT:** no runner unit remains.

  ```bash
  systemctl list-units --all 'actions.runner*'
  ```

  It must list 0 units, and `/etc/sudoers.d/questboard` must be gone.

**Warning: delete only the runner's own directory, never `/home/questboard`.** The app has no key
storage configured, so its ASP.NET Data Protection keys live in the `questboard` user's home
directory (by default under `.aspnet/DataProtection-Keys`). Deleting the home directory discards
them: every user is signed out and every outstanding password-reset and email-change link stops
working.

## Troubleshooting

Useful commands:

```bash
journalctl -u questboard-deploy-poll.service -n 100 --no-pager   # what the last runs did
journalctl -u questboard-deploy-poll.service -f                  # follow a run
journalctl -t questboard-deploy -n 100 --no-pager                 # runs started by hand (install, rollback, setup)
systemctl list-timers questboard-deploy-poll.timer                # when it runs next
systemctl status questboard-deploy-poll.service                   # shows failed after a rejected release
cat /var/lib/questboard-deploy/state/attempts                     # what was tried and how it ended
ls -l /opt/questboard/current                                     # the active release
```

A poll run leaves its lines in the unit's journal. A run started from a root shell (`install`,
`rollback`, `setup`, `verify`) writes the same lines to the terminal and also to the journal under
the tag `questboard-deploy`, so a manual rollback or a refused manual install is not lost when the
terminal closes. Under a systemd unit the installer adds no second copy.

**The installer refuses a release that verifies on a workstation.** The journal line
`attestation verification failed` carries the reason. The usual causes are: `gh` is missing or
older than 2.49.0 (run `setup` again); or `QUESTBOARD_GITHUB_REPO` or `QUESTBOARD_SIGNER_WORKFLOW`
does not match what the release was built under. A refusal is remembered, so once the cause is
fixed run `questboard-deploy install vX.Y.Z` to try that tag again.

**The journal says `verification services unreachable`.** The CT could not reach the Sigstore or
GitHub trust services, so the release could not be checked. This is not a refusal and is not
remembered: nothing changed, and the next poll retries by itself. Nothing needs to be run by hand
unless you are installing manually; then run `questboard-deploy install vX.Y.Z` again once the
CT has network access. If it persists, check DNS and outbound HTTPS from the CT to
`tuf-repo-cdn.sigstore.dev`, `tuf-repo.github.com` and `api.github.com`.

**The journal says `the main-branch check got no usable answer from GitHub`.** GitHub's compare
endpoint did not answer, or answered with the rate limit (HTTP 403 or 429) or a server error. This
is also not a refusal and is not remembered; the next poll retries. The unauthenticated allowance
is shared with anything else on the CT's public address that polls GitHub, so if it keeps
happening look for another poller on that address. A release that really is not on `main` is
refused with a mail instead.

**A bus-connection error in the journal** (`Failed to connect to bus`) means the installer could
not start the migrator through systemd from inside the sandboxed unit. The install stops before
touching the running app and is reported as failed because the database could not be reached.
Running `questboard-deploy install vX.Y.Z` from a root shell works around it for that release;
report it, because the sandbox settings need fixing.

**The new release is healthy but the install reports a health timeout.** A slow first start, for
example after a large migration, can outlast the wait. Raise `QUESTBOARD_HEALTH_TIMEOUT_SECONDS`
in `deploy.conf` and install the tag again by hand.

**The poll keeps skipping a release.** It was refused, failed, rolled back, halted or abandoned
by a manual rollback earlier and is remembered. Find out why from the mail and the journal, fix
the cause, then run `questboard-deploy install vX.Y.Z`.

**An outcome mail never arrived.** The send is logged but never fails the install. Check
`journalctl -u questboard-deploy-poll.service` for `sending outcome mail failed`, and check the
recipient and relay in `deploy.conf`.

**Two runs at once.** A second invocation while one is running exits with `another
questboard-deploy invocation is already running`. Wait for the first to finish.
