using System;
using System.Threading.Tasks;
using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

#nullable enable

namespace ExampleProject.Tests;

[TestSuite]
[RequireGodotRuntime]
public class TetrisTickTests
{
    private ISceneRunner? _runner;
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

    [BeforeTest]
    public void Setup()
    {
        _runner = ISceneRunner.Load("res://Scenes/Tetris.tscn", true);

        // In the editor this helps to ensure proper processing/input; harmless in headless
        _runner.MaximizeView();
    }

    [AfterTest]
    public void Teardown()
    {
        _runner?.Dispose();
        _runner = null;
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task Tick_lets_the_block_fall_down()
    {
        // Arrange: access the instantiated scene via the runner
        AssertThat(_runner).IsNotNull();
        var runner = _runner!;
        var root = runner.Scene() as Node2D;
        AssertThat(root).IsNotNull();
        if (root == null) return;

        // Ensure a stable starting point
        await runner.SimulateFrames(1);

        Timer? dropTimer = null;

        try
        {
            var board = root.GetNodeOrNull<Control>("UIRoot/Board");
            AssertThat(board).IsNotNull();
            var tickRelay = root.GetNodeOrNull<Node>("UIRoot/TickRelay");
            AssertThat(tickRelay).IsNotNull();
            dropTimer = root.GetNodeOrNull<Timer>("UIRoot/DropTimer");
            AssertThat(dropTimer).IsNotNull();
            var timer = dropTimer!;

            await runner.SimulateFrames(1);

            var startY = (int)board.Get("CurrentY");

            timer.Stop();
            for (var i = 0; i < 3; i++)
            {
                timer.EmitSignal(Timer.SignalName.Timeout);
                await runner.SimulateFrames(1);
            }

            var endY = (int)board.Get("CurrentY");

            AssertThat(endY).IsGreater(startY);
        }
        finally
        {
            dropTimer?.Stop();
        }
    }
}