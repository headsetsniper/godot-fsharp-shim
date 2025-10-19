namespace Game

open System
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "TetrisTickRelay", BaseTypeName = "Godot.Node")>]
type TetrisTickRelayImpl(_node: Node) =
    let mutable nodeOpt: Node option = None

    interface IGdScript<Node> with
        member _.Node
            with get () =
                match nodeOpt with
                | Some n -> n
                | None -> raise (InvalidOperationException "Node not set")
            and set v = nodeOpt <- Some v

    member _.Ready() =
        nodeOpt |> Option.iter (fun n -> n.AddUserSignal(new StringName "Tick"))

    member _.EnterTree() =
        nodeOpt |> Option.iter (fun n -> n.AddUserSignal(new StringName "Tick"))

    [<AutoConnect("../DropTimer", "timeout")>]
    member _.OnTimeout() =
        nodeOpt |> Option.iter (fun n -> n.EmitSignal(new StringName "Tick") |> ignore)
