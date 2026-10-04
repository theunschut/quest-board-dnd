#!/usr/bin/env bash
# Proves the release library: staging of an archive into a hardened release
# tree, manifest and JSON helpers, the migrator invocation through systemd-run,
# and the health wait. Needs no root, network, systemd or database: everything
# runs inside a temporary root with recording stand-ins first on PATH.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

export QUESTBOARD_DEPLOY_ROOT
QUESTBOARD_DEPLOY_ROOT="$(mktemp -d)"
trap 'rm -rf "${QUESTBOARD_DEPLOY_ROOT}"' EXIT
# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$QUESTBOARD_DEPLOY_ROOT"

STUB_DIR="${QUESTBOARD_DEPLOY_ROOT}/stubs"
mkdir -p "$STUB_DIR"
export STUB_SYSTEMD_RUN_LOG="${STUB_DIR}/systemd-run-args.log"

# A stand-in for systemd-run: records its arguments one per line, prints
# STUB_MIGRATOR_STDOUT and exits with STUB_MIGRATOR_EXIT.
cat > "${STUB_DIR}/systemd-run" <<'EOF'
#!/bin/sh
for arg in "$@"; do
  printf '%s\n' "$arg" >> "$STUB_SYSTEMD_RUN_LOG"
done
printf '%s\n' "${STUB_MIGRATOR_STDOUT:-}"
exit "${STUB_MIGRATOR_EXIT:-0}"
EOF

# A stand-in for curl, health-check flavour: honours --output, --dump-header
# and --write-out from the STUB_HEALTH_* variables.
cat > "${STUB_DIR}/curl" <<'EOF'
#!/bin/sh
out=""
hdr=""
prev=""
for arg in "$@"; do
  if [ "$prev" = "--output" ]; then out="$arg"; fi
  if [ "$prev" = "--dump-header" ]; then hdr="$arg"; fi
  prev="$arg"
done
if [ -n "$out" ]; then printf '%s' "${STUB_HEALTH_BODY:-}" > "$out"; fi
if [ -n "$hdr" ]; then
  {
    printf 'HTTP/1.1 %s\r\n' "${STUB_HEALTH_CODE:-200}"
    printf 'Content-Type: text/plain\r\n'
    if [ -n "${STUB_HEALTH_VERSION:-}" ]; then
      printf '%s: %s\r\n' "${STUB_HEALTH_HEADER_NAME:-X-QuestBoard-Version}" "$STUB_HEALTH_VERSION"
    fi
    printf '\r\n'
  } > "$hdr"
fi
printf '%s' "${STUB_HEALTH_CODE:-200}"
exit "${STUB_HEALTH_EXIT:-0}"
EOF

# A stand-in for df: prints the header line and STUB_DF_AVAIL (bytes).
cat > "${STUB_DIR}/df" <<'EOF'
#!/bin/sh
printf 'Avail\n%s\n' "${STUB_DF_AVAIL:-1000000000000}"
EOF
chmod +x "${STUB_DIR}/systemd-run" "${STUB_DIR}/curl" "${STUB_DIR}/df"
export PATH="${STUB_DIR}:${PATH}"

# shellcheck source=deploy/lib/common.sh
source "${REPO_ROOT}/deploy/lib/common.sh"
# shellcheck source=deploy/lib/release.sh
source "${REPO_ROOT}/deploy/lib/release.sh"

FAILURES=0

check() {
  local description="$1"
  local expected="$2"
  local actual="$3"
  if [ "$actual" = "$expected" ]; then
    printf 'PASS: %s\n' "$description"
  else
    printf 'FAIL: %s (expected [%s], got [%s])\n' "$description" "$expected" "$actual"
    FAILURES=$((FAILURES + 1))
  fi
}

# Runs a command and prints only its exit status.
status_of() {
  local rc=0
  "$@" >/dev/null 2>&1 || rc=$?
  printf '%s' "$rc"
}

WORK="${QUESTBOARD_DEPLOY_ROOT}/work"
mkdir -p "$WORK"

# --- archive fixtures -----------------------------------------------------

