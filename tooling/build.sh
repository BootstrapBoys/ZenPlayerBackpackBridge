#!/usr/bin/env bash
# Dev build: compile the mod and drop the DLL in artifacts/debug for in-game testing.
#
# Release packaging is deliberately NOT done here - it only happens on an explicit release
# request and produces the zip in artifacts/release.
set -euo pipefail
cd "$(dirname "$0")/.."          # project root, one level up from tooling/

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export VALHEIM_DIR="${VALHEIM_DIR:-$PWD/refs/game}"
export BEPINEX_DIR="${BEPINEX_DIR:-$PWD/refs/game/BepInEx}"
export MODS_DIR="${MODS_DIR:-$PWD/refs/mods}"

dotnet build ZenPlayerBackpackBridge.csproj -c Release \
  -p:TreatWarningsAsErrors=true -p:WarningLevel=9999 -p:NoWarn=

echo "=== binding + version audit ==="
dotnet run --project tooling/build-check/Inspect -c Release

OUT="artifacts/debug"
mkdir -p "$OUT"
cp bin/Release/net48/ZenPlayerBackpackBridge.dll "$OUT/ZenPlayerBackpackBridge.dll"

VERSION=$(grep -oP 'ModVersion = "\K[0-9.]+' zbb_main.cs)
SHA=$(python3 -c "import hashlib,sys;print(hashlib.sha256(open(sys.argv[1],'rb').read()).hexdigest())" \
      "$OUT/ZenPlayerBackpackBridge.dll")

cat > "$OUT/build-info.txt" <<EOF
ZenPlayerBackpackBridge $VERSION
built   $(date -u '+%Y-%m-%d %H:%M UTC')
sha256  $SHA
source  $(pwd)
EOF

# Build outputs are regenerable; the candidate in artifacts/debug is the deliverable.
rm -rf bin obj tooling/build-check/Inspect/bin tooling/build-check/Inspect/obj

echo "artifacts/debug/ZenPlayerBackpackBridge.dll  ($VERSION)"
