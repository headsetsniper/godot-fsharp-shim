using System;

namespace Headsetsniper.Godot.FSharp.ShimGen;

// Public test hooks kept separate to avoid polluting main Program
public static class TestingHooks
{
    // Run the generator in-process to avoid external dotnet process overhead during tests.
    // Returns 0 on success; throws on non-zero if throwOnError is true.
    // If suppressErrorOutput is true, temporarily redirects Console.Error to avoid polluting test logs with expected failures.
    public static int RunInProcess(string implAssemblyPath, string outDir, string? fsSourceDir = null, string? regenerateEnv = null, bool throwOnError = true, bool suppressErrorOutput = false)
    {
        var prev = Environment.GetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS");
        var prevQuiet = Environment.GetEnvironmentVariable("SHIMGEN_QUIET");
        System.IO.TextWriter? prevErr = null;
        try
        {
            if (suppressErrorOutput)
            {
                prevErr = Console.Error;
                Console.SetError(System.IO.TextWriter.Synchronized(System.IO.TextWriter.Null));
            }

            Environment.SetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS", regenerateEnv);
            // Silence informational logs in tests unless explicitly disabled by caller
            Environment.SetEnvironmentVariable("SHIMGEN_QUIET", "1");
            var code = Program.RunPipeline(implAssemblyPath, outDir, fsSourceDir, dryRun: false);
            if (throwOnError && code != 0)
                throw new InvalidOperationException($"ShimGen failed with exit code {code}");
            return code;
        }
        finally
        {
            if (prevErr != null)
            {
                try { Console.SetError(prevErr); } catch { /* ignore */ }
            }
            Environment.SetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS", prev);
            Environment.SetEnvironmentVariable("SHIMGEN_QUIET", prevQuiet);
        }
    }
}
