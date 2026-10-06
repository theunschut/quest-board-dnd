#!/usr/bin/env bash
# Proves the verification library: the checksum check, the pinned attestation
# call with its isolated gh environment, the main-branch ancestry check, and the
# latest-tag and asset download calls. Needs no root, network, systemd or
# database: curl and gh are recording stand-ins first on PATH.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

export QUESTBOARD_DEPLOY_ROOT
QUESTBOARD_DEPLOY_ROOT="$(mktemp -d)"
trap 'rm -rf "${QUESTBOARD_DEPLOY_ROOT}"' EXIT
# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$QUESTBOARD_DEPLOY_ROOT"

STUB_DIR="${QUESTBOARD_DEPLOY_ROOT}/stubs"
mkdir -p "$STUB_DIR"
export STUB_CURL_LOG="${STUB_DIR}/curl-args.log"
export STUB_GH_LOG="${STUB_DIR}/gh-args.log"
export STUB_GH_ENV_LOG="${STUB_DIR}/gh-env.log"

# A stand-in for curl: records every call's arguments (one per line, calls
# separated by a marker), writes STUB_BODY_FILE to the --output path, prints
# STUB_HTTP_CODE and exits with STUB_CURL_EXIT, or with 6 when the requested URL
# (the last argument) contains STUB_CURL_FAIL_MATCH.
cat > "${STUB_DIR}/curl" <<'EOF'
#!/bin/sh
out=""
prev=""
printf '%s\n' '--call--' >> "$STUB_CURL_LOG"
for arg in "$@"; do
  printf '%s\n' "$arg" >> "$STUB_CURL_LOG"
  if [ "$prev" = "--output" ]; then out="$arg"; fi
  prev="$arg"
done
if [ -n "${STUB_CURL_FAIL_MATCH:-}" ]; then
  case "$prev" in *"$STUB_CURL_FAIL_MATCH"*) exit 6 ;; esac
fi
if [ -n "$out" ] && [ -n "${STUB_BODY_FILE:-}" ]; then
  cp "$STUB_BODY_FILE" "$out"
fi
printf '%s' "${STUB_HTTP_CODE:-200}"
exit "${STUB_CURL_EXIT:-0}"
EOF

# A stand-in for gh: records its arguments and the environment that matters,
# proves its cache and state directories are writable, prints STUB_GH_JSON on
# stdout and STUB_GH_STDERR (when set) on stderr, then exits with STUB_GH_EXIT.
# STUB_GH_HANG_SECONDS makes it sleep first, standing in for a stalled network call.
cat > "${STUB_DIR}/gh" <<'EOF'
#!/bin/sh
: > "$STUB_GH_LOG"
for arg in "$@"; do
  printf '%s\n' "$arg" >> "$STUB_GH_LOG"
done
{
  printf 'GH_TOKEN=%s\n' "${GH_TOKEN-unset}"
  printf 'GITHUB_TOKEN=%s\n' "${GITHUB_TOKEN-unset}"
  printf 'GH_ENTERPRISE_TOKEN=%s\n' "${GH_ENTERPRISE_TOKEN-unset}"
  printf 'GH_HOST=%s\n' "${GH_HOST-unset}"
  printf 'GH_CONFIG_DIR=%s\n' "${GH_CONFIG_DIR-unset}"
  printf 'XDG_CACHE_HOME=%s\n' "${XDG_CACHE_HOME-unset}"
  printf 'XDG_STATE_HOME=%s\n' "${XDG_STATE_HOME-unset}"
  printf 'GH_TELEMETRY=%s\n' "${GH_TELEMETRY-unset}"
  printf 'GH_PROMPT_DISABLED=%s\n' "${GH_PROMPT_DISABLED-unset}"
  printf 'GH_NO_UPDATE_NOTIFIER=%s\n' "${GH_NO_UPDATE_NOTIFIER-unset}"
} > "$STUB_GH_ENV_LOG"
if mkdir -p "$XDG_CACHE_HOME/gh" "$XDG_STATE_HOME/gh" "$GH_CONFIG_DIR" \
    && : > "$XDG_CACHE_HOME/gh/probe" && : > "$XDG_STATE_HOME/gh/probe"; then
  printf 'WRITABLE=yes\n' >> "$STUB_GH_ENV_LOG"
