---
applyTo: "ShimGen/**,ShimGen.Tests/**"
---

# ShimGen Pipeline

- BuildTransitive targets in `buildTransitive/*.targets` run before `CoreCompile;Compile` and follow `ResolveShimGenToolPath -> CollectFSharpOutputs -> RunShimGen`.
- The generator executes `dotnet <ShimGen.dll> <fs-assembly> <out-dir> <fsproj-dir>`; keep `.fsproj` outputs in `@(ReferencePath)` so collection succeeds.
- Generated files land in `Scripts/Generated` (added as `Compile` items) and are removed on `Clean`.
- Tool discovery order is NuGet cache `headsetsniper.godot.fsharp.shimgen/<version>/lib/<tfm>/...`, then local `ShimGen/bin/<Configuration>/<TFM>/...`, then `lib/<TFM>` beside the targets file.
- Prefer the locally built generator while iterating; set `SHIMGEN_REGENERATE_SCRIPTS=all` only when you must rewrite shims and close the Godot editor first to avoid file locks.
- Normalize generated files to LF on Windows, prune only when the F# source root is known, and preserve user-appended comments for idempotent writes.
- Keep `SHIMGEN_REGENERATE_SCRIPTS` cleared during test runs to avoid unexpected rewrites.
- `[shimgen]` build logs display tool-path candidates and F# reference discovery—use them when debugging generation.
- Run integration coverage with `dotnet test ShimGen.Tests/ShimGen.Tests.csproj -c Debug`.
