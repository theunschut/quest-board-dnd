#!/usr/bin/env bash
# Proves the setup library: how the signing key for the gh package source is
# judged, how the running version is read from an assembly, the install of gh,
# the install of the installer's own files, and the adoption of a flat install
# into the versioned layout including interruption, repetition and the refusal
# paths. Needs no root, network, systemd or database: every host command is a
# recording stand-in on PATH and every path lives under a temporary root.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

export QUESTBOARD_DEPLOY_ROOT
QUESTBOARD_DEPLOY_ROOT="$(mktemp -d)"
trap 'rm -rf "${QUESTBOARD_DEPLOY_ROOT}"' EXIT
# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$QUESTBOARD_DEPLOY_ROOT"

# --- recording stand-ins for every host command ----------------------------

export STUB_DIR="${QUESTBOARD_DEPLOY_ROOT}/stubs"
mkdir -p "$STUB_DIR"
export STUB_CALLS="${STUB_DIR}/calls.log"
: > "$STUB_CALLS"

cat > "${STUB_DIR}/systemctl" <<'EOF'
#!/bin/sh
printf 'systemctl %s\n' "$*" >> "$STUB_CALLS"
exit 0
EOF
cat > "${STUB_DIR}/systemd-run" <<'EOF'
#!/bin/sh
printf 'systemd-run %s\n' "$*" >> "$STUB_CALLS"
exit 0
EOF
# On "install -y gh" the stand-in makes a gh appear, reporting STUB_GH_AFTER.
cat > "${STUB_DIR}/apt-get" <<'EOF'
#!/bin/sh
printf 'apt-get %s\n' "$*" >> "$STUB_CALLS"
case "$*" in
  *"install -y gh"*) printf '%s\n' "${STUB_GH_AFTER:-2.102.0}" > "${STUB_DIR}/gh-version" ;;
esac
exit 0
EOF
cat > "${STUB_DIR}/gpg" <<'EOF'
#!/bin/sh
printf 'gpg %s\n' "$*" >> "$STUB_CALLS"
cat "${STUB_DIR}/gpg-colons"
EOF
cat > "${STUB_DIR}/dpkg" <<'EOF'
#!/bin/sh
printf 'amd64\n'
EOF
# gh is absent until a gh-version file exists.
cat > "${STUB_DIR}/gh" <<'EOF'
#!/bin/sh
[ -f "${STUB_DIR}/gh-version" ] || exit 127
v="$(cat "${STUB_DIR}/gh-version")"
printf 'gh version %s (2026-01-01)\nhttps://github.com/cli/cli/releases/tag/v%s\n' "$v" "$v"
EOF
# curl records its arguments and, for any --output, writes a healthy body.
cat > "${STUB_DIR}/curl" <<'EOF'
#!/bin/sh
printf 'curl %s\n' "$*" >> "$STUB_CALLS"
out=""
prev=""
for arg in "$@"; do
  if [ "$prev" = "--output" ]; then out="$arg"; fi
  prev="$arg"
