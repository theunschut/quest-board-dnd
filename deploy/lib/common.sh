#!/usr/bin/env bash
# Shared logging, safe config loading, version checks, the HTTP fetch helper
# and outcome mail used by the deploy tooling. Sourced, never executed
# directly. Callers own `set -euo pipefail`.

if [ -n "${QUESTBOARD_COMMON_SH_LOADED:-}" ]; then
  return 0
fi
QUESTBOARD_COMMON_SH_LOADED=1

# The only keys the configuration file may set. Anything else is an error, so a
# typo or an injected line is noticed instead of silently ignored.
QUESTBOARD_CONF_ALLOWED_KEYS=(
  QUESTBOARD_GITHUB_REPO
  QUESTBOARD_SIGNER_WORKFLOW
  QUESTBOARD_NOTIFY_EMAIL
  QUESTBOARD_MAIL_FROM
  QUESTBOARD_SMTP_HOST
  QUESTBOARD_SMTP_PORT
  QUESTBOARD_KEEP_RELEASES
  QUESTBOARD_HEALTH_TIMEOUT_SECONDS
  QUESTBOARD_HEALTH_URL
)

QUESTBOARD_EMAIL_RE='^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+$'
QUESTBOARD_PLAIN_VERSION_RE='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
QUESTBOARD_STRICT_TAG_RE='^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
QUESTBOARD_UTC_STAMP_RE='^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$'
QUESTBOARD_BACKUP_NAME_RE='^questboard-premigration-[A-Za-z0-9._-]+-[0-9]{8}T[0-9]{6}Z\.bak$'

# Prints the current time as YYYY-MM-DDTHH:MM:SSZ in UTC.
questboard_utc_now() {
  date -u '+%Y-%m-%dT%H:%M:%SZ'
}

# Writes a timestamped line to stderr. journald captures stderr for services
# run by systemd; interactive runs simply see it on the terminal.
questboard_log() {
  printf '%s questboard-deploy: %s\n' "$(questboard_utc_now)" "$*" >&2
}

# Logs an error-prefixed message and exits non-zero.
questboard_die() {
  questboard_log "ERROR: $*"
  exit 1
}

# Creates each DIR with MODE, including when it already exists with another
# mode. Missing parent directories are created first and are always 755,
# whatever the caller's umask, so a private leaf never makes its parent
# unreadable. A plain mkdir -p -m would apply MODE to the leaf only, leave an
# existing directory untouched, and gives no say over the parents' mode.
# Usage: questboard_make_dir MODE DIR...
questboard_make_dir() {
  local mode="$1" dir
  shift
  for dir in "$@"; do
    ( umask 022 && mkdir -p "$(dirname "$dir")" )
    install -d -m "$mode" "$dir"
  done
}

