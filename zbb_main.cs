using System;
using System.Collections.Generic;
using System.Reflection;
using AdventureBackpacks.API;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ZenPlayerBackpackBridge
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("ZenDragon.ZenPlayer", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("vapok.mods.adventurebackpacks", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.bootstrapboys.zenplayerbackpackbridge";
        public const string ModName = "ZenPlayer Backpack Bridge";
        public const string ModVersion = "1.3.1";

        public static Plugin Instance { get; private set; }

        internal static BepInEx.Logging.ManualLogSource LogSource => Instance?.Logger;

        public static ConfigEntry<bool> ModEnabled;
        public static ConfigEntry<bool> EnableDebugLogging;
        public static ConfigEntry<KeyboardShortcut> DumpKey;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            ZbbDebugger.Open();

            ModEnabled = Config.Bind("1. General", "Mod Enabled", true,
                "Enable or disable the bridge. Takes effect immediately.");
            EnableDebugLogging = Config.Bind("1. General", "Enable Debug Logging", false,
                "Log every decision the bridge makes (throttled per item).");
            DumpKey = Config.Bind("2. Diagnostics", "Dump Key",
                new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl, KeyCode.LeftShift),
                "Press in game to write a full diagnostics dump to the BepInEx log. Set to None to disable.");

            _harmony = new Harmony(ModGUID);

            try
            {
                _harmony.PatchAll();
                ZbbDebugger.LogInfo($"{ModName} v{ModVersion} loaded" +
                    (ZenPlayerPickupPatch.SeamBound ? "." : " (bridge inactive)."));
                ZbbDebugger.Initialize();
            }
            catch (Exception ex)
            {
                ZbbDebugger.LogError($"{ModName} v{ModVersion} failed to apply patches.", ex);
            }
        }

        private void Update()
        {
            ZbbDebugger.HandleDumpKey();
        }

        private void OnDestroy()
        {
            ZbbDebugger.Shutdown();
        }

        [HarmonyPatch]
        public static class ZenPlayerPickupPatch
        {
            // ZenPlayer's auto-pickup gate. Its transpiler replaces Inventory.CanAddItem inside
            // Player.AutoPickup with this method, which answers "may I pick this item up?" by
            // combining its "have in inventory" filter with the game's capacity check.
            private static MethodBase _seam;

            // ZenPlayer's own state, used to tell why the seam said no.
            private static FieldInfo _pickupModeField;
            private static Func<bool> _autoPickupExtended;
            private static int _haveInInventory = int.MinValue;

            // GetAllBackpackInventories allocates; AutoPickup calls the seam per nearby item per
            // frame, so the list is reused for the whole frame.
            private static int _cacheFrame = -1;
            private static Player _cachePlayer;
            private static List<Inventory> _cacheInventories;

            internal static bool SeamBound => _seam != null;

            internal static string SeamName =>
                _seam == null ? "(not bound)" : $"{_seam.DeclaringType?.FullName}.{_seam.Name}";

            internal static bool ExtendedEnabled
            {
                get
                {
                    try
                    {
                        return _autoPickupExtended != null && _autoPickupExtended();
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            internal static bool FilterActive => IsHaveInInventoryMode();

            internal static string ModeName
            {
                get
                {
                    if (_pickupModeField == null) return "(unbound)";

                    try
                    {
                        object value = _pickupModeField.GetValue(null);
                        return value == null
                            ? "(null)"
                            : Enum.GetName(_pickupModeField.FieldType, value) ?? value.ToString();
                    }
                    catch
                    {
                        return "(error)";
                    }
                }
            }

            public static bool Prepare()
            {
                try
                {
                    Type rules = null;
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name != "ZenPlayer") continue;
                        rules = asm.GetType("ZenPlayer.Sections.AutoPickupRules", false);
                        break;
                    }

                    if (rules == null)
                    {
                        ZbbDebugger.LogWarning(
                            $"{ModName}: type ZenPlayer.Sections.AutoPickupRules not found; bridge inactive.");
                        return false;
                    }

                    _seam = AccessTools.Method(rules, "CanAddItem_Intercept",
                        new[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int) });
                    _pickupModeField = AccessTools.Field(rules, "_pickupMode");

                    Type configs = rules.GetNestedType("Configs", BindingFlags.Public | BindingFlags.NonPublic);
                    object extendedEntry = configs?
                        .GetField("AutoPickupExtended", BindingFlags.Public | BindingFlags.Static)?
                        .GetValue(null);
                    MethodInfo valueGetter = extendedEntry?
                        .GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?
                        .GetGetMethod();
                    if (valueGetter != null)
                    {
                        try
                        {
                            _autoPickupExtended = (Func<bool>)Delegate.CreateDelegate(
                                typeof(Func<bool>), extendedEntry, valueGetter);
                        }
                        catch
                        {
                            _autoPickupExtended = () => (bool)valueGetter.Invoke(extendedEntry, null);
                        }
                    }

                    Type mode = rules.GetNestedType("PickupMode", BindingFlags.Public | BindingFlags.NonPublic);
                    FieldInfo haveInInventory = mode?.GetField("HaveInInventory",
                        BindingFlags.Public | BindingFlags.Static);
                    if (haveInInventory != null)
                    {
                        _haveInInventory = Convert.ToInt32(haveInInventory.GetRawConstantValue());
                    }

                    string missing =
                        _seam == null ? "CanAddItem_Intercept(Inventory, ItemData, int)" :
                        _pickupModeField == null ? "AutoPickupRules._pickupMode" :
                        _autoPickupExtended == null ? "Configs.AutoPickupExtended" :
                        _haveInInventory == int.MinValue ? "PickupMode.HaveInInventory" : null;

                    if (missing != null)
                    {
                        ZbbDebugger.LogWarning(
                            $"{ModName}: missing {missing}; bridge inactive.");
                        return false;
                    }

                    ZbbDebugger.LogInfo($"{ModName}: found ZenPlayer auto-pickup filter {SeamName}.");
                    return true;
                }
                catch (Exception ex)
                {
                    ZbbDebugger.LogWarning(
                        $"{ModName}: binding failed ({ex.Message}); bridge inactive.");
                    return false;
                }
            }

            public static MethodBase TargetMethod()
            {
                return _seam;
            }

            // __0/__1 are positional (inventory, item); name-independent, so a ZenPlayer parameter
            // rename cannot silently unbind the patch.
            [HarmonyPostfix]
            public static void Postfix(Inventory __0, ItemDrop.ItemData __1, ref bool __result)
            {
                ZbbDebugger.Count(PickupOutcome.SeamCall);

                if (__result)
                {
                    ZbbDebugger.Count(PickupOutcome.AlreadyAllowed);
                    return;
                }

                if (!ModEnabled.Value)
                {
                    ZbbDebugger.Count(PickupOutcome.Skipped);
                    return;
                }

                try
                {
                    Player player = Player.m_localPlayer;
                    if (player == null || __0 == null || __1 == null)
                    {
                        ZbbDebugger.Count(PickupOutcome.Skipped);
                        return;
                    }

                    if (!ReferenceEquals(__0, player.GetInventory()))
                    {
                        ZbbDebugger.Count(PickupOutcome.Skipped);
                        return;
                    }

                    string itemName = __1.m_shared?.m_name;

                    // Only extend "have in inventory" filtering. "Everything" needs no help,
                    // and "Nothing" must stay off.
                    if (!IsHaveInInventoryMode())
                    {
                        ZbbDebugger.Trace(PickupOutcome.ModeInactive, itemName);
                        return;
                    }

                    // Preserve the game's capacity decision, including AdventureBackpacks'
                    // inventory overflow into the equipped backpack.
                    if (!__0.CanAddItem(__1, -1))
                    {
                        ZbbDebugger.Trace(PickupOutcome.NoCapacity, itemName);
                        return;
                    }

                    if (string.IsNullOrEmpty(itemName))
                    {
                        ZbbDebugger.Count(PickupOutcome.Skipped);
                        return;
                    }

                    List<Inventory> backpacks = GetBackpackInventories(player);
                    if (backpacks == null)
                    {
                        ZbbDebugger.Count(PickupOutcome.Skipped);
                        return;
                    }

                    foreach (Inventory backpack in backpacks)
                    {
                        if (backpack != null && backpack.HaveItem(itemName, true))
                        {
                            __result = true;
                            ZbbDebugger.Trace(PickupOutcome.Allowed, itemName,
                                backpack.GetAllItems().Count);
                            return;
                        }
                    }

                    ZbbDebugger.Trace(PickupOutcome.NotInBackpack, itemName, backpacks.Count);
                }
                catch (Exception ex)
                {
                    ZbbDebugger.Count(PickupOutcome.Error);
                    ZbbDebugger.LogErrorOnce($"{ModName}: pickup filter error", ex);
                }
            }

            private static int _modeFrame = -1;
            private static bool _modeActive;

            private static bool IsHaveInInventoryMode()
            {
                int frame = Time.frameCount;
                if (frame == _modeFrame) return _modeActive;

                _modeFrame = frame;
                _modeActive = ComputeModeActive();
                return _modeActive;
            }

            private static bool ComputeModeActive()
            {
                if (_autoPickupExtended == null || _pickupModeField == null ||
                    _haveInInventory == int.MinValue)
                {
                    return false;
                }

                return _autoPickupExtended() &&
                       Convert.ToInt32(_pickupModeField.GetValue(null)) == _haveInInventory;
            }

            private static List<Inventory> GetBackpackInventories(Player player)
            {
                int frame = Time.frameCount;
                if (frame != _cacheFrame || !ReferenceEquals(player, _cachePlayer))
                {
                    _cacheInventories = ABAPI.GetAllBackpackInventories(player);
                    _cacheFrame = frame;
                    _cachePlayer = player;
                }

                return _cacheInventories;
            }
        }
    }
}
