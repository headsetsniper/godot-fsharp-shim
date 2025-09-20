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

    private static string RunShimGen(string implPath, string? fsSourceDir = null)
    {
        var outDir = TestHelpers.CreateTempDir();
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
    public void Forwards_Lifecycle_Ready_And_Process()
    {
        // Arrange
        var impl = BuildImplAssembly();
        var outDir = RunShimGen(impl);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(fooPath);

        // Assert forwarding methods exist
        StringAssert.Contains("public override void _Ready() => _impl.Ready();", src);
        StringAssert.Contains("public override void _Process(double delta) => _impl.Process(delta);", src);
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
        File.WriteAllText(fooPath, string.Join("\n", lines));
        var editedWrite = File.GetLastWriteTimeUtc(fooPath);
        Assert.That(editedWrite, Is.GreaterThanOrEqualTo(firstWrite));

        // Act: run ShimGen again with the same fs source dir (hash unchanged)
        RunShimGen(impl, fsDir);
        var secondWrite = File.GetLastWriteTimeUtc(fooPath);

        // Assert: generator should skip rewrite because SourceHash is unchanged
        Assert.That(secondWrite, Is.EqualTo(editedWrite));
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
        System.Threading.Thread.Sleep(50); // ensure timestamp tick difference on fast file systems
        RunShimGen(impl, fsDir);
        var secondWrite = File.GetLastWriteTimeUtc(fooPath);

        // Assert
        Assert.That(secondWrite, Is.GreaterThan(firstWrite));
        var updated = File.ReadAllText(fooPath);
        StringAssert.Contains("// SourceHash:", updated);
        Assert.That(updated, Is.Not.EqualTo(originalSrc));
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
