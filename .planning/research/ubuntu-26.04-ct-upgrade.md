# Ubuntu 26.04 LTS upgrade assessment: Proxmox host, CT 102 (QuestBoard), CT 103 (MS-Sql)

Research only. Nothing was changed on any host. Written 2026-10-06; every source below was accessed
2026-10-06. Items marked **UNCONFIRMED** could not be verified from an authoritative source.

Known inputs: host `pve` (192.168.6.6) is Proxmox VE 9.2 on Debian 13. The operator's `pveversion -v`
reports proxmox-ve 9.2.0, pve-manager 9.2.5, running kernel 7.0.12-1-pve and **pve-container 6.1.11**.
It is a single node, and it also runs Traefik, Postfix, foundryvtt, Ledger and cabinet, plus jellyfin
and qbittorrent (stopped).

## Summary

| # | Question | Verdict | Deciding fact |
|---|----------|---------|---------------|
| 1 | Can this host (PVE 9.2.5, **pve-container 6.1.11**) run Ubuntu 26.04 CTs? | **Supported** | `'26.04' => 1` was added to the known-version list in pve-container **6.0.12** (15 Sep 2025, commit `9197af5`). 6.1.11 (8 Jul 2026) is newer [S1][S2][S3] |
| 2 | Official 26.04 LXC template | **Available** | `ubuntu-26.04-standard_26.04-1_amd64.tar.zst`, uploaded 29 Apr 2026, listed for PVE 8 and PVE 9 [S5][S6] |
| 3 | `systemd-run` with the migrator's sandbox properties inside a 26.04 CT | **Expected to work, UNCONFIRMED for 26.04** | Proxmox documents that systemd needs `nesting=1` to isolate services. The same unit already works on CT 102 under 24.04. Test it on a clone first [S9][S15] |
| 4 | App CT 24.04 → 26.04 in place (`do-release-upgrade`) | **Supported** | LTS-to-LTS upgrades opened on 29 Sep 2026, after 26.04.1 shipped on 27 Aug 2026 [S18][S19] |
| 5 | .NET 10 (`aspnetcore-runtime-10.0`, `dotnet-runtime-10.0`) on 26.04 | **Supported** | Microsoft lists .NET 10 for 26.04 from Ubuntu's own feed. The `resolute` main archive has 10.0.12-0ubuntu1~26.04.1 [S26][S22] |
| 6 | `gh` from `cli.github.com/packages` on 26.04 | **Works, not formally listed** | The repo is `stable main` and is not tied to a release codename. `gh` is a self-contained Go binary [S27] |
| 7 | The deploy shell tooling under Rust coreutils and sudo-rs | **Works on paper; needs a smoke test** | Every flag the deploy scripts use is in the uutils docs. `cp`, `mv` and `rm` stay GNU in 26.04. `flock` and `logger` come from util-linux and are unaffected [S16][S21][S23] |
| 8 | SQL Server 2022 on 24.04 or 26.04 | **Not supported** | SQL Server 2022 supports only Ubuntu 20.04 and 22.04 [S28] |
| 9 | SQL Server 2025 on 24.04 / 26.04 | **24.04 supported (CU1+) / 26.04 not supported** | SQL Server 2025 supports Ubuntu 22.04, and 24.04 from CU1. 26.04 is not listed (pages updated 5 Oct 2026) [S28][S29] |
| 10 | Host minor updates within 9.x | **Routine, low difficulty** | `apt update && apt full-upgrade`, never `apt upgrade`. A new kernel needs a reboot, which stops every CT [S13] |
| 11 | Host major upgrade 8 → 9 | **Already done** | The host runs Debian 13 / PVE 9.2. PVE 8 reached end of life in Aug 2026 [S11] |
| — | **Recommendation** | Host: update now. App CT: wait, rehearse, upgrade from about Nov 2026. SQL CT: do not go to 26.04; move to a new 24.04 CT with SQL Server 2025 before mid-2027 | See section 6 |

## 1. Upgrading the Proxmox host itself

### 1.1 Where the host is

- Check the version with `pveversion -v`. The deciding line for 26.04 guests is
  `pveversion -v | grep pve-container`.