# Builds an archive of a complete release tree in ZIPS_DIR/NAME.zip. Optional
# settings come from the environment: FIX_VERSION (manifest version),
# FIX_HEADER (healthVersionHeader value), FIX_OMIT (a relative path to leave
# out) and FIX_SYMLINK (set to add a symlink entry).
ZIPS_DIR="${WORK}/zips"
mkdir -p "$ZIPS_DIR"

build_zip() {
  local name="$1" version="${FIX_VERSION:-1.2.3}" header="${FIX_HEADER:-true}"
  local tree="${WORK}/tree-${name}"
  rm -rf "$tree"
  mkdir -p "${tree}/app/wwwroot" "${tree}/migrator" "${tree}/deploy/bin"
  printf 'service' > "${tree}/app/QuestBoard.Service.dll"
  printf 'asset' > "${tree}/app/wwwroot/site.js"
  printf 'migrator' > "${tree}/migrator/QuestBoard.Migrator.dll"
  printf '#!/bin/sh\n' > "${tree}/deploy/bin/questboard-deploy"
  chmod 755 "${tree}/deploy/bin/questboard-deploy"
  chmod 666 "${tree}/app/wwwroot/site.js"
  printf '{"version":"%s","commit":"0123456789abcdef0123456789abcdef01234567","healthVersionHeader":%s}\n' \
    "$version" "$header" > "${tree}/release-manifest.json"
  if [ -n "${FIX_OMIT:-}" ]; then
    rm -f "${tree}/${FIX_OMIT}"
  fi
  if [ -n "${FIX_SYMLINK:-}" ]; then
    ln -s /etc/passwd "${tree}/app/link"
  fi
  rm -f "${ZIPS_DIR}/${name}.zip"
  ( cd "$tree" && zip -q -r --symlinks "${ZIPS_DIR}/${name}.zip" . )
}

# Builds an archive with raw entry names (for names zip itself would refuse).
build_raw_zip() {
  local name="$1" entry="$2"
  rm -f "${ZIPS_DIR}/${name}.zip"
  python3 - "${ZIPS_DIR}/${name}.zip" "$entry" <<'PY'
import sys
import zipfile

with zipfile.ZipFile(sys.argv[1], "w") as archive:
    # A complete, otherwise valid release, so only the odd entry name can cause
    # a refusal.
    archive.writestr("app/QuestBoard.Service.dll", "service")
    archive.writestr("migrator/QuestBoard.Migrator.dll", "migrator")
    archive.writestr("deploy/bin/questboard-deploy", "#!/bin/sh\n")
    archive.writestr("release-manifest.json", '{"version":"1.2.3","healthVersionHeader":true}')
    archive.writestr(sys.argv[2], "payload")
PY
}

INSTALL_ROOT="${WORK}/install"
RELEASES="${INSTALL_ROOT}/releases"

reset_install() {
  rm -rf "$INSTALL_ROOT"
  mkdir -p "$RELEASES"
}

staging_dirs() {
  find "$RELEASES" -maxdepth 1 -name '.staging-*' | wc -l | tr -d ' '
}

stage_status() {
  local rc=0
  ( questboard_stage_release "$@" ) >/dev/null 2>&1 || rc=$?
  printf '%s' "$rc"
}

# --- questboard_stage_release ---------------------------------------------

reset_install
unset STUB_DF_AVAIL
FIX_VERSION=1.2.3 build_zip valid
check "stage_release accepts a valid archive" "0" "$(stage_status "${ZIPS_DIR}/valid.zip" "$RELEASES" 1.2.3)"
for path in app/QuestBoard.Service.dll app/wwwroot/site.js migrator/QuestBoard.Migrator.dll deploy/bin/questboard-deploy release-manifest.json; do
  check "staged release contains ${path}" "yes" "$([ -f "${RELEASES}/1.2.3/${path}" ] && echo yes || echo no)"
done
check "no staging directory is left after success" "0" "$(staging_dirs)"
check "no staged file is group or other writable" "0" \
  "$(find "${RELEASES}/1.2.3" -perm /022 -not -type l | wc -l | tr -d ' ')"
check "staged directories are 755" "0" \
  "$(find "${RELEASES}/1.2.3" -type d -not -perm 755 | wc -l | tr -d ' ')"
check "staged files stay readable by everyone" "0" \
  "$(find "${RELEASES}/1.2.3" -type f -not -perm /004 | wc -l | tr -d ' ')"
