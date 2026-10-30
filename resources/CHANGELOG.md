# Changelog

All notable changes to the "ZenPlayer Backpack Bridge" project will be documented in this file.

---

## [1.3.2] - 2026-10-30

### Fixed
- Backpack contents now count only while the backpack is equipped. A backpack carried in the player inventory no longer satisfies ZenPlayer's **Have In Inventory** filter.
- Removed the duplicate "found ZenPlayer auto-pickup filter" startup log line caused by Harmony invoking `Prepare()` twice.

---

## [1.3.1] - 2026-10-29

### Added
- Dedicated log at `BepInEx/BSBLogs/ZenPlayer_Backpack_Bridge.log`, appended per session and rotated to `.old` at 5 MB.
- Startup patch inventory and config snapshot when debug logging is enabled.
- Exact per-decision counters plus a throttled trace of recent pickup decisions.
- **Dump Key** (default `Ctrl+Shift+F10`) writes a full diagnostics dump to the dedicated log and the BepInEx log.

### Fixed
- The bridge no longer overrides a failed inventory-capacity check. A pickup now proceeds only when the player inventory, or AdventureBackpacks' inventory overflow into the equipped backpack, can actually accept the item.
- The bridge now respects ZenPlayer's autopickup mode: it only extends the **Have In Inventory** filter, no longer re-enables pickups in **Nothing** mode and no longer interferes with **Everything** mode.
- The bridge now patches only ZenPlayer's auto-pickup filter. The previous broad scan also patched unrelated methods, including the `printinventory` command's item predicate and per-frame helpers.
- Patch failures, missing ZenPlayer bindings and runtime errors are now logged instead of failing silently or aborting plugin load.

### Changed
- Backpack inventory lookup is cached per frame, removing repeated allocations while autopickup scans nearby items.
- Display name unified to "ZenPlayer Backpack Bridge" across the plugin, README and changelog.

---

## [1.3.0] - 2026-10-22

### Added
- Dynamic reflection-based universal patching targeting ZenPlayer inventory and item detection methods.
- Comprehensive configuration support with live toggles for Mod Enabled and Debug Logging.

### Changed
- Upgraded target framework to .NET Framework 4.8 (`net48`) for full compatibility with latest mod assemblies.

### Fixed
- Fixed issues where items in Adventure Backpacks were ignored by ZenPlayer's `HaveInInventory` autopickup filter.
