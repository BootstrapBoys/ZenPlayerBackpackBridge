# Changelog

All notable changes to the "ZenPlayer & Adventure Backpacks Bridge" project will be documented in this file.

---

## [1.1.1] - 2026-10-22

### Fixed

* **Equipped Check Restriction**: Replaced `ABAPI.GetAllBackpackInventories` with `ABAPI.GetEquippedBackpackInventory`. Items inside unequipped backpacks (e.g., backpacks stored inside the main inventory or chests) are now properly ignored.

---

## [1.1.0] - 2026-10-22

### Changed
- **Optimized Hook Architecture**: Replaced reflection-based assembly scanning (`TargetMethods`) with a direct, targeted Harmony Postfix patch on `Inventory.HaveItem`.
- **Improved Performance**: Eliminated runtime startup overhead caused by searching loaded `AppDomain` assemblies and types for method name substrings.

### Fixed
- Fixed potential instability and fragility issues where minor code updates or method renames in ZenPlayer could break dynamic hooks.
- Fixed unnecessary patch execution on non-player inventory checks by adding early guard checks for `Player.m_localPlayer`'s inventory.

### Security & Stability
- Added safety checks around backpack inventory retrieval to ensure compatibility and prevent null-reference exceptions during scene loads.

---

## [1.0.0] - 2026-10-15

### Added
- Initial release of **ZenPlayer Backpack Bridge**.
- Core synchronization patch between ZenPlayer inventory slots and AdventureBackpacks equipment grids.
- Combined weight, stamina, and encumbrance balance rules.
- Configuration file generation (`com.zenplayer.backpackbridge.cfg`).