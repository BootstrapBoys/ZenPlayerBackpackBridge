#!/usr/bin/env bash
# Decompile a dependency so its source can be read, grepped or diffed.
#
#   ./tooling/decompile.sh AdventureBackpacks          # whole assembly
#   ./tooling/decompile.sh ZenPlayer AdventureBackpacks
#   ./tooling/decompile.sh --type AdventureBackpacks.API.ABAPI AdventureBackpacks
#
# Names are matched against refs/mods, case-insensitively and by substring. Output goes to
# $DECOMP_DIR (default /tmp/zbb-decomp) so the project tree never accumulates derived files.
set -euo pipefail
cd "$(dirname "$0")/.."

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export DOTNET_ROLL_FORWARD=LatestMajor
ILSPY="$DOTNET_ROOT/tools/ilspycmd"
OUT="${DECOMP_DIR:-/tmp/zbb-decomp}"
mkdir -p "$OUT"

[ -x "$ILSPY" ] || { echo "ilspycmd not found at $ILSPY (dotnet tool install -g ilspycmd)"; exit 1; }

type_name=""
if [ "${1:-}" = "--type" ]; then
  type_name="${2:-}"
  [ -n "$type_name" ] || { echo "--type needs a (namespace-qualified) type name"; exit 1; }
  shift 2
fi
[ $# -gt 0 ] || { echo "usage: $0 [--type <Type>] <ModName>..."; exit 1; }

for mod in "$@"; do
  dll=""
  exact="refs/mods/$mod.dll"
  if [ -f "$exact" ]; then
    dll="$exact"
  else
    dll=$(find refs/mods -maxdepth 1 -iname "*$mod*.dll" | head -1)
  fi

  if [ -z "$dll" ]; then
    echo "no DLL matching '$mod' in refs/mods"
    exit 1
  fi

  base=$(basename "$dll" .dll)
  if [ -n "$type_name" ]; then
    target="$OUT/${base}.${type_name##*.}.cs"
    "$ILSPY" -t "$type_name" "$dll" > "$target"
    echo "$target  ($(wc -l < "$target") lines)"
  else
    target="$OUT/$base"
    mkdir -p "$target"
    "$ILSPY" "$dll" -o "$target" >/dev/null
    echo "$target/  ($(find "$target" -name '*.cs' | wc -l) file(s), $(du -sh "$target" | cut -f1))"
  fi
done
