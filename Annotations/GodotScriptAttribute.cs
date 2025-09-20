using System;

namespace Headsetsniper.Godot.FSharp.Annotations;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GodotScriptAttribute : Attribute
{
    public string? ClassName { get; init; }
    public string BaseTypeName { get; init; } = "Godot.Node";
}
