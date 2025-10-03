using System;

namespace Headsetsniper.Godot.FSharp.Annotations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ExportRangeAttribute : Attribute
{
    public double Min { get; }
    public double Max { get; }
    public double Step { get; }
    public bool OrSlider { get; }
    public ExportRangeAttribute(double min, double max, double step = 0, bool orSlider = false)
    {
        Min = min; Max = max; Step = step; OrSlider = orSlider;
    }
}
