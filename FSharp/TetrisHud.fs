namespace Game

open System.Reflection
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "TetrisHud", BaseTypeName = "Godot.Control")>]
type TetrisHudImpl() =
    [<NodePath(Path = "ScoreLabel", Required = true)>]
    member val ScoreLabel: Label option = None with get, set

    [<NodePath(Path = "StatusLabel", Required = true)>]
    member val StatusLabel: Label option = None with get, set

    [<NodePath(Path = "../Board", Required = false)>]
    member val Board: Node2D option = None with get, set

    member _.Ready() = ()

    member this.Process(_delta: double) =
        match this.Board, this.ScoreLabel with
        | Some board, Some scoreLabel ->
            try
                let v = board.Get(new StringName("Score"))

                if v.VariantType = Godot.Variant.Type.Int then
                    scoreLabel.Text <- $"Score: {v.AsInt32()}"
            with _ ->
                ()
        | _ -> ()
