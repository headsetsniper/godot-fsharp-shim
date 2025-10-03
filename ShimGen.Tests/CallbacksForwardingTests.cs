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
    [OneTimeSetUp]
    public void BeforeAll()
    {
        FsBatchComponent.BuildForFixture(typeof(CallbacksForwardingTests));
    }

    [OneTimeTearDown]
    public void AfterAll()
    {
        FsBatchComponent.CleanupForFixture(typeof(CallbacksForwardingTests));
    }

    [Test]
    [FsCase("Callbacks", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Callbacks", BaseTypeName="Godot.Node")>]
type CallbacksImpl() =
    member _.PhysicsProcess(delta: double) = ()
    member _.Input(e: InputEvent) = ()
    member _.UnhandledInput(e: InputEvent) = ()
    member _.Notification(what: int64) = ()
    member _.Ready() = ()
""")]
    public void Forwards_PhysicsProcess_Input_UnhandledInput_Notification()
    {
        var outDir = FsBatch.GetOutDir<CallbacksForwardingTests>();
        var path = Directory.EnumerateFiles(outDir!, "Callbacks.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Callbacks.cs not generated");
        var src = File.ReadAllText(path!);

        StringAssert.Contains("public override void _PhysicsProcess(double delta) => _impl.PhysicsProcess(delta);", src);
        StringAssert.Contains("public override void _Input(Godot.InputEvent @event) => _impl.Input(@event);", src);
        StringAssert.Contains("public override void _UnhandledInput(Godot.InputEvent @event) => _impl.UnhandledInput(@event);", src);
        StringAssert.Contains("public override void _Notification(long what) => _impl.Notification(what);", src);
    }

    [Test]
    [FsCase("UI", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="UI", BaseTypeName="Godot.Control")>]
type UiImpl() =
    member _.Ready() = ()
    member _.GuiInput(e: InputEvent) = ()
    member _.ShortcutInput(e: InputEvent) = ()
    member _.UnhandledKeyInput(e: InputEvent) = ()
    member _.CanDropData(p: Vector2, v: Variant) : bool = true
    member _.DropData(p: Vector2, v: Variant) = ()
    member _.GetDragData(p: Vector2) : obj = obj()
    member _.HasPoint(p: Vector2) : bool = true
    member _.GetMinimumSize() : Vector2 = new Vector2()
    member _.MakeCustomTooltip(s: string) : Control = new Control()
    member _.GetTooltip(p: Vector2) : string = "tip"
""")]
    public void Forwards_Control_Ui_And_DragDrop_Callbacks()
    {
        var outDir = FsBatch.GetOutDir<CallbacksForwardingTests>();
        var path = Directory.EnumerateFiles(outDir!, "UI.cs", SearchOption.AllDirectories).FirstOrDefault();
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
    [FsCase("Painter", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Painter", BaseTypeName="Godot.Node2D")>]
type PainterImpl() =
    member _.Ready() = ()
    member _.Draw() = ()
""")]
    public void Forwards_Draw_For_CanvasItem_Derived()
    {
        var outDir = FsBatch.GetOutDir<CallbacksForwardingTests>();
        var path = Directory.EnumerateFiles(outDir!, "Painter.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Painter.cs not generated");
        var src = File.ReadAllText(path!);

        StringAssert.Contains("public override void _Draw() => _impl.Draw();", src);
    }
}
