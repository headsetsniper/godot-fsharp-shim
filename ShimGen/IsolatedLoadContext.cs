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
    // Probe NuGet global cache for assemblies (helps when tool runs from a NuGet lib folder)
        try
        {
            var nugetRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (string.IsNullOrWhiteSpace(nugetRoot))
            {
                var home = Environment.GetEnvironmentVariable("HOME");
                var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
                var baseDir = !string.IsNullOrEmpty(home) ? home : userProfile;
                if (!string.IsNullOrEmpty(baseDir)) nugetRoot = Path.Combine(baseDir, ".nuget", "packages");
            }
            if (!string.IsNullOrWhiteSpace(nugetRoot) && Directory.Exists(nugetRoot))
            {
                // Typical NuGet structure: <root>/<packageId>/<version>/lib/<tfm>/<assembly>.dll
                // Package ID is lowercased in global packages folder.
                var pkgId = assemblyName.Name!.ToLowerInvariant();
                // Prefer exact package id; also try legacy id for annotations
                foreach (var id in new[] { pkgId, "godot.fsharp.annotations" })
                {
                    var pkgDir = Path.Combine(nugetRoot, id);
                    if (!Directory.Exists(pkgDir)) continue;
                    // Find all candidate dlls matching fileName under lib/*
                    var candidates = Directory.EnumerateFiles(pkgDir, fileName, SearchOption.AllDirectories)
                                              .Where(p => p.Contains(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                              .ToArray();
                    // Prefer net8.0, then higher TFM alphabetically as a fallback
                    string? pick = candidates.FirstOrDefault(p => p.Contains(Path.DirectorySeparatorChar + "net8.0" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                    ?? candidates.OrderByDescending(p => p).FirstOrDefault();
                    if (!string.IsNullOrEmpty(pick)) return LoadFromAssemblyPath(pick!);
                }
            }
        }
        catch { /* ignore probing failures */ }
        return null!;
    }
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (path != null) return LoadUnmanagedDllFromPath(path);
        return IntPtr.Zero;
    }
}
