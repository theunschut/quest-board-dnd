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
    [ -n "${STUB_COMPARE_CODE:-}" ] && code="$STUB_COMPARE_CODE"
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
  *tuf-repo*|https://api.github.com/)
    # The connectivity probe after a failed attestation check.
    [ -n "${STUB_PROBE_EXIT:-}" ] && exit "$STUB_PROBE_EXIT"
    ;;
  *)
    code=404
    ;;
esac
printf '%s' "$code"
exit 0
EOF

# gh: prints STUB_GH_JSON, or fails when it is empty. STUB_GH_STDERR is a
# notice written to stderr alongside a successful result.
cat > "${STUBS}/gh" <<'EOF'
#!/bin/sh
printf 'gh %s\n' "$*" >> "$STUB_CURL_LOG"
if [ -z "${STUB_GH_JSON:-}" ]; then
  echo "verification failed" >&2
  exit 1
fi
if [ -n "${STUB_GH_STDERR:-}" ]; then
  printf '%s\n' "$STUB_GH_STDERR" >&2
fi
printf '%s\n' "$STUB_GH_JSON"
EOF

# timeout: records that the bound was applied and either lets the bound expire
# at once (STUB_TIMEOUT_FIRES=1, exit 124 as the real one does) or runs the
# bounded command.
cat > "${STUBS}/timeout" <<'EOF'
#!/bin/sh
printf 'timeout %s\n' "$*" >> "$STUB_CURL_LOG"
while [ "$#" -gt 0 ]; do
  case "$1" in
    --kill-after=*) shift ;;
    [0-9]*) shift; break ;;
    *) break ;;
  esac
done
if [ "${STUB_TIMEOUT_FIRES:-}" = "1" ]; then
  exit 124
fi
exec "$@"
EOF

# chmod: fails on a staging directory while STUB_CHMOD_FAIL_STAGING=1, to stand
# in for a hardening step that cannot be completed. Otherwise the real chmod.
REAL_CHMOD="$(command -v chmod)"
cat > "${STUBS}/chmod" <<EOF
#!/bin/sh
if [ "\${STUB_CHMOD_FAIL_STAGING:-}" = "1" ]; then
  case "\$*" in *.staging-*) exit 1 ;; esac
fi
exec "${REAL_CHMOD}" "\$@"
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
    # After an apply ran, STUB_STATUS_AFTER_APPLY_JSON/EXIT (when set) say what
    # the database holds then, e.g. a commit whose acknowledgement was lost.
    if [ -f "${STUB_CALL_LOG}.applied" ] && [ "${STUB_STATUS_AFTER_APPLY_JSON+set}" = "set" ]; then
      printf '%s\n' "$STUB_STATUS_AFTER_APPLY_JSON"
      exit "${STUB_STATUS_AFTER_APPLY_EXIT:-0}"
    fi
    printf '%s\n' "${STUB_STATUS_JSON:-}"
    exit "${STUB_STATUS_EXIT:-0}" ;;
  backup)
    printf '{"backupName":"questboard-premigration-%s-20261004T120000Z.bak"}\n' "$label"
    exit "${STUB_BACKUP_EXIT:-0}" ;;
  apply)
    : > "${STUB_CALL_LOG}.applied"
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
  host_guard_mark_test_root "$ROOT"
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
  : > "$HOST_GUARD_JOURNAL"
  unset JOURNAL_STREAM

  export QUESTBOARD_DEPLOY_ROOT="$ROOT"
  unset QUESTBOARD_DEPLOY_CONF
  export STUB_CALL_LOG="${ROOT}/calls.log" STUB_CURL_LOG="${ROOT}/curl.log"
  export STUB_MAIL_DIR="${ROOT}/mail" STUB_DL_DIR="${ROOT}/dl"
  export STUB_LATEST_TAG="v1.3.0" STUB_COMPARE_STATUS="behind"
  export STUB_GH_JSON="$GH_OK" STUB_STATUS_JSON="$STATUS_CLEAN"
  unset STUB_LATEST_EXIT STUB_COMPARE_EXIT STUB_DOWNLOAD_EXIT STUB_UNHEALTHY_VERSION \
    STUB_STATUS_EXIT STUB_BACKUP_EXIT STUB_APPLY_EXIT STUB_SYSTEMCTL_EXIT STUB_PROBE_EXIT \
    STUB_GH_STDERR STUB_TIMEOUT_FIRES STUB_COMPARE_CODE STUB_CHMOD_FAIL_STAGING \
    STUB_STATUS_AFTER_APPLY_JSON STUB_STATUS_AFTER_APPLY_EXIT
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
check "happy: a run from a shell leaves its outcome in the journal" "yes" \
  "$(host_guard_journal | grep -qF 'logger -t questboard-deploy -p daemon.info -- outcome=installed tag=v1.3.0 reason=none' && echo yes || echo no)"