done
if [ -n "$out" ]; then printf 'Healthy' > "$out"; fi
printf '200'
exit 0
EOF
chmod +x "${STUB_DIR}"/*
export PATH="${STUB_DIR}:${PATH}"

PIN=7F38BBB59D064DBCB3D84D725612B36462313325
OLD_KEY=2C6106201985B60E6C7AC87323F3D4EA75716059

write_pinned_colons() {
  cat > "${STUB_DIR}/gpg-colons" <<EOF
tru::1:1700000000:0:3:1:5
pub:-:4096:1:5612B36462313325:1700000000:::-:::scESCA:::::::
fpr:::::::::${PIN}:
uid:-::::1700000000::AAAA::GitHub CLI <opensource@github.com>:::::::::
sub:-:4096:1:AAAAAAAAAAAAAAAA:1700000000::::::s::::::
fpr:::::::::0123456789ABCDEF0123456789ABCDEF01234567:
EOF
}

# shellcheck source=deploy/lib/common.sh
source "${REPO_ROOT}/deploy/lib/common.sh"
# shellcheck source=deploy/lib/deploy.sh
source "${REPO_ROOT}/deploy/lib/deploy.sh"
# shellcheck source=deploy/lib/release.sh
source "${REPO_ROOT}/deploy/lib/release.sh"
# shellcheck source=deploy/lib/setup.sh
source "${REPO_ROOT}/deploy/lib/setup.sh"

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

# Prints 1 when the recorded host calls contain a line matching the pattern.
called() {
  if grep -qE -- "$1" "$STUB_CALLS"; then printf 1; else printf 0; fi
}

# Prints the line number of the first recorded call matching the pattern, or 0.
call_line() {
  grep -nE -- "$1" "$STUB_CALLS" | head -n 1 | cut -d: -f1 || true
}

reset_calls() {
  : > "$STUB_CALLS"
}

WORK="${QUESTBOARD_DEPLOY_ROOT}/work"
mkdir -p "$WORK"

# --- active key fingerprints ------------------------------------------------

COLONS_MIXED="tru::1:1700000000:0:3:1:5
pub:e:4096:1:23F3D4EA75716059:1500000000:1600000000::-:::scESCA:::::::
fpr:::::::::${OLD_KEY}:
sub:e:4096:1:BBBBBBBBBBBBBBBB:1500000000:1600000000:::::s::::::
fpr:::::::::1111111111111111111111111111111111111111:
pub:-:4096:1:5612B36462313325:1700000000:::-:::scESCA:::::::
fpr:::::::::${PIN}:
sub:-:4096:1:AAAAAAAAAAAAAAAA:1700000000::::::s::::::
fpr:::::::::0123456789ABCDEF0123456789ABCDEF01234567:"

check "an expired primary key is skipped and the valid one printed" "$PIN" \
  "$(questboard_setup_active_key_fingerprints "$COLONS_MIXED")"

COLONS_REVOKED="pub:r:4096:1:5612B36462313325:1700000000:::-:::scESCA:::::::
fpr:::::::::${PIN}:"
check "a revoked primary key prints nothing" "" \
  "$(questboard_setup_active_key_fingerprints "$COLONS_REVOKED")"
check "empty input prints nothing" "" "$(questboard_setup_active_key_fingerprints "")"

# --- running version from an assembly ---------------------------------------

HEX32="$(printf 'a%.0s' $(seq 1 32))"
HEX40="0f947c3b${HEX32}"

# Writes the attribute record the compiler emits for an informational version:
# the 01 00 prolog, a one-byte length, the string, then two zero bytes.
version_record() {
  local text="$1"
  printf '\001\000'
  printf "\\$(printf '%03o' "${#text}")"
  printf '%s\000\000' "$text"
}

make_dll() {
  # make_dll FILE VERSION: embeds the record for "VERSION+<40 hex>".
  { printf 'MZ\000\000header'; version_record "$2+${HEX40}"; printf 'trailer'; } > "$1"
}

DLL_ONE="${WORK}/one.dll"
make_dll "$DLL_ONE" 5.3.3
check "the version is read from the informational version" "5.3.3" \
  "$(questboard_setup_detect_flat_version "$DLL_ONE")"

# A 46-character version string has the length byte 0x2E, which is "."; a
# 49-character one has 0x31, which is "1". Both must still be read.
DLL_DOT="${WORK}/dot.dll"
make_dll "$DLL_DOT" 5.3.3
check "the fixture really has the dot length byte" "1" \
  "$(od -An -tx1 -v "$DLL_DOT" | tr -s ' \n' ' ' | grep -c ' 01 00 2e ')"
check "a length byte that reads as a dot does not hide the version" "5.3.3" \
  "$(questboard_setup_detect_flat_version "$DLL_DOT")"
DLL_DIGIT="${WORK}/digit.dll"
make_dll "$DLL_DIGIT" 10.20.30
check "a length byte that reads as a digit does not hide the version" "10.20.30" \
  "$(questboard_setup_detect_flat_version "$DLL_DIGIT")"

DLL_SHORT="${WORK}/short.dll"
{ printf 'MZ\000\000'; version_record "5.3.3+0f947c3"; } > "$DLL_SHORT"
check "a short commit hash is read" "5.3.3" "$(questboard_setup_detect_flat_version "$DLL_SHORT")"

DLL_LEN="${WORK}/len.dll"
{ printf 'MZ\000\000\001\000\005'; printf '5.3.3+%s\000\000' "$HEX40"; } > "$DLL_LEN"
check "a record whose length byte disagrees is ignored" "1" "$(status_of questboard_setup_detect_flat_version "$DLL_LEN")"

DLL_DEV="${WORK}/dev.dll"
{ printf 'MZ\000\000'; version_record "0.0.0-dev+${HEX40}"; } > "$DLL_DEV"
check "a pre-release version is refused" "1" "$(status_of questboard_setup_detect_flat_version "$DLL_DEV")"

DLL_TWO="${WORK}/two.dll"
{
  printf 'MZ\000\000first'; version_record "5.3.3+${HEX40}"
  printf 'second'; version_record "5.3.4+${HEX40}"
} > "$DLL_TWO"
check "two different versions are refused" "1" "$(status_of questboard_setup_detect_flat_version "$DLL_TWO")"

DLL_SAME="${WORK}/same.dll"
{ printf 'MZ\000\000'; version_record "5.3.3+${HEX40}"; printf 'x'; version_record "5.3.3+${HEX40}"; } > "$DLL_SAME"
check "the same version twice is read once" "5.3.3" "$(questboard_setup_detect_flat_version "$DLL_SAME")"

DLL_NONE="${WORK}/none.dll"
printf 'MZ\000\000nothing to see 5.3.3 here' > "$DLL_NONE"
check "no informational version is refused" "1" "$(status_of questboard_setup_detect_flat_version "$DLL_NONE")"

DLL_BARE="${WORK}/bare.dll"
printf 'MZ\000\000header 5.3.3+%s\000\000trailer' "$HEX40" > "$DLL_BARE"
check "a version outside an attribute record is ignored" "1" "$(status_of questboard_setup_detect_flat_version "$DLL_BARE")"
check "a missing file is refused" "1" "$(status_of questboard_setup_detect_flat_version "${WORK}/absent.dll")"

# --- fixtures for the install and adoption scenarios ------------------------

# The release the operator runs setup from: copies of the real libraries, units
# and example, a stand-in dispatcher (setup only copies it) and a manifest.
SRC="${WORK}/source-release"
mkdir -p "${SRC}/deploy/bin"
cp -r "${REPO_ROOT}/deploy/lib" "${SRC}/deploy/lib"
cp -r "${REPO_ROOT}/deploy/systemd" "${SRC}/deploy/systemd"
cp "${REPO_ROOT}/deploy/deploy.conf.example" "${SRC}/deploy/deploy.conf.example"
printf '#!/bin/sh\n# stand-in dispatcher\n' > "${SRC}/deploy/bin/questboard-deploy"
chmod 755 "${SRC}/deploy/bin/questboard-deploy"
printf '{"version":"9.9.9","commit":"","healthVersionHeader":true}\n' > "${SRC}/release-manifest.json"

ROOT_COUNT=0

# Creates a fresh relocated root and points the dispatcher globals at it.
new_root() {
  ROOT_COUNT=$((ROOT_COUNT + 1))
  DEPLOY_ROOT="${WORK}/root-${ROOT_COUNT}"
  mkdir -p "${DEPLOY_ROOT}/etc/systemd/system" "${DEPLOY_ROOT}/usr/bin" "${DEPLOY_ROOT}/opt/questboard"
  printf '[Service]\nUser=questboard\n' > "${DEPLOY_ROOT}/etc/systemd/system/questboard.service"
  printf '#!/bin/sh\n' > "${DEPLOY_ROOT}/usr/bin/dotnet"
  chmod 755 "${DEPLOY_ROOT}/usr/bin/dotnet"
  OPT="${DEPLOY_ROOT}/opt/questboard"
  RELEASES_DIR="${OPT}/releases"
  CURRENT_LINK="${OPT}/current"
  STATE_DIR="${DEPLOY_ROOT}/var/lib/questboard-deploy/state"
  DOWNLOAD_DIR="${DEPLOY_ROOT}/var/lib/questboard-deploy/downloads"
  CONF_PATH="${DEPLOY_ROOT}/etc/questboard/deploy.conf"
  APP_SERVICE=questboard.service
  rm -f "${STUB_DIR}/gh-version"
  printf '2.102.0\n' > "${STUB_DIR}/gh-version"
  write_pinned_colons
  reset_calls
}

# Fills OPT with a flat install reporting version 5.3.3.
make_flat_install() {
  make_dll "${OPT}/QuestBoard.Service.dll" 5.3.3
  printf 'domain' > "${OPT}/QuestBoard.Domain.dll"
  printf '{}' > "${OPT}/appsettings.json"
  printf 'hidden' > "${OPT}/.hidden-entry"
  mkdir -p "${OPT}/wwwroot/css" "${OPT}/runtimes/linux"
  printf 'css' > "${OPT}/wwwroot/css/site.css"
  printf 'native' > "${OPT}/runtimes/linux/lib.so"
}

# Prints the sorted top-level entry names of a directory, one per line.
entries_of() {
  find "$1" -mindepth 1 -maxdepth 1 -printf '%f\n' | sort
}

# Runs setup, capturing its output and status in SETUP_OUT and SETUP_RC. The
# command substitution is a subshell, so configuration loaded by one run never
# leaks into the next.
run_setup() {
  SETUP_RC=0
  SETUP_OUT="$(questboard_setup_main "$SRC" "$@" 2>&1)" || SETUP_RC=$?
}

# Prints the mode of a path.
mode_of() {
  stat -c '%a' "$1"
}

# --- gh install -------------------------------------------------------------

new_root
rm -f "${STUB_DIR}/gh-version"
rc=0
( questboard_setup_install_gh ) >/dev/null 2>&1 || rc=$?
check "gh install from the pinned key succeeds" "0" "$rc"
check "gh install runs apt-get update" "1" "$(called '^apt-get update')"
check "gh install runs apt-get install -y gh" "1" "$(called '^apt-get install -y gh')"
check "gh install writes a signed-by source line" "1" \
  "$(grep -c 'signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg' "${DEPLOY_ROOT}/etc/apt/sources.list.d/github-cli.list" || true)"
check "gh source line names the packages host and architecture" "deb [arch=amd64 signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" \
  "$(cat "${DEPLOY_ROOT}/etc/apt/sources.list.d/github-cli.list")"
check "gh keyring is installed 0644" "644" "$(mode_of "${DEPLOY_ROOT}/usr/share/keyrings/githubcli-archive-keyring.gpg")"
check "gh install never asks for jq" "0" "$(called 'jq')"

new_root
rm -f "${STUB_DIR}/gh-version"
cat > "${STUB_DIR}/gpg-colons" <<EOF
pub:-:4096:1:23F3D4EA75716059:1700000000:::-:::scESCA:::::::
fpr:::::::::${OLD_KEY}:
EOF
rc=0
( questboard_setup_install_gh ) >/dev/null 2>&1 || rc=$?
check "a different active key makes gh install die" "1" "$rc"
check "a different active key causes no apt-get install" "0" "$(called '^apt-get install')"
check "a different active key causes no apt-get update" "0" "$(called '^apt-get update')"
check "a different active key writes no source list" "0" \
  "$([ -e "${DEPLOY_ROOT}/etc/apt/sources.list.d/github-cli.list" ] && echo 1 || echo 0)"

new_root
cat > "${STUB_DIR}/gpg-colons" <<EOF
pub:e:4096:1:5612B36462313325:1600000000:1650000000::-:::scESCA:::::::
fpr:::::::::${PIN}:
EOF
rm -f "${STUB_DIR}/gh-version"
rc=0
( questboard_setup_install_gh ) >/dev/null 2>&1 || rc=$?
check "the pinned key is refused once it has expired" "1" "$rc"

new_root
rc=0
( questboard_setup_install_gh ) >/dev/null 2>&1 || rc=$?
check "an installed gh 2.102.0 is left alone" "0" "$rc"
check "an installed gh causes no apt-get call" "0" "$(called '^apt-get')"
check "an installed gh causes no download" "0" "$(called '^curl')"

new_root
printf '2.49.0\n' > "${STUB_DIR}/gh-version"
rc=0
( questboard_setup_install_gh ) >/dev/null 2>&1 || rc=$?
check "gh 2.49.0 meets the floor exactly" "0" "$rc"
check "gh at the floor causes no apt-get call" "0" "$(called '^apt-get')"

new_root
rm -f "${STUB_DIR}/gh-version"
rc=0
( export STUB_GH_AFTER=2.40.0; questboard_setup_install_gh ) >/dev/null 2>&1 || rc=$?
check "a gh older than 2.49.0 after install dies" "1" "$rc"

# --- setup: flat install, no confirmation -----------------------------------

new_root
make_flat_install
before_entries="$(entries_of "$OPT")"
run_setup
check "setup on a flat install without confirmation exits 2" "2" "$SETUP_RC"
check "the detected version is printed" "1" \
  "$(printf '%s' "$SETUP_OUT" | grep -c 'Running version detected: 5.3.3')"
check "the exact repeat command is printed" "1" \
  "$(printf '%s' "$SETUP_OUT" | grep -c 'questboard-deploy setup --confirm-adopt-version 5.3.3')"
check "the flat install is untouched" "$before_entries" "$(entries_of "$OPT")"
check "the app was not stopped" "0" "$(called '^systemctl stop')"
check "nothing else was called on the host" "0" "$(called '.')"
check "nothing was installed" "0" "$([ -e "${DEPLOY_ROOT}/usr/local/sbin/questboard-deploy" ] && echo 1 || echo 0)"

new_root
make_flat_install
before_entries="$(entries_of "$OPT")"
run_setup --confirm-adopt-version 5.3.4
check "a confirmation for another version exits 2" "2" "$SETUP_RC"
check "a wrong confirmation moves nothing" "$before_entries" "$(entries_of "$OPT")"
check "a wrong confirmation stops nothing" "0" "$(called '^systemctl stop')"

new_root
make_flat_install
run_setup --confirm-adopt-version not-a-version
check "a malformed confirmation is an error" "1" "$SETUP_RC"

# --- setup: flat install, confirmed -----------------------------------------

new_root
make_flat_install
flat_entries="$(entries_of "$OPT")"
run_setup --confirm-adopt-version 5.3.3
check "a confirmed adoption waits for the recipient (exit 3)" "3" "$SETUP_RC"
check "the app is stopped" "1" "$(called '^systemctl stop questboard.service')"
check "the app is stopped before it is started" "1" \
  "$([ "$(call_line '^systemctl stop questboard.service')" -lt "$(call_line '^systemctl start questboard.service')" ] && echo 1 || echo 0)"
check "every former entry is under the release app directory" "$flat_entries" \
  "$(entries_of "${RELEASES_DIR}/5.3.3/app")"
check "only releases and current remain at the top level" "$(printf 'current\nreleases')" "$(entries_of "$OPT")"
check "current points at the adopted release" "${RELEASES_DIR}/5.3.3" "$(readlink "$CURRENT_LINK")"
check "the adopted manifest records an unverified release" \
  "5.3.3|false|true|" \
  "$(python3 -c 'import json,sys; d=json.load(open(sys.argv[1])); print("|".join([d["version"],str(d["healthVersionHeader"]).lower(),str(d["adopted"]).lower(),d["commit"]]))' "${RELEASES_DIR}/5.3.3/release-manifest.json")"
check "the attempt is recorded as adopted" "1" "$(grep -c '^v5.3.3 adopted ' "${STATE_DIR}/attempts" || true)"
check "the adopted app is not writable by group or others" "0" \
  "$(find "${RELEASES_DIR}/5.3.3" -perm /022 ! -type l | wc -l | tr -d ' ')"
check "the opt directory is mode 755" "755" "$(mode_of "$OPT")"
check "the releases directory is mode 755" "755" "$(mode_of "$RELEASES_DIR")"
check "the installer is installed 0755" "755" "$(mode_of "${DEPLOY_ROOT}/usr/local/sbin/questboard-deploy")"
check "the libraries are installed 0644" "644" "$(mode_of "${DEPLOY_ROOT}/usr/local/lib/questboard-deploy/setup.sh")"
check "every shipped library is installed" "$(entries_of "${SRC}/deploy/lib")" \
  "$(entries_of "${DEPLOY_ROOT}/usr/local/lib/questboard-deploy")"
check "the poll service is installed 0644" "644" "$(mode_of "${DEPLOY_ROOT}/etc/systemd/system/questboard-deploy-poll.service")"
check "the poll timer is installed 0644" "644" "$(mode_of "${DEPLOY_ROOT}/etc/systemd/system/questboard-deploy-poll.timer")"
check "the drop-in is installed 0644" "644" "$(mode_of "${DEPLOY_ROOT}/etc/systemd/system/questboard.service.d/10-release-layout.conf")"
check "the base unit was not rewritten" "$(printf '[Service]\nUser=questboard')" \
  "$(cat "${DEPLOY_ROOT}/etc/systemd/system/questboard.service")"
check "deploy.conf is created with mode 600" "600" "$(mode_of "$CONF_PATH")"
check "state directory is mode 700" "700" "$(mode_of "$STATE_DIR")"
check "download directory is mode 700" "700" "$(mode_of "$DOWNLOAD_DIR")"
check "the directory holding both is mode 755" "755" "$(mode_of "$(dirname "$STATE_DIR")")"
check "systemd is reloaded" "1" "$(called '^systemctl daemon-reload')"
check "the app is started" "1" "$(called '^systemctl start questboard.service')"
check "health is checked on the configured URL" "1" "$(called 'curl .*http://127.0.0.1:5000/health')"
check "the poll timer is not enabled yet" "0" "$(called 'enable')"
check "the edit instruction is printed" "1" "$(printf '%s' "$SETUP_OUT" | grep -c 'Edit .*deploy.conf')"
check "the adopted app kept its hidden entry" "hidden" "$(cat "${RELEASES_DIR}/5.3.3/app/.hidden-entry")"

# --- setup: rerun after the recipient is edited -----------------------------

sed -i 's/^QUESTBOARD_NOTIFY_EMAIL=.*/QUESTBOARD_NOTIFY_EMAIL=ops@theunschut.com/' "$CONF_PATH"
conf_sum="$(cksum < "$CONF_PATH")"
releases_before="$(entries_of "$RELEASES_DIR")"
reset_calls
run_setup
check "a rerun with a real recipient exits 0" "0" "$SETUP_RC"
check "a rerun moves nothing" "$releases_before" "$(entries_of "$RELEASES_DIR")"
check "a rerun does not stop the app" "0" "$(called '^systemctl stop')"
check "a rerun does not restart the app" "0" "$(called '^systemctl (start|restart) questboard.service')"
check "a rerun enables the poll timer" "1" "$(called '^systemctl enable --now questboard-deploy-poll.timer')"
check "a rerun never overwrites deploy.conf" "$conf_sum" "$(cksum < "$CONF_PATH")"
check "a rerun prints the first install command" "1" \
  "$(printf '%s' "$SETUP_OUT" | grep -c 'questboard-deploy install vX.Y.Z')"

