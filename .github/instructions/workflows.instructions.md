---
applyTo: "**"
---

# Build & Test Workflow

- Build the sample project with `dotnet build ExampleProject/FsharpWithShim.csproj -c Debug`; this regenerates shims.
- Run core tests via `dotnet test ShimGen.Tests/ShimGen.Tests.csproj -c Debug`; prefer `dotnet test` over IDE Test tasks to avoid timeout issues.
- Execute end-to-end checks with `ExampleProject/TestShims/Run-GodotTests.ps1 -Configuration Debug -GodotBin <path>`; it builds TestShims and runs gdUnit4 headless.
- Pack local NuGets with `dotnet pack Annotations/Headsetsniper.Godot.FSharp.Annotations.csproj -c Release` and `dotnet pack ShimGen/Headsetsniper.Godot.FSharp.ShimGen.csproj -c Release` when preparing packages.
- Simulate CI locally using `act -P ubuntu-latest=catthehacker/ubuntu:act-latest -j build-test-pack` before pushing.
- Push changes to GitHub and monitor CI results once commands succeed locally.
