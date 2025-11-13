```instructions
---
applyTo: "Scripts/**"
---

# Gameplay Scripts

- Replace `GameScript.fs` with your own modules and types and decorate runtime entry points with `[<GodotScript>]` so shim generation exposes them to Godot.
- Use `[<NodePath>]` and `[<OptionalNodePath>]` to describe scene wiring; let the generated shim validate required nodes and provide option values for optional ones.
- Add new `.fs` files to the `<Compile Include="..." />` list in `MyGodotFSharp.fsproj` to control F# compilation order.
- Keep most logic inside pure helpers (such as `ClickCounterLogic`) and call them from the Godot-facing members for easier testing.
- Build with `dotnet build Scripts/MyGodotFSharp.fsproj -c Debug` during iteration to catch F# errors quickly before regenerating shims.
- Treat `Scripts/Generated/**` as transient output from ShimGen; delete or regenerate rather than editing them directly.

```
