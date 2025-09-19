using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Godot.FSharp.Annotations;

namespace ShimGen.Tests;

[TestFixture]
public class ShimGenIntegrationTests
{
    private string BuildImplAssembly(string className = "FooImpl", string baseType = "Godot.Node2D")
    {
        var code =
            "using Godot;\n" +
            "using Godot.FSharp.Annotations;\n" +
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

    private static string RunShimGen(string implPath)
    {
        var outDir = TestHelpers.CreateTempDir();
        // Determine config/tfm from the test output path: …/ShimGen.Tests/bin/{Configuration}/{TFM}
        var testDir = TestContext.CurrentContext.TestDirectory;
        var tfm = Path.GetFileName(testDir);
        var configuration = Path.GetFileName(Path.GetDirectoryName(testDir)!);
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "ShimGen", "bin", configuration, tfm, "ShimGen.dll");
        Assert.That(File.Exists(exe), $"ShimGen not built at {exe}");

        // Ensure the attribute assembly is next to the impl assembly to help resolution
        var implDir = Path.GetDirectoryName(implPath)!;
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var targetAnn = Path.Combine(implDir, Path.GetFileName(annPath));
        if (!File.Exists(targetAnn)) File.Copy(annPath, targetAnn, overwrite: true);
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"\"{exe}\" \"{implPath}\" \"{outDir}\"")
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
        var code = "using Godot; using Godot.FSharp.Annotations; namespace Game { [GodotScript(ClassName=\"Bar\", BaseTypeName=\"Godot.Node\")] public class BarImpl { public int A {get;set;} public string S {get;set;} public object O {get;set;} public void Ready(){} } }";
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

        // Act: run shimgen again
        var _ = RunShimGen(impl);
        var secondWrite = File.GetLastWriteTimeUtc(fooPath);

        // Assert: unchanged
        Assert.That(secondWrite, Is.EqualTo(firstWrite));
    }
}
