using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static class Program
{
    public static int Main(string[] args)
    {
        var (ok, asmPath, outDir, fsDir, dryRun) = ParseOptions(args);
        if (!ok) return PrintUsageAndExit();
        // Roslyn generator is the only path now.

        IsolatedLoadContext? lc = null;
        try
        {
            lc = PrepareLoadContext(asmPath);
            var asm = LoadAssembly(lc, asmPath);
            var types = SafeGetTypes(asm);
            var (regenAll, regenSet) = ParseRegenerateTargets(Environment.GetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS"));

            var (plan, liveTypes) = GenerateForTypes(types, outDir, fsDir, dryRun, regenAll, regenSet);

            if (!string.IsNullOrEmpty(fsDir))
                PruneOrphans(outDir, fsDir!, liveTypes, dryRun, plan.PlannedDeletes);

            PrintSummary(plan, dryRun);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[shimgen] Error: {ex.Message}");
            return 1;
        }
        finally
        {
            Cleanup(lc);
        }
    }

    private static int PrintUsageAndExit()
    {
        Console.Error.WriteLine("Usage: ShimGen <FSharpAssemblyPath> <OutDir> [FsSourceDir]");
        return 2;
    }

    private static (bool ok, string asmPath, string outDir, string? fsDir, bool dryRun) ParseOptions(string[] args)
    {
        if (args is null) return (false, string.Empty, string.Empty, null, false);
        bool dryRun = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var positional = args.Where(a => !string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (positional.Length < 2)
            return (false, string.Empty, string.Empty, null, dryRun);

        string asmPath = positional[0];
        string outDir = positional[1];
        string? fsDir = positional.Length >= 3 ? positional[2] : null;

        // Normalize paths
        try { asmPath = Path.GetFullPath(asmPath); } catch { }
        try { outDir = Path.GetFullPath(outDir); } catch { }
        if (!string.IsNullOrWhiteSpace(fsDir))
        {
            try { fsDir = Path.GetFullPath(fsDir!); } catch { }
        }

        // Basic validation of assembly path
        if (!File.Exists(asmPath))
        {
            Console.Error.WriteLine($"[shimgen] F# assembly not found: {asmPath}");
            return (false, string.Empty, string.Empty, null, dryRun);
        }

        return (true, asmPath, outDir, fsDir, dryRun);
    }

    private static IsolatedLoadContext PrepareLoadContext(string asmPath)
    {
        var lc = CreateLoadContext(asmPath);
        EnsureDependency(lc, "FSharp.Core");
        EnsureDependency(lc, Annotations.Known.Assembly.Name);
        EnsureDependency(lc, Annotations.Known.Assembly.LegacyName);
        // Roslyn assemblies are required for generation
        EnsureDependency(AssemblyLoadContext.Default, "Microsoft.CodeAnalysis");
        EnsureDependency(AssemblyLoadContext.Default, "Microsoft.CodeAnalysis.CSharp");
        return lc;
    }

    private static void Cleanup(IsolatedLoadContext? lc)
    {
        if (lc is null) return;
        try { lc.Unload(); } catch { }
        try { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); } catch { }
    }

    private static IsolatedLoadContext CreateLoadContext(string mainAsmPath)
    {
        var resolver = new AssemblyDependencyResolver(mainAsmPath);
        var asmDir = Path.GetDirectoryName(mainAsmPath)!;
        return new IsolatedLoadContext(resolver, asmDir, AppContext.BaseDirectory, Directory.GetCurrentDirectory());
    }

    private static void EnsureDependency(AssemblyLoadContext lc, string name)
    {
        try
        {
            if (lc.Assemblies.Any(a => a.GetName().Name == name)) return;
            try { _ = lc.LoadFromAssemblyName(new AssemblyName(name)); return; } catch { }
            var nuget = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (string.IsNullOrWhiteSpace(nuget))
                nuget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
            var packageCandidates = new List<string> { name.ToLowerInvariant() };
            if (string.Equals(name, "Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase))
                packageCandidates.Insert(0, "microsoft.codeanalysis.common");
            else if (string.Equals(name, "Microsoft.CodeAnalysis.CSharp", StringComparison.OrdinalIgnoreCase))
                packageCandidates.Insert(0, "microsoft.codeanalysis.csharp");

            string? dll = null;
            foreach (var pkg in packageCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var pkgDir = Path.Combine(nuget, pkg);
                if (!Directory.Exists(pkgDir)) continue;
                dll = Directory.EnumerateFiles(pkgDir, name + ".dll", SearchOption.AllDirectories)
                               .Where(p => p.IndexOf(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                               .Where(p => p.IndexOf(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                               .Where(p => p.IndexOf(Path.DirectorySeparatorChar + "analyzers" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                               .OrderByDescending(GetTfmScore)
                               .ThenByDescending(p => p)
                               .FirstOrDefault();
                if (!string.IsNullOrEmpty(dll)) break;
            }
            if (dll is null && Directory.Exists(nuget))
            {
                try
                {
                    dll = Directory.EnumerateFiles(nuget, name + ".dll", SearchOption.AllDirectories)
                                    .OrderByDescending(p => p)
                                    .FirstOrDefault();
                }
                catch { }
            }
            if (dll != null)
            {
                try
                {
                    if (lc is IsolatedLoadContext ilc)
                        ilc.LoadFromAssemblyPath(dll);
                    else
                        Assembly.LoadFrom(dll);
                }
                catch { }
            }
        }
        catch { }
    }

    private static int GetTfmScore(string path)
    {
        var p = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).ToLowerInvariant();
        int Score(string key, int s) => p.Contains(Path.DirectorySeparatorChar + key + Path.DirectorySeparatorChar) ? s : 0;
        int score = 0;
        score = Math.Max(score, Score("net8.0", 600));
        score = Math.Max(score, Score("net7.0", 500));
        score = Math.Max(score, Score("net6.0", 400));
        score = Math.Max(score, Score("net5.0", 350));
        score = Math.Max(score, Score("netcoreapp3.1", 300));
        score = Math.Max(score, Score("netstandard2.1", 250));
        score = Math.Max(score, Score("netstandard2.0", 200));
        score = Math.Max(score, Score("net472", 100));
        return score;
    }


    private static IEnumerable<Type?> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException rtle)
        {
            foreach (var le in rtle.LoaderExceptions)
                Console.Error.WriteLine($"[shimgen] Loader exception: {le?.Message}");
            return rtle.Types;
        }
    }

    private readonly record struct GenerationPlan(
        List<string> PlannedWrites,
        List<string> PlannedSkips,
        List<(string from, string to)> PlannedMoves,
        List<string> PlannedDeletes,
        int Scanned,
        int Annotated,
        int Written
    );

    private static (GenerationPlan plan, HashSet<string> liveTypeFullNames) GenerateForTypes(IEnumerable<Type?> types, string outDir, string? fsDir, bool dryRun, bool regenAll, HashSet<string> regenSet)
    {
        int scanned = 0, annotated = 0, written = 0;
        var plannedWrites = new List<string>();
        var plannedMoves = new List<(string from, string to)>();
        var plannedDeletes = new List<string>();
        var plannedSkips = new List<string>();
        var seenTypeFullNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            if (type is null) continue;
            scanned++;
            var spec = TryCreateSpec(type);
            if (spec is null) continue;
            annotated++;
            seenTypeFullNames.Add(spec.Value.ImplType.FullName!);

            var code = RoslynCodeGenerator.Generate(spec.Value, fsDir);
            var (path, oldPath, relForThis) = ComputeDestination(outDir, fsDir, spec.Value, code, dryRun);

            bool shouldRegen = regenAll || regenSet.Contains(spec.Value.ClassName) || regenSet.Contains(spec.Value.ImplType.FullName ?? string.Empty);
            if (shouldRegen && !string.IsNullOrEmpty(oldPath))
                path = oldPath!;

            var wouldWrite = WouldWrite(path, code);
            if (dryRun)
            {
                if (wouldWrite) plannedWrites.Add(path); else plannedSkips.Add(path);
            }
            else if (shouldRegen)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, code, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                written++;
                Console.WriteLine($"[shimgen] Regenerated (in-place) {path}");
            }
            else if (WriteIfChanged(path, code))
            {
                written++;
                Console.WriteLine($"[shimgen] Wrote {path}");
            }

            if (!string.IsNullOrEmpty(relForThis))
                RemoveOtherGeneratedForSource(outDir, relForThis!, path, dryRun, plannedDeletes);

            if (!string.IsNullOrEmpty(oldPath) && !PathsEqual(oldPath!, path) && File.Exists(oldPath!))
            {
                try
                {
                    if (IsGeneratedFile(oldPath!))
                    {
                        if (!shouldRegen)
                        {
                            plannedMoves.Add((oldPath!, path));
                            if (!dryRun) File.Delete(oldPath!);
                        }
                    }
                }
                catch { }
            }
        }

        var plan = new GenerationPlan(plannedWrites, plannedSkips, plannedMoves, plannedDeletes, scanned, annotated, written);
        return (plan, seenTypeFullNames);
    }

    private static (string path, string? oldPath, string? relForThis) ComputeDestination(string outDir, string? fsDir, ScriptSpec spec, string code, bool dryRun)
    {
        var destDir = outDir;
        string? relForThis = null;
        if (!string.IsNullOrEmpty(fsDir))
        {
            var (rel, _) = TryGetSourceInfo(fsDir!, spec.ImplType);
            relForThis = rel;
            if (!string.IsNullOrEmpty(rel))
            {
                var relDir = Path.GetDirectoryName(rel);
                if (!string.IsNullOrEmpty(relDir)) destDir = Path.Combine(outDir, relDir);
            }
        }
        var path = Path.Combine(destDir, spec.ClassName + ".cs");
        string? newHash = ExtractHash(code);
        string? oldPath = null;
        if (!string.IsNullOrEmpty(fsDir))
        {
            oldPath = FindExistingGeneratedPath(outDir, spec.ClassName, spec.ImplType.FullName, newHash);
            if (!string.IsNullOrEmpty(oldPath) && !PathsEqual(oldPath!, path))
            {
                if (!dryRun)
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            }
        }
        return (path, oldPath, relForThis);
    }

    private static void PrintSummary(GenerationPlan plan, bool dryRun)
    {
        Console.WriteLine($"[shimgen] Summary: Moves={plan.PlannedMoves.Count}, Deletes={plan.PlannedDeletes.Count}.");
        if (dryRun)
        {
            Console.WriteLine($"[shimgen] Dry-run: Writes={plan.PlannedWrites.Count}, Skipped={plan.PlannedSkips.Count}.");
            foreach (var m in plan.PlannedMoves) Console.WriteLine($"[shimgen] plan MOVE {m.from} -> {m.to}");
            foreach (var d in plan.PlannedDeletes) Console.WriteLine($"[shimgen] plan DELETE {d}");
            foreach (var w in plan.PlannedWrites) Console.WriteLine($"[shimgen] plan WRITE {w}");
        }
        Console.WriteLine($"[shimgen] Completed. Scanned={plan.Scanned}, Annotated={plan.Annotated}, Written={plan.Written}.");
    }

    private static Assembly LoadAssembly(IsolatedLoadContext lc, string path) => lc.LoadFromAssemblyPath(Path.GetFullPath(path));

    private static ScriptSpec? TryCreateSpec(Type t)
    {
        var attr = t.GetCustomAttributesData()
             .FirstOrDefault(a => a.AttributeType.FullName == Annotations.Known.Types.GodotScriptAttribute);
        if (attr is null) return null;

        string? classNameArg = null;
        string? baseTypeNameArg = null;
        bool tool = false;
        string? icon = null;
        foreach (var na in attr.NamedArguments)
        {
            if (na.MemberName == nameof(Annotations.GodotScriptAttribute.ClassName))
                classNameArg = na.TypedValue.Value as string;
            else if (na.MemberName == nameof(Annotations.GodotScriptAttribute.BaseTypeName))
                baseTypeNameArg = na.TypedValue.Value as string;
            else if (na.MemberName == nameof(Annotations.GodotScriptAttribute.Tool) && na.TypedValue.Value is bool b)
                tool = b;
            else if (na.MemberName == nameof(Annotations.GodotScriptAttribute.Icon))
                icon = na.TypedValue.Value as string;
        }
        // Fallback: construct attribute instance and read properties (helps when compilers omit NamedArguments metadata)
        try
        {
            var attrInstance = t.GetCustomAttributes(false).FirstOrDefault(a => a.GetType().FullName == Annotations.Known.Types.GodotScriptAttribute);
            if (attrInstance is not null)
            {
                string? ReadString(string name)
                    => attrInstance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(attrInstance) as string;
                bool? ReadBool(string name)
                {
                    var pi = attrInstance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    if (pi is null) return null;
                    var v = pi.GetValue(attrInstance);
                    return v is bool bb ? bb : null;
                }
                classNameArg ??= ReadString(nameof(Annotations.GodotScriptAttribute.ClassName));
                baseTypeNameArg ??= ReadString(nameof(Annotations.GodotScriptAttribute.BaseTypeName));
                icon ??= ReadString(nameof(Annotations.GodotScriptAttribute.Icon));
                var tb = ReadBool(nameof(Annotations.GodotScriptAttribute.Tool));
                if (tb.HasValue) tool = tb.Value;
            }
        }
        catch { }
        var className = string.IsNullOrWhiteSpace(classNameArg) ? t.Name : classNameArg!;
        var baseTypeName = string.IsNullOrWhiteSpace(baseTypeNameArg) ? "Godot.Node" : baseTypeNameArg!;

        var exports = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                       .Where(p => p.CanRead && p.CanWrite && IsExportable(p.PropertyType))
                       .ToArray();

        bool HasNoArgs(string name) => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                        .Any(m => m.Name == name && m.GetParameters().Length == 0);
        bool HasOneParam(string name, string paramFullName) => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                        .Any(m => m.Name == name && m.GetParameters().Length == 1 &&
                                                  m.GetParameters()[0].ParameterType.FullName == paramFullName);

        var hasReady = HasNoArgs("Ready");
        var hasEnterTree = HasNoArgs("EnterTree");
        var hasExitTree = HasNoArgs("ExitTree");
        var hasProcess = t.GetMethod("Process", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(double) }) != null;
        var hasPhysicsProcess = t.GetMethod("PhysicsProcess", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(double) }) != null;
        var hasInput = HasOneParam("Input", KnownGodot.InputEvent);
        var hasUnhandledInput = HasOneParam("UnhandledInput", KnownGodot.InputEvent);
        var hasNotification = t.GetMethod("Notification", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(long) }) != null;

        // UI callbacks (Control)
        var hasGuiInput = HasOneParam("GuiInput", KnownGodot.InputEvent);
        var hasShortcutInput = HasOneParam("ShortcutInput", KnownGodot.InputEvent);

        // Drawing (CanvasItem)
        var hasDraw = HasNoArgs("Draw");

        // Drag & Drop (Control)
        bool HasTwoParams(string name, string p1, string p2, Type? returnType = null)
            => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                 .Any(m => m.Name == name && m.GetParameters().Length == 2 &&
                           m.GetParameters()[0].ParameterType.FullName == p1 &&
                           m.GetParameters()[1].ParameterType.FullName == p2 &&
                           (returnType == null || m.ReturnType == returnType));
        bool HasReturnAndOneParam(string name, string p1, Type returnType)
            => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                 .Any(m => m.Name == name && m.GetParameters().Length == 1 &&
                           m.GetParameters()[0].ParameterType.FullName == p1 &&
                           m.ReturnType == returnType);
        bool HasReturnAndNoParam(string name, string returnTypeFullName)
            => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                 .Any(m => m.Name == name && m.GetParameters().Length == 0 &&
                           (m.ReturnType.FullName == returnTypeFullName));

        var hasCanDropData = HasTwoParams("CanDropData", KnownGodot.Vector2, KnownGodot.Variant, typeof(bool));
        var hasDropData = HasTwoParams("DropData", KnownGodot.Vector2, KnownGodot.Variant, typeof(void));
        var hasGetDragData = HasReturnAndOneParam("GetDragData", KnownGodot.Vector2, typeof(object)) || HasReturnAndOneParam("GetDragData", KnownGodot.Vector2, Type.GetType(KnownGodot.Variant) ?? typeof(object));

        // More Control callbacks
        var hasUnhandledKeyInput = HasOneParam("UnhandledKeyInput", KnownGodot.InputEvent);
        var hasHasPoint = HasReturnAndOneParam("HasPoint", KnownGodot.Vector2, typeof(bool));
        var hasGetMinimumSize = HasReturnAndNoParam("GetMinimumSize", KnownGodot.Vector2);
        // _MakeCustomTooltip(string) -> Control
        var hasMakeCustomTooltip = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Any(m => m.Name == "MakeCustomTooltip" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string) && m.ReturnType.FullName == KnownGodot.Control);
        // _GetTooltip(Vector2) -> string
        var hasGetTooltip = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Any(m => m.Name == "GetTooltip" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.FullName == KnownGodot.Vector2 && m.ReturnType == typeof(string));

        var signals = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                       .Where(m => m.Name.StartsWith("Signal_") && m.ReturnType == typeof(void))
                       .Select(m => new { Method = m, Name = m.Name.Substring("Signal_".Length) })
                       .Select(x => new SignalSpec(
                           x.Name,
                           x.Method.GetParameters().Select(p => p.ParameterType).ToArray(),
                           x.Method.GetParameters().Select(p => p.Name ?? "arg").ToArray()
                       ))
                       .ToArray();

        // Discover NodePath members (properties/fields) annotated with NodePathAttribute and preload targets
        var nodePathMembers = new List<NodePathMember>();
        var preloadMembers = new List<PreloadMember>();
        var npAttrFull = Annotations.Known.Types.NodePathAttribute;
        var optNpAttrFull = Annotations.Known.Types.OptionalNodePathAttribute;
        var preloadAttrFull = Annotations.Known.Types.PreloadAttribute;
        var addedNodePathNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attrs = p.GetCustomAttributesData();

            var np = attrs.FirstOrDefault(a => a.AttributeType.FullName == npAttrFull);
            var onp = attrs.FirstOrDefault(a => a.AttributeType.FullName == optNpAttrFull);
            if (np is not null || onp is not null)
            {
                string? path = null; Type? memberType = null; bool isProp = false; string? memberName = null;
                // Pull Path from whichever attribute was applied
                var pathAttr = np ?? onp;
                if (pathAttr is not null)
                {
                    foreach (var na2 in pathAttr.NamedArguments)
                        if (na2.MemberName == "Path") { path = na2.TypedValue.Value as string; break; }
                }

                // Resolve target: property, field, or property via accessor method
                if (p is PropertyInfo pi)
                { memberType = pi.PropertyType; isProp = true; memberName = pi.Name; }
                else if (p is FieldInfo fi)
                { memberType = fi.FieldType; isProp = false; memberName = fi.Name; }
                else if (p is MethodInfo mi)
                {
                    string n = mi.Name;
                    if (n.StartsWith("get_", StringComparison.Ordinal) || n.StartsWith("set_", StringComparison.Ordinal))
                    {
                        var pn = n.Substring(4);
                        var pi2 = t.GetProperty(pn, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (pi2 is not null)
                        { memberType = pi2.PropertyType; isProp = true; memberName = pi2.Name; }
                    }
                }

                if (memberType is not null && !string.IsNullOrEmpty(memberName) && !addedNodePathNames.Contains(memberName!))
                {
                    var (isOpt, optInner) = TryUnwrapFSharpOption(memberType);
                    // Enforce developer intent:
                    if (np is not null && isOpt)
                        throw new InvalidOperationException($"[shimgen] {t.FullName}.{memberName}: NodePathAttribute source type must not be Option<'T>. Use OptionalNodePathAttribute for optional references.");
                    if (onp is not null && !isOpt)
                        throw new InvalidOperationException($"[shimgen] {t.FullName}.{memberName}: OptionalNodePathAttribute source type must be Option<'T>.");

                    nodePathMembers.Add(new NodePathMember(memberName!, isOpt ? optInner! : memberType, isProp, path, isOpt));
                    addedNodePathNames.Add(memberName!);
                }
            }

            var pl = attrs.FirstOrDefault(a => a.AttributeType.FullName == preloadAttrFull);
            if (pl is not null)
            {
                string path = string.Empty; bool preloadRequired = false; Type? preloadMemberType = null; bool preloadIsProperty = false;
                foreach (var na2 in pl.NamedArguments)
                {
                    if (na2.MemberName == "Path") path = na2.TypedValue.Value as string ?? string.Empty;
                    else if (na2.MemberName == nameof(Annotations.PreloadAttribute.Required) && na2.TypedValue.Value is bool rb)
                        preloadRequired = rb;
                }
                if (string.IsNullOrEmpty(path) && pl.ConstructorArguments.Count > 0)
                    path = pl.ConstructorArguments[0].Value as string ?? string.Empty;

                switch (p)
                {
                    case PropertyInfo pi:
                        preloadMemberType = pi.PropertyType; preloadIsProperty = true; break;
                    case FieldInfo fi:
                        preloadMemberType = fi.FieldType; preloadIsProperty = false; break;
                }
                if (preloadMemberType is not null)
                {
                    var (isOpt, optInner) = TryUnwrapFSharpOption(preloadMemberType);
                    var targetType = isOpt ? optInner! : preloadMemberType;
                    if (IsSubclassOfByName(targetType, KnownGodot.Resource))
                        preloadMembers.Add(new PreloadMember(p.Name, targetType, preloadIsProperty, path, preloadRequired, isOpt));
                }
            }
        }

        // Discover [AutoConnect(Path, Signal)] on public methods
        var autoConnects = new List<AutoConnectSpec>();
        var acAttrFull = Annotations.Known.Types.AutoConnectAttribute;
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var ad in m.GetCustomAttributesData())
            {
                if (ad.AttributeType.FullName == acAttrFull)
                {
                    // Prefer named arguments (properties), fallback to constructor positional args
                    string path = ad.NamedArguments.FirstOrDefault(na => na.MemberName == "Path").TypedValue.Value as string ?? string.Empty;
                    string sig = ad.NamedArguments.FirstOrDefault(na => na.MemberName == "Signal").TypedValue.Value as string ?? string.Empty;
                    if (string.IsNullOrEmpty(path) && ad.ConstructorArguments.Count > 0)
                        path = ad.ConstructorArguments[0].Value as string ?? string.Empty;
                    if (string.IsNullOrEmpty(sig) && ad.ConstructorArguments.Count > 1)
                        sig = ad.ConstructorArguments[1].Value as string ?? string.Empty;
                    var paramTypes = m.GetParameters().Select(p => p.ParameterType).ToArray();
                    autoConnects.Add(new AutoConnectSpec(path, sig, m.Name, paramTypes));
                }
            }
        }

        return new ScriptSpec(t, className, baseTypeName, exports, tool, icon,
            hasReady, hasEnterTree, hasExitTree, hasProcess, hasPhysicsProcess,
            hasInput, hasUnhandledInput, hasNotification,
            hasGuiInput, hasShortcutInput, hasDraw, hasCanDropData, hasDropData, hasGetDragData,
            hasUnhandledKeyInput, hasHasPoint, hasGetMinimumSize, hasMakeCustomTooltip, hasGetTooltip,
            signals, nodePathMembers.ToArray(), preloadMembers.ToArray(), autoConnects.ToArray());
    }

    private static bool IsExportable(Type t)
    {
        // Primitives and strings
        if (t == typeof(int) || t == typeof(float) || t == typeof(double) ||
            t == typeof(bool) || t == typeof(string))
            return true;

        // Godot math/engine types commonly exported
        if (t.FullName == KnownGodot.Vector2 || t.FullName == KnownGodot.Vector3 || t.FullName == KnownGodot.Color ||
            t.FullName == KnownGodot.Basis || t.FullName == KnownGodot.Rect2 ||
            t.FullName == KnownGodot.Transform2D || t.FullName == KnownGodot.Transform3D ||
            t.FullName == KnownGodot.NodePath || t.FullName == KnownGodot.StringName || t.FullName == KnownGodot.RID)
            return true;

        // Enums
        if (t.IsEnum) return true;

        // Arrays of exportable types
        if (t.IsArray)
        {
            var et = t.GetElementType();
            return et != null && IsExportable(et);
        }

        // List<T> of exportable T
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
        {
            var ga = t.GetGenericArguments();
            return ga.Length == 1 && IsExportable(ga[0]);
        }

        // Dictionary<string, V> with exportable V
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.Dictionary<,>))
        {
            var ga = t.GetGenericArguments();
            return ga.Length == 2 && ga[0] == typeof(string) && IsExportable(ga[1]);
        }

        // Godot resources (Texture2D, PackedScene, etc.)
        if (IsSubclassOfByName(t, KnownGodot.Resource)) return true;

        return false;
    }

    private static bool IsSubclassOfByName(Type t, string baseFullName)
    {
        try
        {
            var cur = t;
            while (cur != null)
            {
                if (cur.FullName == baseFullName) return true;
                cur = cur.BaseType;
            }
        }
        catch { }
        return false;
    }

    private static (bool isOption, Type? inner) TryUnwrapFSharpOption(Type t)
    {
        try
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition().FullName is string gdef &&
                (gdef == "Microsoft.FSharp.Core.FSharpOption`1" || gdef == "FSharpOption`1"))
            {
                var ga = t.GetGenericArguments();
                if (ga.Length == 1) return (true, ga[0]);
            }
        }
        catch { }
        return (false, null);
    }

    // Removed legacy string-based generator. RoslynCodeGenerator is the single path.

    private static (string? rel, string? hash) TryGetSourceInfo(string dir, Type type)
    {
        var src = FindFsSourceForType(dir, type);
        if (string.IsNullOrEmpty(src)) return (null, null);
        var rel = Path.GetRelativePath(dir, src!).Replace('\\', '/');
        var hash = ComputeFileHash(src!);
        return (rel, hash);
    }
    private static string ExtractHash(string text)
    {
        using var sr = new StringReader(text);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (line.TrimStart().StartsWith("// SourceHash:", StringComparison.Ordinal))
            {
                var idx = line.IndexOf(':');
                if (idx >= 0 && idx + 1 < line.Length)
                    return line[(idx + 1)..].Trim();
            }
            if (line.Contains("</auto-generated>", StringComparison.Ordinal))
                break;
        }
        return string.Empty;
    }
    private static (string? sourceType, string? sourceFile) ExtractHeaderInfo(string text)
    {
        string? srcType = null; string? srcFile = null;
        using var sr = new StringReader(text);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            var t = line.TrimStart();
            if (t.StartsWith("// Source F# type:", StringComparison.Ordinal))
                srcType = t.Split(':', 2)[1].Trim();
            else if (t.StartsWith("// SourceFile:", StringComparison.Ordinal))
                srcFile = t.Split(':', 2)[1].Trim();
            else if (t.Contains("</auto-generated>", StringComparison.Ordinal))
                break;
        }
        return (srcType, srcFile);
    }
    private static string ExtractShimGenVersion(string text)
    {
        using var sr = new StringReader(text);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            var t = line.TrimStart();
            if (t.StartsWith("// ShimGenVersion:", StringComparison.Ordinal))
            {
                var idx = t.IndexOf(':');
                if (idx >= 0 && idx + 1 < t.Length)
                    return t[(idx + 1)..].Trim();
            }
            if (t.Contains("</auto-generated>", StringComparison.Ordinal))
                break;
        }
        return string.Empty;
    }
    private static bool IsOlderVersion(string existing, string current)
    {
        // Compare only the numeric prefix (Major.Minor.Patch), ignore pre-release/build metadata
        static Version ParseCore(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new Version(0, 0, 0, 0);
            var core = s.Split('-', '+')[0].Trim();
            if (Version.TryParse(core, out var v)) return v;
            // Try trimming to three components
            var parts = core.Split('.');
            if (parts.Length >= 3 && Version.TryParse(string.Join('.', parts.Take(3)), out v)) return v;
            if (parts.Length >= 2 && Version.TryParse(string.Join('.', parts.Take(2)) + ".0", out v)) return v;
            return new Version(0, 0, 0, 0);
        }
        try
        {
            var a = ParseCore(existing);
            var b = ParseCore(current);
            return a < b;
        }
        catch { return true; }
    }
    private static bool IsGeneratedFile(string path)
    {
        try
        {
            using var sr = new StreamReader(path);
            for (int i = 0; i < 6; i++)
            {
                var line = sr.ReadLine();
                if (line == null) break;
                if (line.Contains("<auto-generated>", StringComparison.Ordinal)) return true;
            }
        }
        catch { }
        return false;
    }
    private static bool PathsEqual(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    private static string? FindExistingGeneratedPath(string outRoot, string className, string? implFullName, string? hash)
    {
        try
        {
            var candidates = Directory.EnumerateFiles(outRoot, className + ".cs", SearchOption.AllDirectories);
            foreach (var file in candidates)
            {
                string content; try { content = File.ReadAllText(file); } catch { continue; }
                // If hashes match, it's the same source, regardless of path
                var h = ExtractHash(content);
                if (!string.IsNullOrEmpty(hash) && h == hash) return file;
                // Otherwise fall back to source type match
                var (srcType, _) = ExtractHeaderInfo(content);
                if (!string.IsNullOrEmpty(implFullName) && string.Equals(srcType, implFullName, StringComparison.Ordinal))
                    return file;
            }
        }
        catch { }
        return null;
    }

    private static void RemoveOtherGeneratedForSource(string outRoot, string relSourceFile, string keepPath, bool dryRun, List<string> plannedDeletes)
    {
        try
        {
            var files = Directory.EnumerateFiles(outRoot, "*.cs", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                if (PathsEqual(f, keepPath)) continue;
                string content; try { content = File.ReadAllText(f); } catch { continue; }
                var (_, srcFile) = ExtractHeaderInfo(content);
                if (!string.IsNullOrEmpty(srcFile) && PathEqualsRel(srcFile!, relSourceFile))
                {
                    try
                    {
                        if (IsGeneratedFile(f))
                        {
                            plannedDeletes.Add(f);
                            if (!dryRun) File.Delete(f);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private static void PruneOrphans(string outRoot, string fsSourceRoot, HashSet<string> liveTypeFullNames, bool dryRun, List<string> plannedDeletes)
    {
        try
        {
            var files = Directory.EnumerateFiles(outRoot, "*.cs", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                string content; try { content = File.ReadAllText(f); } catch { continue; }
                var (srcType, srcFile) = ExtractHeaderInfo(content);
                bool remove = false;
                if (!string.IsNullOrEmpty(srcFile))
                {
                    var abs = Path.GetFullPath(Path.Combine(fsSourceRoot, srcFile!.Replace('/', Path.DirectorySeparatorChar)));
                    if (!File.Exists(abs)) remove = true;
                }
                // If source file is missing or type is not present in current assembly, remove
                if (!remove && !string.IsNullOrEmpty(srcType) && !liveTypeFullNames.Contains(srcType!))
                    remove = true;
                if (remove)
                {
                    try
                    {
                        if (IsGeneratedFile(f))
                        {
                            plannedDeletes.Add(f);
                            if (!dryRun) File.Delete(f);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private static bool PathEqualsRel(string a, string b)
        => string.Equals(NormalizeRel(a), NormalizeRel(b), StringComparison.OrdinalIgnoreCase);
    private static string NormalizeRel(string p) => p.Replace('\\', '/').TrimStart('.', '/');
    private static string? FindFsSourceForType(string dir, Type type)
    {
        try
        {
            var files = Directory.EnumerateFiles(dir, "*.fs", SearchOption.AllDirectories).ToArray();
            if (files.Length == 0) return null;
            var typeName = type.Name;
            var ns = type.Namespace ?? string.Empty;
            int Score(string content)
            {
                int score = 0;
                if (!string.IsNullOrEmpty(ns) && content.IndexOf("namespace " + ns, StringComparison.Ordinal) >= 0)
                    score += 2;
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"(^|\s)type\s+" + System.Text.RegularExpressions.Regex.Escape(typeName) + @"(\b|\s)", System.Text.RegularExpressions.RegexOptions.Multiline))
                    score += 5;
                if (content.Contains("GodotScript", StringComparison.Ordinal)) score += 1;
                return score;
            }
            string? best = null; int bestScore = int.MinValue;
            foreach (var f in files)
            {
                string content; try { content = File.ReadAllText(f); } catch { continue; }
                var s = Score(content);
                if (s > bestScore) { bestScore = s; best = f; }
            }
            return bestScore >= 3 ? best : null;
        }
        catch { return null; }
    }
    private static string ComputeFileHash(string path)
    {
        try
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var fs = File.OpenRead(path);
            var hash = sha.ComputeHash(fs);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch { return string.Empty; }
    }

    private static bool WriteIfChanged(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var existing = File.Exists(path) ? File.ReadAllText(path) : null;
        string Normalize(string s) => s.Replace("\r\n", "\n");
        content = Normalize(content);
        if (existing is not null)
        {
            var existingNorm = Normalize(existing);
            if (existingNorm == content) return false;
            // Preserve user edits appended after generated content when hash matches
            if (existingNorm.Length > content.Length && existingNorm.StartsWith(content, StringComparison.Ordinal))
            {
                // Keep the trailing appendix (e.g., comments) and avoid rewrite
                return false;
            }
            var oldHash = ExtractHash(existingNorm);
            var newHash = ExtractHash(content);
            if (!string.IsNullOrEmpty(oldHash) && oldHash == newHash)
            {
                // If SourceHash matches but the generator is newer (or version missing), force rewrite
                var oldVer = ExtractShimGenVersion(existingNorm);
                var curVer = ExtractShimGenVersion(content);
                // If the previous header included pre-release/build metadata, normalize it to Major.Minor.Patch
                if (NeedsHeaderNormalization(oldVer)) return true;
                if (!IsOlderVersion(oldVer, curVer)) return false;
            }
        }
        // Always write with LF newlines for determinism
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return true;
    }
    private static bool WouldWrite(string path, string content)
    {
        var existing = File.Exists(path) ? File.ReadAllText(path) : null;
        string Normalize(string s) => s.Replace("\r\n", "\n");
        content = Normalize(content);
        if (existing is null) return true;
        var existingNorm = Normalize(existing);
        if (existingNorm == content) return false;
        var oldHash = ExtractHash(existingNorm);
        var newHash = ExtractHash(content);
        if (!string.IsNullOrEmpty(oldHash) && oldHash == newHash)
        {
            var oldVer = ExtractShimGenVersion(existingNorm);
            var curVer = ExtractShimGenVersion(content);
            if (NeedsHeaderNormalization(oldVer)) return true;
            if (!IsOlderVersion(oldVer, curVer)) return false;
        }
        return true;
    }

    private static bool NeedsHeaderNormalization(string versionHeader)
        => !string.IsNullOrWhiteSpace(versionHeader) && (versionHeader.Contains('+') || versionHeader.Contains('-'));

    private static (bool all, HashSet<string> list) ParseRegenerateTargets(string? env)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(env)) return (false, set);
        var v = env.Trim();
        if (string.Equals(v, "all", StringComparison.OrdinalIgnoreCase) || v == "*")
            return (true, set);
        foreach (var part in v.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            set.Add(part.Trim());
        return (false, set);
    }
}
