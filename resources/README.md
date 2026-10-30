# ZenPlayer Backpack Bridge

A lightweight BepInEx compatibility bridge for Valheim that lets **ZenPlayer**'s filtered auto-pickup see items stored in **AdventureBackpacks** backpacks.

## The problem

ZenPlayer's **Have In Inventory** autopickup mode only picks up items you already carry in your player inventory. Items inside your equipped AdventureBackpack live in a separate inventory, so ZenPlayer ignores them — you stop picking up wood because all your wood is in a backpack.

## What it does

While ZenPlayer is in Inventory mode, the bridge treats an item found in your equipped backpack as "already in inventory", so autopickup proceeds. A backpack that is merely carried in your inventory but not equipped is ignored. Capacity is still decided by the game and AdventureBackpacks: if neither your inventory nor AdventureBackpacks' inventory overflow into the equipped backpack can accept the item, it stays on the ground. The bridge itself never moves items.

It does nothing in ZenPlayer's **Everything** mode (vanilla behaviour already works) or **Nothing** mode (autopickup is deliberately off).

## Requirements

| Mod | Version | Link |
| :--- | :--- | :--- |
| **BepInExPack Valheim** | 5.4.2360+ | [Hexium](https://valheim.hexium.gg/mods/denikson/BepInExPack_Valheim) |
| **ZenPlayer** | 1.3.0+ | [Hexium](https://valheim.hexium.gg/mods/ZenDragon/ZenPlayer) |
| **AdventureBackpacks** | 2.4.6+ | [Thunderstore](https://valheim.thunderstore.io/package/Vapok/AdventureBackpacks/) |

ZenPlayer must have **Autopickup → Have In Inventory - Enable** turned on and its autopickup mode set to **Inventory**.

## Installation

### Mod manager

Install **ZenPlayer Backpack Bridge** from [Hexium](https://valheim.hexium.gg/mods/BootstrapBoys/ZenPlayer_Backpack_Bridge) alongside the dependencies above. Gale handles the install.

### Manual

1. Install BepInExPack Valheim, ZenPlayer and AdventureBackpacks.
2. Place `ZenPlayerBackpackBridge.dll` in `Valheim/BepInEx/plugins/`.

## Configuration

- **Mod Enabled** — turn the bridge on or off; takes effect immediately.
- **Enable Debug Logging** — log every decision the bridge makes, throttled per item.
- **Dump Key** — writes a full diagnostics dump to the log; default `Ctrl+Shift+F10`.

## Diagnostics

The bridge writes its own log to `BepInEx/BSBLogs/ZenPlayer_Backpack_Bridge.log`, separate from `LogOutput.log`. It is appended per session and rotated to `.old` at 5 MB. Press the dump key in game to append the current config, patch inventory, player/backpack contents, counters and recent pickup decisions to that file.

## Troubleshooting

- Confirm ZenPlayer's autopickup mode is **Inventory** and **Have In Inventory - Enable** is on; the bridge is inactive in the other two modes.
- Confirm the backpack is **equipped**, not just carried in your inventory; carried backpacks do not count.
- If backpack items are still ignored, enable **Enable Debug Logging**, reproduce the problem, press the dump key, and send `BepInEx/BSBLogs/ZenPlayer_Backpack_Bridge.log`.
- The dump's `[ZenPlayer]` line shows the detected mode and whether the bridge bound to ZenPlayer's auto-pickup filter.