- Check the repositories in the GUI (Node → Updates → Repositories), or with
  `ls /etc/apt/sources.list.d/` and `cat /etc/apt/sources.list.d/*.sources`. PVE 9 uses deb822
  `.sources` files [S12]:
  - Enterprise: `pve-enterprise.sources`, URI `https://enterprise.proxmox.com/debian/pve`,
    component `pve-enterprise`. It is enabled by default. Without a subscription `apt update` fails
    with 401.
  - No-subscription: `proxmox.sources`, URI `http://download.proxmox.com/debian/pve`, suite
    `trixie`, component `pve-no-subscription`.
  - `ceph.sources` also has an enterprise and a no-subscription variant. A leftover enterprise Ceph
    entry also gives 401 errors.
- **Verdict for 26.04 guests: this host is new enough.** pve-container 6.1.11 is later than 6.0.12,
  which added 26.04 ("Ubuntu: record support for future 25.10 and 26.04 LTS release"). The same
  release stopped treating an unknown but recent Ubuntu version as a hard error; it now only logs a
  task warning [S1][S2]. Current `Ubuntu.pm` lists `'26.04' => 1, # resolute LTS` [S3].
  - Minimum versions: pve-container **6.0.12** on PVE 9 (trixie), or **5.3.3** on PVE 8
    (bookworm) [S1][S4].
  - Anything older fails at start with `unsupported Ubuntu version '26.04'` from
    `lxc-pve-prestart-hook`. That is the same failure 24.04 hit in April 2024, which was fixed by
    updating pve-container on the host [S7]. The workaround `ostype: unmanaged` drops PVE's
    network/hostname setup and is not needed here.
  - 26.04.1 also changed the release-upgrade prompt to LTS-only (`Prompt=lts` for resolute) [S17].

### 1.2 Bringing 9.x fully up to date

1. Make sure exactly one PVE repository is active: enterprise with a subscription, otherwise
   no-subscription. Do the same for Ceph.
2. Run `apt update && apt full-upgrade`.
   - Do **not** use `apt upgrade`. Proxmox warns it can leave a "partially upgraded or broken package
     state" [S13].
3. Reboot only if a new kernel was installed. Compare `uname -r` with `proxmox-boot-tool kernel list`.
   - The pve-container update itself needs no reboot. It takes effect at the next CT start.
4. Re-check with `pveversion -v`. The current pve-container is **6.1.14** (27 Aug 2026). It brings:
   - 6.1.12: fix for bug #7380, where systemd-version detection resolved `/proc/PID/root` wrongly and
     `pve8to9 --full` warned "Failed to get cgroup support status" [S10].
   - 6.1.13: an API privilege fix.
   - 6.1.14: config line-feed injection hardening [S1].

   None of these is required for 26.04, but all are worth having.
5. Fetch the template with `pveam update`, then `pveam available --section system | grep ubuntu-26`.

### 1.3 PVE 9.x notes that matter for 26.04 guests

- **Nesting.**
  - The `pct` manual says `nesting` defaults to 0 and "is also required by systemd to isolate
    services" [S9].
  - PVE 9.1 documented this requirement and lifted the /proc and /sys restrictions when nesting is
    on [S8].
  - Debian 13 CTs without nesting are reported to boot to a blank console [S38, forum].
  - Ubuntu 26.04 ships systemd 259 [S16][S22]. Treat `features: nesting=1` as mandatory.
  - Check with `pct config 102 | grep -E '^(unprivileged|features|ostype)'`.
- **Unprivileged vs privileged.**
  - Nesting on an *unprivileged* CT is the supported, low-risk setup.
  - On a *privileged* CT, nesting exposes the host's /proc and /sys to a root that is real root on
    the host, so Proxmox advises against it. Without nesting, systemd sandboxing may fail.
  - New CTs default to unprivileged since pve-container 6.0.6 [S1].
  - Creating a privileged CT on PVE 9 needs the `Sys.Modify` privilege [S8].
  - If CT 102 or 103 turns out to be privileged, rebuild it as unprivileged rather than turning on
    nesting.
