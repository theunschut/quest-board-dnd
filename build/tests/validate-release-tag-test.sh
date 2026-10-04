#!/usr/bin/env bash
# Exercises build/validate-release-tag.sh against a throwaway git repository.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$repo_root/build/validate-release-tag.sh"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

repo="$work/repo"
mkdir -p "$repo"
cd "$repo"

git init --quiet --initial-branch=main .
git config user.email "test@example.com"
git config user.name "Test User"
git config commit.gpgsign false
git config tag.gpgsign false

git commit --quiet --allow-empty -m "first"
older_main="$(git rev-parse HEAD)"
git commit --quiet --allow-empty -m "second"
latest_main="$(git rev-parse HEAD)"

git checkout --quiet -b feature
git commit --quiet --allow-empty -m "unmerged work"
feature_commit="$(git rev-parse HEAD)"
git checkout --quiet main

# The script checks ancestry against origin/main; point a local remote ref at main.
git remote add origin "$repo"
git update-ref refs/remotes/origin/main refs/heads/main

git tag v0.1.0 "$latest_main"
git tag v1.2.3 "$latest_main"
git tag v10.20.30 "$latest_main"
git tag v0.2.0 "$older_main"
git tag v0.3.0 "$feature_commit"
git tag -a -m "annotated release" v2.0.0 "$latest_main"
git tag -a -m "annotated unmerged" v2.1.0 "$feature_commit"

failures=0
out="$work/stdout"
err="$work/stderr"

# check DESCRIPTION EXPECT(0|1) EXPECTED_STDOUT NAME=VALUE...
check() {
  local desc="$1" expect="$2" expected_out="$3"
  shift 3
  local status=0
  (
    unset TAG GITHUB_SHA MAIN_REF
    for kv in "$@"; do
      export "${kv%%=*}=${kv#*=}"
    done
    "$script"
  ) >"$out" 2>"$err" || status=$?

  local actual_out
  actual_out="$(cat "$out")"
  local problem=""
  if [ "$expect" -eq 0 ]; then
    [ "$status" -eq 0 ] || problem="expected success, exit $status ($(cat "$err"))"
    [ -n "$problem" ] || [ "$actual_out" = "$expected_out" ] \
      || problem="expected stdout '$expected_out', got '$actual_out'"
  else
    [ "$status" -ne 0 ] || problem="expected failure, got success"
    [ -n "$problem" ] || [ -z "$actual_out" ] || problem="stdout must be empty on failure, got '$actual_out'"
    [ -n "$problem" ] || grep -q '::error::' "$err" || problem="no ::error:: line on stderr"
  fi

  if [ -n "$problem" ]; then
    echo "FAIL: $desc: $problem"
    failures=$((failures + 1))
  else
    echo "PASS: $desc"
  fi
}

check "v0.1.0 on latest main commit" 0 "version=0.1.0" "TAG=v0.1.0"
check "v1.2.3 on latest main commit" 0 "version=1.2.3" "TAG=v1.2.3"
check "v10.20.30 on latest main commit" 0 "version=10.20.30" "TAG=v10.20.30"
check "v0.2.0 on an older main commit" 0 "version=0.2.0" "TAG=v0.2.0"
check "annotated tag on a main commit" 0 "version=2.0.0" "TAG=v2.0.0"

check "empty TAG" 1 "" "TAG="
check "missing v prefix" 1 "" "TAG=1.2.3"
check "missing patch" 1 "" "TAG=v1.2"
check "four components" 1 "" "TAG=v1.2.3.4"
check "leading zero in major" 1 "" "TAG=v01.2.3"
check "leading zero in minor" 1 "" "TAG=v1.02.3"
check "pre-release suffix" 1 "" "TAG=v1.2.3-rc.1"
check "build metadata suffix" 1 "" "TAG=v1.2.3+build"
check "uppercase V" 1 "" "TAG=V1.2.3"
newline_tag=$'v1.2.3\n'
check "trailing newline" 1 "" "TAG=$newline_tag"
check "shell metacharacters" 1 "" "TAG=v1.2.3; rm -rf /"
check "embedded space" 1 "" "TAG=v1.2.3 v1.2.3"
check "well-formed but nonexistent tag" 1 "" "TAG=v9.9.9"

check "lightweight tag on unmerged commit" 1 "" "TAG=v0.3.0"
check "annotated tag on unmerged commit" 1 "" "TAG=v2.1.0"
check "GITHUB_SHA differs from tag commit" 1 "" "TAG=v1.2.3" "GITHUB_SHA=$older_main"
check "GITHUB_SHA matches tag commit" 0 "version=1.2.3" "TAG=v1.2.3" "GITHUB_SHA=$latest_main"

if [ "$failures" -ne 0 ]; then
  echo "$failures case(s) failed"
  exit 1
fi
echo "All validate-release-tag cases passed."