# A changed drop-in restarts the app and checks health again.
printf '# older drop-in\n' > "${DEPLOY_ROOT}/etc/systemd/system/questboard.service.d/10-release-layout.conf"
reset_calls
run_setup
check "a changed drop-in still exits 0" "0" "$SETUP_RC"
check "a changed drop-in restarts the app" "1" "$(called '^systemctl restart questboard.service')"
check "a changed drop-in is restored" "$(cat "${SRC}/deploy/systemd/questboard.service.d/10-release-layout.conf")" \
  "$(cat "${DEPLOY_ROOT}/etc/systemd/system/questboard.service.d/10-release-layout.conf")"

# --- setup: interrupted adoption --------------------------------------------

new_root
make_flat_install
flat_entries="$(entries_of "$OPT")"
mkdir -p "${RELEASES_DIR}/5.3.3/app"
mv "${OPT}/appsettings.json" "${OPT}/wwwroot" "${RELEASES_DIR}/5.3.3/app/"
run_setup --confirm-adopt-version 5.3.3
check "an interrupted adoption completes on rerun" "3" "$SETUP_RC"
check "the interrupted adoption ends with every entry under the app" "$flat_entries" \
  "$(entries_of "${RELEASES_DIR}/5.3.3/app")"
check "the interrupted adoption creates current" "${RELEASES_DIR}/5.3.3" "$(readlink "$CURRENT_LINK")"

