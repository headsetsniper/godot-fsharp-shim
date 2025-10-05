namespace Game

open System
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<AutoOpen>]
module internal TetrisBoardPipeline =
    type BoardState =
        { Shape: bool[,]
          X: int
          Y: int
          MoveX: int
          Rotate: bool
          HardDrop: bool }

    let applyMove (canPlace: bool[,] -> int -> int -> bool) (s: BoardState) =
        if s.MoveX = 0 then
            s
        else
            let nx = s.X + s.MoveX

            if canPlace s.Shape nx s.Y then
                { s with X = nx; MoveX = 0 }
            else
                { s with MoveX = 0 }

    let applyRotate (canPlace: bool[,] -> int -> int -> bool) (s: BoardState) =
        if not s.Rotate then
            s
        else
            let r = Tetromino.rotateCW s.Shape

            if canPlace r s.X s.Y then
                { s with Shape = r; Rotate = false }
            else
                { s with Rotate = false }

    let applyHardDrop (canPlace: bool[,] -> int -> int -> bool) (s: BoardState) =
        if not s.HardDrop then
            s
        else
            let mutable y = s.Y

            while canPlace s.Shape s.X (y + 1) do
                y <- y + 1

            { s with Y = y; HardDrop = false }

[<GodotScript(ClassName = "TetrisBoard", BaseTypeName = "Godot.Node2D", Tool = true)>]
type TetrisBoardImpl() =
    let mutable node: Node2D = Unchecked.defaultof<_>
    let cols, rows = 10, 20
    let mutable grid: CellFlags[,] = Array2D.zeroCreate rows cols
    let mutable cellSize = 24.0f
    let rng = Random()
    let mutable score = 0
    let mutable curShape: bool[,] = Tetromino.shape Tetromino.Kind.I
    let mutable curX, curY = 3, 0

    interface IGdScript<Node2D> with
        member _.Node
            with get () = node
            and set v = node <- v

    member _.Cols = cols
    member _.Rows = rows

    [<ExportRange(8.0, 64.0, 1.0, true)>]
    member _.CellSize
        with get () = cellSize
        and set (v: float32) = cellSize <- v

    member val MoveX: int = 0 with get, set
    member val RotateRequested: bool = false with get, set
    member val HardDrop: bool = false with get, set

    member _.Score
        with get () = score
        and set (_: int) = ()

    [<Preload("res://icon.svg", Required = false)>]
    member val TileTexture: Texture2D option = None with get, set

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

    member this.DrawPiece(shape: bool[,], ox: int, oy: int, color: Color) =
        let h = shape.GetLength 0
        let w = shape.GetLength 1

        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                if shape[y, x] then
                    let px = float32 (ox + x) * cellSize
                    let py = float32 (oy + y) * cellSize

                    match this.TileTexture with
                    | None -> node.DrawRect(Rect2(Vector2(px, py), Vector2(cellSize, cellSize)), color, true)
                    | Some tex ->
                        node.DrawTextureRect(tex, Rect2(Vector2(px, py), Vector2(cellSize, cellSize)), tile = true)

    member this.DrawBoard() =
        for y in 0 .. rows - 1 do
            for x in 0 .. cols - 1 do
                if grid[y, x] <> CellFlags.Empty then
                    let px = float32 x * cellSize
                    let py = float32 y * cellSize

                    match this.TileTexture with
                    | None ->
                        node.DrawRect(Rect2(Vector2(px, py), Vector2(cellSize, cellSize)), Colors.LightSkyBlue, true)
                    | Some tex ->
                        node.DrawTextureRect(tex, Rect2(Vector2(px, py), Vector2(cellSize, cellSize)), tile = true)

    member this.GetPixelSize() =
        Vector2(float32 cols * cellSize, float32 rows * cellSize)

    member this.SpawnNewPiece() =
        curShape <- Tetromino.shape Tetromino.all.[rng.Next(Tetromino.all.Length)]
        curX <- (cols / 2) - 1
        curY <- 0

        if this.CanPlace(curShape, curX, curY) |> not then
            ()

    member this.Ready() =
        this.Clear()
        this.SpawnNewPiece()
        node.QueueRedraw()

    member this.Draw() =
        this.DrawBoard()
        this.DrawPiece(curShape, curX, curY, Color(1.0f, 0.4f, 0.2f))

    [<AutoConnect("../DropTimer", "timeout")>]
    member this.OnTimeout() =
        if this.CanPlace(curShape, curX, curY + 1) then
            curY <- curY + 1
        else
            this.Lock(curShape, curX, curY)
            let cleared = this.ClearLines()

            if cleared > 0 then
                score <- score + (cleared * 100)

            this.SpawnNewPiece()

        node.QueueRedraw()

    // Pipeline-friendly state get/set at the same level of abstraction
    member private this.GetState() : BoardState =
        { Shape = curShape
          X = curX
          Y = curY
          MoveX = this.MoveX
          Rotate = this.RotateRequested
          HardDrop = this.HardDrop }

    member private this.SetState(s: BoardState) =
        curShape <- s.Shape
        curX <- s.X
        curY <- s.Y
        this.MoveX <- 0
        this.RotateRequested <- false
        this.HardDrop <- false

    member this.Process(_delta: double) =
        let canPlace shape x y = this.CanPlace(shape, x, y)

        this.GetState()
        |> applyMove canPlace
        |> applyRotate canPlace
        |> applyHardDrop canPlace
        |> this.SetState

        node.QueueRedraw()
