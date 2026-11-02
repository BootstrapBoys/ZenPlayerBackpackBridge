# Changelog

All notable changes to the "ZenPlayer Backpack Bridge" project will be documented in this file.

---

## [1.3.5] - 2026-11-02

### Fixed
- Large ground piles no longer pay AdventureBackpacks' expensive capacity/overflow scan for items that cannot fit in either the player inventory or the equipped backpack. The bridge now checks for free space first and only calls the full capacity path when a fit is possible.

---

## [1.3.4] - 2026-11-01

### Changed
- Moved **Enable Debug Logging** into the **Diagnostics** config section alongside **Dump Key**. An existing enabled value is migrated automatically.

---

## [1.3.3] - 2026-11-01

### Fixed
- Reduced autopickup overhead when many items are on the ground: the equipped backpack's item names are cached per frame and checked before the capacity path, so `Inventory.CanAddItem` is only called for items that are actually in the backpack.
- Mode-inactive handling no longer traces every ground item, and the recent-decision trace is skipped entirely unless debug logging is enabled.

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
