using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.Annotations;
using Headsetsniper.Godot.FSharp.ShimGen;

namespace ShimGen.Tests;

[TestFixture]
public class LifecycleAndMetadataTests
{
    [OneTimeSetUp]
    public void BeforeAll()
    {
        FsBatchComponent.BuildForFixture(typeof(LifecycleAndMetadataTests));
    }

    [OneTimeTearDown]
    public void AfterAll()
    {
        FsBatchComponent.CleanupForFixture(typeof(LifecycleAndMetadataTests));
    }

    [Test]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    member val Speed : int = 220 with get, set
    member _.Ready() = ()
    member _.Process(delta: double) = ()
""")]
    public void Emits_Class_And_BaseType()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var fooPath = Directory.EnumerateFiles(outDir!, "Foo.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(fooPath, Is.Not.Null, "Foo.cs not generated");
        var src = File.ReadAllText(fooPath!);
        StringAssert.Contains("[GlobalClass]", src);
        StringAssert.Contains($"public partial class Foo : {KnownGodot.Node2D}", src);
    }

    [Test]
    [FsCase("Wire", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Wire", BaseTypeName="Godot.Node")>]
type WireImpl() =
    [<NodePath>]
    member val Player : Node2D = null with get, set
    member _.Ready() = ()
""")]
    public void NodePath_Auto_Wiring_In_Ready()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var path = Directory.EnumerateFiles(outDir!, "Wire.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains($"GetNodeOrNull<{KnownGodot.Node2D}>(new NodePath(nameof(Player)))", src);
        StringAssert.Contains("_impl.Ready();", src);
    }

    [Test]
    [FsCase("Preloader", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Preloader", BaseTypeName="Godot.Node")>]
type PreloaderImpl() =
    [<Preload("res://things/foo.tscn", Required=true)>]
    member val Scene : PackedScene = null with get, set
    [<Preload("res://assets/optional.tres")>]
    member val Optional : Resource = null with get, set
    member _.Ready() = ()
""")]
    public void Preload_Resources_Are_Loaded_Before_Ready()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var path = Directory.EnumerateFiles(outDir!, "Preloader.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("var __p_Scene = ResourceLoader.Load<Godot.PackedScene>(\"res://things/foo.tscn\");", src);
        StringAssert.Contains("if (__p_Scene == null) throw new System.InvalidOperationException(\"[shimgen][Preloader] Missing preload resource \\\"res://things/foo.tscn\\\" for property \\\"Scene\\\" on Game.PreloaderImpl\");", src);
        StringAssert.Contains("_impl.Scene = __p_Scene;", src);
        StringAssert.Contains("var __p_Optional = ResourceLoader.Load<Godot.Resource>(\"res://assets/optional.tres\");", src);
        StringAssert.Contains("if (__p_Optional == null) throw new System.InvalidOperationException(\"[shimgen][Preloader] Missing preload resource \\\"res://assets/optional.tres\\\" for property \\\"Optional\\\" on Game.PreloaderImpl\");", src);
        StringAssert.Contains("_impl.Optional = __p_Optional;", src);
    }

    [Test]
    [FsCase("Iconed", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Iconed", BaseTypeName="Godot.Node", Icon="res://icon.svg")>]
type IconedImpl() =
    member _.Ready() = ()
""")]
    public void Emits_GlobalClass_And_Icon_When_Provided()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var path = Directory.EnumerateFiles(outDir!, "Iconed.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[GlobalClass]", src);
        StringAssert.Contains("[Icon(\"res://icon.svg\")]", src);
    }

    [Test]
    [FsCase("Tooly", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Tooly", BaseTypeName="Godot.Node", Tool=true)>]
type ToolyImpl() =
    member _.Ready() = ()
""")]
    public void Emits_Tool_Attribute_When_Requested()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var path = Directory.EnumerateFiles(outDir!, "Tooly.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Tool]", src);
    }

    [Test]
    [FsCase("TreeGuy", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="TreeGuy", BaseTypeName="Godot.Node")>]
type TreeGuyImpl() =
    member _.EnterTree() = ()
    member _.ExitTree() = ()
    member _.Ready() = ()
""")]
    public void Forwards_EnterTree_And_ExitTree_When_Present()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var path = Directory.EnumerateFiles(outDir!, "TreeGuy.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("public override void _EnterTree() => _impl.EnterTree();", src);
        StringAssert.Contains("public override void _ExitTree() => _impl.ExitTree();", src);
    }

    [Test]
    public void Forwards_Lifecycle_Ready_And_Process()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var fooPath = Directory.EnumerateFiles(outDir!, "Foo.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(fooPath);
        StringAssert.Contains("public override void _Ready()", src);
        StringAssert.Contains("_impl.Ready();", src);
        StringAssert.Contains("public override void _Process(double delta) => _impl.Process(delta);", src);
    }

    [Test]
    [FsCase("GdInject", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="GdInject", BaseTypeName="Godot.Node2D")>]