# A run under a systemd unit already has its stderr in the journal, so the
# installer adds no second copy.
new_case journal-under-unit
export JOURNAL_STREAM="8:4242"
run_deploy install v1.3.0
unset JOURNAL_STREAM
check "under a unit: the install succeeds" "0" "$RC"
check "under a unit: nothing is copied to the journal a second time" "" "$(host_guard_journal)"
check "under a unit: the line is still on stderr" "yes" "$(out_has 'outcome=installed tag=v1.3.0 reason=none')"

# An error from a manual run reaches the journal too.
new_case journal-error
run_deploy install v1.1.0
check "a refusal from a shell is copied to the journal at error priority" "yes" \
  "$(host_guard_journal | grep -q 'daemon.err -- ERROR: refusing v1.1.0' && echo yes || echo no)"

# gh may print a notice on stderr while it verifies successfully. That must not
# turn a good release into a remembered refusal.
new_case gh-notice
export STUB_GH_STDERR="A new release of gh is available"
run_deploy install v1.3.0
check "gh notice on stderr: the release installs" "0" "$RC"
check "gh notice on stderr: current names the new release" "1.3.0" "$(current_version)"
check "gh notice on stderr: recorded installed" "v1.3.0 installed" "$(last_attempt)"

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

# A refusal is remembered: the poll must not retry a tag the check rejected
# while the verification services were reachable.
run_deploy poll
check "attestation failure: a later poll skips the refused tag" "0" "$RC"
check "attestation failure: a later poll downloads nothing more" "3" "$(downloads_made)"
check "attestation failure: a later poll names the manual command" "yes" "$(out_has 'skipping v1.3.0: refused earlier')"
check "attestation failure: a later poll sends no further mail" "1" "$(mail_count)"

# The verification services are down: no verdict exists, so nothing is staged,
# stopped, mailed or remembered, and the tag is retried.
assert_outage_changed_nothing() {
  local label="$1"
  check "${label}: no new release and no staging directory" "1.2.0" "$(releases_listing)"
  check "${label}: the app was never touched" "" "$(calls)"
  check "${label}: current unchanged" "1.2.0" "$(current_version)"
  check "${label}: no mail" "0" "$(mail_count)"
  check "${label}: no attempt recorded" "" "$(last_attempt)"
  check "${label}: the tag is not remembered" "no" \
    "$(has_text "${ROOT}/var/lib/questboard-deploy/state/attempts" 'v1.3.0')"
  check "${label}: the download directory is cleaned up" "" "$(ls -A "${ROOT}/var/lib/questboard-deploy/downloads")"
}

new_case outage-install
export STUB_GH_JSON="" STUB_PROBE_EXIT=6
run_deploy install v1.3.0
check "outage, install by hand: exit non-zero" "1" "$RC"
check "outage, install by hand: says nothing changed and to retry" "yes" \
  "$(out_has 'verification services unreachable; nothing changed, try again later')"
assert_outage_changed_nothing "outage, install by hand"
unset STUB_PROBE_EXIT
export STUB_GH_JSON="$GH_OK"
run_deploy install v1.3.0
check "outage, install by hand: a retry once the services are back installs" "0" "$RC"
check "outage, install by hand: a retry installs the release" "1.3.0" "$(current_version)"