# Checks one configuration value against the pattern for its key. Prints
# nothing; the exit status is the answer.
questboard__conf_value_ok() {
  local key="$1" value="$2" re
  case "$key" in
    QUESTBOARD_GITHUB_REPO)
      re='^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$'
      [[ "$value" =~ $re ]]
      ;;
    QUESTBOARD_SIGNER_WORKFLOW)
      re='^\.github/workflows/[A-Za-z0-9_.-]+\.ya?ml$'
      [[ "$value" =~ $re ]]
      ;;
    QUESTBOARD_NOTIFY_EMAIL)
      [ -z "$value" ] || [[ "$value" =~ $QUESTBOARD_EMAIL_RE ]]
      ;;
    QUESTBOARD_MAIL_FROM)
      [[ "$value" =~ $QUESTBOARD_EMAIL_RE ]]
      ;;
    QUESTBOARD_SMTP_HOST)
      re='^[A-Za-z0-9.-]+$'
      [[ "$value" =~ $re ]]
      ;;
    QUESTBOARD_SMTP_PORT)
      re='^[0-9]{1,5}$'
      [[ "$value" =~ $re ]] && (( 10#$value >= 1 && 10#$value <= 65535 ))
      ;;
    QUESTBOARD_KEEP_RELEASES)
      re='^[0-9]+$'
      [[ "$value" =~ $re ]] && [ "${#value}" -le 6 ] && (( 10#$value >= 2 ))
      ;;
    QUESTBOARD_HEALTH_TIMEOUT_SECONDS)
      re='^[0-9]+$'
      [[ "$value" =~ $re ]] && [ "${#value}" -le 6 ] && (( 10#$value >= 10 ))
      ;;
    QUESTBOARD_HEALTH_URL)
      re='^http://(127\.0\.0\.1|localhost)(:[0-9]{1,5})?/[A-Za-z0-9/_-]*$'
      [[ "$value" =~ $re ]]
      ;;
    *)
      return 1
      ;;
  esac
}

# Loads KEY=VALUE pairs from a configuration file into global shell variables
# without ever running the file as shell code. The file must be owned by the
# privileged user and must not be writable by group or others. Every line must
# be blank, a comment, or an allow-listed key whose value passes that key's
# pattern; any other line is an error. Values may be wrapped in one pair of
# single or double quotes, which are stripped before validation.
#
# Under a relocated test root (the DEPLOY_ROOT variable of the dispatcher is
# set) the expected owner is the current user rather than root, since the test
# root stands in for the privileged installation root and is never itself
# created by root. The dispatcher sets DEPLOY_ROOT only after it has checked
# that the root is a genuine test tree; nothing in this file reads the
# environment for it.
questboard_load_conf() {
  local conf_file="${1:-}"

  [ -n "$conf_file" ] && [ -f "$conf_file" ] \
    || questboard_die "configuration file not found: ${conf_file}"

  local expected_uid=0
  if [ -n "${DEPLOY_ROOT:-}" ]; then
    expected_uid="$(id -u)"
  fi

  local owner_uid mode
  read -r owner_uid mode < <(stat -c '%u %a' "$conf_file")
  if [ "$owner_uid" != "$expected_uid" ]; then
    questboard_die "configuration file ${conf_file} has an unexpected owner"
  fi

  # Only the last three mode digits matter here: user, group, others.
  mode="${mode: -3}"
  local group_digit="${mode:1:1}" other_digit="${mode:2:1}"
  if (( (8#$group_digit & 2) != 0 )) || (( (8#$other_digit & 2) != 0 )); then
    questboard_die "configuration file ${conf_file} must not be writable by group or others"
  fi

  local line key value candidate allowed
  while IFS= read -r line || [ -n "$line" ]; do
    if [[ "$line" =~ ^[[:space:]]*$ ]] || [[ "$line" =~ ^# ]]; then
      continue
    fi

    if ! [[ "$line" =~ ^([A-Z][A-Z0-9_]*)=(.*)$ ]]; then
      questboard_die "configuration file ${conf_file} has a line that is not KEY=VALUE"
    fi
    key="${BASH_REMATCH[1]}"
    value="${BASH_REMATCH[2]}"

    allowed=0
    for candidate in "${QUESTBOARD_CONF_ALLOWED_KEYS[@]}"; do
      if [ "$candidate" = "$key" ]; then
        allowed=1
        break
      fi
    done
    [ "$allowed" -eq 1 ] || questboard_die "configuration key ${key} is not recognised"

    if [[ "$value" =~ ^\"(.*)\"$ ]]; then
      value="${BASH_REMATCH[1]}"
    elif [[ "$value" =~ ^\'(.*)\'$ ]]; then
      value="${BASH_REMATCH[1]}"
    fi

    questboard__conf_value_ok "$key" "$value" \
      || questboard_die "configuration value for ${key} is not acceptable"

    printf -v "$key" '%s' "$value"
  done < "$conf_file"
}

# Compares two plain MAJOR.MINOR.PATCH versions numerically. Succeeds
# (returns 0) when A is strictly greater than B.
questboard_semver_gt() {
  local a="$1" b="$2"
  local a_major a_minor a_patch b_major b_minor b_patch

  IFS='.' read -r a_major a_minor a_patch <<< "$a"
  IFS='.' read -r b_major b_minor b_patch <<< "$b"

  if (( 10#$a_major != 10#$b_major )); then
    (( 10#$a_major > 10#$b_major ))
    return
  fi
  if (( 10#$a_minor != 10#$b_minor )); then
    (( 10#$a_minor > 10#$b_minor ))
    return
  fi
  (( 10#$a_patch > 10#$b_patch ))
}

# Succeeds for a release tag of exactly vMAJOR.MINOR.PATCH: no leading zeros,
# no pre-release or build suffix, a lower-case v.
questboard_is_strict_tag() {
  [[ "${1:-}" =~ $QUESTBOARD_STRICT_TAG_RE ]]
}

# Succeeds for the same shape without the leading v.
questboard_is_plain_version() {
  [[ "${1:-}" =~ $QUESTBOARD_PLAIN_VERSION_RE ]]
}

# Downloads URL to OUTFILE and prints the HTTP status code. The exit status is
# curl's own, so a non-zero status means the transport failed (no usable code),
# while an HTTP error status still returns zero and is left for the caller to
# judge. The optional HEADERFILE receives the response headers.
questboard_http_fetch() {
  local max_time="$1" url="$2" outfile="$3" headerfile="${4:-}"
  local args=(--silent --show-error --location --max-time "$max_time" --output "$outfile" --write-out '%{http_code}')
  if [ -n "$headerfile" ]; then
    args+=(--dump-header "$headerfile")
  fi
  curl "${args[@]}" "$url"
}

# Maps an outcome to the short label used in the mail subject and result line.
questboard_result_label() {
  case "${1:-}" in
    installed) printf 'installed' ;;
    refused) printf 'refused' ;;
    failed) printf 'failed' ;;
    failed_rolled_back) printf 'failed, rolled back' ;;
    rolled_back) printf 'rolled back' ;;
    halted) printf 'halted - migrations applied' ;;
    *) return 1 ;;
  esac
}

# Maps a reason code to fixed plain-English text. The vocabulary is closed on
# purpose: mail never carries free text, paths, host names or connection
# details, only one of these sentences.
questboard_reason_label() {
  case "${1:-}" in
    checksum_mismatch) printf 'the download did not match its checksum' ;;
    attestation_failed) printf 'the release signature check failed' ;;
    not_on_main) printf 'the release was not built from the main branch' ;;
    asset_missing) printf 'a required release file was missing' ;;
    invalid_content) printf 'the release contents were not valid' ;;
    database_ahead) printf 'the database is newer than this release' ;;
    non_transactional_migration) printf 'a migration cannot run safely in a transaction' ;;
    insufficient_disk) printf 'there was not enough disk space' ;;
    staging_failed) printf 'the release could not be put in place' ;;
    database_unreachable) printf 'the database could not be reached' ;;
    backup_failed) printf 'the pre-migration backup failed' ;;
    apply_failed) printf 'applying the migrations failed' ;;
    unhealthy) printf 'the new release did not become healthy' ;;
    restart_failed) printf 'the service could not be restarted' ;;
    *) return 1 ;;
  esac
}

