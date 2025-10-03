namespace Headsetsniper.Godot.FSharp.Annotations
{
    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class ExportFileAttribute : System.Attribute
    {
        public ExportFileAttribute() { }
        public ExportFileAttribute(string filter) { Filter = filter; }
        public string? Filter { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class ExportDirAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class ExportResourceTypeAttribute : System.Attribute
    {
        public ExportResourceTypeAttribute(string typeName) { TypeName = typeName; }
        public string TypeName { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class ExportMultilineAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class ExportColorNoAlphaAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class ExportEnumListAttribute : System.Attribute
    {
        public ExportEnumListAttribute(string values) { Values = values; }
        public string Values { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class ExportLayerMask2DRenderAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class ExportCategoryAttribute : System.Attribute
    {
        public ExportCategoryAttribute(string name) { Name = name; }
        public string Name { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class ExportSubgroupAttribute : System.Attribute
    {
        public ExportSubgroupAttribute(string name) { Name = name; }
        public string Name { get; }
        public string? Prefix { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class ExportTooltipAttribute : System.Attribute
    {
        public ExportTooltipAttribute(string text) { Text = text; }
        public string Text { get; }
    }
}
