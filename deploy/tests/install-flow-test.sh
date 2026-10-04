#!/usr/bin/env bash
# Proves the installer end to end: every row of the outcome table, the install
# order around migrations, remember-and-skip, one mail per outcome and silence
# on idle polls. Needs no root, network, systemd or database: each case runs
# the real dispatcher against its own temporary root, with recording stand-ins
# for curl, gh, systemctl and systemd-run first on PATH.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
DISPATCHER="${REPO_ROOT}/deploy/bin/questboard-deploy"

BASE="$(mktemp -d)"
trap 'rm -rf "$BASE"' EXIT
# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$BASE"

STUBS="${BASE}/stubs"
mkdir -p "$STUBS"

# --- stand-ins ------------------------------------------------------------

# curl: serves the latest-release, compare, download and health URLs from
# environment variables and files, and captures uploaded mail.
cat > "${STUBS}/curl" <<'EOF'
#!/bin/sh
out=""; hdr=""; upload=""; prev=""; url=""
for arg in "$@"; do
  case "$prev" in
    --output) out="$arg" ;;
    --dump-header) hdr="$arg" ;;
    --upload-file) upload="$arg" ;;
  esac
  prev="$arg"
  url="$arg"
done

if [ -n "$upload" ]; then
  n=$(ls "$STUB_MAIL_DIR" | wc -l)
  cp "$upload" "${STUB_MAIL_DIR}/mail-$((n + 1)).eml"
  exit 0
fi

printf '%s\n' "$url" >> "$STUB_CURL_LOG"
code=200
case "$url" in
  */releases/latest)
    [ -n "${STUB_LATEST_EXIT:-}" ] && exit "$STUB_LATEST_EXIT"
    if [ -z "${STUB_LATEST_TAG:-}" ]; then
      code=404
    else
      printf '{"tag_name":"%s"}' "$STUB_LATEST_TAG" > "$out"
    fi
    ;;
  */compare/*)
    [ -n "${STUB_COMPARE_EXIT:-}" ] && exit "$STUB_COMPARE_EXIT"
    printf '{"status":"%s"}' "${STUB_COMPARE_STATUS:-behind}" > "$out"
    ;;
  */releases/download/*)
    [ -n "${STUB_DOWNLOAD_EXIT:-}" ] && exit "$STUB_DOWNLOAD_EXIT"
    tag=$(printf '%s' "$url" | sed 's|.*/releases/download/\([^/]*\)/.*|\1|')
    asset="${url##*/}"
    if [ -f "${STUB_DL_DIR}/${tag}/${asset}" ]; then
      cp "${STUB_DL_DIR}/${tag}/${asset}" "$out"
    else
      code=404
    fi
    ;;
  */health)
    manifest="${QUESTBOARD_DEPLOY_ROOT}/opt/questboard/current/release-manifest.json"
    version=""
    [ -f "$manifest" ] && version=$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' "$manifest")
    if [ -z "$version" ] || [ "$version" = "${STUB_UNHEALTHY_VERSION:-none}" ]; then
      code=503
      printf 'Unhealthy' > "$out"
    else
      printf 'Healthy' > "$out"
    fi
    if [ -n "$hdr" ]; then
      {
        printf 'HTTP/1.1 %s\r\n' "$code"
        printf 'X-QuestBoard-Version: %s\r\n\r\n' "$version"
      } > "$hdr"
    fi
    ;;
  *)
    code=404
    ;;
esac
printf '%s' "$code"
exit 0
EOF

# gh: prints STUB_GH_JSON, or fails when it is empty.
cat > "${STUBS}/gh" <<'EOF'
#!/bin/sh
printf 'gh %s\n' "$*" >> "$STUB_CURL_LOG"
if [ -z "${STUB_GH_JSON:-}" ]; then
  echo "verification failed" >&2
  exit 1
fi
printf '%s\n' "$STUB_GH_JSON"
EOF

# systemctl: records the call and which release the current link names.
cat > "${STUBS}/systemctl" <<'EOF'
#!/bin/sh
cur=$(basename "$(readlink -f "${QUESTBOARD_DEPLOY_ROOT}/opt/questboard/current" 2>/dev/null)" 2>/dev/null)
printf 'systemctl %s current=%s\n' "$*" "$cur" >> "$STUB_CALL_LOG"
exit "${STUB_SYSTEMCTL_EXIT:-0}"
EOF

# systemd-run: stands in for the migrator. Records subcommand, release and
# which release the current link names, and answers from STUB_*_EXIT/JSON.
cat > "${STUBS}/systemd-run" <<'EOF'
#!/bin/sh
dll=""; sub=""; label=""; prev=""; seen=0
for arg in "$@"; do
  if [ "$seen" = "1" ]; then sub="$arg"; seen=2; fi
  case "$arg" in *QuestBoard.Migrator.dll) dll="$arg"; seen=1 ;; esac
  if [ "$prev" = "--label" ]; then label="$arg"; fi
  prev="$arg"
