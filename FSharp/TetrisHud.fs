namespace Game

open System.Reflection
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "TetrisHud", BaseTypeName = "Godot.Control")>]
type TetrisHudImpl() =
    [<NodePath(Path = "ScoreLabel")>]
    member val ScoreLabel: Label = Unchecked.defaultof<_> with get, set

    [<NodePath(Path = "StatusLabel")>]
    member val StatusLabel: Label = Unchecked.defaultof<_> with get, set

    [<NodePath(Path = "../Board")>]
    member val Board: Node2D = Unchecked.defaultof<_> with get, set

    member _.Ready() = ()

    member this.Process(_delta: double) =
        try
            let v = this.Board.Get(new StringName("Score"))

            if v.VariantType = Godot.Variant.Type.Int then
                this.ScoreLabel.Text <- $"Score: {v.AsInt32()}"
        with _ ->
            ()