- **cgroup v1 is gone on both sides.**
  - PVE 9 dropped cgroup v1 and hybrid hierarchies [S14].
  - systemd 258+ removed cgroup v1 support [S16].
  - PVE 9.2 warns about raw `lxc.cgroup.*` (v1) lines in a CT config [S8]. Check with
    `grep -l 'lxc.cgroup\.' /etc/pve/lxc/*.conf`.
- **systemd-run sandbox properties** (`ProtectSystem=strict`, `ProtectHome`, `PrivateTmp`,
  `ProtectKernel*`, `ProtectControlGroups`, `RestrictNamespaces`, `NoNewPrivileges`,
  `CapabilityBoundingSet=`). These need systemd to set up a mount namespace inside the CT.
  - The symptom when blocked is `status=226/NAMESPACE`.
  - It already works on CT 102 under systemd 255. **UNCONFIRMED** for 259: no 26.04-specific report
    either way was found.
  - Proof test on the rehearsal clone (expect exit 0):
    `systemd-run --quiet --pipe --wait --collect -p ProtectSystem=strict -p ProtectHome=yes -p PrivateTmp=yes -p ProtectKernelTunables=yes -p ProtectKernelModules=yes -p ProtectControlGroups=yes -p RestrictNamespaces=yes -p NoNewPrivileges=yes -p CapabilityBoundingSet= /bin/true; echo $?`
- **Kernel.** CTs run on the host kernel (7.0.12), which is the same line as 26.04's own
  kernel 7.0 [S16]. No mismatch.
- **Debian 13 apt uses `sqv`.** From 2026-02-01 it rejects SHA1-bound repository keys [S25]. This
  only matters if the *host* carries third-party repositories.

### 1.4 Single-node specifics

- A host reboot stops **every** CT at once: Traefik (and so all public sites), Postfix (outbound
  mail), QuestBoard, MS-Sql, foundryvtt, Ledger and cabinet.
  - Schedule a window.
  - Check `onboot`/`startup` so that MS-Sql starts before QuestBoard and Traefik starts early, for
    example `pct set 103 --startup order=1`.
- **Back up every guest to a different disk or to PBS first**, for example
  `vzdump <ids> --storage <other> --mode snapshot --compress zstd`.
  - Use `--mode stop` for CT 103 so SQL Server's files are consistent.
  - `pct snapshot` is only a fast rollback on snapshot-capable storage (LVM-thin, ZFS, Ceph), not on
    a plain directory store.
- Have console or out-of-band access (monitor and keyboard, or IPMI) before rebooting into a new
  kernel. If `vmbr0` does not come up, SSH and the web UI are gone.

### 1.5 Major upgrade 8 → 9 (already done; for the record)

- The official procedure is the "Upgrade from 8 to 9" wiki [S14]:
  - be on pve-manager 8.4.1 or later;
  - run the `pve8to9 --full` checklist before, during and after;
  - switch the Debian and PVE repositories to `trixie`.
- Typical pitfalls listed there:
  - cgroup v1 removal;
  - uninstalling the `systemd-boot` meta-package;
  - the change in journald audit logging;
  - LVM autoactivation;
  - NVIDIA vGPU driver minimums.
- Support dates: PVE 8 end of life Aug 2026 (FAQ: Debian 12 EOL 2026-07; endoflife.date gives
  2026-08-31). PVE 9 end of life: "tba" [S11].

### 1.6 Difficulty and order of operations

- **Difficulty.** A minor 9.x update is low: about 15 minutes plus one reboot. An 8 → 9 upgrade is
  medium-high (a Debian major upgrade with a checklist), but it is behind you.
- **Order:**
  1. Back up all guests.
  2. Update the host and reboot.
  3. App CT: rehearse on a clone, then upgrade.
  4. SQL CT: move to 24.04 + SQL Server 2025 later, as a separate change.

  Never do the host and a CT in the same window.

## 2. App CT (CT 102, Ubuntu 24.04.4, ASP.NET Core 10)

### 2.1 In place, or a fresh CT?

**In place on a snapshot, after a rehearsal on a clone, is the better route here.** Both routes are
supported. A fresh CT would have to carry over state that is easy to miss:

- `/home/questboard/.aspnet/DataProtection-Keys`. Losing it signs everyone out and breaks open
  password-reset and email-change links.
