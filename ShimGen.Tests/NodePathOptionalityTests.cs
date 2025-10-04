using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace ShimGen.Tests;

[TestFixture]
public class NodePathOptionalityFailTests
{
    [OneTimeTearDown]
    public void AfterAll() => FsBatchComponent.CleanupForFixture(typeof(NodePathOptionalityFailTests));

    [Test]
    [FsCase("NpThrowOnOption", """
namespace Game
open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="NpA", BaseTypeName="Godot.Node")>]
type NpA() =
    [<NodePath>]
    member val Child : Node option = None with get, set
    member _.Ready() = ()
""")]
    public void NodePath_On_Option_Throws_Generation_Error()
    {
        Assert.Catch<AssertionException>(() => FsBatchComponent.BuildForFixture(typeof(NodePathOptionalityFailTests)));
    }

    [Test]
    [FsCase("OptionalNpMustBeOption", """
namespace Game
open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="NpB", BaseTypeName="Godot.Node")>]
type NpB() =
    [<OptionalNodePath>]
    member val Child : Node = Unchecked.defaultof<_> with get, set
    member _.Ready() = ()
""")]
    public void OptionalNodePath_On_NonOption_Throws_Generation_Error()
    {
        Assert.Catch<AssertionException>(() => FsBatchComponent.BuildForFixture(typeof(NodePathOptionalityFailTests)));
    }

}

[TestFixture]
public class NodePathOptionalityOkTests
{
    [OneTimeTearDown]
    public void AfterAll() => FsBatchComponent.CleanupForFixture(typeof(NodePathOptionalityOkTests));

    [Test]
    [FsCase("OptionalNpWiring", """
namespace Game
open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="NpC", BaseTypeName="Godot.Node")>]
type NpC() =
    [<OptionalNodePath(Path="SomeChild")>]
    member val Child : Node option = None with get, set
    member _.Ready() = ()
""")]
    public void OptionalNodePath_Wires_To_Option_Some_When_Found()
    {
        FsBatchComponent.BuildForFixture(typeof(NodePathOptionalityOkTests));
        var outDir = FsBatch.GetOutDir<NodePathOptionalityOkTests>();
        var path = Directory.EnumerateFiles(outDir!, "NpC.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains(" = __n_Child == null ? Microsoft.FSharp.Core.FSharpOption<Godot.Node>.None : Microsoft.FSharp.Core.FSharpOption<Godot.Node>.Some(__n_Child);", src);
    }
}
