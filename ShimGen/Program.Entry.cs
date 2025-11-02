using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class Program
{
    public static int Main(string[] args)
    {
        try { Console.WriteLine("[shimgen] entered Main (pre-parse)"); } catch { }
        var (ok, asmPath, outDir, fsDir, dryRun) = ParseOptions(args);
        if (!ok) return PrintUsageAndExit();
        return RunPipeline(asmPath, outDir, fsDir, dryRun);
    }

    private static int PrintUsageAndExit()
    {
        Console.Error.WriteLine("Usage: ShimGen <FSharpAssemblyPath> <OutDir> [FsSourceDir]");
        return 2;
    }

    // Exposed internally for test in-process execution; public wrapper provided in TestingHooks.cs
    internal static int RunPipeline(string asmPath, string outDir, string? fsDir, bool dryRun)
    {
        IsolatedLoadContext? lc = null;
        try
        {
            lc = PrepareLoadContext(asmPath);
            var asm = LoadAssembly(lc, asmPath);
            var mode = ParseMode(Environment.GetEnvironmentVariable("SHIMGEN_MODE"));
            var skipWrites = ParseBooleanFlag(Environment.GetEnvironmentVariable("SHIMGEN_SKIP_WRITES"));
            try { Console.WriteLine($"[shimgen] mode={mode}"); } catch { }
            LogInfo($"[shimgen] Mode={mode}; SkipWrites={skipWrites}");
            if (mode == GenerationMode.Tests)
            {
                var (plan, liveTypes) = GenerateTestShims(asm, outDir, fsDir, dryRun, skipWrites);
                // Do NOT prune in Tests mode; test shim file names do not map 1:1 to F# source file layout
                // and pruning logic based on source roots could erroneously delete freshly generated test shims.
                PrintSummary(plan, dryRun, skipWrites);
                return 0;
            }
            else
            {
                var types = SafeGetTypes(asm);
                var (regenAll, regenSet) = ParseRegenerateTargets(Environment.GetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS"));
                var (plan, liveTypes) = GenerateForTypes(types, outDir, fsDir, dryRun, regenAll, regenSet, skipWrites);
                if (!string.IsNullOrEmpty(fsDir))
                    PruneOrphans(outDir, fsDir!, liveTypes, dryRun || skipWrites, plan.PlannedDeletes);
                PrintSummary(plan, dryRun, skipWrites);
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[shimgen] Error: {ex.Message}");
            return 1;
        }
        finally
        {
            Cleanup(lc);
        }
    }
}
