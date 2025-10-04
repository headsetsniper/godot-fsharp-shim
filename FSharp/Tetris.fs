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
        // NodePath is required; shim wires DropTimer before calling Ready()
        this.DropTimer.WaitTime <- 0.6
        this.DropTimer.Autostart <- true
        this.DropTimer.Start()

    member _.Process(_delta: double) = ()

    member this.Input(ev: InputEvent) =
        match ev with
        | :? InputEventKey as key when key.Pressed && not key.Echo ->
            let setInt name (v: int) =
                try
                    match this.Board.GetType().GetProperty(name, BindingFlags.Instance ||| BindingFlags.Public) with
                    | null -> ()
                    | prop -> prop.SetValue(this.Board, box v)
                with _ ->
                    ()

            let setBool name (v: bool) =
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