new_case outage-poll
export STUB_GH_JSON="" STUB_PROBE_EXIT=6
run_deploy poll
check "outage, poll: exit 0" "0" "$RC"
check "outage, poll: logs that the next poll retries" "yes" "$(out_has 'verification services unreachable')"
assert_outage_changed_nothing "outage, poll"
run_deploy poll
check "outage, poll: a second poll in the outage is equally harmless" "0" "$RC"
assert_outage_changed_nothing "outage, second poll"
check "outage, poll: the tag is downloaded again by the retry" "6" "$(downloads_made)"
unset STUB_PROBE_EXIT
export STUB_GH_JSON="$GH_OK"
run_deploy poll
check "outage, poll: the next poll after the outage installs" "0" "$RC"
check "outage, poll: the release is active" "1.3.0" "$(current_version)"
check "outage, poll: one mail for the install" "1" "$(mail_count)"
check "outage, poll: mail says installed" "installed" "$(mail_result)"
check "outage, poll: recorded installed" "v1.3.0 installed" "$(last_attempt)"

# gh is always run under a time bound.
new_case gh-bounded
run_deploy install v1.3.0
check "gh is run under a time bound" "yes" "$(has_text "${ROOT}/curl.log" 'timeout --kill-after=')"

# A gh run that outlives its bound while the verification services are down is
# an outage like any other: quiet, nothing remembered, retried by the next poll.
new_case gh-timeout-outage
export STUB_TIMEOUT_FIRES=1 STUB_PROBE_EXIT=6
run_deploy poll
check "gh timeout, services down, poll: exit 0" "0" "$RC"
check "gh timeout, services down, poll: logs the outage" "yes" "$(out_has 'verification services unreachable')"
assert_outage_changed_nothing "gh timeout, services down, poll"
unset STUB_TIMEOUT_FIRES STUB_PROBE_EXIT
run_deploy poll
check "gh timeout, services down: the next poll installs" "1.3.0" "$(current_version)"

# The same timeout while the services answer is not an outage: refuse.
new_case gh-timeout-reachable
export STUB_TIMEOUT_FIRES=1
run_deploy install v1.3.0
assert_untouched_refusal "gh timeout, services reachable" "the release signature check failed"

# gh failing while the probe finds the services reachable stays a refusal even
# if some other tag was tried earlier during an outage.
new_case outage-then-refused
export STUB_GH_JSON="" STUB_PROBE_EXIT=6
run_deploy poll
unset STUB_PROBE_EXIT
run_deploy poll
check "outage then a real rejection: refused" "1" "$([ "$RC" -ne 0 ] && echo 1 || echo 0)"
check "outage then a real rejection: remembered" "v1.3.0 refused" "$(last_attempt)"

new_case diverged
export STUB_COMPARE_STATUS="diverged"
run_deploy install v1.3.0
assert_untouched_refusal "commit off main" "the release was not built from the main branch"

# GitHub giving no usable answer to the main-branch check says nothing about the
# release, so it is treated like the verification outage: nothing changes,
# nothing is mailed or remembered, and the next poll retries.
new_case compare-unreachable-install
export STUB_COMPARE_EXIT=6
run_deploy install v1.3.0
check "main check unreachable, install by hand: exit non-zero" "1" "$RC"
check "main check unreachable, install by hand: says nothing changed and to retry" "yes" \
  "$(out_has 'nothing changed, try again later')"
assert_outage_changed_nothing "main check unreachable, install by hand"
unset STUB_COMPARE_EXIT
run_deploy install v1.3.0
check "main check unreachable: a retry once GitHub answers installs" "1.3.0" "$(current_version)"

for compare_code in 403 429 503; do
  new_case "compare-http-${compare_code}"
  export STUB_COMPARE_CODE="$compare_code"
  run_deploy poll
  check "main check HTTP ${compare_code}, poll: exit 0" "0" "$RC"
  check "main check HTTP ${compare_code}, poll: logs that the next poll retries" "yes" "$(out_has 'the next poll retries')"
  assert_outage_changed_nothing "main check HTTP ${compare_code}, poll"
  run_deploy poll
  check "main check HTTP ${compare_code}: a second poll is equally harmless" "0" "$RC"
  unset STUB_COMPARE_CODE
  run_deploy poll
  check "main check HTTP ${compare_code}: the next poll after it clears installs" "1.3.0" "$(current_version)"
  check "main check HTTP ${compare_code}: one mail, for the install" "installed" "$(mail_result)"
