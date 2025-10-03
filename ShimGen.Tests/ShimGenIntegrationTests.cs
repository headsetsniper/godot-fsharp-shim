using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Headsetsniper.Godot.FSharp.Annotations;

namespace ShimGen.Tests;

[TestFixture]
public class ShimGenIntegrationTests
{
    private string BuildImplAssembly(string className = "FooImpl", string baseType = "Godot.Node2D")
    {
        var code =
            "using Godot;\n" +
            "using Headsetsniper.Godot.FSharp.Annotations;\n" +
            "namespace Game\n" +
            "{\n" +
            $"    [GodotScript(ClassName=\"Foo\", BaseTypeName=\"{baseType}\")]\n" +
            $"    public class {className}\n" +
            "    {\n" +
            "        public int Speed { get; set; } = 220;\n" +
            "        public void Ready() { }\n" +
            "        public void Process(double delta) { }\n" +
            "    }\n" +
            "}\n";
        // Reference this test assembly, GodotStubs, and Annotations
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node2D).Assembly;
        return TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "GameImpl");
    }

    [Test]
    public void Exports_Godot_Struct_Types()
    {
        // Arrange: impl with Vector2, Vector3, Color
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Baz\", BaseTypeName=\"Godot.Node\")]",
            "  public class BazImpl {",
            "    public Vector2 V2 { get; set; }",
            "    public Vector3 V3 { get; set; }",
            "    public Color C { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "BazImpl");
        var outDir = RunShimGen(impl);

        // Act
        var bazPath = Directory.EnumerateFiles(outDir, "Baz.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(bazPath, Is.Not.Null, "Baz.cs not generated");
        var src = File.ReadAllText(bazPath!);

        // Assert: export attributes for supported structs
        StringAssert.Contains("[Export] public Godot.Vector2 V2", src);
        StringAssert.Contains("[Export] public Godot.Vector3 V3", src);
        StringAssert.Contains("[Export] public Godot.Color C", src);
    }

    public enum TestEnum { A = 0, B = 1 }

    [Test]
    public void Exports_Arrays_And_Enums()
    {
        // Arrange: arrays of primitives and enums
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Qux\", BaseTypeName=\"Godot.Node\")]",
            "  public class QuxImpl {",
            "    public int[] Numbers { get; set; }",
            "    public string[] Names { get; set; }",
            "    public ShimGen.Tests.ShimGenIntegrationTests.TestEnum Mode { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath), MetadataReference.CreateFromFile(typeof(ShimGenIntegrationTests).Assembly.Location) }, asmName: "QuxImpl");
        var outDir = RunShimGen(impl);

        // Act
        var quxPath = Directory.EnumerateFiles(outDir, "Qux.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(quxPath, Is.Not.Null, "Qux.cs not generated");
        var src = File.ReadAllText(quxPath!);

        // Assert
        StringAssert.Contains("[Export] public System.Int32[] Numbers", src);
        StringAssert.Contains("[Export] public System.String[] Names", src);
        StringAssert.Contains("[Export] public ShimGen.Tests.ShimGenIntegrationTests.TestEnum Mode", src);
    }

    private static string RunShimGen(string implPath, string? fsSourceDir = null, string? outDirOverride = null)
    {
        var outDir = outDirOverride ?? TestHelpers.CreateTempDir();
        // Determine config/tfm from the test output path: …/ShimGen.Tests/bin/{Configuration}/{TFM}
        var testDir = TestContext.CurrentContext.TestDirectory;
        var tfm = Path.GetFileName(testDir);
        var configuration = Path.GetFileName(Path.GetDirectoryName(testDir)!);
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
        var outDirShim = Path.Combine(repoRoot, "ShimGen", "bin", configuration, tfm);
        // Prefer new assembly name (matching PackageId/Project file), but fall back to legacy name
        var exeCandidates = new[]
        {
            Path.Combine(outDirShim, "Headsetsniper.Godot.FSharp.ShimGen.dll"),
            Path.Combine(outDirShim, "ShimGen.dll"),
        };
        var exe = exeCandidates.FirstOrDefault(File.Exists)
                  ?? Directory.EnumerateFiles(outDirShim, "*ShimGen*.dll", SearchOption.TopDirectoryOnly)
                       .OrderByDescending(p => p.Length) // stable choice if multiple
                       .FirstOrDefault();
        Assert.That(exe, Is.Not.Null.And.Not.Empty, $"ShimGen not built; looked in {outDirShim}");
        Assert.That(File.Exists(exe!), Is.True, $"ShimGen not built at {exe}");

        // Ensure the attribute assembly is next to the impl assembly to help resolution
        var implDir = Path.GetDirectoryName(implPath)!;
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var targetAnn = Path.Combine(implDir, Path.GetFileName(annPath));
        if (!File.Exists(targetAnn)) File.Copy(annPath, targetAnn, overwrite: true);
        var args = fsSourceDir == null
            ? $"\"{exe}\" \"{implPath}\" \"{outDir}\""
            : $"\"{exe}\" \"{implPath}\" \"{outDir}\" \"{fsSourceDir}\"";
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var p = System.Diagnostics.Process.Start(psi)!;
        p.WaitForExit();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        Assert.That(p.ExitCode, Is.EqualTo(0), $"ShimGen failed. Stdout:\n{stdout}\nStderr:\n{stderr}");
        return outDir;
    }

    private static (string dir, string file) CreateTempFsSource(string? content = null)
    {
        var dir = TestHelpers.CreateTempDir();
        var file = Path.Combine(dir, "Game.fs");
        // Ensure content matches the type 'Game.FooImpl' so ShimGen can locate it for hashing.
        var fs = content ?? string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Headsetsniper.Godot.FSharp.Annotations",
            "",
            "[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")>]",
            "type FooImpl() =",
            "    do ()"
        }) + "\n";
        File.WriteAllText(file, fs);
        return (dir, file);
    }

    [Test]
    public void Emits_Class_And_BaseType()
    {
        // Arrange
        var impl = BuildImplAssembly(baseType: "Godot.Node2D");
        var outDir = RunShimGen(impl);

        // Act
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(fooPath, Is.Not.Null, "Foo.cs not generated");
        var src = File.ReadAllText(fooPath!);

        // Assert
        StringAssert.Contains("[GlobalClass]", src);
        StringAssert.Contains("public partial class Foo : Godot.Node2D", src);
    }

    [Test]
    public void Emits_GlobalClass_And_Icon_When_Provided()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Iconed\", BaseTypeName=\"Godot.Node\", Icon=\"res://icon.svg\")]",
            "  public class IconedImpl { public void Ready(){} }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "IconedImpl");
        var outDir = RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Iconed.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[GlobalClass]", src);
        StringAssert.Contains("[Icon(\"res://icon.svg\")]", src);
    }

    [Test]
    public void Emits_Tool_Attribute_When_Requested()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Tooly\", BaseTypeName=\"Godot.Node\", Tool=true)]",
            "  public class ToolyImpl { public void Ready(){} }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "ToolyImpl");
        var outDir = RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Tooly.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Tool]", src);
    }

    [Test]
    public void Forwards_EnterTree_And_ExitTree_When_Present()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"TreeGuy\", BaseTypeName=\"Godot.Node\")]",
            "  public class TreeGuyImpl { public void EnterTree(){} public void ExitTree(){} public void Ready(){} }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "TreeGuyImpl");
        var outDir = RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "TreeGuy.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("public override void _EnterTree() => _impl.EnterTree();", src);
        StringAssert.Contains("public override void _ExitTree() => _impl.ExitTree();", src);
    }

    [Test]
    public void NodePath_Auto_Wiring_In_Ready()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Wire\", BaseTypeName=\"Godot.Node\")]",
            "  public class WireImpl {",
            "    [NodePath] public Node2D Player { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "WireImpl");
        var outDir = RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Wire.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("GetNodeOrNull<Godot.Node2D>(new NodePath(nameof(Player)))", src);
        StringAssert.Contains("_impl.Ready();", src);
    }

    [Test]
    public void Export_Range_Hint_Is_Emitted()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Rangey\", BaseTypeName=\"Godot.Node\")]",
            "  public class RangeyImpl {",
            "    [ExportRange(0, 10, 0.5, true)] public float Speed { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "RangeyImpl");
        var outDir = RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Rangey.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.Range, \"0,10,0.5,1\")]", src);
    }

    [Test]
    public void Forwards_Lifecycle_Ready_And_Process()
    {
        // Arrange
        var impl = BuildImplAssembly();
        var outDir = RunShimGen(impl);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(fooPath);

        // Assert forwarding methods exist
        StringAssert.Contains("public override void _Ready()", src);
        StringAssert.Contains("_impl.Ready();", src);
        StringAssert.Contains("public override void _Process(double delta) => _impl.Process(delta);", src);
    }

    [Test]
    public void Ready_Sets_IGdScript_Node_Before_Forwarding()
    {
        // Arrange: impl declares Ready and implements IGdScript<Node2D>
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"GdInject\", BaseTypeName=\"Godot.Node2D\")]",
            "  public class GdInjectImpl : IGdScript<Node2D> {",
            "    public Node2D Node { get; set; }",
            "    public bool WasReady { get; private set; }",
            "    public void Ready(){ WasReady = Node != null; }",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node2D).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "GdInjectImpl");
        var outDir = RunShimGen(impl);

        // Act
        var path = Directory.EnumerateFiles(outDir, "GdInject.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "GdInject.cs not generated");
        var src = File.ReadAllText(path!);

        // Assert: _Ready sets IGdScript<Node2D>.Node = this; then calls _impl.Ready();
        StringAssert.Contains("if (_impl is IGdScript<Godot.Node2D> gd)", src);
        StringAssert.Contains("gd.Node = this;", src);
        StringAssert.Contains("_impl.Ready();", src);
    }

    [Test]
    public void Exports_Primitive_Properties()
    {
        // Arrange
        var impl = BuildImplAssembly();
        var outDir = RunShimGen(impl);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(fooPath);

        // Assert primitive export exists
        StringAssert.Contains("[Export] public System.Int32 Speed", src);

        // And the shim compiles against stubs and the impl
        var stubsAsm = typeof(Godot.Node2D).Assembly;
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var shimDll = TestHelpers.CompileCSharp(src, new[] {
            TestHelpers.RefFromAssembly(stubsAsm),
            TestHelpers.RefFromPath(annPath),
            MetadataReference.CreateFromFile(impl)
        }, asmName: "FooShim");
        Assert.That(File.Exists(shimDll), Is.True);
    }

    [Test]
    public void Exports_Only_Primitive_Properties()
    {
        // Arrange: an impl with multiple properties, only primitive ones should get [Export]
        var code = "using Godot; using Headsetsniper.Godot.FSharp.Annotations; namespace Game { [GodotScript(ClassName=\"Bar\", BaseTypeName=\"Godot.Node\")] public class BarImpl { public int A {get;set;} public string S {get;set;} public object O {get;set;} public void Ready(){} } }";
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "BarImpl");
        var outDir = RunShimGen(impl);

        // Act
        var barPath = Directory.EnumerateFiles(outDir, "Bar.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(barPath, Is.Not.Null, "Bar.cs not generated");
        var src = File.ReadAllText(barPath!);

        // Assert: primitive exports present, object missing
        StringAssert.Contains("[Export] public System.Int32 A", src);
        StringAssert.Contains("[Export] public System.String S", src);
        StringAssert.DoesNotContain("object O", src);
    }

    [Test]
    public void Idempotent_Writes_Do_Not_Rewrite()
    {
        // Arrange
        var impl = BuildImplAssembly();
        var outDir = RunShimGen(impl);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var firstWrite = File.GetLastWriteTimeUtc(fooPath);
        var initialContent = File.ReadAllText(fooPath);

        // Act: run shimgen again
        var _ = RunShimGen(impl);
        var secondWrite = File.GetLastWriteTimeUtc(fooPath);

        // Assert: unchanged
        Assert.That(secondWrite, Is.EqualTo(firstWrite));
    }

    [Test]
    public void HashHeader_Skips_Rewrite_When_Hash_Unchanged()
    {
        // Arrange: run once with a temp fs source dir to produce a header with SourceHash
        var impl = BuildImplAssembly();
        var (fsDir, fsFile) = CreateTempFsSource();
        var outDir = RunShimGen(impl, fsDir);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var original = File.ReadAllText(fooPath);
        StringAssert.Contains("// SourceHash:", original);
        StringAssert.Contains("// SourceFile:", original);
        var firstWrite = File.GetLastWriteTimeUtc(fooPath);

        // Modify body (simulate manual edit) but keep the header & SourceHash intact
        var lines = File.ReadAllLines(fooPath).ToList();
        var idxEndHeader = lines.FindIndex(l => l.Contains("</auto-generated>"));
        Assert.That(idxEndHeader, Is.GreaterThan(0));
        lines.Add("// trailing comment that should not trigger rewrite when hash unchanged");
        var editedContent = string.Join("\n", lines);
        File.WriteAllText(fooPath, editedContent);
        var editedWrite = File.GetLastWriteTimeUtc(fooPath);
        Assert.That(editedWrite, Is.GreaterThanOrEqualTo(firstWrite));

        // Act: run ShimGen again with the same fs source dir (hash unchanged)
        RunShimGen(impl, fsDir, outDir);
        var secondWrite = File.GetLastWriteTimeUtc(fooPath);
        // Assert: generator should skip rewrite because SourceHash is unchanged
        // Prefer content equality over timestamp to avoid filesystem tick edge cases
        var after = File.ReadAllText(fooPath);
        Assert.That(after, Is.EqualTo(editedContent));
    }

    [Test]
    public void Header_Contains_ShimGen_Version()
    {
        var impl = BuildImplAssembly();
        var (fsDir, _) = CreateTempFsSource();
        var outDir = RunShimGen(impl, fsDir);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(fooPath);
        StringAssert.Contains("// ShimGenVersion:", src);
    }

    [Test]
    public void Rewrites_When_Generator_Version_Is_Newer_Even_If_Hash_Matches()
    {
        // Arrange: initial run produces a file with SourceHash and ShimGenVersion
        var impl = BuildImplAssembly();
        var (fsDir, _) = CreateTempFsSource();
        var outDir = RunShimGen(impl, fsDir);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var original = File.ReadAllText(fooPath);
        StringAssert.Contains("// SourceHash:", original);
        StringAssert.Contains("// ShimGenVersion:", original);

        // Simulate an older generator by modifying the ShimGenVersion header to a lower version, keep the same hash
        var lines = File.ReadAllLines(fooPath).ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("// ShimGenVersion:", StringComparison.Ordinal))
            {
                lines[i] = "// ShimGenVersion: 0.0.0"; // definitely older
                break;
            }
        }
        var downgraded = string.Join("\n", lines);
        File.WriteAllText(fooPath, downgraded);

        // Act: run ShimGen again with same fsDir (hash unchanged) => should rewrite due to newer generator
        System.Threading.Thread.Sleep(10);
        RunShimGen(impl, fsDir, outDir);
        var after = File.ReadAllText(fooPath);

        // Assert: content should no longer equal the downgraded header version; ShimGenVersion should not be 0.0.0
        Assert.That(after, Is.Not.EqualTo(downgraded));
        StringAssert.DoesNotContain("// ShimGenVersion: 0.0.0", after);
    }

    [Test]
    public void HashHeader_Rewrites_When_Hash_Changes()
    {
        // Arrange: initial run
        var impl = BuildImplAssembly();
        var (fsDir, fsFile) = CreateTempFsSource();
        var outDir = RunShimGen(impl, fsDir);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var originalSrc = File.ReadAllText(fooPath);
        var firstWrite = File.GetLastWriteTimeUtc(fooPath);

        // Change fs content to alter hash
        File.WriteAllText(fsFile, ("namespace Game\n\nopen Headsetsniper.Godot.FSharp.Annotations\n\n[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")>]\ntype FooImpl() =\n    do ()\n// changed\n"));

        // Act: run again; hash differs so rewrite should occur
        // Short sleep may not guarantee timestamp differences on all filesystems; avoid relying solely on timestamps
        System.Threading.Thread.Sleep(10);
        RunShimGen(impl, fsDir, outDir);
        var secondWrite = File.GetLastWriteTimeUtc(fooPath);
        var updated = File.ReadAllText(fooPath);
        // Assert: content changed (hash header or body) and SourceHash present
        StringAssert.Contains("// SourceHash:", updated);
        Assert.That(updated, Is.Not.EqualTo(originalSrc));
    }

    [Test]
    public void Writes_To_Subfolders_Mirroring_Fs_Source()
    {
        // Arrange: create nested F# source path and a matching type namespace
        var root = TestHelpers.CreateTempDir();
        var nestedDir = Path.Combine(root, "Game", "Scripts");
        Directory.CreateDirectory(nestedDir);
        var fsFile = Path.Combine(nestedDir, "Foo.fs");
        var fsContent = string.Join("\n", new[]{
            "namespace Game.Scripts",
            "",
            "open Headsetsniper.Godot.FSharp.Annotations",
            "",
            "[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")>]",
            "type FooImpl() =",
            "    do ()"
        }) + "\n";
        File.WriteAllText(fsFile, fsContent);

        // Impl assembly consistent with namespace/type
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game.Scripts {",
            "  [GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")]",
            "  public class FooImpl { public void Ready(){} }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node2D).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "GameScriptsImpl");

        // Act: run shimgen with fsSourceDir pointing at the root
        var outDir = RunShimGen(impl, root);

        // Expect Foo.cs in Game/Scripts subfolder under outDir
        var expectedDir = Path.Combine(outDir, "Game", "Scripts");
        var expectedFile = Path.Combine(expectedDir, "Foo.cs");
        Assert.That(File.Exists(expectedFile), Is.True, $"Expected generated file at {expectedFile}");
        var src = File.ReadAllText(expectedFile);
        StringAssert.Contains("public partial class Foo : Godot.Node2D", src);
    }

    [Test]
    public void Relocates_When_Fs_File_Moved()
    {
        // Arrange initial nested location
        var root = TestHelpers.CreateTempDir();
        var dirA = Path.Combine(root, "Game", "Scripts");
        Directory.CreateDirectory(dirA);
        var fsA = Path.Combine(dirA, "Foo.fs");
        var fsContent = string.Join("\n", new[]{
            "namespace Game.Scripts",
            "",
            "open Headsetsniper.Godot.FSharp.Annotations",
            "",
            "[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")>]",
            "type FooImpl() =",
            "    do ()"
        }) + "\n";
        File.WriteAllText(fsA, fsContent);

        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game.Scripts {",
            "  [GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")]",
            "  public class FooImpl { public void Ready(){} }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node2D).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "GameScriptsImpl_Move");
        var outDir = RunShimGen(impl, root);

        var oldGenerated = Path.Combine(outDir, "Game", "Scripts", "Foo.cs");
        Assert.That(File.Exists(oldGenerated), Is.True, "Initial generated file missing");

        // Move the fs file to a different subfolder (simulate refactor)
        var dirB = Path.Combine(root, "Game", "Gameplay");
        Directory.CreateDirectory(dirB);
        var fsB = Path.Combine(dirB, "Foo.fs");
        File.Move(fsA, fsB);

        // Act: run ShimGen again; it should generate in new place and remove the old duplicate
        RunShimGen(impl, root, outDir);

        var newGenerated = Path.Combine(outDir, "Game", "Gameplay", "Foo.cs");
        Assert.That(File.Exists(newGenerated), Is.True, "New generated file missing at relocated path");
        Assert.That(File.Exists(oldGenerated), Is.False, "Old generated file should be removed after relocation");
    }

    [Test]
    public void Relocates_On_Class_Rename_And_Removes_Old()
    {
        // Arrange: initial class FooImpl with ClassName Foo
        var root = TestHelpers.CreateTempDir();
        var dir = Path.Combine(root, "Game");
        Directory.CreateDirectory(dir);
        var fs = Path.Combine(dir, "Foo.fs");
        var fsContent = string.Join("\n", new[]{
            "namespace Game",
            "open Headsetsniper.Godot.FSharp.Annotations",
            "[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")>]",
            "type FooImpl() = do ()"
        }) + "\n";
        File.WriteAllText(fs, fsContent);

        var codeFoo = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game { [GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")] public class FooImpl { public void Ready(){} } }"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node2D).Assembly;
        var implFoo = TestHelpers.CompileCSharp(codeFoo, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "GameImpl_Rename");
        var outDir = RunShimGen(implFoo, root);
        var fooGen = Path.Combine(outDir, "Game", "Foo.cs");
        Assert.That(File.Exists(fooGen), Is.True);

        // Rename class: ClassName=Bar (same file content for hash, or modify impl to BarImpl)
        var codeBar = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game { [GodotScript(ClassName=\"Bar\", BaseTypeName=\"Godot.Node2D\")] public class BarImpl { public void Ready(){} } }"
        });
        var implBar = TestHelpers.CompileCSharp(codeBar, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "GameImpl_Rename2");

        // Act: run shimgen again; expect Bar.cs and Foo.cs removed
        RunShimGen(implBar, root, outDir);
        var barGen = Path.Combine(outDir, "Game", "Bar.cs");
        Assert.That(File.Exists(barGen), Is.True, "Expected Bar.cs after rename");
        Assert.That(File.Exists(fooGen), Is.False, "Old Foo.cs should be removed after rename");
    }

    [Test]
    public void Prunes_Generated_When_Source_Removed()
    {
        // Arrange: generate one file from fs source, then delete source
        var root = TestHelpers.CreateTempDir();
        var dir = Path.Combine(root, "Game");
        Directory.CreateDirectory(dir);
        var fs = Path.Combine(dir, "Foo.fs");
        var fsContent = string.Join("\n", new[]{
            "namespace Game",
            "open Headsetsniper.Godot.FSharp.Annotations",
            "[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")>]",
            "type FooImpl() = do ()"
        }) + "\n";
        File.WriteAllText(fs, fsContent);

        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game { [GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")] public class FooImpl { public void Ready(){} } }"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node2D).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "GameImpl_Prune");
        var outDir = RunShimGen(impl, root);
        var gen = Path.Combine(outDir, "Game", "Foo.cs");
        Assert.That(File.Exists(gen), Is.True);

        // Remove source and run again: no attributes/types will be found; file should be pruned
        File.Delete(fs);
        RunShimGen(impl, root, outDir);
        Assert.That(File.Exists(gen), Is.False, "Generated file should be pruned when source removed");
    }

    [Test]
    public void Emits_Signal_Attributes_And_Invokers()
    {
        // Convention: a public void method starting with "Signal_" translates to [Signal] public event and an invoker method
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Sig\", BaseTypeName=\"Godot.Node\")]",
            "  public class SigImpl {",
            "    public void Signal_Fired() {}",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "SigImpl");
        var outDir = RunShimGen(impl);

        var sigPath = Directory.EnumerateFiles(outDir, "Sig.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(sigPath, Is.Not.Null, "Sig.cs not generated");
        var src = File.ReadAllText(sigPath!);

        StringAssert.Contains("[Signal]", src);
        StringAssert.Contains("public event System.Action Fired;", src);
        StringAssert.Contains("public void EmitFired()", src);
    }

    [Test]
    public void Forwards_PhysicsProcess_Input_UnhandledInput_Notification()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Callbacks\", BaseTypeName=\"Godot.Node\")]",
            "  public class CallbacksImpl {",
            "    public void PhysicsProcess(double delta){}",
            "    public void Input(InputEvent e){}",
            "    public void UnhandledInput(InputEvent e){}",
            "    public void Notification(long what){}",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "CallbacksImpl");
        var outDir = RunShimGen(impl);

        var path = Directory.EnumerateFiles(outDir, "Callbacks.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Callbacks.cs not generated");
        var src = File.ReadAllText(path!);

        StringAssert.Contains("public override void _PhysicsProcess(double delta) => _impl.PhysicsProcess(delta);", src);
        StringAssert.Contains("public override void _Input(Godot.InputEvent @event) => _impl.Input(@event);", src);
        StringAssert.Contains("public override void _UnhandledInput(Godot.InputEvent @event) => _impl.UnhandledInput(@event);", src);
        StringAssert.Contains("public override void _Notification(long what) => _impl.Notification(what);", src);
    }
}
