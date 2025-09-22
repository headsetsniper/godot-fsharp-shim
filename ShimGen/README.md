# Headsetsniper.Godot.FSharp.ShimGen

Generates C# shims for F# Godot scripts so Godot's C# tooling can see them.

## Install

In your Godot C# project (net8.0 with Godot 4.5 SDK):

- Add PackageReference: `Headsetsniper.Godot.FSharp.ShimGen`
- Add ProjectReference(s) to your F# logic project(s) that use `Headsetsniper.Godot.FSharp.Annotations`.

That's it. No extra targets, imports, or ItemGroups needed. On build:

- We resolve your F# project outputs from the normal `ReferencePath` (after `ResolveReferences`).
- We run the generator with `dotnet <ShimGen.dll> <fs-assembly> <out-dir> <fsproj-dir>`.
- We include `Scripts/Generated/**/*.cs` in the same build so the shims compile immediately.
  The generator mirrors the F# folder structure, follows moves/renames, and prunes orphaned shims.

## Configuration

- `FSharpShimsEnabled` (default `true`): set to `false` to disable generation.
- `FSharpShimsOutDir` (default `$(MSBuildProjectDirectory)\Scripts\Generated`): change output directory.
- Console runner flags: `--dry-run` to preview planned writes/moves/deletes without applying.

## How it works

- A buildTransitive target pipeline runs: `ResolveShimGenToolPath` → `CollectFSharpOutputs` → `RunShimGen`.
- It finds F# assemblies via `@(ReferencePath)` entries whose `MSBuildSourceProjectFile` ends with `.fsproj`.
- It runs before `CoreCompile` so generated files are picked up in the same build.
- The generated shim will set `IGdScript<TNode>.Node` in `_Ready()` before invoking your F# `Ready()`.
