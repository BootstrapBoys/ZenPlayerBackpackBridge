using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AdventureBackpacks.API;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ZenPlayerBackpackBridge
{
    /// <summary>
    /// Why the bridge did or did not allow a pickup. Counted on every seam call; the most
    /// interesting outcomes are also kept in a small ring buffer for the on-demand dump.
    /// </summary>
    internal enum PickupOutcome
    {
        SeamCall,
        AlreadyAllowed,
        Skipped,
        ModeInactive,
        NoCapacity,
        NotInBackpack,
        Allowed,
        Error,
        Count
    }

    /// <summary>
    /// All diagnostics live here so the gameplay code stays free of logging calls.
    /// Nothing in this class may throw: a failed log line must never break a pickup.
    /// </summary>
    internal static class ZbbDebugger
    {
        private const int TraceCapacity = 64;
        private const float SameItemInterval = 2f;

        private struct TraceEntry
        {
            public int Frame;
            public string Item;
            public PickupOutcome Outcome;
            public string Detail;
        }

        private struct LastEntry
        {
            public PickupOutcome Outcome;
            public float Time;
        }

        private static readonly TraceEntry[] _trace = new TraceEntry[TraceCapacity];
        private static readonly long[] _counts = new long[(int)PickupOutcome.Count];
        private static readonly Dictionary<string, LastEntry> _lastTrace =
            new Dictionary<string, LastEntry>();

        private static int _traceNext;
        private static int _traceCount;
        private static bool _errorLogged;

        private static readonly object _fileLock = new object();
        private static StreamWriter _writer;
        private static string _logPath;

        public static string LogPath => _logPath;

        public static bool Enabled
        {
            get
            {
                try
                {
                    return Plugin.EnableDebugLogging != null && Plugin.EnableDebugLogging.Value;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void Open()
        {
            try
            {
                OpenLog();

                if (_writer != null)
                {
                    WriteFile("SESSION",
                        $"===== {Plugin.ModName} v{Plugin.ModVersion} session start =====");
                }
            }
            catch (Exception ex)
            {
                Write("WARN", $"could not open diagnostics log: {ex.Message}");
            }
        }

        public static void Initialize()
        {
            try
            {
                if (!Enabled)
                {
                    WarnIfUnpatched();
                    return;
                }

                LogInfo($"[Log] {_logPath}");
                DumpConfig();
                DumpPatchInventory();
            }
            catch (Exception ex)
            {
                Write("ERROR", $"diagnostics init failed: {ex}");
            }
        }

        /// <summary>Called from Plugin.Update; does nothing until a key is configured/pressed.</summary>
        public static void HandleDumpKey()
        {
            try
            {
                ConfigEntry<KeyboardShortcut> key = Plugin.DumpKey;
                if (key == null) return;

                KeyboardShortcut shortcut = key.Value;
                if (shortcut.MainKey == KeyCode.None) return;
                if (shortcut.IsDown()) DumpAll();
            }
            catch
            {
                // Input/config problems must not affect gameplay.
            }
        }

        public static void DumpAll()
        {
            try
            {
                LogInfo("==================== ZBB DUMP START ====================");
                DumpConfig();
                DumpPatchInventory();
                DumpPlayer();
                DumpCounters();
                DumpTrace();
                LogInfo("===================== ZBB DUMP END =====================");
            }
            catch (Exception ex)
            {
                Write("ERROR", $"diagnostics dump failed: {ex}");
            }
        }

        public static void DumpConfig()
        {
            try
            {
                LogInfo($"[Config] {Plugin.ModName} v{Plugin.ModVersion} ModEnabled={Plugin.ModEnabled.Value} Debug={Enabled} DumpKey={Plugin.DumpKey.Value} LogFile={_logPath}");
                LogInfo($"[ZenPlayer] version={VersionOf("ZenPlayer")} seam={Plugin.ZenPlayerPickupPatch.SeamName} bound={Plugin.ZenPlayerPickupPatch.SeamBound}");
                LogInfo($"[ZenPlayer] extended={Plugin.ZenPlayerPickupPatch.ExtendedEnabled} mode={Plugin.ZenPlayerPickupPatch.ModeName} filterActive={Plugin.ZenPlayerPickupPatch.FilterActive}");
                LogInfo($"[AdventureBackpacks] version={VersionOf("AdventureBackpacks")} equippedBackpack={EquippedBackpackState()} allBackpacks={BackpackCount()}");
            }
            catch (Exception ex)
            {
                Write("ERROR", $"config dump failed: {ex}");
            }
        }

        public static void DumpPatchInventory()
        {
            try
            {
                int total = 0;
                foreach (MethodBase method in Harmony.GetAllPatchedMethods())
                {
                    Patches info = Harmony.GetPatchInfo(method);
                    int mine = CountOwned(info);
                    if (mine == 0) continue;

                    total += mine;
                    LogInfo($"[Patches] {Describe(method)} <- {mine} patch(es)");

                    foreach (Patch p in info.Postfixes.Where(IsMine))
                        LogDebug($"[Patches]   postfix {Describe(p.PatchMethod)} priority={p.priority}");
                    foreach (Patch p in info.Prefixes.Where(IsMine))
                        LogDebug($"[Patches]   prefix {Describe(p.PatchMethod)} priority={p.priority}");
                }

                if (total == 0)
                    LogWarning($"[Patches] none owned by {Plugin.ModGUID}; bridge inactive (seam={Plugin.ZenPlayerPickupPatch.SeamName})");
                else
                    LogInfo($"[Patches] {total} patch(es) owned by {Plugin.ModGUID}");
            }
            catch (Exception ex)
            {
                Write("ERROR", $"patch inventory failed: {ex}");
            }
        }

        public static void DumpPlayer()
        {
            try
            {
                Player player = Player.m_localPlayer;
                if (player == null)
                {
                    LogInfo("[Player] none");
                    return;
                }

                Inventory inventory = player.GetInventory();
                LogInfo($"[Player] inventory={inventory?.GetAllItems().Count ?? 0} items {Summary(inventory)}");

                Inventory equipped = ABAPI.GetEquippedBackpackInventory(player);
                LogInfo($"[Player] equipped backpack={(equipped == null ? "none" : equipped.GetAllItems().Count + " items")}");
                LogInfo("[Backpacks] filter uses the equipped backpack only");

                List<Inventory> backpacks = ABAPI.GetAllBackpackInventories(player);
                if (backpacks == null || backpacks.Count == 0)
                {
                    LogInfo("[Backpacks] none found (equipped or carried)");
                    return;
                }

                for (int i = 0; i < backpacks.Count; i++)
                {
                    Inventory backpack = backpacks[i];
                    string tag = ReferenceEquals(backpack, equipped) ? " [equipped]" : "";
                    LogInfo($"[Backpacks] #{i}{tag} items={backpack?.GetAllItems().Count ?? 0} {Summary(backpack)}");
                }
            }
            catch (Exception ex)
            {
                Write("ERROR", $"player dump failed: {ex}");
            }
        }

        public static void DumpCounters()
        {
            try
            {
                LogInfo(
                    $"[Counters] seam={CountOf(PickupOutcome.SeamCall)} already={CountOf(PickupOutcome.AlreadyAllowed)} " +
                    $"skipped={CountOf(PickupOutcome.Skipped)} modeInactive={CountOf(PickupOutcome.ModeInactive)} " +
                    $"noCapacity={CountOf(PickupOutcome.NoCapacity)} notInBackpack={CountOf(PickupOutcome.NotInBackpack)} " +
                    $"allowed={CountOf(PickupOutcome.Allowed)} errors={CountOf(PickupOutcome.Error)}");
            }
            catch (Exception ex)
            {
                Write("ERROR", $"counter dump failed: {ex}");
            }
        }

        public static void DumpTrace()
        {
            try
            {
                if (_traceCount == 0)
                {
                    LogInfo("[Trace] no decisions recorded; the seam may not have been called, or ZenPlayer's extended autopickup is off");
                    return;
                }

                LogInfo($"[Trace] newest first ({_traceCount} recorded, last {TraceCapacity} kept)");
                int shown = Math.Min(_traceCount, 24);
                for (int i = 0; i < shown; i++)
                {
                    int index = (_traceNext - 1 - i + TraceCapacity) % TraceCapacity;
                    TraceEntry entry = _trace[index];
                    LogInfo($"[Trace] f{entry.Frame} {entry.Outcome,-15} {entry.Item ?? "(none)"} {entry.Detail}");
                }
            }
            catch (Exception ex)
            {
                Write("ERROR", $"trace dump failed: {ex}");
            }
        }

        public static void Count(PickupOutcome outcome)
        {
            int index = (int)outcome;
            if (index >= 0 && index < _counts.Length) _counts[index]++;
        }

        public static void Trace(PickupOutcome outcome, string item, int value = 0)
        {
            Count(outcome);

            try
            {
                if (!ShouldRecord(outcome, item)) return;

                string detail;
                switch (outcome)
                {
                    case PickupOutcome.Allowed:
                    case PickupOutcome.NotInBackpack:
                        detail = $"equippedBackpackItems={value}";
                        break;
                    case PickupOutcome.NoCapacity:
                        detail = "capacity=false";
                        break;
                    case PickupOutcome.ModeInactive:
                        detail = $"mode={Plugin.ZenPlayerPickupPatch.ModeName} extended={Plugin.ZenPlayerPickupPatch.ExtendedEnabled}";
                        break;
                    default:
                        detail = null;
                        break;
                }

                _trace[_traceNext] = new TraceEntry
                {
                    Frame = Time.frameCount,
                    Item = item,
                    Outcome = outcome,
                    Detail = detail
                };
                _traceNext = (_traceNext + 1) % TraceCapacity;
                if (_traceCount < TraceCapacity) _traceCount++;

                LogDebug($"{outcome} item={item ?? "(none)"} {detail}");
            }
            catch
            {
                // Tracing is best-effort.
            }
        }

        public static void LogDebug(string message)
        {
            if (!Enabled) return;
            Write("DEBUG", message);
        }

        public static void LogInfo(string message)
        {
            Write("INFO", message);
        }

        public static void LogWarning(string message)
        {
            Write("WARN", message);
        }

        public static void LogError(string message, Exception ex = null)
        {
            Write("ERROR", ex == null ? message : $"{message}: {ex}");
        }

        public static void LogErrorOnce(string message, Exception ex)
        {
            if (_errorLogged) return;
            _errorLogged = true;
            Write("ERROR", $"{message} (further errors suppressed): {ex}");
        }

        private static void Write(string level, string message)
        {
            WriteFile(level, message);

            try
            {
                BepInEx.Logging.ManualLogSource logger = Plugin.LogSource;
                if (logger == null) return;

                string line = $"[ZBB][{level}] {message}";
                switch (level)
                {
                    case "ERROR":
                        logger.LogError(line);
                        break;
                    case "WARN":
                        logger.LogWarning(line);
                        break;
                    default:
                        logger.LogInfo(line);
                        break;
                }
            }
            catch
            {
                // Every logging path is deliberately swallow-all.
            }
        }

        private static void OpenLog()
        {
            try
            {
                if (_writer != null) return;

                string root = Paths.BepInExRootPath;
                if (string.IsNullOrEmpty(root))
                    root = Path.Combine(Directory.GetCurrentDirectory(), "BepInEx");

                string directory = Path.Combine(root, "BSBLogs");
                Directory.CreateDirectory(directory);

                _logPath = Path.Combine(directory, SafeFileName(Plugin.ModName) + ".log");

                const long maxBytes = 5 * 1024 * 1024;
                try
                {
                    if (File.Exists(_logPath) && new FileInfo(_logPath).Length > maxBytes)
                    {
                        string old = _logPath + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(_logPath, old);
                    }
                }
                catch
                {
                    // Rotation is optional; appending to the existing file is fine.
                }

                FileStream stream = new FileStream(_logPath, FileMode.Append, FileAccess.Write,
                    FileShare.ReadWrite);
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                _writer = null;
                Write("WARN", $"could not open diagnostics file: {ex.Message}");
            }
        }

        public static void Shutdown()
        {
            try
            {
                lock (_fileLock)
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                    _writer = null;
                }
            }
            catch
            {
                // Shutdown must never throw.
            }
        }

        private static void WriteFile(string level, string message)
        {
            try
            {
                lock (_fileLock)
                {
                    if (_writer == null) return;
                    _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ZBB][{level}] {message}");
                }
            }
            catch
            {
                try
                {
                    lock (_fileLock)
                    {
                        _writer?.Dispose();
                        _writer = null;
                    }
                }
                catch
                {
                    // Give up on the file sink; console logging still works.
                }
            }
        }

        private static string SafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "ZenPlayerBackpackBridge";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name.Replace(' ', '_');
        }

        private static bool ShouldRecord(PickupOutcome outcome, string item)
        {
            if (string.IsNullOrEmpty(item)) return true;

            float now = Time.realtimeSinceStartup;
            if (_lastTrace.TryGetValue(item, out LastEntry last) &&
                last.Outcome == outcome && now - last.Time < SameItemInterval)
            {
                return false;
            }

            if (_lastTrace.Count > 256) _lastTrace.Clear();
            _lastTrace[item] = new LastEntry { Outcome = outcome, Time = now };
            return true;
        }

        private static void WarnIfUnpatched()
        {
            int owned = 0;
            foreach (MethodBase method in Harmony.GetAllPatchedMethods())
                owned += CountOwned(Harmony.GetPatchInfo(method));

            if (owned == 0)
                LogWarning($"[Patches] none owned by {Plugin.ModGUID}; bridge inactive (seam={Plugin.ZenPlayerPickupPatch.SeamName})");
        }

        private static long CountOf(PickupOutcome outcome)
        {
            int index = (int)outcome;
            return index >= 0 && index < _counts.Length ? _counts[index] : 0;
        }

        private static int CountOwned(Patches info)
        {
            if (info == null) return 0;
            return info.Prefixes.Count(IsMine) +
                   info.Postfixes.Count(IsMine) +
                   info.Transpilers.Count(IsMine) +
                   info.Finalizers.Count(IsMine) +
                   info.ILManipulators.Count(IsMine);
        }

        private static bool IsMine(Patch patch)
        {
            return patch.owner == Plugin.ModGUID;
        }

        private static string Describe(MethodBase method)
        {
            return method == null ? "(null)" : $"{method.DeclaringType?.FullName ?? "?"}.{method.Name}";
        }

        private static string VersionOf(string assemblyName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == assemblyName)
                    return asm.GetName().Version?.ToString(3) ?? "?";
            }

            return "(not loaded)";
        }

        private static int BackpackCount()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return -1;

            List<Inventory> backpacks = ABAPI.GetAllBackpackInventories(player);
            return backpacks?.Count ?? 0;
        }

        private static string EquippedBackpackState()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return "none (no player)";

            Inventory equipped = ABAPI.GetEquippedBackpackInventory(player);
            return equipped == null ? "none" : $"{equipped.GetAllItems().Count} items";
        }

        private static string Summary(Inventory inventory)
        {
            if (inventory == null) return "(null)";

            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            if (items.Count == 0) return "(empty)";

            StringBuilder sb = new StringBuilder();
            int show = Math.Min(items.Count, 8);
            for (int i = 0; i < show; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(items[i].m_shared?.m_name).Append(" x").Append(items[i].m_stack);
            }

            if (items.Count > show) sb.Append(", +").Append(items.Count - show).Append(" more");
            return sb.ToString();
        }
    }
}
