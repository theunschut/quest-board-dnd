#!/usr/bin/env bash
# Validates the release tag before anything is built from it.
#
# Environment (nothing is taken from arguments):
#   TAG         required. Must be exactly vMAJOR.MINOR.PATCH with no leading zeros,
#               pre-release suffix or build metadata.
#   GITHUB_SHA  optional. When set, the tag must resolve to this commit.
#   MAIN_REF    optional, default origin/main. The tag's commit must be reachable from it.
#
# On success prints "version=X.Y.Z" (no leading v) to stdout. On any failure stdout stays
# empty, a "::error::" line goes to stderr and the exit status is non-zero. The raw tag is
# never echoed into a message until it has passed the strict format check.
set -euo pipefail

TAG="${TAG:-}"
MAIN_REF="${MAIN_REF:-origin/main}"

fail() {
  echo "::error::$1" >&2
  exit 1
}

[ -n "$TAG" ] || fail "TAG environment variable is required and must not be empty"

strict_tag='^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
[[ "$TAG" =~ $strict_tag ]] \
  || fail "Tag is not a strict release tag (expected vMAJOR.MINOR.PATCH, no leading zeros, no pre-release or build metadata)"

# ^{commit} peels annotated tags, so lightweight and annotated tags behave the same.
tag_commit="$(git rev-parse --verify --quiet "refs/tags/${TAG}^{commit}")" \
  || fail "Tag '$TAG' does not resolve to a commit"

if [ -n "${GITHUB_SHA:-}" ] && [ "$tag_commit" != "$GITHUB_SHA" ]; then
  fail "Tag '$TAG' commit does not match GITHUB_SHA"
fi

git merge-base --is-ancestor "$tag_commit" "$MAIN_REF" 2>/dev/null \
  || fail "Tag '$TAG' commit is not reachable from $MAIN_REF"

printf 'version=%s\n' "${TAG#v}"
