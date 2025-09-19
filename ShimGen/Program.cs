using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Godot.FSharp.Annotations;

static Type? ResolveGodotType(string baseTypeName)
{
    // Load Godot assemblies already referenced by the C# project at compile-time
    // We rely on Type.GetType with assembly-qualified names or simple names.
    var t = Type.GetType(baseTypeName);
    if (t != null) return t;

    // Fallbacks for common Godot base types
    var candidates = new[]
    {
        "Godot.Node2D, GodotSharp",
        "Godot.Node3D, GodotSharp",
        "Godot.Node, GodotSharp",
        "Godot.Control, GodotSharp"
    };
    foreach (var c in candidates)
    {
        var tt = Type.GetType(c);
        if (tt != null && tt.FullName == baseTypeName) return tt;
    }
    // Last resort: simple name match in already loaded assemblies
    return AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(baseTypeName))
        .FirstOrDefault(x => x != null);
}

static bool IsExportablePrimitive(Type t) =>
    t == typeof(int) || t == typeof(float) || t == typeof(double) ||
    t == typeof(bool) || t == typeof(string);

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: ShimGen <FSharpAssemblyPath> <OutDir>");
    Environment.Exit(2);
}

var asmPath = args[0];
var outDir = args[1];

var asm = Assembly.LoadFrom(asmPath);
var types = asm.GetTypes();

foreach (var t in types)
{
    var attr = t.GetCustomAttribute<GodotScriptAttribute>();
    if (attr is null) continue;

    var className = string.IsNullOrWhiteSpace(attr.ClassName) ? t.Name : attr.ClassName!;
    var baseType = ResolveGodotType(attr.BaseTypeName);
    if (baseType is null)
    {
        Console.Error.WriteLine($"Skip {t.FullName}: cannot resolve BaseTypeName '{attr.BaseTypeName}'.");
        continue;
    }

    var exports = new List<PropertyInfo>();
    foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        if (p.CanRead && p.CanWrite && IsExportablePrimitive(p.PropertyType))
            exports.Add(p);

    var hasReady = t.GetMethod("Ready", BindingFlags.Instance | BindingFlags.Public, new Type[] { }) != null;
    var hasProcess = t.GetMethod("Process", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(double) }) != null;

    var ns = "Generated";
    var sb = new StringBuilder();

    sb.AppendLine("using Godot;");
    sb.AppendLine($"namespace {ns};");
    sb.AppendLine("[GlobalClass]");
    sb.AppendLine($"public partial class {className} : {baseType.FullName}");
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
        File.WriteAllText(filePath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}
