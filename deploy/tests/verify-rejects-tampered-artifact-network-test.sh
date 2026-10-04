#!/usr/bin/env bash
# Proves, against real attested bytes from a public release, that the
# installer's own verification (questboard_verify_attestation) accepts the
# genuine artifact and refuses a tampered copy, a wrong repository, a wrong
# signer workflow and a wrong source ref, and that a token exported in the
# caller's environment never reaches gh.
#
# Needs network access to github.com and the Sigstore trust service, so it only
# runs when QUESTBOARD_TEST_NETWORK=1. Read-only: nothing is written to GitHub.
set -euo pipefail

if [ "${QUESTBOARD_TEST_NETWORK:-}" != "1" ]; then
  echo "SKIP: set QUESTBOARD_TEST_NETWORK=1 to run the network verification test"
  exit 0
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
FIXTURE_DIR="${SCRIPT_DIR}/fixtures"

# shellcheck source=deploy/lib/common.sh
source "${REPO_ROOT}/deploy/lib/common.sh"
# shellcheck source=deploy/lib/verify.sh
source "${REPO_ROOT}/deploy/lib/verify.sh"
# shellcheck source=deploy/tests/fixtures/public-attested-artifact.env
source "${FIXTURE_DIR}/public-attested-artifact.env"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT
# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$WORK_DIR"

FAILURES=0

check() {
  local description="$1" expected="$2" actual="$3"
  if [ "$actual" = "$expected" ]; then
    printf 'PASS: %s\n' "$description"
  else
    printf 'FAIL: %s (expected %s, got %s)\n' "$description" "$expected" "$actual"
    FAILURES=$((FAILURES + 1))
  fi
}

# Prints 0 when verification of ARTIFACT succeeds with the given pins, 1 when it
# is refused.
verify_status() {
  local artifact="$1" repo="$2" signer_workflow="$3" source_ref="$4"
  if questboard_verify_attestation "$artifact" "$BUNDLE_PATH" "$repo" "$signer_workflow" "$source_ref" >/dev/null 2>&1; then
    printf '0'
  else
    printf '1'
  fi
}

ARTIFACT_PATH="${WORK_DIR}/artifact.bin"
BUNDLE_PATH="${FIXTURE_DIR}/public-attested-artifact.sigstore.jsonl"

questboard_log "downloading the fixture artifact"
curl --fail --silent --show-error --location --max-time 120 -o "$ARTIFACT_PATH" "$ARTIFACT_URL"

ACTUAL_SHA256="$(sha256sum "$ARTIFACT_PATH" | awk '{print $1}')"
if [ "$ACTUAL_SHA256" != "$ARTIFACT_SHA256" ]; then
  questboard_die "the downloaded fixture artifact does not match its recorded sha256"
fi
printf 'PASS: fixture artifact matches its recorded sha256\n'

# One byte flipped at offset 100.
TAMPERED_PATH="${WORK_DIR}/tampered.bin"
cp "$ARTIFACT_PATH" "$TAMPERED_PATH"
python3 - "$TAMPERED_PATH" <<'EOF_PY'
import sys

with open(sys.argv[1], "r+b") as handle:
    handle.seek(100)
    byte = handle.read(1)
    handle.seek(100)
    handle.write(bytes([byte[0] ^ 0xFF]))
EOF_PY

DIGEST="$(questboard_verify_attestation "$ARTIFACT_PATH" "$BUNDLE_PATH" "$REPO" "$SIGNER_WORKFLOW" "$SOURCE_REF" 2>/dev/null || true)"
check "genuine artifact verifies and reports its source digest" "$SOURCE_DIGEST" "$DIGEST"

check "one-byte-tampered artifact is refused" "1" \
  "$(verify_status "$TAMPERED_PATH" "$REPO" "$SIGNER_WORKFLOW" "$SOURCE_REF")"

check "wrong repository is refused" "1" \
  "$(verify_status "$ARTIFACT_PATH" "cli/not-cli" "$SIGNER_WORKFLOW" "$SOURCE_REF")"

check "wrong signer workflow is refused" "1" \
  "$(verify_status "$ARTIFACT_PATH" "$REPO" ".github/workflows/other.yml" "$SOURCE_REF")"

check "wrong source ref is refused" "1" \
  "$(verify_status "$ARTIFACT_PATH" "$REPO" "$SIGNER_WORKFLOW" "refs/heads/not-trunk")"

# Token isolation: export bogus tokens and put a gh wrapper first on PATH that
# records what it was handed before running the real gh. The verification must
# strip every token and still pass.
REAL_GH="$(command -v gh)"
GH_WRAPPER_DIR="${WORK_DIR}/gh-wrapper"
GH_SEEN_LOG="${WORK_DIR}/gh-seen-tokens.log"
mkdir -p "$GH_WRAPPER_DIR"
: > "$GH_SEEN_LOG"
{
  printf '#!/usr/bin/env bash\n'
  printf 'printf "%%s\\n" "GH_TOKEN=${GH_TOKEN-unset} GITHUB_TOKEN=${GITHUB_TOKEN-unset}" >> %q\n' "$GH_SEEN_LOG"
  printf 'exec %q "$@"\n' "$REAL_GH"
} > "${GH_WRAPPER_DIR}/gh"
chmod +x "${GH_WRAPPER_DIR}/gh"

TOKEN_RESULT="$(
  export GH_TOKEN=bogus-token-value GITHUB_TOKEN=bogus-token-value
  PATH="${GH_WRAPPER_DIR}:${PATH}"
  verify_status "$ARTIFACT_PATH" "$REPO" "$SIGNER_WORKFLOW" "$SOURCE_REF"
)"
check "genuine artifact still verifies with tokens exported" "0" "$TOKEN_RESULT"

SEEN_LINES="$(wc -l < "$GH_SEEN_LOG")"
if [ "$SEEN_LINES" -ge 1 ]; then
  printf 'PASS: the gh wrapper was invoked\n'
else
  printf 'FAIL: the gh wrapper was never invoked\n'
  FAILURES=$((FAILURES + 1))
fi
check "gh saw no token" "0" "$(grep -vc '^GH_TOKEN=unset GITHUB_TOKEN=unset$' "$GH_SEEN_LOG" || true)"

if [ "$FAILURES" -ne 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES" >&2
  exit 1
fi

printf 'All checks passed\n'
