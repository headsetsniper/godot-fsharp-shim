# Templates

This folder contains the `Headsetsniper.Godot.FSharp.Templates` package. Install it locally to scaffold a new gameplay project:

```powershell
# From the repo root
dotnet new install Templates/Headsetsniper.Godot.FSharp.Templates

# Create a new project
mkdir MyGame
cd MyGame
dotnet new godot-fsharp --includeTests true --GodotBinPath "C:\\Path\\To\\Godot.exe"
```

The template exposes three parameters:

- `--annotationsVersion` (default `0.*`): version expression for the `Headsetsniper.Godot.FSharp.Annotations` package.
- `--includeTests` (default `false`): generate the gdUnit4 test project alongside the gameplay project.
- `--GodotBinPath` (default `SET_ME`): absolute path to the Godot Mono executable written into the generated `.runsettings` files.

When tests are included, either pass `--GodotBinPath` at template creation time or set `GODOT_BIN` in your environment so the generated run script can find the editor before running `dotnet test`.
