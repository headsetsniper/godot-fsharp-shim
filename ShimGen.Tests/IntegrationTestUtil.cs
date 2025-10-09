using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.Annotations;
using Headsetsniper.Godot.FSharp.ShimGen;

namespace ShimGen.Tests;

internal static class IntegrationTestUtil
{
    public static string RunShimGen(string implPath, string? fsSourceDir = null, string? outDirOverride = null)
    {
        var outDir = outDirOverride ?? TestHelpers.CreateTempDir();
        // Ensure the attribute assembly is next to the impl assembly to help resolution in test runs
        var implDir = Path.GetDirectoryName(implPath)!;
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var targetAnn = Path.Combine(implDir, Path.GetFileName(annPath));
        if (!File.Exists(targetAnn)) File.Copy(annPath, targetAnn, overwrite: true);

        try
        {
            // Prefer in-process run to avoid dotnet process spawn overhead
            var regen = (string?)null; // explicitly clear during normal runs
            var code = Headsetsniper.Godot.FSharp.ShimGen.TestingHooks.RunInProcess(implPath, outDir, fsSourceDir, regenerateEnv: regen, throwOnError: false);
            if (code == 0) return outDir;
            // Fall back to external process to preserve behavior if in-process failed for environment reasons
        }
        catch
        {
            // ignore and fall back
        }

        var testDir = TestContext.CurrentContext.TestDirectory;
        var tfm = Path.GetFileName(testDir);
        var configuration = Path.GetFileName(Path.GetDirectoryName(testDir)!);
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
        var outDirShim = Path.Combine(repoRoot, "ShimGen", "bin", configuration, tfm);
        var exeCandidates = new[]
        {
            Path.Combine(outDirShim, "Headsetsniper.Godot.FSharp.ShimGen.dll"),
            Path.Combine(outDirShim, "ShimGen.dll"),
        };
        var exe = exeCandidates.FirstOrDefault(File.Exists)
                  ?? Directory.EnumerateFiles(outDirShim, "*ShimGen*.dll", SearchOption.TopDirectoryOnly)
                       .OrderByDescending(p => p.Length)
                       .FirstOrDefault();
        Assert.That(exe, Is.Not.Null.And.Not.Empty, $"ShimGen not built; looked in {outDirShim}");
        Assert.That(File.Exists(exe!), Is.True, $"ShimGen not built at {exe}");

        var args = fsSourceDir == null
            ? $"\"{exe}\" \"{implPath}\" \"{outDir}\""
            : $"\"{exe}\" \"{implPath}\" \"{outDir}\" \"{fsSourceDir}\"";

        var env = new System.Collections.Generic.Dictionary<string, string?>
        {
            ["SHIMGEN_REGENERATE_SCRIPTS"] = null,
        };
        var res = ProcessUtil.Run("dotnet", args, env: env);
        Assert.That(res.ExitCode, Is.EqualTo(0), $"ShimGen failed. Stdout:\n{res.Stdout}\nStderr:\n{res.Stderr}");
        return outDir;
    }
}
