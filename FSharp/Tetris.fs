namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "Tetris", BaseTypeName = "Godot.Node2D", Icon = "res://icon.svg", Tool = false)>]
type TetrisImpl() =
    [<NodePath(Path = "UIRoot/Board")>]
    member val Board: Node = Unchecked.defaultof<_> with get, set

    [<NodePath(Path = "UIRoot/HUD")>]
    member val Hud: Control = Unchecked.defaultof<_> with get, set

    [<NodePath(Path = "UIRoot/DropTimer")>]
    member val DropTimer: Timer = Unchecked.defaultof<_> with get, set

    member this.Ready() =
        this.DropTimer.WaitTime <- 0.6
        this.DropTimer.Autostart <- true
        this.DropTimer.Start()

    member _.Process(_delta: double) = ()

    member this.Input(ev: InputEvent) =
        let setInt (name: string) (v: int) =
            this.Board.Set(new StringName(name), Godot.Variant.op_Implicit v)

        let setBool (name: string) (v: bool) =
            this.Board.Set(new StringName(name), Godot.Variant.op_Implicit v)

        match ev with
        | :? InputEventKey as key when key.Pressed && not key.Echo ->
            match key.Keycode with
            | Key.Left
            | Key.A -> setInt "MoveX" -1
            | Key.Right
            | Key.D -> setInt "MoveX" 1
            | Key.Down -> ()
            | Key.Up
            | Key.W -> setBool "RotateRequested" true
            | Key.Space -> setBool "HardDrop" true
            | _ -> ()
        | _ -> ()