- `/etc/questboard/env` and `/etc/questboard/deploy.conf`.
- The base `questboard.service` unit. `questboard-deploy setup` installs only the drop-in and the
  poll units.
- `/var/lib/questboard-deploy/state`.
- The `questboard` user's UID.
- The IP 192.168.6.12, which Traefik routes to and which Postfix may allow.

The fresh route only wins if the in-place rehearsal fails.

Path facts:

- 24.04 → 26.04 upgrades opened on 29 Sep 2026 [S19]. They were held back after 26.04.1 (27 Aug 2026)
  for "regressions in a recent version of rust-coreutils" [S18].
- Third-party repositories (here `github-cli.list`) are **disabled during a release upgrade** and
  must be re-enabled afterwards [S20].
- LTS upgrades only ever go to the next LTS; you cannot skip one [S20].

### 2.2 .NET 10

- Microsoft's Ubuntu page lists 26.04 with .NET 10.0, 9.0 and 8.0 supported. 10.0 comes from Ubuntu's
  built-in feed; the Microsoft feed has none [S26].
- `aspnetcore-runtime-10.0` and `dotnet-runtime-10.0` are in `resolute` main at
  10.0.12-0ubuntu1~26.04.1 [S22]. `/usr/bin/dotnet` keeps the same path.
- `noble-updates` already carries **10.0.12-0ubuntu1~24.04.1** [S22]. If the CT really is still on
  10.0.9, it is three servicing releases behind. Run `apt update && apt full-upgrade` on 24.04 first;
  this is also a prerequisite for `do-release-upgrade`.

### 2.3 `gh`

- The setup writes a one-line `.list` with `signed-by=/usr/share/keyrings/...`. APT 3.x still reads
  that format; removal is "not before 2029" [S25].
- `apt modernize-sources` converts it to deb822 if wanted.
- Ubuntu's own archive `gh` is old (2.46.0) [S22], so keep GitHub's repository.
- After the upgrade, re-enable the disabled entry and run `apt update`. The installer's pinned-key
  check in `deploy/lib/setup.sh` is unaffected.

### 2.4 Behaviour changes that touch the shell tooling

- **Coreutils.**
  - 26.04 installs `coreutils-from-uutils` (uutils 0.8.0-0ubuntu3) by default [S21][S22].
  - The metapackage lists uutils first, so upgraded systems switch too.
  - **Exceptions: `cp`, `mv` and `rm` stay GNU** because of unresolved TOCTOU races [S16][S21].
  - Flags `deploy/` uses, checked against the uutils docs [S23]:

    | Flag | Notes |
    |------|-------|
    | `timeout --kill-after` | documented |
    | `install -d -m` / `install -m` | documented |
    | `stat -c '%u %a'` | documented; only the *default* stat output labels differ [S37] |
    | `sha256sum --check --strict --status` | documented |
    | `df --output=avail -B1` | documented |
    | `sort -V`, `sort -z -k1,1nr` | documented; the latter runs on the SQL CT |
    | `readlink -f`, `mktemp -u`/`-d`, `env -u`, `head -c`, `date -u -R` | documented |
    | `mv -T` | GNU |

    The uutils docs describe the current upstream (0.13), not 0.8.0. **UNCONFIRMED** that every edge
    case matches GNU on 0.8.0.
  - `flock -n` and `logger` come from util-linux 2.41 and are not affected.
  - Fallback: `apt install coreutils-from-gnu`, noting that `build-essential` hard-depends on the
    uutils provider [S21].
- **sudo-rs** is the default `sudo`; the old one is `sudo.ws` [S16].
  - sudo-rs does not support `sudo -E`, always resets the environment, and ignores `logfile` and
    `mail_badpass` [S24].
  - `docs/deploy.md` already forbids `sudo -E`, `env_keep` and `SETENV`, so this matches the design.
  - Re-check any `/etc/sudoers.d` rules with `sudo -l`.
- **Python 3.14** (from 3.12). The deploy scripts only use `json` and `sys` from the standard
  library. Low risk.
