# Godot.FSharp.Annotations

Provides the `[GodotScript]` attribute to mark F# classes as Godot scripts.

## Usage

- Install the NuGet package in your F# project:
  - Package Id: `Godot.FSharp.Annotations`
- Annotate your F# types:

```fsharp
namespace Game
open Godot.FSharp.Annotations

[<GodotScript(ClassName = "Foo", BaseTypeName = "Godot.Node2D")>]
type FooImpl() =
    let mutable speed = 220
    member _.Speed with get() = speed and set v = speed <- v
    member _.Ready() = ()
    member _.Process(delta: double) = ignore delta
```

- When used together with `Godot.FSharp.ShimGen` in your C# Godot project, shims will be generated automatically.

## Attribute Reference

- `GodotScriptAttribute`
  - `ClassName`: name of the generated class in Godot
  - `BaseTypeName`: fully-qualified Godot base type (e.g., `Godot.Node2D`)
