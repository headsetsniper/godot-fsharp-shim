using System;

namespace Headsetsniper.Godot.FSharp.ShimGen;

// Public test hooks kept separate to avoid polluting main Program
public static class TestingHooks
{
    // Run the generator in-process to avoid external dotnet process overhead during tests.
    // Returns 0 on success; throws on non-zero if throwOnError is true.
    public static int RunInProcess(string implAssemblyPath, string outDir, string? fsSourceDir = null, string? regenerateEnv = null, bool throwOnError = true)
    {
        var prev = Environment.GetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS");
        try
        {
            Environment.SetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS", regenerateEnv);
            var code = Program.RunPipeline(implAssemblyPath, outDir, fsSourceDir, dryRun: false);
            if (throwOnError && code != 0)
                throw new InvalidOperationException($"ShimGen failed with exit code {code}");
            return code;
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS", prev);
        }
    }
}
