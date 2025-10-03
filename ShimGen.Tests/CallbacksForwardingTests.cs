using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.Annotations;
using Headsetsniper.Godot.FSharp.ShimGen;

namespace ShimGen.Tests;

[TestFixture]
public class CallbacksForwardingTests
{
    [Test]
    public void Forwards_PhysicsProcess_Input_UnhandledInput_Notification()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            $"  [GodotScript(ClassName=\"Callbacks\", BaseTypeName=\"{KnownGodot.Node}\")]",
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
        var outDir = IntegrationTestUtil.RunShimGen(impl);

        var path = Directory.EnumerateFiles(outDir, "Callbacks.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Callbacks.cs not generated");
        var src = File.ReadAllText(path!);

        StringAssert.Contains("public override void _PhysicsProcess(double delta) => _impl.PhysicsProcess(delta);", src);
        StringAssert.Contains("public override void _Input(Godot.InputEvent @event) => _impl.Input(@event);", src);
        StringAssert.Contains("public override void _UnhandledInput(Godot.InputEvent @event) => _impl.UnhandledInput(@event);", src);
        StringAssert.Contains("public override void _Notification(long what) => _impl.Notification(what);", src);
    }

    [Test]
    public void Forwards_Control_Ui_And_DragDrop_Callbacks()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            $"  [GodotScript(ClassName=\"UI\", BaseTypeName=\"{KnownGodot.Control}\")]",
            "  public class UiImpl {",
            "    public void Ready(){}",
            "    public void GuiInput(InputEvent e){}",
            "    public void ShortcutInput(InputEvent e){}",
            "    public void UnhandledKeyInput(InputEvent e){}",
            "    public bool CanDropData(Vector2 p, Variant v) => true;",
            "    public void DropData(Vector2 p, Variant v){}",
            "    public object GetDragData(Vector2 p) => new object();",
            "    public bool HasPoint(Vector2 p) => true;",
            "    public Vector2 GetMinimumSize() => new Vector2();",
            "    public Control MakeCustomTooltip(string s) => new Control();",
            "    public string GetTooltip(Vector2 p) => \"tip\";",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Control).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "UiImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);

        var path = Directory.EnumerateFiles(outDir, "UI.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "UI.cs not generated");
        var src = File.ReadAllText(path!);

        StringAssert.Contains("public override void _GuiInput(Godot.InputEvent @event) => _impl.GuiInput(@event);", src);
        StringAssert.Contains("public override void _ShortcutInput(Godot.InputEvent @event) => _impl.ShortcutInput(@event);", src);
        StringAssert.Contains("public override void _UnhandledKeyInput(Godot.InputEvent @event) => _impl.UnhandledKeyInput(@event);", src);
        StringAssert.Contains("public override bool _CanDropData(Godot.Vector2 atPosition, Godot.Variant data) => _impl.CanDropData(atPosition, data);", src);
        StringAssert.Contains("public override void _DropData(Godot.Vector2 atPosition, Godot.Variant data) => _impl.DropData(atPosition, data);", src);
        StringAssert.Contains("public override Godot.Variant _GetDragData(Godot.Vector2 atPosition) => (Godot.Variant)_impl.GetDragData(atPosition);", src);
        StringAssert.Contains("public override bool _HasPoint(Godot.Vector2 position) => _impl.HasPoint(position);", src);
        StringAssert.Contains("public override Godot.Vector2 _GetMinimumSize() => _impl.GetMinimumSize();", src);
        StringAssert.Contains("public override Godot.Control _MakeCustomTooltip(string forText) => _impl.MakeCustomTooltip(forText);", src);
        StringAssert.Contains("public override string _GetTooltip(Godot.Vector2 atPosition) => _impl.GetTooltip(atPosition);", src);
    }

    [Test]
    public void Forwards_Draw_For_CanvasItem_Derived()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            $"  [GodotScript(ClassName=\"Painter\", BaseTypeName=\"{KnownGodot.Node2D}\")]",
            "  public class PainterImpl {",
            "    public void Ready(){}",
            "    public void Draw(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node2D).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "PainterImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);

        var path = Directory.EnumerateFiles(outDir, "Painter.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Painter.cs not generated");
        var src = File.ReadAllText(path!);

        StringAssert.Contains("public override void _Draw() => _impl.Draw();", src);
    }
}
