# F# with Godot via C# Shims

This repository demonstrates how to drive Godot game logic in F# while generating on-disk C# shims that Godot can consume.

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

## Using the packages in your own project

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

## Configuration knobs

- `FSharpShimsEnabled` (true by default)
- `FSharpShimsOutDir` (default `Scripts/Generated`)
- Command-line runner supports `--dry-run` to print planned writes/moves/deletes without changes.

## Local development flow

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

- Tool scripts

  - Enable editor-time behavior by setting `Tool=true` on `GodotScript`.
  - F#: `[<GodotScript(ClassName = "Foo", BaseTypeName = "Godot.Node2D", Tool = true)>]`

- Lifecycle forwarding (EnterTree/ExitTree)

  - Implement `EnterTree()` or `ExitTree()` in your F# type to receive those callbacks.
  - `_Ready`, `_Process`, `_PhysicsProcess`, `_Input`, `_UnhandledInput`, `_Notification` are also supported when present.

- NodePath auto‑wiring in \_Ready

  - Decorate fields/properties with `[NodePath]` to auto‑resolve nodes before calling `Ready()`.
  - Default path is `nameof(Member)`; override with `Path = "Some/Child"`. Use `Required=false` to suppress error when missing.
  - F# example: - `[<NodePath>]
member val Player : Godot.Node2D = Unchecked.defaultof<_> with get, set`

- Export range hints
  - Add `[ExportRange(min, max, step, orSlider)]` to numeric properties to show a range control in the editor.
  - F# example: - `[<ExportRange(0.0, 10.0, 0.5, true)>]
member val Speed : float32 = 1.0f with get, set`

Notes

- The shim sets `IGdScript<TNode>.Node = this` inside `_Ready()` before invoking your `Ready()`.
- NodePath wiring also runs inside `_Ready()` prior to `Ready()`.

## Todo

Planned work to reach comprehensive Godot capability support in F# via shims.

- Script metadata and registration

  - Global class registration: F# attribute to declare name/icon; emit [GlobalClass] on shim.
  - Tool scripts: F# attribute to mark scripts as editor tools; emit [Tool] on shim. V
  - Class name/base type: ensure shim class name and base type mirror F# type and intended Godot base.

- Exports (editor parity)

  - Types: primitives, enums, flags/bitmask, arrays/lists, dictionaries, Godot resources (Texture2D, PackedScene, etc.), math types (Vector2/3, Color, Basis, Rect2, Transform\*), NodePath, StringName, RID.
  - Hints/UI: Range (min/max/step/slider) V, file/dir/resource path filters, multiline/string hint, color-no-alpha, layer masks, enum lists, flags bitmask, category/group/subgroup, tooltips.
  - Defaults/categories: respect default values; support category/subgroup grouping.

- Signals

  - Declaration: F# attribute for strongly-typed signals (arg names/types); generate [Signal], event, and Emit methods.
  - Autoconnect: optional attribute to auto-wire child node signals to methods (on \_Ready or explicit).

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
