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
    public static string RunShimGen(string implPath, string? fsSourceDir = null, string? outDirOverride = null, bool suppressErrorOutput = false, bool assertOnError = false)
    {
        var outDir = outDirOverride ?? TestHelpers.CreateTempDir();
        var implDir = Path.GetDirectoryName(implPath)!;
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var targetAnn = Path.Combine(implDir, Path.GetFileName(annPath));
        if (!File.Exists(targetAnn)) File.Copy(annPath, targetAnn, overwrite: true);

        try
        {
            var regen = (string?)null;
            var code = Headsetsniper.Godot.FSharp.ShimGen.TestingHooks.RunInProcess(implPath, outDir, fsSourceDir, regenerateEnv: regen, throwOnError: false, suppressErrorOutput: suppressErrorOutput);
            if (code == 0) return outDir;
            if (assertOnError)
                Assert.Fail($"ShimGen failed in-process with exit code {code}.");
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
        if (assertOnError && res.ExitCode != 0)
        {
            Assert.Fail("ShimGen failed.");
        }
        Assert.That(res.ExitCode, Is.EqualTo(0), $"ShimGen failed. Stdout:\n{res.Stdout}\nStderr:\n{res.Stderr}");
        return outDir;
    }
}
