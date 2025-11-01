# F# with Godot via C# Shims

[![CI](https://github.com/headsetsniper/godot-fsharp-shim/actions/workflows/pack.yml/badge.svg)](https://github.com/headsetsniper/godot-fsharp-shim/actions/workflows/pack.yml)
[![NuGet (ShimGen)](https://img.shields.io/nuget/v/Headsetsniper.Godot.FSharp.ShimGen.svg)](https://www.nuget.org/packages/Headsetsniper.Godot.FSharp.ShimGen)
[![NuGet (Annotations)](https://img.shields.io/nuget/v/Headsetsniper.Godot.FSharp.Annotations.svg)](https://www.nuget.org/packages/Headsetsniper.Godot.FSharp.Annotations)

This repository lets you write gameplay in F# and auto-generate C# shims that Godot can compile and recognize.

## Table of contents

- [Projects](#projects)
- [Quick start](#quick-start)
- [Features](#features)
  - [GlobalClass and icon](#globalclass-and-icon)
  - [Tool scripts](#tool-scripts)
  - [Constructor injection (DI)](#constructor-injection-di)
  - [Lifecycle forwarding](#lifecycle-forwarding)
  - [NodePath auto-wiring](#nodepath-auto-wiring)
  - [Editor hints](#editor-hints)
  - [Option and preload semantics](#option-and-preload-semantics)
  - [Signals](#signals)
  - [Autoconnect](#autoconnect)
- [Configuration](#configuration)
- [Testing with gdUnit4](#testing-with-gdunit4)
- [F# test shims (Tests mode)](#f-test-shims-tests-mode)
- [Troubleshooting](#troubleshooting)
- [Roadmap](#roadmap)
- [Roslyn generator](#roslyn-generator)
- [Local development](#local-development)

## Projects

- `Annotations/`: F# attributes and interfaces packaged as `Headsetsniper.Godot.FSharp.Annotations` (`dotnet add package Headsetsniper.Godot.FSharp.Annotations`).
- `ShimGen/`: shim generator CLI plus buildTransitive targets published as `Headsetsniper.Godot.FSharp.ShimGen` (`dotnet add package Headsetsniper.Godot.FSharp.ShimGen`).
- `FSharp/`: sample gameplay logic written in F#.
- `ExampleProject/`: Godot C# project that consumes generated shims and demonstrates gdUnit4 testing.
- `Templates/`: `dotnet new` templates that scaffold an F# gameplay project with optional gdUnit4 tests (`dotnet new install Headsetsniper.Godot.FSharp.Templates`).

## Quick start

### Scaffold a new game from the template

```powershell
dotnet new install Headsetsniper.Godot.FSharp.Templates
dotnet new godot-fsharp -n MyGameFSharp --IncludeTests
```

Open the generated Godot solution, run `dotnet build` on the C# project, and the `Headsetsniper.Godot.FSharp.ShimGen` package will generate shims under `Scripts/Generated` automatically.

### Wire the packages into an existing solution

```powershell
dotnet add FSharp/MyGame.fsproj package Headsetsniper.Godot.FSharp.Annotations
dotnet add ExampleProject/MyGame.csproj package Headsetsniper.Godot.FSharp.ShimGen
```

Reference the F# gameplay project from the Godot C# project, decorate classes with `[<GodotScript>]` and other attributes, then build. ShimGen finds the referenced F# assembly and emits the C# shims that Godot discovers automatically.

## Templates

Install the published template straight from NuGet (recommended):

```powershell
dotnet new install Headsetsniper.Godot.FSharp.Templates
dotnet new godot-fsharp -n MyGameFSharp --IncludeTests
```

- `--IncludeTests` (or `-I`) adds a gdUnit4-ready F# test project and a companion C# TestShims project wired for shim generation; omit or pass `false` to generate only the gameplay project.
- `--AnnotationsVersion` (or `-A`) accepts any NuGet version expression (defaults to `0.*`).
- Generated projects target `net9.0` and reference `Headsetsniper.Godot.FSharp.Annotations`; the optional test project mirrors the repo's gdUnit4 configuration, including a `.runsettings` stub (`GODOT_BIN` must be updated).
- Pin a specific release with `dotnet new install Headsetsniper.Godot.FSharp.Templates::0.10.2` and upgrade later via `dotnet new update`.
- Uninstall when you no longer need the template:

  ```powershell
  dotnet new uninstall Headsetsniper.Godot.FSharp.Templates
  ```

## Features

### Constructor injection (DI)

Gameplay scripts can opt into DI by defining a single public constructor:

- First parameter must match `BaseTypeName` (the node the shim derives from). The shim passes `this`.
- Remaining parameters bind to `[NodePath]` and `[Preload]` members:
  - Name-based binding first (parameter name matches member name).
  - If no name match: unique type-based binding (exact type, unambiguous).
- Requirements to enable DI:
  - All required `[NodePath]` (non-Option) present in the constructor.
  - All required `[Preload]` present.
  - Exactly one public constructor.
- When DI is active:
  - The shim constructs your F# implementation in `_Ready()` after resolving NodePaths/Preloads.
  - For `[GodotTool]` scripts, implementing `IGdToolScript<T>` still assigns `Node = this` inside `_Ready()` before `Ready()` runs.
  - No property wiring is performed for NodePath/Preload (they are provided via constructor args).
- When DI is not active (tool script, multiple ctors, or missing bindings):
  - The shim falls back to eager construction and property wiring in `_Ready()`.
  - Clear warnings are emitted describing why DI was not used.

Preload with Option<'T>: DI still injects the concrete resource type and will fail fast if the resource is missing. Prefer non-Option types for Preload targets to reflect this guarantee.

### Lifecycle forwarding

- Implement `EnterTree()` or `ExitTree()` in your F# type to receive those callbacks.
- `_Ready`, `_Process`, `_PhysicsProcess`, `_Input`, `_UnhandledInput`, `_Notification` are also supported when present.
  - Control-specific callbacks (Godot 4.5): `_GuiInput`, `_ShortcutInput`, `_UnhandledKeyInput`, drag & drop (`_CanDropData`, `_DropData`, `_GetDragData`), hit-testing (`_HasPoint`), sizing (`_GetMinimumSize`), tooltips (`_MakeCustomTooltip`, `_GetTooltip`).
  - Drawing (CanvasItem family): `_Draw`.

#### Callback matrix

The shim forwards callbacks when your F# implementation exposes matching methods. It also respects base type capabilities (e.g., Control-only methods).

| Base type                   | Godot callback                  | F# method to implement                                   | Shim override emitted                                 | Notes                                                                            |
| --------------------------- | ------------------------------- | -------------------------------------------------------- | ----------------------------------------------------- | -------------------------------------------------------------------------------- |
| Node                        | \_EnterTree                     | `member _.EnterTree()`                                   | `public override void _EnterTree()`                   |                                                                                  |
| Node                        | \_Ready                         | `member _.Ready()`                                       | `public override void _Ready()`                       | Tool scripts implementing `IGdToolScript<T>` receive `Node = this` before wiring |
| Node                        | \_ExitTree                      | `member _.ExitTree()`                                    | `public override void _ExitTree()`                    |                                                                                  |
| Node                        | \_Process(double)               | `member _.Process(delta: double)`                        | `public override void _Process(double)`               |                                                                                  |
| Node                        | \_PhysicsProcess(double)        | `member _.PhysicsProcess(delta: double)`                 | `public override void _PhysicsProcess(double)`        |                                                                                  |
| Node                        | \_Input(InputEvent)             | `member _.Input(ev: InputEvent)`                         | `public override void _Input(InputEvent)`             |                                                                                  |
| Node                        | \_UnhandledInput(InputEvent)    | `member _.UnhandledInput(ev: InputEvent)`                | `public override void _UnhandledInput(InputEvent)`    |                                                                                  |
| Node                        | \_Notification(long)            | `member _.Notification(what: int64)`                     | `public override void _Notification(long)`            |                                                                                  |
| CanvasItem (Node2D/Control) | \_Draw                          | `member _.Draw()`                                        | `public override void _Draw()`                        |                                                                                  |
| Control                     | \_GuiInput(InputEvent)          | `member _.GuiInput(ev: InputEvent)`                      | `public override void _GuiInput(InputEvent)`          | Godot 4.5                                                                        |
| Control                     | \_ShortcutInput(InputEvent)     | `member _.ShortcutInput(ev: InputEvent)`                 | `public override void _ShortcutInput(InputEvent)`     | Godot 4.5                                                                        |
| Control                     | \_UnhandledKeyInput(InputEvent) | `member _.UnhandledKeyInput(ev: InputEvent)`             | `public override void _UnhandledKeyInput(InputEvent)` | Godot 4.5                                                                        |
| Control                     | \_CanDropData(Vector2, Variant) | `member _.CanDropData(p: Vector2, data: Variant) : bool` | `public override bool _CanDropData(Vector2, Variant)` | Drag & drop                                                                      |
| Control                     | \_DropData(Vector2, Variant)    | `member _.DropData(p: Vector2, data: Variant)`           | `public override void _DropData(Vector2, Variant)`    | Drag & drop                                                                      |
| Control                     | \_GetDragData(Vector2)          | `member _.GetDragData(p: Vector2) : obj`                 | `public override Variant _GetDragData(Vector2)`       | Shim casts returned object to `Variant`                                          |
| Control                     | \_HasPoint(Vector2)             | `member _.HasPoint(p: Vector2) : bool`                   | `public override bool _HasPoint(Vector2)`             | Hit testing                                                                      |
| Control                     | \_GetMinimumSize()              | `member _.GetMinimumSize() : Vector2`                    | `public override Vector2 _GetMinimumSize()`           | Layout                                                                           |
| Control                     | \_MakeCustomTooltip(string)     | `member _.MakeCustomTooltip(text: string) : Control`     | `public override Control _MakeCustomTooltip(string)`  | Custom tooltip control                                                           |
| Control                     | \_GetTooltip(Vector2)           | `member _.GetTooltip(p: Vector2) : string`               | `public override string _GetTooltip(Vector2)`         | Tooltip text                                                                     |

### NodePath auto-wiring

- Decorate fields/properties with `[NodePath]` so the shim resolves nodes before invoking your implementation.
- The default lookup uses `nameof(Member)`; override with `Path = "Some/Child"`. Use `[OptionalNodePath]` on an `Option<'T>` member when the reference is optional.
- F# example:

  ```fsharp
  [<NodePath>]
  member val Player : Godot.Node2D = Unchecked.defaultof<_> with get, set
  ```

- Optional node references with Option<'T>
  - `[OptionalNodePath]` requires an `Option<'TNode>` and records presence as `Some/None`.
  - `[NodePath]` must target a non-Option type; generation fails if the source type is `Option<'T>`.
- Runtime wiring semantics
  - **Constructor injection active** (default for gameplay scripts): the shim resolves every required `[NodePath]`/`[Preload]` before instantiating your F# type. Missing required nodes throw before construction begins, guaranteeing that constructor parameters are satisfied.
  - **Property wiring fallback** (tool scripts or DI-disabled scenarios): the shim creates the F# type up front, then assigns `[NodePath]` members in `_Ready()`. Required paths still throw if missing; optional paths become `None` when the node cannot be found.

### GlobalClass and icon

- Set `ClassName` on `[<GodotScript>]` to control the name that appears in the Godot editor; it defaults to the F# type name.
- Provide `Icon = "res://path/to/icon.svg"` (or `GodotScript.IconPath`) and ensure the asset is imported so the generated shim can apply `[GlobalClass]` with the icon.

### Tool scripts

- Mark editor-time scripts with `[<GodotTool(... )>]` when you need `_Process`/`_Draw` to run inside the editor. The old `Tool = true` flag on `[<GodotScript>]` has been removed.
- F# example: `[<GodotTool(ClassName = "Board", BaseTypeName = "Godot.Control")>]`
- Constructor injection is intentionally disabled for tool scripts so the shim can be created even when the Godot scene tree is incomplete. The generator falls back to property wiring in `_Ready()`.
- Implement `IGdToolScript<TBase>` on tool scripts when you need the shim instance (the Godot node) injected. The generator assigns `Node = this` at the top of `_Ready()` before invoking your implementation.
- Gameplay/runtime scripts must _not_ implement `IGdToolScript<T>`. If they do, the generator emits a warning and skips the assignment because constructor injection already passes `this` to your F# constructor.

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

- `[GodotTool]` scripts that implement `IGdToolScript<TNode>` receive `Node = this` inside `_Ready()` before any user code runs.
- During property wiring (tool scripts or DI-disabled flows), NodePath resolution still happens inside `_Ready()` prior to calling `Ready()`.

### Option and preload semantics

- Why: Godot/C# uses nullable reference types, while F# favors non-null. To bridge this, the shim understands `Option<'T>`.

- Exports

  - If an F# export is `Option<'T>`, the generated C# shim property is of type `T`.
  - Getter returns the inner value or `default(T)` if `None`.
  - Setter wraps the assigned value into `Some value` on the F# side.

- NodePath

  - If a NodePath target is `Option<'TNode>`, the shim assigns `Some node` when found, `None` when missing.
  - NodePath is required (non-Option) and throws if missing; OptionalNodePath is for `Option<'T>` and sets `None` if missing.

- Preload
  - For `[<Preload(...)]` members whose type is a Godot Resource (e.g., `Texture2D`, `PackedScene`): the shim always attempts to load and will throw `InvalidOperationException` when the resource is missing.
  - With DI: constructor parameters receive concrete resources; missing assets throw before construction.
  - With property wiring: members are assigned after load. Prefer non-Option types to reflect the guarantee.
  - For non-preloadable references (e.g., NodePath, arbitrary references not covered by Preload), keep using `Option<'T>` when the reference may be absent.

Examples

```fsharp
// Export Option: shows up in the inspector as T, Option wrapping in F#
[<Export>] member val MaybeName : string option = None with get, set

// NodePath Option: captured if present, None otherwise
[<OptionalNodePath>]
member val Camera : Camera2D option = None with get, set

// Preload of resource: throws if missing, F# receives a non-null Texture2D
[<Preload("res://icon.svg")>]
member val Icon : Texture2D = Unchecked.defaultof<_> with get, set
```

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

## Configuration

- FSharpShimsEnabled (true by default)
  - Master switch to enable/disable shim generation.
- FSharpShimsOutDir (default `Scripts/Generated`)
  - Output folder for generated C# shims; path stability helps keep Godot UIDs stable.
- FSharpShimsVerbose (false by default)
  - When `true`, increases `[shimgen]` log verbosity and prints tool stdout at Normal importance.
- FSharpShimsRegenerate (empty by default)
  - Forwarded to the generator as the `SHIMGEN_REGENERATE_SCRIPTS` environment variable when set. Examples:
    - `FSharpShimsRegenerate=all` (or `*`) — regenerate all shims in-place.
    - `FSharpShimsRegenerate=Tetris,TetrisBoard` — regenerate the listed scripts.
- FSharpShimsFallbackInclude (true by default)
  - Automatically includes `$(FSharpShimsOutDir)/**/*.cs` at compile time when the generator hasn’t added them (e.g., Godot/editor-driven builds). Inclusion is idempotent and avoids duplicate source warnings.

Notes

- Consumers do NOT need to manually include or exclude `Scripts/Generated/**/*.cs` in their project files. The buildTransitive targets add them when the generator runs and fall back to including existing files when it doesn’t.
- Command-line runner supports `--dry-run` to print planned writes/moves/deletes without changes.

### In-place regeneration (preserve Godot UIDs)

You can set regeneration either via MSBuild property or environment variable. Both map to the same behavior.

- MSBuild property (recommended in CI or local builds):
  - `FSharpShimsRegenerate=all` (or `*`) to regenerate all scripts in-place.
  - `FSharpShimsRegenerate=Tetris,TetrisBoard` (comma/semicolon/whitespace separated), or use F# full names like `Game.TetrisImpl`.
- Environment variable (equivalent):
  - `SHIMGEN_REGENERATE_SCRIPTS=all` or a comma-separated list.

Notes:

- When regenerating in-place and a prior generated file is found, the generator overwrites that exact path rather than relocating. This keeps the same UID next to the file.
- If no previous file is found for a script, it falls back to the normal output path under `Scripts/Generated`.
- Close the Godot editor before regeneration on Windows to avoid file locks under `Scripts/Generated`. (Sporadical Error)

## Testing with gdUnit4

The example project is wired to run gdUnit4 tests directly via `dotnet test` using a `.runsettings` file and minimal csproj configuration.

### Project setup (ExampleProject)

- In `FsharpWithShim.csproj`:

  - Set `RunSettingsFilePath` to `$(MSBuildProjectDirectory)\​.runsettings` so `dotnet test` picks it up automatically.
  - Disable MSTest adapter discovery: `<VSTestTestAdapterPath>none</VSTestTestAdapterPath>`
  - Select the test framework: `<TestFramework>GdUnit4</TestFramework>`
  - Reference required packages (known-good pairing):
    - `Microsoft.NET.Test.Sdk` (tested with 18.0.0)
    - `gdUnit4.api` 5.0.0
    - `gdUnit4.test.adapter` 3.0.0
    - `gdUnit4.analyzers`
  - Include adapter sources:
    - `Compile Include="gdunit4_testadapter_v5\**\*.cs"`
    - `Compile Include="addons\gdUnit4\src\dotnet\**\*.cs" Visible="false" Condition="'$(GodotTargetPlatform)'!='windows-editor'"`
  - Define the gdUnit4 .NET API constant outside Windows editor builds so the adapter exposes its bridge types:
    - `<PropertyGroup Condition="'$(GodotTargetPlatform)'!='windows-editor'">`
    - `  <DefineConstants>$(DefineConstants);GDUNIT4NET_API_V5</DefineConstants>`
    - `</PropertyGroup>`
  - ShimGen targets handle generated files; no manual include/exclude for `Scripts/Generated` is needed.
  - Keep the gdUnit4 editor plugin enabled in `project.godot` to ensure discovery works the same locally and on CI.

- `.runsettings` (in `ExampleProject/.runsettings`):
  - Point `GODOT_BIN` to your Godot Mono executable.
  - Use stable runtime parameters (Windows-friendly): `-d -v --headless --audio-driver Dummy --rendering-driver opengl3 --screen 0`
    - Avoid `--quit` and DAP/LSP flags in test runs; they can break the test adapter’s handshake.
  - Increase compile timeout for editor-driven rebuilds and disable “no tests” as error for smoother CI/local runs.

Example excerpt from a working `.runsettings`:

- GODOT_BIN set to your local Godot 4.5 Mono executable path
- Parameters: `-d -v --headless --audio-driver Dummy --rendering-driver opengl3 --screen 0`
- Capture standard output/logs enabled
- Extended timeouts for initial editor-driven compile

### Run tests

```powershell
dotnet test ExampleProject/FsharpWithShim.csproj -c Debug
```

The Godot editor can remain running; transient "Failed to bind socket. Error: 3." messages during rebuild are expected and harmless. The `.runsettings` is picked up automatically via the csproj property.

Stability tip (Windows): enable the opt-in pre-test cleanup that terminates only stale testhost processes belonging to this project:

```powershell
dotnet test ExampleProject/FsharpWithShim.csproj -c Debug /p:GdUnitKillStaleTestHosts=true
```

This is provided by the package’s buildTransitive targets and runs just before VSTest. For details, see `ShimGen/buildTransitive/Headsetsniper.Godot.FSharp.ShimGen.targets`.

## F# test shims (Tests mode)

You can author your gdUnit4 tests in F# and have the generator emit C# wrapper classes ("test shims") that the gdUnit4 C# adapter discovers.

### Why

gdUnit4’s .NET adapter scans C# test assemblies for classes attributed with `[TestSuite]`. F# test code compiles to IL, but placing tests directly in the gameplay assembly can cause circular references or discovery gaps. The Tests mode creates a second C# project containing only generated forwarding classes; each wrapper reflects an F# suite’s methods and invokes them via reflection (Tasks awaited synchronously).

### Setup pattern

1. Create an F# test project (e.g. `FSharp.Tests`) containing your F# gdUnit4 tests:

```fsharp
open GdUnit4
[<TestSuite>]
type MathTests() =
   [<BeforeTest>] member _.Setup() = ()
   [<TestCase>]  member _.Adds() = ()
   [<AfterTest>] member _.Teardown() = ()
```

2. Create a C# project (e.g. `TestShims`) that:

- References the F# test project.
- References (or imports locally) `Headsetsniper.Godot.FSharp.ShimGen` targets.
- Sets `FSharpShimsMode=Tests` and points `FSharpShimsOutDir` at `Scripts/GeneratedTests` (you can move it elsewhere if needed; the default matches this path).
- Optionally sets `FSharpShimsTestAssemblyName` when you need to override the default (we auto-pick the first referenced F# assembly whose name ends with `Tests`).

Minimal `FsharpWithShim.TestShims.csproj` example:

```xml
<Project Sdk="Godot.NET.Sdk/4.5.0">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- Enable Tests mode and separate output folder for generated test shims -->
    <FSharpShimsMode>Tests</FSharpShimsMode>
    <FSharpShimsOutDir>Scripts/GeneratedTests</FSharpShimsOutDir>
  </PropertyGroup>

  <ItemGroup>
    <FSharpShimsOutDir>Scripts/GeneratedTests</FSharpShimsOutDir>
    <ProjectReference Include="..\..\FSharp.Tests\FSharp.Tests.fsproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- The generator that emits C# test shims this project compiles -->
    <PackageReference Include="Headsetsniper.Godot.FSharp.ShimGen" Version="$(ShimGenPackageVersion)" />
  </ItemGroup>
</Project>
```

`$(ShimGenPackageVersion)` comes from the shared props emitted by the template. If you are wiring this up manually, replace it with a NuGet expression such as `0.10.*` or a pinned version like `0.10.2`.

3. Build the C# project. The shared buildTransitive target invokes ShimGen in `Tests` mode and emits one shim per discovered suite: `SuiteName_TestsShim.cs`. No extra MSBuild targets or manual generator calls are required, and the generator automatically focuses on the first referenced `*.Tests` assembly unless you override `FSharpShimsTestAssemblyName`.
4. Run `dotnet test` on the C# project; gdUnit4 discovers the shim classes (they have `[TestSuite]`). Each shim method obtains a `MethodInfo` on the F# implementation instance and invokes it (awaiting Tasks).

The targets also emit a ready-to-use `Run-GodotTests.ps1` script next to your test shim project (enabled by default). Override `FSharpShimsTestsScriptPath` to place it elsewhere, or disable generation via `FSharpShimsTestsScriptEnabled=false` if you prefer to supply your own runner.

### Discovery heuristics

An F# type is treated as a test suite if ANY of the following are true:

- Has `[TestSuite]` attribute (full name match or short name).
- Assembly name ends with `.Tests` AND the type name ends with `Tests`.
- Assembly name ends with `.Tests` AND it has at least one method whose name contains `Test` OR has an attribute whose type name ends with `TestCaseAttribute`.

Compiler generated F# artifacts (names starting with `<` or `$`) are ignored.

### Generated shim shape

```
namespace GeneratedTests;
[TestSuite]
public class MySuite_TestsShim {
  private readonly SampleTests.MySuite _impl = new();
  [BeforeTest] public void Setup() { /* reflection invoke */ }
  [TestCase]  public void Adds() { /* reflection invoke (await Task) */ }
  [AfterTest] public void Teardown() { /* reflection invoke */ }
}
```

### MSBuild properties (Tests mode)

| Property                          | Purpose                                                      | Default                                                                        |
| --------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------ |
| `FSharpShimsMode`                 | Set to `Tests` to enable test shim generation                | `Scripts`                                                                      |
| `FSharpShimsOutDir`               | Output directory for generated test shims                    | `Scripts/Generated` (or `Scripts/GeneratedTests` when `FSharpShimsMode=Tests`) |
| `FSharpShimsTestAssemblyName`     | Restrict scanning to a specific F# test assembly base name   | Auto-detects first referenced `*.Tests`                                        |
| `FSharpShimsVerbose`              | Verbose logging (`[shimgen]`)                                | `false`                                                                        |
| `FSharpShimsRegenerate`           | Forwarded to `SHIMGEN_REGENERATE_SCRIPTS` (see regeneration) | (empty)                                                                        |
| `FSharpShimsTestsScriptEnabled`   | Emit `Run-GodotTests.ps1` alongside the test shim project    | `true`                                                                         |
| `FSharpShimsTestsScriptPath`      | Destination path for the emitted PowerShell runner           | `$(MSBuildProjectDirectory)\Run-GodotTests.ps1`                                |
| `FSharpShimsTestsScriptOverwrite` | Force regeneration of the script even when it already exists | `false`                                                                        |

### Environment variables

| Variable                                | Meaning                                           |
| --------------------------------------- | ------------------------------------------------- |
| `SHIMGEN_MODE=Tests`                    | Forces Tests mode (also set via MSBuild property) |
| `SHIMGEN_REGENERATE_SCRIPTS=all`        | Regenerate all shims in place                     |
| `SHIMGEN_TEST_SUITES=Pattern1,Pattern2` | (Planned) Filter to matching suite names          |

### Caching (planned)

Future optimization: skip regeneration when all existing shim files are newer than the F# test assembly timestamp unless `SHIMGEN_REGENERATE_SCRIPTS` is set. This keeps incremental test runs fast.

### Troubleshooting

| Symptom                                                     | Likely Cause                                                            | Fix                                                                  |
| ----------------------------------------------------------- | ----------------------------------------------------------------------- | -------------------------------------------------------------------- |
| No `[shimgen] mode=Tests` log                               | Property/Env not set or older ShimGen DLL                               | Rebuild ShimGen; ensure `FSharpShimsMode=Tests` before `CoreCompile` |
| Shim file generated then disappears                         | Pruning (disabled in Tests mode in recent versions)                     | Update package / ensure pruning disabled for Tests                   |
| `$Name_TestsShim.cs` invalid file                           | Compiler generated F# nested type got through                           | Update package (filter added)                                        |
| Duplicate gdUnit4 warnings                                  | Including adapter sources twice                                         | Condition inclusion or disable addon sources in TestShims            |
| Crash (AccessViolation) running TestShims via `dotnet test` | Godot engine not initialized; native ResourceLoader static ctor invoked | Use headless Godot runner script or run through Godot CLI            |

### Headless Godot test runs

Engine-driven F# gdUnit4 tests (those that touch APIs requiring an initialized engine: `ResourceLoader`, scene loading, nodes) must execute under a Godot process. Running `dotnet test` directly on `FsharpWithShim.TestShims.csproj` loads the shim assembly in a plain test host and can crash with an access violation.

Use the helper script added in `ExampleProject/TestShims/Run-GodotTests.ps1` (the template emits the same script under `TestShims/Run-GodotTests.ps1`):

```powershell
cd ExampleProject/TestShims
./Run-GodotTests.ps1 -Configuration Debug -GodotBin "C:\Path\To\Godot.exe"
```

Parameters:

- `-Configuration` (default Debug)
- `-GodotBin` path to Godot Mono executable (falls back to `$env:GODOT_BIN` or `Godot/godot.exe` under repo root)
- `-SkipBuild` to reuse existing build
- `-Quiet` suppresses detailed per-suite/test output (verbose is the default)
- `-ShowWindow` runs tests with a window (default is headless with Dummy audio)
- `-CleanupOnly` kills stale `godot`/`testhost`/`vstest` processes and exits (no build/run)

What the script does:

1. Builds the TestShims project (unless `-SkipBuild`).
2. Locates `FsharpWithShim.TestShims.dll` under `.godot/mono/temp/bin/<Configuration>`.
3. Launches Godot with the gdUnit4 runner (`res://addons/gdUnit4/runners/GdUnit4.dll -a`) in headless mode by default or windowed with `-ShowWindow`.
4. Kills stale `godot`/`testhost`/`vstest` processes pre/post run, streams output, and summarizes the latest gdUnit4 XML report.

Planned: suite filtering via `SHIMGEN_TEST_SUITES` or a script parameter that maps to gdUnit4 `-suites=...` argument.

Recommendation: reserve `dotnet test ShimGen.Tests` for generator tests; use the headless script (or direct Godot CLI) for gameplay tests.

Windowed test runs (useful for diagnostics):

```powershell
cd ExampleProject/TestShims
./Run-GodotTests.ps1 -GodotBin "C:\Path\To\Godot.exe" -ShowWindow
```

Cleanup only (then run your own dotnet test):

```powershell
cd ExampleProject/TestShims
./Run-GodotTests.ps1 -CleanupOnly
dotnet test
```

### Source control

Do not commit `Scripts/GeneratedTests`; treat them like normal build outputs. Regenerate deterministically on CI.

## Troubleshooting

- Icon doesn’t show in the editor
  - Ensure `Icon` points to a valid Godot resource path (e.g., `res://icon.svg`) and the asset is imported by Godot.
- Generated files aren’t picked up by the build
  - The generator runs before `CoreCompile` and includes `Scripts/Generated/**/*.cs` at evaluation. Check build output for `[shimgen]` logs; verify the package is installed in the Godot C# project (not the F# one).
- Autoconnect didn’t wire my signal
  - Confirm the `Path` resolves (node exists). The shim uses `GetNodeOrNull` and skips if missing. Ensure your method parameters match the signal’s signature.
- Tests can’t locate assemblies
  - If running tests outside the repo, ensure the stub Godot types are used only within the test project; no runtime Godot dependency is required for generation-time tests.

## Roadmap

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

  - Node: \_EnterTree, \_Ready, \_ExitTree, \_Process, \_PhysicsProcess, \_Notification (parity ensured). V
  - Input/UI: \_Input, \_UnhandledInput, Control.ShortcutInput, Control.GuiInput, drag/drop (CanDropData/GetDragData/DropData). V
  - Drawing: \_Draw forwarding and helper surface hook if applicable V
  - Editor: support editor-only callbacks when [Tool] is set. V

- RPC / Multiplayer

  - RPC methods: F# attribute covering Godot 4 RPC options (CallLocal, TransferMode, Channel, AnyPeer/Authority, Reliable/Unreliable); emit [Rpc] on shim methods.
  - Sync variables: attribute to replicate exported properties (MultiplayerSynchronizer or property RPC).

- NodePath auto-wiring / onready

  - Node references: F# [NodePath]/[Node] attributes; resolve/capture typed nodes in \_Ready with validation and friendly errors. V
  - Preload: attribute for preloading PackedScene/Resource fields (editor/runtime-safe). V

- Type mapping and marshalling

  - F# types: Option<'T>, Result<'T,'E>, tuples, records, discriminated unions; define export/serialization strategy and runtime invocation mapping.
  - Collections: smooth interop for F# array/list/map with Godot.Collections.Array/Dictionary where appropriate.

- Resources and custom types

  - Custom Resources: allow F# classes to inherit Resource; support [GlobalClass] V and exports within resources.
  - Script icons/editor meta: allow icon and editor metadata decoration from F#. V

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

  - Cookbook: examples for exports with hints, signals, RPC, NodePath wiring, tool scripts, resources. V
  - Templates: ready-to-use Godot+F# project template using this package.

- Test coverage
  - Add tests for export hints V and types V, UI callbacks V, RPC attributes/invocation, NodePath wiring V, Option/DU/records marshalling, resources/global V classes, tool scripts behavior V, autoconnect V.
  - Cross-platform: validate generation on Windows/Linux/macOS.

## Local development

Most users only need the NuGet packages (`Headsetsniper.Godot.FSharp.Annotations`, `Headsetsniper.Godot.FSharp.ShimGen`, and `Headsetsniper.Godot.FSharp.Templates`). The steps below are for contributors iterating on this repository or building custom packages locally.

These steps help when you want to iterate on the packages locally before publishing.

### Build local packages

```powershell
# From the repo root
dotnet pack Annotations\Headsetsniper.Godot.FSharp.Annotations.csproj -c Release
dotnet pack ShimGen\Headsetsniper.Godot.FSharp.ShimGen.csproj -c Release
mkdir -Force .nupkgs
Copy-Item Annotations\bin\Release\*.nupkg .nupkgs\
Copy-Item ShimGen\bin\Release\*.nupkg .nupkgs\
```

### Local NuGet feed

The solution-level `NuGet.Config` already points to `.nupkgs`. If you need to recreate it, use:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="local" value="$(SolutionDir).nupkgs" />
  </packageSources>
</configuration>
```

### Build the sample solution

```powershell
dotnet restore FsharpWithShim.csproj
dotnet build FsharpWithShim.csproj -v:n
```

During this build the `GenerateFSharpShims` target scans referenced F# projects and writes shims into `Scripts/Generated`, mirroring your F# folder structure. Use the console runner’s `--dry-run` switch to preview file moves.

### Run the test suite

```powershell
dotnet test ShimGen.Tests/ShimGen.Tests.csproj -c Debug
```

### Pack release artifacts

```powershell
dotnet pack Annotations -c Release
dotnet pack ShimGen -c Release
```