else
  printf 'WRITABLE=no\n' >> "$STUB_GH_ENV_LOG"
fi
if [ -n "${STUB_GH_HANG_SECONDS:-}" ]; then
  exec sleep "$STUB_GH_HANG_SECONDS"
fi
if [ -n "${STUB_GH_STDERR:-}" ]; then
  printf '%s\n' "$STUB_GH_STDERR" >&2
fi
printf '%s' "${STUB_GH_JSON:-}"
exit "${STUB_GH_EXIT:-0}"
EOF
chmod +x "${STUB_DIR}/curl" "${STUB_DIR}/gh"
export PATH="${STUB_DIR}:${PATH}"

# shellcheck source=deploy/lib/common.sh
source "${REPO_ROOT}/deploy/lib/common.sh"
# shellcheck source=deploy/lib/verify.sh
source "${REPO_ROOT}/deploy/lib/verify.sh"

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

curl_calls() {
  if [ -f "$STUB_CURL_LOG" ]; then
    grep -c -x -- '--call--' "$STUB_CURL_LOG" || true
  else
    printf '0'
  fi
}

reset_stubs() {
  rm -f "$STUB_CURL_LOG" "$STUB_GH_LOG" "$STUB_GH_ENV_LOG"
  unset STUB_HTTP_CODE STUB_CURL_EXIT STUB_CURL_FAIL_MATCH STUB_BODY_FILE STUB_GH_EXIT STUB_GH_JSON \
    STUB_GH_STDERR STUB_GH_HANG_SECONDS
}

WORK="${QUESTBOARD_DEPLOY_ROOT}/work"
mkdir -p "$WORK"

SHA="0123456789abcdef0123456789abcdef01234567"

# --- questboard_verify_checksum -------------------------------------------

ZIP_DIR="${WORK}/zipdir"
mkdir -p "$ZIP_DIR"
printf 'hello release\n' > "${WORK}/payload.txt"
( cd "$WORK" && zip -q "${ZIP_DIR}/questboard-v1.2.3.zip" payload.txt )
( cd "$ZIP_DIR" && sha256sum questboard-v1.2.3.zip > questboard-v1.2.3.zip.sha256 )

check "checksum passes for a matching zip" "0" \
  "$(status_of questboard_verify_checksum "$ZIP_DIR" questboard-v1.2.3.zip)"

