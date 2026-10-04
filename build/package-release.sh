#!/usr/bin/env bash
# Packages a release: the app, its migrator and the deploy tooling, plus a manifest, zipped
# with a checksum. Framework-dependent and portable (no runtime identifier).
set -euo pipefail

VERSION=""
COMMIT=""
OUTPUT=""
NO_BUILD=0

while [ "$#" -gt 0 ]; do
  case "$1" in
    --version)
      VERSION="${2:-}"
      shift 2
      ;;
    --commit)
      COMMIT="${2:-}"
      shift 2
      ;;
    --output)
      OUTPUT="${2:-}"
      shift 2
      ;;
    --no-build)
      NO_BUILD=1
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

if [ -z "$VERSION" ] || [ -z "$COMMIT" ] || [ -z "$OUTPUT" ]; then
  echo "Usage: package-release.sh --version X.Y.Z --commit <40-hex-sha> --output DIR [--no-build]" >&2
  exit 1
fi

if ! [[ "$VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Invalid --version '$VERSION': must be strict semver X.Y.Z" >&2
  exit 1
fi

if ! [[ "$COMMIT" =~ ^[0-9a-f]{40}$ ]]; then
  echo "Invalid --commit '$COMMIT': must be 40 lowercase hex characters" >&2
  exit 1
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ ! -d "$REPO_ROOT/deploy" ]; then
  echo "Repository has no deploy/ directory: $REPO_ROOT/deploy" >&2
  exit 1
fi

mkdir -p "$OUTPUT"
OUTPUT="$(cd "$OUTPUT" && pwd)"
STAGE_DIR="$OUTPUT/stage"

rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR"

cd "$REPO_ROOT"

PUBLISH_ARGS=(-c Release "-p:Version=$VERSION" "-p:SourceRevisionId=$COMMIT" -p:ContinuousIntegrationBuild=true)
if [ "$NO_BUILD" -eq 1 ]; then
  PUBLISH_ARGS+=(--no-build)
fi

dotnet publish QuestBoard.Service/QuestBoard.Service.csproj "${PUBLISH_ARGS[@]}" -o "$STAGE_DIR/app" >&2
dotnet publish QuestBoard.Migrator/QuestBoard.Migrator.csproj "${PUBLISH_ARGS[@]}" -o "$STAGE_DIR/migrator" >&2

mkdir -p "$STAGE_DIR/deploy"
(cd deploy && tar -cf - --exclude='./tests' .) | (cd "$STAGE_DIR/deploy" && tar -xf -)

python3 - "$STAGE_DIR/release-manifest.json" "$VERSION" "$COMMIT" <<'PY'
import json
import sys

path, version, commit = sys.argv[1:4]
with open(path, "w", encoding="utf-8") as handle:
    json.dump({"version": version, "commit": commit, "healthVersionHeader": True}, handle)
    handle.write("\n")
PY

ZIP_NAME="questboard-v$VERSION.zip"
ZIP_PATH="$OUTPUT/$ZIP_NAME"
rm -f "$ZIP_PATH" "$ZIP_PATH.sha256"

(cd "$STAGE_DIR" && zip -r -q -X "$ZIP_PATH" .)
(cd "$OUTPUT" && sha256sum "$ZIP_NAME" > "$ZIP_NAME.sha256")

echo "$ZIP_PATH"
