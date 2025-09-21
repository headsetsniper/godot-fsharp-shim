namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "Foo", BaseTypeName = "Godot.Node2D")>]
type FooImpl() =
    // Exposed to the editor via generated [Export] (int is safest across Godot versions)
    let mutable speed : int = 220
    // Injected by the generated shim in _Ready() before calling our Ready()
    let mutable node : Node2D = Unchecked.defaultof<_>

    // Interface must come before members in F# class definitions
    interface IGdScript<Node2D> with
        member _.Node
            with get() = node
            and set v = node <- v

    member _.Speed with get() = speed and set v = speed <- v

    member _.Ready() =
        // Create a sprite so we can see something in the scene
        let sprite = new Sprite2D()
        let tex = ResourceLoader.Load("res://icon.svg") :?> Texture2D
        sprite.Texture <- tex
        sprite.Centered <- true
        node.AddChild(sprite)

    member _.Process(delta: double) =
        // WASD movement
        let dt = float32 delta
        let mutable dir = Vector2.Zero
        if Input.IsKeyPressed Key.W then dir <- dir + Vector2.Up
        if Input.IsKeyPressed Key.S then dir <- dir + Vector2.Down
        if Input.IsKeyPressed Key.A then dir <- dir + Vector2.Left
        if Input.IsKeyPressed Key.D then dir <- dir + Vector2.Right
        if dir.Length() > 0.0f then
            dir <- dir.Normalized()
        node.Position <- node.Position + (dir * (float32 speed) * dt)
