// Audits every string-addressed binding the mod uses at runtime, plus the version contract.
// Valheim/Harmony members reached from C# are proven by the compiler; this proves the rest.
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class Program
{
    private static int _fail;
    private static string _root;

    private static int Main()
    {
        _root = Environment.GetEnvironmentVariable("MOD_ROOT") ?? Directory.GetCurrentDirectory();
        string managed = Path.Combine(_root, "refs/game/valheim_Data/Managed");
        string core = Path.Combine(_root, "refs/game/BepInEx/core");
        string mods = Path.Combine(_root, "refs/mods");

        var paths = new[] { managed, core, mods }
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*.dll"))
            .GroupBy(Path.GetFileName)
            .Select(g => g.First())
            .ToList();

        using var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths));

        Console.WriteLine("### dependency versions");
        var ab = Load(mlc, Path.Combine(mods, "AdventureBackpacks.dll"));
        var zp = Load(mlc, Path.Combine(mods, "ZenPlayer.dll"));
        Version(ab, "AdventureBackpacks");
        Version(zp, "ZenPlayer");

        Console.WriteLine("\n### BepInPlugin GUIDs the hard dependencies name");
        PluginAttribute(zp, "ZenDragon.ZenPlayer");
        PluginAttribute(ab, "vapok.mods.adventurebackpacks");

        Console.WriteLine("\n### AdventureBackpacks.API.ABAPI surface");
        Method(ab, "AdventureBackpacks.API.ABAPI", "IsLoaded", Array.Empty<string>(), "Boolean", true);
        Method(ab, "AdventureBackpacks.API.ABAPI", "GetEquippedBackpackInventory", new[] { "Player" }, "Inventory", true);
        Method(ab, "AdventureBackpacks.API.ABAPI", "GetAllBackpackInventories", new[] { "Player" }, "List`1", true);

        Console.WriteLine("\n### ZenPlayer auto-pickup seam");
        Method(zp, "ZenPlayer.Sections.AutoPickupRules", "CanAddItem_Intercept",
               new[] { "Inventory", "ItemData", "Int32" }, "Boolean", true);
        Field(zp, "ZenPlayer.Sections.AutoPickupRules", "_pickupMode", "PickupMode", true);
        Field(zp, "ZenPlayer.Sections.AutoPickupRules+Configs", "AutoPickupExtended", "ConfigEntry`1", true);
        EnumValue(zp, "ZenPlayer.Sections.AutoPickupRules+PickupMode", "HaveInInventory", 1);

        Console.WriteLine("\n### Valheim members addressed by name");
        var vh = Load(mlc, Path.Combine(managed, "assembly_valheim.dll"));
        Method(vh, "Inventory", "HaveItem", new[] { "String", "Boolean" }, "Boolean", false);

        Console.WriteLine("\n### version contract (must match in all four places)");
        string asmVersion = CheckBuiltAssembly(mlc);
        string constVersion = CheckConst(mlc);
        string csprojVersion = CheckCsproj();
        string manifestVersion = CheckManifest();
        string changelogVersion = CheckChangelog();

        var all = new[] { asmVersion, constVersion, csprojVersion, manifestVersion, changelogVersion };
        if (all.Distinct().Count() == 1 && all[0] != null)
            Report(true, $"all version sources agree on {all[0]}", null);
        else
            Report(false, "version drift", string.Join(" | ", new[]
            {
                $"assembly={asmVersion}", $"Plugin.ModVersion={constVersion}", $"csproj={csprojVersion}",
                $"manifest={manifestVersion}", $"CHANGELOG={changelogVersion}"
            }));

        Console.WriteLine("\n### display name");
        CheckDisplayName(mlc);

        Console.WriteLine(_fail == 0 ? "\nALL AUDITED BINDINGS OK" : $"\n{_fail} BINDING(S) FAILED");
        return _fail == 0 ? 0 : 1;
    }

    private static Assembly Load(MetadataLoadContext mlc, string path)
    {
        if (!File.Exists(path))
        {
            Report(false, Path.GetFileName(path), "missing assembly");
            return null;
        }

        try
        {
            return mlc.LoadFromAssemblyPath(path);
        }
        catch (Exception ex)
        {
            Report(false, Path.GetFileName(path), $"could not load: {ex.Message}");
            return null;
        }
    }

    private static void Version(Assembly asm, string label)
    {
        if (asm == null) return;
        var v = asm.GetName().Version;
        Console.WriteLine($"  {label,-20} {v}  (file {Path.GetFileName(asm.Location)})");
    }

    private static void PluginAttribute(Assembly asm, string expectedGuid)
    {
        if (asm == null) return;

        Type[] types;
        try
        {
            types = asm.GetTypes();
        }
        catch (Exception ex)
        {
            Report(false, $"BepInPlugin {expectedGuid}", $"could not enumerate types: {ex.Message}");
            return;
        }

        Type owner = null;
        string version = "?";
        foreach (Type t in types)
        {
            CustomAttributeData[] attrs;
            try
            {
                attrs = t.GetCustomAttributesData().ToArray();
            }
            catch
            {
                // A type whose attributes live in an assembly we do not reference (e.g. Jotunn)
                // cannot be inspected; it cannot be the BepInPlugin carrier either.
                continue;
            }

            foreach (CustomAttributeData a in attrs)
            {
                string attributeName;
                try
                {
                    attributeName = a.AttributeType.Name;
                }
                catch
                {
                    continue;
                }

                if (attributeName != "BepInPlugin" || a.ConstructorArguments.Count == 0) continue;
                if (a.ConstructorArguments[0].Value as string != expectedGuid) continue;
                owner = t;
                if (a.ConstructorArguments.Count > 2) version = a.ConstructorArguments[2].Value as string;
            }
        }

        if (owner == null)
            Report(false, $"BepInPlugin {expectedGuid}", "GUID not present in assembly");
        else
            Report(true, $"BepInPlugin {expectedGuid} {version}  [{owner.FullName}]", null);
    }

    private static void Method(Assembly asm, string typeName, string method, string[] paramTypeNames,
                              string returnType, bool expectStatic)
    {
        if (asm == null) return;

        Type t = ResolveType(asm, typeName);
        if (t == null)
        {
            Report(false, $"{typeName}.{method}", "type not found");
            return;
        }

        MethodInfo m = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                    BindingFlags.NonPublic)
                        .FirstOrDefault(x => x.Name == method &&
                                             x.GetParameters().Length == paramTypeNames.Length &&
                                             x.ReturnType.Name == returnType &&
                                             x.IsStatic == expectStatic &&
                                             x.GetParameters().Select(p => p.ParameterType.Name)
                                              .SequenceEqual(paramTypeNames));

        if (m == null)
        {
            var near = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                    BindingFlags.NonPublic)
                        .Where(x => x.Name == method)
                        .Select(x => $"{(x.IsStatic ? "static " : "")}({string.Join(",", x.GetParameters().Select(p => p.ParameterType.Name))}):{x.ReturnType.Name}");
            Report(false, $"{typeName}.{method}({string.Join(",", paramTypeNames)}):{returnType}",
                   "no matching overload; found: " + string.Join(" | ", near));
            return;
        }

        Report(true, $"{typeName}.{method}({string.Join(",", paramTypeNames)}):{returnType}" +
                     (expectStatic ? " [static]" : ""), null);
    }

    private static string CheckBuiltAssembly(MetadataLoadContext mlc)
    {
        string dll = Path.Combine(_root, "bin/Release/net48/ZenPlayerBackpackBridge.dll");
        if (!File.Exists(dll))
        {
            Report(false, "built assembly", "not built yet - build before auditing");
            return null;
        }

        try
        {
            var asm = mlc.LoadFromAssemblyPath(dll);
            string v = asm.GetName().Version?.ToString(3);
            Report(true, $"built assembly {asm.GetName().Name} {v}", null);
            return v;
        }
        catch (Exception ex)
        {
            Report(false, "built assembly", ex.Message);
            return null;
        }
    }

    private static string CheckConst(MetadataLoadContext mlc)
    {
        string dll = Path.Combine(_root, "bin/Release/net48/ZenPlayerBackpackBridge.dll");
        if (!File.Exists(dll)) return null;

        try
        {
            var asm = mlc.LoadFromAssemblyPath(dll);
            FieldInfo f = asm.GetType("ZenPlayerBackpackBridge.Plugin")
                            ?.GetField("ModVersion", BindingFlags.Public | BindingFlags.Static);
            string v = f?.GetRawConstantValue() as string;
            if (v == null)
            {
                Report(false, "Plugin.ModVersion", "const not found");
                return null;
            }
            Report(true, $"Plugin.ModVersion = {v}", null);
            return v;
        }
        catch (Exception ex)
        {
            Report(false, "Plugin.ModVersion", ex.Message);
            return null;
        }
    }

    private static string CheckCsproj()
    {
        try
        {
            XDocument doc = XDocument.Load(Path.Combine(_root, "ZenPlayerBackpackBridge.csproj"));
            string v = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value?.Trim();
            if (string.IsNullOrEmpty(v))
            {
                Report(false, "csproj Version", "element not found");
                return null;
            }
            Report(true, $"csproj Version = {v}", null);
            return v;
        }
        catch (Exception ex)
        {
            Report(false, "csproj Version", ex.Message);
            return null;
        }
    }

    private static string CheckManifest()
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(_root, "resources/manifest.json")));
            string v = doc.RootElement.GetProperty("version_number").GetString();
            Report(true, $"manifest version_number = {v}", null);
            return v;
        }
        catch (Exception ex)
        {
            Report(false, "manifest version_number", ex.Message);
            return null;
        }
    }

    private static string CheckChangelog()
    {
        try
        {
            string text = File.ReadAllText(Path.Combine(_root, "resources/CHANGELOG.md"));
            Match m = Regex.Match(text, @"^##\s*\[([0-9]+(?:\.[0-9]+)*)\]", RegexOptions.Multiline);
            if (!m.Success)
            {
                Report(false, "CHANGELOG top heading", "no '## [x.y.z]' heading found");
                return null;
            }
            Report(true, $"CHANGELOG top heading = {m.Groups[1].Value}", null);
            return m.Groups[1].Value;
        }
        catch (Exception ex)
        {
            Report(false, "CHANGELOG top heading", ex.Message);
            return null;
        }
    }

    private static void Field(Assembly asm, string typeName, string field, string fieldType, bool expectStatic)
    {
        if (asm == null) return;

        Type t = ResolveType(asm, typeName);
        if (t == null)
        {
            Report(false, $"{typeName}.{field}", "type not found");
            return;
        }

        FieldInfo f = t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                  BindingFlags.NonPublic)
                       .FirstOrDefault(x => x.Name == field && x.FieldType.Name == fieldType &&
                                            x.IsStatic == expectStatic);

        if (f == null)
        {
            string actual = string.Join(" | ", t.GetFields(BindingFlags.Instance | BindingFlags.Static |
                                                           BindingFlags.Public | BindingFlags.NonPublic)
                                            .Where(x => x.Name == field)
                                            .Select(x => $"{x.FieldType.Name} {(x.IsStatic ? "static" : "instance")}"));
            Report(false, $"{typeName}.{field}:{fieldType}",
                   string.IsNullOrEmpty(actual) ? "field not found" : "found: " + actual);
            return;
        }

        Report(true, $"{typeName}.{field}:{fieldType} [{(expectStatic ? "static" : "instance")}]", null);
    }

    private static void EnumValue(Assembly asm, string typeName, string member, int expected)
    {
        if (asm == null) return;

        Type t = ResolveType(asm, typeName);
        FieldInfo f = t?.GetField(member, BindingFlags.Public | BindingFlags.Static);
        if (f == null || !f.IsLiteral)
        {
            Report(false, $"{typeName}.{member} == {expected}", "enum member not found");
            return;
        }

        int actual = Convert.ToInt32(f.GetRawConstantValue());
        Report(actual == expected, $"{typeName}.{member} == {expected}", $"actual {actual}");
    }

    private static Type ResolveType(Assembly asm, string typeName)
    {
        Type t = asm.GetType(typeName);
        if (t != null) return t;

        int plus = typeName.LastIndexOf('+');
        if (plus <= 0) return null;

        Type outer = asm.GetType(typeName.Substring(0, plus));
        return outer?.GetNestedType(typeName.Substring(plus + 1),
                                    BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static void CheckDisplayName(MetadataLoadContext mlc)
    {
        string dll = Path.Combine(_root, "bin/Release/net48/ZenPlayerBackpackBridge.dll");
        if (!File.Exists(dll)) return;

        string name;
        try
        {
            var asm = mlc.LoadFromAssemblyPath(dll);
            FieldInfo f = asm.GetType("ZenPlayerBackpackBridge.Plugin")
                            ?.GetField("ModName", BindingFlags.Public | BindingFlags.Static);
            name = f?.GetRawConstantValue() as string;
        }
        catch (Exception ex)
        {
            Report(false, "Plugin.ModName", ex.Message);
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            Report(false, "Plugin.ModName", "const not found");
            return;
        }

        string readme = Path.Combine(_root, "resources/README.md");
        string heading = File.Exists(readme)
            ? File.ReadLines(readme).FirstOrDefault(l => l.StartsWith("# "))?.Substring(2).Trim()
            : null;

        Report(heading == name, $"display name \"{name}\"", $"README heading is \"{heading}\"");
    }

    private static void Report(bool ok, string what, string detail)
    {
        if (!ok) _fail++;
        Console.WriteLine(ok ? $"  OK    {what}" : $"  FAIL  {what}  ->  {detail}");
    }
}
