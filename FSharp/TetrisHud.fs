namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "TetrisHud", BaseTypeName = "Godot.Control")>]
type TetrisHudImpl() =
    [<NodePath(Path = "ScoreLabel")>]
    member val ScoreLabel: Label = Unchecked.defaultof<_> with get, set

    [<NodePath(Path = "StatusLabel")>]
    member val StatusLabel: Label = Unchecked.defaultof<_> with get, set

    [<NodePath(Path = "../Board")>]
    member val Board: Node = Unchecked.defaultof<_> with get, set

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
