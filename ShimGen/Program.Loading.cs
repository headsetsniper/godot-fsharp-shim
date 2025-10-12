using System.Reflection;
using System.Runtime.Loader;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class Program
{
    private static IsolatedLoadContext PrepareLoadContext(string asmPath)
    {
        var lc = CreateLoadContext(asmPath);
        EnsureDependency(lc, "FSharp.Core");
        EnsureDependency(lc, Annotations.Known.Assembly.Name);
        EnsureDependency(lc, Annotations.Known.Assembly.LegacyName);
        EnsureDependency(AssemblyLoadContext.Default, "Microsoft.CodeAnalysis");
        EnsureDependency(AssemblyLoadContext.Default, "Microsoft.CodeAnalysis.CSharp");
        return lc;
    }

    private static void Cleanup(IsolatedLoadContext? lc)
    {
        if (lc is null) return;
        try { lc.Unload(); } catch { }
        try { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); } catch { }
    }

    private static IsolatedLoadContext CreateLoadContext(string mainAsmPath)
    {
        var resolver = new AssemblyDependencyResolver(mainAsmPath);
        var asmDir = Path.GetDirectoryName(mainAsmPath)!;
        return new IsolatedLoadContext(resolver, asmDir, AppContext.BaseDirectory, Directory.GetCurrentDirectory());
    }

    private static void EnsureDependency(AssemblyLoadContext lc, string name)
    {
        try
        {
            if (lc.Assemblies.Any(a => a.GetName().Name == name)) return;
            try { _ = lc.LoadFromAssemblyName(new AssemblyName(name)); return; } catch { }
            var nuget = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (string.IsNullOrWhiteSpace(nuget))
                nuget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
            var packageCandidates = new List<string> { name.ToLowerInvariant() };
            if (string.Equals(name, "Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase))
                packageCandidates.Insert(0, "microsoft.codeanalysis.common");
            else if (string.Equals(name, "Microsoft.CodeAnalysis.CSharp", StringComparison.OrdinalIgnoreCase))
                packageCandidates.Insert(0, "microsoft.codeanalysis.csharp");

            string? dll = null;
            foreach (var pkg in packageCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var pkgDir = Path.Combine(nuget, pkg);
                if (!Directory.Exists(pkgDir)) continue;
                dll = Directory.EnumerateFiles(pkgDir, name + ".dll", SearchOption.AllDirectories)
                               .Where(p => p.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                               .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                               .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}analyzers{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                               .OrderByDescending(GetTfmScore)
                               .ThenByDescending(p => p)
                               .FirstOrDefault();
                if (!string.IsNullOrEmpty(dll)) break;
            }
            if (dll is null && Directory.Exists(nuget))
            {
                try
                {
                    dll = Directory.EnumerateFiles(nuget, name + ".dll", SearchOption.AllDirectories)
                                    .OrderByDescending(p => p)
                                    .FirstOrDefault();
                }
                catch { }
            }
            if (dll != null)
            {
                try
                {
                    if (lc is IsolatedLoadContext ilc)
                        ilc.LoadFromAssemblyPath(dll);
                    else
                        Assembly.LoadFrom(dll);
                }
                catch { }
            }
        }
        catch { }
    }

    private static int GetTfmScore(string path)
    {
        var p = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).ToLowerInvariant();
        int Score(string key, int s) => p.Contains(Path.DirectorySeparatorChar + key + Path.DirectorySeparatorChar) ? s : 0;
        int score = 0;
        score = Math.Max(score, Score("net9.0", 600));
        score = Math.Max(score, Score("net7.0", 500));
        score = Math.Max(score, Score("net6.0", 400));
        score = Math.Max(score, Score("net5.0", 350));
        score = Math.Max(score, Score("netcoreapp3.1", 300));
        score = Math.Max(score, Score("netstandard2.1", 250));
        score = Math.Max(score, Score("netstandard2.0", 200));
        score = Math.Max(score, Score("net472", 100));
        return score;
    }

    private static Assembly LoadAssembly(IsolatedLoadContext lc, string path) => lc.LoadFromAssemblyPath(Path.GetFullPath(path));

    private static IEnumerable<Type?> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException rtle)
        {
            foreach (var le in rtle.LoaderExceptions)
                Console.Error.WriteLine($"[shimgen] Loader exception: {le?.Message}");
            return rtle.Types;
        }
    }
}
