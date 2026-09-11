#!/usr/bin/env bash
# Copies the two game repos' hand-run SQL into sql/fixtures/ so the integration tests
# build the real schema, not a guess. Re-run whenever either repo adds a migration, then
# update the ORDER file next to the copies if the new file has to run in sequence.
#
#   ops/sync-fixtures.sh          copy (overwrites the .sql and README copies)
#   ops/sync-fixtures.sh --check  exit 1 if the copies differ from the sibling repos
#
# Sibling locations default to the layout under ~/Coding/Claude Repos; override with
# CC_SQL_DIR / MMR_SQL_DIR. Missing siblings are reported and skipped, never fatal, so
# CI without the sibling checkouts still passes.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CC_SQL_DIR="${CC_SQL_DIR:-$ROOT/../../Competitive Crosswords/CompetitiveCrosswords/server/sql}"
MMR_SQL_DIR="${MMR_SQL_DIR:-$ROOT/../../Make It Red/server/sql}"
MODE="${1:-copy}"
status=0

sync_one() {
  local label="$1" src="$2" dst="$3"
  if [[ ! -d "$src" ]]; then
    echo "skip $label: $src not found"
    return
  fi
  mkdir -p "$dst"
  local f name
  for f in "$src"/*.sql "$src"/README.md; do
    [[ -e "$f" ]] || continue
    name="$(basename "$f")"
    if [[ "$MODE" == "--check" ]]; then
      if ! cmp -s "$f" "$dst/$name"; then
        echo "drift $label/$name"
        status=1
      fi
    else
      cp "$f" "$dst/$name"
      echo "copied $label/$name"
    fi
  done
}

sync_one crosswords "$CC_SQL_DIR" "$ROOT/sql/fixtures/crosswords"
sync_one makemered "$MMR_SQL_DIR" "$ROOT/sql/fixtures/makemered"
exit $status
