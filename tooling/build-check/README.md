# Build check

`Inspect/` loads the real reference DLLs through a `MetadataLoadContext` (nothing is executed)
and audits every binding this mod addresses by name, then checks the version and display-name
contract.

## What it checks

- The exact BepInPlugin GUIDs declared as hard dependencies exist in the referenced mods:
  `ZenDragon.ZenPlayer` and `vapok.mods.adventurebackpacks`.
- `AdventureBackpacks.API.ABAPI.GetAllBackpackInventories(Player)` still exists with the expected
  static/return shape.
- ZenPlayer's auto-pickup seam still exists:
  `AutoPickupRules.CanAddItem_Intercept(Inventory, ItemData, int)`, its private `_pickupMode`
  field, `Configs.AutoPickupExtended`, and `PickupMode.HaveInInventory == 1`. These are the
  members the bridge binds by name, so a ZenPlayer update fails here rather than silently
  disabling the feature.
- `Inventory.HaveItem(string, bool)` still exists in `assembly_valheim`.
- The built assembly version, `Plugin.ModVersion`, the csproj `<Version>`, `manifest.json`'s
  `version_number` and the top CHANGELOG heading all agree.
- `Plugin.ModName` matches the README's H1 heading, so the display name cannot drift.

It exits non-zero on any failure, so `../build.sh` stops before dropping a bad candidate in
`artifacts/debug/`.

## What it does NOT check

Existence is not behaviour: it cannot prove that ZenPlayer passes the expected values, that
Harmony binds the seam at runtime, or that AdventureBackpacks' overflow handles the item. Those
are verified in game.

## Run

```bash
export DOTNET_ROOT=$HOME/.dotnet
export DOTNET_ROLL_FORWARD=LatestMajor
dotnet run --project tooling/build-check/Inspect -c Release
```

Run it from the project root (it resolves `refs/` relatively), or set `MOD_ROOT` to the project
directory. `../decompile.sh <ModName>` dumps a dependency's source to `/tmp/zbb-decomp` when a
failure needs explaining.
