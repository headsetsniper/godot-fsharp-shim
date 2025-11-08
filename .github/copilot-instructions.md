# Godot F# Shim Essentials

- This repo turns F# gameplay code into C# shims that Godot 4.5 can load; `FSharp/` holds logic and `ExampleProject/` consumes it.
- Build with `dotnet build ExampleProject/FsharpWithShim.csproj -c Debug`; run core tests via `dotnet test ShimGen.Tests/ShimGen.Tests.csproj -c Debug` or `ExampleProject/TestShims/Run-GodotTests.ps1 -Configuration Debug -GodotBin <path>`.
- Keep code functional-first, avoid pass-through helpers, order methods by importance, and write AAA tests with blank lines between arrange/act/assert.
- Never commit anything under `Scripts/Generated`; regenerate via the ShimGen targets when needed.
- See the topic files under `.github/instructions/*.instructions.md` for area-specific guidance.
- Assume the terminal is already positioned at the repository root—run commands directly without prepending extra `cd` segments unless you want it to be non-root.
- Shell commands should always run with line numbering enabled(`grep, tail, sed, etc.`).
- When debugging third-party behavior, search online docs or the package's GitHub source before disassembling.
