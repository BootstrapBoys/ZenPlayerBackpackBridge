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
| `tooling/` | `build.sh`, `decompile.sh`, and the binding/version audit |
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

Packaging is intentionally manual and only happens on request: build, then zip the DLL with `resources/{README.md,CHANGELOG.md,manifest.json,icon.png}` at the archive root. There is no GitHub Actions build, because the game reference assemblies cannot be redistributed with the repository.
