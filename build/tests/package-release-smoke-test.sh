#!/usr/bin/env bash
# End-to-end check of the release artifact: package (or take a given zip), verify the layout,
# run the packaged migrator against an unreachable database, and run the packaged app from a
# versioned layout reached through a "current" symlink.
#
# Usage: package-release-smoke-test.sh [ZIP]
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK="$(mktemp -d)"
APP_PID=""
FAILURES=0

cleanup() {
  if [ -n "$APP_PID" ]; then
    kill "$APP_PID" 2>/dev/null || true
    wait "$APP_PID" 2>/dev/null || true
  fi
  rm -rf "$WORK"
}
trap cleanup EXIT

check() {
  local description="$1" expected="$2" actual="$3"
  if [ "$expected" = "$actual" ]; then
    echo "PASS: $description"
  else
    echo "FAIL: $description (expected '$expected', got '$actual')"
    FAILURES=$((FAILURES + 1))
  fi
}

fail_now() {
  echo "FAIL: $1"
  exit 1
}

ZIP="${1:-}"
if [ -z "$ZIP" ]; then
  ZIP="$("$REPO_ROOT/build/package-release.sh" --version 0.0.1 --commit "$(git -C "$REPO_ROOT" rev-parse HEAD)" --output "$WORK/out" | tail -n 1)" \
    || fail_now "package-release.sh failed"
fi
[ -f "$ZIP" ] || fail_now "zip not found: $ZIP"
ZIP="$(cd "$(dirname "$ZIP")" && pwd)/$(basename "$ZIP")"

MANIFEST="$(unzip -p "$ZIP" release-manifest.json)" || fail_now "zip has no release-manifest.json"
VERSION="$(printf '%s' "$MANIFEST" | python3 -c 'import json,sys; print(json.load(sys.stdin)["version"])')"
HEADER_FLAG="$(printf '%s' "$MANIFEST" | python3 -c 'import json,sys; print(str(json.load(sys.stdin)["healthVersionHeader"]).lower())')"

check "manifest healthVersionHeader is true" "true" "$HEADER_FLAG"
if [ -z "${1:-}" ]; then
  check "manifest version matches the packaged version" "0.0.1" "$VERSION"
fi

# Checksum sits next to the zip and names it.
(cd "$(dirname "$ZIP")" && sha256sum -c "$(basename "$ZIP").sha256" >/dev/null 2>&1)
check "sha256 file verifies the zip" "0" "$?"

ENTRIES="$(unzip -Z1 "$ZIP")"
for entry in release-manifest.json app/QuestBoard.Service.dll migrator/QuestBoard.Migrator.dll deploy/systemd/questboard.service.d/10-release-layout.conf; do
  if printf '%s\n' "$ENTRIES" | grep -qxF "$entry"; then
    check "zip contains $entry" "present" "present"
  else
    check "zip contains $entry" "present" "missing"
  fi
done
TEST_ENTRIES="$(printf '%s\n' "$ENTRIES" | grep -c '^deploy/tests/' || true)"
check "zip holds no deploy/tests entries" "0" "$TEST_ENTRIES"

# Lay the release out the way the server does: releases/<version> with current pointing at it.
OPT="$WORK/root/opt/questboard"
mkdir -p "$OPT/releases/$VERSION"
unzip -q "$ZIP" -d "$OPT/releases/$VERSION"
ln -s "releases/$VERSION" "$OPT/current"

# The migrator must fail closed and quiet when the database is unreachable.
MIGRATOR_OUT="$WORK/migrator.out"
ConnectionStrings__DefaultConnection='Server=tcp:127.0.0.1,1;Database=SmokeProbe;User Id=smoke-user;Password=smoke-secret-value;Connect Timeout=2;ConnectRetryCount=0;TrustServerCertificate=true' \
  dotnet "$OPT/current/migrator/QuestBoard.Migrator.dll" status >"$MIGRATOR_OUT" 2>&1
check "migrator status exits 4 when the database is unreachable" "4" "$?"
LEAKS="$(grep -cE 'smoke-user|smoke-secret-value|127\.0\.0\.1' "$MIGRATOR_OUT" || true)"
check "migrator output leaks no host, user or password" "0" "$LEAKS"

# Run the app from current/app exactly as the service drop-in does.
PORT="$(python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()')"
(
  cd "$OPT/current/app" || exit 1
  # Testing skips the database-backed startup work so the app can boot without SQL Server.
  ASPNETCORE_ENVIRONMENT=Testing ASPNETCORE_URLS="http://127.0.0.1:$PORT" \
    exec dotnet "$OPT/current/app/QuestBoard.Service.dll"
) >"$WORK/app.log" 2>&1 &
APP_PID=$!

STATUS=""
for _ in $(seq 1 60); do
  STATUS="$(curl -sS -m 5 -D "$WORK/health.headers" -o "$WORK/health.body" -w '%{http_code}' "http://127.0.0.1:$PORT/health" 2>/dev/null || true)"
  [ "$STATUS" = "200" ] && break
  if ! kill -0 "$APP_PID" 2>/dev/null; then
    break
  fi
  sleep 1
done
check "GET /health returns 200" "200" "$STATUS"

if [ "$STATUS" != "200" ]; then
  echo "--- app log ---"
  tail -n 40 "$WORK/app.log"
fi

HEADER_VALUE="$(grep -i '^x-questboard-version:' "$WORK/health.headers" 2>/dev/null | head -n 1 | cut -d: -f2- | tr -d ' \r\n')"
check "X-QuestBoard-Version equals the packaged version" "$VERSION" "$HEADER_VALUE"

BODY="$(cat "$WORK/health.body" 2>/dev/null || true)"
case "$BODY" in
  Healthy | Degraded) check "health body is Healthy or Degraded" "ok" "ok" ;;
  *) check "health body is Healthy or Degraded" "Healthy or Degraded" "$BODY" ;;
esac

FAVICON_STATUS="$(curl -sS -m 5 -o /dev/null -w '%{http_code}' "http://127.0.0.1:$PORT/favicon.ico" 2>/dev/null || true)"
check "GET /favicon.ico returns 200 (content root resolves through current)" "200" "$FAVICON_STATUS"

if [ "$FAILURES" -ne 0 ]; then
  echo "$FAILURES check(s) failed"
  exit 1
fi
echo "All checks passed"
