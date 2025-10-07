using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class RoslynCodeGenerator
{
    private static IEnumerable<MemberDeclarationSyntax> BuildUiAndCanvasMembers(ScriptSpec spec)
    {
        bool IsControl() => spec.BaseTypeName.EndsWith(".Control", StringComparison.Ordinal);
        bool IsCanvasItem() => spec.BaseTypeName.EndsWith(".CanvasItem", StringComparison.Ordinal) || spec.BaseTypeName.EndsWith(".Node2D", StringComparison.Ordinal) || spec.BaseTypeName.EndsWith(".Control", StringComparison.Ordinal);
        var list = new List<MemberDeclarationSyntax>();

        MethodDeclarationSyntax Make(string returnType, string name, string expr, params (string type, string id)[] parms)
            => BuildExprOverride(returnType, name, expr, parms);

        if (IsControl() && spec.HasGuiInput) list.Add(Make("void", "_GuiInput", "_impl.GuiInput(@event)", ("Godot.InputEvent", "@event")));
        if (IsControl() && spec.HasShortcutInput) list.Add(Make("void", "_ShortcutInput", "_impl.ShortcutInput(@event)", ("Godot.InputEvent", "@event")));
        if (IsControl() && spec.HasUnhandledKeyInput) list.Add(Make("void", "_UnhandledKeyInput", "_impl.UnhandledKeyInput(@event)", ("Godot.InputEvent", "@event")));
        if (IsCanvasItem() && spec.HasDraw) list.Add(Make("void", "_Draw", "_impl.Draw()"));
        if (IsControl() && spec.HasCanDropData) list.Add(Make("bool", "_CanDropData", "_impl.CanDropData(atPosition, data)", ("Godot.Vector2", "atPosition"), ("Godot.Variant", "data")));
        if (IsControl() && spec.HasDropData) list.Add(Make("void", "_DropData", "_impl.DropData(atPosition, data)", ("Godot.Vector2", "atPosition"), ("Godot.Variant", "data")));
        if (IsControl() && spec.HasGetDragData) list.Add(Make("Godot.Variant", "_GetDragData", "(Godot.Variant)_impl.GetDragData(atPosition)", ("Godot.Vector2", "atPosition")));
        if (IsControl() && spec.HasHasPoint) list.Add(Make("bool", "_HasPoint", "_impl.HasPoint(position)", ("Godot.Vector2", "position")));
        if (IsControl() && spec.HasGetMinimumSize) list.Add(Make("Godot.Vector2", "_GetMinimumSize", "_impl.GetMinimumSize()"));
        if (IsControl() && spec.HasMakeCustomTooltip) list.Add(Make("Godot.Control", "_MakeCustomTooltip", "_impl.MakeCustomTooltip(forText)", ("string", "forText")));
        if (IsControl() && spec.HasGetTooltip) list.Add(Make("string", "_GetTooltip", "_impl.GetTooltip(atPosition)", ("Godot.Vector2", "atPosition")));

        return list;
    }
}
