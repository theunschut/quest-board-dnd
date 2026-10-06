#!/usr/bin/env bash
# Trust decisions for a downloaded release: checksum, signed build provenance,
# ancestry on the main branch, and the public calls that find and fetch a
# release. Sourced, never executed directly; callers own `set -euo pipefail`
# and source common.sh first.
#
# Every failure here stops the install. There is no switch, variable or fallback
# that lets an unverified release through, and nothing in this file sends a
# credential of any kind.

if [ -n "${QUESTBOARD_VERIFY_SH_LOADED:-}" ]; then
  return 0
fi
QUESTBOARD_VERIFY_SH_LOADED=1

QUESTBOARD_GITHUB_API_URL='https://api.github.com'
QUESTBOARD_GITHUB_DOWNLOAD_URL='https://github.com'

# What `gh attestation verify` needs to reach to fetch its trusted roots, probed
# only after gh has already failed. Fixed here and never read from the
# environment or the configuration file.
QUESTBOARD_SIGSTORE_TUF_URL='https://tuf-repo-cdn.sigstore.dev/'
QUESTBOARD_GITHUB_TUF_URL='https://tuf-repo.github.com/'

# Returned by questboard_verify_attestation, instead of 1, only when gh failed
# and a connectivity probe then showed the verification services unreachable.
# Nothing was judged about the artifact. It is still a non-zero status, so a
# caller that does not know about it treats it as a refusal.
QUESTBOARD_VERIFY_UNREACHABLE=3

# Returned by questboard_commit_on_branch, instead of 1, when GitHub gave no
# usable answer (no response, the rate limit, a server error). Like the
# unreachable status it is non-zero, so a caller that does not know about it
# treats it as a refusal.
QUESTBOARD_COMMIT_CHECK_NO_VERDICT=3

# How long one `gh attestation verify` run may take, and how much longer a run
# that ignores the polite stop gets before it is killed. gh fetches its trust
# root and the attestation over the network; without a bound a stalled call
# would hold the installer's lock for good, so no later poll or manual run could
# ever start. Fixed here and never read from the environment or the
# configuration file.
QUESTBOARD_GH_VERIFY_TIMEOUT_SECONDS=120
QUESTBOARD_GH_VERIFY_KILL_AFTER_SECONDS=10

QUESTBOARD_SHA256_LINE_RE='^[0-9a-f]{64} [ *][A-Za-z0-9._-]+$'
QUESTBOARD_COMMIT_SHA_RE='^[0-9a-f]{40}$'
QUESTBOARD_REPO_RE='^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$'
QUESTBOARD_REF_NAME_RE='^[A-Za-z0-9_./-]+$'
QUESTBOARD_ASSET_NAME_RE='^[A-Za-z0-9._-]+$'

# Checks DIR/ZIP_NAME.sha256 against DIR/ZIP_NAME. The checksum file must be
# exactly one line in sha256sum format naming that very zip, so a file that
# vouches for something else, or for several things, never passes. Returns 0 on
# a match and 1 on any mismatch or malformed file.
questboard_verify_checksum() {
  local dir="$1" zip_name="$2"
  local sum_file="${dir}/${zip_name}.sha256"

  [[ "$zip_name" =~ $QUESTBOARD_ASSET_NAME_RE ]] || return 1
  [ -f "$sum_file" ] && [ -f "${dir}/${zip_name}" ] || return 1

  local line_count
  line_count="$(wc -l < "$sum_file")" || return 1
  # Counts newline characters: the file must be exactly one terminated line.
  # Callers read this status through `if`, which switches errexit off, so a
  # count that could not be read must end the check itself.
  line_count="${line_count//[[:space:]]/}"
  if [ "$line_count" != "1" ]; then
    return 1
  fi

  local line
  line="$(head -n 1 "$sum_file")" || return 1
  [[ "$line" =~ $QUESTBOARD_SHA256_LINE_RE ]] || return 1
  [ "${line:66}" = "$zip_name" ] || return 1

  ( cd "$dir" && sha256sum --check --strict --status "${zip_name}.sha256" ) || return 1
  return 0
}

# Succeeds (0) when at least one of the services gh needs for verification
# cannot be reached at all: DNS, connection or timeout failure. Any HTTP answer,
# whatever its status, counts as reachable. Fails (1) when every probe got an
# answer. The probes carry no credential and are only meaningful after gh has
# failed, to tell an outage apart from a rejected artifact.
questboard_verification_services_unreachable() {
  local url
  for url in "$QUESTBOARD_SIGSTORE_TUF_URL" "$QUESTBOARD_GITHUB_TUF_URL" "${QUESTBOARD_GITHUB_API_URL}/"; do
    if ! questboard_http_fetch 10 "$url" /dev/null >/dev/null 2>&1; then
      return 0
    fi
  done
  return 1
}

