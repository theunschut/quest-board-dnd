#!/usr/bin/env bash
# One-time (and safely repeatable) installation of the installer itself: the gh
# command line client from GitHub's apt repository, the installer and its
# libraries, the poll units, the drop-in for the application unit and the
# configuration file; and the adoption of the flat install that is running today
# into the versioned release layout. Sourced only by the dispatcher's setup
# command, never executed directly.
#
# The caller (the dispatcher) has already sourced common.sh, deploy.sh and
# release.sh and has set these globals: DEPLOY_ROOT, RELEASES_DIR, CURRENT_LINK,
# STATE_DIR, DOWNLOAD_DIR, CONF_PATH and APP_SERVICE. Every path is placed under
# DEPLOY_ROOT, which is empty in production and a temporary directory in tests.
#
# setup runs from a root shell and never from the sandboxed poll unit, so the
# poll unit needs no write access to /etc or /usr.

if [ -n "${QUESTBOARD_SETUP_SH_LOADED:-}" ]; then
  return 0
fi
QUESTBOARD_SETUP_SH_LOADED=1

# The fingerprint of the signing key GitHub publishes for its apt repository.
# Nothing is installed from that repository unless the downloaded keyring's
# only active primary key has exactly this fingerprint.
QUESTBOARD_GH_KEY_FINGERPRINT=7F38BBB59D064DBCB3D84D725612B36462313325
QUESTBOARD_GH_KEYRING_URL=https://cli.github.com/packages/githubcli-archive-keyring.gpg
QUESTBOARD_GH_KEYRING_PATH=/usr/share/keyrings/githubcli-archive-keyring.gpg
QUESTBOARD_GH_MIN_VERSION=2.49.0

# Set by questboard_setup_install_files.
SETUP_DROPIN_CHANGED=0
SETUP_CONFIG_CREATED=0

# Prints the fingerprint of every primary key in COLONS_TEXT (the output of
# `gpg --show-keys --with-colons`) that is neither expired nor revoked. The
# fingerprint of a primary key is the first fpr record after its pub record;
# later fpr records belong to subkeys and are skipped.
questboard_setup_active_key_fingerprints() {
  printf '%s\n' "${1:-}" | awk -F: '
    $1 == "pub" {
      active = ($2 == "e" || $2 == "r" || $2 == "i" || $2 == "d") ? 0 : 1
      want = 1
      next
    }
    $1 == "fpr" && want {
      if (active) print $10
      want = 0
      next
    }
    $1 == "sub" { want = 0 }
  '
}

# Prints X.Y.Z read from the informational version ("X.Y.Z+<hex>") compiled into
# DLL. Fails when there is no such version or when the file carries more than
# one different one, because guessing which release is running would defeat the
# point of asking the operator to confirm it.
#
# The version is matched as the attribute record the compiler writes: the 01 00
# prolog, a one-byte length, the string itself and two zero bytes. The length
# byte must equal the string's length. A looser match that only looked at the
# byte before the version broke on a real build: a 46-character version string
# has the length byte 0x2E, which is ".".
questboard_setup_detect_flat_version() {
  local dll="${1:-}"
  [ -f "$dll" ] || return 1

  local found
  found="$(python3 - "$dll" <<'PY'
import re
import sys

with open(sys.argv[1], "rb") as handle:
    data = handle.read()

pattern = re.compile(rb"\x01\x00([\x01-\x7f])((\d+\.\d+\.\d+)\+[0-9a-f]{7,40})\x00\x00")
versions = sorted({
    match.group(3).decode("ascii")
    for match in pattern.finditer(data)
    if match.group(1)[0] == len(match.group(2))
})
if len(versions) != 1:
    sys.exit(1)
print(versions[0])
PY
  )" || return 1

  questboard_is_plain_version "$found" || return 1
  printf '%s\n' "$found"
}

# Prints the plain version reported by `gh --version`, or nothing when gh is
# missing or does not answer in the expected form.
questboard_setup__gh_version() {
  command -v gh >/dev/null 2>&1 || return 0
  local line
  # Take the first line in the shell rather than through head, so an early
  # close cannot fail the pipeline under pipefail.
  line="$(gh --version 2>/dev/null)" || return 0
  line="${line%%$'\n'*}"
  printf '%s\n' "$line" | sed -n 's/^gh version \([0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\).*/\1/p'
}

