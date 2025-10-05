namespace Game

open System
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<AutoOpen>]
module internal TetrisUiPipeline =
    type UiState =
        { Shape: bool[,]
          X: int
          Y: int
          MoveX: int
          Rotate: bool
          HardDrop: bool }

    let applyMove (canPlace: bool[,] -> int -> int -> bool) (s: UiState) =
        if s.MoveX = 0 then
            s
        else
            let nx = s.X + s.MoveX

            if canPlace s.Shape nx s.Y then
                { s with X = nx; MoveX = 0 }
            else
                { s with MoveX = 0 }

    let applyRotate (canPlace: bool[,] -> int -> int -> bool) (s: UiState) =
        if not s.Rotate then
            s
        else
            let r = Tetromino.rotateCW s.Shape

            if canPlace r s.X s.Y then
                { s with Shape = r; Rotate = false }
            else
                { s with Rotate = false }

    let applyHardDrop (canPlace: bool[,] -> int -> int -> bool) (s: UiState) =
        if not s.HardDrop then
            s
        else
            let mutable y = s.Y

            while canPlace s.Shape s.X (y + 1) do
                y <- y + 1

            { s with Y = y; HardDrop = false }

[<GodotScript(ClassName = "TetrisUiBoard", BaseTypeName = "Godot.Control", Tool = true)>]
type TetrisUiBoardImpl() =
    let mutable node: Control = Unchecked.defaultof<_>
    let cols, rows = 10, 20
    let mutable grid: CellFlags[,] = Array2D.zeroCreate rows cols
    let rng = Random()
    let mutable score = 0
    let mutable curShape: bool[,] = Tetromino.shape Tetromino.Kind.I
    let mutable curX, curY = 3, 0
    let mutable cells: ColorRect[,] = Array2D.zeroCreate rows cols
    // request flags set via signals
    let mutable rotateRequested = false
    let mutable hardDropRequested = false
    let mutable printedReady = false
    let mutable printedBuilt = false
    let mutable printedDrawOnce = false
    let mutable printedProcessOnce = false
    let mutable timeoutCount = 0
    let mutable gridReady = false

    let debug (msg: string) = GD.Print $"[TetrisUiBoard] {msg}"

    interface IGdScript<Control> with
        member _.Node
            with get () = node
            and set v = node <- v

    [<ExportRange(8.0, 64.0, 1.0, true)>]
    member val CellSize: float32 = 24.0f with get, set

    member val MoveX: int = 0 with get, set

    member _.Score
        with get () = score
        and set (_: int) = ()

    member private this.EnsureGrid() =
        let existing = node.GetNodeOrNull<GridContainer>(new NodePath "Grid")

        let gridContainer =
            if isNull existing then
                debug ("Creating GridContainer (editor=" + string (Engine.IsEditorHint()) + ")")
                let g = new GridContainer()
                g.Name <- new StringName "Grid"
                g.Columns <- cols
                g.SizeFlagsHorizontal <- Control.SizeFlags.Fill ||| Control.SizeFlags.Expand
                g.SizeFlagsVertical <- Control.SizeFlags.Fill ||| Control.SizeFlags.Expand
                g.CustomMinimumSize <- Vector2(float32 cols * this.CellSize, float32 rows * this.CellSize)
                node.AddChild(g)
                g
            else
                existing

        gridContainer.SetAnchorsPreset Control.LayoutPreset.FullRect
        gridContainer.OffsetLeft <- 0f
        gridContainer.OffsetTop <- 0f
        gridContainer.OffsetRight <- 0f
        gridContainer.OffsetBottom <- 0f

        // Clear and rebuild children once on first init or mismatch
        if gridContainer.GetChildCount() <> cols * rows then
            while gridContainer.GetChildCount() > 0 do
                let ch = gridContainer.GetChild(0)
                gridContainer.RemoveChild(ch)
                ch.QueueFree()

            for y in 0 .. rows - 1 do
                for x in 0 .. cols - 1 do
                    let cr = new ColorRect()
                    cr.CustomMinimumSize <- Vector2(this.CellSize, this.CellSize)
                    cr.SizeFlagsHorizontal <- Control.SizeFlags.Fill ||| Control.SizeFlags.Expand
                    cr.SizeFlagsVertical <- Control.SizeFlags.Fill ||| Control.SizeFlags.Expand
                    cr.Color <- Colors.Transparent
                    gridContainer.AddChild(cr)
                    cells[y, x] <- cr

            if not printedBuilt then
                printedBuilt <- true
                debug $"Initialized grid {cols}x{rows} at path {node.GetPath()} with CellSize={this.CellSize}"

            gridReady <- true
        else
            // Map existing children in row-major order
            for i in 0 .. gridContainer.GetChildCount() - 1 do
                let y = i / cols
                let x = i % cols
                cells[y, x] <- gridContainer.GetChild(i) :?> ColorRect

            gridReady <- true

    member private this.SyncCellSizes() =
        for y in 0 .. rows - 1 do
            for x in 0 .. cols - 1 do
                let c = cells[y, x]

                if not (obj.ReferenceEquals(c, null)) then
                    c.CustomMinimumSize <- Vector2(this.CellSize, this.CellSize)

        match node.GetNodeOrNull<GridContainer>(new NodePath "Grid") with
        | null -> ()
        | gc -> gc.CustomMinimumSize <- Vector2(float32 cols * this.CellSize, float32 rows * this.CellSize)

    member private _.PaintBase() =
        for y in 0 .. rows - 1 do
            for x in 0 .. cols - 1 do
                let filled = grid[y, x] <> CellFlags.Empty
                let c = cells[y, x]

                if not (obj.ReferenceEquals(c, null)) then
                    c.Color <-
                        if filled then
                            Colors.LightSkyBlue
                        else
                            Color(0.25f, 0.25f, 0.25f, 0.25f)

    member private _.PaintOverlay() =
        let h = curShape.GetLength 0
        let w = curShape.GetLength 1

        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                if curShape[y, x] then
                    let gx = curX + x
                    let gy = curY + y

                    if gx >= 0 && gx < cols && gy >= 0 && gy < rows then
                        let c = cells[gy, gx]

                        if not (obj.ReferenceEquals(c, null)) then
                            c.Color <- Color(1.0f, 0.4f, 0.2f)

    member private this.DrawUi() =
        if not gridReady then
            this.EnsureGrid()

        if not gridReady then
            ()
        else
            if not printedDrawOnce then
                printedDrawOnce <- true
                debug "DrawUi() invoked"

            this.SyncCellSizes()
            this.PaintBase()
            this.PaintOverlay()

    member _.Clear() = grid <- Array2D.zeroCreate rows cols

    member _.CanPlace(shape: bool[,], ox: int, oy: int) =
        let h = shape.GetLength 0
        let w = shape.GetLength 1

        let inside x y =
            x >= 0 && x < cols && y >= 0 && y < rows

        seq {
            for y in 0 .. h - 1 do
                for x in 0 .. w - 1 do
                    if shape[y, x] then
                        let gx, gy = ox + x, oy + y

                        if not (inside gx gy) then
                            yield false
                        elif grid[gy, gx] <> CellFlags.Empty then
                            yield false
        }
        |> Seq.isEmpty

    member _.Lock(shape: bool[,], ox: int, oy: int) =
        let h = shape.GetLength 0
        let w = shape.GetLength 1

        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                if shape[y, x] then
                    grid[oy + y, ox + x] <- CellFlags.Filled

    member _.ClearLines() =
        let isFull r =
            seq { for x in 0 .. cols - 1 -> grid[r, x] <> CellFlags.Empty } |> Seq.forall id

        let mutable dst = rows - 1
        let mutable cleared = 0

        for y in [ rows - 1 .. -1 .. 0 ] do
            if not (isFull y) then
                for x in 0 .. cols - 1 do
                    grid[dst, x] <- grid[y, x]

                dst <- dst - 1
            else
                cleared <- cleared + 1

        for y in 0..dst do
            for x in 0 .. cols - 1 do
                grid[y, x] <- CellFlags.Empty

        cleared

    member this.SpawnNewPiece() =
        curShape <- Tetromino.shape (Tetromino.all.[rng.Next(Tetromino.all.Length)])
        curX <- (cols / 2) - 1
        curY <- 0

        if this.CanPlace(curShape, curX, curY) |> not then
            ()

    member this.Ready() =
        if not printedReady then
            printedReady <- true
            debug ("Ready() (editor=" + string (Engine.IsEditorHint()) + ")")

        if Engine.IsEditorHint() then
            // Ensure _Process runs in the editor
            node.ProcessMode <- Node.ProcessModeEnum.Always
            node.SetProcess true
            node.QueueRedraw()

        // Define and connect user signals for inputs
        // Ensure signals exist even if no external connections
        node.AddUserSignal(new StringName "RequestRotate")
        node.AddUserSignal(new StringName "RequestHardDrop")
        // Self-connect to flip request flags, processed in _Process
        node.Connect(new StringName "RequestRotate", Callable.From(fun () -> rotateRequested <- true))
        |> ignore

        node.Connect(new StringName "RequestHardDrop", Callable.From(fun () -> hardDropRequested <- true))
        |> ignore

        this.EnsureGrid()
        // Set a subtle background so the board bounds are visible
        node.Modulate <- Color(0.1f, 0.1f, 0.1f, 1.0f)

        this.Clear()
        this.SpawnNewPiece()
        this.DrawUi()

    member this.EnterTree() =
        // Node is injected in _Ready; avoid touching 'node' here
        debug ("EnterTree() (editor=" + string (Engine.IsEditorHint()) + ")")

    [<AutoConnect("../DropTimer", "timeout")>]
    member this.OnTimeout() =
        timeoutCount <- timeoutCount + 1

        if timeoutCount <= 3 then
            debug $"OnTimeout #{timeoutCount}"

        if this.CanPlace(curShape, curX, curY + 1) then
            curY <- curY + 1
        else
            this.Lock(curShape, curX, curY)
            let cleared = this.ClearLines()

            if cleared > 0 then
                score <- score + (cleared * 100)

            this.SpawnNewPiece()

        this.DrawUi()

    member this.Process(_delta: double) =
        if not printedProcessOnce && Engine.IsEditorHint() then
            printedProcessOnce <- true
            debug "Process() (editor)"

        let canPlace shape x y = this.CanPlace(shape, x, y)

        let state: UiState =
            { Shape = curShape
              X = curX
              Y = curY
              MoveX = this.MoveX
              Rotate = rotateRequested
              HardDrop = hardDropRequested }

        let updated =
            state |> applyMove canPlace |> applyRotate canPlace |> applyHardDrop canPlace

        curShape <- updated.Shape
        curX <- updated.X
        curY <- updated.Y
        this.MoveX <- 0
        rotateRequested <- false
        hardDropRequested <- false

        this.DrawUi()

        if Engine.IsEditorHint() then
            node.QueueRedraw()

    member this.Draw() =
        if Engine.IsEditorHint() then
            // This confirms CanvasItem draw is called in-editor when queued
            if not printedDrawOnce then
                debug "Draw() (editor)"
