#!/usr/bin/env bash
# Proves the shared helpers used by the installer: version and tag checks, the
# allow-list configuration loader, the HTTP fetch helper and the outcome mail
# (rendering and sending). Needs no root, network, systemd or database:
# everything runs against a relocated temporary root with a stand-in curl.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

export QUESTBOARD_DEPLOY_ROOT
QUESTBOARD_DEPLOY_ROOT="$(mktemp -d)"
trap 'rm -rf "${QUESTBOARD_DEPLOY_ROOT}"' EXIT
# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$QUESTBOARD_DEPLOY_ROOT"

# A per-test stand-in for curl. It records its arguments one per line, prints
# STUB_CURL_OUT and exits with STUB_CURL_EXIT, so no test touches the network.
STUB_DIR="${QUESTBOARD_DEPLOY_ROOT}/stubs"
mkdir -p "$STUB_DIR"
export STUB_CURL_LOG="${STUB_DIR}/curl-args.log"
cat > "${STUB_DIR}/curl" <<'EOF'
#!/bin/sh
: > "$STUB_CURL_LOG"
for arg in "$@"; do
  printf '%s\n' "$arg" >> "$STUB_CURL_LOG"
done
printf '%s' "${STUB_CURL_OUT:-}"
exit "${STUB_CURL_EXIT:-0}"
EOF
chmod +x "${STUB_DIR}/curl"
export PATH="${STUB_DIR}:${PATH}"

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

# Runs a command and prints only its exit status.
status_of() {
  local rc=0
  "$@" >/dev/null 2>&1 || rc=$?
  printf '%s' "$rc"
}

# --- questboard_semver_gt -------------------------------------------------

check "1.2.10 > 1.2.9" "0" "$(status_of questboard_semver_gt 1.2.10 1.2.9)"
check "2.0.0 > 1.9.9" "0" "$(status_of questboard_semver_gt 2.0.0 1.9.9)"
check "1.2.3 is not greater than 1.2.3" "1" "$(status_of questboard_semver_gt 1.2.3 1.2.3)"
check "1.2.3 is not greater than 1.10.0" "1" "$(status_of questboard_semver_gt 1.2.3 1.10.0)"

# --- questboard_is_strict_tag / questboard_is_plain_version ---------------

for tag in v0.1.0 v1.2.3 v10.20.30; do
  check "strict tag accepts ${tag}" "0" "$(status_of questboard_is_strict_tag "$tag")"
done
for tag in v01.2.3 v1.2.3-rc.1 v1.2.3+b v1.2 1.2.3 V1.2.3 "v1.2.3 " ""; do
  check "strict tag rejects [${tag}]" "1" "$(status_of questboard_is_strict_tag "$tag")"
done
check "plain version accepts 1.2.3" "0" "$(status_of questboard_is_plain_version 1.2.3)"
check "plain version rejects v1.2.3" "1" "$(status_of questboard_is_plain_version v1.2.3)"
check "plain version rejects 1.02.3" "1" "$(status_of questboard_is_plain_version 1.02.3)"

# --- questboard_load_conf -------------------------------------------------

CONF_DIR="${QUESTBOARD_DEPLOY_ROOT}/conf"
mkdir -p "$CONF_DIR"

write_conf() {
  local name="$1"
  local mode="$2"
  shift 2
  local file="${CONF_DIR}/${name}"
  printf '%s\n' "$@" > "$file"
  chmod "$mode" "$file"
  printf '%s' "$file"
}

clear_conf_vars() {
  local key
  for key in "${QUESTBOARD_CONF_ALLOWED_KEYS[@]}"; do
    unset "$key"
  done
}