type GdInjectImpl() =
    member _.Ready() = ()
""")]
    public void Ready_Sets_IGdScript_Node_Before_Forwarding()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var path = Directory.EnumerateFiles(outDir!, "GdInject.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "GdInject.cs not generated");
        var src = File.ReadAllText(path!);
        StringAssert.Contains($"if (_impl is IGdScript<{KnownGodot.Node2D}> gd)", src);
        StringAssert.Contains("gd.Node = this;", src);
        StringAssert.Contains("_impl.Ready();", src);
    }

    [Test]
    [FsCase("Sig", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Sig", BaseTypeName="Godot.Node")>]
type SigImpl() =
    member _.Signal_Fired() = ()
    member _.Ready() = ()
""")]
    public void Emits_Signal_Attributes_And_Invokers()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var sigPath = Directory.EnumerateFiles(outDir!, "Sig.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(sigPath, Is.Not.Null, "Sig.cs not generated");
        var src = File.ReadAllText(sigPath!);
        StringAssert.Contains("[Signal]", src);
        StringAssert.Contains("public event System.Action Fired;", src);
        StringAssert.Contains("public void EmitFired()", src);
    }

    [Test]
    [FsCase("Sig2", """
namespace Game

open System
open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Sig2", BaseTypeName="Godot.Node")>]
type Sig2Impl() =
    member _.Signal_Scored(points:int, who:string) = ()
    member _.Ready() = ()
""")]
    public void Emits_Typed_Signals_With_Args()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var sigPath = Directory.EnumerateFiles(outDir!, "Sig2.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(sigPath, Is.Not.Null, "Sig2.cs not generated");
        var src = File.ReadAllText(sigPath!);

        StringAssert.Contains("[Signal]", src);
        StringAssert.Contains("public event System.Action<System.Int32, System.String> Scored;", src);
        StringAssert.Contains("public void EmitScored(System.Int32 points, System.String who) => Scored?.Invoke(points, who);", src);
    }

    [Test]
    [FsCase("AutoA", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="AutoA", BaseTypeName="Godot.Node")>]
type AutoAImpl() =
    [<AutoConnect("/root/World", "Fired")>]
    member _.OnFired() = ()
    member _.Ready() = ()
""")]
    public void Autoconnect_Emits_Connect_NoArgs()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var path = Directory.EnumerateFiles(outDir!, "AutoA.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("GetNodeOrNull<Node>(new NodePath(\"/root/World\"))?.Connect(\"Fired\", Callable.From(() => _impl.OnFired()))", src);
    }

    [Test]
    [FsCase("AutoB", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="AutoB", BaseTypeName="Godot.Node")>]
type AutoBImpl() =
    [<AutoConnect("/root/World", "Scored")>]
    member _.OnScored(points:int, who:string) = ()
    member _.Ready() = ()
""")]
    public void Autoconnect_Emits_Connect_TypedArgs()
    {
        var outDir = FsBatch.GetOutDir<LifecycleAndMetadataTests>();
        var path = Directory.EnumerateFiles(outDir!, "AutoB.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("GetNodeOrNull<Node>(new NodePath(\"/root/World\"))?.Connect(\"Scored\", Callable.From((System.Int32 arg0, System.String arg1) => _impl.OnScored(arg0, arg1)))", src);
    }
}
