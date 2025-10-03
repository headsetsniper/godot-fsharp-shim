namespace Headsetsniper.Godot.FSharp.Annotations;

// Centralized well-known identifiers (namespace, assembly and type FullNames)
// Use these from other projects (e.g., ShimGen) to avoid magic strings.
public static class Known
{
    public static class Namespace
    {
        public const string Root = nameof(Headsetsniper) + "." + nameof(Godot) + "." + nameof(FSharp) + "." + nameof(Annotations);
    }

    public static class Assembly
    {
        // Primary assembly name for this package
        public static readonly string Name = typeof(GodotScriptAttribute).Assembly.GetName().Name!;
        // Legacy package id we still probe for (for migration scenarios)
        public const string LegacyName = "Godot.FSharp.Annotations";
    }

    public static class Types
    {
        public static readonly string GodotScriptAttribute = typeof(GodotScriptAttribute).FullName!;
        public static readonly string IGdScript = typeof(IGdScript<>).FullName!.Split('`')[0];

        public static readonly string NodePathAttribute = typeof(NodePathAttribute).FullName!;
        public static readonly string AutoConnectAttribute = typeof(AutoConnectAttribute).FullName!;

        public static readonly string ExportCategoryAttribute = typeof(ExportCategoryAttribute).FullName!;
        public static readonly string ExportSubgroupAttribute = typeof(ExportSubgroupAttribute).FullName!;
        public static readonly string ExportTooltipAttribute = typeof(ExportTooltipAttribute).FullName!;
        public static readonly string ExportRangeAttribute = typeof(ExportRangeAttribute).FullName!;
        public static readonly string ExportFileAttribute = typeof(ExportFileAttribute).FullName!;
        public static readonly string ExportDirAttribute = typeof(ExportDirAttribute).FullName!;
        public static readonly string ExportResourceTypeAttribute = typeof(ExportResourceTypeAttribute).FullName!;
        public static readonly string ExportMultilineAttribute = typeof(ExportMultilineAttribute).FullName!;
        public static readonly string ExportEnumListAttribute = typeof(ExportEnumListAttribute).FullName!;
        public static readonly string ExportColorNoAlphaAttribute = typeof(ExportColorNoAlphaAttribute).FullName!;
        public static readonly string ExportLayerMask2DRenderAttribute = typeof(ExportLayerMask2DRenderAttribute).FullName!;
    }
}
