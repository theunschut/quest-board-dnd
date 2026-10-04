#!/usr/bin/env bash
# Release handling for the installer: staging a verified archive into an
# immutable, root-owned release tree, reading its manifest, running the
# database migrator as the application user, small JSON helpers, and the health
# wait that confirms the new release is the one serving. Sourced, never executed
# directly; callers own `set -euo pipefail` and source common.sh first.

if [ -n "${QUESTBOARD_RELEASE_SH_LOADED:-}" ]; then
  return 0
fi
QUESTBOARD_RELEASE_SH_LOADED=1

QUESTBOARD_APP_USER=questboard

# Prints the value of KEY from RELEASE_DIR/release-manifest.json. Booleans print
# as true or false. Returns 1 when the file, the key or valid JSON is missing.
questboard_manifest_get() {
  local release_dir="$1" key="$2"
  local manifest="${release_dir}/release-manifest.json"

  [[ "$key" =~ ^[A-Za-z][A-Za-z0-9]*$ ]] || return 1
  [ -f "$manifest" ] || return 1

  python3 - "$manifest" "$key" <<'PY'
import json
import sys

try:
    with open(sys.argv[1], encoding="utf-8") as handle:
        data = json.load(handle)
except Exception:
    sys.exit(1)

if not isinstance(data, dict) or sys.argv[2] not in data:
    sys.exit(1)

value = data[sys.argv[2]]
if isinstance(value, bool):
    print("true" if value else "false")
elif isinstance(value, str):
    print(value)
elif value is None:
    sys.exit(1)
else:
    print(json.dumps(value))
PY
}

# Succeeds when the release carries a migrator to run.
questboard_release_has_migrator() {
  [ -f "${1}/migrator/QuestBoard.Migrator.dll" ]
}

# Prints the value of top-level KEY from the JSON text. Booleans print as true
# or false, a missing or null key prints nothing. Returns 1 when the text is not
# a JSON object.
questboard_json_get() {
  local json="$1" key="$2"

  printf '%s' "$json" | python3 -c '
import json
import sys

try:
    data = json.load(sys.stdin)
except Exception:
    sys.exit(1)

if not isinstance(data, dict):
    sys.exit(1)

value = data.get(sys.argv[1])
if isinstance(value, bool):
    print("true" if value else "false")
elif isinstance(value, str):
    print(value)
elif value is None:
    print("")
else:
    print(json.dumps(value))
' "$key"
}

# Prints how many items the list at top-level KEY holds; a missing or null key
# counts as an empty list. Returns 1 when the text is not a JSON object or the
# value is not a list.
questboard_json_list_length() {
  local json="$1" key="$2"

  printf '%s' "$json" | python3 -c '
import json
import sys

try:
    data = json.load(sys.stdin)
except Exception:
    sys.exit(1)

if not isinstance(data, dict):
    sys.exit(1)

value = data.get(sys.argv[1])
if value is None:
    print(0)
elif isinstance(value, list):
    print(len(value))
else:
    sys.exit(1)
' "$key"
}

# Makes a release tree safe to run from. The application user has to read its
# own code but must never be able to change it, so a compromised application
# cannot plant code that the next release switch would then run as part of an
# installed tree. Ownership moves to root only when running as root, which is
# the case in production and not in the offline tests.
questboard_secure_tree() {
  local dir="$1"

  if [ "$(id -u)" -eq 0 ]; then
    chown -R root:root "$dir"
  fi
  find "$dir" -type d -exec chmod 755 {} +
  find "$dir" -type f -exec chmod go-w,a+r {} +
}

# Removes a staging directory and returns the given status.
questboard__stage_fail() {
  local staging="$1" status="$2"
  rm -rf "$staging"
  return "$status"
}

