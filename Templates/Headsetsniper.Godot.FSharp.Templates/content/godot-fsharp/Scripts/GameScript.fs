namespace MyGodotFSharp

open Godot
open Headsetsniper.Godot.FSharp.Annotations

[<RequireQualifiedAccess>]
module ClickCounterLogic =
    let labelText count =
        if count <= 0 then
            "Click anywhere on this control."
        else
            $"Clicks registered: {count}"

[<GodotScript(ClassName = "ClickCounterControl", BaseTypeName = "Godot.Control")>]
type ClickCounterControl(node: Control) =
    let mutable owner = node
    let mutable clicks = 0

    [<OptionalNodePath(Path = "ClickLabel")>]
    member val ClickLabel: Label option = None with get, set

    member private this.EnsureLabel() =
        match this.ClickLabel with
        | Some label -> label
        | None ->
            let created = new Label()
            created.Name <- "ClickLabel"
            created.HorizontalAlignment <- HorizontalAlignment.Center
            created.VerticalAlignment <- VerticalAlignment.Center
            created.SetAnchorsPreset(Control.LayoutPreset.FullRect)
            owner.AddChild(created)
            this.ClickLabel <- Some created
            created

    member private this.UpdateLabel() =
        let label = this.EnsureLabel()
        label.Text <- ClickCounterLogic.labelText clicks

    member this.GuiInput(event: InputEvent) =
        match event with
        | :? InputEventMouseButton as mouse when mouse.Pressed && mouse.ButtonIndex = MouseButton.Left ->
            clicks <- clicks + 1
            this.UpdateLabel()
        | _ -> ()

    member this.Ready() =
        owner.MouseFilter <- Control.MouseFilterEnum.Stop
        this.UpdateLabel()
