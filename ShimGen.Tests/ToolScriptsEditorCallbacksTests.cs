using System.IO;
using System.Linq;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.Annotations;
using Headsetsniper.Godot.FSharp.ShimGen;

namespace ShimGen.Tests;

[TestFixture]
public class ToolScriptsEditorCallbacksTests
{
    [OneTimeSetUp]
    public void BeforeAll()
    {
        FsBatchComponent.BuildForFixture(typeof(ToolScriptsEditorCallbacksTests));
    }

    [OneTimeTearDown]
    public void AfterAll()
    {
        FsBatchComponent.CleanupForFixture(typeof(ToolScriptsEditorCallbacksTests));
    }

    [Test]
    [FsCase("ToolControl", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="ToolControl", BaseTypeName="Godot.Control", Tool=true)>]
type ToolControlImpl() =
    member _.Ready() = ()
    member _.GuiInput(e: InputEvent) = ()
    member _.ShortcutInput(e: InputEvent) = ()
""")]
    public void Emits_Tool_Attribute_And_Forwards_Control_Callbacks()
    {
        var outDir = FsBatch.GetOutDir<ToolScriptsEditorCallbacksTests>();
        var path = Directory.EnumerateFiles(outDir!, "ToolControl.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "ToolControl.cs not generated");
        var src = File.ReadAllText(path!);

        StringAssert.Contains("[Tool]", src);
        StringAssert.Contains("public override void _GuiInput(Godot.InputEvent @event) => _impl.GuiInput(@event);", src);
        StringAssert.Contains("public override void _ShortcutInput(Godot.InputEvent @event) => _impl.ShortcutInput(@event);", src);
    }
}
