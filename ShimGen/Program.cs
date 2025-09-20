using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Headsetsniper.Godot.FSharp.Annotations;

internal sealed class Program
{
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

    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ShimGen <FSharpAssemblyPath> <OutDir>");
            return 2;
        }

        var asmPath = args[0];
        var outDir = args[1];
        var fsSourceDir = args.Length >= 3 ? args[2] : null;

        var mainAsmPath = Path.GetFullPath(asmPath);
        var resolver = new AssemblyDependencyResolver(mainAsmPath);
        var asmDir = Path.GetDirectoryName(mainAsmPath)!;
        var loadContext = new IsolatedLoadContext(resolver, asmDir, AppContext.BaseDirectory, Directory.GetCurrentDirectory());

        TryEnsureDependency(loadContext, "FSharp.Core");
        // Probe both legacy and new package IDs for the annotations assembly
        TryEnsureDependency(loadContext, "Headsetsniper.Godot.FSharp.Annotations");
        TryEnsureDependency(loadContext, "Godot.FSharp.Annotations");

        Assembly asm;
        try
        {
            asm = loadContext.LoadFromAssemblyPath(mainAsmPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load assembly '{asmPath}': {ex.Message}");
            return 3;
        }

        IEnumerable<Type> types;
        try
        {
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException rtle)
        {
            types = rtle.Types.Where(t => t != null)!;
            foreach (var le in rtle.LoaderExceptions)
                Console.Error.WriteLine($"[shimgen] Loader exception: {le?.Message}");
        }

        int scanned = 0, annotated = 0, written = 0;
        foreach (var t in types)
        {
            if (t == null) continue;
            scanned++;
            var cad = t.GetCustomAttributesData()
                       .FirstOrDefault(a => a.AttributeType.FullName == "Headsetsniper.Godot.FSharp.Annotations.GodotScriptAttribute");
            if (cad is null) continue;
            annotated++;

            string? classNameArg = null;
            string? baseTypeNameArg = null;
            foreach (var na in cad.NamedArguments)
            {
                if (na.MemberName == nameof(GodotScriptAttribute.ClassName))
                    classNameArg = na.TypedValue.Value as string;
                else if (na.MemberName == nameof(GodotScriptAttribute.BaseTypeName))
                    baseTypeNameArg = na.TypedValue.Value as string;
            }

            var className = string.IsNullOrWhiteSpace(classNameArg) ? t.Name : classNameArg!;
            var baseTypeName = string.IsNullOrWhiteSpace(baseTypeNameArg) ? "Godot.Node" : baseTypeNameArg!;

            var exports = new List<PropertyInfo>();
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                if (p.CanRead && p.CanWrite && IsExportable(p.PropertyType))
                    exports.Add(p);

            bool HasMethodNoArgs(string name) => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                                  .Any(m => m.Name == name && m.GetParameters().Length == 0);
            bool HasMethodOneParam(string name, string paramFullName) => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                                  .Any(m => m.Name == name && m.GetParameters().Length == 1 &&
                                                            m.GetParameters()[0].ParameterType.FullName == paramFullName);

            var hasReady = HasMethodNoArgs("Ready");
            var hasProcess = t.GetMethod("Process", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(double) }) != null;
            var hasPhysicsProcess = t.GetMethod("PhysicsProcess", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(double) }) != null;
            var hasInput = HasMethodOneParam("Input", "Godot.InputEvent");
            var hasUnhandledInput = HasMethodOneParam("UnhandledInput", "Godot.InputEvent");
            var hasNotification = t.GetMethod("Notification", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(long) }) != null;

            var ns = "Generated";
            var sb = new StringBuilder();

            // Deterministic header to indicate this file is generated and should not be edited.
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// This file was generated by Headsetsniper.Godot.FSharp.ShimGen.");
            sb.AppendLine("// Do NOT edit this file manually. Any changes will be overwritten.");
            sb.AppendLine($"// Source F# type: {t.FullName}");
            string? srcRel = null; string? srcHash = null;
            if (!string.IsNullOrEmpty(fsSourceDir))
            {
                var srcPath = FindFsSourceForType(fsSourceDir!, t);
                if (!string.IsNullOrEmpty(srcPath))
                {
                    srcRel = Path.GetRelativePath(fsSourceDir!, srcPath!).Replace('\\', '/');
                    srcHash = ComputeFileHash(srcPath!);
                    sb.AppendLine($"// SourceFile: {srcRel}");
                    sb.AppendLine($"// SourceHash: {srcHash}");
                }
            }
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("using Godot;");
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine("[GlobalClass]");
            sb.AppendLine($"public partial class {className} : {baseTypeName}");
            sb.AppendLine("{");
            sb.AppendLine($"    private readonly {t.FullName} _impl = new {t.FullName}();");

            foreach (var p in exports)
                sb.AppendLine($"    [Export] public {GetTypeDisplayName(p.PropertyType)} {p.Name} {{ get => _impl.{p.Name}; set => _impl.{p.Name} = value; }}");

            if (hasReady) sb.AppendLine("    public override void _Ready() => _impl.Ready();");
            if (hasProcess) sb.AppendLine("    public override void _Process(double delta) => _impl.Process(delta);");
            if (hasPhysicsProcess) sb.AppendLine("    public override void _PhysicsProcess(double delta) => _impl.PhysicsProcess(delta);");
            if (hasInput) sb.AppendLine("    public override void _Input(Godot.InputEvent @event) => _impl.Input(@event);");
            if (hasUnhandledInput) sb.AppendLine("    public override void _UnhandledInput(Godot.InputEvent @event) => _impl.UnhandledInput(@event);");
            if (hasNotification) sb.AppendLine("    public override void _Notification(long what) => _impl.Notification(what);");

            // Signals by convention: public void Signal_<Name>()
            var signalMethods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                  .Where(m => m.Name.StartsWith("Signal_") && m.GetParameters().Length == 0 && m.ReturnType == typeof(void));
            foreach (var sm in signalMethods)
            {
                var sigName = sm.Name.Substring("Signal_".Length);
                sb.AppendLine($"    [Signal] public event System.Action {sigName};");
                sb.AppendLine($"    public void Emit{sigName}() => {sigName}?.Invoke();");
            }

            sb.AppendLine("}");

            var filePath = Path.Combine(outDir, $"{className}.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var text = sb.ToString();
            // Idempotency policy: Skip when content equal, or when SourceHash matches.
            bool shouldWrite = true;
            if (File.Exists(filePath))
            {
                var existing = File.ReadAllText(filePath);
                if (existing == text)
                {
                    shouldWrite = false;
                }
                else
                {
                    var oldHash = ExtractHash(existing);
                    var newHash = ExtractHash(text);
                    if (!string.IsNullOrEmpty(oldHash) && !string.IsNullOrEmpty(newHash) && oldHash == newHash)
                    {
                        shouldWrite = false;
                    }
                }
            }
            if (shouldWrite)
            {
                File.WriteAllText(filePath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                written++;
                Console.WriteLine($"[shimgen] Wrote {filePath}");
            }
        }
        Console.WriteLine($"[shimgen] Completed. Scanned={scanned}, Annotated={annotated}, Written={written}.");
        return 0;
    }

    private static string GetTypeDisplayName(Type t)
    {
        if (t.IsArray)
        {
            var elem = t.GetElementType()!;
            return GetTypeDisplayName(elem) + "[]";
        }
        // Build fully-qualified name with dots for nested types
        if (t.IsNested)
        {
            var parts = new System.Collections.Generic.List<string>();
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

    private static string ComputeFsSourcesHash(string dir)
    {
        try
        {
            // Gather all .fs files deterministically
            var files = Directory.EnumerateFiles(dir, "*.fs", SearchOption.AllDirectories)
                                 .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                 .ToArray();
            using var sha = System.Security.Cryptography.SHA256.Create();
            foreach (var f in files)
            {
                // Include relative path + content for stability
                var rel = Path.GetRelativePath(dir, f).Replace('\\', '/');
                var relBytes = Encoding.UTF8.GetBytes(rel);
                sha.TransformBlock(relBytes, 0, relBytes.Length, null, 0);
                var bytes = File.ReadAllBytes(f);
                sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    // Best-effort: locate the .fs file that defines the provided type by scanning for namespace and type declaration.
    private static string? FindFsSourceForType(string dir, Type type)
    {
        try
        {
            var files = Directory.EnumerateFiles(dir, "*.fs", SearchOption.AllDirectories).ToArray();
            if (files.Length == 0) return null;
            var typeName = type.Name;
            var ns = type.Namespace ?? string.Empty;
            int Score(string path, string content)
            {
                int score = 0;
                if (!string.IsNullOrEmpty(ns) && content.IndexOf("namespace " + ns, StringComparison.Ordinal) >= 0)
                    score += 2;
                // match 'type FooImpl' with word boundary
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"(^|\s)type\s+" + System.Text.RegularExpressions.Regex.Escape(typeName) + @"(\b|\s)", System.Text.RegularExpressions.RegexOptions.Multiline))
                    score += 5;
                // attribute hint
                if (content.Contains("GodotScript", StringComparison.Ordinal)) score += 1;
                return score;
            }
            string? best = null; int bestScore = int.MinValue;
            foreach (var f in files)
            {
                string content;
                try { content = File.ReadAllText(f); } catch { continue; }
                var s = Score(f, content);
                if (s > bestScore)
                {
                    bestScore = s; best = f;
                }
            }
            // Require at least a minimal signal that it's the right file
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

    private static void TryEnsureDependency(AssemblyLoadContext alc, string name)
    {
        try
        {
            if (alc.Assemblies.Any(a => a.GetName().Name == name)) return;
            try { _ = alc.LoadFromAssemblyName(new AssemblyName(name)); return; } catch { }

            var nuget = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (string.IsNullOrWhiteSpace(nuget))
                nuget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
            var pkgDir = Path.Combine(nuget, name.ToLowerInvariant());
            if (!Directory.Exists(pkgDir)) return;
            string? dll = Directory.EnumerateFiles(pkgDir, name + ".dll", SearchOption.AllDirectories)
                                   .OrderByDescending(p => p)
                                   .FirstOrDefault();
            if (dll != null) alc.LoadFromAssemblyPath(dll);
        }
        catch { }
    }
}

internal sealed class IsolatedLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string[] _fallbackDirs;
    public IsolatedLoadContext(AssemblyDependencyResolver resolver, params string[] fallbackDirs) : base(isCollectible: true)
    {
        _resolver = resolver;
        _fallbackDirs = fallbackDirs ?? Array.Empty<string>();
    }
    protected override Assembly Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path != null)
            return LoadFromAssemblyPath(path);
        var fileName = assemblyName.Name + ".dll";
        foreach (var dir in _fallbackDirs)
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
                return LoadFromAssemblyPath(candidate);
        }
        return null!;
    }
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (path != null)
            return LoadUnmanagedDllFromPath(path);
        return IntPtr.Zero;
    }
}
