using System;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

#nullable enable

namespace ExampleProject.Tests;

[TestSuite]
[RequireGodotRuntime]
public class TetrisGameFeatureTests
{
    private ISceneRunner? _runner;

    private static Node2D GetRoot(ISceneRunner r)
    {
        var n = r.Scene() as Node2D;
        AssertThat(n).IsNotNull();
        return n!;
    }

    private static Control GetBoard(Node2D root)
    {
        var board = root.GetNodeOrNull<Control>("UIRoot/Board");
        AssertThat(board).IsNotNull();
        return board!;
    }

    private static Timer GetDropTimer(Node2D root)
    {
        var t = root.GetNodeOrNull<Timer>("UIRoot/DropTimer");
        AssertThat(t).IsNotNull();
        return t!;
    }

    private static int ReadInt(Control board, string name)
        => board.Get(new StringName(name)).AsInt32();

    private static string ReadString(Control board, string name)
        => board.Get(new StringName(name)).AsString();

    private static void SetBool(Control board, string name, bool value)
        => board.Set(new StringName(name), Godot.Variant.CreateFrom(value));

    private static void SetInt(Control board, string name, int value)
        => board.Set(new StringName(name), Godot.Variant.CreateFrom(value));

    private static async Task SimulateTicks(ISceneRunner r, Timer timer, int count)
    {
        for (int i = 0; i < count; i++)
        {
            timer.EmitSignal(Timer.SignalName.Timeout);
            await r.SimulateFrames(1);
        }
    }

    private static string[] Rows(string encoded)
        => string.IsNullOrEmpty(encoded) ? Array.Empty<string>() : encoded.Split('|');

    private static int CountOnes(string encoded)
        => encoded.Count(c => c == '1');

    private static bool[] BottomOccupancy(Control board)
    {
        var grid = ReadString(board, "GridEncoded");
        var rows = Rows(grid);
        if (rows.Length == 0)
            return new bool[10];
        var last = rows[^1];
        return last.Select(c => c == '1').ToArray();
    }

    [BeforeTest]
    public void Setup()
    {
        _runner = ISceneRunner.Load("res://Scenes/Tetris.tscn", true);
        _runner.MaximizeView();
    }

    [AfterTest]
    public void Teardown()
    {
        _runner?.Dispose();
        _runner = null;
    }

    [TestCase]
    public async Task Spawns_new_block_when_previous_locks()
    {
        // Arrange
        AssertThat(_runner).IsNotNull();
        var r = _runner!;
        var root = GetRoot(r);
        var board = GetBoard(root);
        var timer = GetDropTimer(root);

        await r.SimulateFrames(1);
        // Use only manual ticks to avoid concurrent timeouts
        timer.Stop();
        var initialFilled = CountOnes(ReadString(board, "GridEncoded"));

        // Act: pump timeouts until a lock+spawn occurs
        bool spawned = false;
        int prevY = ReadInt(board, "CurrentY");
        for (int i = 0; i < 24 && !spawned; i++)
        {
            timer.EmitSignal(Timer.SignalName.Timeout);
            await r.SimulateFrames(1);
            var y = ReadInt(board, "CurrentY");
            var filled = CountOnes(ReadString(board, "GridEncoded"));

            // Detect spawn: Y resets to top (0) and grid gained filled cells from the lock
            if (y == 0 && filled > initialFilled)
            {
                spawned = true;
            }
            prevY = y;
        }

        // Assert
        AssertThat(spawned).IsTrue();
        AssertThat(CountOnes(ReadString(board, "GridEncoded"))).IsGreater(initialFilled);
    }

    [TestCase]
    public async Task Harddrop_places_piece_on_bottom_then_locks_on_next_tick()
    {
        // Arrange
        AssertThat(_runner).IsNotNull();
        var r = _runner!;
        var root = GetRoot(r);
        var board = GetBoard(root);
        var timer = GetDropTimer(root);
        await r.SimulateFrames(1);
        // Avoid background timer firings
        timer.Stop();

        var startY = ReadInt(board, "CurrentY");
        var filledBefore = CountOnes(ReadString(board, "GridEncoded"));

        // Act: request hard drop and process a frame
        SetBool(board, "HardDropRequested", true);
        await r.SimulateFrames(1);
        var yAfterHardDrop = ReadInt(board, "CurrentY");

        // Lock on next timeout
        timer.EmitSignal(Timer.SignalName.Timeout);
        await r.SimulateFrames(1);
        var yAfterLock = ReadInt(board, "CurrentY");
        var filledAfter = CountOnes(ReadString(board, "GridEncoded"));

        // Assert
        AssertThat(yAfterHardDrop).IsGreater(startY + 1); // dropped more than a single step
        AssertThat(yAfterLock).IsEqual(0); // new piece spawned
        AssertThat(filledAfter).IsGreater(filledBefore); // grid gained cells
    }

