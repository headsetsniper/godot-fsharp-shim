namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "TetrisHud", BaseTypeName = "Godot.Control")>]
type TetrisHudImpl(_node: Control, scoreLabel: Label, statusLabel: Label, board: Node) =
    [<NodePath(Path = "ScoreLabel")>]
    member val ScoreLabel: Label = scoreLabel with get, set

    [<NodePath(Path = "StatusLabel")>]
    member val StatusLabel: Label = statusLabel with get, set

    [<NodePath(Path = "../Board")>]
    member val Board: Node = board with get, set

    member _.Ready() = ()

    member this.Process(_delta: double) =
        let tryGetScore () =
            try
                let v = this.Board.Get(new StringName "Score")

                if v.VariantType = Godot.Variant.Type.Int then
                    Some(v.AsInt32())
                else
                    None
            with _ ->
                None

        let renderScore s = this.ScoreLabel.Text <- $"Score: {s}"

        match tryGetScore () with
        | Some s -> renderScore s
        | None -> ()
