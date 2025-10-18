# Templates

This folder contains the `Headsetsniper.Godot.FSharp.Templates` package. Install it locally to scaffold a new gameplay project:

```powershell
# From the repo root
dotnet new install Templates/Headsetsniper.Godot.FSharp.Templates

# Create a new project
mkdir MyGame
cd MyGame
dotnet new godot-fsharp --includeTests true
```

The template exposes two parameters:

- `--annotationsVersion` (default `0.*`): version expression for the `Headsetsniper.Godot.FSharp.Annotations` package.
- `--includeTests` (default `false`): generate the gdUnit4 test project alongside the gameplay project.

When tests are included, update the generated `.runsettings` file with the path to your Godot Mono executable before running `dotnet test`.
