# F# with Godot via C# Shims

This repository lets you write gameplay in F# and auto-generate C# shims that Godot can compile and recognize.

## Table of contents

- [Projects](#projects)
- [Quick start](#quick-start)
- [Features](#features)
  - [GlobalClass and Icon](#globalclass-and-icon)
  - [Tool scripts](#tool-scripts)
  - [Lifecycle forwarding (EnterTree/ExitTree)](#lifecycle-forwarding-entertreeexittree)
  - [NodePath auto‑wiring in \_Ready](#nodepath-auto-wiring-in-_ready)
  - [Editor hints](#editor-hints)
  - [Signals](#signals)
  - [Autoconnect](#autoconnect)
- [Configuration](#configuration)
- [Local development](#local-development)
- [Todo](#todo)

## Projects

- `Annotations` (NuGet: Headsetsniper.Godot.FSharp.Annotations)
  - Provides `[GodotScript]` attribute used in F#.
  - Provides `IGdScript<'TNode>` which lets your F# impl receive its Godot node.
- `FSharp`
  - Your F# gameplay logic referencing `Annotations`.
- `ShimGen` (NuGet: Headsetsniper.Godot.FSharp.ShimGen)
  - Console runner + MSBuild buildTransitive target to generate shims into `Scripts/Generated`.
  - Mirrors your F# folder structure under the output. Moves/renames are followed and old files pruned.
- `Scenes`, `Scripts`
  - Your Godot project code.

## Quick start

1. In your F# project:

- Install `Headsetsniper.Godot.FSharp.Annotations`.
- Annotate classes with:
  - `[<GodotScript(ClassName = "Foo", BaseTypeName = "Godot.Node2D")>]`
- Optionally implement `IGdScript<Node2D>` (or your base type) to get the node injected in Ready:

```fsharp
type FooImpl() =
  interface IGdScript<Node2D> with
    member val Node = Unchecked.defaultof<Node2D> with get, set
  member this.Ready() =
    // this.Node is set by the generated shim before calling Ready()
    ()
```

2. In your Godot C# project:

- Install `Headsetsniper.Godot.FSharp.ShimGen`.
- Add a `ProjectReference` to your F# project(s).
- Build. Shims appear under `Scripts/Generated` and are compiled.
- Shims include headers with SourceFile and SourceHash. The generator relocates outputs on moves/renames and prunes orphans.

## Configuration

- `FSharpShimsEnabled` (true by default)
- `FSharpShimsOutDir` (default `Scripts/Generated`)
- Command-line runner supports `--dry-run` to print planned writes/moves/deletes without changes.

## Local development

1. Pack the two NuGet packages locally

- Annotations: provides the `[GodotScript]` attribute
- ShimGen: buildTransitive target that auto-runs the shim generator and includes `Scripts/Generated/**/*.cs` at evaluation

```powershell
# From the repo root
dotnet pack Annotations\Headsetsniper.Godot.FSharp.Annotations.csproj -c Release
dotnet pack ShimGen\Headsetsniper.Godot.FSharp.ShimGen.csproj -c Release
mkdir -Force .nupkgs
Copy-Item Annotations\bin\Release\*.nupkg .nupkgs\
Copy-Item ShimGen\bin\Release\*.nupkg .nupkgs\
```

2. Use the included NuGet.Config

A solution-level `NuGet.Config` is included that adds a local source at `.nupkgs` alongside nuget.org. If you need to re-create it, it should look like:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="local" value="$(SolutionDir).nupkgs" />
  </packageSources>
</configuration>
```

3. Build the main project

```powershell
dotnet restore FsharpWithShim.csproj
dotnet build FsharpWithShim.csproj -v:n
```

- During build, the package’s `GenerateFSharpShims` target runs before `CoreCompile`, scans the referenced F# project(s), and writes shims into `Scripts/Generated`. Those shims are then included at evaluation time by the package’s buildTransitive target.
  It mirrors folder structure relative to your F# root. If you move/rename source files or classes, the generator will move the corresponding shims and remove old ones. Use `--dry-run` with the console runner to preview actions.

## Development

- Run tests: `dotnet test ShimGen.Tests`
- Pack packages:

  - `dotnet pack Annotations -c Release`
  - `dotnet pack ShimGen -c Release`

## Features

The generator and annotations now support these capabilities out of the box:

### GlobalClass and Icon

- Shims are emitted with `[GlobalClass]` automatically so the script shows up in the Godot editor.
- Provide an editor icon by setting `Icon` on `GodotScript`.
- F#: `[<GodotScript(ClassName = "Foo", BaseTypeName = "Godot.Node2D", Icon = "res://icon.svg")>]`
- Notes:
  - `Icon` should be a Godot resource path (e.g., `res://...` or `uid://...`).
  - The asset must exist in the project and be imported by Godot for the icon to appear.
  - `ClassName` controls the name of the generated shim/script visible in the editor; defaults to the F# type name if omitted.

### Tool scripts

- Enable editor-time behavior by setting `Tool=true` on `GodotScript`.
- F#: `[<GodotScript(ClassName = "Foo", BaseTypeName = "Godot.Node2D", Tool = true)>]`

### Lifecycle forwarding (EnterTree/ExitTree)

- Implement `EnterTree()` or `ExitTree()` in your F# type to receive those callbacks.
- `_Ready`, `_Process`, `_PhysicsProcess`, `_Input`, `_UnhandledInput`, `_Notification` are also supported when present.

### NodePath auto‑wiring in \_Ready

- Decorate fields/properties with `[NodePath]` to auto‑resolve nodes before calling `Ready()`.
- Default path is `nameof(Member)`; override with `Path = "Some/Child"`. Use `Required=false` to suppress error when missing.
- F# example:

  ```fsharp
  [<NodePath>]
  member val Player : Godot.Node2D = Unchecked.defaultof<_> with get, set
  ```

### Editor hints

- Exported properties show rich editors in Godot based on their type and optional attributes.

- Exported types (supported now):

  - Primitives: int, float, double, bool, string
  - Enums (incl. flags/bitmask)
  - Arrays: T[] (when T is supported)
  - Collections: List<'T>, Dictionary<string, 'V> (when element/value types are supported)
  - Godot structs: Vector2, Vector3, Color, Basis, Rect2, Transform2D, Transform3D
  - Engine types: NodePath, StringName, RID
  - Godot resources: any type deriving from Godot.Resource (e.g., Texture2D, PackedScene)

- Range slider: `[<ExportRange(min, max, step, orSlider)>]`

  - Example: `[<ExportRange(0.0, 10.0, 0.5, true)>] member val Speed : float32 = 1.0f with get, set`

- Enum flags/bitmask: mark enum with `[<System.Flags>]`

  - The shim emits `PropertyHint.Flags` with a comma-separated list of enum names.
  - Example: `[<System.Flags>] type MyFlags = | None = 0 | One = 1<<<0 | Two = 1<<<1 | Three = One ||| Two`

- File/Dir pickers: `[<ExportFile("*.png,*.jpg")>]` or `[<ExportDir>]` on string properties.

- Resource type filter: `[<ExportResourceType("Texture2D")>]` to filter resource picker.

- Multiline text: `[<ExportMultiline>]` on string properties.

- String enum list: `[<ExportEnumList("A,B,C")>]` on string properties.

- Color without alpha: `[<ExportColorNoAlpha>]` on Color.

- Layer masks: `[<ExportLayerMask2DRender>]` for 2D render layers.

- Categories and subgroups:
  - Group related properties under headers/subheaders in the Inspector.
  - Category: `[<ExportCategory("Movement")>]`
  - Subgroup: `[<ExportSubgroup("Speed", Prefix = "spd_")>]` (Prefix is optional)
  - Tooltip: `[<ExportTooltip("Units per second")>]`
  - Example: `[<ExportCategory("Movement")>][<ExportSubgroup("Speed", Prefix = "spd_")>][<ExportTooltip("Units per second")>] member val Speed : float32 = 1.0f with get, set`

Notes

- The shim sets `IGdScript<TNode>.Node = this` inside `_Ready()` before invoking your `Ready()`.
- NodePath wiring also runs inside `_Ready()` prior to `Ready()`.

### Signals

- Convention-based signals with strong typing are supported.

  - Declare public methods in your F# implementation whose names start with `Signal_`.
  - The portion after `Signal_` becomes the signal name on the generated shim.
  - The method parameters determine the signal's argument types; zero parameters produce a parameterless signal.

- What the shim generates:

  - For `member this.Signal_Fired() = ()`:

    - `[Signal] public event System.Action Fired;`
    - `public void EmitFired() => Fired?.Invoke();`

  - For `member this.Signal_Scored(points:int, who:string) = ()`:
    - `[Signal] public event System.Action<System.Int32, System.String> Scored;`
    - `public void EmitScored(System.Int32 points, System.String who) => Scored?.Invoke(points, who);`

- Notes:

  - Signal names are taken verbatim from the suffix after `Signal_` (e.g., `Signal_GameOver` -> `GameOver`).
  - Use regular .NET types compatible with Godot for parameters (e.g., `int`, `string`, Godot types).
  - You can emit the signal from your F# code by calling the shim’s `Emit<Name>(...)` method as shown above.

### Autoconnect

- Automatically connect a node's signal to a method on your F# implementation.

  - Decorate a public method with `[<AutoConnect(Path = "child/path", Signal = "pressed")>]`.
  - In `_Ready()`, the shim will resolve the node at `Path` and call `Connect("Signal", Callable.From(...))` to forward to your method.

- Examples:

  - No-arg signal (e.g., Button.pressed):

    ```fsharp
    [<GodotScript(ClassName = "Hud", BaseTypeName = "Godot.Control")>]
    type Hud() =
      member _.Ready() = ()

      [<AutoConnect(Path = "StartButton", Signal = "pressed")>]
      member _.OnStartPressed() =
        // Handle the button press
        ()
    ```

  - Typed signal args:

    ```fsharp
    [<GodotScript(ClassName = "Spawner", BaseTypeName = "Godot.Node2D")>]
    type Spawner() =
      member _.Ready() = ()

      [<AutoConnect(Path = "Enemy", Signal = "damaged")>]
      member _.OnEnemyDamaged(amount:int, source:string) =
        // amount and source are forwarded from the signal
        ()
    ```

- Notes:
  - `Path` is resolved via `GetNodeOrNull<Node>(new NodePath(Path))`; if missing, no connection is made.
  - Method parameters must match the signal's argument types and order.
  - You can stack multiple `[<AutoConnect ...>]` attributes on the same method to connect several nodes/signals.

## Todo

Planned work to reach comprehensive Godot capability support in F# via shims.

- Script metadata and registration

  - Global class registration: F# attribute to declare name/icon; emit [GlobalClass] on shim. V
  - Tool scripts: F# attribute to mark scripts as editor tools; emit [Tool] on shim. V
  - Class name/base type: ensure shim class name and base type mirror F# type and intended Godot base. V

- Exports (editor parity)

  - Types: aim for parity across primitives V, enums (incl. flags/bitmask) V, arrays/lists V, dictionaries V, Godot resources V, math types V, NodePath V, StringName V, RID V.
  - Hints/UI: Range (min/max/step/slider) V, file/dir/resource path filters V, multiline/string hint V, color-no-alpha V, layer masks V, enum lists V, flags bitmask V.
  - Defaults/categories: respect default values; support category/subgroup grouping. Support Tooltips. V

- Signals

  - Declaration: F# attribute for strongly-typed signals (arg names/types); generate [Signal], event, and Emit methods. V
  - Autoconnect: optional attribute to auto-wire child node signals to methods (on \_Ready or explicit). V

- Lifecycle and callbacks coverage

  - Node: \_EnterTree, \_Ready, \_ExitTree, \_Process, \_PhysicsProcess, \_Notification (parity ensured).
  - Input/UI: \_Input, \_UnhandledInput, Control.ShortcutInput, Control.GuiInput, drag/drop (CanDropData/GetDragData/DropData).
  - Drawing: \_Draw forwarding and helper surface hook if applicable.
  - Editor: support editor-only callbacks when [Tool] is set.

- RPC / Multiplayer

  - RPC methods: F# attribute covering Godot 4 RPC options (CallLocal, TransferMode, Channel, AnyPeer/Authority, Reliable/Unreliable); emit [Rpc] on shim methods.
  - Sync variables: attribute to replicate exported properties (MultiplayerSynchronizer or property RPC).

- NodePath auto-wiring / onready

  - Node references: F# [NodePath]/[Node] attributes; resolve/capture typed nodes in \_Ready with validation and friendly errors.
  - Preload: attribute for preloading PackedScene/Resource fields (editor/runtime-safe).

- Type mapping and marshalling

  - F# types: Option<'T>, Result<'T,'E>, tuples, records, discriminated unions; define export/serialization strategy and runtime invocation mapping.
  - Collections: smooth interop for F# array/list/map with Godot.Collections.Array/Dictionary where appropriate.

- Resources and custom types

  - Custom Resources: allow F# classes to inherit Resource; support [GlobalClass] and exports within resources.
  - Script icons/editor meta: allow icon and editor metadata decoration from F#.

- Error handling and diagnostics

  - Shim error messages: include F# type/method context in forwarding errors.
  - Editor diagnostics: optional verbose logging of wiring/autoconnect/export resolution in editor.

- Editor plugin support (advanced)

  - Authoring EditorPlugin/EditorInspectorPlugin in F# (patterns + shim support), ensure editor loads tools correctly.

- Async/await and coroutines

  - F# async helpers: bridge F# async with Godot Task/ToSignal; cancellation/timer utilities; idiomatic awaiting of signals.

- Build/IDE ergonomics

  - Maintain design-time-friendly targets (avoid heavy Conditions needing runtime metadata) and deterministic includes without duplicates.

- Documentation and samples

  - Cookbook: examples for exports with hints, signals, RPC, NodePath wiring, tool scripts, resources.
  - Templates: ready-to-use Godot+F# project template using this package.

- Test coverage
  - Add tests for export hints and types, UI callbacks, RPC attributes/invocation, NodePath wiring, Option/DU/records marshalling, resources/global classes, tool scripts behavior, autoconnect.
  - Cross-platform: validate generation on Windows/Linux/macOS.

Priorities

- P0: Export hints parity; NodePath auto-wiring; complete lifecycle callbacks; GlobalClass/Tool.
- P1: RPC attributes and sync vars; custom resources; Option/DU/records marshalling.
- P2: Autoconnect signals; async helpers; editor plugin patterns; expanded docs/templates.
