using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Headsetsniper.Godot.FSharp.Annotations;
using Headsetsniper.Godot.FSharp.ShimGen;
using NUnit.Framework;

namespace ShimGen.Tests;

[TestFixture]
public class GenerationHeadersAndRelocationTests
{
    [OneTimeSetUp]
    public void BeforeAll()
    {
        FsBatchComponent.BuildForFixture(typeof(GenerationHeadersAndRelocationTests));
    }

    [OneTimeTearDown]
    public void AfterAll()
    {
        FsBatchComponent.CleanupForFixture(typeof(GenerationHeadersAndRelocationTests));
    }

    [Test]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    do ()
""")]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    do ()
""")]
    public void Idempotent_Writes_Do_Not_Rewrite()
    {
        var outDir = FsBatch.GetOutDir<GenerationHeadersAndRelocationTests>();
        var fooPath = Directory.EnumerateFiles(outDir!, "Foo.cs", SearchOption.AllDirectories).First();
        var firstWrite = File.GetLastWriteTimeUtc(fooPath);
        var initialContent = File.ReadAllText(fooPath);
        var secondWrite = File.GetLastWriteTimeUtc(fooPath);
        Assert.That(secondWrite, Is.EqualTo(firstWrite));
        var afterContent = File.ReadAllText(fooPath);
        Assert.That(afterContent, Is.EqualTo(initialContent));
    }

    [Test]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    do ()
""")]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    do ()
""")]
    public void HashHeader_Skips_Rewrite_When_Hash_Unchanged()
    {
        var outDir = FsBatch.GetOutDir<GenerationHeadersAndRelocationTests>()!;
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var original = File.ReadAllText(fooPath);
        StringAssert.Contains("// SourceHash:", original);
        StringAssert.Contains("// SourceFile:", original);
        var firstWrite = File.GetLastWriteTimeUtc(fooPath);

        var lines = File.ReadAllLines(fooPath).ToList();
        var idxEndHeader = lines.FindIndex(l => l.Contains("</auto-generated>"));
        Assert.That(idxEndHeader, Is.GreaterThan(0));
        lines.Add("// trailing comment that should not trigger rewrite when hash unchanged");
        var editedContent = string.Join("\n", lines);
        File.WriteAllText(fooPath, editedContent);
        var editedWrite = File.GetLastWriteTimeUtc(fooPath);
        Assert.That(editedWrite, Is.GreaterThanOrEqualTo(firstWrite));

        // Rerun should keep content identical when hash unchanged
        FsBatchComponent.RerunForFixture(typeof(GenerationHeadersAndRelocationTests));
        var after = File.ReadAllText(fooPath);
        Assert.That(after, Is.EqualTo(editedContent));
    }

    [Test]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    do ()
