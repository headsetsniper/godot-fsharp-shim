using System.Reflection;
using System.Runtime.Loader;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal sealed class IsolatedLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string[] _fallbackDirs;
    public IsolatedLoadContext(AssemblyDependencyResolver resolver, params string[] fallbackDirs) : base(isCollectible: true)
    { _resolver = resolver; _fallbackDirs = fallbackDirs ?? Array.Empty<string>(); }
    protected override Assembly Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path != null) return LoadFromAssemblyPath(path);
        var fileName = assemblyName.Name + ".dll";
        foreach (var dir in _fallbackDirs)
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) return LoadFromAssemblyPath(candidate);
        }
        return null!;
    }
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (path != null) return LoadUnmanagedDllFromPath(path);
        return IntPtr.Zero;
    }
}
