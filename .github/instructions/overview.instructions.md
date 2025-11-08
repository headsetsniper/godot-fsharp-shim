---
applyTo: "**"
---

# Repository Overview

- F# gameplay lives in `FSharp/`, shim generation tooling in `ShimGen/`, attributes in `Annotations/`, and the Godot consumer sits under `ExampleProject/`.
- The build pipeline turns `[<GodotScript>]` F# types into C# shims under `Scripts/Generated` so Godot 4.5 can load them.
- Never commit files in `Scripts/Generated`; rerun the build or force regeneration when updates are required.
- Key references: `Annotations/GodotScriptAttribute.cs`, `Annotations/IGdToolScript.cs`, `ShimGen/buildTransitive/Headsetsniper.Godot.FSharp.ShimGen.targets`, `ShimGen/Program.cs`, `ShimGen/ScriptSpec.cs`, `ShimGen.Tests/`, and `ExampleProject/FsharpWithShim.csproj`.
- Trust generator-enforced wiring: missing nodes or resources will fail fast with clear errors, so avoid redundant null checks.