VALID_CONF="$(write_conf valid.conf 600 \
  '# comment line' \
  '' \
  'QUESTBOARD_GITHUB_REPO=Theunschut/quest-board-dnd' \
  'QUESTBOARD_SIGNER_WORKFLOW=.github/workflows/release.yml' \
  'QUESTBOARD_NOTIFY_EMAIL=ops@example.com' \
  'QUESTBOARD_MAIL_FROM=noreply@example.com' \
  'QUESTBOARD_SMTP_HOST=192.168.6.13' \
  'QUESTBOARD_SMTP_PORT=25' \
  'QUESTBOARD_KEEP_RELEASES=3' \
  'QUESTBOARD_HEALTH_TIMEOUT_SECONDS=60' \
  'QUESTBOARD_HEALTH_URL="http://127.0.0.1:5000/health"')"

clear_conf_vars
questboard_load_conf "$VALID_CONF"
check "load_conf reads the repository" "Theunschut/quest-board-dnd" "${QUESTBOARD_GITHUB_REPO:-}"
check "load_conf reads the signer workflow" ".github/workflows/release.yml" "${QUESTBOARD_SIGNER_WORKFLOW:-}"
check "load_conf reads the recipient" "ops@example.com" "${QUESTBOARD_NOTIFY_EMAIL:-}"
check "load_conf reads the relay port" "25" "${QUESTBOARD_SMTP_PORT:-}"
check "load_conf reads the retained release count" "3" "${QUESTBOARD_KEEP_RELEASES:-}"
check "load_conf strips quotes from the health url" "http://127.0.0.1:5000/health" "${QUESTBOARD_HEALTH_URL:-}"

EMPTY_RECIPIENT_CONF="$(write_conf empty-recipient.conf 600 'QUESTBOARD_NOTIFY_EMAIL=')"
clear_conf_vars
questboard_load_conf "$EMPTY_RECIPIENT_CONF"
check "load_conf accepts an empty recipient" "" "${QUESTBOARD_NOTIFY_EMAIL-unset}"

conf_status() {
  local file="$1"
  local rc=0
  ( questboard_load_conf "$file" ) >/dev/null 2>&1 || rc=$?
  printf '%s' "$rc"
}

check "load_conf rejects an unknown key" "1" \
  "$(conf_status "$(write_conf unknown-key.conf 600 'QUESTBOARD_SOMETHING_ELSE=1')")"
check "load_conf rejects a line without '='" "1" \
  "$(conf_status "$(write_conf no-equals.conf 600 'QUESTBOARD_SMTP_PORT 25')")"
check "load_conf rejects a group-writable file" "1" \
  "$(conf_status "$(write_conf group-writable.conf 660 'QUESTBOARD_SMTP_PORT=25')")"
check "load_conf rejects a world-writable file" "1" \
  "$(conf_status "$(write_conf world-writable.conf 666 'QUESTBOARD_SMTP_PORT=25')")"
check "load_conf rejects a non-numeric port" "1" \
  "$(conf_status "$(write_conf bad-port.conf 600 'QUESTBOARD_SMTP_PORT=twentyfive')")"
check "load_conf rejects an out-of-range port" "1" \
  "$(conf_status "$(write_conf big-port.conf 600 'QUESTBOARD_SMTP_PORT=99999')")"
check "load_conf rejects a keep count below two" "1" \
  "$(conf_status "$(write_conf keep-one.conf 600 'QUESTBOARD_KEEP_RELEASES=1')")"
check "load_conf rejects a health timeout below ten seconds" "1" \
  "$(conf_status "$(write_conf timeout-short.conf 600 'QUESTBOARD_HEALTH_TIMEOUT_SECONDS=5')")"
check "load_conf rejects a non-loopback health url" "1" \
  "$(conf_status "$(write_conf remote-health.conf 600 'QUESTBOARD_HEALTH_URL=http://example.com/health')")"
check "load_conf rejects a malformed signer workflow" "1" \
  "$(conf_status "$(write_conf bad-workflow.conf 600 'QUESTBOARD_SIGNER_WORKFLOW=../../etc/passwd')")"
check "load_conf rejects a missing file" "1" \
  "$(conf_status "${CONF_DIR}/does-not-exist.conf")"