- **GnuPG 2.4.8.** `gpg --show-keys --with-colons` is unchanged in format [S22].
- **APT 3.x.**
  - `apt-key` is removed [S16].
  - `/etc/apt/trusted.gpg` is **no longer a trusted keyring** ("Unset Dir::Etc::trusted", apt 2.9.24)
    [S25].
  - Ubuntu 26.04 still verifies with `gpgv`, not Sequoia [S16], so the 2026-02-01 SHA1 cutoff in
    `sqv` does not apply on 26.04.
- **systemd 259.** 26.04 is the last release with SysV init-script compatibility [S16]. Chrony is
  the default time daemon only on new installs. Time sync does not apply inside a CT anyway.

## 3. SQL CT (CT 103, Ubuntu 22.04, SQL Server 2022)

### 3.1 Support matrix [S28][S29]

| | 22.04 | 24.04 | 26.04 |
|-|-------|-------|-------|
| SQL Server 2022 (16.x) | Supported (CU10+) | **Not supported** | **Not supported** |
| SQL Server 2025 (17.x) | Supported | Supported from CU1 | **Not supported** (not listed) |

- Running in LXC is not a Microsoft-listed platform, which is already true today. Microsoft lists
  only bare OS and Docker.
- Lifecycle dates:
  - Ubuntu 22.04 standard support ends mid-2027 (endoflife.date: 2027-06-01) [S35].
  - SQL Server 2022 mainstream support ends 2028-01-11; SQL Server 2025's ends 2031-01-06 [S35].
  - Microsoft supports a distribution only until the earlier of the distribution's or SQL Server's
    end of life [S28].
  - Ubuntu Pro (free for up to 5 machines [S20]) would extend 22.04 security updates. **UNCONFIRMED**
    whether Microsoft counts ESM as "supported".

### 3.2 Upgrade path

- **26.04 is not possible (supported) yet.**
- The recommended route is a **new unprivileged 24.04 CT with SQL Server 2025 (CU1+), followed by
  backup and restore**, which is Microsoft's own advice for moving distributions [S28].
- Not recommended: an in-place 22.04 → 24.04 `do-release-upgrade` under SQL Server. The repository
  path is per-OS (`ubuntu/22.04/...` vs `ubuntu/24.04/...`), and Microsoft documents no in-place OS
  upgrade.
- An in-place 2022 → 2025 *package* upgrade on 22.04 is supported: switch the repository to
  `mssql-server-2025`, then `apt-get install mssql-server` [S30]. It still leaves 22.04's
  end-of-life to deal with.

### 3.3 Moving 2022 → 2025: points to watch

- **Compatibility level.** A restored or upgraded database keeps its level (160) when that level is
  100 or higher. 2025 adds 170 [S32]. Keep 160 first and raise it later as a separate, tested change.
- **One way only.** "No SQL Server backup can be restored to an earlier version" [S33]. Rollback means
  going back to the untouched CT 103, and anything written after cutover is lost.
- **Edition and licence.**
  - Check the edition with `SELECT SERVERPROPERTY('Edition'), SERVERPROPERTY('ProductVersion'),
    SERVERPROPERTY('ProductUpdateLevel');`, or read the first lines of `/var/opt/mssql/log/errorlog`.
  - Developer editions (in 2025: Enterprise Developer and Standard Developer) are "not licensed for
    production" [S31].
  - Express is free for production. In 2025 it gains a 50 GB database limit (up from 10 GB) and the
    former Advanced Services features, but still has no SQL Agent [S31]. 70 MB fits easily.
  - Choose Express in `mssql-conf setup` on the new CT.
- **Logins** are in `master`, not in the user database.
  - Recreate the app's SQL login on the new instance.
  - Re-map orphaned users with `ALTER USER [u] WITH LOGIN = [u]`, or recreate the login with its
    original SID.
- Re-install `prune-premigration-backups.sh` and its cron entry on the new CT.

### 3.4 apt key hygiene

- CT 103's Microsoft key is in legacy `/etc/apt/trusted.gpg`. This works with a warning on 22.04 and
  24.04, and is **ignored** by APT 3.x (25.04+ / 26.04) [S25].
- On the new CT, follow Microsoft's keyring form:
  `curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg`
  plus `signed-by` [S29].
