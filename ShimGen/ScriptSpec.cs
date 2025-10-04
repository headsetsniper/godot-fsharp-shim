using System.Reflection;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal readonly record struct ScriptSpec(
    Type ImplType,
    string ClassName,
    string BaseTypeName,
    PropertyInfo[] Exports,
    bool Tool,
    string? Icon,
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
    bool HasGetTooltip,
    SignalSpec[] Signals,
    NodePathMember[] NodePathMembers,
    PreloadMember[] PreloadMembers,
    AutoConnectSpec[] AutoConnects
);

internal readonly record struct NodePathMember(
    string Name,
    Type MemberType,
    bool IsProperty,
    string? Path,
    bool Required,
    bool IsOption
);

internal readonly record struct PreloadMember(
    string Name,
    Type MemberType,
    bool IsProperty,
    string Path,
    bool Required,
    bool IsOption
);

internal readonly record struct SignalSpec(
    string Name,
    Type[] ParamTypes,
    string[] ParamNames
);

internal readonly record struct AutoConnectSpec(
    string Path,
    string Signal,
    string HandlerName,
    Type[] ParamTypes
);
