# In-game test plan — ZenPlayer Backpack Bridge 1.3.3

## Setup

1. Install `artifacts/debug/ZenPlayerBackpackBridge.dll` into `BepInEx/plugins/` (or deploy it through your mod manager).
2. In ZenPlayer's config, enable **Autopickup → Have In Inventory - Enable** and set the autopickup mode to **Inventory** (cycle it with ZenPlayer's toggle key; the HUD shows the current mode).
3. Note whether AdventureBackpacks' **Enable Inventory Overflow To Backpack** is on. Scenario 6 depends on it.
4. In the bridge config, set **Enable Debug Logging = true**. Leave **Dump Key** at `Ctrl+Shift+F10` unless it conflicts with another mod.
5. Launch the game, load a world, and keep `BepInEx/BSBLogs/ZenPlayer_Backpack_Bridge.log` open (the bridge writes there; `LogOutput.log` gets the same lines).

Every scenario below assumes **Inventory** mode unless it says otherwise.

## 1. Startup evidence — do this first

Search the log for these lines before touching an item:

```
[ZBB][INFO] ZenPlayer Backpack Bridge v1.3.3 loaded.
[ZBB][INFO] [Log] .../BepInEx/BSBLogs/ZenPlayer_Backpack_Bridge.log
[ZBB][INFO] ZenPlayer Backpack Bridge: found ZenPlayer auto-pickup filter ZenPlayer.Sections.AutoPickupRules.CanAddItem_Intercept.
[ZBB][INFO] [Patches] ZenPlayer.Sections.AutoPickupRules.CanAddItem_Intercept <- 1 patch(es)
[ZBB][INFO] [ZenPlayer] version=1.3.0 seam=ZenPlayer.Sections.AutoPickupRules.CanAddItem_Intercept bound=True
[ZBB][INFO] [ZenPlayer] extended=True mode=HaveInInventory filterActive=True
```

If `bound=False`, `[Patches] none owned ...` appears, or the "found" line is missing, stop and send the log. Nothing below can work until that is fixed.

## 2. Core matrix

Press the dump key after each scenario and keep the block. Press it while standing still, not in a menu.

| # | Setup | Expected | Dump evidence |
|---|-------|----------|---------------|
| 1 | Drop an item you do **not** have in your inventory or any backpack | Not picked up | `NotInBackpack` trace for that item |
| 2 | Put the same item in your player inventory, drop another | Picked up normally | `already` counter rises; no bridge override |
| 3 | Put the item only in the **equipped** backpack, drop another | Picked up | `Allowed` trace with `equippedBackpackItems=N` |
| 4 | Put the item only in a backpack, then **unequip** it (still carried in inventory) | **Not** picked up | `NotInBackpack` with `equippedBackpackItems=-1` |
| 4b | Equip that same backpack and drop another | Picked up | `Allowed` trace |
| 5 | Fill the player inventory and every backpack, drop an item that is in the equipped backpack | Not picked up, no repeated "No room in inventory" | `NoCapacity` trace; `allowed` unchanged |
| 6 | Fill the player inventory, enable AB overflow, leave room in the **equipped** backpack, item also in that backpack | Picked up into the equipped backpack | `Allowed` trace; item appears in that backpack |
| 7 | Set ZenPlayer mode to **Nothing**, item in a backpack | Not picked up | `modeInactive` counter rises; config shows `mode=Nothing` |
| 8 | Set ZenPlayer mode to **Everything**, any item | Vanilla/AdventureBackpacks behaviour; bridge never overrides | `modeInactive` counter rises, or `already`; config shows `mode=Everything` |
| 9 | Turn **Mod Enabled** off, repeat #3, turn it back on | No bridge pickup while off; pickup resumes without a restart | `skipped` counter rises while off |
| 10 | Run `zen_player inventory`, then `zen_player equipped` | Items listed under the correct section; no backpack item appears in the wrong list | visual check |
| 11 | Two backpacks both holding the same item | One pickup, no duplicate or extra item | a single `Allowed` trace |
| 12 | Drop an item that would exceed your carry weight, with a copy in a backpack | Not picked up | may show `Allowed`; the game's weight check still refuses it |
| 13 | Drop a large pile of mixed items, disable debug logging, and walk through it | No stutter; pickup behaviour unchanged | counters still rise; trace is not recorded with debug off |

Scenario 12 is expected to look contradictory: the bridge only answers the inventory-capacity question, and `Player.AutoPickup` separately checks carry weight. `Allowed` in the trace plus an item still on the ground is correct if you are overweight.

