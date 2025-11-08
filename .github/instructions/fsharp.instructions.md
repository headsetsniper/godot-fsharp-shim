---
applyTo: "FSharp/**,FSharp.Tests/**"
---

# F# Gameplay Patterns

- Keep shared libraries F#-only; the generator emits the C# shims.
- Reference `Headsetsniper.Godot.FSharp.Annotations` and decorate runtime types with `[<GodotScript(...)]` so the generator can find them.
- Use constructor injection for runtime nodes; implement `IGdToolScript<'TNode>` only on `[<GodotTool>]` types that need the node injected during `_Ready()`.
- Express wiring with `[<NodePath>]`, `[<OptionalNodePath>]`, `[<AutoConnect>]`, `[<Preload>]`, and `[<Export>]`; non-option NodePaths are required, `Option` fields stay optional, and `[<Preload>]` fails fast while assigning `Some` for option resources.
- Model updates through compact state records and compose pure steps (`applyMove`, `applyRotate`, `applyHardDrop`) before mutating Godot nodes, splitting rendering into small, purpose-named helpers.
- Order functions from most to least important, follow IOSP with helpers at the same abstraction level, and prefer well-named functions over comments or pass-through wrappers.
- Tool scripts should call `QueueRedraw()` in `Ready`, rely on `Ready/Process` (not `EnterTree`) for node injection, anchor Control nodes to `FullRect` with zero offsets, apply a faint board background, and paint empty grid cells light grey for good in-editor visibility.
- Boards and views should communicate through exported values (strings, flags, ints) so dependent scripts can read them each `_Process` and self-redraw.
- Use `[<AutoConnect(path, "signal")>]` for signal handlers instead of manual `Connect`; rely on the generated shim to wire them.
- Avoid reflection in gameplay; prefer typed APIs or `Node.Set` with implicit Variant conversions.
- Centralize timing through relay nodes that `[<AutoConnect>]` timer signals and re-emit custom ticks instead of scattering timers.
- When exporting encoded values, follow the existing `'|'`-delimited string format so tool scripts can parse them consistently.
- Add new signals via `[<AutoConnect>]` or `AddUserSignal`, then confirm the generated C# shim emits/handles them before scene wiring.
- Keep deterministic or test-specific behaviors configurable through exported text rather than hard-coded logic.
- Keep tests feature-focused in AAA format with blank lines between arrange/act/assert phases.
- Re-read tests before committing to keep them concise, readable, and DRY.
