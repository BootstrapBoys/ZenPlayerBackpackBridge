using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;
using AdventureBackpacks.API;

namespace ZenPlayerBackpackBridge
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("ZenDragon.ZenPlayer", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("vapok.mods.adventurebackpacks", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.bootstrapboys.zenplayerbackpackbridge";
        public const string ModName = "ZenPlayer Backpack Bridge";
        public const string ModVersion = "1.1.1";

        public static Plugin Instance { get; private set; }

        public static ConfigEntry<bool> ModEnabled;
        public static ConfigEntry<bool> EnableDebugLogging;

        private void Awake()
        {
            Instance = this;

            ModEnabled = Config.Bind("1. General", "Mod Enabled", true, "Enable or disable bridge.");
            EnableDebugLogging = Config.Bind("1. General", "Enable Debug Logging", false, "Print debug info to log.");

            Harmony harmony = new Harmony(ModGUID);
            harmony.PatchAll();

            Logger.LogInfo("ZenPlayer & AdventureBackpacks Bridge v1.1.1 loaded successfully!");
        }

        [HarmonyPatch(typeof(Inventory), "HaveItem", new Type[] { typeof(string), typeof(bool) })]
        public static class Inventory_HaveItem_Patch
        {
            public static bool Prepare()
            {
                return ModEnabled.Value;
            }

            [HarmonyPostfix]
            public static void Postfix(Inventory __instance, string name, bool matchWorldLevel, ref bool __result)
            {
                // If the item was already found, or mod is disabled, or local player is null, do nothing
                if (__result || !ModEnabled.Value || Player.m_localPlayer == null) return;

                try
                {
                    if (!ABAPI.IsLoaded()) return;

                    // Ensure we are checking the local player's main inventory
                    Inventory playerInventory = ((Humanoid)Player.m_localPlayer).GetInventory();
                    if (__instance != playerInventory) return;

                    if (string.IsNullOrEmpty(name)) return;

                    // Only retrieve the inventory of the currently EQUIPPED backpack
                    Inventory equippedBackpackInventory = ABAPI.GetEquippedBackpackInventory(Player.m_localPlayer);
                    if (equippedBackpackInventory == null) return;

                    // Check if the item exists inside the equipped backpack
                    if (equippedBackpackInventory.HaveItem(name, matchWorldLevel))
                    {
                        __result = true;
                        if (EnableDebugLogging.Value)
                        {
                            Plugin.Instance.Logger.LogInfo($"[Bridge] Item '{name}' found in equipped backpack! Overriding HaveItem result to TRUE.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (EnableDebugLogging.Value)
                    {
                        Plugin.Instance.Logger.LogError($"[Bridge] Error in Inventory.HaveItem Postfix patch: {ex}");
                    }
                }
            }
        }
    }
}