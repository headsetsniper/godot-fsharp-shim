# Headsetsniper.Godot.FSharp.Annotations

Provides the `[GodotScript]` attribute and `IGdScript<'TNode>` for F# Godot scripts.

## Usage

- Install the NuGet package in your F# project:
  - Package Id: `Headsetsniper.Godot.FSharp.Annotations`
- Annotate your F# types:

```fsharp
namespace Game
open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<GodotScript(ClassName = "Foo", BaseTypeName = "Godot.Node2D")>]
type FooImpl() =
    let mutable speed = 220
    member _.Speed with get() = speed and set v = speed <- v
  // Receive the backing Godot node via shim injection
  let mutable node : Node2D = Unchecked.defaultof<_>
  interface IGdScript<Node2D> with
    member _.Node with get() = node and set v = node <- v

  member _.Ready() =
    // node is set here by the generated shim
    ()
    member _.Process(delta: double) = ignore delta
```

- When used together with `Headsetsniper.Godot.FSharp.ShimGen` in your C# Godot project, shims will be generated automatically.

## Attribute Reference

- `GodotScriptAttribute`
  - `ClassName`: name of the generated class in Godot
  - `BaseTypeName`: fully-qualified Godot base type (e.g., `Godot.Node2D`)
- `IGdScript<'TNode>`
  - `Node` property is set by the generated shim in `_Ready()` before your `Ready()` method executes.