check "the staged entry point stays executable" "yes" \
  "$([ -x "${RELEASES}/1.2.3/deploy/bin/questboard-deploy" ] && echo yes || echo no)"

assert_refused_clean() {
  local description="$1" zip="$2" version="$3" expected="$4"
  reset_install
  check "${description} returns ${expected}" "$expected" "$(stage_status "$zip" "$RELEASES" "$version")"
  check "${description} leaves no release directory" "no" "$([ -e "${RELEASES}/${version}" ] && echo yes || echo no)"
  check "${description} leaves no staging directory" "0" "$(staging_dirs)"
}

FIX_VERSION=1.2.3 FIX_SYMLINK=1 build_zip symlink
assert_refused_clean "a symlink entry" "${ZIPS_DIR}/symlink.zip" 1.2.3 3

build_raw_zip parent "../evil"
assert_refused_clean "a parent-directory entry" "${ZIPS_DIR}/parent.zip" 1.2.3 3

build_raw_zip nested-parent "app/../../evil"
assert_refused_clean "a nested parent-directory entry" "${ZIPS_DIR}/nested-parent.zip" 1.2.3 3

build_raw_zip absolute "/tmp/evil"
assert_refused_clean "an absolute entry" "${ZIPS_DIR}/absolute.zip" 1.2.3 3

build_raw_zip backslash 'app\evil'
assert_refused_clean "a backslash entry" "${ZIPS_DIR}/backslash.zip" 1.2.3 3

build_raw_zip harmless "app/extra.txt"
reset_install
check "the raw archive fixture is accepted with a harmless extra entry" "0" \
  "$(stage_status "${ZIPS_DIR}/harmless.zip" "$RELEASES" 1.2.3)"

FIX_VERSION=1.2.4 build_zip wrong-version
assert_refused_clean "a manifest version different from the tag" "${ZIPS_DIR}/wrong-version.zip" 1.2.3 3

FIX_VERSION=1.2.3 FIX_HEADER=false build_zip no-header
assert_refused_clean "a manifest without the version header" "${ZIPS_DIR}/no-header.zip" 1.2.3 3

FIX_VERSION=1.2.3 FIX_OMIT=migrator/QuestBoard.Migrator.dll build_zip no-migrator
assert_refused_clean "an archive without the migrator" "${ZIPS_DIR}/no-migrator.zip" 1.2.3 3

FIX_VERSION=1.2.3 FIX_OMIT=deploy/bin/questboard-deploy build_zip no-installer
assert_refused_clean "an archive without the installer entry point" "${ZIPS_DIR}/no-installer.zip" 1.2.3 3

FIX_VERSION=1.2.3 FIX_OMIT=app/QuestBoard.Service.dll build_zip no-app
assert_refused_clean "an archive without the application" "${ZIPS_DIR}/no-app.zip" 1.2.3 3

printf 'this is not a zip' > "${ZIPS_DIR}/garbage.zip"
assert_refused_clean "a file that is not an archive" "${ZIPS_DIR}/garbage.zip" 1.2.3 3

export STUB_DF_AVAIL=10
assert_refused_clean "too little free disk space" "${ZIPS_DIR}/valid.zip" 1.2.3 4
unset STUB_DF_AVAIL

# A leftover, inactive release directory is replaced.
reset_install
mkdir -p "${RELEASES}/1.2.3"
printf 'old' > "${RELEASES}/1.2.3/stale-marker"
mkdir -p "${RELEASES}/.staging-1.2.3"
printf 'old' > "${RELEASES}/.staging-1.2.3/stale-marker"
check "stage_release replaces a leftover inactive release" "0" "$(stage_status "${ZIPS_DIR}/valid.zip" "$RELEASES" 1.2.3)"
check "the leftover release content is gone" "no" "$([ -e "${RELEASES}/1.2.3/stale-marker" ] && echo yes || echo no)"
check "the new release content is present" "yes" "$([ -f "${RELEASES}/1.2.3/release-manifest.json" ] && echo yes || echo no)"
check "the leftover staging directory is gone" "0" "$(staging_dirs)"