done

# An answer that says the commit is unknown to the repository is definite.
new_case compare-404
export STUB_COMPARE_CODE=404
run_deploy install v1.3.0
assert_untouched_refusal "main check HTTP 404" "the release was not built from the main branch"

# A hardening step that fails while staging must stop the install: the release
# is never put in place, and the reason is accurate (not "disk space").
new_case hardening-fails
export STUB_CHMOD_FAIL_STAGING=1
run_deploy install v1.3.0
check "failed hardening: exit non-zero" "1" "$RC"
check "failed hardening: no new release and no staging directory" "1.2.0" "$(releases_listing)"
check "failed hardening: the app was never touched" "" "$(calls)"
check "failed hardening: current unchanged" "1.2.0" "$(current_version)"
check "failed hardening: mail says failed with the staging reason" \
  "failed (the release could not be put in place)" "$(mail_result)"
check "failed hardening: recorded failed" "v1.3.0 failed" "$(last_attempt)"

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

# The backup is skipped only when the status explicitly says there is no
# database yet. A status that does not say, or says something else, stops the
# install before the app is touched: a safety net must not fail open.
new_case fresh-host
export STUB_STATUS_JSON='{"databaseExists":false,"applied":[],"pending":["B"],"unknown":[],"nonTransactional":[],"canBackup":false}'
run_deploy install v1.3.0
check "fresh host: exit 0" "0" "$RC"
check "fresh host: no backup is taken, the migrations apply" \
  "migrator status 1.3.0 current=1.2.0|systemctl stop questboard.service current=1.2.0|migrator apply 1.3.0 current=1.2.0|systemctl start questboard.service current=1.3.0" \
  "$(calls)"

new_case status-without-database-flag
export STUB_STATUS_JSON='{"applied":["A"],"pending":["B"],"unknown":[],"nonTransactional":[],"canBackup":true}'
run_deploy install v1.3.0
check "status without databaseExists: exit non-zero" "1" "$RC"
check "status without databaseExists: nothing runs but the status check" "migrator status 1.3.0 current=1.2.0" "$(calls)"
check "status without databaseExists: no migration was applied" "no" "$(has_text "${ROOT}/calls.log" 'migrator apply')"
check "status without databaseExists: mail says failed" "failed (the database could not be reached)" "$(mail_result)"
check "status without databaseExists: staged release removed" "1.2.0" "$(releases_listing)"
check "status without databaseExists: current unchanged" "1.2.0" "$(current_version)"

new_case status-database-flag-not-boolean
export STUB_STATUS_JSON='{"databaseExists":"yes","applied":["A"],"pending":["B"],"unknown":[],"nonTransactional":[],"canBackup":true}'
run_deploy install v1.3.0
check "databaseExists that is not a boolean: nothing runs but the status check" "migrator status 1.3.0 current=1.2.0" "$(calls)"
check "databaseExists that is not a boolean: recorded failed" "v1.3.0 failed" "$(last_attempt)"

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
check "apply failure: the database is read again, then the previous release is restarted" \
  "migrator status 1.3.0 current=1.2.0|migrator backup 1.3.0 --label v1.3.0 current=1.2.0|systemctl stop questboard.service current=1.2.0|migrator apply 1.3.0 current=1.2.0|migrator status 1.3.0 current=1.2.0|systemctl start questboard.service current=1.2.0" \
  "$(calls)"
check "apply failure: current still the previous release" "1.2.0" "$(current_version)"
check "apply failure: new release removed" "1.2.0" "$(releases_listing)"
check "apply failure: mail says failed, rolled back" "failed, rolled back (applying the migrations failed)" "$(mail_result)"
check "apply failure: previous release reported healthy" "yes" "$(has_text "${ROOT}/mail/mail-1.eml" 'Previous release healthy: yes')"
check "apply failure: recorded" "v1.3.0 failed_rolled_back" "$(last_attempt)"

