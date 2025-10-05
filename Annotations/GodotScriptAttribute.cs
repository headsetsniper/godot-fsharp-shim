using System;

namespace Headsetsniper.Godot.FSharp.Annotations;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GodotScriptAttribute : Attribute
{
    public string? ClassName { get; set; }
    public string BaseTypeName { get; set; } = "Godot.Node";
    public bool Tool { get; set; } = false;
    public string? Icon { get; set; }
}
