#!/usr/bin/env bash
# Verifies a published release from a workstation exactly the way the server
# will verify it, so the first zip carried to a server can be trusted before
# the installer exists there. Takes one strict vMAJOR.MINOR.PATCH tag.
#
# Workstation only and read-only: it downloads the release zip, its checksum
# and its Sigstore bundle from the public release download URL with curl and no
# credential of any kind, sources the installer's own verification functions,
# and calls them as the installer does. It then proves that a copy of the zip
# with one byte changed is refused. It is never installed on the server.
#
# Prints PASS or FAIL for every step and exits 1 on the first failure. On
# success the last line is `sha256 questboard-vX.Y.Z.zip <hex>`, the hash to
# compare against on the server before the first install.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

SIGNER_WORKFLOW=".github/workflows/release.yml"
MAIN_BRANCH="main"

pass() {
  printf 'PASS: %s\n' "$1"
}

fail() {
  printf 'FAIL: %s\n' "$1"
  exit 1
}

# shellcheck source=deploy/lib/common.sh
source "${REPO_ROOT}/deploy/lib/common.sh"
# shellcheck source=deploy/lib/verify.sh
source "${REPO_ROOT}/deploy/lib/verify.sh"

TAG="${1:-}"
if [ -z "$TAG" ]; then
  echo "Usage: verify-published-release.sh vMAJOR.MINOR.PATCH" >&2
  exit 1
fi

if ! questboard_is_strict_tag "$TAG"; then
  fail "'${TAG}' is a strict vMAJOR.MINOR.PATCH tag"
fi
pass "'${TAG}' is a strict vMAJOR.MINOR.PATCH tag"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

REPO=""
if ! REPO="$(env -u GH_TOKEN -u GITHUB_TOKEN -u GH_ENTERPRISE_TOKEN \
    gh repo view --json nameWithOwner --jq .nameWithOwner 2>/dev/null)" || [ -z "$REPO" ]; then
  # A workstation with no gh login cannot ask the API, so fall back to the
  # owner/name in the origin remote of this checkout.
  REPO="$(git -C "$REPO_ROOT" remote get-url origin 2>/dev/null \
    | sed -E 's#^(git@github\.com:|https://github\.com/|ssh://git@github\.com/)##; s#\.git$##')" || REPO=""
  if ! [[ "$REPO" =~ $QUESTBOARD_REPO_RE ]]; then
    fail "resolve the repository with gh repo view or the origin remote"
  fi
fi
pass "resolved repository ${REPO}"

ZIP_NAME="questboard-${TAG}.zip"
SUM_NAME="${ZIP_NAME}.sha256"
BUNDLE_NAME="${ZIP_NAME}.sigstore.json"
DOWNLOAD_BASE="https://github.com/${REPO}/releases/download/${TAG}"

for asset in "$ZIP_NAME" "$SUM_NAME" "$BUNDLE_NAME"; do
  if ! curl --fail --silent --show-error --location --max-time 300 \
      --output "${WORK_DIR}/${asset}" "${DOWNLOAD_BASE}/${asset}"; then
    fail "download ${asset} from the public release page"
  fi
  pass "downloaded ${asset} from the public release page"
done

if ! questboard_verify_checksum "$WORK_DIR" "$ZIP_NAME"; then
  fail "${ZIP_NAME} matches ${SUM_NAME}"
fi
pass "${ZIP_NAME} matches ${SUM_NAME}"

DIGEST=""
if ! DIGEST="$(questboard_verify_attestation "${WORK_DIR}/${ZIP_NAME}" "${WORK_DIR}/${BUNDLE_NAME}" \
    "$REPO" "$SIGNER_WORKFLOW" "refs/tags/${TAG}")"; then
  fail "verify the published attestation for ${ZIP_NAME}"
fi
pass "verified the published attestation (source commit ${DIGEST})"

if ! questboard_commit_on_branch "$REPO" "$DIGEST" "$MAIN_BRANCH"; then
  fail "confirm the attested commit ${DIGEST} is on ${MAIN_BRANCH}"
fi
pass "confirmed the attested commit ${DIGEST} is on ${MAIN_BRANCH}"

TAMPERED_PATH="${WORK_DIR}/tampered-${ZIP_NAME}"
cp "${WORK_DIR}/${ZIP_NAME}" "$TAMPERED_PATH"
python3 - "$TAMPERED_PATH" <<'EOF_PY'
import sys

with open(sys.argv[1], "r+b") as handle:
    handle.seek(100)
    byte = handle.read(1)
    handle.seek(100)
    handle.write(bytes([byte[0] ^ 0xFF]))
EOF_PY

if questboard_verify_attestation "$TAMPERED_PATH" "${WORK_DIR}/${BUNDLE_NAME}" \
    "$REPO" "$SIGNER_WORKFLOW" "refs/tags/${TAG}" >/dev/null 2>&1; then
  fail "refuse a one-byte-modified copy of ${ZIP_NAME}"
fi
pass "refused a one-byte-modified copy of ${ZIP_NAME}"

ZIP_SHA256="$(sha256sum "${WORK_DIR}/${ZIP_NAME}" | awk '{print $1}')"
echo "all checks passed for ${TAG}"
echo "sha256 ${ZIP_NAME} ${ZIP_SHA256}"