# Prints a CRLF-terminated RFC 5322 outcome message. Every input is validated
# first; on any violation nothing is printed and 1 is returned. The body holds
# only the version, the result, UTC timestamps and, for a halted release, the
# backup file name.
questboard_render_mail() {
  local to="" from="" outcome="" version="" started="" finished=""
  local reason="" backup="" previous_healthy="" update_from=""

  while [ $# -gt 0 ]; do
    [ $# -ge 2 ] || return 1
    case "$1" in
      --to) to="$2" ;;
      --from) from="$2" ;;
      --outcome) outcome="$2" ;;
      --version) version="$2" ;;
      --started) started="$2" ;;
      --finished) finished="$2" ;;
      --reason) reason="$2" ;;
      --backup) backup="$2" ;;
      --previous-healthy) previous_healthy="$2" ;;
      --update-from) update_from="$2" ;;
      *) return 1 ;;
    esac
    shift 2
  done

  [[ "$to" =~ $QUESTBOARD_EMAIL_RE ]] || return 1
  [[ "$from" =~ $QUESTBOARD_EMAIL_RE ]] || return 1
  local result_label
  result_label="$(questboard_result_label "$outcome")" || return 1
  questboard_is_plain_version "$version" || return 1
  [[ "$started" =~ $QUESTBOARD_UTC_STAMP_RE ]] || return 1
  [[ "$finished" =~ $QUESTBOARD_UTC_STAMP_RE ]] || return 1

  local reason_label=""
  if [ -n "$reason" ]; then
    reason_label="$(questboard_reason_label "$reason")" || return 1
  fi
  if [ -n "$backup" ]; then
    [[ "$backup" =~ $QUESTBOARD_BACKUP_NAME_RE ]] || return 1
  fi
  if [ -n "$previous_healthy" ]; then
    case "$previous_healthy" in
      yes|no) ;;
      *) return 1 ;;
    esac
  fi
  if [ -n "$update_from" ]; then
    questboard_is_plain_version "$update_from" || return 1
  fi

  local result_line="$result_label"
  if [ -n "$reason_label" ]; then
    result_line="${result_label} (${reason_label})"
  fi

  local stamp
  stamp="$(date -u '+%Y%m%dT%H%M%SZ')"

  printf 'From: %s\r\n' "$from"
  printf 'To: %s\r\n' "$to"
  printf 'Subject: [questboard-deploy] %s v%s\r\n' "$result_label" "$version"
  printf 'Date: %s\r\n' "$(date -u -R)"
  printf 'Message-ID: <%s.%s.%s@questboard-deploy>\r\n' "$stamp" "$$" "$RANDOM"
  printf 'MIME-Version: 1.0\r\n'
  printf 'Content-Type: text/plain; charset=UTF-8\r\n'
  printf '\r\n'
  printf 'Version: %s\r\n' "$version"
  printf 'Result: %s\r\n' "$result_line"
  printf 'Started: %s\r\n' "$started"
  printf 'Finished: %s\r\n' "$finished"
  if [ -n "$backup" ] && [ "$outcome" = "halted" ]; then
    printf 'Backup: %s\r\n' "$backup"
  fi
  if [ -n "$previous_healthy" ]; then
    printf 'Previous release healthy: %s\r\n' "$previous_healthy"
  fi
  if [ -n "$update_from" ]; then
    printf 'Installer update available: run setup from release %s\r\n' "$update_from"
  fi
}

# Sends the rendered message in MSGFILE from FROM to TO through the SMTP relay
# at HOST and PORT. The machine's own mail transfer agent is deliberately not
# used. A failed send is logged and never changes the deploy result: this
# function always returns 0. Usage: questboard_send_mail MSGFILE TO FROM HOST PORT
questboard_send_mail() {
  local msgfile="${1:-}"
  local to="${2:-}"
  local from="${3:-}"
  local host="${4:-}"
  local port="${5:-}"

  if [ -z "$to" ]; then
    questboard_log "no notification recipient configured, skipping outcome mail"
    return 0
  fi
  if [ -z "$from" ] || [ -z "$host" ] || [ -z "$port" ]; then
    questboard_log "mail relay is not fully configured, skipping outcome mail"
    return 0
  fi
  if [ ! -f "$msgfile" ]; then
    questboard_log "outcome mail file is missing, skipping outcome mail"
    return 0
  fi

  if ! curl --silent --show-error --max-time 30 \
      --url "smtp://${host}:${port}/questboard-deploy" \
      --mail-from "$from" --mail-rcpt "$to" \
      --upload-file "$msgfile" >/dev/null 2>&1; then
    questboard_log "sending outcome mail failed"
  fi

  return 0
}
