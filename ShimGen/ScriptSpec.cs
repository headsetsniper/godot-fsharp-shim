using System.Reflection;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal readonly record struct ScriptSpec(
    Type ImplType,
    string ClassName,
    string BaseTypeName,
    PropertyInfo[] Exports,
    bool HasReady,
    bool HasProcess,
    bool HasPhysicsProcess,
    bool HasInput,
    bool HasUnhandledInput,
    bool HasNotification,
    string[] SignalNames
);