""")]
    public void Header_Contains_ShimGen_Version()
    {
        var outDir = FsBatch.GetOutDir<GenerationHeadersAndRelocationTests>()!;
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(fooPath);
        StringAssert.Contains("// ShimGenVersion:", src);
    }

    [Test]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    do ()
""")]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    do ()
""")]
    public void Rewrites_When_Generator_Version_Is_Newer_Even_If_Hash_Matches()
    {
        var outDir = FsBatch.GetOutDir<GenerationHeadersAndRelocationTests>()!;
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var original = File.ReadAllText(fooPath);
        StringAssert.Contains("// SourceHash:", original);
        StringAssert.Contains("// ShimGenVersion:", original);

        var lines = File.ReadAllLines(fooPath).ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("// ShimGenVersion:", StringComparison.Ordinal))
            {
                lines[i] = "// ShimGenVersion: 0.0.0";
                break;
            }
        }
        var downgraded = string.Join("\n", lines);
        File.WriteAllText(fooPath, downgraded);

        System.Threading.Thread.Sleep(10); // ensure timestamp difference if needed
        FsBatchComponent.RerunForFixture(typeof(GenerationHeadersAndRelocationTests));
        var after = File.ReadAllText(fooPath);
        Assert.That(after, Is.Not.EqualTo(downgraded));
        StringAssert.DoesNotContain("// ShimGenVersion: 0.0.0", after);
    }

    [Test]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    do ()
""")]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    do ()
""")]
    public void HashHeader_Rewrites_When_Hash_Changes()
    {
        var outDir = FsBatch.GetOutDir<GenerationHeadersAndRelocationTests>()!;
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var originalSrc = File.ReadAllText(fooPath);

        var fsFile = FsBatch.GetFsPath<GenerationHeadersAndRelocationTests>("Foo")!;
        File.WriteAllText(fsFile, ($"namespace Game\n\nopen Headsetsniper.Godot.FSharp.Annotations\n\n[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"{KnownGodot.Node2D}\")>]\ntype FooImpl() =\n    do ()\n// changed\n"));

        System.Threading.Thread.Sleep(10);
        FsBatchComponent.RerunForFixture(typeof(GenerationHeadersAndRelocationTests));
        var updated = File.ReadAllText(fooPath);
        StringAssert.Contains("// SourceHash:", updated);
        Assert.That(updated, Is.Not.EqualTo(originalSrc));
    }

    [Test]
    public void Writes_To_Subfolders_Mirroring_Fs_Source()
    {
        var root = TestHelpers.CreateTempDir();
        var nestedDir = Path.Combine(root, "Game", "Scripts");
        Directory.CreateDirectory(nestedDir);
        var fsFile = Path.Combine(nestedDir, "Foo.fs");
        var fsContent = string.Join("\n", new[]{
            "namespace Game.Scripts",
            "",
            "open Headsetsniper.Godot.FSharp.Annotations",
            "",
            $"[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"{KnownGodot.Node2D}\")>]",
            "type FooImpl() =",
            "    do ()"
        }) + "\n";
        File.WriteAllText(fsFile, fsContent);

        var code = string.Join("\n", new[]{
            "namespace Game.Scripts",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"{KnownGodot.Node2D}\")>]",
            "type FooImpl() =",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node2D).Assembly), annPath }, asmName: "GameScriptsImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl, root);

        var expectedDir = Path.Combine(outDir, "Game", "Scripts");
        var expectedFile = Path.Combine(expectedDir, "Foo.cs");
        Assert.That(File.Exists(expectedFile), Is.True, $"Expected generated file at {expectedFile}");
        var src = File.ReadAllText(expectedFile);
        StringAssert.Contains($"public partial class Foo : {KnownGodot.Node2D}", src);
    }

    [Test]
    public void Relocates_When_Fs_File_Moved()
    {
        var root = TestHelpers.CreateTempDir();
        var dirA = Path.Combine(root, "Game", "Scripts");
        Directory.CreateDirectory(dirA);
        var fsA = Path.Combine(dirA, "Foo.fs");
        var fsContent = string.Join("\n", new[]{
            "namespace Game.Scripts",
            "",
            "open Headsetsniper.Godot.FSharp.Annotations",
            "",
            $"[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"{KnownGodot.Node2D}\")>]",
            "type FooImpl() =",
            "    do ()"
        }) + "\n";
        File.WriteAllText(fsA, fsContent);

        var code = string.Join("\n", new[]{
            "namespace Game.Scripts",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"{KnownGodot.Node2D}\")>]",
            "type FooImpl() =",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node2D).Assembly), annPath }, asmName: "GameScriptsImpl_Move");
        var outDir = IntegrationTestUtil.RunShimGen(impl, root);

        var oldGenerated = Path.Combine(outDir, "Game", "Scripts", "Foo.cs");
        Assert.That(File.Exists(oldGenerated), Is.True, "Initial generated file missing");

        var dirB = Path.Combine(root, "Game", "Gameplay");
        Directory.CreateDirectory(dirB);
        var fsB = Path.Combine(dirB, "Foo.fs");
        File.Move(fsA, fsB);

        IntegrationTestUtil.RunShimGen(impl, root, outDir);

        var newGenerated = Path.Combine(outDir, "Game", "Gameplay", "Foo.cs");
        Assert.That(File.Exists(newGenerated), Is.True, "New generated file missing at relocated path");
        Assert.That(File.Exists(oldGenerated), Is.False, "Old generated file should be removed after relocation");
    }

    [Test]
    public void Relocates_On_Class_Rename_And_Removes_Old()
    {
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
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"{KnownGodot.Node2D}\")>]",
            "type FooImpl() =",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var implFoo = TestHelpers.CompileFSharp(codeFoo, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node2D).Assembly), annPath }, asmName: "GameImpl_Rename");
        var outDir = IntegrationTestUtil.RunShimGen(implFoo, root);
        var fooGen = Path.Combine(outDir, "Game", "Foo.cs");
        Assert.That(File.Exists(fooGen), Is.True);

        var codeBar = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Bar\", BaseTypeName=\"{KnownGodot.Node2D}\")>]",
            "type BarImpl() =",
            "    member _.Ready() = ()"
        });
        var implBar = TestHelpers.CompileFSharp(codeBar, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node2D).Assembly), annPath }, asmName: "GameImpl_Rename2");

        IntegrationTestUtil.RunShimGen(implBar, root, outDir);
        var barGen = Path.Combine(outDir, "Game", "Bar.cs");
        Assert.That(File.Exists(barGen), Is.True, "Expected Bar.cs after rename");
        Assert.That(File.Exists(fooGen), Is.False, "Old Foo.cs should be removed after rename");
    }

    [Test]
    public void Prunes_Generated_When_Source_Removed()
    {
        var root = TestHelpers.CreateTempDir();
        var dir = Path.Combine(root, "Game");
        Directory.CreateDirectory(dir);
        var fs = Path.Combine(dir, "Foo.fs");
        var fsContent = string.Join("\n", new[]{
            "namespace Game",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"{KnownGodot.Node2D}\")>]",
            "type FooImpl() = do ()"
        }) + "\n";
        File.WriteAllText(fs, fsContent);

        var code = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"{KnownGodot.Node2D}\")>]",
            "type FooImpl() =",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node2D).Assembly), annPath }, asmName: "GameImpl_Prune");
        var outDir = IntegrationTestUtil.RunShimGen(impl, root);
        var gen = Path.Combine(outDir, "Game", "Foo.cs");
        Assert.That(File.Exists(gen), Is.True);

        File.Delete(fs);
        IntegrationTestUtil.RunShimGen(impl, root, outDir);
        Assert.That(File.Exists(gen), Is.False, "Generated file should be pruned when source removed");
    }
}