Scenario 4 is the regression this release fixes: an unequipped backpack must contribute nothing to the filter.

## 3. Reading the dump

`Ctrl+Shift+F10` writes the following block:

```
[ZBB][INFO] ==================== ZBB DUMP START ====================
[ZBB][INFO] [Config] ZenPlayer Backpack Bridge v1.3.3 ModEnabled=True Debug=True DumpKey=F10 + LeftControl + LeftShift LogFile=.../BepInEx/BSBLogs/ZenPlayer_Backpack_Bridge.log
[ZBB][INFO] [ZenPlayer] version=1.3.0 seam=... bound=True
[ZBB][INFO] [ZenPlayer] extended=True mode=HaveInInventory filterActive=True
[ZBB][INFO] [AdventureBackpacks] version=2.4.6 equippedBackpack=12 items allBackpacks=2
[ZBB][INFO] [Patches] ZenPlayer.Sections.AutoPickupRules.CanAddItem_Intercept <- 1 patch(es)
[ZBB][INFO] [Patches] 1 patch(es) owned by com.bootstrapboys.zenplayerbackpackbridge
[ZBB][INFO] [Player] inventory=24 items ...
[ZBB][INFO] [Player] equipped backpack=12 items
[ZBB][INFO] [Backpacks] filter uses the equipped backpack only
[ZBB][INFO] [Backpacks] #0 [equipped] items=12 $item_wood x50, $item_stone x30
[ZBB][INFO] [Backpacks] #1 items=8 $item_resin x20
[ZBB][INFO] [Counters] seam=1843 already=402 skipped=0 modeInactive=0 noCapacity=6 notInBackpack=1435 allowed=7 errors=0
[ZBB][INFO] [Trace] newest first (64 recorded, last 64 kept)
[ZBB][INFO] [Trace] f12345 Allowed         $item_wood equippedBackpackItems=12
[ZBB][INFO] [Trace] f12340 NotInBackpack   $item_stone equippedBackpackItems=-1
[ZBB][INFO] ===================== ZBB DUMP END =====================
```

- **`seam`** is the number of times ZenPlayer asked the pickup gate since launch. If it stays at 0 while you walk over items, the patch is not in the call path — check the startup lines and ZenPlayer's mode.
- **`already`** means ZenPlayer's own filter passed; the bridge did nothing.
- **`allowed`** is the number of pickups the bridge enabled.
- **`noCapacity`** means `Inventory.CanAddItem` returned false — either the player inventory and all overflow targets are full, or AdventureBackpacks refused.
- **`notInBackpack`** means the item was not found in the **equipped** backpack. `equippedBackpackItems=-1` means no backpack was equipped at all; `0` means one is equipped but empty.
- **`modeInactive`** means ZenPlayer was not in extended **Inventory** mode at that moment.

The trace is only recorded when **Enable Debug Logging** is on and is throttled to one entry per item per outcome every 2 seconds, so it does not grow per frame. Counters are exact and always update.

## 4. Failure signatures

| Symptom | Likely cause | What to capture |
|---|---|---|
| `seam=0` after walking over items | ZenPlayer extended autopickup off, mode not Inventory, or patch not bound | full startup block + `[Config]`/`[ZenPlayer]` dump |
| `allowed` rises but the item stays on the ground | A later gate refused it (carry weight, quest item, AB restriction) | item name, whether you are overweight, screenshot |
| `NoCapacity` with empty inventory slots | `CanAddItem` returned false; check AB overflow and equipped backpack room | dump + AdventureBackpacks config |
| `NotInBackpack` while the item is visibly in a backpack | Expected if the backpack is carried but not equipped; otherwise a name/world-level mismatch | whether the backpack was equipped, `[Backpacks]` summary + screenshot |
| Repeated "No room in inventory" | The capacity guard failed | dump + the item name |
| `[Patches] none owned` | Harmony did not apply the postfix | the warning and the "found/missing" line above it |
| No trace entries at all | The seam was never called | startup block + dump |

## 5. What to send

- `BepInEx/BSBLogs/ZenPlayer_Backpack_Bridge.log` with debug logging on, containing one dump per failing scenario. Press the key **before** closing the session; the counters reset on restart.
- A screenshot of the HUD pickup mode (Everything / Inventory / Nothing) and of the relevant inventory/backpack.
- The exact item name (the `$item_...` key from the dump) and whether the backpack was **equipped or merely carried**.
