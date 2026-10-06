---
phase: 89
slug: pull-based-release-deployment
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-10-05
---

# Phase 89 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| GitHub hosted runner → release assets | The release workflow builds, tests, attests and drafts; publishing waits on the `deploy` environment approval | Release zip, `.sha256`, Sigstore bundle (public) |
| Public internet → App CT installer | The poll reads `releases/latest` without a token and downloads the assets | Untrusted bytes until checksum, attestation and main ancestry pass |
| Installer (root) → migrator (app user) | The migrator runs through `systemd-run` as `questboard`; PID 1 hands it `/etc/questboard/env` | Database connection string (secret), never seen by the installer |
| App CT → SQL CT | Migrator `status` / `backup` / `apply` over the app's SQL login | Schema, migration history, copy-only backups |
| App CT → mail relay | Outcome mail via `smtp://192.168.6.13:25` | Closed-vocabulary outcome text, no secrets |
| Operator / orchestrator → GitHub settings and CT root | Merges, tags, environment approval, temporary root SSH during the cutover | Repository settings, production host state |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-89-01 | Tampering | Verify order, attestation, publish re-verify | high | mitigate | Checksum → `gh attestation verify` pinned to repo, signer workflow, tag ref, `--deny-self-hosted-runners` → main ancestry, before staging (`deploy/bin/questboard-deploy`, `deploy/lib/verify.sh`); publish job re-verifies (`release.yml`); real-byte refusal test in CI | closed |
| T-89-02 | Spoofing | Tag off main / branch-built artifact / unprotected tags | high | mitigate | `build/validate-release-tag.sh`; `questboard_commit_on_branch`; tag ruleset + deploy reviewer + tag policy verified by `check-github-settings.sh` (4 PASS) | closed |
| T-89-03 | Tampering | Replay / downgrade via releases/latest | medium | mitigate | Strictly newer strict-semver only; older tags refused with "use rollback"; rollback local and database-checked | closed |
| T-89-04 | Elevation of privilege | Migrator sandbox; `/opt/questboard` ownership | high | mitigate | `systemd-run` as `questboard` with NoNewPrivileges, empty CapabilityBoundingSet, ProtectSystem=strict, ProtectHome; release trees root:root, no group/other write (verified on CT) | closed |
| T-89-05 | Information disclosure | Migrator output, mail body, env handoff, relay check | high | mitigate | Fixed connection-failure text; type-name-only unexpected failures; closed-vocabulary mail; env file only via `EnvironmentFile=`; only three relay keys read during cutover | closed |
| T-89-06 | Tampering | Backup label / SQL parameters | high | mitigate | Label regex before connecting; database, file and set names as SQL parameters | closed |
| T-89-07 | Tampering | Non-transactional migrations, atomic apply, install order | high | mitigate | DB-less preflight; one caller transaction; status+backup before stop, apply before switch; hosted `migrator-sql` 11/11 executed | closed |
| T-89-08 | Tampering | Concurrent invocations | medium | mitigate | `flock -n` on `/run/questboard-deploy/deploy.lock` for setup, poll, install, rollback | closed |
| T-89-09 | Denial of service | Mail budget | medium | mitigate | Single `finish()` mail path; idle/unreachable/remembered polls journal-only; production: 3 idle polls, 1 mail | closed |
| T-89-10 | Tampering / Elevation | Release staging | high | mitigate | Extraction only after verification; absolute/parent/backslash entry checks; symlink refusal; manifest/tag match; `mv -T` | closed |
| T-89-11 | Elevation of privilege | Config loader / `deploy.conf` | high | mitigate | Allow-list loader, per-key regex, owner/mode checks, `printf -v`; `deploy.conf` root 600 (verified) | closed |
| T-89-12 | Elevation of privilege | `release.yml` permissions, expressions, pins | high | mitigate | `permissions: {}`, least job grants, no `${{ }}` in `run:`, SHA pins, zizmor; enforced by `release-workflow-test.sh` | closed |
| T-89-13 | Elevation of privilege | Self-hosted runner path | critical | mitigate | `binary-release.yml` deleted; runs-on scan in CI; runner 21 deregistered, uninstalled on CT; 0 runners; checker PASS | closed |
| T-89-14 | Information disclosure | Tokens reaching gh | medium | mitigate | `env -u` of token variables, private gh/XDG dirs; tested offline and over the network | closed |
| T-89-15 | Tampering | gh apt key in setup | high | mitigate | Pinned primary key fingerprint checked before any apt change; `signed-by`; gh ≥ 2.49.0 | closed |
| T-89-16 | Information disclosure / DoS | Backup files on the SQL CT | medium | mitigate | Files stay in SQL Server's directory (mssql-owned); prune keeps newest 5 of the migrator's pattern; weekly cron; 27 GB free | closed |
| T-89-17 | Spoofing | `/health` version header, health wait | medium | mitigate | `X-QuestBoard-Version` from the build version; loopback-only health URL; header must equal installed version | closed |
| T-89-18 | Repudiation / data loss | Manual restore | low | accept | See accepted risks | closed |
| T-89-19 | Denial of service | `/home/questboard` (Data Protection keys) | medium | mitigate | setup never touches it; runbook limits deletion to the runner directory; verified present after retirement | closed |
| T-89-20 | Denial of service | GitHub API rate limit | low | mitigate | One releases/latest call per poll, one compare per install; 403/404/transport journal-only | closed |
| T-89-21 | Information disclosure | CI SQL password literal | low | accept | See accepted risks | closed |
| T-89-22 | Information disclosure | `/health` version header | low | accept | See accepted risks | closed |
| T-89-23 | Tampering | `package-release.sh` inputs | medium | mitigate | Strict version/commit regexes; `deploy/tests` excluded from the zip | closed |
| T-89-24 | Denial of service | Backup disk on the SQL CT | medium | mitigate | COPY_ONLY + INIT into a unique file; prune + headroom (T-89-16) | closed |
| T-89-25 | Tampering | `mssql` image tag in CI | low | accept | See accepted risks | closed |
| T-89-26 | Tampering | `current` symlink switch | medium | mitigate | Previous recorded first; temp link + `mv -T` | closed |
| T-89-27 | Tampering | State file | low | accept | See accepted risks | closed |
| T-89-28 | Denial of service | Pruning | low | mitigate | Active and previous never pruned; only plain version names considered | closed |
| T-89-29 | Repudiation / Tampering | Overwriting a published release | medium | mitigate | Refuses when published; `--clobber` only on drafts | closed |
| T-89-30 | Denial of service | Rollback into an incompatible schema | high | mitigate | Target migrator status must show nothing unknown/pending; adopted refused; auto switch-back only without committed migrations | closed |
| T-89-31 | Repudiation | Operator actions log | low | mitigate | Attempts file + journal for poll runs; manual runs also log via `logger -t questboard-deploy` (skipped under `JOURNAL_STREAM`); a manual rollback that fails health records `abandoned`/`failed` (commit 6a5c613b, released in v5.4.2, installed on CT 102 and byte-verified) | closed |
| T-89-32 | Tampering | Adopting the wrong version | medium | mitigate | Operator must repeat the detected version; production: detected 5.3.3 matched the site footer before adoption | closed |
| T-89-33 | Denial of service | Half-finished cutover | medium | mitigate | App stopped first; resumable moves; `current` created last; health before success | closed |
| T-89-34 | Repudiation / DoS | Timer with placeholder mail config | low | mitigate | Timer enabled only once a real recipient is set | closed |
| T-89-35 | Spoofing | First install trust-on-first-use | high | mitigate | Workstation `verify-published-release.sh` (v5.4.0 `00a5ef36…`, v5.4.1 `9f3f52d1…`); on the CT `sha256sum -c` against those exact hashes printed `OK` before unzip/setup; afterwards the 9 installed installer files, the app DLL and the manifest on CT 102 were byte-identical to a freshly verified v5.4.1 zip | closed |
| T-89-36 | Information disclosure | Docs content | low | mitigate | Placeholders only; output documented as secret-free | closed |
| T-89-37 | Spoofing | Operator trusting an unverified first zip | medium | mitigate | Runbook requires workstation verification and CT hash comparison | closed |
| T-89-38 | Tampering | Agent acting on GitHub | medium | accept | See accepted risks | closed |
| T-89-39 | Denial of service | Sandbox blocks systemd-run | medium | mitigate | Status first; production first install succeeded through the sandboxed unit | closed |
| T-89-40 | Elevation of privilege | Leftover sudoers rule | high | mitigate | `/etc/sudoers.d/questboard` and `deploy.sh` removed; `visudo -c` OK | closed |
| T-89-SC | Tampering | Actions / CI lint supply chain | high | mitigate | Actions pinned by SHA; lint images by digest; new jobs `contents: read`; original `build` job left tag-pinned by decision | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-89-01 | T-89-18 | Restore from a pre-migration backup stays a documented manual SQL step by decision; the runbook states that writes after the backup began are discarded | Operator (planning decision) | 2026-10-05 |
| AR-89-02 | T-89-21 | Literal SA password guards an ephemeral CI-only SQL container with no data; documented in a YAML comment | Operator (planning decision) | 2026-10-05 |
| AR-89-03 | T-89-22 | The site footer already shows the version publicly; the header adds no information | Operator (planning decision) | 2026-10-05 |
| AR-89-04 | T-89-25 | CI-only `mssql/server:2022-latest` test dependency in jobs without secrets; digest pinning would add manual bumps for no reachable asset | Operator (planning decision) | 2026-10-05 |
| AR-89-05 | T-89-27 | State directory is root-only (700) inside the unit's write allow-list; an explicit install overrides a stale memory | Operator (planning decision) | 2026-10-05 |
| AR-89-06 | T-89-13 (plan 10 interim) | Runner stayed registered between merge and the first healthy pull-based install so a working deploy path always existed; superseded — runner retired in plan 12 | Operator (planning decision) | 2026-10-05 |
| AR-89-07 | T-89-38 | The plan reserved push, merge, tag and settings changes for the operator. In execution the orchestrator pushed (including a re-authoring of 59 unpushed commits to the noreply address), opened and merged PRs #155–#157, created the `deploy` environment and tag ruleset, pushed tags v5.4.0/v5.4.1, deregistered runner 21 and cancelled a stale queued run — each on the operator's explicit chat instruction. The `deploy` environment approval stayed with the operator. Compensating controls: settings re-verified read-only (4 PASS), `validate-release-tag.sh` on each tag, and the installer verifies attestation and main ancestry regardless of who pushed | Operator (chat, 2026-10-05) | 2026-10-05 |

