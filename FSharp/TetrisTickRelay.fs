namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "TetrisTickRelay", BaseTypeName = "Godot.Node")>]
type TetrisTickRelayImpl(node: Node) =
    member _.Ready() =
        node.AddUserSignal(new StringName "Tick")

    member _.EnterTree() =
        node.AddUserSignal(new StringName "Tick")

    [<AutoConnect("../DropTimer", "timeout")>]
    member _.OnTimeout() =
        node.EmitSignal(new StringName "Tick") |> ignore