# The assembly moves last, so a run cut off after it still resumes without
# asking again, because the earlier run was already confirmed.
new_root
make_flat_install
flat_entries="$(entries_of "$OPT")"
mkdir -p "${RELEASES_DIR}/5.3.3/app"
printf '{"version":"5.3.3","commit":"","healthVersionHeader":false,"adopted":true}\n' > "${RELEASES_DIR}/5.3.3/release-manifest.json"
for name in QuestBoard.Domain.dll appsettings.json .hidden-entry wwwroot runtimes QuestBoard.Service.dll; do
  mv "${OPT}/${name}" "${RELEASES_DIR}/5.3.3/app/"
done
run_setup
check "a run cut off after the last move resumes without asking" "3" "$SETUP_RC"
check "the resumed adoption creates current" "${RELEASES_DIR}/5.3.3" "$(readlink "$CURRENT_LINK")"
check "the resumed adoption keeps every entry" "$flat_entries" "$(entries_of "${RELEASES_DIR}/5.3.3/app")"
check "the resumed adoption starts the app" "1" "$(called '^systemctl start questboard.service')"

# --- setup: fresh host ------------------------------------------------------

new_root
run_setup
check "a fresh host exits 3 until deploy.conf is edited" "3" "$SETUP_RC"
check "a fresh host gets a releases directory" "1" "$([ -d "$RELEASES_DIR" ] && echo 1 || echo 0)"
check "a fresh host's opt directory holds only releases" "releases" "$(entries_of "$OPT")"
check "a fresh host prints the install command" "1" \
  "$(printf '%s' "$SETUP_OUT" | grep -c 'questboard-deploy install vX.Y.Z')"
