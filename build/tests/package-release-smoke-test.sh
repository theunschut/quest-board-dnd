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
  # A here-string, not a pipe: grep -q exits on the first match, and under
  # pipefail the writer's broken pipe would turn a hit into a false miss.
  if grep -qxF "$entry" <<<"$ENTRIES"; then
    check "zip contains $entry" "present" "present"
  else
    check "zip contains $entry" "present" "missing"
  fi
done
TEST_ENTRIES="$(printf '%s\n' "$ENTRIES" | grep -c '^deploy/tests/' || true)"
check "zip holds no deploy/tests entries" "0" "$TEST_ENTRIES"

# Every vendored front-end file committed under wwwroot/lib must travel in the zip, or a page would
# lose its styles, scripts or fonts in production.
LIB_PREFIX="QuestBoard.Service/wwwroot/lib/"
LIB_FILES="$(git -C "$REPO_ROOT" ls-files "$LIB_PREFIX")"
LIB_COUNT=0
while IFS= read -r lib_file; do
  [ -n "$lib_file" ] || continue
  LIB_COUNT=$((LIB_COUNT + 1))
  lib_entry="app/wwwroot/lib/${lib_file#"$LIB_PREFIX"}"
  if grep -qxF "$lib_entry" <<<"$ENTRIES"; then
    check "zip contains $lib_entry" "present" "present"
  else
    check "zip contains $lib_entry" "present" "missing"
  fi
done <<<"$LIB_FILES"
if [ "$LIB_COUNT" -gt 0 ]; then
  check "zip carries vendor files" "present" "present"
else
  check "zip carries vendor files" "present" "none committed under $LIB_PREFIX"
fi

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

# Every vendored file a layout points at must be served by the packaged app.
LAYOUT_SOURCES=(
  "QuestBoard.Service/Views/Shared/_Layout.cshtml"
  "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml"
  "QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml"
  "QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml"
  "QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml"
)
LAYOUT_PATHS=()
for layout in "${LAYOUT_SOURCES[@]}"; do
  LAYOUT_PATHS+=("$REPO_ROOT/$layout")
done
LIB_URLS="$(grep -ohE '(href|src)="~/lib/[^"?#]+' "${LAYOUT_PATHS[@]}" | sed -E 's/^(href|src)="~//' | sort -u || true)"
LIB_URL_COUNT=0
while IFS= read -r lib_url; do
  [ -n "$lib_url" ] || continue
  LIB_URL_COUNT=$((LIB_URL_COUNT + 1))
  lib_code="$(curl -s -m 10 -o /dev/null -w '%{http_code}' "http://127.0.0.1:$PORT$lib_url" 2>/dev/null || true)"
  check "GET $lib_url returns 200" "200" "$lib_code"
done <<<"$LIB_URLS"
if [ "$LIB_URL_COUNT" -gt 0 ]; then
  check "layouts reference vendor files" "present" "present"
else
  check "layouts reference vendor files" "present" "none found"
fi

if [ "$FAILURES" -ne 0 ]; then
  echo "$FAILURES check(s) failed"
  exit 1
fi
echo "All checks passed"