PWNED_FILE="${QUESTBOARD_DEPLOY_ROOT}/PWNED"
INJECT_CONF="${CONF_DIR}/inject.conf"
printf '%s\n' 'QUESTBOARD_GITHUB_REPO=$(touch '"${PWNED_FILE}"')' > "$INJECT_CONF"
chmod 600 "$INJECT_CONF"
check "load_conf rejects a command-substitution value" "1" "$(conf_status "$INJECT_CONF")"
check "a command-substitution value never runs" "absent" \
  "$([ -e "$PWNED_FILE" ] && echo present || echo absent)"

BACKTICK_CONF="${CONF_DIR}/backtick.conf"
printf '%s\n' 'QUESTBOARD_NOTIFY_EMAIL=`touch '"${PWNED_FILE}"'`@example.com' > "$BACKTICK_CONF"
chmod 600 "$BACKTICK_CONF"
check "load_conf rejects a backtick value" "1" "$(conf_status "$BACKTICK_CONF")"
check "a backtick value never runs" "absent" \
  "$([ -e "$PWNED_FILE" ] && echo present || echo absent)"

if [ "$(id -u)" -ne 0 ]; then
  OWNER_CONF="$(write_conf owner.conf 600 'QUESTBOARD_SMTP_PORT=25')"
  owner_rc=0
  ( unset QUESTBOARD_DEPLOY_ROOT; questboard_load_conf "$OWNER_CONF" ) >/dev/null 2>&1 || owner_rc=$?
  check "load_conf rejects a file not owned by root outside a test root" "1" "$owner_rc"
fi

# --- questboard_http_fetch ------------------------------------------------

FETCH_OUT="${QUESTBOARD_DEPLOY_ROOT}/fetch-body"
export STUB_CURL_OUT="200" STUB_CURL_EXIT=0
check "http_fetch prints the status code" "200" \
  "$(questboard_http_fetch 20 https://example.com/x "$FETCH_OUT")"
check "http_fetch passes the documented curl arguments" \
  "--silent --show-error --location --max-time 20 --output ${FETCH_OUT} --write-out %{http_code} https://example.com/x" \
  "$(paste -sd' ' "$STUB_CURL_LOG")"

questboard_http_fetch 20 https://example.com/x "$FETCH_OUT" "${FETCH_OUT}.headers" >/dev/null
check "http_fetch adds --dump-header when a header file is given" "yes" \
  "$(grep -qx -- '--dump-header' "$STUB_CURL_LOG" && echo yes || echo no)"

export STUB_CURL_OUT="000" STUB_CURL_EXIT=28
fetch_rc=0
fetch_out="$(questboard_http_fetch 20 https://example.com/x "$FETCH_OUT")" || fetch_rc=$?
check "http_fetch returns curl's exit status on a transport failure" "28" "$fetch_rc"
check "http_fetch still prints the status code on a transport failure" "000" "$fetch_out"

# --- questboard_render_mail -----------------------------------------------

MAIL_FILE="${QUESTBOARD_DEPLOY_ROOT}/mail.eml"
STARTED="2026-10-04T10:00:00Z"
FINISHED="2026-10-04T10:02:30Z"
BACKUP="questboard-premigration-QuestBoard-20261004T100100Z.bak"

render() {
  questboard_render_mail --to ops@example.com --from noreply@example.com \
    --started "$STARTED" --finished "$FINISHED" "$@"
}

render --outcome halted --version 1.2.3 --reason unhealthy --backup "$BACKUP" \
  --previous-healthy no --update-from 1.2.3 > "$MAIL_FILE"

line_count="$(wc -l < "$MAIL_FILE")"
crlf_count="$(grep -c $'\r$' "$MAIL_FILE")"
check "every mail line ends in CRLF" "$line_count" "$crlf_count"

# Strip the carriage returns once so header and body checks read plainly.
MAIL_TEXT="${QUESTBOARD_DEPLOY_ROOT}/mail.txt"
tr -d '\r' < "$MAIL_FILE" > "$MAIL_TEXT"

