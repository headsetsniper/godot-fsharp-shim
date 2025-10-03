using System.Reflection;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal readonly record struct ScriptSpec(
    Type ImplType,
    string ClassName,
    string BaseTypeName,
    PropertyInfo[] Exports,
    bool Tool,
    bool HasReady,
    bool HasEnterTree,
    bool HasExitTree,
    bool HasProcess,
    bool HasPhysicsProcess,
    bool HasInput,
    bool HasUnhandledInput,
    bool HasNotification,
    string[] SignalNames,
    NodePathMember[] NodePathMembers
);

internal readonly record struct NodePathMember(
    string Name,
    Type MemberType,
    bool IsProperty,
    string? Path,
    bool Required
);
