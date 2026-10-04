#!/usr/bin/env bash
# Decision and bookkeeping functions for the installer: the outcome table, the
# memory of tags already tried, atomic activation of a release, pruning of old
# releases and detection of a newer installer. Sourced, never executed
# directly. It sources nothing itself: the caller must already have sourced
# common.sh, whose logging and time helpers it uses.

if [ -n "${QUESTBOARD_DEPLOY_SH_LOADED:-}" ]; then
  return 0
fi
QUESTBOARD_DEPLOY_SH_LOADED=1

# Maps the stage an install reached and its result to exactly one outcome and
# the action to take next. Prints "OUTCOME ACTION". Returns 2 for any stage or
# result that is not in the table, so a caller bug is loud rather than a
# silent default.
#
#   STAGE     verify | content | status | backup | apply | health
#   RESULT    fail | ok | invalid | disk | unknown_applied | non_transactional | error
#   MIGRATED  1 when this install committed a migration, otherwise 0
#   HAS_PREVIOUS  1 when a previous release exists to go back to, otherwise 0
#
# Actions: keep (leave the active release alone), none (nothing to do),
# restart_previous (start the previous release again), switch_back (reactivate
# the previous release), leave (leave the new release active).
questboard_decide_outcome() {
  local stage="${1:-}" result="${2:-}" migrated="${3:-}" has_previous="${4:-}"

  case "${stage}:${result}" in
    verify:fail)
      printf 'refused keep\n' ;;
    content:invalid)
      printf 'refused keep\n' ;;
    content:disk)
      printf 'failed keep\n' ;;
    status:unknown_applied)
      printf 'refused keep\n' ;;
    status:non_transactional)
      printf 'refused keep\n' ;;
    status:error)
      printf 'failed keep\n' ;;
    backup:fail)
      printf 'failed keep\n' ;;
    apply:fail)
      # The migration ran inside one transaction and rolled back, so the old
      # schema is intact and the previous release can simply be started again.
      printf 'failed_rolled_back restart_previous\n' ;;
    health:ok)
      printf 'installed none\n' ;;
    health:fail)
      case "${migrated}:${has_previous}" in
        0:1)
          printf 'rolled_back switch_back\n' ;;
        0:0)
          printf 'failed leave\n' ;;
        1:0|1:1)
          # A committed migration makes the previous release unsafe to run
          # against the new schema, so the new release stays active. systemd
          # keeps restarting it in case the fault is transient.
          printf 'halted leave\n' ;;
        *)
          return 2 ;;
      esac
      ;;
    *)
      return 2 ;;
  esac
}

# Appends "TAG OUTCOME UTC" to STATE_DIR/attempts, creating the state directory
# (mode 700) when missing. The tag and outcome must be plain tokens because the
# file is read back line by line.
questboard_record_attempt() {
  local state_dir="$1" tag="$2" outcome="$3"

  [[ "$tag" =~ ^[A-Za-z0-9._+-]+$ ]] \
    || questboard_die "refusing to record an attempt for an unusual tag"
  case "$outcome" in
    installed|refused|failed|failed_rolled_back|rolled_back|halted|adopted|rolled_back_manual|abandoned) ;;
    *) questboard_die "refusing to record an unknown outcome: ${outcome}" ;;
  esac

  questboard_make_dir 700 "$state_dir"
  printf '%s %s %s\n' "$tag" "$outcome" "$(questboard_utc_now)" >> "${state_dir}/attempts"
}

# Prints the latest recorded outcome for TAG when it is one that later polls
# must skip: refused, failed, failed_rolled_back, rolled_back, halted or
# abandoned (the release an operator rolled away from by hand). Prints nothing
# for a tag never tried, one that last installed, or one whose last outcome is
# not a bad one (adopted, rolled_back_manual). An explicit later install
# therefore clears the memory simply by recording installed.
questboard_remembered_outcome() {
  local state_dir="$1" tag="$2"
  local attempts="${state_dir}/attempts"
  [ -f "$attempts" ] || return 0

  local latest
  latest="$(TAG="$tag" awk '$1 == ENVIRON["TAG"] { outcome = $2 } END { if (outcome != "") print outcome }' "$attempts")"
  case "$latest" in
    refused|failed|failed_rolled_back|rolled_back|halted|abandoned) printf '%s\n' "$latest" ;;
  esac
  return 0
}

# Prints the version the current link points at (the directory name), or
# nothing when there is no link.
questboard_active_version() {
  local current_link="$1"
  if [ -L "$current_link" ]; then
    basename "$(readlink -f "$current_link")"
  fi
  return 0
}

