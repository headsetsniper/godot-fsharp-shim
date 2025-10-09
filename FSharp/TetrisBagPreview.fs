namespace Game

open System
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotTool(ClassName = "TetrisBagPreview", BaseTypeName = "Godot.Control")>]
type TetrisBagPreviewImpl() =
    let mutable nodeOpt: Control option = None
    let mutable printedReady = false

    interface IGdScript<Control> with
        member _.Node
            with get () =
                match nodeOpt with
                | Some n -> n
                | None -> raise (InvalidOperationException "Node not set")
            and set v = nodeOpt <- Some v

    [<OptionalNodePath(Path = "../Board")>]
    member val BoardOpt: Node option = None with get, set

    [<ExportRange(8.0, 64.0, 1.0, true)>]
    member val CellSize: float32 = 24.0f with get, set

    member this.Ready() =
        if not printedReady then
            printedReady <- true

        nodeOpt |> Option.iter (fun n -> n.QueueRedraw())

    member this.Draw() =
        match nodeOpt, this.BoardOpt with
        | Some n, Some b ->
            let enc =
                try
                    b.Get(new StringName "BagEncoded").AsString()
                with _ ->
                    ""

            let cs = this.CellSize
            let inset = MathF.Min(cs * 0.18f, 3.0f)
            let border = MathF.Max(1.0f, MathF.Min(2.0f, cs * 0.06f))
            let bgPanel = Color(0.08f, 0.08f, 0.10f, 1.0f)
            let gridLine = Color(1f, 1f, 1f, 0.06f)
            let cellBg = Color(0.20f, 0.20f, 0.22f, 0.35f)
            let fillColor = Color(0.95f, 0.8f, 0.2f)

            let draw x y (c: Color) =
                n.DrawRect(Rect2(float32 x * cs, float32 y * cs, cs, cs), c, true)

            let drawInset x y (c: Color) =
                let ox = float32 x * cs + inset
                let oy = float32 y * cs + inset
                let sz = cs - (inset * 2.0f)

                if sz > 0.0f then
                    n.DrawRect(Rect2(ox, oy, sz, sz), c, true)

            let drawBorder w h (c: Color) (th: single) =
                let sz = Vector2(float32 w * cs, float32 h * cs)
                let p0 = Vector2(0f, 0f)
                let p1 = Vector2(sz.X, 0f)
                let p2 = Vector2(sz.X, sz.Y)
                let p3 = Vector2(0f, sz.Y)
                n.DrawLine(p0, p1, c, th, true)
                n.DrawLine(p1, p2, c, th, true)
                n.DrawLine(p2, p3, c, th, true)
                n.DrawLine(p3, p0, c, th, true)

            ()

            // Panel background and min size
            n.CustomMinimumSize <- Vector2(4f * cs, 4f * cs)
            n.DrawRect(Rect2(0f, 0f, 4f * cs, 4f * cs), bgPanel, true)

            for y in 0..3 do
                for x in 0..3 do
                    draw x y cellBg

            // Grid lines
            for x in 1..3 do
                let xPos = float32 x * cs
                n.DrawLine(Vector2(xPos, 0f), Vector2(xPos, 4f * cs), gridLine, 1.0f, true)

            for y in 1..3 do
                let yPos = float32 y * cs
                n.DrawLine(Vector2(0f, yPos), Vector2(4f * cs, yPos), gridLine, 1.0f, true)

            // Outer border
            drawBorder 4 4 (Color(1f, 1f, 1f, 0.12f)) border

            if not (String.IsNullOrEmpty enc) then
                let rows = enc.Split('|')
                let h = rows.Length
                let w = rows[0].Length
                ()
                let ox = (4 - w) / 2
                let oy = (4 - h) / 2

                for y in 0 .. h - 1 do
                    for x in 0 .. w - 1 do
                        if rows[y][x] = '1' then
                            drawInset (ox + x) (oy + y) fillColor
            else
                ()
        | _ -> ()
