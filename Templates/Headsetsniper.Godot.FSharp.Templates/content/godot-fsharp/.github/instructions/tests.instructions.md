```instructions
---
applyTo: "Tests/**"
---

# gdUnit4 Specs

- Follow Arrange/Act/Assert with blank lines; keep each spec focused on a single behavior of your gameplay scripts.
- Mirror the module and function names found under `Scripts/` so the generated test shims can discover matching APIs.
- Add every new `.fs` file to the `<Compile Include="..." />` list in `MyGodotFSharp.Tests.fsproj`; file order controls scenario execution.
- Reference helper modules through normal F# `open` statements instead of reaching into generated shims—those are exercised via the TestShims harness.
- Run specs from the command line with `TestShims/Run-GodotTests.ps1 -Configuration Debug -GodotBin <path-to-godot>` once ShimGen regenerates the test adapters.
- Store reusable gdUnit4 data under `Specs/` to keep the generated harness lean and deterministic.

```
