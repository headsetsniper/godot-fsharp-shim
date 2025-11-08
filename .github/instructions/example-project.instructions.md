---
applyTo: "ExampleProject/**"
---

# Example Project Notes

- Uses `Sdk="Godot.NET.Sdk/4.5.0"` targeting `net9.0`; build with `dotnet build ExampleProject/FsharpWithShim.csproj -c Debug` to regenerate shims.
- `EnableDefaultItems=false`, so add every new hand-written C# file to `FsharpWithShim.csproj` using explicit `<Compile Include="..." />` entries.
- `Scripts/Generated` is build output only—never edit or commit these files; rerun the build or generator when they need updates.
- Close the Godot editor before forcing regeneration and set `SHIMGEN_REGENERATE_SCRIPTS=all` only when you must rewrite existing shims.
- For end-to-end checks run `ExampleProject/TestShims/Run-GodotTests.ps1 -Configuration Debug -GodotBin <path>`; it builds `TestShims` and executes gdUnit4 headlessly.
- The project conditionally imports the local ShimGen targets so keep your local generator current during iteration.
- Establish the bridge by `ProjectReference` to `FSharp/FSharp.fsproj` and `PackageReference` to `Headsetsniper.Godot.FSharp.ShimGen`; no extra targets are required.
