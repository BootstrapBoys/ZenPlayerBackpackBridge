#!/usr/bin/env bash
# Fetch the reference assemblies CI needs to build this mod. Nothing downloaded here is
# committed: refs/ and .ci/ are ignored. The game assemblies come from the anonymous Valheim
# dedicated-server depot (app 896660); the dependency zips are pinned in resources/manifest.json.
#
# ZBB_CI_WORK can point at a reusable download cache (used by the local smoke test).
set -euo pipefail
cd "$(dirname "$0")/.."

ROOT="$PWD"
WORK="${ZBB_CI_WORK:-$ROOT/.ci}"
DOWNLOADS="$WORK/downloads"
GAME="$WORK/game"
DEPOT_DOWNLOADER_VERSION="DepotDownloader_3.4.0"

for tool in curl unzip python3; do
  command -v "$tool" >/dev/null || { echo "missing required tool: $tool" >&2; exit 1; }
done

mkdir -p "$DOWNLOADS" refs/game/valheim_Data/Managed refs/game/BepInEx/core refs/mods

dep() {
  python3 - "$1" <<'PY'
import json, sys
prefix = sys.argv[1]
deps = json.load(open("resources/manifest.json"))["dependencies"]
print(next(d for d in deps if d.startswith(prefix)))
PY
}

BEPINEX_DEP="$(dep 'denikson-BepInExPack_Valheim-')"
ADVENTUREBACKPACKS_DEP="$(dep 'Vapok-AdventureBackpacks-')"
ZENPLAYER_DEP="$(dep 'ZenDragon-ZenPlayer-')"

fetch_zip() { # url, destination
  local url="$1" out="$2"
  if [ ! -f "$out" ]; then
    echo "Downloading $(basename "$out")"
    curl -fsSL --retry 3 --retry-delay 5 -o "$out" "$url"
  fi
}

# 1. Game assemblies: public anonymous depot. The dedicated server uses the same managed
#    assemblies the client does, so it is a valid compile reference.
DEPOT_DOWNLOADER="$WORK/depot/DepotDownloader"
if [ ! -x "$DEPOT_DOWNLOADER" ]; then
  fetch_zip \
    "https://github.com/SteamRE/DepotDownloader/releases/download/${DEPOT_DOWNLOADER_VERSION}/DepotDownloader-linux-x64.zip" \
    "$WORK/depot.zip"
  mkdir -p "$WORK/depot"
  unzip -q -o "$WORK/depot.zip" -d "$WORK/depot"
  chmod +x "$DEPOT_DOWNLOADER"
fi

if [ ! -f "$GAME/valheim_server_Data/Managed/assembly_valheim.dll" ]; then
  echo "Downloading Valheim dedicated server (app 896660, anonymous)"
  "$DEPOT_DOWNLOADER" -app 896660 -dir "$GAME" -validate
fi

cp -f "$GAME/valheim_server_Data/Managed/"*.dll refs/game/valheim_Data/Managed/

# 2. BepInEx core and the mod assemblies, from the same versions the manifest requires.
fetch_zip "https://gcdn.thunderstore.io/live/repository/packages/${BEPINEX_DEP}.zip" \
  "$DOWNLOADS/bepinex.zip"
rm -rf "$WORK/bepinex"
mkdir -p "$WORK/bepinex"
unzip -q -o "$DOWNLOADS/bepinex.zip" -d "$WORK/bepinex"
BEPINEX_CORE="$(find "$WORK/bepinex" -type d -path '*/BepInEx/core' -print -quit)"
[ -n "$BEPINEX_CORE" ] || { echo "BepInEx core not found in package" >&2; exit 1; }
cp -f "$BEPINEX_CORE/"*.dll refs/game/BepInEx/core/

fetch_zip "https://gcdn.thunderstore.io/live/repository/packages/${ADVENTUREBACKPACKS_DEP}.zip" \
  "$DOWNLOADS/adventurebackpacks.zip"
rm -rf "$WORK/adventurebackpacks"
mkdir -p "$WORK/adventurebackpacks"
unzip -q -o "$DOWNLOADS/adventurebackpacks.zip" -d "$WORK/adventurebackpacks"
AB_DLL="$(find "$WORK/adventurebackpacks" -name AdventureBackpacks.dll -print -quit)"
[ -n "$AB_DLL" ] || { echo "AdventureBackpacks.dll not found in package" >&2; exit 1; }
cp -f "$AB_DLL" refs/mods/AdventureBackpacks.dll

fetch_zip "https://gcdn.thunderstore.io/live/repository/packages/${ZENPLAYER_DEP}.zip" \
  "$DOWNLOADS/zenplayer.zip"
rm -rf "$WORK/zenplayer"
mkdir -p "$WORK/zenplayer"
unzip -q -o "$DOWNLOADS/zenplayer.zip" -d "$WORK/zenplayer"
ZEN_DLL="$(find "$WORK/zenplayer" -name ZenPlayer.dll -print -quit)"
[ -n "$ZEN_DLL" ] || { echo "ZenPlayer.dll not found in package" >&2; exit 1; }
cp -f "$ZEN_DLL" refs/mods/ZenPlayer.dll

# 3. Fail with the missing path instead of letting MSBuild report a wall of reference errors.
required=(
  refs/game/valheim_Data/Managed/assembly_valheim.dll
  refs/game/valheim_Data/Managed/UnityEngine.dll
  refs/game/valheim_Data/Managed/UnityEngine.CoreModule.dll
  refs/game/valheim_Data/Managed/mscorlib.dll
  refs/game/BepInEx/core/BepInEx.dll
  refs/game/BepInEx/core/0Harmony.dll
  refs/mods/AdventureBackpacks.dll
  refs/mods/ZenPlayer.dll
)
for file in "${required[@]}"; do
  [ -f "$file" ] || { echo "missing reference: $file" >&2; exit 1; }
done

echo "Reference assemblies ready:"
du -sh refs/game refs/mods
