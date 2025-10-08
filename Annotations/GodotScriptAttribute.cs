using System;

namespace Headsetsniper.Godot.FSharp.Annotations;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GodotScriptAttribute : Attribute
{
    // New constructor overloads to support positional args from F# usage
    public GodotScriptAttribute() { }

    public GodotScriptAttribute(string className, string baseTypeName)
    {
        ClassName = className;
        BaseTypeName = baseTypeName;
    }

    public GodotScriptAttribute(string className, string baseTypeName, string icon)
    {
        ClassName = className;
        BaseTypeName = baseTypeName;
        Icon = icon;
    }

    public string? ClassName { get; set; }
    public string BaseTypeName { get; set; } = "Godot.Node";
    // Deprecated: use [GodotTool] attribute instead. Left for backward compatibility.
    public bool Tool { get; set; } = false;
    public string? Icon { get; set; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GodotToolAttribute : Attribute
{
    public GodotToolAttribute() { }

    public GodotToolAttribute(string className, string baseTypeName)
    {
        ClassName = className;
        BaseTypeName = baseTypeName;
    }

    public GodotToolAttribute(string className, string baseTypeName, string icon)
    {
        ClassName = className;
        BaseTypeName = baseTypeName;
        Icon = icon;
    }

    public string? ClassName { get; set; }
    public string BaseTypeName { get; set; } = "Godot.Node";
    public string? Icon { get; set; }
}
