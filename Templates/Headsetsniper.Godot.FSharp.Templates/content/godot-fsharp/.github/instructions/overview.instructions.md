```instructions
---
applyTo: "**"
---

# Godot F# Template Guide

- Add `Scripts/MyGodotFSharp.fsproj` to the solution Godot builds and reference it from the C# project that Godot loads at runtime.
- Attach `Headsetsniper.Godot.FSharp.ShimGen` to that C# project (or reuse an existing import) so builds emit shims under `Scripts/Generated`; never hand-edit those files.
- Run `dotnet build` for the Godot project whenever you change F# gameplay so ShimGen refreshes the generated C#.
- Keep the F# project reference marked `ReferenceOutputAssembly=true` to ensure ShimGen locates the compiled assembly.
- Pass `--IncludeTests true` to `dotnet new godot-fsharp` when you want gdUnit4 specs and the TestShims harness.
- Align ShimGen, gdUnit4, and Godot package versions across projects during upgrades to avoid binding conflicts.

```
