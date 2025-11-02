using System;
using System.IO;
using System.Linq;
using Headsetsniper.Godot.FSharp.Annotations;
using NUnit.Framework;

namespace ShimGen.Tests;

[TestFixture]
public class SkipWritesFlagTests
{
    [Test]
    public void Scripts_Mode_SkipWrites_Preserves_Manual_Edits()
    {
        var annPath = typeof(GodotScriptAttribute).Assembly.Location;
        var godotStubPath = TestHelpers.RefPathFromAssembly(typeof(Godot.Node2D).Assembly);
        var code = string.Join("\n", new[]
        {
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            "[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")>]",
            "type FooImpl() =",
            "    member _.Ready() = ()"
        });
        var implPath = TestHelpers.CompileFSharp(code, new[] { annPath, godotStubPath }, asmName: "SkipWritesScripts");
        var fsRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(implPath)!, "..", "..", ".."));

        var outDir = IntegrationTestUtil.RunShimGen(implPath, fsRoot);
        var generated = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(generated, Is.Not.Null, "Expected Foo.cs to be generated");

        var sentinel = "// manual edit\n";
        File.WriteAllText(generated!, sentinel);

        var prevSkip = Environment.GetEnvironmentVariable("SHIMGEN_SKIP_WRITES");
        try
        {
            Environment.SetEnvironmentVariable("SHIMGEN_SKIP_WRITES", "1");
            IntegrationTestUtil.RunShimGen(implPath, fsRoot, outDirOverride: outDir);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHIMGEN_SKIP_WRITES", prevSkip);
        }

        var after = File.ReadAllText(generated!);
        Assert.That(after, Is.EqualTo(sentinel), "Skip-writes flag should preserve manual edits in Scripts mode");
    }

    [Test]
    public void Tests_Mode_SkipWrites_Preserves_Manual_Edits()
    {
        var code = string.Join(Environment.NewLine, new[]
        {
            "using System;",
            "using System.Threading.Tasks;",
            "namespace GdUnit4 {",
            "  [AttributeUsage(AttributeTargets.Class)] public sealed class TestSuiteAttribute : Attribute {}",
            "  [AttributeUsage(AttributeTargets.Method)] public sealed class BeforeTestAttribute : Attribute {}",
            "  [AttributeUsage(AttributeTargets.Method)] public sealed class AfterTestAttribute : Attribute {}",
            "  [AttributeUsage(AttributeTargets.Method)] public sealed class TestCaseAttribute : Attribute {}",
            "}",
            "namespace SampleTests {",
            "  [GdUnit4.TestSuite] public class MathTests {",
            "    [GdUnit4.TestCase] public void Adds() {}",
            "  }",
            "}"
        });
        var asmPath = CompileGdUnitAssembly(code);
        var outDir = TestHelpers.CreateTempDir();

        var prevMode = Environment.GetEnvironmentVariable("SHIMGEN_MODE");
        var prevSkip = Environment.GetEnvironmentVariable("SHIMGEN_SKIP_WRITES");
        try
        {
            Environment.SetEnvironmentVariable("SHIMGEN_MODE", "Tests");
            Environment.SetEnvironmentVariable("SHIMGEN_SKIP_WRITES", null);
            Headsetsniper.Godot.FSharp.ShimGen.TestingHooks.RunInProcess(asmPath, outDir, fsSourceDir: null, regenerateEnv: null, throwOnError: true, suppressErrorOutput: false);

            var shimPath = Directory.EnumerateFiles(outDir, "*MathTests_TestsShim.cs", SearchOption.AllDirectories).FirstOrDefault();
            Assert.That(shimPath, Is.Not.Null, "Expected test shim not generated");
            File.WriteAllText(shimPath!, "// manual edit\n");

            Environment.SetEnvironmentVariable("SHIMGEN_SKIP_WRITES", "1");
            Headsetsniper.Godot.FSharp.ShimGen.TestingHooks.RunInProcess(asmPath, outDir, fsSourceDir: null, regenerateEnv: null, throwOnError: true, suppressErrorOutput: false);

            var after = File.ReadAllText(shimPath!);
            Assert.That(after, Is.EqualTo("// manual edit\n"), "Skip-writes flag should preserve manual edits in Tests mode");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHIMGEN_MODE", prevMode);
            Environment.SetEnvironmentVariable("SHIMGEN_SKIP_WRITES", prevSkip);
        }
    }

    private static string CompileGdUnitAssembly(string code)
    {
        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(code, new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview));
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
            .Select(a => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "SampleTests.Tests",
            new[] { syntaxTree },
            refs,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        var dir = TestHelpers.CreateTempDir();
        var asmPath = Path.Combine(dir, "SampleTests.Tests.dll");
        using var fs = File.Open(asmPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var emit = compilation.Emit(fs);
        if (!emit.Success)
        {
            var errors = string.Join("\n", emit.Diagnostics.Select(d => d.ToString()));
            Assert.Fail("Failed to emit test assembly: " + errors);
        }
        return asmPath;
    }
}