# Verifies the signed provenance of ARTIFACT from BUNDLE, pinned to the
# repository, the signing workflow and the release tag ref, and prints the
# 40-character commit the artifact was built from. Returns 1 on any failure and
# 3 (QUESTBOARD_VERIFY_UNREACHABLE) when gh failed (including by running past
# its time bound) and the services it needs were then found unreachable, so that
# no verdict on the artifact exists. A gh run that stalls while the services
# answer is a refusal.
#
# Verification needs the Sigstore trust root, which gh fetches over the network
# into a cache directory, and it needs somewhere writable for its own state.
# The service unit's home directory is read-only, so gh gets a private temp
# directory for all of it, and every token variable is removed so the check can
# never lean on a credential. There is deliberately no way to proceed without a
# successful check: an unreachable service yields 3, which is a non-zero status
# like 1, and the artifact is not installed either way.
questboard_verify_attestation() {
  local artifact="$1" bundle="$2" repo="$3" signer_workflow="$4" source_ref="$5"

  [[ "$repo" =~ $QUESTBOARD_REPO_RE ]] || return 1
  [ -f "$artifact" ] && [ -f "$bundle" ] || return 1

  local work
  work="$(mktemp -d)" || return 1

  # The result is whatever gh prints on stdout. Anything it says on stderr (a
  # notice, a deprecation warning, a trust-root refresh message) is kept apart
  # so it can neither spoil a valid result nor pass for one.
  local output="" errtext="" rc=0
  env -u GH_TOKEN -u GITHUB_TOKEN -u GH_ENTERPRISE_TOKEN -u GITHUB_ENTERPRISE_TOKEN -u GH_HOST \
      GH_CONFIG_DIR="${work}/config" \
      XDG_CONFIG_HOME="${work}/config" \
      XDG_CACHE_HOME="${work}/cache" \
      XDG_STATE_HOME="${work}/state" \
      GH_TELEMETRY=false GH_NO_UPDATE_NOTIFIER=1 GH_PROMPT_DISABLED=1 \
      timeout --kill-after="${QUESTBOARD_GH_VERIFY_KILL_AFTER_SECONDS}" "${QUESTBOARD_GH_VERIFY_TIMEOUT_SECONDS}" \
      gh attestation verify "$artifact" \
        --bundle "$bundle" \
        --repo "$repo" \
        --signer-workflow "${repo}/${signer_workflow}" \
        --source-ref "$source_ref" \
        --deny-self-hosted-runners \
        --format json >"${work}/stdout" 2>"${work}/stderr" || rc=$?

  output="$(cat "${work}/stdout" 2>/dev/null)" || output=""
  errtext="$(head -c 4000 "${work}/stderr" 2>/dev/null)" || errtext=""
  rm -rf "$work"

  if [ "$rc" -ne 0 ]; then
    # timeout exits 124 when it stopped gh and 137 when it had to kill it. Either
    # way gh gave no answer, which is judged below like any other failed run.
    if [ "$rc" -eq 124 ] || [ "$rc" -eq 137 ]; then
      errtext="gh did not finish within ${QUESTBOARD_GH_VERIFY_TIMEOUT_SECONDS} seconds. ${errtext}"
    fi
    if questboard_verification_services_unreachable; then
      questboard_log "attestation verification could not run, the verification services are unreachable: ${errtext}"
      return "$QUESTBOARD_VERIFY_UNREACHABLE"
    fi
    questboard_log "attestation verification failed: ${errtext}"
    return 1
  fi

  local digest
  digest="$(printf '%s' "$output" | python3 -c '
import json, sys
try:
    data = json.load(sys.stdin)
    print(data[0]["verificationResult"]["signature"]["certificate"]["sourceRepositoryDigest"])
except Exception:
    sys.exit(1)
' 2>/dev/null)" || return 1

  [[ "$digest" =~ $QUESTBOARD_COMMIT_SHA_RE ]] || return 1
  printf '%s\n' "$digest"
}