- `microsoft.asc` (…EB3E94ADBE1229CF) carries SHA1 signatures. Newer repositories use
  `microsoft-2025.asc` (…EE4D7792F748182B) [S34].
  - **UNCONFIRMED** which key signs `ubuntu/24.04/mssql-server-2025`.
  - If apt reports `NO_PUBKEY EE4D7792F748182B`, add `microsoft-2025.asc` as well.

## 4. Sequence and rollback

**0. Host.**
- Back up all guests with vzdump to another disk or PBS.
- `apt full-upgrade` to pve-container 6.1.14 and the latest kernel.
- Reboot in a window. Confirm every CT is back and `/health` answers.

**1. App CT (CT 102).**
- *Before:*
  - Take a native SQL backup (see SQL below).
  - `systemctl disable --now questboard-deploy-poll.timer`, so no release installs mid-upgrade.
  - `vzdump 102`, plus `pct snapshot 102 pre-2604` if the storage supports it.
  - Copy `/etc/questboard/`, `/etc/systemd/system/questboard.service*` and
    `/home/questboard/.aspnet/` off the CT.
- *Rehearsal:*
  - `pct clone 102 <new> --full`.
  - Before first start, set a spare IP (`pct set <new> --net0 ...`).
  - Use `pct mount` to remove the `multi-user.target.wants/questboard.service` and
    `timers.target.wants/questboard-deploy-poll.timer` links, so the clone neither touches the
    production database nor sends mail.
  - Upgrade the clone and run the checks below, apart from the app itself.
- *Upgrade:*
  - Run `apt update && apt full-upgrade` on 24.04 and reboot the CT.
  - Run `do-release-upgrade` from `pct enter 102` or inside `tmux`, not over a plain SSH session.
    Keep locally modified config files.
  - `pct reboot 102`.
- *Verify:*
  - `lsb_release -d`
  - `dotnet --list-runtimes` should show 10.0.x
  - `systemctl is-active questboard`
  - `curl -fsS http://127.0.0.1:5000/health`
  - re-enable `github-cli.list`, then `apt update` with no key errors
  - `/opt/questboard/current/deploy/bin/questboard-deploy setup`
  - `systemctl start questboard-deploy-poll.service`, then `journalctl -u questboard-deploy-poll -n 50`. This exercises the sandbox.
  - the `systemd-run` probe from section 1.3
  - `sudo -l` for any sudoers rules
  - finally re-enable the poll timer
- *Rollback:* `pct rollback 102 pre-2604`, or `pct restore 102 <vzdump> --force` after
  `pct stop 102`. The database is on CT 103 and is unaffected.

**2. SQL CT (CT 103).**
- *Before anything:*
  - `BACKUP DATABASE … TO DISK='/var/opt/mssql/data/<name>.bak' WITH COPY_ONLY, CHECKSUM, INIT`
  - `RESTORE VERIFYONLY … WITH CHECKSUM`
  - copy the file off the box (`pct pull 103 …` on the host, then to another disk or PBS) and
    record its `sha256sum`
  - `vzdump 103 --mode stop`
- *Build:*
  - Create a new unprivileged 24.04 CT with nesting=1.
  - Install SQL Server 2025 Express (keyring + `signed-by`).
  - Restore `WITH MOVE`, recreate the login and re-map users.
  - Leave the compatibility level at 160.
- *Cutover:*
  - Stop the app.
  - Take a final COPY_ONLY backup on CT 103 and restore it on the new CT.
  - Stop CT 103 and give the new CT its IP, or change the connection string in
    `/etc/questboard/env`.
  - Start the app and check `/health`.
- *Rollback:* stop the new CT, start CT 103 untouched, and restore the app's connection string.
  Writes made after cutover are lost.

## 5. Risks, ranked