done
version=$(basename "$(dirname "$(dirname "$dll")")")
cur=$(basename "$(readlink -f "${QUESTBOARD_DEPLOY_ROOT}/opt/questboard/current" 2>/dev/null)" 2>/dev/null)
line="migrator ${sub} ${version}"
[ -n "$label" ] && line="${line} --label ${label}"
printf '%s current=%s\n' "$line" "$cur" >> "$STUB_CALL_LOG"
case "$sub" in
  status)
    printf '%s\n' "${STUB_STATUS_JSON:-}"
    exit "${STUB_STATUS_EXIT:-0}" ;;
  backup)
    printf '{"backupName":"questboard-premigration-%s-20261004T120000Z.bak"}\n' "$label"
    exit "${STUB_BACKUP_EXIT:-0}" ;;
  apply)
    printf '{"applied":["Pending"]}\n'
    exit "${STUB_APPLY_EXIT:-0}" ;;
esac
exit 1
EOF
chmod +x "${STUBS}"/*
export PATH="${STUBS}:${PATH}"

# --- helpers --------------------------------------------------------------

FAILURES=0

check() {
  local description="$1" expected="$2" actual="$3"
  if [ "$actual" = "$expected" ]; then
    printf 'PASS: %s\n' "$description"
  else
    printf 'FAIL: %s (expected [%s], got [%s])\n' "$description" "$expected" "$actual"
    FAILURES=$((FAILURES + 1))
  fi
}

# Prints yes when the file contains the fixed text, otherwise no.
has_text() {
  if grep -qF -- "$2" "$1" 2>/dev/null; then printf 'yes'; else printf 'no'; fi
}

STATUS_CLEAN='{"databaseExists":true,"applied":["A"],"pending":[],"unknown":[],"nonTransactional":[],"canBackup":true}'
STATUS_PENDING='{"databaseExists":true,"applied":["A"],"pending":["B"],"unknown":[],"nonTransactional":[],"canBackup":true}'
GH_OK='[{"verificationResult":{"signature":{"certificate":{"sourceRepositoryDigest":"0123456789abcdef0123456789abcdef01234567"}}}}]'

# Builds a synthetic release archive and its checksum and placeholder bundle
# into DIR. Ships the repository's real deploy directory, minus the tests.
build_release() {
  local version="$1" dir="$2"
  local tree="${BASE}/tree-${version}"
  rm -rf "$tree"
  mkdir -p "${tree}/app" "${tree}/migrator" "${tree}/deploy" "$dir"
  printf 'service' > "${tree}/app/QuestBoard.Service.dll"
  printf 'migrator' > "${tree}/migrator/QuestBoard.Migrator.dll"
  ( cd "${REPO_ROOT}/deploy" && tar -cf - --exclude='./tests' . ) | ( cd "${tree}/deploy" && tar -xf - )
  printf '{"version":"%s","commit":"0123456789abcdef0123456789abcdef01234567","healthVersionHeader":true}\n' \
    "$version" > "${tree}/release-manifest.json"
  local name="questboard-v${version}.zip"
  rm -f "${dir}/${name}"
  ( cd "$tree" && zip -q -r -X "${dir}/${name}" . )
  ( cd "$dir" && sha256sum "$name" > "${name}.sha256" )
  printf 'bundle' > "${dir}/${name}.sigstore.json"
}

build_release 1.3.0 "${BASE}/dl/v1.3.0"
build_release 1.3.1 "${BASE}/dl/v1.3.1"

# Writes an already-installed release directory (not activated).
install_release() {
  local version="$1" adopted="${2:-}"
  local dir="${ROOT}/opt/questboard/releases/${version}"
  mkdir -p "${dir}/app"
  printf 'service' > "${dir}/app/QuestBoard.Service.dll"
  if [ "$adopted" = "adopted" ]; then
    printf '{"version":"%s","adopted":true,"healthVersionHeader":false}\n' "$version" > "${dir}/release-manifest.json"
  else
    mkdir -p "${dir}/migrator"
    printf 'migrator' > "${dir}/migrator/QuestBoard.Migrator.dll"
    printf '{"version":"%s","healthVersionHeader":true}\n' "$version" > "${dir}/release-manifest.json"
  fi
}

activate_installed() {
  ln -sfn "${ROOT}/opt/questboard/releases/$1" "${ROOT}/opt/questboard/current"
}

# Copies the shipped installer files to where an installed system keeps them,
# so the installed copies match what a release ships.
install_deploy_copies() {
  local src="${1:-${REPO_ROOT}/deploy}" f name
  mkdir -p "${ROOT}/usr/local/sbin" "${ROOT}/usr/local/lib/questboard-deploy" \
    "${ROOT}/etc/systemd/system/questboard.service.d"
  cp "${src}/bin/questboard-deploy" "${ROOT}/usr/local/sbin/questboard-deploy"
  for f in "${src}"/lib/*.sh; do
    cp "$f" "${ROOT}/usr/local/lib/questboard-deploy/$(basename "$f")"
  done
  for name in questboard-deploy-poll.service questboard-deploy-poll.timer; do
    if [ -f "${src}/systemd/${name}" ]; then
      cp "${src}/systemd/${name}" "${ROOT}/etc/systemd/system/${name}"
    fi
  done
  if [ -f "${src}/systemd/questboard.service.d/10-release-layout.conf" ]; then
    cp "${src}/systemd/questboard.service.d/10-release-layout.conf" \
      "${ROOT}/etc/systemd/system/questboard.service.d/10-release-layout.conf"
  fi
}

# A fresh root: release 1.2.0 active, the config in place, v1.3.0 and v1.3.1
# offered, and every stand-in at its healthy default.
new_case() {
  ROOT="${BASE}/case-$1"
  rm -rf "$ROOT"
  mkdir -p "${ROOT}/etc/questboard" "${ROOT}/mail" "${ROOT}/opt/questboard/releases"
  install_release 1.2.0
  activate_installed 1.2.0
  install_deploy_copies
  cat > "${ROOT}/etc/questboard/deploy.conf" <<'EOF'
QUESTBOARD_GITHUB_REPO=owner/quest-board
QUESTBOARD_NOTIFY_EMAIL=ops@example.com
QUESTBOARD_MAIL_FROM=deploy@example.com
QUESTBOARD_SMTP_HOST=smtp.example.com
QUESTBOARD_HEALTH_TIMEOUT_SECONDS=10
EOF
  chmod 600 "${ROOT}/etc/questboard/deploy.conf"
  cp -a "${BASE}/dl" "${ROOT}/dl"
  : > "${ROOT}/calls.log"
  : > "${ROOT}/curl.log"

  export QUESTBOARD_DEPLOY_ROOT="$ROOT"
  unset QUESTBOARD_DEPLOY_CONF
  export STUB_CALL_LOG="${ROOT}/calls.log" STUB_CURL_LOG="${ROOT}/curl.log"
  export STUB_MAIL_DIR="${ROOT}/mail" STUB_DL_DIR="${ROOT}/dl"
  export STUB_LATEST_TAG="v1.3.0" STUB_COMPARE_STATUS="behind"
  export STUB_GH_JSON="$GH_OK" STUB_STATUS_JSON="$STATUS_CLEAN"
  unset STUB_LATEST_EXIT STUB_COMPARE_EXIT STUB_DOWNLOAD_EXIT STUB_UNHEALTHY_VERSION \
    STUB_STATUS_EXIT STUB_BACKUP_EXIT STUB_APPLY_EXIT STUB_SYSTEMCTL_EXIT
}

RC=0
run_deploy() {
  RC=0
  "$DISPATCHER" "$@" > "${ROOT}/out.log" 2>&1 || RC=$?
}

calls() { paste -sd'|' "${ROOT}/calls.log"; }
mail_count() { find "${ROOT}/mail" -type f | wc -l | tr -d ' '; }
mail_text() { tr -d '\r' < "${ROOT}/mail/mail-1.eml"; }
mail_result() { mail_text | sed -n 's/^Result: //p'; }
current_version() { basename "$(readlink -f "${ROOT}/opt/questboard/current")"; }
last_attempt() { tail -n 1 "${ROOT}/var/lib/questboard-deploy/state/attempts" 2>/dev/null | awk '{print $1" "$2}'; }
previous_version() { cat "${ROOT}/var/lib/questboard-deploy/state/previous" 2>/dev/null || true; }
releases_listing() { ls -A "${ROOT}/opt/questboard/releases" | paste -sd' ' -; }
downloads_made() { grep -c 'releases/download' "${ROOT}/curl.log" || true; }
out_has() { has_text "${ROOT}/out.log" "$1"; }

# --- happy paths ----------------------------------------------------------

new_case happy
run_deploy install v1.3.0
check "happy: exit 0" "0" "$RC"
check "happy: order is status, stop, switch, start" \
  "migrator status 1.3.0 current=1.2.0|systemctl stop questboard.service current=1.2.0|systemctl start questboard.service current=1.3.0" \
  "$(calls)"
check "happy: current names the new release" "1.3.0" "$(current_version)"
check "happy: previous is the old release" "1.2.0" "$(previous_version)"
check "happy: attempt recorded" "v1.3.0 installed" "$(last_attempt)"
check "happy: exactly one mail" "1" "$(mail_count)"
check "happy: mail says installed" "installed" "$(mail_result)"
check "happy: no installer update notice when files match" "no" "$(has_text "${ROOT}/mail/mail-1.eml" 'Installer update available')"
check "happy: the download directory is cleaned up" "" "$(ls -A "${ROOT}/var/lib/questboard-deploy/downloads")"

new_case pending
export STUB_STATUS_JSON="$STATUS_PENDING"
run_deploy install v1.3.0
check "pending: exit 0" "0" "$RC"
check "pending: order is status, backup, stop, apply, switch, start" \
  "migrator status 1.3.0 current=1.2.0|migrator backup 1.3.0 --label v1.3.0 current=1.2.0|systemctl stop questboard.service current=1.2.0|migrator apply 1.3.0 current=1.2.0|systemctl start questboard.service current=1.3.0" \
  "$(calls)"
check "pending: current names the new release" "1.3.0" "$(current_version)"
check "pending: one mail" "1" "$(mail_count)"
check "pending: mail says installed" "installed" "$(mail_result)"

# --- refusals before anything is staged -----------------------------------

assert_untouched_refusal() {
  local label="$1" reason_text="$2"
  check "${label}: exit non-zero" "1" "$RC"
  check "${label}: no new release and no staging directory" "1.2.0" "$(releases_listing)"
  check "${label}: the app was never touched" "" "$(calls)"
  check "${label}: current unchanged" "1.2.0" "$(current_version)"
  check "${label}: one mail" "1" "$(mail_count)"
  check "${label}: mail says refused" "refused (${reason_text})" "$(mail_result)"
  check "${label}: tag remembered" "v1.3.0 refused" "$(last_attempt)"
}

new_case checksum
printf '%064d  questboard-v1.3.0.zip\n' 0 > "${ROOT}/dl/v1.3.0/questboard-v1.3.0.zip.sha256"
run_deploy install v1.3.0
assert_untouched_refusal "checksum mismatch" "the download did not match its checksum"

new_case attest
export STUB_GH_JSON=""
run_deploy install v1.3.0
assert_untouched_refusal "attestation failure" "the release signature check failed"

new_case diverged
export STUB_COMPARE_STATUS="diverged"
run_deploy install v1.3.0
assert_untouched_refusal "commit off main" "the release was not built from the main branch"

new_case compareunreachable
export STUB_COMPARE_EXIT=6
run_deploy install v1.3.0
assert_untouched_refusal "main check unreachable" "the release was not built from the main branch"

new_case nobundle
rm -f "${ROOT}/dl/v1.3.0/questboard-v1.3.0.zip.sigstore.json"
run_deploy install v1.3.0
assert_untouched_refusal "missing bundle asset" "a required release file was missing"

new_case nozip
rm -f "${ROOT}/dl/v1.3.0/questboard-v1.3.0.zip"
run_deploy install v1.3.0
assert_untouched_refusal "missing zip asset" "a required release file was missing"

# --- refusals and failures at the database stages -------------------------

new_case dbahead
export STUB_STATUS_EXIT=2 STUB_STATUS_JSON=""
run_deploy install v1.3.0
check "database ahead: exit non-zero" "1" "$RC"
check "database ahead: staged release removed" "1.2.0" "$(releases_listing)"
check "database ahead: only the status check ran" "migrator status 1.3.0 current=1.2.0" "$(calls)"
check "database ahead: mail says refused" "refused (the database is newer than this release)" "$(mail_result)"
check "database ahead: remembered" "v1.3.0 refused" "$(last_attempt)"

new_case nontx
export STUB_STATUS_EXIT=3 STUB_STATUS_JSON=""
run_deploy install v1.3.0
check "non-transactional: exit non-zero" "1" "$RC"
check "non-transactional: staged release removed" "1.2.0" "$(releases_listing)"
check "non-transactional: the app was never stopped" "migrator status 1.3.0 current=1.2.0" "$(calls)"
check "non-transactional: mail says refused" "refused (a migration cannot run safely in a transaction)" "$(mail_result)"

new_case dbdown
export STUB_STATUS_EXIT=4 STUB_STATUS_JSON=""
run_deploy install v1.3.0
check "database unreachable: exit non-zero" "1" "$RC"
check "database unreachable: app untouched" "migrator status 1.3.0 current=1.2.0" "$(calls)"
check "database unreachable: mail says failed" "failed (the database could not be reached)" "$(mail_result)"
check "database unreachable: recorded failed" "v1.3.0 failed" "$(last_attempt)"
check "database unreachable: staged release removed" "1.2.0" "$(releases_listing)"

new_case nobackup
export STUB_STATUS_JSON="$STATUS_PENDING" STUB_BACKUP_EXIT=5
run_deploy install v1.3.0
check "backup failure: exit non-zero" "1" "$RC"
check "backup failure: aborts before the app stops" \
  "migrator status 1.3.0 current=1.2.0|migrator backup 1.3.0 --label v1.3.0 current=1.2.0" "$(calls)"
check "backup failure: mail says failed" "failed (the pre-migration backup failed)" "$(mail_result)"
check "backup failure: current unchanged" "1.2.0" "$(current_version)"

new_case applyfail
export STUB_STATUS_JSON="$STATUS_PENDING" STUB_APPLY_EXIT=6
run_deploy install v1.3.0
check "apply failure: exit non-zero" "1" "$RC"
check "apply failure: stop then start, previous release restarted" \
  "migrator status 1.3.0 current=1.2.0|migrator backup 1.3.0 --label v1.3.0 current=1.2.0|systemctl stop questboard.service current=1.2.0|migrator apply 1.3.0 current=1.2.0|systemctl start questboard.service current=1.2.0" \
  "$(calls)"
check "apply failure: current still the previous release" "1.2.0" "$(current_version)"
check "apply failure: new release removed" "1.2.0" "$(releases_listing)"
check "apply failure: mail says failed, rolled back" "failed, rolled back (applying the migrations failed)" "$(mail_result)"
check "apply failure: previous release reported healthy" "yes" "$(has_text "${ROOT}/mail/mail-1.eml" 'Previous release healthy: yes')"
check "apply failure: recorded" "v1.3.0 failed_rolled_back" "$(last_attempt)"

# --- unhealthy new release ------------------------------------------------

new_case unhealthy
export STUB_UNHEALTHY_VERSION=1.3.0
run_deploy install v1.3.0
check "unhealthy, nothing pending: exit non-zero" "1" "$RC"
check "unhealthy, nothing pending: switched back and restarted" \
  "migrator status 1.3.0 current=1.2.0|systemctl stop questboard.service current=1.2.0|systemctl start questboard.service current=1.3.0|systemctl stop questboard.service current=1.3.0|systemctl start questboard.service current=1.2.0" \
  "$(calls)"
check "unhealthy, nothing pending: current is the previous release" "1.2.0" "$(current_version)"
check "unhealthy, nothing pending: one mail" "1" "$(mail_count)"
check "unhealthy, nothing pending: mail says rolled back" "rolled back (the new release did not become healthy)" "$(mail_result)"
check "unhealthy, nothing pending: previous confirmed healthy" "yes" "$(has_text "${ROOT}/mail/mail-1.eml" 'Previous release healthy: yes')"
check "unhealthy, nothing pending: the earlier previous value is restored" "" "$(previous_version)"
check "unhealthy, nothing pending: recorded" "v1.3.0 rolled_back" "$(last_attempt)"

new_case halted
export STUB_STATUS_JSON="$STATUS_PENDING" STUB_UNHEALTHY_VERSION=1.3.0
run_deploy install v1.3.0
check "halted: exit non-zero" "1" "$RC"
check "halted: nothing is stopped after the failed health wait" \
  "migrator status 1.3.0 current=1.2.0|migrator backup 1.3.0 --label v1.3.0 current=1.2.0|systemctl stop questboard.service current=1.2.0|migrator apply 1.3.0 current=1.2.0|systemctl start questboard.service current=1.3.0" \
  "$(calls)"
check "halted: the new release stays active" "1.3.0" "$(current_version)"
check "halted: one mail" "1" "$(mail_count)"
check "halted: mail says halted" "halted - migrations applied (the new release did not become healthy)" "$(mail_result)"
check "halted: mail carries the backup name" "yes" \
  "$(has_text "${ROOT}/mail/mail-1.eml" 'Backup: questboard-premigration-v1.3.0-20261004T120000Z.bak')"
check "halted: recorded" "v1.3.0 halted" "$(last_attempt)"

# --- poll -----------------------------------------------------------------

new_case poll-same
export STUB_LATEST_TAG=v1.2.0
run_deploy poll
check "poll: latest equals active exits 0" "0" "$RC"
check "poll: latest equals active sends no mail" "0" "$(mail_count)"
check "poll: latest equals active changes nothing" "" "$(calls)"
check "poll: latest equals active logs why" "yes" "$(out_has 'nothing newer than 1.2.0')"
check "poll: no download for an idle poll" "0" "$(downloads_made)"

new_case poll-down
export STUB_LATEST_EXIT=6
run_deploy poll
check "poll: GitHub unreachable exits 0" "0" "$RC"
check "poll: GitHub unreachable sends no mail" "0" "$(mail_count)"
check "poll: GitHub unreachable records nothing" "" "$(last_attempt)"
check "poll: GitHub unreachable logs one line" "yes" "$(out_has 'GitHub unreachable; the next poll retries')"

new_case poll-none
export STUB_LATEST_TAG=""
run_deploy poll
check "poll: no release yet exits 0" "0" "$RC"
check "poll: no release yet sends no mail" "0" "$(mail_count)"

new_case poll-remembered
mkdir -p "${ROOT}/var/lib/questboard-deploy/state"
printf 'v1.3.0 refused 2026-10-04T10:00:00Z\n' > "${ROOT}/var/lib/questboard-deploy/state/attempts"
run_deploy poll
check "poll: a remembered tag exits 0" "0" "$RC"
check "poll: a remembered tag sends no mail" "0" "$(mail_count)"
check "poll: a remembered tag is not downloaded" "0" "$(downloads_made)"
check "poll: a remembered tag names the manual command" "yes" "$(out_has 'questboard-deploy install v1.3.0')"
check "poll: a remembered tag adds no attempt" "v1.3.0 refused" "$(last_attempt)"

new_case poll-newer
mkdir -p "${ROOT}/var/lib/questboard-deploy/state"
printf 'v1.3.0 refused 2026-10-04T10:00:00Z\n' > "${ROOT}/var/lib/questboard-deploy/state/attempts"
export STUB_LATEST_TAG=v1.3.1
run_deploy poll
check "poll: a newer tag than the remembered one installs" "0" "$RC"
check "poll: a newer tag than the remembered one is active" "1.3.1" "$(current_version)"
check "poll: a newer tag sends one mail" "1" "$(mail_count)"

new_case poll-transport
export STUB_DOWNLOAD_EXIT=7
run_deploy poll
check "poll: a download transport failure exits 0" "0" "$RC"
check "poll: a download transport failure sends no mail" "0" "$(mail_count)"
check "poll: a download transport failure remembers nothing" "" "$(last_attempt)"
check "poll: a download transport failure leaves no download directory" "" "$(ls -A "${ROOT}/var/lib/questboard-deploy/downloads")"
check "poll: a download transport failure changes nothing" "" "$(calls)"

new_case poll-ok
run_deploy poll
check "poll: a newer verified tag installs" "0" "$RC"
check "poll: a newer verified tag is active" "1.3.0" "$(current_version)"

new_case manual-after-refusal
mkdir -p "${ROOT}/var/lib/questboard-deploy/state"
printf 'v1.3.0 refused 2026-10-04T10:00:00Z\n' > "${ROOT}/var/lib/questboard-deploy/state/attempts"
run_deploy install v1.3.0
check "install by hand retries a remembered tag" "0" "$RC"
check "install by hand records the new outcome" "v1.3.0 installed" "$(last_attempt)"

new_case manual-by-hand-download-failure
export STUB_DOWNLOAD_EXIT=7
run_deploy install v1.3.0
check "install by hand: a download transport failure is an error" "1" "$RC"
check "install by hand: a download transport failure sends no mail" "0" "$(mail_count)"

# --- locking --------------------------------------------------------------

new_case locked
mkdir -p "${ROOT}/run/questboard-deploy"
( exec 8>"${ROOT}/run/questboard-deploy/deploy.lock"; flock -n 8; : > "${ROOT}/lock-held"; exec sleep 30 ) &
holder=$!
for _ in $(seq 1 50); do
  [ -f "${ROOT}/lock-held" ] && break
  sleep 0.1
done
run_deploy install v1.3.0
kill "$holder" 2>/dev/null || true
wait "$holder" 2>/dev/null || true
check "lock: a second invocation exits non-zero" "1" "$RC"
check "lock: it says why" "yes" "$(out_has 'another questboard-deploy invocation is already running')"
check "lock: it touches nothing" "" "$(calls)"
check "lock: current unchanged" "1.2.0" "$(current_version)"
check "lock: no mail" "0" "$(mail_count)"

# --- configuration --------------------------------------------------------

new_case env-ignored
export QUESTBOARD_HEALTH_URL="http://127.0.0.1:1/never"
run_deploy install v1.3.0
unset QUESTBOARD_HEALTH_URL
check "an environment variable cannot override the configured health URL" "0" "$RC"

new_case missing-repo
sed -i '/QUESTBOARD_GITHUB_REPO/d' "${ROOT}/etc/questboard/deploy.conf"
run_deploy install v1.3.0
check "a configuration without a repository is refused" "1" "$RC"
check "a configuration without a repository touches nothing" "" "$(calls)"

# --- manual rollback ------------------------------------------------------

# 1.3.0 active, 1.2.0 still on disk as the previous release.
rollback_case() {
  new_case "$1"
  install_release 1.3.0
  activate_installed 1.3.0
  mkdir -p "${ROOT}/var/lib/questboard-deploy/state"
  printf '1.2.0\n' > "${ROOT}/var/lib/questboard-deploy/state/previous"
}

rollback_case rb-ok
run_deploy rollback 1.2.0
check "rollback: exit 0" "0" "$RC"
check "rollback: status, stop, switch, start" \
  "migrator status 1.2.0 current=1.3.0|systemctl stop questboard.service current=1.3.0|systemctl start questboard.service current=1.2.0" \
  "$(calls)"
check "rollback: current names the target" "1.2.0" "$(current_version)"
check "rollback: recorded as a manual rollback" "v1.2.0 rolled_back_manual" "$(last_attempt)"
check "rollback: sends no mail" "0" "$(mail_count)"
check "rollback: reports on the terminal" "yes" "$(out_has 'rolled back to release 1.2.0')"

check "rollback: the release rolled back from is abandoned" "yes" \
  "$(has_text "${ROOT}/var/lib/questboard-deploy/state/attempts" 'v1.3.0 abandoned')"
check "rollback: the abandon is logged" "yes" "$(out_has 'outcome=abandoned tag=v1.3.0')"

# The rollback must stick: the abandoned release is still the latest published
# one, and polls must leave it alone until it is installed by hand.
export STUB_LATEST_TAG=v1.3.0
: > "${ROOT}/calls.log"
: > "${ROOT}/curl.log"
run_deploy poll
check "rollback sticks: the next poll exits 0" "0" "$RC"
check "rollback sticks: the poll installs nothing" "" "$(calls)"
check "rollback sticks: the poll downloads nothing" "0" "$(downloads_made)"
check "rollback sticks: the poll sends no mail" "0" "$(mail_count)"
check "rollback sticks: current is still the target" "1.2.0" "$(current_version)"
check "rollback sticks: the poll names the manual command" "yes" "$(out_has 'skipping v1.3.0: abandoned earlier; run questboard-deploy install v1.3.0 to try it again')"
check "rollback sticks: the poll adds no attempt" "v1.2.0 rolled_back_manual" "$(last_attempt)"
run_deploy poll
check "rollback sticks: a second poll still changes nothing" "" "$(calls)"

# An explicit install of the abandoned tag still works and clears the memory.
run_deploy install v1.3.0
check "rollback then install by hand: exit 0" "0" "$RC"
check "rollback then install by hand: the release is active" "1.3.0" "$(current_version)"
check "rollback then install by hand: recorded installed" "v1.3.0 installed" "$(last_attempt)"

# After the memory is cleared, a rollback again followed by a newer release.
rollback_case rb-newer-release
run_deploy rollback 1.2.0
export STUB_LATEST_TAG=v1.3.1
run_deploy poll
check "a newer release than the abandoned one: the poll installs it" "0" "$RC"
check "a newer release than the abandoned one: it is active" "1.3.1" "$(current_version)"
check "a newer release than the abandoned one: one mail" "1" "$(mail_count)"
check "a newer release than the abandoned one: recorded installed" "v1.3.1 installed" "$(last_attempt)"

rollback_case rb-unknown
export STUB_STATUS_EXIT=2 STUB_STATUS_JSON=""
run_deploy rollback 1.2.0
check "rollback to a release unaware of applied migrations: refused" "1" "$RC"
check "rollback unknown: says why" "yes" "$(out_has 'the database holds migrations release 1.2.0 does not know')"
check "rollback unknown: nothing changes" "migrator status 1.2.0 current=1.3.0" "$(calls)"
check "rollback unknown: current unchanged" "1.3.0" "$(current_version)"
check "rollback unknown: nothing recorded" "" "$(last_attempt)"

rollback_case rb-pending
export STUB_STATUS_JSON="$STATUS_PENDING"
run_deploy rollback 1.2.0
check "rollback to a release with pending migrations: refused" "1" "$RC"
check "rollback pending: says to install instead" "yes" "$(out_has 'install it instead')"
check "rollback pending: nothing changes" "migrator status 1.2.0 current=1.3.0" "$(calls)"
check "rollback pending: nothing abandoned or recorded" "" "$(last_attempt)"

rollback_case rb-unreadable
export STUB_STATUS_EXIT=4 STUB_STATUS_JSON=""
run_deploy rollback 1.2.0
check "rollback with an unreadable database: refused" "1" "$RC"
check "rollback unreadable: nothing changes" "migrator status 1.2.0 current=1.3.0" "$(calls)"

rollback_case rb-adopted
rm -rf "${ROOT}/opt/questboard/releases/1.2.0"
install_release 1.2.0 adopted
run_deploy rollback 1.2.0
check "rollback to the adopted release: refused" "1" "$RC"
check "rollback adopted: points at the manual restore steps" "yes" "$(out_has 'docs/deploy.md')"
check "rollback adopted: the migrator was never run" "" "$(calls)"
check "rollback adopted: current unchanged" "1.3.0" "$(current_version)"
check "rollback adopted: nothing abandoned or recorded" "" "$(last_attempt)"

rollback_case rb-unhealthy
export STUB_UNHEALTHY_VERSION=1.2.0
run_deploy rollback 1.2.0
check "rollback to a release that never becomes healthy: fails" "1" "$RC"
check "rollback unhealthy: nothing abandoned or recorded" "" "$(last_attempt)"

rollback_case rb-missing
run_deploy rollback 1.1.0
check "rollback to a version not on disk: refused" "1" "$RC"
check "rollback missing: nothing runs" "" "$(calls)"

rollback_case rb-active
run_deploy rollback 1.3.0
check "rollback to the active version: refused" "1" "$RC"
check "rollback active: nothing runs" "" "$(calls)"
check "rollback active: nothing abandoned or recorded" "" "$(last_attempt)"

rollback_case rb-badarg
run_deploy rollback v1.2.0
check "rollback with a tag instead of a version: refused" "1" "$RC"

# --- redeploy and older tags ----------------------------------------------

new_case redeploy
run_deploy install v1.2.0
check "redeploy: exit 0" "0" "$RC"
check "redeploy: restart only" "systemctl restart questboard.service current=1.2.0" "$(calls)"
check "redeploy: nothing downloaded" "0" "$(downloads_made)"
check "redeploy: one mail" "1" "$(mail_count)"
check "redeploy: mail says installed" "installed" "$(mail_result)"
check "redeploy: current unchanged" "1.2.0" "$(current_version)"

new_case redeploy-unhealthy
export STUB_UNHEALTHY_VERSION=1.2.0
run_deploy install v1.2.0
check "redeploy unhealthy: exit non-zero" "1" "$RC"
check "redeploy unhealthy: one mail" "1" "$(mail_count)"
check "redeploy unhealthy: mail says failed" "failed (the service could not be restarted)" "$(mail_result)"

new_case older
run_deploy install v1.1.0
check "an older tag: refused" "1" "$RC"
check "an older tag: points at rollback" "yes" "$(out_has 'use rollback')"
check "an older tag: no mail" "0" "$(mail_count)"
check "an older tag: no record" "" "$(last_attempt)"
check "an older tag: nothing downloaded or run" "" "$(calls)"
check "an older tag: nothing downloaded" "0" "$(downloads_made)"

new_case badtag
run_deploy install 1.3.0
check "a tag without the v prefix: refused" "1" "$RC"
check "a tag without the v prefix: no mail" "0" "$(mail_count)"

# --- installer update notice ----------------------------------------------

new_case update-notice
printf '# local change\n' >> "${ROOT}/usr/local/lib/questboard-deploy/common.sh"
before="$(sha256sum "${ROOT}/usr/local/lib/questboard-deploy/common.sh" "${ROOT}/usr/local/sbin/questboard-deploy" | paste -sd' ' -)"
run_deploy install v1.3.0
after="$(sha256sum "${ROOT}/usr/local/lib/questboard-deploy/common.sh" "${ROOT}/usr/local/sbin/questboard-deploy" | paste -sd' ' -)"
check "update notice: install succeeds" "0" "$RC"
check "update notice: mail announces the update" "yes" \
  "$(has_text "${ROOT}/mail/mail-1.eml" 'Installer update available: run setup from release 1.3.0')"
check "update notice: installed files are never modified" "$before" "$after"

new_case missing-installed-copy
rm -f "${ROOT}/usr/local/lib/questboard-deploy/release.sh"
run_deploy install v1.3.0
check "update notice: a missing installed file counts as a difference" "yes" \
  "$(has_text "${ROOT}/mail/mail-1.eml" 'Installer update available')"

# --- a real packaged release installs through its own installer -----------

if [ -n "${QUESTBOARD_TEST_PACKAGED_ZIP:-}" ]; then
  zip_path="$QUESTBOARD_TEST_PACKAGED_ZIP"
  pkg_version="$(unzip -p "$zip_path" release-manifest.json | python3 -c 'import json,sys; print(json.load(sys.stdin)["version"])')"
  new_case packaged
  rm -rf "${ROOT}/opt/questboard/releases/1.2.0"
  install_release 0.0.0
  activate_installed 0.0.0
  rm -rf "${ROOT}/dl"
  mkdir -p "${ROOT}/dl/v${pkg_version}"
  cp "$zip_path" "${ROOT}/dl/v${pkg_version}/questboard-v${pkg_version}.zip"
  ( cd "${ROOT}/dl/v${pkg_version}" \
    && sha256sum "questboard-v${pkg_version}.zip" > "questboard-v${pkg_version}.zip.sha256" )
  printf 'bundle' > "${ROOT}/dl/v${pkg_version}/questboard-v${pkg_version}.zip.sigstore.json"
  export STUB_LATEST_TAG="v${pkg_version}"
  shipped="${BASE}/shipped"
  rm -rf "$shipped"
  mkdir -p "$shipped"
  unzip -q "$zip_path" 'deploy/bin/*' 'deploy/lib/*' -d "$shipped"
  install_deploy_copies "${shipped}/deploy"
  RC=0
  bash "${shipped}/deploy/bin/questboard-deploy" install "v${pkg_version}" > "${ROOT}/out.log" 2>&1 || RC=$?
  check "packaged zip: the shipped installer installs it" "0" "$RC"
  check "packaged zip: current names its version" "$pkg_version" "$(current_version)"
  check "packaged zip: one mail" "1" "$(mail_count)"
  check "packaged zip: mail says installed" "installed" "$(mail_result)"
  check "packaged zip: recorded" "v${pkg_version} installed" "$(last_attempt)"
else
  printf 'SKIP: packaged zip not supplied\n'
fi

check "host commands were never called" "" "$(host_guard_calls)"

if [ "$FAILURES" -gt 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES"
  exit 1
fi
printf 'all install flow checks passed\n'