# A failed apply is only a rollback when the database still holds the same
# pending migrations. If the commit went through even though the migrator
# reported a failure (a lost acknowledgement), the old release must not be
# started on the migrated schema: the install carries on as an applied one.
new_case apply-failed-but-committed
export STUB_STATUS_JSON="$STATUS_PENDING" STUB_APPLY_EXIT=6 STUB_STATUS_AFTER_APPLY_JSON="$STATUS_CLEAN"
run_deploy install v1.3.0
check "apply failed but committed: exit 0, the new release is healthy" "0" "$RC"
check "apply failed but committed: the database is read again, then the new release starts" \
  "migrator status 1.3.0 current=1.2.0|migrator backup 1.3.0 --label v1.3.0 current=1.2.0|systemctl stop questboard.service current=1.2.0|migrator apply 1.3.0 current=1.2.0|migrator status 1.3.0 current=1.2.0|systemctl start questboard.service current=1.3.0" \
  "$(calls)"
check "apply failed but committed: current names the new release" "1.3.0" "$(current_version)"
check "apply failed but committed: one mail, installed" "installed" "$(mail_result)"
check "apply failed but committed: recorded installed" "v1.3.0 installed" "$(last_attempt)"
check "apply failed but committed: the journal says what happened" "yes" \
  "$(out_has 'the database already holds the migrations')"

new_case apply-failed-but-committed-unhealthy
export STUB_STATUS_JSON="$STATUS_PENDING" STUB_APPLY_EXIT=6 STUB_STATUS_AFTER_APPLY_JSON="$STATUS_CLEAN" \
  STUB_UNHEALTHY_VERSION=1.3.0
run_deploy install v1.3.0
check "apply failed but committed, unhealthy: exit non-zero" "1" "$RC"
check "apply failed but committed, unhealthy: the new release stays active" "1.3.0" "$(current_version)"
check "apply failed but committed, unhealthy: the previous release is never restarted" "no" \
  "$(has_text "${ROOT}/calls.log" 'systemctl start questboard.service current=1.2.0')"
check "apply failed but committed, unhealthy: mail says halted" \
  "halted - migrations applied (the new release did not become healthy)" "$(mail_result)"
check "apply failed but committed, unhealthy: mail carries the backup name" "yes" \
  "$(has_text "${ROOT}/mail/mail-1.eml" 'Backup: questboard-premigration-v1.3.0-20261004T120000Z.bak')"
check "apply failed but committed, unhealthy: recorded halted" "v1.3.0 halted" "$(last_attempt)"

# Some but not all migrations gone from the pending list is also not a rollback.
new_case apply-failed-partly-committed
export STUB_STATUS_JSON='{"databaseExists":true,"applied":["A"],"pending":["B","C"],"unknown":[],"nonTransactional":[],"canBackup":true}' \
  STUB_APPLY_EXIT=6 STUB_STATUS_AFTER_APPLY_JSON="$STATUS_PENDING"
run_deploy install v1.3.0
check "apply failed, partly committed: the previous release is not restarted on it" "no" \
  "$(has_text "${ROOT}/calls.log" 'systemctl start questboard.service current=1.2.0')"
check "apply failed, partly committed: the new release is made active" "1.3.0" "$(current_version)"

# When the database cannot be read again, nothing proves the schema changed, so
# the install ends as it always did: the previous release is started again.
new_case apply-failed-unreadable
export STUB_STATUS_JSON="$STATUS_PENDING" STUB_APPLY_EXIT=6 STUB_STATUS_AFTER_APPLY_JSON="" STUB_STATUS_AFTER_APPLY_EXIT=4
run_deploy install v1.3.0
check "apply failed, database unreadable: exit non-zero" "1" "$RC"
check "apply failed, database unreadable: the database was asked three times" "3" \
  "$(grep -c 'migrator status 1.3.0 current=1.2.0' "${ROOT}/calls.log" | awk '{print $1 - 1}')"
check "apply failed, database unreadable: current is still the previous release" "1.2.0" "$(current_version)"
check "apply failed, database unreadable: mail says failed, rolled back" \
  "failed, rolled back (applying the migrations failed)" "$(mail_result)"
check "apply failed, database unreadable: the journal says it could not be confirmed" "yes" \
  "$(out_has 'could not be read again')"

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