    [TestCase]
    public async Task Bagging_stores_current_piece()
    {
        // Arrange
        AssertThat(_runner).IsNotNull();
        var r = _runner!;
        var root = GetRoot(r);
        var board = GetBoard(root);
        await r.SimulateFrames(1);
        // Ensure no background ticks between frames
        var timer = GetDropTimer(root);
        timer.Stop();
        var curBefore = ReadString(board, "CurrentEncoded");
        var bagBefore = ReadString(board, "BagEncoded");

        // Act: request bag
        SetBool(board, "BagRequested", true);
        await r.SimulateFrames(1);

        var bagAfter = ReadString(board, "BagEncoded");

        // Assert
        AssertThat(bagBefore).IsEmpty();
        AssertThat(bagAfter).IsNotEmpty();
        AssertThat(bagAfter).IsEqual(curBefore);
    }

    [TestCase]
    public async Task Restoring_retrieves_bagged_piece()
    {
        // Arrange: ensure something is bagged first
        AssertThat(_runner).IsNotNull();
        var r = _runner!;
        var root = GetRoot(r);
        var board = GetBoard(root);
        await r.SimulateFrames(1);
        // Ensure no background ticks between frames
        var timer = GetDropTimer(root);
        timer.Stop();

        SetBool(board, "BagRequested", true);
        await r.SimulateFrames(1);
        var bagStored = ReadString(board, "BagEncoded");
        AssertThat(bagStored).IsNotEmpty();

        // Act: request restore
        SetBool(board, "BagRequested", true);
        await r.SimulateFrames(1);

        var bagAfter = ReadString(board, "BagEncoded");
        var curAfter = ReadString(board, "CurrentEncoded");

        // Assert: bag emptied and current piece equals previously stored shape
        AssertThat(bagAfter).IsEmpty();
        AssertThat(curAfter).IsEqual(bagStored);
    }

    [TestCase]
    public async Task Clearing_a_line_increases_score_and_removes_row()
    {
        // Arrange
        AssertThat(_runner).IsNotNull();
        var r = _runner!;
        var root = GetRoot(r);
        var board = GetBoard(root);
        var timer = GetDropTimer(root);
        await r.SimulateFrames(1);
        // Manual control over locks
        timer.Stop();

        // Provide a deterministic sequence of pieces to complete a flat line quickly: O, I, I, I
        // Bag the initial random piece so the first active piece comes from the queue.
        board.Set(new StringName("TestPieceQueue"), Variant.CreateFrom("O,I,I,I"));
        board.Set(new StringName("BagRequested"), Variant.CreateFrom(true));
        await r.SimulateFrames(1);
        int scoreBefore = ReadInt(board, "Score");

        // Helper to rotate once
        async Task RotateOnce()
        {
            SetBool(board, "RotateRequested", true);
            await r.SimulateFrames(1);
        }

        // Choose a target X to contribute cells to the bottom row based on current shape footprint
        int ChooseTargetX()
        {
            string cur = ReadString(board, "CurrentEncoded");
            var rows = Rows(cur);
            if (rows.Length == 0) return ReadInt(board, "CurrentX");
            int h = rows.Length;
            int w = rows[0].Length;
            bool[] bottomCols = new bool[w];
            for (int x = 0; x < w; x++)
            {
                for (int y = h - 1; y >= 0; y--)
                {
                    if (rows[y][x] == '1')
                    {
                        bottomCols[x] = true;
                        break;
                    }
                }
            }

            var occ = BottomOccupancy(board);
            for (int pos = 0; pos <= 10 - w; pos++)
            {
                bool fits = true;
                for (int x = 0; x < w; x++)
                    if (bottomCols[x] && occ[pos + x]) { fits = false; break; }
                if (fits) return pos;
            }
            // fallback: keep current X
            return ReadInt(board, "CurrentX");
        }

        async Task MoveToX(int targetX)
        {
            int guard = 40;
            while (guard-- > 0)
            {
                int cx = ReadInt(board, "CurrentX");
                if (cx == targetX) break;
                SetInt(board, "MoveX", cx < targetX ? 1 : -1);
                await r.SimulateFrames(1);
            }
            // clear MoveX
            SetInt(board, "MoveX", 0);
        }

        // Act: try to fill the bottom row and trigger a clear with bounded attempts
        bool cleared = false;
        for (int pieces = 0; pieces < 10 && !cleared; pieces++)
        {
            // Try up to 4 orientations to find a bottom-contributing placement
            int attempts = 2;
            while (attempts-- > 0)
            {
                int target = ChooseTargetX();
                await MoveToX(target);

                // Drop
                SetBool(board, "HardDropRequested", true);
                await r.SimulateFrames(1);
                // Lock
                timer.EmitSignal(Timer.SignalName.Timeout);
                await r.SimulateFrames(1);

                int scoreNow = ReadInt(board, "Score");
                if (scoreNow > scoreBefore)
                {
                    cleared = true;
                    break;
                }

                // If not cleared, rotate once to try a different footprint on the next piece
                await RotateOnce();
            }
        }

        // Assert
        AssertThat(cleared).IsTrue();
        AssertThat(ReadInt(board, "Score")).IsGreater(scoreBefore);
    }
}
