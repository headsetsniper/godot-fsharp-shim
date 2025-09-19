using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Godot.FSharp.Annotations;

internal sealed class Program
{
    private static bool IsExportablePrimitive(Type t) =>
        t == typeof(int) || t == typeof(float) || t == typeof(double) ||
        t == typeof(bool) || t == typeof(string);

    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ShimGen <FSharpAssemblyPath> <OutDir>");
            return 2;
        }

        var asmPath = args[0];
        var outDir = args[1];

        var mainAsmPath = Path.GetFullPath(asmPath);
        var resolver = new AssemblyDependencyResolver(mainAsmPath);
        var loadContext = new IsolatedLoadContext(resolver);

        // Make sure these are resolvable in the isolated context
        TryEnsureDependency(loadContext, "FSharp.Core");
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
                       .FirstOrDefault(a => a.AttributeType.FullName == "Godot.FSharp.Annotations.GodotScriptAttribute");
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
                if (p.CanRead && p.CanWrite && IsExportablePrimitive(p.PropertyType))
                    exports.Add(p);

            var hasReady = t.GetMethod("Ready", BindingFlags.Instance | BindingFlags.Public, Array.Empty<Type>()) != null;
            var hasProcess = t.GetMethod("Process", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(double) }) != null;

            var ns = "Generated";
            var sb = new StringBuilder();

            sb.AppendLine("using Godot;");
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine("[GlobalClass]");
            sb.AppendLine($"public partial class {className} : {baseTypeName}");
            sb.AppendLine("{");
            sb.AppendLine($"    private readonly {t.FullName} _impl = new {t.FullName}();");

            foreach (var p in exports)
                sb.AppendLine($"    [Export] public {p.PropertyType.FullName} {p.Name} {{ get => _impl.{p.Name}; set => _impl.{p.Name} = value; }}");

            if (hasReady) sb.AppendLine("    public override void _Ready() => _impl.Ready();");
            if (hasProcess) sb.AppendLine("    public override void _Process(double delta) => _impl.Process(delta);");

            sb.AppendLine("}");

            var filePath = Path.Combine(outDir, $"{className}.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var text = sb.ToString();

            if (!File.Exists(filePath) || File.ReadAllText(filePath) != text)
            {
                File.WriteAllText(filePath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                written++;
                Console.WriteLine($"[shimgen] Wrote {filePath}");
            }
        }
        Console.WriteLine($"[shimgen] Completed. Scanned={scanned}, Annotated={annotated}, Written={written}.");
        return 0;
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
    public IsolatedLoadContext(AssemblyDependencyResolver resolver) : base(isCollectible: true)
    {
        _resolver = resolver;
    }
    protected override Assembly Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path != null)
            return LoadFromAssemblyPath(path);
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