check "a fresh host never stops, starts or restarts the app" "0" \
  "$(called '^systemctl (stop|start|restart) questboard.service')"
check "a fresh host does not enable the timer" "0" "$(called 'enable')"

# --- setup: refusal paths ---------------------------------------------------

new_root
make_flat_install
rm -f "${DEPLOY_ROOT}/etc/systemd/system/questboard.service"
run_setup --confirm-adopt-version 5.3.3
check "a missing base unit makes setup fail" "1" "$SETUP_RC"
check "a missing base unit stops nothing" "0" "$(called '^systemctl stop')"
check "a missing base unit installs nothing" "0" "$([ -e "${DEPLOY_ROOT}/usr/local/sbin/questboard-deploy" ] && echo 1 || echo 0)"
check "a missing base unit moves nothing" "1" "$([ -f "${OPT}/QuestBoard.Service.dll" ] && echo 1 || echo 0)"

new_root
rm -f "${DEPLOY_ROOT}/usr/bin/dotnet"
run_setup
check "a missing dotnet makes setup fail" "1" "$SETUP_RC"

new_root
SETUP_RC=0
SETUP_OUT="$(questboard_setup_main "${WORK}/not-a-release" 2>&1)" || SETUP_RC=$?
check "a source that is not a release is refused" "1" "$SETUP_RC"
check "the refusal says to use a verified release" "1" \
  "$(printf '%s' "$SETUP_OUT" | grep -c 'unpacked, verified release')"