for header in From To Subject Date Message-ID MIME-Version Content-Type; do
  check "mail carries a ${header} header" "1" "$(grep -c "^${header}: " "$MAIL_TEXT")"
done
check "mail content type is plain UTF-8 text" "yes" \
  "$(grep -qx 'Content-Type: text/plain; charset=UTF-8' "$MAIL_TEXT" && echo yes || echo no)"
check "mail subject names the result and version" \
  "Subject: [questboard-deploy] halted - migrations applied v1.2.3" \
  "$(grep '^Subject: ' "$MAIL_TEXT")"
check "mail headers are ASCII" "0" \
  "$(sed '/^$/q' "$MAIL_TEXT" | LC_ALL=C grep -c '[^[:print:][:space:]]' || true)"

MAIL_BODY="${QUESTBOARD_DEPLOY_ROOT}/mail-body.txt"
sed '1,/^$/d' "$MAIL_TEXT" > "$MAIL_BODY"
bad_prefix="$(grep -vcE '^(Version|Result|Started|Finished|Backup|Previous release healthy|Installer update available): ' "$MAIL_BODY" || true)"
check "every body line starts with an allowed label" "0" "$bad_prefix"
check "mail body names the backup for a halted result" "Backup: ${BACKUP}" "$(grep '^Backup: ' "$MAIL_BODY")"
check "mail body carries the reason as plain text" \
  "Result: halted - migrations applied (the new release did not become healthy)" \
  "$(grep '^Result: ' "$MAIL_BODY")"
check "mail body offers the installer update" \
  "Installer update available: run setup from release 1.2.3" \
  "$(grep '^Installer update available: ' "$MAIL_BODY")"
for forbidden in '/' '=' 'Server' 'Password' 'Data Source'; do
  check "mail body never contains '${forbidden}'" "0" \
    "$(grep -cF -- "$forbidden" "$MAIL_BODY" || true)"
done

render --outcome installed --version 1.2.3 > "$MAIL_FILE"
check "an installed mail has no Backup line" "0" "$(grep -c '^Backup:' "$MAIL_FILE" || true)"
render --outcome failed --version 1.2.3 --backup "$BACKUP" > "$MAIL_FILE"
check "a backup name is left out unless the result is halted" "0" "$(grep -c '^Backup:' "$MAIL_FILE" || true)"

render_status() {
  local rc=0
  local out
  out="$(render "$@" 2>/dev/null)" || rc=$?
  printf '%s:%s' "$rc" "${#out}"
}

check "render_mail refuses an unknown outcome" "1:0" "$(render_status --outcome exploded --version 1.2.3)"
check "render_mail refuses a version with a v" "1:0" "$(render_status --outcome installed --version v1.2.3)"
check "render_mail refuses a non-plain version" "1:0" "$(render_status --outcome installed --version '1.2.3; rm')"
check "render_mail refuses a backup with a path" "1:0" \
  "$(render_status --outcome halted --version 1.2.3 --backup /var/backups/questboard-premigration-Db-20261004T100100Z.bak)"
check "render_mail refuses a backup with a bad name" "1:0" \
  "$(render_status --outcome halted --version 1.2.3 --backup backup.bak)"
check "render_mail refuses a reason outside the closed set" "1:0" \
  "$(render_status --outcome failed --version 1.2.3 --reason 'Server=db;Password=x')"
raw_render_status() {
  local rc=0
  local out
  out="$(questboard_render_mail "$@" 2>/dev/null)" || rc=$?
  printf '%s:%s' "$rc" "${#out}"
}

check "render_mail refuses a malformed timestamp" "1:0" \
  "$(raw_render_status --to ops@example.com --from noreply@example.com --outcome installed --version 1.2.3 --started yesterday --finished "$FINISHED")"
check "render_mail refuses a header-injecting recipient" "1:0" \
  "$(raw_render_status --to $'ops@example.com\r\nBcc: evil@example.com' --from noreply@example.com --outcome installed --version 1.2.3 --started "$STARTED" --finished "$FINISHED")"
