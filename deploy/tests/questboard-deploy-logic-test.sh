#!/usr/bin/env bash
# Proves the installer's decision and bookkeeping functions: every row of the
# outcome table, remember-and-skip state, atomic activation, pruning and
# detection of a newer installer. Needs no root, network, systemd or database:
# everything runs against a relocated temporary root.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

export QUESTBOARD_DEPLOY_ROOT
QUESTBOARD_DEPLOY_ROOT="$(mktemp -d)"
trap 'rm -rf "${QUESTBOARD_DEPLOY_ROOT}"' EXIT
# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$QUESTBOARD_DEPLOY_ROOT"

# shellcheck source=deploy/lib/common.sh
source "${REPO_ROOT}/deploy/lib/common.sh"
# shellcheck source=deploy/lib/deploy.sh
source "${REPO_ROOT}/deploy/lib/deploy.sh"

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

# --- questboard_decide_outcome: one check per row of the table ------------

decide() {
  questboard_decide_outcome "$@" 2>/dev/null
}

check "verify fail -> refused keep" "refused keep" "$(decide verify fail 0 1)"
check "content invalid -> refused keep" "refused keep" "$(decide content invalid 0 1)"
check "content disk -> failed keep" "failed keep" "$(decide content disk 0 1)"
check "content error -> failed keep" "failed keep" "$(decide content error 0 1)"
check "status unknown_applied -> refused keep" "refused keep" "$(decide status unknown_applied 0 1)"
check "status non_transactional -> refused keep" "refused keep" "$(decide status non_transactional 0 1)"
check "status error -> failed keep" "failed keep" "$(decide status error 0 1)"
check "backup fail -> failed keep" "failed keep" "$(decide backup fail 0 1)"
check "apply fail -> failed_rolled_back restart_previous" "failed_rolled_back restart_previous" "$(decide apply fail 0 1)"
check "health ok -> installed none" "installed none" "$(decide health ok 1 1)"
check "health fail, no migration, previous exists -> rolled_back switch_back" "rolled_back switch_back" "$(decide health fail 0 1)"
check "health fail, no migration, no previous -> failed leave" "failed leave" "$(decide health fail 0 0)"
check "health fail after a migration -> halted leave" "halted leave" "$(decide health fail 1 1)"
check "health fail after a migration without a previous -> halted leave" "halted leave" "$(decide health fail 1 0)"

check "an unknown stage exits 2" "2" "$(status_of questboard_decide_outcome nonsense fail 0 1)"
check "an unknown result exits 2" "2" "$(status_of questboard_decide_outcome verify exploded 0 1)"
check "a known stage with another stage's result exits 2" "2" "$(status_of questboard_decide_outcome verify invalid 0 1)"
check "an unrecognised migrated flag on a health failure exits 2" "2" "$(status_of questboard_decide_outcome health fail maybe 1)"
check "an empty call exits 2" "2" "$(status_of questboard_decide_outcome)"

# --- questboard_record_attempt / questboard_remembered_outcome ------------

STATE="${QUESTBOARD_DEPLOY_ROOT}/state-memory"

check "an untried tag has no remembered outcome" "" "$(questboard_remembered_outcome "$STATE" v1.2.0)"

for outcome in refused failed failed_rolled_back rolled_back halted abandoned; do
  questboard_record_attempt "$STATE" v1.2.0 "$outcome"
  check "a tag last recorded ${outcome} is remembered as ${outcome}" "$outcome" \
    "$(questboard_remembered_outcome "$STATE" v1.2.0)"
done

check "a different tag is unaffected" "" "$(questboard_remembered_outcome "$STATE" v1.2.1)"
check "a tag that only shares a prefix is unaffected" "" "$(questboard_remembered_outcome "$STATE" v1.2)"
check "the state directory is private" "700" "$(stat -c '%a' "$STATE")"

questboard_record_attempt "$STATE" v1.2.0 installed
check "a later installed line clears the memory" "" "$(questboard_remembered_outcome "$STATE" v1.2.0)"

