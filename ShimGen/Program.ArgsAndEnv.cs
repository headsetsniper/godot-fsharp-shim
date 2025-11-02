using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class Program
{
    private enum GenerationMode { Scripts, Tests }

    private static (bool ok, string asmPath, string outDir, string? fsDir, bool dryRun) ParseOptions(string[] args)
    {
        if (args is null) return (false, string.Empty, string.Empty, null, false);
        bool dryRun = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));
        // Accept optional --mode=Tests or --mode Tests
        string? cliMode = null;
        var cleaned = new System.Collections.Generic.List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase)) continue;
            if (a.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase)) { cliMode = a.Substring(7); continue; }
            if (string.Equals(a, "--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { cliMode = args[i + 1]; i++; continue; }
            cleaned.Add(a);
        }
        var positional = cleaned.ToArray();
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

        if (!string.IsNullOrEmpty(cliMode))
            Environment.SetEnvironmentVariable("SHIMGEN_MODE", cliMode);

        return (true, asmPath, outDir, fsDir, dryRun);
    }

    private static GenerationMode ParseMode(string? env)
    {
        var v = env?.Trim();
        if (string.IsNullOrWhiteSpace(v)) return GenerationMode.Scripts;
        if (v.Equals("tests", StringComparison.OrdinalIgnoreCase) || v.Equals("test", StringComparison.OrdinalIgnoreCase))
            return GenerationMode.Tests;
        return GenerationMode.Scripts;
    }

    private static bool ParseBooleanFlag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim();
        return v.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               v.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               v.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               v.Equals("on", StringComparison.OrdinalIgnoreCase);
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