check "render_mail refuses a previous-healthy value outside yes or no" "1:0" \
  "$(render_status --outcome rolled_back --version 1.2.3 --previous-healthy maybe)"

check "result labels cover the closed set" \
  "installed|refused|failed|failed, rolled back|rolled back|halted - migrations applied" \
  "$(for o in installed refused failed failed_rolled_back rolled_back halted; do questboard_result_label "$o"; printf '|'; done | sed 's/|$//')"

for code in checksum_mismatch attestation_failed not_on_main asset_missing invalid_content database_ahead \
    non_transactional_migration insufficient_disk database_unreachable backup_failed apply_failed unhealthy restart_failed; do
  label="$(questboard_reason_label "$code")"
  unsafe="$(printf '%s' "$label" | grep -cE '[/=]|Server|Password|Data Source' || true)"
  check "reason label for ${code} is non-empty and safe" "ok" \
    "$([ -n "$label" ] && [ "$unsafe" = "0" ] && echo ok || echo bad)"
done
check "an unknown reason has no label" "1" "$(status_of questboard_reason_label made_up)"

# --- questboard_send_mail -------------------------------------------------

printf 'Subject: test\r\n\r\nbody\r\n' > "$MAIL_FILE"

export QUESTBOARD_NOTIFY_EMAIL="" QUESTBOARD_MAIL_FROM="noreply@example.com"
export QUESTBOARD_SMTP_HOST="192.168.6.13" QUESTBOARD_SMTP_PORT="25"
rm -f "$STUB_CURL_LOG"
send_log="$(questboard_send_mail "$MAIL_FILE" 2>&1)"
check "send_mail with an empty recipient does not call curl" "no" \
  "$([ -e "$STUB_CURL_LOG" ] && echo yes || echo no)"
check "send_mail with an empty recipient logs one line" "1" "$(printf '%s\n' "$send_log" | wc -l)"

export QUESTBOARD_NOTIFY_EMAIL="ops@example.com"
export STUB_CURL_OUT="" STUB_CURL_EXIT=0
questboard_send_mail "$MAIL_FILE"
check "send_mail targets the configured relay with a helo path" "yes" \
  "$(grep -qx 'smtp://192.168.6.13:25/questboard-deploy' "$STUB_CURL_LOG" && echo yes || echo no)"
check "send_mail passes the sender" "yes" \
  "$(grep -qx -- '--mail-from' "$STUB_CURL_LOG" && grep -qx 'noreply@example.com' "$STUB_CURL_LOG" && echo yes || echo no)"
check "send_mail passes the recipient" "yes" \
  "$(grep -qx -- '--mail-rcpt' "$STUB_CURL_LOG" && grep -qx 'ops@example.com' "$STUB_CURL_LOG" && echo yes || echo no)"
check "send_mail uploads the message file" "yes" \
  "$(grep -qx -- '--upload-file' "$STUB_CURL_LOG" && grep -qxF "$MAIL_FILE" "$STUB_CURL_LOG" && echo yes || echo no)"
check "send_mail never touches the local mail agent" "" "$(host_guard_calls)"

export STUB_CURL_EXIT=55
fail_rc=0
fail_log="$(questboard_send_mail "$MAIL_FILE" 2>&1)" || fail_rc=$?
check "send_mail returns 0 when curl fails" "0" "$fail_rc"
check "send_mail logs exactly one line when curl fails" "1" "$(printf '%s\n' "$fail_log" | wc -l)"

# --- questboard_log -------------------------------------------------------

check "log lines carry a UTC timestamp and the tool name" "yes" \
  "$(questboard_log hello 2>&1 | grep -qE '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z questboard-deploy: hello$' && echo yes || echo no)"
check "die exits non-zero" "1" "$( ( questboard_die boom ) >/dev/null 2>&1; echo $? )"

if [ "$FAILURES" -gt 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES"
  exit 1
fi
printf 'all common checks passed\n'