# Succeeds when the installed gh is at least the minimum version.
questboard_setup__gh_new_enough() {
  local have
  have="$(questboard_setup__gh_version)"
  [ -n "$have" ] || return 1
  ! questboard_semver_gt "$QUESTBOARD_GH_MIN_VERSION" "$have"
}

# Installs gh from GitHub's apt repository. Nothing on the machine changes
# until the keyring's active primary key matches the pinned fingerprint. No JSON
# tool is installed: the installer handles JSON with python3.
questboard_setup_install_gh() {
  if questboard_setup__gh_new_enough; then
    questboard_log "gh $(questboard_setup__gh_version) is already installed"
    return 0
  fi

  if ! command -v gpg >/dev/null 2>&1; then
    questboard_log "installing gnupg to check the repository key"
    # Refresh the lists first: on a host that has not run apt for a while the
    # cached lists name package versions the mirror no longer serves.
    apt-get update -qq || questboard_die "apt-get update failed"
    DEBIAN_FRONTEND=noninteractive apt-get install -y gnupg \
      || questboard_die "could not install gnupg"
  fi

  local tmp
  tmp="$(mktemp -d)" || questboard_die "could not create a temporary directory"
  # The directory is removed on every path out of this function.
  local keyring="${tmp}/keyring.gpg"

  if ! curl --fail --silent --show-error --location --max-time 60 \
      --output "$keyring" "$QUESTBOARD_GH_KEYRING_URL"; then
    rm -rf "$tmp"
    questboard_die "could not download the GitHub CLI keyring"
  fi

  local colons fingerprints
  colons="$(gpg --show-keys --with-colons "$keyring" 2>/dev/null)" || colons=""
  fingerprints="$(questboard_setup_active_key_fingerprints "$colons")"
  if [ "$fingerprints" != "$QUESTBOARD_GH_KEY_FINGERPRINT" ]; then
    rm -rf "$tmp"
    questboard_die "the GitHub CLI keyring does not carry the expected signing key; nothing was installed"
  fi

  local root="${DEPLOY_ROOT:-}"
  local keyring_dest="${root}${QUESTBOARD_GH_KEYRING_PATH}"
  local list_dest="${root}/etc/apt/sources.list.d/github-cli.list"
  mkdir -p "$(dirname "$keyring_dest")" "$(dirname "$list_dest")"
  install -m 0644 "$keyring" "$keyring_dest"
  rm -rf "$tmp"

  local arch
  arch="$(dpkg --print-architecture)" || questboard_die "could not determine the package architecture"
  printf 'deb [arch=%s signed-by=%s] https://cli.github.com/packages stable main\n' \
    "$arch" "$QUESTBOARD_GH_KEYRING_PATH" > "$list_dest"
  chmod 0644 "$list_dest"

  apt-get update -qq || questboard_die "apt-get update failed"
  DEBIAN_FRONTEND=noninteractive apt-get install -y gh || questboard_die "could not install gh"

  questboard_setup__gh_new_enough \
    || questboard_die "gh ${QUESTBOARD_GH_MIN_VERSION} or newer is required, but the installed gh is older or missing"
  questboard_log "installed gh $(questboard_setup__gh_version)"
}

