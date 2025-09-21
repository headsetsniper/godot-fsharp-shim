using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static class Program
{
    public static int Main(string[] args)
    {
        var (ok, asmPath, outDir, fsDir, dryRun) = ParseOptions(args);
        if (!ok)
        {
            Console.Error.WriteLine("Usage: ShimGen <FSharpAssemblyPath> <OutDir> [FsSourceDir]");
            return 2;
        }
        IsolatedLoadContext? lc = null;
        try
        {
            lc = CreateLoadContext(asmPath);
            EnsureDependency(lc, "FSharp.Core");
            EnsureDependency(lc, "Headsetsniper.Godot.FSharp.Annotations");
            EnsureDependency(lc, "Godot.FSharp.Annotations"); // legacy id support

            Assembly? asm = LoadAssembly(lc, asmPath);
            IEnumerable<Type?>? types = SafeGetTypes(asm);

            int scanned = 0, annotated = 0, written = 0;
            var plannedWrites = new List<string>();
            var plannedMoves = new List<(string from, string to)>();
            var plannedDeletes = new List<string>();
            var plannedSkips = new List<string>();
            var seenSourceRel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTypeFullNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in types)
            {
                if (type is null) continue;
                scanned++;
                var spec = TryCreateSpec(type);
                if (spec is null) continue;
                annotated++;
                seenTypeFullNames.Add(spec.Value.ImplType.FullName!);

                var code = GenerateCode(spec.Value, fsDir);
                // Place output under subfolders that mirror the F# source's relative path (when provided)
                var destDir = outDir;
                string? relForThis = null;
                if (!string.IsNullOrEmpty(fsDir))
                {
                    var (rel, _) = TryGetSourceInfo(fsDir!, spec.Value.ImplType);
                    relForThis = rel;
                    if (!string.IsNullOrEmpty(rel))
                    {
                        var relDir = Path.GetDirectoryName(rel);
                        if (!string.IsNullOrEmpty(relDir)) destDir = Path.Combine(outDir, relDir);
                    }
                }
                var path = Path.Combine(destDir, spec.Value.ClassName + ".cs");
                // If a previously generated file for the same script exists at a different location, relocate (delete old)
                string? newHash = ExtractHash(code);
                string? oldPath = null;
                if (!string.IsNullOrEmpty(fsDir))
                {
                    oldPath = FindExistingGeneratedPath(outDir, spec.Value.ClassName, spec.Value.ImplType.FullName, newHash);
                    if (!string.IsNullOrEmpty(oldPath) && !PathsEqual(oldPath!, path))
                    {
                        // Ensure new directory exists before removing old
                        if (!dryRun)
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    }
                }
                var wouldWrite = WouldWrite(path, code);
                if (dryRun)
                {
                    if (wouldWrite) plannedWrites.Add(path); else plannedSkips.Add(path);
                }
                else if (WriteIfChanged(path, code))
                {
                    written++;
                    Console.WriteLine($"[shimgen] Wrote {path}");
                }
                // Remove other generated files that reference the same source file (handles class rename duplicates)
                if (!string.IsNullOrEmpty(relForThis))
                {
                    seenSourceRel.Add(relForThis!);
                    RemoveOtherGeneratedForSource(outDir, relForThis!, path, dryRun, plannedDeletes);
                }
                if (!string.IsNullOrEmpty(oldPath) && !PathsEqual(oldPath!, path) && File.Exists(oldPath!))
                {
                    try
                    {
                        if (IsGeneratedFile(oldPath!))
                        {
                            plannedMoves.Add((oldPath!, path));
                            if (!dryRun) File.Delete(oldPath!);
                        }
                    }
                    catch { }
                }
            }
            // Prune orphans: generated files whose SourceFile no longer exists or whose type is no longer present
            if (!string.IsNullOrEmpty(fsDir))
            {
                PruneOrphans(outDir, fsDir!, seenTypeFullNames, dryRun, plannedDeletes);
            }
            // Concise summary for CI
            Console.WriteLine($"[shimgen] Summary: Moves={plannedMoves.Count}, Deletes={plannedDeletes.Count}.");
            if (dryRun)
            {
                Console.WriteLine($"[shimgen] Dry-run: Writes={plannedWrites.Count}, Skipped={plannedSkips.Count}.");
                foreach (var m in plannedMoves) Console.WriteLine($"[shimgen] plan MOVE {m.from} -> {m.to}");
                foreach (var d in plannedDeletes) Console.WriteLine($"[shimgen] plan DELETE {d}");
                foreach (var w in plannedWrites) Console.WriteLine($"[shimgen] plan WRITE {w}");
            }
            Console.WriteLine($"[shimgen] Completed. Scanned={scanned}, Annotated={annotated}, Written={written}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[shimgen] Error: {ex.Message}");
            return 1;
        }
        finally
        {
            // Drop references to allow collectible ALC to unload
            if (lc is not null)
            {
                try { lc.Unload(); } catch { /* ignore */ }
                // Encourage prompt release of native handles loaded via the ALC
                try { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); } catch { /* ignore */ }
            }
        }
    }

    private static (bool ok, string asmPath, string outDir, string? fsDir, bool dryRun) ParseOptions(string[] args)
    {
        if (args.Length < 2) return (false, "", "", null, false);
        string? asm = null; string? outDir = null; string? fsDir = null; bool dry = false;
        foreach (var a in args)
        {
            // Accept only '-' prefixes for flags. Treat '/' as path root (Unix) rather than a flag.
            if (a.StartsWith("-"))
            {
                var flag = a.TrimStart('-', '/').ToLowerInvariant();
                if (flag is "dry-run" or "n" or "noop") dry = true;
            }
            else if (asm is null) asm = Path.GetFullPath(a);
            else if (outDir is null) outDir = a;
            else if (fsDir is null) fsDir = a;
        }
        if (asm is null || outDir is null) return (false, "", "", null, false);
        Directory.CreateDirectory(outDir);
        return (true, asm, outDir, fsDir, dry);
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
            var pkgDir = Path.Combine(nuget, name.ToLowerInvariant());
            if (!Directory.Exists(pkgDir)) return;
            var dll = Directory.EnumerateFiles(pkgDir, name + ".dll", SearchOption.AllDirectories)
                               .OrderByDescending(p => p).FirstOrDefault();
            if (dll != null) ((IsolatedLoadContext)lc).LoadFromAssemblyPath(dll);
        }
        catch { }
    }
    private static Assembly LoadAssembly(IsolatedLoadContext lc, string path) => lc.LoadFromAssemblyPath(Path.GetFullPath(path));

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

    private static ScriptSpec? TryCreateSpec(Type t)
    {
        var attr = t.GetCustomAttributesData()
                     .FirstOrDefault(a => a.AttributeType.FullName == "Headsetsniper.Godot.FSharp.Annotations.GodotScriptAttribute");
        if (attr is null) return null;

        string? classNameArg = null;
        string? baseTypeNameArg = null;
        foreach (var na in attr.NamedArguments)
        {
            if (na.MemberName == nameof(Annotations.GodotScriptAttribute.ClassName))
                classNameArg = na.TypedValue.Value as string;
            else if (na.MemberName == nameof(Annotations.GodotScriptAttribute.BaseTypeName))
                baseTypeNameArg = na.TypedValue.Value as string;
        }
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
        var hasProcess = t.GetMethod("Process", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(double) }) != null;
        var hasPhysicsProcess = t.GetMethod("PhysicsProcess", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(double) }) != null;
        var hasInput = HasOneParam("Input", "Godot.InputEvent");
        var hasUnhandledInput = HasOneParam("UnhandledInput", "Godot.InputEvent");
        var hasNotification = t.GetMethod("Notification", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(long) }) != null;

        var signalMethods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                             .Where(m => m.Name.StartsWith("Signal_") && m.GetParameters().Length == 0 && m.ReturnType == typeof(void))
                             .Select(m => m.Name.Substring("Signal_".Length))
                             .ToArray();

        return new ScriptSpec(t, className, baseTypeName, exports, hasReady, hasProcess, hasPhysicsProcess, hasInput, hasUnhandledInput, hasNotification, signalMethods);
    }

    private static bool IsExportable(Type t)
    {
        if (t == typeof(int) || t == typeof(float) || t == typeof(double) ||
            t == typeof(bool) || t == typeof(string))
            return true;
        if (t.FullName == "Godot.Vector2" || t.FullName == "Godot.Vector3" || t.FullName == "Godot.Color")
            return true;
        if (t.IsEnum) return true;
        if (t.IsArray)
        {
            var et = t.GetElementType();
            return et != null && IsExportable(et);
        }
        return false;
    }

    private static string GenerateCode(ScriptSpec spec, string? fsSourceDir)
    {
        var ns = "Generated";
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// This file was generated by Headsetsniper.Godot.FSharp.ShimGen.");
        sb.AppendLine("// Do NOT edit this file manually. Any changes will be overwritten.");
        sb.AppendLine($"// ShimGenVersion: {GetGeneratorVersion()}");
        sb.AppendLine($"// Source F# type: {spec.ImplType.FullName}");
        if (!string.IsNullOrEmpty(fsSourceDir))
        {
            var (rel, hash) = TryGetSourceInfo(fsSourceDir!, spec.ImplType);
            if (!string.IsNullOrEmpty(rel))
            {
                sb.AppendLine($"// SourceFile: {rel}");
                if (!string.IsNullOrEmpty(hash)) sb.AppendLine($"// SourceHash: {hash}");
            }
        }
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
        sb.AppendLine("using Godot;");
        sb.AppendLine("using Headsetsniper.Godot.FSharp.Annotations;");
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine("[GlobalClass]");
        sb.AppendLine($"public partial class {spec.ClassName} : {spec.BaseTypeName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {GetTypeDisplayName(spec.ImplType)} _impl = new {GetTypeDisplayName(spec.ImplType)}();");

        foreach (var p in spec.Exports)
            sb.AppendLine($"    [Export] public {GetTypeDisplayName(p.PropertyType)} {p.Name} {{ get => _impl.{p.Name}; set => _impl.{p.Name} = value; }}");

        if (spec.HasReady)
        {
            sb.AppendLine("    public override void _Ready()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_impl is IGdScript<" + spec.BaseTypeName + "> gd)");
            sb.AppendLine("            gd.Node = this;");
            sb.AppendLine("        _impl.Ready();");
            sb.AppendLine("    }");
        }
        if (spec.HasProcess) sb.AppendLine("    public override void _Process(double delta) => _impl.Process(delta);");
        if (spec.HasPhysicsProcess) sb.AppendLine("    public override void _PhysicsProcess(double delta) => _impl.PhysicsProcess(delta);");
        if (spec.HasInput) sb.AppendLine("    public override void _Input(Godot.InputEvent @event) => _impl.Input(@event);");
        if (spec.HasUnhandledInput) sb.AppendLine("    public override void _UnhandledInput(Godot.InputEvent @event) => _impl.UnhandledInput(@event);");
        if (spec.HasNotification) sb.AppendLine("    public override void _Notification(long what) => _impl.Notification(what);");

        foreach (var sig in spec.SignalNames)
        {
            sb.AppendLine($"    [Signal] public event System.Action {sig};");
            sb.AppendLine($"    public void Emit{sig}() => {sig}?.Invoke();");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetGeneratorVersion()
    {
        try
        {
            var asm = typeof(Program).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info)) return info!;
            var ver = asm.GetName().Version?.ToString();
            return string.IsNullOrWhiteSpace(ver) ? "0.0.0" : ver!;
        }
        catch { return "0.0.0"; }
    }

    private static string GetTypeDisplayName(Type t)
    {
        if (t.IsArray)
        {
            var elem = t.GetElementType()!;
            return GetTypeDisplayName(elem) + "[]";
        }
        if (t.IsNested)
        {
            var parts = new List<string>();
            var cur = t;
            while (cur != null)
            {
                parts.Add(cur.Name);
                cur = cur.DeclaringType;
            }
            parts.Reverse();
            var ns = t.Namespace;
            var prefix = string.IsNullOrEmpty(ns) ? string.Empty : ns + ".";
            return prefix + string.Join(".", parts);
        }
        return t.FullName!;
    }

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
        if (existing is not null)
        {
            if (existing == content) return false;
            var oldHash = ExtractHash(existing);
            var newHash = ExtractHash(content);
            if (!string.IsNullOrEmpty(oldHash) && oldHash == newHash)
            {
                // If SourceHash matches but the generator is newer (or version missing), force rewrite
                var oldVer = ExtractShimGenVersion(existing);
                var curVer = GetGeneratorVersion();
                if (!IsOlderVersion(oldVer, curVer)) return false;
            }
        }
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return true;
    }
    private static bool WouldWrite(string path, string content)
    {
        var existing = File.Exists(path) ? File.ReadAllText(path) : null;
        if (existing is null) return true;
        if (existing == content) return false;
        var oldHash = ExtractHash(existing);
        var newHash = ExtractHash(content);
        if (!string.IsNullOrEmpty(oldHash) && oldHash == newHash)
        {
            var oldVer = ExtractShimGenVersion(existing);
            var curVer = GetGeneratorVersion();
            if (!IsOlderVersion(oldVer, curVer)) return false;
        }
        return true;
    }
}