questboard_record_attempt "$STATE" v1.3.0 halted
questboard_record_attempt "$STATE" v1.3.0 adopted
check "adopted is never remembered" "" "$(questboard_remembered_outcome "$STATE" v1.3.0)"
questboard_record_attempt "$STATE" v1.3.0 failed
questboard_record_attempt "$STATE" v1.3.0 rolled_back_manual
check "rolled_back_manual is never remembered" "" "$(questboard_remembered_outcome "$STATE" v1.3.0)"

questboard_record_attempt "$STATE" v1.5.0 abandoned
check "abandoned is remembered so a poll skips the release rolled away from" "abandoned" \
  "$(questboard_remembered_outcome "$STATE" v1.5.0)"
questboard_record_attempt "$STATE" v1.5.0 installed
check "an explicit install clears an abandoned tag" "" "$(questboard_remembered_outcome "$STATE" v1.5.0)"

check "attempt lines are TAG OUTCOME UTC" "yes" \
  "$(grep -qE '^v1\.2\.0 installed [0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$' "${STATE}/attempts" && echo yes || echo no)"
check "an unknown outcome is refused" "1" \
  "$( ( questboard_record_attempt "$STATE" v1.4.0 exploded ) >/dev/null 2>&1; echo $? )"
check "a tag with whitespace is refused" "1" \
  "$( ( questboard_record_attempt "$STATE" 'v1.4.0 installed' failed ) >/dev/null 2>&1; echo $? )"
check "nothing was recorded for the refused attempts" "0" "$(grep -c '^v1.4.0' "${STATE}/attempts" || true)"

# --- questboard_activate_release ------------------------------------------

make_release() {
  local releases_dir="$1" version="$2"
  mkdir -p "${releases_dir}/${version}/app"
  printf '{"version":"%s","commit":"abc","healthVersionHeader":true}' "$version" \
    > "${releases_dir}/${version}/release-manifest.json"
}

make_adopted_release() {
  local releases_dir="$1" version="$2"
  mkdir -p "${releases_dir}/${version}/app"
  printf '{"version":"%s","commit":"","healthVersionHeader":false,"adopted":true}' "$version" \
    > "${releases_dir}/${version}/release-manifest.json"
}

ACT="${QUESTBOARD_DEPLOY_ROOT}/activate"
mkdir -p "${ACT}/releases"
make_adopted_release "${ACT}/releases" 1.2.0
make_release "${ACT}/releases" 1.2.1
ln -s "${ACT}/releases/1.2.0" "${ACT}/current"

questboard_activate_release 1.2.1 "${ACT}/releases" "${ACT}/current" "${ACT}/state"

check "activation repoints current at the new release" "${ACT}/releases/1.2.1" "$(readlink -f "${ACT}/current")"
check "activation records the previous release" "1.2.0" "$(cat "${ACT}/state/previous")"
check "no temporary current link remains" "0" "$(find "$ACT" -maxdepth 1 -name 'current.*' | wc -l)"
check "active_version reports the new release" "1.2.1" "$(questboard_active_version "${ACT}/current")"
check "previous_version reports the old release" "1.2.0" "$(questboard_previous_version "${ACT}/state")"
check "active_version with no link prints nothing" "" "$(questboard_active_version "${ACT}/no-such-link")"
check "previous_version with no state prints nothing" "" "$(questboard_previous_version "${ACT}/no-such-state")"

questboard_activate_release 1.2.0 "${ACT}/releases" "${ACT}/current" "${ACT}/state"
check "rolling back to an adopted release works" "1.2.0" "$(questboard_active_version "${ACT}/current")"
check "rolling back records the release left behind" "1.2.1" "$(questboard_previous_version "${ACT}/state")"

questboard_activate_release 1.2.0 "${ACT}/releases" "${ACT}/current" "${ACT}/state"
check "re-activating the active release keeps the recorded previous" "1.2.1" "$(questboard_previous_version "${ACT}/state")"

check "activating a missing version dies" "1" \
  "$( ( questboard_activate_release 9.9.9 "${ACT}/releases" "${ACT}/current" "${ACT}/state" ) >/dev/null 2>&1; echo $? )"
check "a failed activation leaves current untouched" "1.2.0" "$(questboard_active_version "${ACT}/current")"

