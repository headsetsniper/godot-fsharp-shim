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
    [Test]
    public void Emits_Class_And_BaseType()
    {
        var impl = IntegrationTestUtil.BuildImplAssemblyFs(baseType: KnownGodot.Node2D);
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(fooPath, Is.Not.Null, "Foo.cs not generated");
        var src = File.ReadAllText(fooPath!);
        StringAssert.Contains("[GlobalClass]", src);
        StringAssert.Contains($"public partial class Foo : {KnownGodot.Node2D}", src);
    }

    [Test]
    public void NodePath_Auto_Wiring_In_Ready()
    {
        var code = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Wire\", BaseTypeName=\"{KnownGodot.Node}\")>]",
            "type WireImpl() =",
            "    [<NodePath>]",
            "    member val Player : Node2D = null with get, set",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node).Assembly), annPath }, asmName: "WireImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Wire.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains($"GetNodeOrNull<{KnownGodot.Node2D}>(new NodePath(nameof(Player)))", src);
        StringAssert.Contains("_impl.Ready();", src);
    }

    [Test]
    public void Emits_GlobalClass_And_Icon_When_Provided()
    {
        var code = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Iconed\", BaseTypeName=\"{KnownGodot.Node}\", Icon=\"res://icon.svg\")>]",
            "type IconedImpl() =",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node).Assembly), annPath }, asmName: "IconedImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
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
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Tooly\", BaseTypeName=\"{KnownGodot.Node}\", Tool=true)>]",
            "type ToolyImpl() =",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node).Assembly), annPath }, asmName: "ToolyImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Tooly.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Tool]", src);
    }

    [Test]
    public void Forwards_EnterTree_And_ExitTree_When_Present()
    {
        var code = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"TreeGuy\", BaseTypeName=\"{KnownGodot.Node}\")>]",
            "type TreeGuyImpl() =",
            "    member _.EnterTree() = ()",
            "    member _.ExitTree() = ()",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node).Assembly), annPath }, asmName: "TreeGuyImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "TreeGuy.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("public override void _EnterTree() => _impl.EnterTree();", src);
        StringAssert.Contains("public override void _ExitTree() => _impl.ExitTree();", src);
    }

    [Test]
    public void Forwards_Lifecycle_Ready_And_Process()
    {
        var impl = IntegrationTestUtil.BuildImplAssemblyFs();
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(fooPath);
        StringAssert.Contains("public override void _Ready()", src);
        StringAssert.Contains("_impl.Ready();", src);
        StringAssert.Contains("public override void _Process(double delta) => _impl.Process(delta);", src);
    }

    [Test]
    public void Ready_Sets_IGdScript_Node_Before_Forwarding()
    {
        var code = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"GdInject\", BaseTypeName=\"{KnownGodot.Node2D}\")>]",
            "type GdInjectImpl() =",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node2D).Assembly), annPath }, asmName: "GdInjectImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "GdInject.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "GdInject.cs not generated");
        var src = File.ReadAllText(path!);
        StringAssert.Contains($"if (_impl is IGdScript<{KnownGodot.Node2D}> gd)", src);
        StringAssert.Contains("gd.Node = this;", src);
        StringAssert.Contains("_impl.Ready();", src);
    }

    [Test]
    public void Emits_Signal_Attributes_And_Invokers()
    {
        var code = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Sig\", BaseTypeName=\"{KnownGodot.Node}\")>]",
            "type SigImpl() =",
            "    member _.Signal_Fired() = ()",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node).Assembly), annPath }, asmName: "SigImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var sigPath = Directory.EnumerateFiles(outDir, "Sig.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(sigPath, Is.Not.Null, "Sig.cs not generated");
        var src = File.ReadAllText(sigPath!);
        StringAssert.Contains("[Signal]", src);
        StringAssert.Contains("public event System.Action Fired;", src);
        StringAssert.Contains("public void EmitFired()", src);
    }

    [Test]
    public void Emits_Typed_Signals_With_Args()
    {
        var code = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Sig2\", BaseTypeName=\"{KnownGodot.Node}\")>]",
            "type Sig2Impl() =",
            "    member _.Signal_Scored(points:int, who:string) = ()",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node).Assembly), annPath }, asmName: "Sig2Impl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);

        var sigPath = Directory.EnumerateFiles(outDir, "Sig2.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(sigPath, Is.Not.Null, "Sig2.cs not generated");
        var src = File.ReadAllText(sigPath!);

        StringAssert.Contains("[Signal]", src);
        StringAssert.Contains("public event System.Action<System.Int32, System.String> Scored;", src);
        StringAssert.Contains("public void EmitScored(System.Int32 points, System.String who) => Scored?.Invoke(points, who);", src);
    }

    [Test]
    public void Autoconnect_Emits_Connect_NoArgs()
    {
        var code = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"AutoA\", BaseTypeName=\"{KnownGodot.Node}\")>]",
            "type AutoAImpl() =",
            "    [<AutoConnect(\"/root/World\", \"Fired\")>]",
            "    member _.OnFired() = ()",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node).Assembly), annPath }, asmName: "AutoAImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "AutoA.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("GetNodeOrNull<Node>(new NodePath(\"/root/World\"))?.Connect(\"Fired\", Callable.From(() => _impl.OnFired()))", src);
    }

    [Test]
    public void Autoconnect_Emits_Connect_TypedArgs()
    {
        var code = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"AutoB\", BaseTypeName=\"{KnownGodot.Node}\")>]",
            "type AutoBImpl() =",
            "    [<AutoConnect(\"/root/World\", \"Scored\")>]",
            "    member _.OnScored(points:int, who:string) = ()",
            "    member _.Ready() = ()"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var impl = TestHelpers.CompileFSharp(code, new[] { TestHelpers.RefPathFromAssembly(typeof(Godot.Node).Assembly), annPath }, asmName: "AutoBImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "AutoB.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("GetNodeOrNull<Node>(new NodePath(\"/root/World\"))?.Connect(\"Scored\", Callable.From((System.Int32 arg0, System.String arg1) => _impl.OnScored(arg0, arg1)))", src);
    }
}