# Asks the public compare endpoint, with no credential, whether commit SHA is
# identical to or behind BRANCH of REPO. A tag on some other branch can be built
# by the same signing workflow, so the attested commit must be part of main's
# history. Returns:
#   0  the commit is identical to or behind the branch
#   1  a definite refusal: the answer says the commit is ahead of or diverged
#      from the branch, GitHub does not know the commit (404), the answer was a
#      client error, or the answer could not be read
#   3  no verdict (QUESTBOARD_COMMIT_CHECK_NO_VERDICT): the request got no answer
#      at all, or an answer that says nothing about the commit (a 403 or 429 from
#      the unauthenticated rate limit, a 5xx). The caller retries later.
# Anything not named above, including invalid input, is a refusal.
questboard_commit_on_branch() {
  local repo="$1" sha="$2" branch="$3"

  [[ "$sha" =~ $QUESTBOARD_COMMIT_SHA_RE ]] || return 1
  [[ "$repo" =~ $QUESTBOARD_REPO_RE ]] || return 1
  [[ "$branch" =~ $QUESTBOARD_REF_NAME_RE ]] || return 1

  local body code="" rc=0 status
  body="$(mktemp)" || return 1
  code="$(questboard_http_fetch 30 "${QUESTBOARD_GITHUB_API_URL}/repos/${repo}/compare/${branch}...${sha}" "$body")" || rc=$?

  if [ "$rc" -ne 0 ]; then
    rm -f "$body"
    questboard_log "the main-branch check got no answer from GitHub"
    return "$QUESTBOARD_COMMIT_CHECK_NO_VERDICT"
  fi
  case "$code" in
    200) ;;
    403|429|5[0-9][0-9])
      rm -f "$body"
      questboard_log "the main-branch check got HTTP ${code} from GitHub, which says nothing about the commit"
      return "$QUESTBOARD_COMMIT_CHECK_NO_VERDICT"
      ;;
    *)
      rm -f "$body"
      return 1
      ;;
  esac

  status="$(python3 -c '
import json, sys
try:
    print(json.load(open(sys.argv[1])).get("status", ""))
except Exception:
    sys.exit(1)
' "$body" 2>/dev/null)" || status=""
  rm -f "$body"

  case "$status" in
    identical|behind) return 0 ;;
    *) return 1 ;;
  esac
}

# Prints the tag of the newest published release of REPO, read from the public
# latest-release endpoint in one request with no credential. Returns 0 when a
# tag was printed, 3 when no release is published (404) and 1 when GitHub could
# not be reached or answered anything else. The caller treats 1 as "try again
# next time", never as a refusal.
questboard_fetch_latest_tag() {
  local repo="$1"

  [[ "$repo" =~ $QUESTBOARD_REPO_RE ]] || return 1

  local body code="" rc=0
  body="$(mktemp)" || return 1
  code="$(questboard_http_fetch 30 "${QUESTBOARD_GITHUB_API_URL}/repos/${repo}/releases/latest" "$body")" || rc=$?

  if [ "$rc" -ne 0 ]; then
    rm -f "$body"
    return 1
  fi
  if [ "$code" = "404" ]; then
    rm -f "$body"
    return 3
  fi
  if [ "$code" != "200" ]; then
    rm -f "$body"
    return 1
  fi

  local tag
  tag="$(python3 -c '
import json, sys
try:
    value = json.load(open(sys.argv[1])).get("tag_name", "")
    print(value if isinstance(value, str) else "")
except Exception:
    sys.exit(1)
' "$body" 2>/dev/null)" || tag=""
  rm -f "$body"

  [ -n "$tag" ] || return 1
  printf '%s\n' "$tag"
}

# Downloads release asset ASSET of TAG in REPO to DEST. Returns 0 when it was
# fetched (200), 2 when the asset does not exist (404) and 1 for a transport
# failure or any other status. A partial DEST is never left behind.
questboard_download_asset() {
  local repo="$1" tag="$2" asset="$3" dest="$4"

  [[ "$repo" =~ $QUESTBOARD_REPO_RE ]] || return 1
  [[ "$tag" =~ $QUESTBOARD_ASSET_NAME_RE ]] || return 1
  [[ "$asset" =~ $QUESTBOARD_ASSET_NAME_RE ]] || return 1

  local code="" rc=0
  code="$(questboard_http_fetch 300 "${QUESTBOARD_GITHUB_DOWNLOAD_URL}/${repo}/releases/download/${tag}/${asset}" "$dest")" || rc=$?

  if [ "$rc" -eq 0 ] && [ "$code" = "200" ]; then
    return 0
  fi

  rm -f "$dest"
  if [ "$rc" -eq 0 ] && [ "$code" = "404" ]; then
    return 2
  fi
  return 1
}
