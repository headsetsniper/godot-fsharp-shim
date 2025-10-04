namespace Game

open System
open System.Reflection
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "Tetris", BaseTypeName = "Godot.Node2D", Icon = "res://icon.svg", Tool = false)>]
type TetrisImpl() =
    [<NodePath(Path = "Board")>]
    member val Board: Node2D = Unchecked.defaultof<_> with get, set

    [<NodePath(Path = "HUD")>]
    member val Hud: Control = Unchecked.defaultof<_> with get, set

    [<NodePath(Path = "DropTimer")>]
    member val DropTimer: Timer = Unchecked.defaultof<_> with get, set

    member this.Ready() =
        // Guard in case the node hasn't been wired by the shim (older package or misconfigured scene)
        if not (obj.ReferenceEquals(this.DropTimer, null)) then
            this.DropTimer.WaitTime <- 0.6
            this.DropTimer.Autostart <- true
            // Fallback connect for older shim packages that don't emit [AutoConnect]
            if not (obj.ReferenceEquals(this.Board, null)) then
                try
                    let handler =
                        Callable.From(fun () ->
                            try
                                let mi =
                                    this.Board
                                        .GetType()
                                        .GetMethod("OnTimeout", BindingFlags.Instance ||| BindingFlags.Public)

                                if not (obj.ReferenceEquals(mi, null)) then
                                    mi.Invoke(this.Board, [||]) |> ignore
                            with _ ->
                                ())

                    this.DropTimer.Connect("timeout", handler) |> ignore
                with _ ->
                    ()

            this.DropTimer.Start()

    member _.Process(_delta: double) = ()

    member this.Input(ev: InputEvent) =
        match ev with
        | :? InputEventKey as key when key.Pressed && not key.Echo ->
            let setInt name (v: int) =
                if not (obj.ReferenceEquals(this.Board, null)) then
                    try
                        match this.Board.GetType().GetProperty(name, BindingFlags.Instance ||| BindingFlags.Public) with
                        | null -> ()
                        | prop -> prop.SetValue(this.Board, box v)
                    with _ ->
                        ()

            let setBool name (v: bool) =
                if not (obj.ReferenceEquals(this.Board, null)) then
                    try
                        match this.Board.GetType().GetProperty(name, BindingFlags.Instance ||| BindingFlags.Public) with
                        | null -> ()
                        | prop -> prop.SetValue(this.Board, box v)
                    with _ ->
                        ()

            match key.Keycode with
            | Key.Left -> setInt "MoveX" -1
            | Key.Right -> setInt "MoveX" 1
            | Key.Down -> () // timer handles regular drop
            | Key.Up -> setBool "RotateRequested" true
            | Key.Space -> setBool "HardDrop" true
            | _ -> ()
        | _ -> ()

// No extra wiring node needed; Board autoconnects directly to Tetris via NodePath("..")
