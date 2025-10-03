# AI agent guide for this repo (godot-fsharp-shim)

This repo lets you write Godot gameplay in F# and auto-generate C# shims that Godot recognizes.

- Annotations (`Annotations/`): attributes and interfaces for F# (e.g., `GodotScriptAttribute`, `IGdScript<'TNode>`). NuGet: `Headsetsniper.Godot.FSharp.Annotations`.
- Shim generator (`ShimGen/`): console runner + buildTransitive targets that generate shims into `Scripts/Generated`. NuGet: `Headsetsniper.Godot.FSharp.ShimGen`.
- Example projects: `FSharp/` (logic) + `ExampleProject/` (Godot C# consumer).

## How shims are generated (big picture)

- MSBuild targets (buildTransitive) at `ShimGen/buildTransitive/*.targets` drive generation:
  - Pipeline: `ResolveShimGenToolPath` → `CollectFSharpOutputs` → `RunShimGen` (BeforeTargets `CoreCompile;Compile`).
  - Collects F# assemblies from `@(ReferencePath)` entries whose `MSBuildSourceProjectFile` ends with `.fsproj`.
  - Executes: `dotnet <ShimGen.dll> <fs-assembly> <out-dir> <fsproj-dir>`.
  - Includes `Scripts/Generated/**/*.cs` as Compile items in the same build and removes them on `Clean`.
- Tool resolution is deterministic on CI/local:
  - Prefer exact NuGet cache path: `<nuget-root>/headsetsniper.godot.fsharp.shimgen/<version>/lib/<tfm>/Headsetsniper.Godot.FSharp.ShimGen.dll`.
  - Fallbacks: repo local `ShimGen/bin/<Configuration>/<TFM>/...` or `lib/<TFM>` next to the targets file.

## Authoring F# gameplay (pattern)

- Reference `Headsetsniper.Godot.FSharp.Annotations` in your F# project.
- Decorate a class: `[<GodotScript(ClassName = "Foo", BaseTypeName = "Godot.Node2D")>]`.
- Optionally implement `IGdScript<Node2D>`; the shim sets `Node` in `_Ready()` then calls your `Ready()`.
- In the Godot C# project: add a ProjectReference to the F# project and a PackageReference to `Headsetsniper.Godot.FSharp.ShimGen`. No extra targets/imports needed.

## Developer workflows (commands)

- Build example project:
  - `dotnet build ExampleProject/FsharpWithShim.csproj -c Debug`
- Run tests:
  - `dotnet test ShimGen.Tests/ShimGen.Tests.csproj -c Debug`
- Pack local nupkgs (manual dev flow):
  - `dotnet pack Annotations/Headsetsniper.Godot.FSharp.Annotations.csproj -c Release`
  - `dotnet pack ShimGen/Headsetsniper.Godot.FSharp.ShimGen.csproj -c Release`
- CI locally with act:
  - `act -P ubuntu-latest=catthehacker/ubuntu:act-latest -j build-test-pack`
- Push to GitHub and monitor CI results.

## Conventions and gotchas

- Use dotnet test and not the Test task as copilot seems to struggle with waiting for long tests.
- No C# support for the library that has the source types. Like `GodotScriptAttribute`. Only F# on that side and C# as generation output!
- Do not commit files under `Scripts/Generated` (they are build outputs).
- Godot SDK: projects use `Sdk="Godot.NET.Sdk/4.5.0"` with `net8.0`.
- You’ll see `[shimgen]` build logs that show tool path candidates and F# ReferencePath discovery.
- Write Code in a functional manner. Adhere to IOSP and Same Level of Abstraction principles. But dont write pure Pass Through functions. Avoid writing comments, if it can also be accomplished with a well named function.
- Order methods in a class from most to least important. Try putting the main method first and then order by proximity to the main method and when its used.
- Keep tests in a AAA structure. Arrange, Act, Assert. Use blank lines to separate these sections.
- Always make another pass over generated Tests to ensure they are readable, dry and maintainable.
- Tests should read like different features of this repo.

## Key files/directories

- `Annotations/GodotScriptAttribute.cs`, `Annotations/IGdScript.cs`
- `ShimGen/buildTransitive/Headsetsniper.Godot.FSharp.ShimGen.targets`
- `ShimGen/Program.cs`, `ShimGen/ScriptSpec.cs`
- `ShimGen.Tests/` (integration tests for generator behavior)
- `ExampleProject/FsharpWithShim.csproj` (consumer wiring)

If any of this is unclear (e.g., export hints or RPC support expectations), leave a note and we’ll extend this guide with concrete repo examples.