new_root
run_setup --bogus
check "an unknown argument is refused" "1" "$SETUP_RC"

# --- setup: an existing configuration is never overwritten ------------------

new_root
make_dll "${DEPLOY_ROOT}/unused.dll" 1.0.0
mkdir -p "${RELEASES_DIR}/4.0.0/app" "$(dirname "$CONF_PATH")"
printf '{"version":"4.0.0","commit":"","healthVersionHeader":false,"adopted":true}\n' > "${RELEASES_DIR}/4.0.0/release-manifest.json"
printf 'dll' > "${RELEASES_DIR}/4.0.0/app/QuestBoard.Service.dll"
ln -s "${RELEASES_DIR}/4.0.0" "$CURRENT_LINK"
{
  printf 'QUESTBOARD_NOTIFY_EMAIL=ops@theunschut.com\n'
  printf 'QUESTBOARD_KEEP_RELEASES=7\n'
} > "$CONF_PATH"
chmod 600 "$CONF_PATH"
conf_sum="$(cksum < "$CONF_PATH")"
run_setup
check "an existing deploy.conf with a real recipient completes (exit 0)" "0" "$SETUP_RC"
check "an existing deploy.conf is left exactly as it was" "$conf_sum" "$(cksum < "$CONF_PATH")"
check "the versioned layout is recognised" "1" "$(printf '%s' "$SETUP_OUT" | grep -c 'already in the versioned layout')"
check "the new drop-in restarts the app once" "1" "$(called '^systemctl restart questboard.service')"

# --- nothing reached the real host -----------------------------------------

check "no host-guard stand-in was ever called" "" "$(host_guard_calls)"

if [ "$FAILURES" -gt 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES"
  exit 1
fi
printf 'all setup checks passed\n'