FRESH="${QUESTBOARD_DEPLOY_ROOT}/fresh"
mkdir -p "${FRESH}/releases"
make_release "${FRESH}/releases" 1.0.0
questboard_activate_release 1.0.0 "${FRESH}/releases" "${FRESH}/current" "${FRESH}/state"
check "a first activation works with no current link" "1.0.0" "$(questboard_active_version "${FRESH}/current")"
check "a first activation records no previous release" "" "$(questboard_previous_version "${FRESH}/state")"

# --- questboard_prune_releases --------------------------------------------

PRU="${QUESTBOARD_DEPLOY_ROOT}/prune"
mkdir -p "${PRU}/releases" "${PRU}/state"
make_adopted_release "${PRU}/releases" 0.9.0
for v in 1.0.0 1.1.0 1.2.0 1.3.0 1.4.0 1.5.0; do
  make_release "${PRU}/releases" "$v"
done
mkdir -p "${PRU}/releases/.staging-1.6.0/app"
ln -s "${PRU}/releases/1.5.0" "${PRU}/current"
printf '0.9.0\n' > "${PRU}/state/previous"

questboard_prune_releases "${PRU}/releases" "${PRU}/current" "${PRU}/state" 3

remaining="$(find "${PRU}/releases" -mindepth 1 -maxdepth 1 -type d -not -name '.staging-*' -printf '%f\n' | sort -V | paste -sd' ')"
check "pruning keeps the active, the previous and the newest others up to the count" \
  "0.9.0 1.4.0 1.5.0" "$remaining"
check "pruning never removes the active release" "yes" "$([ -d "${PRU}/releases/1.5.0" ] && echo yes || echo no)"
check "pruning never removes the previous (adopted) release" "yes" "$([ -d "${PRU}/releases/0.9.0" ] && echo yes || echo no)"
check "pruning ignores staging directories" "yes" "$([ -d "${PRU}/releases/.staging-1.6.0" ] && echo yes || echo no)"
check "the current link still resolves after pruning" "1.5.0" "$(questboard_active_version "${PRU}/current")"

questboard_prune_releases "${PRU}/releases" "${PRU}/current" "${PRU}/state" 3
check "pruning twice changes nothing" "0.9.0 1.4.0 1.5.0" \
  "$(find "${PRU}/releases" -mindepth 1 -maxdepth 1 -type d -not -name '.staging-*' -printf '%f\n' | sort -V | paste -sd' ')"

PRU2="${QUESTBOARD_DEPLOY_ROOT}/prune-protected"
mkdir -p "${PRU2}/releases" "${PRU2}/state"
for v in 1.0.0 1.1.0 1.2.0; do
  make_release "${PRU2}/releases" "$v"
done
ln -s "${PRU2}/releases/1.0.0" "${PRU2}/current"
printf '1.1.0\n' > "${PRU2}/state/previous"
questboard_prune_releases "${PRU2}/releases" "${PRU2}/current" "${PRU2}/state" 2
check "an old active release and its previous survive even past the count" "1.0.0 1.1.0" \
  "$(find "${PRU2}/releases" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' | sort -V | paste -sd' ')"

mkdir -p "${PRU2}/releases/notes"
questboard_prune_releases "${PRU2}/releases" "${PRU2}/current" "${PRU2}/state" 2
check "pruning leaves directories that are not versions alone" "yes" "$([ -d "${PRU2}/releases/notes" ] && echo yes || echo no)"
check "pruning an empty releases directory is harmless" "0" \
  "$(mkdir -p "${PRU2}/empty"; status_of questboard_prune_releases "${PRU2}/empty" "${PRU2}/current" "${PRU2}/state" 3)"

# --- questboard_deploy_files_differ ---------------------------------------

DIFF_REL="${QUESTBOARD_DEPLOY_ROOT}/diff/release"
DIFF_ROOT="${QUESTBOARD_DEPLOY_ROOT}/diff/root"

build_shipped_tree() {
  local dir="$1"
  mkdir -p "${dir}/deploy/bin" "${dir}/deploy/lib" "${dir}/deploy/systemd/questboard.service.d"
  printf '#!/bin/sh\necho dispatcher\n' > "${dir}/deploy/bin/questboard-deploy"
  printf 'common\n' > "${dir}/deploy/lib/common.sh"
  printf 'deploy\n' > "${dir}/deploy/lib/deploy.sh"
  printf 'service\n' > "${dir}/deploy/systemd/questboard-deploy-poll.service"
  printf 'timer\n' > "${dir}/deploy/systemd/questboard-deploy-poll.timer"
  printf 'dropin\n' > "${dir}/deploy/systemd/questboard.service.d/10-release-layout.conf"
}

