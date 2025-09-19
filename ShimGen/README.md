# Godot.FSharp.ShimGen

Generates C# shims for F# Godot scripts so Godot's C# tooling can see them.

## Install

In your Godot C# project (net8.0 with Godot 4.5 SDK):

- Add PackageReference: `Godot.FSharp.ShimGen`
- Add ProjectReference(s) to your F# logic project(s) that use `Godot.FSharp.Annotations`.

That's it. On build, shims are generated into `Scripts/Generated` and compiled into your project.

## Configuration

- `FSharpShimsEnabled` (default `true`): set to `false` to disable generation.
- `FSharpShimsOutDir` (default `$(MSBuildProjectDirectory)\Scripts\Generated`): change output directory.

## How it works

- A buildTransitive target runs before `CoreCompile` after `ResolveReferences`.
- It finds F# project references by their `.fsproj` extension, resolves their TargetPath, and runs the ShimGen runner.
- It includes `Scripts/Generated/**/*.cs` in the compile items at evaluation time so the files are compiled.
