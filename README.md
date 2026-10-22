# ZenPlayer Backpack Bridge

**ZenPlayer Backpack Bridge** is a Valheim compatibility mod designed to bridge inventory checks between **ZenPlayer** and **AdventureBackpacks**.

It dynamically hooks into ZenPlayer's inventory verification methods to ensure that items stored inside equipped backpacks are recognized during item and requirement checks—preventing failed checks when items exist within a backpack inventory.

---

## 🚀 How It Works

When ZenPlayer queries the player's inventory for specific items or prerequisites (e.g., checking if the player possesses required materials or tools):
1. **Primary Inventory Check:** ZenPlayer runs its default inventory checks.
2. **Backpack Interception:** If the primary check returns `false`, ZenPlayer Backpack Bridge intercepts the result and queries `AdventureBackpacks.API` (`ABAPI`).
3. **Backpack Scanning:** It checks all currently equipped backpack inventories on `Player.m_localPlayer` for matching `ItemDrop.ItemData` or localization string identifiers (`$item_...`).
4. **Override Result:** If the item is located inside an equipped backpack, the check dynamically overrides the result to `true`.

---

## 🛠️ Features

* **Dynamic Reflection Patching:** Automatically discovers and hooks relevant ZenPlayer inventory and item check methods (`Inventory`, `Item`, `Have`, `Check`) at runtime.
* **AdventureBackpacks API Integration:** Direct integration via `ABAPI.GetAllBackpackInventories()` to safely search equipped backpacks.
* **Zero Inventory Duplication:** Read-only checks that preserve backpack contents and native player inventories.
* **Configurable Logging:** Optional debug logging for tracking hooked methods and intercepted item overrides in real time.

---

## 📋 Requirements

This mod requires the following dependencies to function:

| Mod | GUID | Link |
| :--- | :--- | :--- |
| **BepInExPack Valheim** | N/A | [Hexium](https://valheim.hexium.gg/mods/denikson/BepInExPack_Valheim) |
| **ZenPlayer** | `ZenDragon.ZenPlayer` | [Hexium](https://valheim.hexium.gg/mods/ZenDragon/ZenPlayer) |
| **AdventureBackpacks** | `vapok.mods.adventurebackpacks` | [Thunderstore](https://valheim.thunderstore.io/package/Vapok/AdventureBackpacks/) |

---

## ⚙️ Installation

### **Via Mod Manager (r2modman / Thunderstore Mod Manager)**
1. Open your mod manager and select **Valheim**.
2. Search for `ZenPlayer Backpack Bridge`.
3. Click **Download with Dependencies**.

### **Manual Installation**
1. Install [BepInExPack Valheim](https://valheim.hexium.gg/mods/denikson/BepInExPack_Valheim), [ZenPlayer](https://valheim.hexium.gg/mods/ZenDragon/ZenPlayer), and [AdventureBackpacks](https://valheim.thunderstore.io/package/Vapok/AdventureBackpacks/).
2. Download `ZenPlayerBackpackBridge.dll`.
3. Place `ZenPlayerBackpackBridge.dll` inside your Valheim installation folder under:
   `Valheim/BepInEx/plugins/`

---

## ⚙️ Configuration

A configuration file is generated upon first run at:
`Valheim/BepInEx/config/com.bootstrapboys.zenplayerbackpackbridge.cfg`

```ini
[1. General]

## Enable or disable bridge.
# Setting type: Boolean
# Default value: true
Mod Enabled = true

## Print debug info to log.
# Setting type: Boolean
# Default value: false
Enable Debug Logging = false
```

---

## 🐛 Troubleshooting & Debugging

If items in your backpack are not being registered:
1. Ensure **Mod Enabled** is set to `true` in `com.bootstrapboys.zenplayerbackpackbridge.cfg`.
2. Enable **Enable Debug Logging** in the config.
3. Open `BepInEx/LogOutput.log` and search for entries tagged with `[Bridge v1.1.1]`.
   * You should see lines indicating hooked methods on boot:
     `[Bridge v1.1.1] Successfully hooked ZenPlayer method: CheckClass.HaveItem`
   * When checking items, successful overrides will log:
     `[Bridge v1.1.1] Method HaveItem missed item '$item_stone', but found it in backpack! Overriding result to TRUE.`
