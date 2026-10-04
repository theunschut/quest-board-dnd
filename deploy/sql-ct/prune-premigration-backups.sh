#!/usr/bin/env bash
# Keeps the newest KEEP pre-migration database backups in DIR and removes the
# rest.
#
# The installer asks SQL Server to write a full backup before it applies
# migrations, and SQL Server writes that file to its own disk, so trimming old
# ones is a filesystem job on the SQL Server host, not a database privilege.
# Run this as root on that host, from a weekly cron entry or by hand after a
# release that migrated. It touches only regular files directly inside DIR whose
# names match questboard-premigration-*.bak, which is the pattern the migrator's
# backup command creates; every other file and directory is left alone.
#
# Usage: prune-premigration-backups.sh [DIR] [KEEP]
#   DIR   backup directory (default /var/opt/mssql/data)
#   KEEP  how many of the newest backups to keep, 1 or more (default 5)
set -euo pipefail

dir="${1-/var/opt/mssql/data}"
keep="${2-5}"

if ! [[ "$keep" =~ ^[1-9][0-9]*$ ]]; then
  echo "KEEP must be a whole number of 1 or more" >&2
  exit 1
fi
if [ ! -d "$dir" ]; then
  echo "not a directory: ${dir}" >&2
  exit 1
fi

# Newest first. Names are NUL-terminated so no file name can be misread, and the
# name breaks a tie between files with the same modification time.
mapfile -d '' -t entries < <(
  find "$dir" -maxdepth 1 -type f -name 'questboard-premigration-*.bak' -printf '%T@ %f\0' \
    | sort -z -k1,1nr -k2
)

index=0
for entry in "${entries[@]}"; do
  index=$((index + 1))
  if [ "$index" -le "$keep" ]; then
    continue
  fi
  name="${entry#* }"
  rm -f -- "${dir}/${name}"
  echo "removed ${name}"
done