# Stages the verified archive ZIP as RELEASES_DIR/VERSION. Returns 0 when it is
# staged, 3 when the archive content is not acceptable and 4 when there is not
# enough free disk space. Extraction happens in a staging directory beside the
# final one and is moved into place with a single rename, so a release directory
# is either complete and hardened or absent. Every failing path removes the
# staging directory.
questboard_stage_release() {
  local zip="$1" releases_dir="$2" version="$3"

  questboard_is_plain_version "$version" || questboard_die "refusing to stage an invalid version: ${version}"
  [ -f "$zip" ] || return 3
  mkdir -p "$releases_dir"

  local staging="${releases_dir}/.staging-${version}"
  local final="${releases_dir}/${version}"

  # The release the service is running from is never replaced underneath it.
  local active target
  active="$(readlink -f -- "${releases_dir}/../current" 2>/dev/null || true)"
  target="$(readlink -f -- "$final" 2>/dev/null || true)"
  if [ -n "$active" ] && [ -n "$target" ] && [ "$active" = "$target" ]; then
    questboard_die "release ${version} is the active release and cannot be restaged"
  fi

  rm -rf "$staging"

  # Extraction needs room for the unpacked tree plus headroom for the hardening
  # pass and for the release that is still running.
  local summary uncompressed avail
  summary="$(unzip -Zt "$zip" 2>/dev/null)" || return 3
  uncompressed="$(printf '%s\n' "$summary" | sed -n 's/.* \([0-9][0-9]*\) bytes uncompressed.*/\1/p' | tail -n 1)"
  [[ "$uncompressed" =~ ^[0-9]+$ ]] || return 3
  avail="$(df --output=avail -B1 "$releases_dir" 2>/dev/null | tail -n 1 | tr -d '[:space:]')"
  [[ "$avail" =~ ^[0-9]+$ ]] || return 4
  if [ "$avail" -lt $(( uncompressed * 2 )) ]; then
    return 4
  fi

  # Entry names are judged before anything is written to disk.
  local entries entry
  entries="$(unzip -Z1 "$zip" 2>/dev/null)" || return 3
  [ -n "$entries" ] || return 3
  while IFS= read -r entry; do
    case "$entry" in
      /*|*\\*) return 3 ;;
    esac
    if [[ "$entry" =~ (^|/)\.\.(/|$) ]]; then
      return 3
    fi
  done <<< "$entries"

  if ! ( umask 022 && unzip -q -d "$staging" "$zip" ) >/dev/null 2>&1; then
    questboard__stage_fail "$staging" 3
    return
  fi

  if [ -n "$(find "$staging" -type l -print -quit)" ]; then
    questboard__stage_fail "$staging" 3
    return
  fi

  local manifest_version header_flag
  manifest_version="$(questboard_manifest_get "$staging" version)" || manifest_version=""
  header_flag="$(questboard_manifest_get "$staging" healthVersionHeader)" || header_flag=""
  if [ "$manifest_version" != "$version" ] || [ "$header_flag" != "true" ]; then
    questboard__stage_fail "$staging" 3
    return
  fi

  local required
  for required in app/QuestBoard.Service.dll migrator/QuestBoard.Migrator.dll deploy/bin/questboard-deploy; do
    if [ ! -f "${staging}/${required}" ]; then
      questboard__stage_fail "$staging" 3
      return
    fi
  done

  questboard_secure_tree "$staging"

  rm -rf "$final"
  mv -T "$staging" "$final"
}

# Runs one migrator subcommand (status, backup or apply) as the application
# user and returns the migrator's own exit code; its JSON line passes through on
# stdout. A refused argument returns 64 without starting anything.
#
# The environment file is handed to systemd, which reads it itself and starts
# the process as the application user. It is parsed exactly as it is for the
# application's own unit, and the secret in it never passes through this script,
# a command line or the journal. The hardening properties give the short-lived
# process only what a database client needs: no new privileges, no capabilities,
# a read-only filesystem apart from a private temp directory, and a hard
# runtime cap.
questboard_run_migrator() {
  local release_dir="$1" env_file="$2" subcommand="$3"
  shift 3

  local path_re='^/[A-Za-z0-9._/-]+$'
  [[ "$release_dir" =~ $path_re ]] || return 64
  [[ "$env_file" =~ $path_re ]] || return 64

  local label=""
  case "$subcommand" in
    status|apply)
      [ $# -eq 0 ] || return 64
      ;;
    backup)
      [ $# -eq 2 ] && [ "$1" = "--label" ] || return 64
      [[ "$2" =~ ^[A-Za-z0-9._-]{1,64}$ ]] || return 64
      label="$2"
      ;;
    *)
      return 64
      ;;
  esac

  local args=(
    --quiet --pipe --wait --collect
    "--uid=${QUESTBOARD_APP_USER}" "--gid=${QUESTBOARD_APP_USER}"
    "--working-directory=${release_dir}/migrator"
    "--property=EnvironmentFile=${env_file}"
    --property=NoNewPrivileges=yes
    --property=PrivateTmp=yes
    --property=ProtectSystem=strict
    --property=ProtectHome=yes
    --property=ProtectKernelTunables=yes
    --property=ProtectKernelModules=yes
    --property=ProtectControlGroups=yes
    --property=RestrictNamespaces=yes
    --property=LockPersonality=yes
    --property=CapabilityBoundingSet=
    "--property=RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6"
    --property=RuntimeMaxSec=1800
    /usr/bin/dotnet "${release_dir}/migrator/QuestBoard.Migrator.dll" "$subcommand"
  )
  if [ -n "$label" ]; then
    args+=(--label "$label")
  fi

  local rc=0
  systemd-run "${args[@]}" || rc=$?
  return "$rc"
}

# Waits up to TIMEOUT seconds for URL to report a healthy service. Healthy means
# HTTP 200 with a body of Healthy or Degraded and, when REQUIRE_HEADER is 1, an
# X-QuestBoard-Version response header equal to VERSION, so a still-running old
# release or a stale process is never mistaken for the new one. A degraded
# service passes with one warning. Returns 0 when healthy and 1 on timeout.
questboard_wait_for_health() {
  local url="$1" version="$2" timeout="$3" require_header="$4"

  [[ "$timeout" =~ ^[0-9]+$ ]] || return 1

  local work
  work="$(mktemp -d)" || return 1
  local body_file="${work}/body" header_file="${work}/headers"

  local deadline=$(( SECONDS + timeout ))
  local code rc body line value seen
  while true; do
    : > "$body_file"
    : > "$header_file"
    rc=0
    code="$(questboard_http_fetch 5 "$url" "$body_file" "$header_file")" || rc=$?

    if [ "$rc" -eq 0 ] && [ "$code" = "200" ]; then
      body="$(tr -d '[:space:]' < "$body_file")"
      if [ "$body" = "Healthy" ] || [ "$body" = "Degraded" ]; then
        seen=0
        if [ "$require_header" = "1" ]; then
          while IFS= read -r line; do
            line="${line%$'\r'}"
            if [[ "${line,,}" == x-questboard-version:* ]]; then
              value="${line#*:}"
              value="${value#"${value%%[![:space:]]*}"}"
              value="${value%"${value##*[![:space:]]}"}"
              if [ "$value" = "$version" ]; then
                seen=1
              else
                seen=0
              fi
            fi
          done < "$header_file"
        else
          seen=1
        fi

        if [ "$seen" -eq 1 ]; then
          if [ "$body" = "Degraded" ]; then
            questboard_log "WARNING: service reports Degraded"
          fi
          rm -rf "$work"
          return 0
        fi
      fi
    fi

    if [ "$SECONDS" -ge "$deadline" ]; then
      break
    fi
    sleep 2
  done

  rm -rf "$work"
  return 1
}
