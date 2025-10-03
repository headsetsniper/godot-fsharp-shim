using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.Annotations;

namespace ShimGen.Tests;

[TestFixture]
public class LifecycleAndMetadataTests
{
    [Test]
    public void Emits_Class_And_BaseType()
    {
        var impl = IntegrationTestUtil.BuildImplAssembly(baseType: "Godot.Node2D");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(fooPath, Is.Not.Null, "Foo.cs not generated");
        var src = File.ReadAllText(fooPath!);
        StringAssert.Contains("[GlobalClass]", src);
        StringAssert.Contains("public partial class Foo : Godot.Node2D", src);
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
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Wire.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("GetNodeOrNull<Godot.Node2D>(new NodePath(nameof(Player)))", src);
        StringAssert.Contains("_impl.Ready();", src);
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
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Tooly\", BaseTypeName=\"Godot.Node\", Tool=true)]",
            "  public class ToolyImpl { public void Ready(){} }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "ToolyImpl");
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
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"TreeGuy\", BaseTypeName=\"Godot.Node\")]",
            "  public class TreeGuyImpl { public void EnterTree(){} public void ExitTree(){} public void Ready(){} }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "TreeGuyImpl");
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
        var impl = IntegrationTestUtil.BuildImplAssembly();
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
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "GdInject.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "GdInject.cs not generated");
        var src = File.ReadAllText(path!);
        StringAssert.Contains("if (_impl is IGdScript<Godot.Node2D> gd)", src);
        StringAssert.Contains("gd.Node = this;", src);
        StringAssert.Contains("_impl.Ready();", src);
    }

    [Test]
    public void Emits_Signal_Attributes_And_Invokers()
    {
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
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Sig2\", BaseTypeName=\"Godot.Node\")]",
            "  public class Sig2Impl {",
            "    public void Signal_Scored(int points, string who) {}",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "Sig2Impl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);

        var sigPath = Directory.EnumerateFiles(outDir, "Sig2.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(sigPath, Is.Not.Null, "Sig2.cs not generated");
        var src = File.ReadAllText(sigPath!);

        StringAssert.Contains("[Signal]", src);
        StringAssert.Contains("public event System.Action<System.Int32, System.String> Scored;", src);
        StringAssert.Contains("public void EmitScored(System.Int32 points, System.String who) => Scored?.Invoke(points, who);", src);
    }
}
