using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.Annotations;

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
            "  [GodotScript(ClassName=\"Callbacks\", BaseTypeName=\"Godot.Node\")]",
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
}