# Copies the installer, its libraries, the units, the drop-in and, only when it
# does not exist yet, the configuration file from SOURCE_ROOT into place; creates
# the state and download directories; then loads the configuration so the health
# URL and timeout used later come from it. Sets SETUP_DROPIN_CHANGED and
# SETUP_CONFIG_CREATED to 0 or 1. An existing configuration file is never
# overwritten, and ownership changes happen only when running as root.
questboard_setup_install_files() {
  local source_root="$1"
  local root="${DEPLOY_ROOT:-}"
  local is_root=0
  [ "$(id -u)" -eq 0 ] && is_root=1

  SETUP_DROPIN_CHANGED=0
  SETUP_CONFIG_CREATED=0

  local bin_dir="${root}/usr/local/sbin"
  local lib_dir="${root}/usr/local/lib/questboard-deploy"
  local unit_dir="${root}/etc/systemd/system"
  local dropin_dir="${unit_dir}/questboard.service.d"

  mkdir -p "$bin_dir" "$lib_dir" "$unit_dir" "$dropin_dir"

  install -m 0755 "${source_root}/deploy/bin/questboard-deploy" "${bin_dir}/questboard-deploy"

  local src
  for src in "${source_root}"/deploy/lib/*.sh; do
    [ -f "$src" ] || continue
    install -m 0644 "$src" "${lib_dir}/$(basename "$src")"
  done

  local unit
  for unit in questboard-deploy-poll.service questboard-deploy-poll.timer; do
    [ -f "${source_root}/deploy/systemd/${unit}" ] \
      || questboard_die "the release does not contain ${unit}"
    install -m 0644 "${source_root}/deploy/systemd/${unit}" "${unit_dir}/${unit}"
  done

  local dropin_src="${source_root}/deploy/systemd/questboard.service.d/10-release-layout.conf"
  local dropin_dst="${dropin_dir}/10-release-layout.conf"
  [ -f "$dropin_src" ] || questboard_die "the release does not contain the application unit drop-in"
  if [ ! -f "$dropin_dst" ] || ! cmp -s "$dropin_src" "$dropin_dst"; then
    SETUP_DROPIN_CHANGED=1
  fi
  install -m 0644 "$dropin_src" "$dropin_dst"

  if [ "$is_root" -eq 1 ]; then
    chown root:root "${bin_dir}/questboard-deploy" "${lib_dir}" "${lib_dir}"/*.sh \
      "${unit_dir}/questboard-deploy-poll.service" "${unit_dir}/questboard-deploy-poll.timer" "$dropin_dst"
  fi

  if [ ! -e "$CONF_PATH" ]; then
    [ -f "${source_root}/deploy/deploy.conf.example" ] \
      || questboard_die "the release does not contain deploy.conf.example"
    mkdir -p "$(dirname "$CONF_PATH")"
    install -m 0600 "${source_root}/deploy/deploy.conf.example" "$CONF_PATH"
    chmod 600 "$CONF_PATH"
    if [ "$is_root" -eq 1 ]; then
      chown root:root "$CONF_PATH"
    fi
    SETUP_CONFIG_CREATED=1
    questboard_log "created ${CONF_PATH} from the example"
  fi

  questboard_make_dir 700 "$STATE_DIR" "$DOWNLOAD_DIR"
  mkdir -p "$RELEASES_DIR"

  # The same defaults the dispatcher applies, so a configuration that omits a
  # key behaves the same here as it does at poll time.
  : "${QUESTBOARD_HEALTH_URL:=http://127.0.0.1:5000/health}"
  : "${QUESTBOARD_HEALTH_TIMEOUT_SECONDS:=120}"
  : "${QUESTBOARD_KEEP_RELEASES:=5}"
  questboard_load_conf "$CONF_PATH"
}

# Makes the release directory root-owned and not writable by anyone else, so the
# application user can neither change code nor swap the current link.
questboard_setup__lock_down_opt() {
  local opt_dir="${DEPLOY_ROOT:-}/opt/questboard"
  chmod 755 "$opt_dir" "$RELEASES_DIR"
  if [ "$(id -u)" -eq 0 ]; then
    chown root:root "$opt_dir" "$RELEASES_DIR"
  fi
}

# Prints the version of a half-finished adoption: a release directory marked as
# adopted that has its application but no current link yet. Prints nothing when
# there is none.
questboard_setup__interrupted_adoption() {
  local dir name
  for dir in "${RELEASES_DIR}"/*; do
    [ -d "$dir" ] && [ ! -L "$dir" ] || continue
    name="$(basename "$dir")"
    questboard_is_plain_version "$name" || continue
    [ -f "${dir}/app/QuestBoard.Service.dll" ] || continue
    if [ "$(questboard_manifest_get "$dir" adopted 2>/dev/null || true)" = "true" ]; then
      printf '%s\n' "$name"
      return 0
    fi
  done
  return 0
}

# Moves the flat install into RELEASES_DIR/VERSION/app and makes it the current
# release. The application is stopped first. Every top-level entry except
# releases and current is moved, so a repeated run moves whatever is left, and
# the main assembly goes last: while it is still in the flat directory the
# adoption is known to be unfinished. The current link only appears after every
# entry has moved.
#
# This release predates attestation, so it is recorded as adopted rather than
# verified: it carries the same trust it had before the cutover and is not
# offered as a manual rollback target.
questboard_setup_adopt() {
  local version="$1"
  questboard_is_plain_version "$version" || questboard_die "refusing to adopt an invalid version"

  local opt_dir="${DEPLOY_ROOT:-}/opt/questboard"
  local release_dir="${RELEASES_DIR}/${version}"
  local app_dir="${release_dir}/app"

  systemctl stop "$APP_SERVICE" || questboard_die "could not stop ${APP_SERVICE}"

  mkdir -p "$app_dir"

  python3 - "${release_dir}/release-manifest.json" "$version" <<'PY'
import json
import sys

manifest = {
    "version": sys.argv[2],
    "commit": "",
    "healthVersionHeader": False,
    "adopted": True,
}
with open(sys.argv[1], "w", encoding="utf-8") as handle:
    json.dump(manifest, handle, separators=(",", ":"))
    handle.write("\n")
PY

  local entry name
  while IFS= read -r -d '' entry; do
    name="$(basename "$entry")"
    case "$name" in
      releases|current|QuestBoard.Service.dll) continue ;;
    esac
    mv -- "$entry" "${app_dir}/${name}"
  done < <(find "$opt_dir" -mindepth 1 -maxdepth 1 -print0)

  if [ -e "${opt_dir}/QuestBoard.Service.dll" ]; then
    mv -- "${opt_dir}/QuestBoard.Service.dll" "${app_dir}/QuestBoard.Service.dll"
  fi

  questboard_secure_tree "$release_dir"
  questboard_setup__lock_down_opt
  questboard_activate_release "$version" "$RELEASES_DIR" "$CURRENT_LINK" "$STATE_DIR"
  questboard_record_attempt "$STATE_DIR" "v${version}" adopted
  questboard_log "adopted the running install as release ${version}"
}

# Installs everything and brings the server into the versioned layout.
#
#   questboard_setup_main DEFAULT_SOURCE_ROOT [--from DIR] [--confirm-adopt-version X.Y.Z]
#
# Exit status: 0 complete and the poll timer enabled; 2 waiting for the operator
# to confirm the running version (nothing was changed); 3 waiting for the
# configuration file to be edited (the timer is not enabled); 1 on error.
questboard_setup_main() {
  local source_root="${1:-}"
  shift || true
  local confirm_version=""

  while [ $# -gt 0 ]; do
    case "$1" in
      --from)
        [ $# -ge 2 ] || questboard_die "--from needs a directory"
        source_root="$2"
        shift 2
        ;;
      --confirm-adopt-version)
        [ $# -ge 2 ] || questboard_die "--confirm-adopt-version needs a version"
        confirm_version="$2"
        shift 2
        ;;
      *)
        questboard_die "unknown setup argument: $1"
        ;;
    esac
  done

  if [ -n "$confirm_version" ] && ! questboard_is_plain_version "$confirm_version"; then
    questboard_die "--confirm-adopt-version expects a version like 1.2.3"
  fi

  if [ ! -f "${source_root}/deploy/bin/questboard-deploy" ] \
      || [ ! -f "${source_root}/deploy/lib/common.sh" ] \
      || [ ! -f "${source_root}/release-manifest.json" ]; then
    questboard_die "run setup from an unpacked, verified release"
  fi

  local root="${DEPLOY_ROOT:-}"
  local opt_dir="${root}/opt/questboard"

  local tool
  for tool in python3 curl unzip flock sha256sum systemctl systemd-run; do
    command -v "$tool" >/dev/null 2>&1 || questboard_die "required tool is missing: ${tool}"
  done
  [ -x "${root}/usr/bin/dotnet" ] || questboard_die "required tool is missing: /usr/bin/dotnet"
  [ -f "${root}/etc/systemd/system/questboard.service" ] \
    || questboard_die "create the base questboard.service first (docs/server-setup.md)"

  # Decide what the layout needs before anything on the machine is changed, so
  # a refusal to guess leaves the server exactly as it was.
  local layout="" detected="" adopt_version=""
  if [ -L "$CURRENT_LINK" ]; then
    layout="versioned"
  elif [ -f "${opt_dir}/QuestBoard.Service.dll" ]; then
    layout="flat"
    detected="$(questboard_setup_detect_flat_version "${opt_dir}/QuestBoard.Service.dll")" \
      || questboard_die "could not read exactly one running version from ${opt_dir}/QuestBoard.Service.dll"
    if [ "$confirm_version" != "$detected" ]; then
      if [ -n "$confirm_version" ]; then
        printf 'The version you gave (%s) does not match the running version (%s). Nothing was changed.\n' \
          "$confirm_version" "$detected"
      fi
      printf 'Running version detected: %s. Check it against the site footer, then run: questboard-deploy setup --confirm-adopt-version %s\n' \
        "$detected" "$detected"
      return 2
    fi
    adopt_version="$detected"
  else
    adopt_version="$(questboard_setup__interrupted_adoption)"
    if [ -n "$adopt_version" ]; then
      layout="resume"
    else
      layout="fresh"
    fi
  fi

  questboard_setup_install_gh
  questboard_setup_install_files "$source_root"

  local changed_app=0
  case "$layout" in
    versioned)
      questboard_log "already in the versioned layout"
      questboard_setup__lock_down_opt
      ;;
    flat|resume)
      questboard_setup_adopt "$adopt_version"
      changed_app=1
      ;;
    fresh)
      questboard_setup__lock_down_opt
      printf 'No release installed yet. After editing deploy.conf run: questboard-deploy install vX.Y.Z\n'
      ;;
  esac

  systemctl daemon-reload || questboard_die "systemctl daemon-reload failed"

  if [ "$layout" != "fresh" ]; then
    if [ "$changed_app" -eq 1 ]; then
      systemctl start "$APP_SERVICE" || questboard_die "could not start ${APP_SERVICE}"
    elif [ "$SETUP_DROPIN_CHANGED" -eq 1 ]; then
      systemctl restart "$APP_SERVICE" || questboard_die "could not restart ${APP_SERVICE}"
    fi

    if [ "$changed_app" -eq 1 ] || [ "$SETUP_DROPIN_CHANGED" -eq 1 ]; then
      local active header require_header=0
      active="$(questboard_active_version "$CURRENT_LINK")"
      header="$(questboard_manifest_get "$CURRENT_LINK" healthVersionHeader 2>/dev/null || true)"
      [ "$header" = "true" ] && require_header=1
      if ! questboard_wait_for_health "$QUESTBOARD_HEALTH_URL" "$active" \
          "$QUESTBOARD_HEALTH_TIMEOUT_SECONDS" "$require_header"; then
        questboard_die "${APP_SERVICE} did not become healthy after setup; check: journalctl -u ${APP_SERVICE}"
      fi
      questboard_log "${APP_SERVICE} is healthy on release ${active}"
    fi
  fi

  local recipient="${QUESTBOARD_NOTIFY_EMAIL:-}"
  if [ "$SETUP_CONFIG_CREATED" -eq 1 ] || [ -z "$recipient" ] || [[ "$recipient" == *@example.com ]]; then
    printf 'Edit %s (recipient, relay, sender), then run setup again.\n' "$CONF_PATH"
    return 3
  fi

  systemctl enable --now questboard-deploy-poll.timer \
    || questboard_die "could not enable the poll timer"

  printf 'Setup complete. The installer is in /usr/local/sbin/questboard-deploy and polls every 5 minutes.\n'
  printf 'For an immediate first install run: questboard-deploy install vX.Y.Z\n'
  return 0
}