# The active release is never replaced.
reset_install
mkdir -p "${RELEASES}/1.2.3"
printf 'live' > "${RELEASES}/1.2.3/live-marker"
ln -s "${RELEASES}/1.2.3" "${INSTALL_ROOT}/current"
check "stage_release dies when the version is the active release" "1" "$(stage_status "${ZIPS_DIR}/valid.zip" "$RELEASES" 1.2.3)"
check "the active release is untouched" "yes" "$([ -f "${RELEASES}/1.2.3/live-marker" ] && echo yes || echo no)"
check "no staging directory is left when the active release is refused" "0" "$(staging_dirs)"

# Another version can be staged while a different release is active.
FIX_VERSION=1.2.4 build_zip next
check "stage_release stages a different version beside the active one" "0" "$(stage_status "${ZIPS_DIR}/next.zip" "$RELEASES" 1.2.4)"
check "the active release is still intact after staging another" "yes" "$([ -f "${RELEASES}/1.2.3/live-marker" ] && echo yes || echo no)"

check "stage_release refuses an invalid version string" "1" "$(stage_status "${ZIPS_DIR}/valid.zip" "$RELEASES" 'v1.2.3')"

# --- questboard_manifest_get ----------------------------------------------

MANIFEST_DIR="${WORK}/manifest-release"
mkdir -p "$MANIFEST_DIR"
printf '{"version":"1.2.3","commit":"abc","healthVersionHeader":true,"adopted":false}\n' > "${MANIFEST_DIR}/release-manifest.json"
check "manifest_get reads the version" "1.2.3" "$(questboard_manifest_get "$MANIFEST_DIR" version)"
check "manifest_get prints a true boolean as true" "true" "$(questboard_manifest_get "$MANIFEST_DIR" healthVersionHeader)"
check "manifest_get prints a false boolean as false" "false" "$(questboard_manifest_get "$MANIFEST_DIR" adopted)"
check "manifest_get returns 1 for a missing key" "1" "$(status_of questboard_manifest_get "$MANIFEST_DIR" nothing)"
check "manifest_get returns 1 for an unsafe key" "1" "$(status_of questboard_manifest_get "$MANIFEST_DIR" 'a;b')"
check "manifest_get returns 1 without a manifest" "1" "$(status_of questboard_manifest_get "${WORK}/nowhere" version)"
printf 'not json' > "${MANIFEST_DIR}/release-manifest.json"
check "manifest_get returns 1 for an unreadable manifest" "1" "$(status_of questboard_manifest_get "$MANIFEST_DIR" version)"

# --- questboard_release_has_migrator ---------------------------------------

check "release_has_migrator is true for a release with one" "0" \
  "$(status_of questboard_release_has_migrator "${RELEASES}/1.2.4")"
check "release_has_migrator is false for a release without one" "1" \
  "$(status_of questboard_release_has_migrator "$MANIFEST_DIR")"

# --- JSON helpers ---------------------------------------------------------

JSON='{"state":"ok","pending":["a","b","c"],"canBackup":true,"count":4,"nothing":null,"nested":{"x":1}}'
check "json_get reads a string" "ok" "$(questboard_json_get "$JSON" state)"
check "json_get prints a boolean as true" "true" "$(questboard_json_get "$JSON" canBackup)"
check "json_get reads a number" "4" "$(questboard_json_get "$JSON" count)"
check "json_get prints nothing for a missing key" "" "$(questboard_json_get "$JSON" absent)"
check "json_get prints nothing for a null key" "" "$(questboard_json_get "$JSON" nothing)"
check "json_get returns 1 for invalid JSON" "1" "$(status_of questboard_json_get 'not json' state)"
check "json_get returns 1 for a JSON array" "1" "$(status_of questboard_json_get '[1,2]' state)"
check "json_list_length counts a list" "3" "$(questboard_json_list_length "$JSON" pending)"
check "json_list_length counts a missing key as empty" "0" "$(questboard_json_list_length "$JSON" absent)"
check "json_list_length returns 1 for a non-list value" "1" "$(status_of questboard_json_list_length "$JSON" state)"
check "json_list_length returns 1 for invalid JSON" "1" "$(status_of questboard_json_list_length 'not json' pending)"

PWNED="${WORK}/PWNED"
questboard_json_get "{\"a\":\"\$(touch ${PWNED})\"}" a >/dev/null
check "json_get never evaluates the values it reads" "absent" "$([ -e "$PWNED" ] && echo present || echo absent)"

