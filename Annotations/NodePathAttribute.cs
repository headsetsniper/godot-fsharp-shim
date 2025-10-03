using System;

namespace Headsetsniper.Godot.FSharp.Annotations;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NodePathAttribute : Attribute
{
    public string? Path { get; init; }
    public bool Required { get; init; } = true;
}
