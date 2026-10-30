# ZenPlayer Backpack Bridge — source

Source repository for the Valheim BepInEx mod. The published package page is on Hexium:

<https://valheim.hexium.gg/mods/BootstrapBoys/ZenPlayer_Backpack_Bridge>

The package README, changelog, manifest and icon live in `resources/`; that folder is what gets zipped with the DLL. The in-game test plan is in `docs/TESTING.md`.

## Layout

| Path | Purpose |
| :--- | :--- |
| `zbb_main.cs` | Plugin entry point, configuration and the ZenPlayer auto-pickup postfix |
| `zbb_debugger.cs` | Diagnostics: dedicated log, patch inventory, dumps, decision trace |
| `resources/` | Files shipped inside the mod zip (`README.md`, `CHANGELOG.md`, `manifest.json`, `icon.png`) |
| `refs/game/` | Local Valheim + BepInEx reference assemblies (not committed) |
| `refs/mods/` | Local AdventureBackpacks and ZenPlayer assemblies (not committed) |
| `tooling/` | `build.sh`, `decompile.sh`, `ci-fetch-refs.sh`, and the binding/version audit |
| `artifacts/` | Build output: `debug/` candidate DLL, `release/` packages (not committed) |
| `docs/` | Test plan and development notes |

## Build

Requires the .NET SDK 8 and a local Valheim install. Reference assemblies are deliberately not committed: copy the game's `valheim_Data/Managed` and `BepInEx/core` into `refs/game/`, and `AdventureBackpacks.dll` / `ZenPlayer.dll` into `refs/mods/`, or override the paths:

```bash
VALHEIM_DIR=/path/to/Valheim \
BEPINEX_DIR=/path/to/BepInEx \
MODS_DIR=/path/to/mods \
  ./tooling/build.sh
```

The script builds with warnings-as-errors, runs the binding and version audit, and drops `artifacts/debug/ZenPlayerBackpackBridge.dll` with a `build-info.txt`.

## Release

Push a `vX.Y.Z` tag, or run the **Build and Release** workflow manually with the version. The workflow downloads the reference assemblies (anonymous Valheim dedicated-server depot plus the dependency zips pinned in `resources/manifest.json`), builds with warnings-as-errors, runs the binding/version audit, packages the DLL with `resources/*` at the archive root, writes a `CHECKSUMS.txt`, and publishes a GitHub release.

No game or mod DLLs are committed to the repository.