# The variables that relocate the installer for these tests relax the root check
# and the owner check on the configuration file. They are honoured only for a
# tree that proves it is a test tree; a run that inherits them from somewhere
# else is refused and touches nothing.
assert_seam_refused() {
  local label="$1"
  check "${label}: refused" "1" "$RC"
  check "${label}: says why" "yes" "$(out_has 'not a test tree owned by this user')"
  check "${label}: nothing ran" "" "$(calls)"
  check "${label}: current unchanged" "1.2.0" "$(current_version)"
  check "${label}: no mail" "0" "$(mail_count)"
  check "${label}: no state written" "no" "$([ -e "${ROOT}/var" ] && echo yes || echo no)"
}

new_case seam-no-marker
rm -f "${ROOT}/.questboard-test-root"
run_deploy install v1.3.0
assert_seam_refused "a relocated root without the test marker"

new_case seam-marker-symlink
rm -f "${ROOT}/.questboard-test-root"
ln -s /dev/null "${ROOT}/.questboard-test-root"
run_deploy install v1.3.0
assert_seam_refused "a test marker that is a symlink"

new_case seam-conf-only
RC=0
env -u QUESTBOARD_DEPLOY_ROOT QUESTBOARD_DEPLOY_CONF="${ROOT}/etc/questboard/deploy.conf" \
  "$DISPATCHER" install v1.3.0 > "${ROOT}/out.log" 2>&1 || RC=$?
check "a configuration path without a test tree: refused" "1" "$RC"
check "a configuration path without a test tree: says why" "yes" "$(out_has 'not a test tree owned by this user')"
check "a configuration path without a test tree: nothing ran" "" "$(calls)"

new_case seam-conf-in-test-tree
printf 'QUESTBOARD_SMTP_PORT=25\n' > "${ROOT}/alt.conf"
chmod 600 "${ROOT}/alt.conf"
export QUESTBOARD_DEPLOY_CONF="${ROOT}/alt.conf"
run_deploy install v1.3.0
unset QUESTBOARD_DEPLOY_CONF
check "a configuration path in a test tree is used" "yes" "$(out_has 'QUESTBOARD_GITHUB_REPO is not set')"
check "a configuration path in a test tree: nothing ran" "" "$(calls)"

if [ "$(id -u)" -ne 0 ]; then
  new_case seam-absent-not-root
  RC=0
  env -u QUESTBOARD_DEPLOY_ROOT -u QUESTBOARD_DEPLOY_CONF "$DISPATCHER" install v1.3.0 > "${ROOT}/out.log" 2>&1 || RC=$?
  check "without any relocation a non-root caller is refused" "1" "$RC"
  check "without any relocation the root check says so" "yes" "$(out_has 'must be run as root')"
  check "without any relocation nothing ran" "" "$(calls)"
fi

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
check "rollback: the manual switch is in the journal" "yes" \
  "$(host_guard_journal | grep -qF 'outcome=rolled_back_manual tag=v1.2.0 reason=none' && echo yes || echo no)"
check "rollback: the abandon is in the journal" "yes" \
  "$(host_guard_journal | grep -qF 'outcome=abandoned tag=v1.3.0 reason=none' && echo yes || echo no)"

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
# The link already moved and the app was restarted, so this is an attempt that
# happened: it is recorded, and the release rolled away from is abandoned so a
# poll does not undo the operator's decision.
check "rollback unhealthy: the switch is recorded as a failed attempt" "v1.2.0 failed" "$(last_attempt)"
check "rollback unhealthy: the release rolled back from is abandoned" "yes" \
  "$(has_text "${ROOT}/var/lib/questboard-deploy/state/attempts" 'v1.3.0 abandoned')"
check "rollback unhealthy: the attempt is logged" "yes" "$(out_has 'outcome=failed tag=v1.2.0 reason=unhealthy')"
check "rollback unhealthy: sends no mail" "0" "$(mail_count)"
check "rollback unhealthy: current is the target that was switched to" "1.2.0" "$(current_version)"
export STUB_LATEST_TAG=v1.3.0
: > "${ROOT}/calls.log"
run_deploy poll
check "rollback unhealthy: a later poll does not reinstall the abandoned release" "" "$(calls)"

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