install_tree() {
  local release="$1" root="$2"
  rm -rf "$root"
  mkdir -p "${root}/usr/local/sbin" "${root}/usr/local/lib/questboard-deploy" \
    "${root}/etc/systemd/system/questboard.service.d"
  cp "${release}/deploy/bin/questboard-deploy" "${root}/usr/local/sbin/questboard-deploy"
  cp "${release}"/deploy/lib/*.sh "${root}/usr/local/lib/questboard-deploy/"
  cp "${release}/deploy/systemd/questboard-deploy-poll.service" \
    "${release}/deploy/systemd/questboard-deploy-poll.timer" "${root}/etc/systemd/system/"
  cp "${release}/deploy/systemd/questboard.service.d/10-release-layout.conf" \
    "${root}/etc/systemd/system/questboard.service.d/"
}

build_shipped_tree "$DIFF_REL"
install_tree "$DIFF_REL" "$DIFF_ROOT"

check "identical installer files do not differ" "1" \
  "$(status_of questboard_deploy_files_differ "$DIFF_REL" "$DIFF_ROOT")"

printf 'common, newer\n' > "${DIFF_REL}/deploy/lib/common.sh"
check "one changed library file differs" "0" \
  "$(status_of questboard_deploy_files_differ "$DIFF_REL" "$DIFF_ROOT")"
install_tree "$DIFF_REL" "$DIFF_ROOT"

printf '#!/bin/sh\necho newer dispatcher\n' > "${DIFF_REL}/deploy/bin/questboard-deploy"
check "a changed dispatcher differs" "0" \
  "$(status_of questboard_deploy_files_differ "$DIFF_REL" "$DIFF_ROOT")"
install_tree "$DIFF_REL" "$DIFF_ROOT"

printf 'timer, newer\n' > "${DIFF_REL}/deploy/systemd/questboard-deploy-poll.timer"
check "a changed unit differs" "0" \
  "$(status_of questboard_deploy_files_differ "$DIFF_REL" "$DIFF_ROOT")"
install_tree "$DIFF_REL" "$DIFF_ROOT"

printf 'dropin, newer\n' > "${DIFF_REL}/deploy/systemd/questboard.service.d/10-release-layout.conf"
check "a changed drop-in differs" "0" \
  "$(status_of questboard_deploy_files_differ "$DIFF_REL" "$DIFF_ROOT")"
install_tree "$DIFF_REL" "$DIFF_ROOT"

rm -f "${DIFF_ROOT}/etc/systemd/system/questboard-deploy-poll.service"
check "a missing installed unit differs" "0" \
  "$(status_of questboard_deploy_files_differ "$DIFF_REL" "$DIFF_ROOT")"
install_tree "$DIFF_REL" "$DIFF_ROOT"

rm -f "${DIFF_ROOT}/usr/local/lib/questboard-deploy/deploy.sh"
check "a missing installed library file differs" "0" \
  "$(status_of questboard_deploy_files_differ "$DIFF_REL" "$DIFF_ROOT")"
install_tree "$DIFF_REL" "$DIFF_ROOT"

printf '#!/bin/sh\necho even newer dispatcher\n' > "${DIFF_REL}/deploy/bin/questboard-deploy"
questboard_deploy_files_differ "$DIFF_REL" "$DIFF_ROOT" || true
check "detection never rewrites the installed files" "echo newer dispatcher" \
  "$(sed -n 2p "${DIFF_ROOT}/usr/local/sbin/questboard-deploy")"

ADOPTED_REL="${QUESTBOARD_DEPLOY_ROOT}/diff/adopted"
make_adopted_release "$(dirname "$ADOPTED_REL")" adopted
check "a release without a deploy directory does not differ" "1" \
  "$(status_of questboard_deploy_files_differ "$ADOPTED_REL" "$DIFF_ROOT")"

# --- the host was never touched -------------------------------------------

check "no host management command was called" "" "$(host_guard_calls)"

if [ "$FAILURES" -gt 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES"
  exit 1
fi
printf 'all deploy checks passed\n'
