namespace Game

open System.Reflection
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "TetrisHud", BaseTypeName = "Godot.Control")>]
type TetrisHudImpl() =
    let tryRef (x: obj) =
        if obj.ReferenceEquals(x, null) then None else Some x

    [<NodePath(Path = "ScoreLabel", Required = true)>]
    member val ScoreLabel: Label = Unchecked.defaultof<_> with get, set

    [<NodePath(Path = "StatusLabel", Required = true)>]
    member val StatusLabel: Label = Unchecked.defaultof<_> with get, set

    [<NodePath(Path = "../Board", Required = false)>]
    member val Board: Node2D = Unchecked.defaultof<_> with get, set

    member _.Ready() = ()

    member this.Process(_delta: double) =
        match tryRef this.Board, tryRef this.ScoreLabel with
        | Some board, Some scoreLabel ->
            try
                let v = board.Get(new StringName("Score"))

                if v.VariantType = Godot.Variant.Type.Int then
                    scoreLabel.Text <- $"Score: {v.AsInt32()}"
            with _ ->
                ()
        | _ -> ()