# Prints the version recorded as the previous release, or nothing.
questboard_previous_version() {
  local state_dir="$1"
  if [ -f "${state_dir}/previous" ]; then
    tr -d '[:space:]' < "${state_dir}/previous"
    printf '\n'
  fi
  return 0
}

# Atomically repoints CURRENT_LINK at RELEASES_DIR/VERSION. The version that
# was active is recorded in STATE_DIR/previous before the switch. The new link
# is built under a temporary name and moved into place with mv -T, so there is
# never a moment without a valid current link.
questboard_activate_release() {
  local version="$1" releases_dir="$2" current_link="$3" state_dir="$4"

  local target="${releases_dir}/${version}"
  [ -d "$target" ] || questboard_die "cannot activate ${version}: ${target} does not exist"

  questboard_make_dir 700 "$state_dir"

  local active
  active="$(questboard_active_version "$current_link")"
  if [ -n "$active" ] && [ "$active" != "$version" ]; then
    printf '%s\n' "$active" > "${state_dir}/previous"
  fi

  local tmp_link
  tmp_link="$(mktemp -u "${current_link}.XXXXXX")"
  ln -s "$target" "$tmp_link"
  mv -T "$tmp_link" "$current_link"
}

# Removes release directories beyond KEEP, oldest first, never removing the
# active or previous release even if that leaves more than KEEP on disk.
# Only directories named like a plain version are considered, so staging
# directories and anything foreign are left alone.
questboard_prune_releases() {
  local releases_dir="$1" current_link="$2" state_dir="$3" keep="$4"

  [[ "$keep" =~ ^[0-9]+$ ]] || questboard_die "retained release count must be a number"

  local active_version previous_version
  active_version="$(questboard_active_version "$current_link")"
  previous_version="$(questboard_previous_version "$state_dir")"

  local versions=() entry name
  for entry in "${releases_dir}"/*; do
    [ -d "$entry" ] && [ ! -L "$entry" ] || continue
    name="$(basename "$entry")"
    questboard_is_plain_version "$name" || continue
    versions+=("$name")
  done
  [ "${#versions[@]}" -gt 0 ] || return 0

  local sorted=()
  mapfile -t sorted < <(printf '%s\n' "${versions[@]}" | sort -V)

  local total="${#sorted[@]}"
  local to_delete_count=$(( total > keep ? total - keep : 0 ))
  [ "$to_delete_count" -gt 0 ] || return 0

  local deleted=0 v
  for v in "${sorted[@]}"; do
    [ "$deleted" -lt "$to_delete_count" ] || break
    if [ "$v" = "$active_version" ] || [ "$v" = "$previous_version" ]; then
      continue
    fi
    rm -rf "${releases_dir:?}/${v}"
    deleted=$((deleted + 1))
  done
  return 0
}

# Succeeds (0) when any installer file shipped in RELEASE_DIR/deploy differs
# from, or is missing in, the copy installed under INSTALL_ROOT; returns 1 when
# every shipped file matches. A release without a deploy directory (an adopted
# one) returns 1. This only detects: the running installer never rewrites
# itself, so an update is always a separate, deliberate setup run.
questboard_deploy_files_differ() {
  local release_dir="$1" install_root="$2"
  local shipped="${release_dir}/deploy"
  [ -d "$shipped" ] || return 1

  local src dst name

  src="${shipped}/bin/questboard-deploy"
  if [ -f "$src" ]; then
    dst="${install_root}/usr/local/sbin/questboard-deploy"
    [ -f "$dst" ] && cmp -s "$src" "$dst" || return 0
  fi

  for src in "${shipped}"/lib/*.sh; do
    [ -f "$src" ] || continue
    name="$(basename "$src")"
    dst="${install_root}/usr/local/lib/questboard-deploy/${name}"
    [ -f "$dst" ] && cmp -s "$src" "$dst" || return 0
  done

  for name in questboard-deploy-poll.service questboard-deploy-poll.timer; do
    src="${shipped}/systemd/${name}"
    [ -f "$src" ] || continue
    dst="${install_root}/etc/systemd/system/${name}"
    [ -f "$dst" ] && cmp -s "$src" "$dst" || return 0
  done

  src="${shipped}/systemd/questboard.service.d/10-release-layout.conf"
  if [ -f "$src" ]; then
    dst="${install_root}/etc/systemd/system/questboard.service.d/10-release-layout.conf"
    [ -f "$dst" ] && cmp -s "$src" "$dst" || return 0
  fi

  return 1
}
