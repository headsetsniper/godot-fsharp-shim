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

[<GodotTool(ClassName = "TetrisUiBoard", BaseTypeName = "Godot.Control")>]
type TetrisUiBoardImpl() =
    let mutable nodeOpt: Control option = None
    let mutable node: Control = Unchecked.defaultof<_>
    let cols, rows = 10, 20
    let mutable grid: CellFlags[,] = Array2D.zeroCreate rows cols
    let rng = Random()
    let mutable score = 0
    let mutable curShape: bool[,] = Tetromino.shape Tetromino.Kind.I
    let mutable curX, curY = 3, 0
    let mutable cells: ColorRect[,] = Array2D.zeroCreate rows cols
    // request flags exposed as exported properties
    let mutable rotateRequested = false
    let mutable hardDropRequested = false
    let mutable bagRequested = false
    let mutable printedReady = false
    let mutable printedBuilt = false
    let mutable printedDrawOnce = false
    let mutable printedProcessOnce = false
    let mutable timeoutCount = 0
    let mutable gridReady = false
    let mutable useInternalRendering = true
    // bag state (UI moved to TetrisBagPreview)
    let mutable bagShape: bool[,] option = None
    // test hook: deterministic piece sequence
    let mutable testQueue: Tetromino.Kind[] = Array.empty
    let mutable testQueueIndex = 0

    let debug (_msg: string) = ()
    let warn (_msg: string) = ()

    // No custom Godot signals; we drive redraws directly on views

    interface IGdToolScript<Control> with
        member _.Node
            with get () =
                match nodeOpt with
                | Some n -> n
                | None -> raise (InvalidOperationException "Node not set")
            and set v =
                node <- v
                nodeOpt <- Some v

    [<NodePath(Path = "../TickRelay")>]
    member val TickRelay: Node = Unchecked.defaultof<_> with get, set

    [<ExportRange(8.0, 64.0, 1.0, true)>]
    member val CellSize: float32 = 24.0f with get, set

    member val MoveX: int = 0 with get, set

    [<Export>]
    member _.RotateRequested
        with get () = rotateRequested
        and set v = rotateRequested <- v

    [<Export>]
    member _.HardDropRequested
        with get () = hardDropRequested
        and set v = hardDropRequested <- v

    [<Export>]
    member _.BagRequested
        with get () = bagRequested
        and set v = bagRequested <- v

    [<Export>]
    member _.Score
        with get () = score
        and set (_: int) = ()

    [<Export>]
    member this.TestPieceQueue
        with get () =
            if testQueue.Length = 0 then
                ""
            else
                let names = testQueue |> Array.map (fun k -> k.ToString())
                String.Join(",", names)
        and set (value: string) =
            let parse (s: string) =
                match s.Trim().ToUpperInvariant() with
                | "I" -> Some Tetromino.Kind.I
                | "O" -> Some Tetromino.Kind.O
                | "T" -> Some Tetromino.Kind.T
                | "S" -> Some Tetromino.Kind.S
                | "Z" -> Some Tetromino.Kind.Z
                | "J" -> Some Tetromino.Kind.J
                | "L" -> Some Tetromino.Kind.L
                | _ -> None

            let tokens =
                if String.IsNullOrWhiteSpace value then
                    [||]
                else
                    value.Split([| ','; ';'; '\n'; '\r'; '\t' |], StringSplitOptions.RemoveEmptyEntries)

            testQueue <- tokens |> Array.choose parse
            testQueueIndex <- 0

    member private this.EnsureGrid() =
        // In editor, _Process can be scheduled before _Ready has injected Node.
        // Bail out until Node is available to avoid NREs.
        match nodeOpt with
        | None -> gridReady <- false
        | Some node ->
            if not useInternalRendering then
                gridReady <- true
                ()
            else
                let existing = node.GetNodeOrNull<GridContainer>(new NodePath "Grid")

                let gridContainer =
                    if isNull existing then
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

                    gridReady <- true
                else
                    // Map existing children in row-major order
                    for i in 0 .. gridContainer.GetChildCount() - 1 do
                        let y = i / cols
                        let x = i % cols
                        cells[y, x] <- gridContainer.GetChild(i) :?> ColorRect

                    gridReady <- true

                ()

    member private this.SyncCellSizes() =
        for y in 0 .. rows - 1 do
            for x in 0 .. cols - 1 do
                let c = cells[y, x]

                if not (obj.ReferenceEquals(c, null)) then
                    c.CustomMinimumSize <- Vector2(this.CellSize, this.CellSize)

        match nodeOpt with
        | Some n ->
            let gc = n.GetNodeOrNull<GridContainer>(new NodePath "Grid")

            if not (isNull gc) then
                gc.CustomMinimumSize <- Vector2(float32 cols * this.CellSize, float32 rows * this.CellSize)
        | None -> ()


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

    member private _.EncodeBag() =
        match bagShape with
        | None -> ""
        | Some shape ->
            let h = shape.GetLength 0
            let w = shape.GetLength 1

            let rows =
                [ for y in 0 .. h - 1 ->
                      let chars = [ for x in 0 .. w - 1 -> if shape[y, x] then '1' else '0' ]
                      new String(Array.ofList chars) ]

            String.Join("|", rows)

    [<Export>]
    member val BagEncoded: string = "" with get, set

    member private _.EncodeGrid() =
        let rows =
            [ for y in 0 .. rows - 1 ->
                  let chars =
                      [ for x in 0 .. cols - 1 -> if grid[y, x] <> CellFlags.Empty then '1' else '0' ]

                  new String(Array.ofList chars) ]

        String.Join("|", rows)

    [<Export>]
    member val GridEncoded: string = "" with get, set

    member private _.EncodeCurrent() =
        let h = curShape.GetLength 0
        let w = curShape.GetLength 1

        let rows =
            [ for y in 0 .. h - 1 ->
                  let chars = [ for x in 0 .. w - 1 -> if curShape[y, x] then '1' else '0' ]
                  new String(Array.ofList chars) ]

        String.Join("|", rows)

    [<Export>]
    member val CurrentEncoded: string = "" with get, set

    [<Export>]
    member val CurrentX: int = 0 with get, set

    [<Export>]
    member val CurrentY: int = 0 with get, set

    member private this.PushStateToExports() =
        this.GridEncoded <- this.EncodeGrid()
        this.CurrentEncoded <- this.EncodeCurrent()
        this.CurrentX <- curX
        this.CurrentY <- curY
        this.BagEncoded <- this.EncodeBag()

    member private this.DrawUi() =
        match nodeOpt with
        | None -> ()
        | Some _ ->
            if not gridReady then
                this.EnsureGrid()

            if gridReady then
                if not printedDrawOnce then
                    printedDrawOnce <- true

                if useInternalRendering then
                    this.SyncCellSizes()
                    this.PaintBase()
                    this.PaintOverlay()

                this.PushStateToExports()
                // Also nudge external views directly
                match nodeOpt with
                | Some n ->
                    let gv = n.GetNodeOrNull<Control>(new NodePath "GridView")

                    if not (isNull gv) then
                        gv.QueueRedraw()

                    let bp = n.GetNodeOrNull<Control>(new NodePath "../BagPreview")

                    if not (isNull bp) then
                        bp.QueueRedraw()
                | None -> ()

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
        if testQueue.Length > 0 then
            let kind = testQueue[testQueueIndex % testQueue.Length]
            testQueueIndex <- testQueueIndex + 1
            curShape <- Tetromino.shape kind
        else
            curShape <- Tetromino.shape (Tetromino.all.[rng.Next(Tetromino.all.Length)])

        curX <- (cols / 2) - 1
        curY <- 0

        if this.CanPlace(curShape, curX, curY) |> not then
            ()

    member this.Ready() =
        if not printedReady then
            printedReady <- true

        // Ensure _Process runs both in editor and at runtime
        nodeOpt
        |> Option.iter (fun n ->
            n.ProcessMode <- Node.ProcessModeEnum.Always
            n.SetProcess true
            n.QueueRedraw())

        // No custom signals; inputs are exposed via exported properties

        // Disable internal rendering if an external GridView is present
        useInternalRendering <-
            match nodeOpt with
            | None -> true
            | Some n -> isNull (n.GetNodeOrNull<Control>(new NodePath "GridView"))

        if useInternalRendering then
            this.EnsureGrid()
        else
            gridReady <- true
        // Do not tint the board; views draw their own backgrounds

        this.Clear()
        this.SpawnNewPiece()
        this.DrawUi()
        // Push exports and nudge initial redraws
        this.PushStateToExports()
        this.DrawUi()

        // Connect to TickRelay.Tick after ensuring signal exists
        let tr = this.TickRelay

        if obj.ReferenceEquals(tr, null) then
            ()
        else
            let tick = new StringName "Tick"

            if not (tr.HasSignal tick) then
                tr.AddUserSignal tick

            let err =
                tr.Connect(tick, Callable.From(new System.Action(fun () -> this.OnTimeout())))

            if err <> Error.Ok && err <> Error.AlreadyInUse then
                ()

    member _.EnterTree() = ()

    member this.OnTimeout() =
        timeoutCount <- timeoutCount + 1

        if timeoutCount <= 3 then
            ()

        if this.CanPlace(curShape, curX, curY + 1) then
            curY <- curY + 1
        else
            this.Lock(curShape, curX, curY)
            let cleared = this.ClearLines()

            if cleared > 0 then
                score <- score + (cleared * 100)

            this.SpawnNewPiece()
            this.PushStateToExports()

        this.DrawUi()

    member this.Process(_delta: double) =
        match nodeOpt with
        | None -> () // wait for _Ready to inject Node
        | Some node ->
            if not printedProcessOnce && Engine.IsEditorHint() then
                printedProcessOnce <- true

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
            this.PushStateToExports()

            // Bag mechanic: Shift toggles store or retrieve
            if bagRequested then
                match bagShape with
                | None ->
                    // Store current piece, spawn a new one
                    bagShape <- Some curShape
                    this.SpawnNewPiece()
                    this.PushStateToExports()
                | Some stored ->
                    // Retrieve stored piece and clear bag; no swapping
                    curShape <- stored
                    bagShape <- None
                    curX <- (cols / 2) - 1
                    curY <- 0

                    if not (this.CanPlace(curShape, curX, curY)) then
                        this.SpawnNewPiece()

                    this.PushStateToExports()

            this.MoveX <- 0
            rotateRequested <- false
            hardDropRequested <- false
            bagRequested <- false

            this.DrawUi()

            if Engine.IsEditorHint() then
                node.QueueRedraw()