# --- questboard_run_migrator ----------------------------------------------

RELEASE_DIR="${RELEASES}/1.2.4"
ENV_FILE="${WORK}/etc-questboard-env"
printf 'ConnectionStrings__DefaultConnection=Server=db;Password=TopSecret!\n' > "$ENV_FILE"

migrator_args() {
  paste -sd'\n' "$STUB_SYSTEMD_RUN_LOG"
}

EXPECTED_BASE="--quiet
--pipe
--wait
--collect
--uid=questboard
--gid=questboard
--working-directory=${RELEASE_DIR}/migrator
--property=EnvironmentFile=${ENV_FILE}
--property=NoNewPrivileges=yes
--property=PrivateTmp=yes
--property=ProtectSystem=strict
--property=ProtectHome=yes
--property=ProtectKernelTunables=yes
--property=ProtectKernelModules=yes
--property=ProtectControlGroups=yes
--property=RestrictNamespaces=yes
--property=LockPersonality=yes
--property=CapabilityBoundingSet=
--property=RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6
--property=RuntimeMaxSec=1800
/usr/bin/dotnet
${RELEASE_DIR}/migrator/QuestBoard.Migrator.dll"

rm -f "$STUB_SYSTEMD_RUN_LOG"
export STUB_MIGRATOR_EXIT=2 STUB_MIGRATOR_STDOUT='{"state":"databaseAhead"}'
mig_rc=0
mig_out="$(questboard_run_migrator "$RELEASE_DIR" "$ENV_FILE" status)" || mig_rc=$?
check "run_migrator propagates the migrator exit code" "2" "$mig_rc"
check "run_migrator passes the migrator stdout through" '{"state":"databaseAhead"}' "$mig_out"
check "run_migrator passes exactly the documented argument list for status" \
  "${EXPECTED_BASE}
status" "$(migrator_args)"

rm -f "$STUB_SYSTEMD_RUN_LOG"
export STUB_MIGRATOR_EXIT=0
questboard_run_migrator "$RELEASE_DIR" "$ENV_FILE" apply >/dev/null
check "run_migrator passes the apply subcommand last" "apply" "$(tail -n 1 "$STUB_SYSTEMD_RUN_LOG")"

rm -f "$STUB_SYSTEMD_RUN_LOG"
questboard_run_migrator "$RELEASE_DIR" "$ENV_FILE" backup --label v1.2.4 >/dev/null
check "run_migrator passes the backup subcommand with its label" \
  "${EXPECTED_BASE}
backup
--label
v1.2.4" "$(migrator_args)"
check "the secret file content never appears on any argument" "0" \
  "$(grep -c 'TopSecret' "$STUB_SYSTEMD_RUN_LOG" || true)"

refused_migrator() {
  rm -f "$STUB_SYSTEMD_RUN_LOG"
  local rc=0
  questboard_run_migrator "$@" >/dev/null 2>&1 || rc=$?
  printf '%s:%s' "$rc" "$([ -e "$STUB_SYSTEMD_RUN_LOG" ] && echo called || echo not-called)"
}

check "run_migrator refuses an unknown subcommand" "64:not-called" "$(refused_migrator "$RELEASE_DIR" "$ENV_FILE" restore)"
check "run_migrator refuses a subcommand with extra words" "64:not-called" "$(refused_migrator "$RELEASE_DIR" "$ENV_FILE" 'status; rm -rf /')"
check "run_migrator refuses a backup without a label" "64:not-called" "$(refused_migrator "$RELEASE_DIR" "$ENV_FILE" backup)"
check "run_migrator refuses a label with a space" "64:not-called" "$(refused_migrator "$RELEASE_DIR" "$ENV_FILE" backup --label 'a b')"
check "run_migrator refuses a label with a slash" "64:not-called" "$(refused_migrator "$RELEASE_DIR" "$ENV_FILE" backup --label '../x')"
check "run_migrator refuses an over-long label" "64:not-called" \
  "$(refused_migrator "$RELEASE_DIR" "$ENV_FILE" backup --label "$(printf 'a%.0s' $(seq 1 65))")"
