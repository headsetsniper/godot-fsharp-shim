using System;

namespace Headsetsniper.Godot.FSharp.Annotations;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class OptionalNodePathAttribute : Attribute
{
    public string? Path { get; init; }
}
