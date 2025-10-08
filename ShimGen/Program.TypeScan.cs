using System.Reflection;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class Program
{
    private static ScriptSpec? TryCreateSpec(Type t)
    {
        var (scriptAttr, toolAttr) = ResolvePrimaryAttributes(t);
        if (scriptAttr is null && toolAttr is null) return null;
        if (scriptAttr is not null && toolAttr is not null)
            throw new InvalidOperationException($"[shimgen] {t.FullName}: Cannot apply both GodotScript and GodotTool. Choose one.");

        var meta = ExtractScriptMetadata(t, scriptAttr, toolAttr);
        var exports = CollectExportProperties(t);
        var callbacks = DetectCallbacks(t);
        var (nodePaths, preloads) = CollectNodePathsAndPreloads(t);
        var autoConnects = CollectAutoConnects(t);
        var (useCtorInjection, ctorParams) = ComputeConstructorInjection(t, meta.BaseTypeName, nodePaths, preloads, toolAttr is not null);

        return new ScriptSpec(
            t,
            meta.ClassName,
            meta.BaseTypeName,
            exports,
            meta.Tool,
            meta.Icon,
            useCtorInjection,
            ctorParams,
            callbacks.HasReady,
            callbacks.HasEnterTree,
            callbacks.HasExitTree,
            callbacks.HasProcess,
            callbacks.HasPhysicsProcess,
            callbacks.HasInput,
            callbacks.HasUnhandledInput,
            callbacks.HasNotification,
            callbacks.HasGuiInput,
            callbacks.HasShortcutInput,
            callbacks.HasDraw,
            callbacks.HasCanDropData,
            callbacks.HasDropData,
            callbacks.HasGetDragData,
            callbacks.HasUnhandledKeyInput,
            callbacks.HasHasPoint,
            callbacks.HasGetMinimumSize,
            callbacks.HasMakeCustomTooltip,
            callbacks.HasGetTooltip,
            CollectSignals(t),
            nodePaths.ToArray(),
            preloads.ToArray(),
            autoConnects.ToArray());
    }

    private sealed record ScriptMeta(string ClassName, string BaseTypeName, bool Tool, string? Icon);

    private static (CustomAttributeData? scriptAttr, CustomAttributeData? toolAttr) ResolvePrimaryAttributes(Type t)
    {
        var attrs = t.GetCustomAttributesData();
        var scriptAttr = attrs.FirstOrDefault(a => a.AttributeType.FullName == Annotations.Known.Types.GodotScriptAttribute);
        var toolAttr = attrs.FirstOrDefault(a => a.AttributeType.FullName == Annotations.Known.Types.GodotToolAttribute);
        return (scriptAttr, toolAttr);
    }

    private static ScriptMeta ExtractScriptMetadata(Type t, CustomAttributeData? scriptAttr, CustomAttributeData? toolAttr)
    {
        string? classNameArg = null, baseTypeNameArg = null, icon = null; bool tool = false;
        var effective = scriptAttr ?? toolAttr!;
        foreach (var na in effective.NamedArguments)
        {
            if (na.MemberName == nameof(Annotations.GodotScriptAttribute.ClassName)) classNameArg = na.TypedValue.Value as string;
            else if (na.MemberName == nameof(Annotations.GodotScriptAttribute.BaseTypeName)) baseTypeNameArg = na.TypedValue.Value as string;
            else if (na.MemberName == nameof(Annotations.GodotScriptAttribute.Tool) && na.TypedValue.Value is bool tb1) tool = tb1;
            else if (na.MemberName == nameof(Annotations.GodotScriptAttribute.Icon)) icon = na.TypedValue.Value as string;
        }
        try
        {
            var attrInstance = t.GetCustomAttributes(false)
                .FirstOrDefault(a => a.GetType().FullName == (scriptAttr is not null ? Annotations.Known.Types.GodotScriptAttribute : Annotations.Known.Types.GodotToolAttribute));
            if (attrInstance is not null)
            {
                string? ReadString(string name)
                    => attrInstance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(attrInstance) as string;
                classNameArg ??= ReadString(nameof(Annotations.GodotScriptAttribute.ClassName));
                baseTypeNameArg ??= ReadString(nameof(Annotations.GodotScriptAttribute.BaseTypeName));
                icon ??= ReadString(nameof(Annotations.GodotScriptAttribute.Icon));
                if (attrInstance.GetType().FullName == Annotations.Known.Types.GodotScriptAttribute)
                {
                    var piTool = attrInstance.GetType().GetProperty(nameof(Annotations.GodotScriptAttribute.Tool), BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    if (piTool is not null && piTool.GetValue(attrInstance) is bool tb2) tool = tool || tb2;
                }
            }
        }
        catch { }
        if (toolAttr is not null) tool = true;
        var className = string.IsNullOrWhiteSpace(classNameArg) ? t.Name : classNameArg!;
        var baseTypeName = string.IsNullOrWhiteSpace(baseTypeNameArg) ? "Godot.Node" : baseTypeNameArg!;
        return new ScriptMeta(className, baseTypeName, tool, icon);
    }

    private static PropertyInfo[] CollectExportProperties(Type t)
        => t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
             .Where(p => p.CanRead && p.CanWrite && IsExportable(p.PropertyType))
             .ToArray();

    private sealed record Callbacks(
        bool HasReady,
        bool HasEnterTree,
        bool HasExitTree,
        bool HasProcess,
        bool HasPhysicsProcess,
        bool HasInput,
        bool HasUnhandledInput,
        bool HasNotification,
        bool HasGuiInput,
        bool HasShortcutInput,
        bool HasDraw,
        bool HasCanDropData,
        bool HasDropData,
        bool HasGetDragData,
        bool HasUnhandledKeyInput,
        bool HasHasPoint,
        bool HasGetMinimumSize,
        bool HasMakeCustomTooltip,
        bool HasGetTooltip
    );

    private static Callbacks DetectCallbacks(Type t)
    {
        bool HasNoArgs(string name) => t.GetMethods(BindingFlags.Instance | BindingFlags.Public).Any(m => m.Name == name && m.GetParameters().Length == 0);
        bool HasOneParam(string name, string paramFullName) => t.GetMethods(BindingFlags.Instance | BindingFlags.Public).Any(m => m.Name == name && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.FullName == paramFullName);
        bool HasTwoParams(string name, string p1, string p2, Type? returnType = null)
            => t.GetMethods(BindingFlags.Instance | BindingFlags.Public).Any(m => m.Name == name && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.FullName == p1 && m.GetParameters()[1].ParameterType.FullName == p2 && (returnType == null || m.ReturnType == returnType));
        bool HasReturnAndOneParam(string name, string p1, Type returnType)
            => t.GetMethods(BindingFlags.Instance | BindingFlags.Public).Any(m => m.Name == name && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.FullName == p1 && m.ReturnType == returnType);
        bool HasReturnAndNoParam(string name, string returnTypeFullName)
            => t.GetMethods(BindingFlags.Instance | BindingFlags.Public).Any(m => m.Name == name && m.GetParameters().Length == 0 && (m.ReturnType.FullName == returnTypeFullName));

        return new Callbacks(
            HasNoArgs("Ready"),
            HasNoArgs("EnterTree"),
            HasNoArgs("ExitTree"),
            t.GetMethod("Process", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(double) }) != null,
            t.GetMethod("PhysicsProcess", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(double) }) != null,
            HasOneParam("Input", KnownGodot.InputEvent),
            HasOneParam("UnhandledInput", KnownGodot.InputEvent),
            t.GetMethod("Notification", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(long) }) != null,
            HasOneParam("GuiInput", KnownGodot.InputEvent),
            HasOneParam("ShortcutInput", KnownGodot.InputEvent),
            HasNoArgs("Draw"),
            HasTwoParams("CanDropData", KnownGodot.Vector2, KnownGodot.Variant, typeof(bool)),
            HasTwoParams("DropData", KnownGodot.Vector2, KnownGodot.Variant, typeof(void)),
            HasReturnAndOneParam("GetDragData", KnownGodot.Vector2, typeof(object)) || HasReturnAndOneParam("GetDragData", KnownGodot.Vector2, Type.GetType(KnownGodot.Variant) ?? typeof(object)),
            HasOneParam("UnhandledKeyInput", KnownGodot.InputEvent),
            HasReturnAndOneParam("HasPoint", KnownGodot.Vector2, typeof(bool)),
            HasReturnAndNoParam("GetMinimumSize", KnownGodot.Vector2),
            t.GetMethods(BindingFlags.Instance | BindingFlags.Public).Any(m => m.Name == "MakeCustomTooltip" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string) && m.ReturnType.FullName == KnownGodot.Control),
            t.GetMethods(BindingFlags.Instance | BindingFlags.Public).Any(m => m.Name == "GetTooltip" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.FullName == KnownGodot.Vector2 && m.ReturnType == typeof(string))
        );
    }

    private static SignalSpec[] CollectSignals(Type t)
        => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
             .Where(m => m.Name.StartsWith("Signal_") && m.ReturnType == typeof(void))
             .Select(m => new { Method = m, Name = m.Name.Substring("Signal_".Length) })
             .Select(x => new SignalSpec(
                 x.Name,
                 x.Method.GetParameters().Select(p => p.ParameterType).ToArray(),
                 x.Method.GetParameters().Select(p => p.Name ?? "arg").ToArray()
             ))
             .ToArray();

    private static (List<NodePathMember> nodePaths, List<PreloadMember> preloads) CollectNodePathsAndPreloads(Type t)
    {
        var nodePathMembers = new List<NodePathMember>();
        var preloadMembers = new List<PreloadMember>();
        var npAttrFull = Annotations.Known.Types.NodePathAttribute;
        var optNpAttrFull = Annotations.Known.Types.OptionalNodePathAttribute;
        var preloadAttrFull = Annotations.Known.Types.PreloadAttribute;
        var addedNodePathNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attrs = member.GetCustomAttributesData();

            // NodePath / OptionalNodePath
            var np = attrs.FirstOrDefault(a => a.AttributeType.FullName == npAttrFull);
            var onp = attrs.FirstOrDefault(a => a.AttributeType.FullName == optNpAttrFull);
            if (np is not null || onp is not null)
            {
                var (memberName, memberType, isProp) = ResolveTargetMember(t, member);
                if (memberType is not null && !string.IsNullOrEmpty(memberName) && !addedNodePathNames.Contains(memberName!))
                {
                    string? path = ReadNamedArgument(np ?? onp!, "Path");
                    var (isOpt, optInner) = TryUnwrapFSharpOption(memberType);
                    if (np is not null && isOpt)
                        throw new InvalidOperationException($"[shimgen] {t.FullName}.{memberName}: NodePathAttribute source type must not be Option<'T>. Use OptionalNodePathAttribute for optional references.");
                    if (onp is not null && !isOpt)
                        throw new InvalidOperationException($"[shimgen] {t.FullName}.{memberName}: OptionalNodePathAttribute source type must be Option<'T>.");

                    nodePathMembers.Add(new NodePathMember(memberName!, isOpt ? optInner! : memberType, isProp, path, isOpt));
                    addedNodePathNames.Add(memberName!);
                }
            }

            // Preload
            var pl = attrs.FirstOrDefault(a => a.AttributeType.FullName == preloadAttrFull);
            if (pl is not null)
            {
                var (memberName, memberType, isProp) = ResolveTargetMember(t, member);
                if (memberType is null || string.IsNullOrEmpty(memberName)) continue;

                var path = ReadNamedArgument(pl, "Path") ?? (pl.ConstructorArguments.Count > 0 ? pl.ConstructorArguments[0].Value as string : null) ?? string.Empty;
                var required = ReadNamedBool(pl, nameof(Annotations.PreloadAttribute.Required));
                var (isOpt, optInner) = TryUnwrapFSharpOption(memberType);
                var targetType = isOpt ? optInner! : memberType;
                if (IsSubclassOfByName(targetType, KnownGodot.Resource))
                    preloadMembers.Add(new PreloadMember(memberName!, targetType, isProp, path, required, isOpt));
            }
        }
        return (nodePathMembers, preloadMembers);
    }

    private static string? ReadNamedArgument(CustomAttributeData data, string name)
        => data.NamedArguments.FirstOrDefault(na => na.MemberName == name).TypedValue.Value as string;

    private static bool ReadNamedBool(CustomAttributeData data, string name)
        => data.NamedArguments.FirstOrDefault(na => na.MemberName == name).TypedValue.Value as bool? ?? false;

    private static (string? memberName, Type? memberType, bool isProperty) ResolveTargetMember(Type t, MemberInfo m)
    {
        if (m is PropertyInfo pi) return (pi.Name, pi.PropertyType, true);
        if (m is FieldInfo fi) return (fi.Name, fi.FieldType, false);
        if (m is MethodInfo mi)
        {
            var n = mi.Name;
            if (n.StartsWith("get_", StringComparison.Ordinal) || n.StartsWith("set_", StringComparison.Ordinal))
            {
                var pn = n.Substring(4);
                var pi2 = t.GetProperty(pn, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi2 is not null) return (pi2.Name, pi2.PropertyType, true);
            }
        }
        return (null, null, false);
    }

    private static List<AutoConnectSpec> CollectAutoConnects(Type t)
    {
        var list = new List<AutoConnectSpec>();
        var acAttrFull = Annotations.Known.Types.AutoConnectAttribute;
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            foreach (var ad in m.GetCustomAttributesData())
            {
                if (ad.AttributeType.FullName != acAttrFull) continue;
                var path = ReadNamedArgument(ad, "Path") ?? (ad.ConstructorArguments.Count > 0 ? ad.ConstructorArguments[0].Value as string : null) ?? string.Empty;
                var sig = ReadNamedArgument(ad, "Signal") ?? (ad.ConstructorArguments.Count > 1 ? ad.ConstructorArguments[1].Value as string : null) ?? string.Empty;
                var paramTypes = m.GetParameters().Select(p => p.ParameterType).ToArray();
                list.Add(new AutoConnectSpec(path, sig, m.Name, paramTypes));
            }
        return list;
    }

    private static (bool useCtorInjection, CtorParamBinding[] bindings) ComputeConstructorInjection(
        Type t,
        string baseTypeName,
        List<NodePathMember> nodePathMembers,
        List<PreloadMember> preloadMembers,
        bool isTool)
    {
        if (isTool) return (false, Array.Empty<CtorParamBinding>());
        var ctors = t.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        if (ctors.Length == 0) return (false, Array.Empty<CtorParamBinding>());
        if (ctors.Length > 1)
        {
            Console.Error.WriteLine($"[shimgen][warn] {t.FullName}: Multiple public constructors found. DI requires a single constructor. Falling back to property wiring.");
            return (false, Array.Empty<CtorParamBinding>());
        }
        var ctor = ctors[0];
        var ps = ctor.GetParameters();
        if (ps.Length == 0) return (false, Array.Empty<CtorParamBinding>());
        var expectedSelf = ps[0].ParameterType.FullName ?? string.Empty;
        if (!string.Equals(expectedSelf, baseTypeName, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"[shimgen][warn] {t.FullName}: First constructor parameter must be '{baseTypeName}' for DI. Found '{expectedSelf}'. Falling back to property wiring.");
            return (false, Array.Empty<CtorParamBinding>());
        }

        var bindings = new List<CtorParamBinding> { new(CtorParamKind.Self, null, ps[0].ParameterType) };
        var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var npByName = nodePathMembers.ToDictionary(n => n.Name, StringComparer.OrdinalIgnoreCase);
        var plByName = preloadMembers.ToDictionary(n => n.Name, StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < ps.Length; i++)
        {
            var p = ps[i];
            if (npByName.TryGetValue(p.Name ?? string.Empty, out var npN) && npN.MemberType == p.ParameterType && !bound.Contains(npN.Name))
            { bindings.Add(new(CtorParamKind.NodePath, npN.Name, p.ParameterType)); bound.Add(npN.Name); continue; }
            if (plByName.TryGetValue(p.Name ?? string.Empty, out var plN) && plN.MemberType == p.ParameterType && !bound.Contains(plN.Name))
            { bindings.Add(new(CtorParamKind.Preload, plN.Name, p.ParameterType)); bound.Add(plN.Name); continue; }

            var npMatches = nodePathMembers.Where(n => n.MemberType == p.ParameterType && !bound.Contains(n.Name)).ToArray();
            if (npMatches.Length == 1)
            { bindings.Add(new(CtorParamKind.NodePath, npMatches[0].Name, p.ParameterType)); bound.Add(npMatches[0].Name); continue; }
            if (npMatches.Length > 1)
            {
                Console.Error.WriteLine($"[shimgen][warn] {t.FullName}: Ambiguous constructor parameter '{p.Name}:{p.ParameterType.Name}'. Multiple NodePath members share this type. Rename the parameter to match the target member name.");
                return (false, Array.Empty<CtorParamBinding>());
            }

            var plMatches = preloadMembers.Where(n => n.MemberType == p.ParameterType && !bound.Contains(n.Name)).ToArray();
            if (plMatches.Length == 1)
            { bindings.Add(new(CtorParamKind.Preload, plMatches[0].Name, p.ParameterType)); bound.Add(plMatches[0].Name); continue; }
            if (plMatches.Length > 1)
            {
                Console.Error.WriteLine($"[shimgen][warn] {t.FullName}: Ambiguous constructor parameter '{p.Name}:{p.ParameterType.Name}'. Multiple Preload members share this type. Rename the parameter to match the target member name.");
                return (false, Array.Empty<CtorParamBinding>());
            }

            Console.Error.WriteLine($"[shimgen][warn] {t.FullName}: Constructor parameter '{p.Name}' of type '{p.ParameterType.FullName}' did not match any NodePath/Preload members. DI disabled for this type.");
            return (false, Array.Empty<CtorParamBinding>());
        }

        var requiredNpMissing = nodePathMembers.Where(n => !n.IsOption).Select(n => n.Name).Except(bindings.Where(b => b.Kind == CtorParamKind.NodePath).Select(b => b.MemberName!), StringComparer.OrdinalIgnoreCase).ToArray();
        var requiredPlMissing = preloadMembers.Where(n => n.Required).Select(n => n.Name).Except(bindings.Where(b => b.Kind == CtorParamKind.Preload).Select(b => b.MemberName!), StringComparer.OrdinalIgnoreCase).ToArray();
        if (requiredNpMissing.Length == 0 && requiredPlMissing.Length == 0)
            return (true, bindings.ToArray());

        if (requiredNpMissing.Length > 0)
            Console.Error.WriteLine($"[shimgen][warn] {t.FullName}: DI requires all required [NodePath] members in the constructor. Missing: {string.Join(", ", requiredNpMissing)}. Falling back to property wiring.");
        if (requiredPlMissing.Length > 0)
            Console.Error.WriteLine($"[shimgen][warn] {t.FullName}: DI requires all Required [Preload] members in the constructor. Missing: {string.Join(", ", requiredPlMissing)}. Falling back to property wiring.");
        return (false, Array.Empty<CtorParamBinding>());
    }

    private static bool IsExportable(Type t)
    {
        if (t == typeof(int) || t == typeof(float) || t == typeof(double) ||
            t == typeof(bool) || t == typeof(string))
            return true;

        if (t.FullName == KnownGodot.Vector2 || t.FullName == KnownGodot.Vector3 || t.FullName == KnownGodot.Color ||
            t.FullName == KnownGodot.Basis || t.FullName == KnownGodot.Rect2 ||
            t.FullName == KnownGodot.Transform2D || t.FullName == KnownGodot.Transform3D ||
            t.FullName == KnownGodot.NodePath || t.FullName == KnownGodot.StringName || t.FullName == KnownGodot.RID)
            return true;

        if (t.IsEnum) return true;

        if (t.IsArray)
        {
            var et = t.GetElementType();
            return et != null && IsExportable(et);
        }

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
        {
            var ga = t.GetGenericArguments();
            return ga.Length == 1 && IsExportable(ga[0]);
        }

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.Dictionary<,>))
        {
            var ga = t.GetGenericArguments();
            return ga.Length == 2 && ga[0] == typeof(string) && IsExportable(ga[1]);
        }

        if (IsSubclassOfByName(t, KnownGodot.Resource)) return true;

        return false;
    }

    private static bool IsSubclassOfByName(Type t, string baseFullName)
    {
        try
        {
            var cur = t;
            while (cur != null)
            {
                if (cur.FullName == baseFullName) return true;
                cur = cur.BaseType;
            }
        }
        catch { }
        return false;
    }

    private static (bool isOption, Type? inner) TryUnwrapFSharpOption(Type t)
    {
        try
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition().FullName is string gdef &&
                (gdef == "Microsoft.FSharp.Core.FSharpOption`1" || gdef == "FSharpOption`1"))
            {
                var ga = t.GetGenericArguments();
                if (ga.Length == 1) return (true, ga[0]);
            }
        }
        catch { }
        return (false, null);
    }
}
