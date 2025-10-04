namespace Game

open System
open System.Reflection
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "Tetris", BaseTypeName = "Godot.Node2D", Icon = "res://icon.svg", Tool = false)>]
type TetrisImpl() =
    [<NodePath(Path = "Board", Required = true)>]
    member val Board : Node2D = null with get, set

    [<NodePath(Path = "HUD", Required = true)>]
    member val Hud : Control = null with get, set

    [<NodePath(Path = "DropTimer", Required = true)>]
    member val DropTimer : Timer = null with get, set

    member this.Ready() =
        if not (isNull this.DropTimer) then
            this.DropTimer.WaitTime <- 0.6
            this.DropTimer.Autostart <- true
            this.DropTimer.Start()

    member _.Process(_delta: double) = ()

    member this.Input(ev: InputEvent) =
        match ev with
        | :? InputEventKey as key when key.Pressed && not key.Echo ->
            let setBoardProp name (value: obj) =
                if not (isNull this.Board) then
                    try
                        let prop = this.Board.GetType().GetProperty(name, BindingFlags.Instance ||| BindingFlags.Public)
                        if not (isNull prop) then prop.SetValue(this.Board, value)
                    with _ -> ()
            match key.Keycode with
            | Key.Left -> setBoardProp "MoveX" (box -1)
            | Key.Right -> setBoardProp "MoveX" (box 1)
            | Key.Down -> () // timer handles regular drop
            | Key.Up -> setBoardProp "Rotate" (box true)
            | Key.Space -> setBoardProp "HardDrop" (box true)
            | _ -> ()
        | _ -> ()

// No extra wiring node needed; Board autoconnects directly to Tetris via NodePath("..")
