using System.Threading.Tasks;
using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ExampleProject.Tests;

// Basic smoke tests to validate GdUnit wiring and Tick/Drop behavior
[TestSuite]
public partial class TetrisTickTests : GodotObject
{
    // Contract
    // - Loads the Tetris scene from res://Scenes/Tetris.tscn
    // - Ensures TickRelay and DropTimer exist
    // - Simulates a few timers to check piece falls (CurrentY increases)

    private static PackedScene LoadTetrisScene()
    {
        var scene = (PackedScene)ResourceLoader.Load("res://Scenes/Tetris.tscn");
        AssertThat(scene).IsNotNull();
        return scene;
    }

    [TestCase]
    public void Sanity_checks()
    {
        AssertThat(1 + 1).IsEqual(2);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task Tick_lets_the_block_fall_down()
    {
        // Arrange: load and instantiate the Tetris scene
        var scene = LoadTetrisScene();
        var root = scene.Instantiate<Node2D>();
        AssertThat(root).IsNotNull();

        // We add it to the scene tree to allow timers/signals to work
        var tree = Engine.GetMainLoop() as SceneTree;
        AssertThat(tree).IsNotNull();
        tree!.Root.AddChild(root);

        // Find UI Board and related nodes
        var board = root.GetNodeOrNull<Control>("UIRoot/Board");
        AssertThat(board).IsNotNull();
        var tickRelay = root.GetNodeOrNull<Node>("UIRoot/TickRelay");
        AssertThat(tickRelay).IsNotNull();
        var timer = root.GetNodeOrNull<Timer>("UIRoot/DropTimer");
        AssertThat(timer).IsNotNull();

        // Ensure nodes have emitted Ready and connections are established
        await ToSignal(board, Node.SignalName.Ready);
        await ToSignal(tickRelay, Node.SignalName.Ready);
        await ToSignal(timer, Node.SignalName.Ready);
        // Give the scene one more frame for shim wiring
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        // Capture starting Y export from the board (exposed via shim)
        var startY = (int)board.Get("CurrentY");

        // Act: drive ticks via the Timer timeout (full wiring path)
        timer.Stop();
        for (var i = 0; i < 6; i++)
        {
            timer.EmitSignal(Timer.SignalName.Timeout);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        var endY = (int)board.Get("CurrentY");

        // Assert: piece should have fallen by at least 1
        AssertThat(endY).IsGreater(startY);

        // Cleanup
        root.QueueFree();
    }
}