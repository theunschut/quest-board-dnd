#!/usr/bin/env bash
# Runs every offline deploy test: each deploy/tests/*-test.sh except the
# *-network-test.sh files, which need a network and are run on their own.
# Works from any directory. Exits non-zero if any test file fails.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

passed=0
failed=0
failed_names=()

shopt -s nullglob
for test_file in "${SCRIPT_DIR}"/*-test.sh; do
  name="$(basename "$test_file")"
  case "$name" in
    *-network-test.sh) continue ;;
  esac

  if output="$(bash "$test_file" 2>&1)"; then
    passed=$((passed + 1))
    printf 'PASS: %s\n' "$name"
  else
    failed=$((failed + 1))
    failed_names+=("$name")
    printf 'FAIL: %s\n' "$name"
    printf '%s\n' "$output" | sed 's/^/    /'
  fi
done

printf '%d passed, %d failed\n' "$passed" "$failed"

if [ "$failed" -gt 0 ]; then
  exit 1
fi
