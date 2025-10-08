using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class Program
{
    private static (bool ok, string asmPath, string outDir, string? fsDir, bool dryRun) ParseOptions(string[] args)
    {
        if (args is null) return (false, string.Empty, string.Empty, null, false);
        bool dryRun = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var positional = args.Where(a => !string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (positional.Length < 2)
            return (false, string.Empty, string.Empty, null, dryRun);

        string asmPath = positional[0];
        string outDir = positional[1];
        string? fsDir = positional.Length >= 3 ? positional[2] : null;

        try { asmPath = Path.GetFullPath(asmPath); } catch { }
        try { outDir = Path.GetFullPath(outDir); } catch { }
        if (!string.IsNullOrWhiteSpace(fsDir))
        {
            try { fsDir = Path.GetFullPath(fsDir!); } catch { }
        }

        if (!File.Exists(asmPath))
        {
            Console.Error.WriteLine($"[shimgen] F# assembly not found: {asmPath}");
            return (false, string.Empty, string.Empty, null, dryRun);
        }

        return (true, asmPath, outDir, fsDir, dryRun);
    }

    private static (bool all, HashSet<string> list) ParseRegenerateTargets(string? env)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(env)) return (false, set);
        var v = env.Trim();
        if (string.Equals(v, "all", StringComparison.OrdinalIgnoreCase) || v == "*")
            return (true, set);
        foreach (var part in v.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            set.Add(part.Trim());
        return (false, set);
    }
}
