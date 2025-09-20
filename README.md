# F# with Godot via C# Shims

This repository demonstrates how to drive Godot game logic in F# while generating on-disk C# shims that Godot can consume.

## Projects

- `Annotations` (NuGet: Headsetsniper.Godot.FSharp.Annotations)
  - Provides `[GodotScript]` attribute used in F#.
- `FSharp`
  - Your F# gameplay logic referencing `Annotations`.
- `ShimGen` (NuGet: Headsetsniper.Godot.FSharp.ShimGen)
  - Console runner + MSBuild buildTransitive target to generate shims into `Scripts/Generated`.
- `Scenes`, `Scripts`
  - Your Godot project code.

## Using the packages in your own project

1. In your F# project:

- Install `Headsetsniper.Godot.FSharp.Annotations`.
- Annotate classes with:
  - `[<GodotScript(ClassName = "Foo", BaseTypeName = "Godot.Node2D")>]`

2. In your Godot C# project:

- Install `Headsetsniper.Godot.FSharp.ShimGen`.
- Add a `ProjectReference` to your F# project(s).
- Build. Shims appear under `Scripts/Generated` and are compiled.

## Configuration knobs

- `FSharpShimsEnabled` (true by default)
- `FSharpShimsOutDir` (default `Scripts/Generated`)

## Local development flow

1. Pack the two NuGet packages locally

- Annotations: provides the `[GodotScript]` attribute
- ShimGen: buildTransitive target that auto-runs the shim generator and includes `Scripts/Generated/**/*.cs` at evaluation

```powershell
# From the repo root
dotnet pack Annotations\Headsetsniper.Godot.FSharp.Annotations.csproj -c Release
dotnet pack ShimGen\Headsetsniper.Godot.FSharp.ShimGen.csproj -c Release
mkdir -Force .nupkgs
Copy-Item Annotations\bin\Release\*.nupkg .nupkgs\
Copy-Item ShimGen\bin\Release\*.nupkg .nupkgs\
```

2. Use the included NuGet.Config

A solution-level `NuGet.Config` is included that adds a local source at `.nupkgs` alongside nuget.org. If you need to re-create it, it should look like:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="local" value="$(SolutionDir).nupkgs" />
  </packageSources>
</configuration>
```

3. Build the main project

```powershell
dotnet restore FsharpWithShim.csproj
dotnet build FsharpWithShim.csproj -v:n
```

During build, the package’s `GenerateFSharpShims` target runs before `CoreCompile`, scans the referenced F# project(s), and writes shims into `Scripts/Generated`. Those shims are then included at evaluation time by the package’s buildTransitive target.

## Development

- Run tests: `dotnet test ShimGen.Tests`
- Pack packages:
  - `dotnet pack Annotations -c Release`
  - `dotnet pack ShimGen -c Release`
