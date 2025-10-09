namespace Game

open System
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotTool(ClassName = "TetrisGridView", BaseTypeName = "Godot.Control")>]
type TetrisGridViewImpl() =
    let mutable nodeOpt: Control option = None
    let mutable printedReady = false
    let mutable printedDraw = false

    interface IGdScript<Control> with
        member _.Node
            with get () =
                match nodeOpt with
                | Some n -> n
                | None -> raise (InvalidOperationException "Node not set")
            and set v = nodeOpt <- Some v

    [<OptionalNodePath(Path = "../")>]
    member val BoardOpt: Node option = None with get, set

    [<ExportRange(8.0, 64.0, 1.0, true)>]
    member val CellSize: float32 = 24.0f with get, set

    member _.Ready() =
        nodeOpt |> Option.iter (fun n -> n.QueueRedraw())

    member this.Draw() =
        match nodeOpt, this.BoardOpt with
        | Some node, Some board ->
            if not printedReady then
                printedReady <- true

            let cs = this.CellSize
            let inset = MathF.Min(cs * 0.18f, 3.0f)
            let border = MathF.Max(1.0f, MathF.Min(2.0f, cs * 0.06f))
            let bgPanel = Colors.AliceBlue
            let gridLine = Colors.LightGray
            let cellBg = Colors.DarkGray
            let cellFilled = Colors.LightSkyBlue
            let overlayColor = Colors.Orange

            let draw x y (c: Color) =
                node.DrawRect(Rect2(float32 x * cs, float32 y * cs, cs, cs), c, true)

            let drawInset x y (c: Color) =
                let ox = float32 x * cs + inset
                let oy = float32 y * cs + inset
                let sz = cs - (inset * 2.0f)

                if sz > 0.0f then
                    node.DrawRect(Rect2(ox, oy, sz, sz), c, true)

            let drawBorder w h (c: Color) (th: single) =
                let sz = Vector2(float32 w * cs, float32 h * cs)
                let p0 = Vector2(0f, 0f)
                let p1 = Vector2(sz.X, 0f)
                let p2 = Vector2(sz.X, sz.Y)
                let p3 = Vector2(0f, sz.Y)
                node.DrawLine(p0, p1, c, th, true)
                node.DrawLine(p1, p2, c, th, true)
                node.DrawLine(p2, p3, c, th, true)
                node.DrawLine(p3, p0, c, th, true)

            let grid =
                try
                    board.Get(new StringName "GridEncoded").AsString()
                with _ ->
                    ""

            if not (String.IsNullOrEmpty grid) then
                let rows = grid.Split('|')
                let h = rows.Length
                let w = rows[0].Length
                node.CustomMinimumSize <- Vector2(float32 w * cs, float32 h * cs)

                if not printedDraw then
                    printedDraw <- true

                // Panel background
                node.DrawRect(Rect2(0f, 0f, float32 w * cs, float32 h * cs), bgPanel, true)

                for y in 0 .. h - 1 do
                    for x in 0 .. w - 1 do
                        let filled = rows[y][x] = '1'
                        draw x y cellBg

                        if filled then
                            drawInset x y cellFilled

                // Grid lines
                for x in 1 .. w - 1 do
                    let xPos = float32 x * cs
                    node.DrawLine(Vector2(xPos, 0f), Vector2(xPos, float32 h * cs), gridLine, 1.0f, true)

                for y in 1 .. h - 1 do
                    let yPos = float32 y * cs
                    node.DrawLine(Vector2(0f, yPos), Vector2(float32 w * cs, yPos), gridLine, 1.0f, true)

                // Outer border
                drawBorder w h (Color(1f, 1f, 1f, 0.12f)) border
            else
                // Fallback to a 10x20 board so the panel isn't empty before exports arrive
                let w, h = 10, 20
                node.CustomMinimumSize <- Vector2(float32 w * cs, float32 h * cs)
                node.DrawRect(Rect2(0f, 0f, float32 w * cs, float32 h * cs), bgPanel, true)

                for y in 0 .. h - 1 do
                    for x in 0 .. w - 1 do
                        draw x y cellBg

                for x in 1 .. w - 1 do
                    let xPos = float32 x * cs
                    node.DrawLine(Vector2(xPos, 0f), Vector2(xPos, float32 h * cs), gridLine, 1.0f, true)

                for y in 1 .. h - 1 do
                    let yPos = float32 y * cs
                    node.DrawLine(Vector2(0f, yPos), Vector2(float32 w * cs, yPos), gridLine, 1.0f, true)

                drawBorder w h (Color(1f, 1f, 1f, 0.12f)) border

            let cur =
                try
                    board.Get(new StringName "CurrentEncoded").AsString()
                with _ ->
                    ""

            if not (String.IsNullOrEmpty cur) then
                let r2 = cur.Split('|')
                let h2 = r2.Length
                let w2 = r2[0].Length
                let cx = board.Get(new StringName "CurrentX").AsInt32()
                let cy = board.Get(new StringName "CurrentY").AsInt32()

                for y in 0 .. h2 - 1 do
                    for x in 0 .. w2 - 1 do
                        if r2[y][x] = '1' then
                            drawInset (cx + x) (cy + y) overlayColor
        | _ -> ()
