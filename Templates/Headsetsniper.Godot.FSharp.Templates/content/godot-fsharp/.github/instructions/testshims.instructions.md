```instructions
---
applyTo: "TestShims/**"
---

# Test Shim Harness

- `MyGodotFSharp.TestShims.csproj` references `Headsetsniper.Godot.FSharp.ShimGen` with `<FSharpShimsMode>Tests</FSharpShimsMode>` so it emits adapters under `GeneratedTests/` for gdUnit4.
- Run `dotnet build TestShims/MyGodotFSharp.TestShims.csproj -c Debug` after changing specs or gameplay to refresh the generated test shims.
- Use `Run-GodotTests.ps1 -Configuration Debug` to execute gdUnit4 suites headlessly; the script builds both Tests and TestShims, resolves `GODOT_BIN` from `.runsettings`, and runs Godot from the folder above `TestShims` so `project.godot` and `addons/gdUnit4` must live there.
- Keep `GeneratedTests/**` out of source control; delete them if they drift and rebuild to regenerate.
- Update the gdUnit4 package versions here in lockstep with `Tests/` whenever you upgrade to a newer release.
- If the generator cannot resolve your F# assembly, confirm the `ProjectReference` to `Tests/MyGodotFSharp.Tests.fsproj` still has `ReferenceOutputAssembly=true`.

```
