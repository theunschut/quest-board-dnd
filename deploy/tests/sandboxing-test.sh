#!/usr/bin/env bash
# Static, offline checks on the files that define how the installer is run: the
# poll unit's hardening and write allow-list, the timer cadence, the drop-in for
# the application unit, the migrator's sandbox properties, the configuration
# template, and the rule that no script ever sources the secrets or settings
# files. Reads files only; touches nothing outside a temporary directory.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# shellcheck source=deploy/lib/common.sh
source "${REPO_ROOT}/deploy/lib/common.sh"

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

# Prints how many lines of FILE equal LINE exactly.
count_exact() {
  grep -cxF -- "$2" "$1" || true
}

SERVICE="${REPO_ROOT}/deploy/systemd/questboard-deploy-poll.service"
TIMER="${REPO_ROOT}/deploy/systemd/questboard-deploy-poll.timer"
DROPIN="${REPO_ROOT}/deploy/systemd/questboard.service.d/10-release-layout.conf"
EXAMPLE="${REPO_ROOT}/deploy/deploy.conf.example"
RELEASE_LIB="${REPO_ROOT}/deploy/lib/release.sh"

# --- poll service ---------------------------------------------------------

for line in \
  'Type=oneshot' \
  'ExecStart=/usr/local/sbin/questboard-deploy poll' \
  'After=network-online.target' \
  'Wants=network-online.target' \
  'RuntimeDirectory=questboard-deploy' \
  'RuntimeDirectoryPreserve=yes' \
  'NoNewPrivileges=yes' \
  'PrivateTmp=yes' \
  'ProtectSystem=strict' \
  'ProtectHome=read-only' \
  'ProtectKernelTunables=yes' \
  'ProtectKernelModules=yes' \
  'ProtectControlGroups=yes' \
  'RestrictNamespaces=yes' \
  'LockPersonality=yes' \
  'RestrictRealtime=yes' \
  'RestrictSUIDSGID=yes' \
  'SystemCallArchitectures=native' \
  'ReadWritePaths=/opt/questboard /var/lib/questboard-deploy'; do
  check "poll service has exactly one '${line}'" "1" "$(count_exact "$SERVICE" "$line")"
done

check "poll service has a single ReadWritePaths line" "1" "$(grep -c '^ReadWritePaths=' "$SERVICE" || true)"
check "no write path under /etc or /usr" "0" \
  "$(grep '^ReadWritePaths=' "$SERVICE" | grep -cE '(=| )/(etc|usr)(/| |$)' || true)"
check "poll service runs as root (no User=)" "0" "$(grep -c '^User=' "$SERVICE" || true)"

# --- poll timer -----------------------------------------------------------

for line in 'OnBootSec=2min' 'OnUnitActiveSec=5min' 'RandomizedDelaySec=30' 'WantedBy=timers.target'; do
  check "timer has exactly one '${line}'" "1" "$(count_exact "$TIMER" "$line")"
done

# --- drop-in for the application unit --------------------------------------

check "drop-in clears ExecStart" "1" "$(count_exact "$DROPIN" 'ExecStart=')"
check "drop-in resets ExecStart to the current release" "1" \
  "$(count_exact "$DROPIN" 'ExecStart=/usr/bin/dotnet /opt/questboard/current/app/QuestBoard.Service.dll')"
check "drop-in sets the working directory" "1" \
  "$(count_exact "$DROPIN" 'WorkingDirectory=/opt/questboard/current/app')"
check "drop-in sets no User=" "0" "$(grep -c '^[[:space:]]*User=' "$DROPIN" || true)"
check "drop-in sets no EnvironmentFile=" "0" "$(grep -c '^[[:space:]]*EnvironmentFile=' "$DROPIN" || true)"
first_exec="$(grep '^ExecStart=' "$DROPIN" | head -n 1)"
check "the clearing ExecStart comes first" "ExecStart=" "$first_exec"

# --- migrator sandbox properties -------------------------------------------

for prop in \
  'EnvironmentFile=' \
  'NoNewPrivileges=yes' \
  'PrivateTmp=yes' \
  'ProtectSystem=strict' \
  'ProtectHome=yes' \
  'ProtectKernelTunables=yes' \
  'ProtectKernelModules=yes' \
  'ProtectControlGroups=yes' \
  'RestrictNamespaces=yes' \
  'LockPersonality=yes' \
  'CapabilityBoundingSet=' \
  'RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6' \
  'RuntimeMaxSec=1800'; do
  check "migrator run sets --property=${prop}" "1" \
    "$(grep -cF -- "--property=${prop}" "$RELEASE_LIB" || true)"
done
check "migrator run sets --uid=" "1" "$(grep -c -- '--uid=' "$RELEASE_LIB" || true)"
check "migrator run sets --gid=" "1" "$(grep -c -- '--gid=' "$RELEASE_LIB" || true)"

# --- configuration template ------------------------------------------------

CONF_COPY="${WORK}/deploy.conf"
cp "$EXAMPLE" "$CONF_COPY"
chmod 600 "$CONF_COPY"

# Under a relocated root the loader expects the current user as owner.
missing_report="$(
  export QUESTBOARD_DEPLOY_ROOT="$WORK"
  for key in "${QUESTBOARD_CONF_ALLOWED_KEYS[@]}"; do unset "$key"; done
  questboard_load_conf "$CONF_COPY"
  for key in "${QUESTBOARD_CONF_ALLOWED_KEYS[@]}"; do
    [ -n "${!key:-}" ] || printf 'missing %s;' "$key"
  done
  printf 'done'
)"
check "the example loads and defines every allow-listed key" "done" "$missing_report"
check "the example has one line per allow-listed key" "${#QUESTBOARD_CONF_ALLOWED_KEYS[@]}" \
  "$(grep -c '^QUESTBOARD_' "$EXAMPLE" || true)"
check "the example recipient is a placeholder setup refuses" "1" \
  "$(grep -c '^QUESTBOARD_NOTIFY_EMAIL=.*@example\.com$' "$EXAMPLE" || true)"

# --- nothing sources the environment file or the settings file --------------

bad=0
for file in "${REPO_ROOT}"/deploy/bin/* "${REPO_ROOT}"/deploy/lib/*.sh; do
  [ -f "$file" ] || continue
  if grep -nE '^[[:space:]]*(source|\.)[[:space:]]+[^#]*(env|deploy\.conf)["'\'']?[[:space:]]*(#.*)?$' "$file" \
      | grep -v 'shellcheck' >/dev/null; then
    printf 'sources a secrets or settings file: %s\n' "$file" >&2
    bad=1
  fi
done
check "no installer file sources an env or deploy.conf path" "0" "$bad"

if [ "$FAILURES" -gt 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES"
  exit 1
fi
printf 'all sandboxing checks passed\n'
