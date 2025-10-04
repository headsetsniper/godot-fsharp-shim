using System;

namespace Headsetsniper.Godot.FSharp.Annotations;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PreloadAttribute : Attribute
{
    public PreloadAttribute(string path)
    {
        Path = path ?? string.Empty;
    }

    public string Path { get; }
    public bool Required { get; init; } = false;
}
