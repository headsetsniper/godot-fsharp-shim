using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace ShimGen.Tests;

[TestFixture]
public class TestsModeGenerationTests
{
    private string _workDir = null!;
    private string _outDir = null!;

    [SetUp]
    public void SetUp()
    {
        _workDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "tests_mode_work", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
        _outDir = Path.Combine(_workDir, "generated");
        Directory.CreateDirectory(_outDir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_workDir, recursive: true); } catch { }
    }

    [Test]
    public void Generates_Test_Shim_For_Synthetic_GdUnit_Suite()
    {
        // Arrange: build a minimal in-memory C# assembly that mimics an F# test assembly shape.
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
            "    [GdUnit4.BeforeTest] void Setup() {}",
            "    [GdUnit4.TestCase] public Task Adds() { return Task.CompletedTask; }",
            "    [GdUnit4.TestCase] public void Multiplies() {}",
            "    [GdUnit4.AfterTest] void Teardown() {}",
            "  }",
            "}"
        });
        var asmPath = CompileAssembly("SampleTests.Tests", code, _workDir);

        // Act: run ShimGen in Tests mode.
        var prevMode = Environment.GetEnvironmentVariable("SHIMGEN_MODE");
        try
        {
            Environment.SetEnvironmentVariable("SHIMGEN_MODE", "Tests");
            var codeResult = Headsetsniper.Godot.FSharp.ShimGen.TestingHooks.RunInProcess(asmPath, _outDir, fsSourceDir: null, regenerateEnv: null, throwOnError: true, suppressErrorOutput: false);
            Assert.That(codeResult, Is.EqualTo(0));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHIMGEN_MODE", prevMode);
        }

        // Assert: one shim file exists with expected class name and forwarded methods.
        var shim = Directory.EnumerateFiles(_outDir, "*MathTests_TestsShim.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(shim, Is.Not.Null, "Expected test shim not generated");
        var src = File.ReadAllText(shim!);
        StringAssert.Contains("class MathTests_TestsShim", src);
        StringAssert.Contains("[TestSuite]", src);
        StringAssert.Contains("public void Adds", src);
        StringAssert.Contains("public void Multiplies", src);
        // Ensure reflection invocation lines present.
        StringAssert.Contains("GetMethod(\"Adds\"", src);
    }

    private static string CompileAssembly(string assemblyName, string code, string workDir)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Preview));
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var asmPath = Path.Combine(workDir, assemblyName + ".dll");
        var emitResult = compilation.Emit(asmPath);
        if (!emitResult.Success)
        {
            var errors = string.Join("\n", emitResult.Diagnostics.Select(d => d.ToString()));
            Assert.Fail("Failed to emit test assembly: " + errors);
        }
        return asmPath;
    }
}
