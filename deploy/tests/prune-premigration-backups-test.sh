#!/usr/bin/env bash
# Proves the retention script for pre-migration backups on the database host:
# it keeps the newest N matching files, removes only matching regular files and
# refuses bad input without deleting anything. Runs entirely in a temporary
# directory.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PRUNE="${REPO_ROOT}/deploy/sql-ct/prune-premigration-backups.sh"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

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

# Prints the sorted entry names of a directory, joined with spaces.
listing() {
  find "$1" -mindepth 1 -maxdepth 1 -printf '%f\n' | sort | tr '\n' ' '
}

# Builds a directory with seven matching backups of distinct ages (backup-1 is
# the oldest) and three look-alikes that must never be touched.
build_dir() {
  local dir="$1" i
  mkdir -p "$dir"
  for i in 1 2 3 4 5 6 7; do
    : > "${dir}/questboard-premigration-host-2026010${i}T000000Z.bak"
    touch -d "2026-01-0${i} 00:00:00 UTC" "${dir}/questboard-premigration-host-2026010${i}T000000Z.bak"
  done
  : > "${dir}/other.bak"
  touch -d "2020-01-01 00:00:00 UTC" "${dir}/other.bak"
  mkdir -p "${dir}/questboard-premigration-x.bak"
  : > "${dir}/questboard-premigration-y.bak.txt"
  touch -d "2020-01-01 00:00:00 UTC" "${dir}/questboard-premigration-y.bak.txt"
}

# --- keep five of seven ------------------------------------------------------

D1="${WORK}/d1"
build_dir "$D1"
out="$(bash "$PRUNE" "$D1" 5)"
check "KEEP=5 prints a line for each of the two oldest backups" \
  "$(printf 'removed questboard-premigration-host-20260101T000000Z.bak\nremoved questboard-premigration-host-20260102T000000Z.bak')" \
  "$(printf '%s\n' "$out" | sort)"
check "KEEP=5 leaves the five newest and every look-alike" \
  "other.bak questboard-premigration-host-20260103T000000Z.bak questboard-premigration-host-20260104T000000Z.bak questboard-premigration-host-20260105T000000Z.bak questboard-premigration-host-20260106T000000Z.bak questboard-premigration-host-20260107T000000Z.bak questboard-premigration-x.bak questboard-premigration-y.bak.txt " \
  "$(listing "$D1")"
check "the look-alike directory survives" "1" "$([ -d "${D1}/questboard-premigration-x.bak" ] && echo 1 || echo 0)"

out="$(bash "$PRUNE" "$D1" 5)"
check "a second run removes nothing" "" "$out"

# --- keep more than exist ----------------------------------------------------

D2="${WORK}/d2"
build_dir "$D2"
before="$(listing "$D2")"
out="$(bash "$PRUNE" "$D2" 50)"
check "KEEP above the file count removes nothing" "$before" "$(listing "$D2")"
check "KEEP above the file count prints nothing" "" "$out"

# --- default keep ------------------------------------------------------------

D3="${WORK}/d3"
build_dir "$D3"
bash "$PRUNE" "$D3" >/dev/null
check "the default keeps five" "5" "$(find "$D3" -maxdepth 1 -type f -name 'questboard-premigration-*.bak' | wc -l | tr -d ' ')"

# --- refusals ----------------------------------------------------------------

D4="${WORK}/d4"
build_dir "$D4"
before="$(listing "$D4")"
for bad in 0 abc -1 1.5 ""; do
  rc=0
  bash "$PRUNE" "$D4" "$bad" >/dev/null 2>&1 || rc=$?
  check "KEEP='${bad}' exits non-zero" "1" "$rc"
done
check "refused runs delete nothing" "$before" "$(listing "$D4")"

rc=0
bash "$PRUNE" "${WORK}/missing" 5 >/dev/null 2>&1 || rc=$?
check "a missing directory exits non-zero" "1" "$rc"

# --- no matches --------------------------------------------------------------

D5="${WORK}/d5"
mkdir -p "$D5"
: > "${D5}/other.bak"
: > "${D5}/notes.txt"
before="$(listing "$D5")"
rc=0
out="$(bash "$PRUNE" "$D5" 3)" || rc=$?
check "a directory with no matches exits 0" "0" "$rc"
check "a directory with no matches is untouched" "$before" "$(listing "$D5")"

D6="${WORK}/d6"
mkdir -p "$D6"
rc=0
bash "$PRUNE" "$D6" 3 >/dev/null || rc=$?
check "an empty directory exits 0" "0" "$rc"

if [ "$FAILURES" -gt 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES"
  exit 1
fi
printf 'all prune checks passed\n'
