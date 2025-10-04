using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.Annotations;
using Headsetsniper.Godot.FSharp.ShimGen;

namespace ShimGen.Tests;

[TestFixture]
public class PreloadOptionSemanticsTests
{
    [OneTimeSetUp]
    public void BeforeAll() => FsBatchComponent.BuildForFixture(typeof(PreloadOptionSemanticsTests));

    [OneTimeTearDown]
    public void AfterAll() => FsBatchComponent.CleanupForFixture(typeof(PreloadOptionSemanticsTests));

    [Test]
    [FsCase("PreloadRequiredThrows", """
namespace Game
open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="PL1", BaseTypeName="Godot.Node")>]
type PL1() =
    [<Preload("res://missing/never.there")>]
    member val Tex : Texture2D = Unchecked.defaultof<_> with get, set
    member _.Ready() = ()
""")]
    public void Preload_Missing_Throws_InvalidOperation()
    {
        var outDir = FsBatch.GetOutDir<PreloadOptionSemanticsTests>();
        var path = Directory.EnumerateFiles(outDir!, "PL1.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("throw new System.InvalidOperationException(\"[shimgen][PL1] Missing preload resource", src);
    }

    [Test]
    [FsCase("PreloadOptionSome", """
namespace Game
open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="PL2", BaseTypeName="Godot.Node")>]
type PL2() =
    [<Preload("res://icon.svg")>]
    member val Tex : Texture2D option = None with get, set
    member _.Ready() = ()
""")]
    public void Preload_Option_Assigns_Some()
    {
        var outDir = FsBatch.GetOutDir<PreloadOptionSemanticsTests>();
        var path = Directory.EnumerateFiles(outDir!, "PL2.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("= Microsoft.FSharp.Core.FSharpOption<Godot.Texture2D>.Some(__p_Tex);", src);
    }
}
