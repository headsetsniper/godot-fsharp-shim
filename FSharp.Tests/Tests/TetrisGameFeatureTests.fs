namespace FSharp.Tests

open System
open System.Threading.Tasks
open GdUnit4
open Godot

[<TestSuite; RequireGodotRuntime>]
type TetrisGameFeatureTests() =
    let mutable runner: ISceneRunner option = None

    let stringName (value: string) = new StringName(value)

    let requireRunner () =
        match runner with
        | Some value -> value
        | None -> failwith "Scene runner not initialized."

    let getRoot (r: ISceneRunner) =
        let node = r.Scene() :?> Node2D
        Assertions.AssertThat(node <> null).IsTrue() |> ignore
        node

    let getBoard (root: Node2D) =
        let board = root.GetNodeOrNull<Control>("UIRoot/Board")
        Assertions.AssertThat(board <> null).IsTrue() |> ignore
        board

    let getDropTimer (root: Node2D) =
        let timer = root.GetNodeOrNull<Timer>("UIRoot/DropTimer")
        Assertions.AssertThat(timer <> null).IsTrue() |> ignore
        timer

    let readInt (board: Control) name = board.Get(stringName name).AsInt32()

    let readString (board: Control) name = board.Get(stringName name).AsString()

    let setBool (board: Control) name (value: bool) =
        board.Set(stringName name, Variant.CreateFrom(value)) |> ignore

    let setInt (board: Control) name (value: int) =
        board.Set(stringName name, Variant.CreateFrom(value)) |> ignore

    let rows encoded =
        if String.IsNullOrEmpty encoded then
            Array.empty
        else
            encoded.Split('|')

    let countOnes encoded =
        encoded |> Seq.filter (fun c -> c = '1') |> Seq.length

    let bottomOccupancy (board: Control) =
        let grid = readString board "GridEncoded"
        let rs = rows grid

        if rs.Length = 0 then
            Array.create 10 false
        else
            rs.[rs.Length - 1] |> Seq.map (fun c -> c = '1') |> Array.ofSeq

    [<BeforeTest>]
    member _.Setup() =
        let r = ISceneRunner.Load("res://Scenes/Tetris.tscn", true)
        r.MaximizeView()
        runner <- Some r

    [<AfterTest>]
    member _.Teardown() =
        runner |> Option.iter (fun r -> r.Dispose())
        runner <- None

    [<TestCase>]
    member _.``Spawns new block when previous locks``() : Task =
        task {
            let r = requireRunner ()
            let root = getRoot r
            let board = getBoard root
            let timer = getDropTimer root

            do! r.SimulateFrames(1u)
            timer.Stop()

            let initialFilled = readString board "GridEncoded" |> countOnes

            let mutable spawned = false
            let mutable steps = 0

            while not spawned && steps < 24 do
                timer.EmitSignal(Timer.SignalName.Timeout) |> ignore
                do! r.SimulateFrames(1u)

                let y = readInt board "CurrentY"
                let filled = readString board "GridEncoded" |> countOnes

                if y = 0 && filled > initialFilled then
                    spawned <- true

                steps <- steps + 1

            Assertions.AssertThat(spawned).IsTrue() |> ignore

            let finalFilled = readString board "GridEncoded" |> countOnes
            Assertions.AssertThat(finalFilled).IsGreater(initialFilled) |> ignore
        }

    [<TestCase>]
    member _.``Hard drop places piece on bottom then locks on next tick``() : Task =
        task {
            let r = requireRunner ()
            let root = getRoot r
            let board = getBoard root
            let timer = getDropTimer root

            do! r.SimulateFrames(1u)
            timer.Stop()

            let startY = readInt board "CurrentY"
            let filledBefore = readString board "GridEncoded" |> countOnes

            setBool board "HardDropRequested" true
            do! r.SimulateFrames(1u)
            let yAfterHardDrop = readInt board "CurrentY"

            timer.EmitSignal(Timer.SignalName.Timeout) |> ignore
            do! r.SimulateFrames(1u)

            let yAfterLock = readInt board "CurrentY"
            let filledAfter = readString board "GridEncoded" |> countOnes

            Assertions.AssertThat(yAfterHardDrop).IsGreater(startY + 1) |> ignore
            Assertions.AssertThat(yAfterLock).IsEqual(0) |> ignore
            Assertions.AssertThat(filledAfter).IsGreater(filledBefore) |> ignore
        }

    [<TestCase>]
    member _.``Bagging stores current piece``() : Task =
        task {
            let r = requireRunner ()
            let root = getRoot r
            let board = getBoard root
            let timer = getDropTimer root

            do! r.SimulateFrames(1u)
            timer.Stop()

            let curBefore = readString board "CurrentEncoded"
            let bagBefore = readString board "BagEncoded"

            setBool board "BagRequested" true
            do! r.SimulateFrames(1u)

            let bagAfter = readString board "BagEncoded"

            Assertions.AssertThat(bagBefore).IsEmpty() |> ignore
            Assertions.AssertThat(bagAfter).IsNotEmpty() |> ignore
            Assertions.AssertThat(bagAfter).IsEqual(curBefore) |> ignore
        }

    [<TestCase>]
    member _.``Restoring retrieves bagged piece``() : Task =
        task {
            let r = requireRunner ()
            let root = getRoot r
            let board = getBoard root
            let timer = getDropTimer root

            do! r.SimulateFrames(1u)
            timer.Stop()

            setBool board "BagRequested" true
            do! r.SimulateFrames(1u)

            let bagStored = readString board "BagEncoded"
            Assertions.AssertThat(bagStored).IsNotEmpty() |> ignore

            setBool board "BagRequested" true
            do! r.SimulateFrames(1u)

            let bagAfter = readString board "BagEncoded"
            let curAfter = readString board "CurrentEncoded"

            Assertions.AssertThat(bagAfter).IsEmpty() |> ignore
            Assertions.AssertThat(curAfter).IsEqual(bagStored) |> ignore
        }

    [<TestCase>]
    member _.``Clearing a line increases score and removes row``() : Task =
        task {
            let r = requireRunner ()
            let root = getRoot r
            let board = getBoard root
            let timer = getDropTimer root

            do! r.SimulateFrames(1u)
            timer.Stop()

            board.Set(stringName "TestPieceQueue", Variant.CreateFrom("O,I,I,I")) |> ignore
            setBool board "BagRequested" true
            do! r.SimulateFrames(1u)

            let scoreBefore = readInt board "Score"

            let rotateOnce () =
                task {
                    setBool board "RotateRequested" true
                    do! r.SimulateFrames(1u)
                }

            let chooseTargetX () =
                let current = readString board "CurrentEncoded"
                let rs = rows current

                if rs.Length = 0 then
                    readInt board "CurrentX"
                else
                    let h = rs.Length
                    let w = rs.[0].Length

                    let bottomCols =
                        Array.init w (fun x -> seq { h - 1 .. -1 .. 0 } |> Seq.exists (fun y -> rs.[y].[x] = '1'))

                    let occ = bottomOccupancy board

                    let rec tryPosition pos =
                        if pos > 10 - w then
                            readInt board "CurrentX"
                        else
                            let fits =
                                bottomCols
                                |> Array.mapi (fun i filled -> (not filled) || (not occ.[pos + i]))
                                |> Array.forall id

                            if fits then pos else tryPosition (pos + 1)

                    tryPosition 0

            let moveToX target =
                task {
                    let mutable guard = 40
                    let mutable arrived = false

                    while not arrived && guard > 0 do
                        let cx = readInt board "CurrentX"

                        if cx = target then
                            arrived <- true
                        else
                            setInt board "MoveX" (if cx < target then 1 else -1)
                            do! r.SimulateFrames(1u)
                            guard <- guard - 1

                    setInt board "MoveX" 0
                }

            let mutable cleared = false
            let mutable attempts = 0

            while not cleared && attempts < 10 do
                let mutable orientationTries = 2

                while not cleared && orientationTries > 0 do
                    let target = chooseTargetX ()
                    do! moveToX target

                    setBool board "HardDropRequested" true
                    do! r.SimulateFrames(1u)
                    timer.EmitSignal(Timer.SignalName.Timeout) |> ignore
                    do! r.SimulateFrames(1u)

                    let scoreNow = readInt board "Score"

                    if scoreNow > scoreBefore then
                        cleared <- true
                    else
                        do! rotateOnce ()
                        orientationTries <- orientationTries - 1

                attempts <- attempts + 1

            Assertions.AssertThat(cleared).IsTrue() |> ignore

            let finalScore = readInt board "Score"
            Assertions.AssertThat(finalScore).IsGreater(scoreBefore) |> ignore
        }