check "run_migrator refuses a label on status" "64:not-called" "$(refused_migrator "$RELEASE_DIR" "$ENV_FILE" status --label x)"
check "run_migrator refuses a relative environment file path" "64:not-called" "$(refused_migrator "$RELEASE_DIR" env status)"
check "run_migrator refuses a release path with a space" "64:not-called" "$(refused_migrator '/opt/a b' "$ENV_FILE" status)"

# --- questboard_wait_for_health -------------------------------------------

HEALTH_URL="http://127.0.0.1:5000/health"

health_status() {
  local rc=0
  ( questboard_wait_for_health "$@" ) >/dev/null 2>&1 || rc=$?
  printf '%s' "$rc"
}

clear_health() {
  unset STUB_HEALTH_CODE STUB_HEALTH_BODY STUB_HEALTH_VERSION STUB_HEALTH_HEADER_NAME STUB_HEALTH_EXIT
}

clear_health
export STUB_HEALTH_CODE=200 STUB_HEALTH_BODY=Healthy STUB_HEALTH_VERSION=1.2.3
check "health: 200, Healthy and a matching header passes" "0" "$(health_status "$HEALTH_URL" 1.2.3 1 1)"

export STUB_HEALTH_BODY=$'Healthy\n'
check "health: a trailing newline in the body is tolerated" "0" "$(health_status "$HEALTH_URL" 1.2.3 1 1)"

export STUB_HEALTH_BODY=Degraded
degraded_log="$(questboard_wait_for_health "$HEALTH_URL" 1.2.3 1 1 2>&1 >/dev/null)" || true
check "health: 200, Degraded and a matching header passes" "0" "$(health_status "$HEALTH_URL" 1.2.3 1 1)"
check "health: a Degraded pass logs exactly one warning line" "1" "$(printf '%s\n' "$degraded_log" | grep -c 'Degraded')"

export STUB_HEALTH_BODY=Healthy STUB_HEALTH_VERSION=9.9.9
check "health: a different version header never passes" "1" "$(health_status "$HEALTH_URL" 1.2.3 1 1)"

export STUB_HEALTH_HEADER_NAME=x-questboard-version STUB_HEALTH_VERSION=1.2.3
check "health: the header name is matched case-insensitively" "0" "$(health_status "$HEALTH_URL" 1.2.3 1 1)"

export STUB_HEALTH_HEADER_NAME=X-QuestBoard-Version STUB_HEALTH_VERSION=9.9.9
check "health: with the header not required a different version still passes" "0" "$(health_status "$HEALTH_URL" 1.2.3 1 0)"

unset STUB_HEALTH_VERSION
check "health: a missing header never passes when required" "1" "$(health_status "$HEALTH_URL" 1.2.3 1 1)"
check "health: a missing header passes when not required" "0" "$(health_status "$HEALTH_URL" 1.2.3 1 0)"

export STUB_HEALTH_CODE=503 STUB_HEALTH_BODY=Unhealthy STUB_HEALTH_VERSION=1.2.3
check "health: 503 never passes" "1" "$(health_status "$HEALTH_URL" 1.2.3 1 1)"

export STUB_HEALTH_CODE=200 STUB_HEALTH_BODY=Unhealthy
check "health: a 200 with an Unhealthy body never passes" "1" "$(health_status "$HEALTH_URL" 1.2.3 1 1)"

export STUB_HEALTH_CODE=000 STUB_HEALTH_BODY='' STUB_HEALTH_EXIT=7
check "health: a refused connection never passes" "1" "$(health_status "$HEALTH_URL" 1.2.3 1 1)"
unset STUB_HEALTH_EXIT

export STUB_HEALTH_CODE=503 STUB_HEALTH_BODY=Unhealthy
started="$(date +%s)"
health_status "$HEALTH_URL" 1.2.3 3 1 >/dev/null
elapsed=$(( $(date +%s) - started ))
check "health: a timeout of 3 returns within roughly 6 seconds" "yes" \
  "$([ "$elapsed" -ge 3 ] && [ "$elapsed" -le 6 ] && echo yes || echo "no (${elapsed}s)")"

check "health: a non-numeric timeout is a failure" "1" "$(health_status "$HEALTH_URL" 1.2.3 soon 1)"

check "host commands were never called" "" "$(host_guard_calls)"

if [ "$FAILURES" -gt 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES"
  exit 1
fi
printf 'all release checks passed\n'