# Flip one byte in the middle of a copy and keep the original checksum.
FLIP_DIR="${WORK}/flipdir"
mkdir -p "$FLIP_DIR"
cp "${ZIP_DIR}/questboard-v1.2.3.zip" "${ZIP_DIR}/questboard-v1.2.3.zip.sha256" "$FLIP_DIR/"
python3 - "${FLIP_DIR}/questboard-v1.2.3.zip" <<'PY'
import sys
path = sys.argv[1]
data = bytearray(open(path, "rb").read())
data[len(data) // 2] ^= 0x01
open(path, "wb").write(bytes(data))
PY
check "checksum fails when one byte of the zip changed" "1" \
  "$(status_of questboard_verify_checksum "$FLIP_DIR" questboard-v1.2.3.zip)"

OTHER_DIR="${WORK}/otherdir"
mkdir -p "$OTHER_DIR"
cp "${ZIP_DIR}/questboard-v1.2.3.zip" "$OTHER_DIR/"
( cd "$ZIP_DIR" && sha256sum questboard-v1.2.3.zip | sed 's/questboard-v1.2.3.zip/questboard-v9.9.9.zip/' ) \
  > "${OTHER_DIR}/questboard-v1.2.3.zip.sha256"
check "checksum fails when the file names another zip" "1" \
  "$(status_of questboard_verify_checksum "$OTHER_DIR" questboard-v1.2.3.zip)"

TWO_DIR="${WORK}/twodir"
mkdir -p "$TWO_DIR"
cp "${ZIP_DIR}/questboard-v1.2.3.zip" "$TWO_DIR/"
{
  cat "${ZIP_DIR}/questboard-v1.2.3.zip.sha256"
  cat "${ZIP_DIR}/questboard-v1.2.3.zip.sha256"
} > "${TWO_DIR}/questboard-v1.2.3.zip.sha256"
check "checksum fails when the file has two lines" "1" \
  "$(status_of questboard_verify_checksum "$TWO_DIR" questboard-v1.2.3.zip)"

NONHEX_DIR="${WORK}/nonhexdir"
mkdir -p "$NONHEX_DIR"
cp "${ZIP_DIR}/questboard-v1.2.3.zip" "$NONHEX_DIR/"
printf '%s  questboard-v1.2.3.zip\n' "$(printf 'z%.0s' $(seq 1 64))" > "${NONHEX_DIR}/questboard-v1.2.3.zip.sha256"
check "checksum fails when the hash is not hex" "1" \
  "$(status_of questboard_verify_checksum "$NONHEX_DIR" questboard-v1.2.3.zip)"

check "checksum fails when the checksum file is missing" "1" \
  "$(status_of questboard_verify_checksum "$WORK" questboard-v1.2.3.zip)"

# --- questboard_verify_attestation ----------------------------------------

ARTIFACT="${ZIP_DIR}/questboard-v1.2.3.zip"
BUNDLE="${WORK}/questboard-v1.2.3.zip.sigstore.json"
printf '{}' > "$BUNDLE"
GOOD_JSON="[{\"verificationResult\":{\"signature\":{\"certificate\":{\"sourceRepositoryDigest\":\"${SHA}\"}}}}]"

reset_stubs
export GH_TOKEN="exported-gh-token" GITHUB_TOKEN="exported-github-token" GH_ENTERPRISE_TOKEN="exported-ent-token" GH_HOST="ghe.example.com"
export STUB_GH_JSON="$GOOD_JSON" STUB_GH_EXIT=0
att_rc=0
att_out="$(questboard_verify_attestation "$ARTIFACT" "$BUNDLE" owner/repo .github/workflows/release.yml refs/tags/v1.2.3 2>/dev/null)" || att_rc=$?
check "attestation passes with a valid verification result" "0" "$att_rc"
check "attestation prints the attested commit" "$SHA" "$att_out"
check "attestation passes the exact pinned argument list" \
  "attestation verify ${ARTIFACT} --bundle ${BUNDLE} --repo owner/repo --signer-workflow owner/repo/.github/workflows/release.yml --source-ref refs/tags/v1.2.3 --deny-self-hosted-runners --format json" \
  "$(paste -sd' ' "$STUB_GH_LOG")"
check "gh never sees GH_TOKEN" "GH_TOKEN=unset" "$(grep '^GH_TOKEN=' "$STUB_GH_ENV_LOG")"
check "gh never sees GITHUB_TOKEN" "GITHUB_TOKEN=unset" "$(grep '^GITHUB_TOKEN=' "$STUB_GH_ENV_LOG")"
check "gh never sees GH_ENTERPRISE_TOKEN" "GH_ENTERPRISE_TOKEN=unset" "$(grep '^GH_ENTERPRISE_TOKEN=' "$STUB_GH_ENV_LOG")"
check "gh never sees GH_HOST" "GH_HOST=unset" "$(grep '^GH_HOST=' "$STUB_GH_ENV_LOG")"
check "gh runs without telemetry, prompts or update checks" "GH_TELEMETRY=false GH_PROMPT_DISABLED=1 GH_NO_UPDATE_NOTIFIER=1" \
  "$(grep -E '^(GH_TELEMETRY|GH_PROMPT_DISABLED|GH_NO_UPDATE_NOTIFIER)=' "$STUB_GH_ENV_LOG" | sort -r | paste -sd' ')"
check "gh could write its cache and state" "WRITABLE=yes" "$(grep '^WRITABLE=' "$STUB_GH_ENV_LOG")"

gh_cache="$(sed -n 's/^XDG_CACHE_HOME=//p' "$STUB_GH_ENV_LOG")"
gh_state="$(sed -n 's/^XDG_STATE_HOME=//p' "$STUB_GH_ENV_LOG")"
gh_config="$(sed -n 's/^GH_CONFIG_DIR=//p' "$STUB_GH_ENV_LOG")"
gh_root="$(dirname "$gh_cache")"
check "gh cache, state and config share one private directory" "yes" \
  "$([ "$(dirname "$gh_state")" = "$gh_root" ] && [ "$(dirname "$gh_config")" = "$gh_root" ] && echo yes || echo no)"
check "gh private directory is not the user's home or cache" "yes" \
  "$([ "$gh_root" != "$HOME" ] && [ "$gh_root" != "${HOME}/.cache" ] && echo yes || echo no)"
check "gh private directory is removed afterwards" "gone" \
  "$([ -e "$gh_root" ] && echo present || echo gone)"

reset_stubs
export STUB_GH_JSON="$GOOD_JSON" STUB_GH_EXIT=1
check "attestation fails when gh exits non-zero" "1" \
  "$(status_of questboard_verify_attestation "$ARTIFACT" "$BUNDLE" owner/repo .github/workflows/release.yml refs/tags/v1.2.3)"
gh_root="$(dirname "$(sed -n 's/^XDG_CACHE_HOME=//p' "$STUB_GH_ENV_LOG")")"
check "gh private directory is removed after a failure too" "gone" \
  "$([ -e "$gh_root" ] && echo present || echo gone)"

export STUB_GH_EXIT=0 STUB_GH_JSON='[{"verificationResult":{"signature":{"certificate":{}}}}]'
check "attestation fails when the digest is missing" "1" \
  "$(status_of questboard_verify_attestation "$ARTIFACT" "$BUNDLE" owner/repo .github/workflows/release.yml refs/tags/v1.2.3)"

# gh may print notices on stderr while it succeeds. Only stdout is the result.
reset_stubs
export STUB_GH_JSON="$GOOD_JSON" STUB_GH_EXIT=0 STUB_GH_STDERR="A new release of gh is available: 2.0.0 -> 2.1.0"
stderr_rc=0
stderr_out="$(questboard_verify_attestation "$ARTIFACT" "$BUNDLE" owner/repo .github/workflows/release.yml refs/tags/v1.2.3 2>/dev/null)" || stderr_rc=$?
check "a notice on gh's stderr does not spoil a valid result" "0" "$stderr_rc"
check "a notice on gh's stderr still yields the attested commit" "$SHA" "$stderr_out"

export STUB_GH_JSON='not json at all' STUB_GH_STDERR="a notice"
check "a notice on stderr does not rescue a result that is not JSON" "1" \
  "$(status_of questboard_verify_attestation "$ARTIFACT" "$BUNDLE" owner/repo .github/workflows/release.yml refs/tags/v1.2.3)"

reset_stubs
export STUB_GH_EXIT=0 STUB_GH_JSON='not json at all'
check "attestation fails when the output is not JSON" "1" \
  "$(status_of questboard_verify_attestation "$ARTIFACT" "$BUNDLE" owner/repo .github/workflows/release.yml refs/tags/v1.2.3)"

export STUB_GH_JSON='[]'
check "attestation fails when there are no results" "1" \
  "$(status_of questboard_verify_attestation "$ARTIFACT" "$BUNDLE" owner/repo .github/workflows/release.yml refs/tags/v1.2.3)"

export STUB_GH_JSON='[{"verificationResult":{"signature":{"certificate":{"sourceRepositoryDigest":"abc123"}}}}]'
check "attestation fails when the digest is not 40 hex characters" "1" \
  "$(status_of questboard_verify_attestation "$ARTIFACT" "$BUNDLE" owner/repo .github/workflows/release.yml refs/tags/v1.2.3)"

export STUB_GH_JSON='[{"verificationResult":{"signature":{"certificate":{"sourceRepositoryDigest":"0123456789ABCDEF0123456789ABCDEF01234567"}}}}]'
check "attestation fails when the digest is upper case" "1" \
  "$(status_of questboard_verify_attestation "$ARTIFACT" "$BUNDLE" owner/repo .github/workflows/release.yml refs/tags/v1.2.3)"

# A failed gh run is a refusal unless the services it needs are then found
# unreachable; only that finding yields the distinct unreachable status.
ATT_ARGS=("$ARTIFACT" "$BUNDLE" owner/repo .github/workflows/release.yml refs/tags/v1.2.3)

reset_stubs
export STUB_GH_JSON="" STUB_GH_EXIT=1 STUB_HTTP_CODE=200
check "a rejected artifact with every service reachable returns 1" "1" \
  "$(status_of questboard_verify_attestation "${ATT_ARGS[@]}")"
check "a rejected artifact prints no commit" "" \
  "$(questboard_verify_attestation "${ATT_ARGS[@]}" 2>/dev/null || true)"
check "the probe asked the Sigstore trust service" "yes" \
  "$(grep -qxF 'https://tuf-repo-cdn.sigstore.dev/' "$STUB_CURL_LOG" && echo yes || echo no)"
check "the probe asked the GitHub trust service" "yes" \
  "$(grep -qxF 'https://tuf-repo.github.com/' "$STUB_CURL_LOG" && echo yes || echo no)"
check "the probe asked the GitHub API" "yes" \
  "$(grep -qxF 'https://api.github.com/' "$STUB_CURL_LOG" && echo yes || echo no)"
check "the probe sends no credential" "0" \
  "$(grep -ciE 'authorization|token|--header|-H$' "$STUB_CURL_LOG" || true)"

reset_stubs
export STUB_GH_JSON="" STUB_GH_EXIT=1 STUB_HTTP_CODE=500
check "an error status from a service still counts as reachable" "1" \
  "$(status_of questboard_verify_attestation "${ATT_ARGS[@]}")"

reset_stubs
export STUB_GH_JSON="" STUB_GH_EXIT=1 STUB_CURL_EXIT=6
check "gh failing with every service unreachable returns 3" "3" \
  "$(status_of questboard_verify_attestation "${ATT_ARGS[@]}")"
check "an unreachable finding prints no commit" "" \
  "$(questboard_verify_attestation "${ATT_ARGS[@]}" 2>/dev/null || true)"

for service in tuf-repo-cdn.sigstore.dev tuf-repo.github.com api.github.com; do
  reset_stubs
  export STUB_GH_JSON="" STUB_GH_EXIT=1 STUB_CURL_FAIL_MATCH="$service"
  check "gh failing with only ${service} unreachable returns 3" "3" \
    "$(status_of questboard_verify_attestation "${ATT_ARGS[@]}")"
done

reset_stubs
export STUB_GH_JSON="$GOOD_JSON" STUB_GH_EXIT=0 STUB_CURL_EXIT=6
check "a successful gh run never needs the probe, even with the network down" "0" \
  "$(status_of questboard_verify_attestation "${ATT_ARGS[@]}")"
check "a successful gh run makes no probe call" "0" "$(curl_calls)"

reset_stubs
export STUB_GH_JSON='not json at all' STUB_GH_EXIT=0 STUB_CURL_EXIT=6
check "a bad result from a successful gh run is a refusal, not an outage" "1" \
  "$(status_of questboard_verify_attestation "${ATT_ARGS[@]}")"

# A gh run that stalls is cut off after a fixed bound. Without that bound a
# stalled network call would hold the installer's lock for good. The cut-off is
# judged like any other gh failure: unreachable services mean no verdict, an
# answering network means the artifact is refused.
check "the gh time bound is a positive number of seconds" "yes" \
  "$([[ "${QUESTBOARD_GH_VERIFY_TIMEOUT_SECONDS:-}" =~ ^[1-9][0-9]*$ ]] && echo yes || echo no)"
QUESTBOARD_GH_VERIFY_TIMEOUT_SECONDS=1
QUESTBOARD_GH_VERIFY_KILL_AFTER_SECONDS=1

reset_stubs
export STUB_GH_HANG_SECONDS=30 STUB_CURL_EXIT=6
hang_started=$SECONDS
check "a stalled gh with the services unreachable returns 3" "3" \
  "$(status_of questboard_verify_attestation "${ATT_ARGS[@]}")"
check "a stalled gh is cut off long before it would have finished" "yes" \
  "$([ $((SECONDS - hang_started)) -lt 15 ] && echo yes || echo no)"

reset_stubs
export STUB_GH_HANG_SECONDS=30 STUB_HTTP_CODE=200
check "a stalled gh with every service reachable returns 1" "1" \
  "$(status_of questboard_verify_attestation "${ATT_ARGS[@]}")"
check "a stalled gh prints no commit" "" \
  "$(questboard_verify_attestation "${ATT_ARGS[@]}" 2>/dev/null || true)"

reset_stubs
export STUB_GH_JSON="$GOOD_JSON" STUB_GH_EXIT=0
check "a gh run inside the time bound still verifies" "0" \
  "$(status_of questboard_verify_attestation "${ATT_ARGS[@]}")"
QUESTBOARD_GH_VERIFY_TIMEOUT_SECONDS=120
QUESTBOARD_GH_VERIFY_KILL_AFTER_SECONDS=10

reset_stubs
check "the probe finds reachable services reachable" "1" "$(status_of questboard_verification_services_unreachable)"
export STUB_CURL_EXIT=28
check "the probe finds a timed-out service unreachable" "0" "$(status_of questboard_verification_services_unreachable)"

reset_stubs
check "attestation fails without a bundle file and never calls gh" "no" \
  "$(questboard_verify_attestation "$ARTIFACT" "${WORK}/missing.json" owner/repo .github/workflows/release.yml refs/tags/v1.2.3 >/dev/null 2>&1 || true; [ -e "$STUB_GH_LOG" ] && echo yes || echo no)"
unset GH_TOKEN GITHUB_TOKEN GH_ENTERPRISE_TOKEN GH_HOST

# --- questboard_commit_on_branch ------------------------------------------

BODY="${WORK}/compare-body.json"
compare_status() {
  local api_status="$1"
  printf '{"status":"%s","ahead_by":0}' "$api_status" > "$BODY"
  reset_stubs
  export STUB_HTTP_CODE=200 STUB_BODY_FILE="$BODY"
  status_of questboard_commit_on_branch owner/repo "$SHA" main
}

check "commit on branch: identical is accepted" "0" "$(compare_status identical)"
check "commit on branch: behind is accepted" "0" "$(compare_status behind)"
check "commit on branch: ahead is refused" "1" "$(compare_status ahead)"
check "commit on branch: diverged is refused" "1" "$(compare_status diverged)"

# A definite answer that the commit is not part of main's history is a refusal.
# A 404 means GitHub does not know the commit in this repository, and any other
# client error is an answer too.
for code in 404 422; do
  reset_stubs
  export STUB_HTTP_CODE="$code" STUB_BODY_FILE="$BODY"
  check "commit on branch: HTTP ${code} is refused (1)" "1" \
    "$(status_of questboard_commit_on_branch owner/repo "$SHA" main)"
done

# No answer at all, or an answer that says nothing about the commit (rate limit,
# server error), is no verdict: it must not be mistaken for a refusal.
for code in 403 429 500 502 503 504; do
  reset_stubs
  export STUB_HTTP_CODE="$code" STUB_BODY_FILE="$BODY"
  check "commit on branch: HTTP ${code} is no verdict (3)" "3" \
    "$(status_of questboard_commit_on_branch owner/repo "$SHA" main)"
done

reset_stubs
export STUB_HTTP_CODE=000 STUB_CURL_EXIT=28 STUB_BODY_FILE="$BODY"
check "commit on branch: transport failure is no verdict (3)" "3" "$(status_of questboard_commit_on_branch owner/repo "$SHA" main)"

printf 'not json' > "$BODY"
reset_stubs
export STUB_HTTP_CODE=200 STUB_BODY_FILE="$BODY"
check "commit on branch: an unreadable answer is refused" "1" "$(status_of questboard_commit_on_branch owner/repo "$SHA" main)"

reset_stubs
check "commit on branch: a non-hex SHA is refused" "1" "$(status_of questboard_commit_on_branch owner/repo 'nothex; rm -rf /' main)"
check "commit on branch: a non-hex SHA makes no request" "0" "$(curl_calls)"
check "commit on branch: an upper-case SHA is refused" "1" \
  "$(status_of questboard_commit_on_branch owner/repo 0123456789ABCDEF0123456789ABCDEF01234567 main)"

printf '{"status":"identical"}' > "$BODY"
reset_stubs
export STUB_HTTP_CODE=200 STUB_BODY_FILE="$BODY"
questboard_commit_on_branch owner/repo "$SHA" main
check "commit on branch: asks the public compare endpoint" \
  "https://api.github.com/repos/owner/repo/compare/main...${SHA}" \
  "$(tail -n 1 "$STUB_CURL_LOG")"
check "commit on branch: sends no header of any kind" "0" \
  "$(grep -ciE '^(--header|-H|--user|--oauth2-bearer|--netrc)' "$STUB_CURL_LOG" || true)"

# --- questboard_fetch_latest_tag ------------------------------------------

printf '{"tag_name":"v1.4.0","name":"release"}' > "$BODY"
reset_stubs
export STUB_HTTP_CODE=200 STUB_BODY_FILE="$BODY"
tag_rc=0
tag_out="$(questboard_fetch_latest_tag owner/repo)" || tag_rc=$?
check "latest tag: 200 returns 0" "0" "$tag_rc"
check "latest tag: 200 prints the tag name" "v1.4.0" "$tag_out"
check "latest tag: makes exactly one request" "1" "$(curl_calls)"
check "latest tag: asks the public latest-release endpoint" \
  "https://api.github.com/repos/owner/repo/releases/latest" "$(tail -n 1 "$STUB_CURL_LOG")"

reset_stubs
export STUB_HTTP_CODE=404 STUB_BODY_FILE="$BODY"
check "latest tag: 404 means none published (3)" "3" "$(status_of questboard_fetch_latest_tag owner/repo)"

reset_stubs
export STUB_HTTP_CODE=403 STUB_BODY_FILE="$BODY"
check "latest tag: 403 means unreachable (1)" "1" "$(status_of questboard_fetch_latest_tag owner/repo)"

reset_stubs
export STUB_HTTP_CODE=000 STUB_CURL_EXIT=6
check "latest tag: a transport failure means unreachable (1)" "1" "$(status_of questboard_fetch_latest_tag owner/repo)"

printf '{"name":"no tag here"}' > "$BODY"
reset_stubs
export STUB_HTTP_CODE=200 STUB_BODY_FILE="$BODY"
check "latest tag: a 200 without a tag name means unreachable (1)" "1" "$(status_of questboard_fetch_latest_tag owner/repo)"

# --- questboard_download_asset --------------------------------------------

printf 'asset bytes' > "$BODY"
DEST="${WORK}/downloaded.zip"
rm -f "$DEST"
reset_stubs
export STUB_HTTP_CODE=200 STUB_BODY_FILE="$BODY"
check "download: 200 returns 0" "0" "$(status_of questboard_download_asset owner/repo v1.2.3 questboard-v1.2.3.zip "$DEST")"
check "download: 200 leaves the file in place" "yes" "$([ -f "$DEST" ] && echo yes || echo no)"
check "download: asks the release download url" \
  "https://github.com/owner/repo/releases/download/v1.2.3/questboard-v1.2.3.zip" "$(tail -n 1 "$STUB_CURL_LOG")"
check "download: allows five minutes" "yes" \
  "$(grep -qx -- '300' "$STUB_CURL_LOG" && echo yes || echo no)"

rm -f "$DEST"
reset_stubs
export STUB_HTTP_CODE=404 STUB_BODY_FILE="$BODY"
check "download: 404 returns 2" "2" "$(status_of questboard_download_asset owner/repo v1.2.3 questboard-v1.2.3.zip "$DEST")"
check "download: 404 leaves no file behind" "no" "$([ -f "$DEST" ] && echo yes || echo no)"

rm -f "$DEST"
reset_stubs
export STUB_HTTP_CODE=500 STUB_BODY_FILE="$BODY"
check "download: 500 returns 1" "1" "$(status_of questboard_download_asset owner/repo v1.2.3 questboard-v1.2.3.zip "$DEST")"
check "download: 500 leaves no file behind" "no" "$([ -f "$DEST" ] && echo yes || echo no)"

rm -f "$DEST"
reset_stubs
export STUB_HTTP_CODE=000 STUB_CURL_EXIT=28 STUB_BODY_FILE="$BODY"
check "download: a transport failure returns 1" "1" "$(status_of questboard_download_asset owner/repo v1.2.3 questboard-v1.2.3.zip "$DEST")"
check "download: a transport failure leaves no partial file" "no" "$([ -f "$DEST" ] && echo yes || echo no)"

reset_stubs
check "download: an unsafe asset name is refused without a request" "1:0" \
  "$(status_of questboard_download_asset owner/repo v1.2.3 '../etc/passwd' "$DEST"):$(curl_calls)"

check "host commands were never called" "" "$(host_guard_calls)"

if [ "$FAILURES" -gt 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES"
  exit 1
fi
printf 'all verify checks passed\n'
