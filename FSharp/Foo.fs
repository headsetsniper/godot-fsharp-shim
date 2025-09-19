namespace Game

open Godot.FSharp.Annotations

[<GodotScript(ClassName = "Foo", BaseTypeName = "Godot.Node2D")>]
type FooImpl() =
    let mutable speed = 220
    member _.Speed with get() = speed and set v = speed <- v

    member _.Ready() = ()
    member _.Process(delta: double) = ignore delta