| Rank | Risk | Likelihood / impact | Mitigation |
|------|------|---------------------|------------|
| 1 | SQL Server 2022 → 2025 is one-way; orphaned logins; post-cutover writes lost on rollback | Medium / High | Off-box verified backup, rehearsed restore, keep CT 103 untouched until stable |
| 2 | State loss when rebuilding the App CT fresh (Data Protection keys, env, service unit) | Medium (fresh route only) / High | Upgrade in place; copy `/home/questboard/.aspnet` and `/etc/questboard` first |
| 3 | Host reboot takes down every CT; on a single node a bad kernel or network leaves no remote access | Low / High | Maintenance window, startup order, console access, keep the previous kernel bootable |
| 4 | Sandboxed `systemd-run` migrator fails in a 26.04 CT (`226/NAMESPACE`), which blocks releases that migrate | Low-Med / Medium | `nesting=1`, unprivileged CT, probe on the clone |
| 5 | uutils edge-case differences in `deploy/` scripts | Low / Medium | Rehearsal run of setup and poll; `coreutils-from-gnu` fallback |
| 6 | `do-release-upgrade` disables `cli.github.com`, so `gh`/apt break until re-enabled | High / Low | Re-enable after the upgrade; `apt update` check |
| 7 | APT 3.x ignores `/etc/apt/trusted.gpg` | Only if a CT with a legacy key reaches 25.04+ / Low | Move keys to `signed-by` keyrings |
| 8 | SQL Server in LXC is outside Microsoft's support matrix | Existing / Low | Accepted today; unchanged |
| 9 | sudo-rs differences (`-E`, ignored sudoers options) | Low / Low | Already avoided by design; `sudo -l` |
| 10 | pve-container 6.1.11 systemd-version detection bug #7380 (warning) | Low / Low | Update to 6.1.12+ |

## 6. Recommendation

- **Proxmox host: do it now.** Version 9.2.5 with pve-container 6.1.11 already supports 26.04
  guests. Still run the routine `apt full-upgrade` (to 6.1.14 and the newest kernel) in a short
  window, after backing up every guest. No major upgrade is pending.
- **App CT: wait until about November 2026, then upgrade in place.** There is no pressure: 24.04 is
  supported until 2029 and .NET 10 is supported on it. Let the LTS upgrade rollout (opened
  29 Sep 2026) settle for a few weeks.
  - In the meantime, bring 24.04 current, including .NET 10.0.12.
  - Confirm `nesting=1` and that the CT is unprivileged.
  - Rehearse on a clone.
  - Go ahead once the rehearsal passes the section 4 checks.
- **SQL CT: do not target 26.04.** Neither SQL Server 2022 nor 2025 supports it. Plan a move to a
  **new 24.04 CT with SQL Server 2025 Express (CU1+)** in Q1 2027, before 22.04's standard support
  ends (about May/June 2027). Revisit 26.04 only once Microsoft lists it for SQL Server 2025.
  - Now: confirm the edition (a Developer edition in production is a licensing problem whatever
    happens with the upgrade) and move the Microsoft key out of `/etc/apt/trusted.gpg`.

## Sources (all accessed 2026-10-06)