---

## Operational Events

| Event | Detail | Status |
|-------|--------|--------|
| Temporary root SSH for the orchestrator on CT 102 | Granted by the operator for the cutover and runner retirement: existing `claude` key, `from="192.168.1.140"`, same-day `expiry-time`, marker `claude-temp-root-89`. `/etc/questboard/env` was only grepped for three non-secret relay keys. A first grant on the wrong CT was reverted by the operator. Grant removed afterwards; root login shown refused; unprivileged `claude` login unchanged | closed |
| Decision change: verification outage | An unreachable verification service is now a quiet retry (no mail, not remembered) instead of "counts as failed"; adds no install path (outage branch deletes the download and stages nothing) | recorded |
| Decision change: abandoned marker | `rollback` records the release it left as `abandoned` so the poll does not reinstall it | recorded |
| Leftover logs | `/root/setup-adopt.log`, `/root/setup-final.log` on CT 102 (apt and installer output, no secrets) | closed — removed by the operator on 2026-10-06 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-05 | 41 | 38 | 3 (T-89-35 blocking; T-89-31, T-89-38 non-blocking) | gsd-security-auditor |
| 2026-10-05 | 41 | 40 | 1 (T-89-31 non-blocking, fix in progress) | orchestrator — T-89-35 closed with CT hash-check record and byte-identity check; T-89-38 accepted by operator |
| 2026-10-06 | 41 | 41 | 0 | orchestrator — T-89-31 closed: review-fix round released as v5.4.2, installed by the poll, installer updated via setup, CT files byte-identical to the verified zip |

### Residual notes (not gaps in declared mitigations)

- `MigrationRunner` database-creation path prints the raw `SqlException` message if `Create()` fails; journal only, never mail.
- The release build job holds `id-token`/`attestations: write` while running the test suite (dependency code next to signing ability); splitting attestation into its own job is a possible later hardening.
- `prevent_self_review=false` on the `deploy` environment: with a single maintainer the tagger also approves.
- Org-level runners could not be listed with the available token (`admin:org` missing); the operator confirmed on 2026-10-06 that the retired runner was a repository runner, so the repository count of 0 covers it.
- The plan-05/06 bypass grep is a one-time acceptance check, not a committed test.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-05