- [S1] pve-container debian/changelog (trixie, HEAD): https://git.proxmox.com/?p=pve-container.git;a=blob_plain;f=debian/changelog;hb=HEAD
- [S2] Commit 9197af5 "setup: ubuntu: record support for future 25.10 and 26.04 LTS release": https://git.proxmox.com/?p=pve-container.git;a=commit;h=9197af5b29f84cc683663d801d9ef8f5f89f62d8
- [S3] `Ubuntu.pm` (HEAD): https://git.proxmox.com/?p=pve-container.git;a=blob;f=src/PVE/LXC/Setup/Ubuntu.pm;hb=HEAD
- [S4] pve-container changelog (stable-bookworm): https://git.proxmox.com/?p=pve-container.git;a=blob_plain;f=debian/changelog;hb=refs/heads/stable-bookworm
- [S5] Proxmox forum, 26.04 template thread (staff post 29 Apr 2026): https://forum.proxmox.com/threads/ubuntu-26-04-likely-template-release-date.183082/
- [S6] Template index: http://download.proxmox.com/images/aplinfo-pve-9.dat and http://download.proxmox.com/images/system/
- [S7] Proxmox forum, the same failure for 24.04 ("unsupported Ubuntu version '24.04'", fixed in 5.1.10): https://forum.proxmox.com/goto/post?id=657702
- [S8] Proxmox VE Roadmap (9.0 / 9.1 / 9.2 notes): https://pve.proxmox.com/wiki/Roadmap
- [S9] `pct` manual: https://pve.proxmox.com/pve-docs/pct.1.html
- [S10] Proxmox bug 7380: https://bugzilla.proxmox.com/show_bug.cgi?id=7380
- [S11] Proxmox VE FAQ (support table): https://pve.proxmox.com/wiki/FAQ ; https://endoflife.date/proxmox-ve
- [S12] Package Repositories: https://pve.proxmox.com/wiki/Package_Repositories
- [S13] System Software Updates: https://pve.proxmox.com/wiki/System_Software_Updates
- [S14] Upgrade from 8 to 9: https://pve.proxmox.com/wiki/Upgrade_from_8_to_9
- [S15] pve-devel docs patch "document that systemd requires LXC nesting": https://lists.proxmox.com/pipermail/pve-devel/2025-October/076163.html
- [S16] Ubuntu 26.04 release notes, summary for LTS users: https://documentation.ubuntu.com/release-notes/26.04/summary-for-lts-users/
- [S17] Ubuntu 26.04.1 release notes: https://documentation.ubuntu.com/release-notes/26.04/1/
- [S18] "Ubuntu 26.04.1 LTS released" (27 Aug 2026): https://discourse.ubuntu.com/t/ubuntu-26-04-1-lts-released/86808
- [S19] ubuntu-announce, upgrades from 24.04 enabled (29 Sep 2026): https://lists.ubuntu.com/archives/ubuntu-announce/2026-September/000328.html
- [S20] Ubuntu Server docs, upgrade your release: https://documentation.ubuntu.com/server/how-to/software/upgrade-your-release/
- [S21] "An update on rust-coreutils" (22 Apr 2026): https://discourse.ubuntu.com/t/an-update-on-rust-coreutils/80773
- [S22] packages.ubuntu.com (resolute: coreutils, rust-coreutils, aspnetcore-runtime-10.0, dotnet-runtime-10.0, systemd, gnupg, python3, sudo-rs, gh; noble-updates: aspnetcore-runtime-10.0): https://packages.ubuntu.com/resolute/aspnetcore-runtime-10.0 (and sibling pages)
- [S23] uutils coreutils docs: https://uutils.org/coreutils/docs/utils/ (timeout, install, stat, sha256sum, date, sort, df, readlink, mktemp, env, head)
- [S24] sudo-rs README: https://github.com/trifectatechfoundation/sudo-rs
- [S25] APT debian/changelog: https://salsa.debian.org/apt-team/apt/-/raw/main/debian/changelog
- [S26] Microsoft Learn, Install .NET on Ubuntu: https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install
- [S27] GitHub CLI Linux install docs: https://github.com/cli/cli/blob/trunk/docs/install_linux.md
- [S28] Microsoft Learn, Release information for SQL Server on Linux (updated 2026-10-05): https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-release-notes
- [S29] Microsoft Learn, Ubuntu: Install SQL Server on Linux: https://learn.microsoft.com/en-us/sql/linux/install-upgrade/quickstart-install-ubuntu?view=sql-server-ver17
- [S30] Microsoft Learn, Configure repositories for SQL Server 2025 on Linux: https://learn.microsoft.com/en-us/sql/linux/install-upgrade/change-repo-2025
- [S31] Microsoft Learn, Editions of SQL Server 2025: https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2025
- [S32] Microsoft Learn, ALTER DATABASE compatibility level: https://learn.microsoft.com/en-us/sql/t-sql/statements/alter-database-transact-sql-compatibility-level?view=sql-server-ver17
- [S33] Microsoft Learn, RESTORE statements: https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-transact-sql?view=sql-server-ver17
- [S34] packages.microsoft.com keys README: https://packages.microsoft.com/keys/README
- [S35] endoflife.date (Ubuntu, SQL Server): https://endoflife.date/ubuntu , https://endoflife.date/mssqlserver
- [S37] (secondary) Ubuntu 26.04 Rust coreutils guide: https://computingforgeeks.com/ubuntu-2604-rust-coreutils-guide/
- [S38] (secondary, forum) Debian 13 LXC needs nesting: https://forum.proxmox.com/goto/post?id=817222
