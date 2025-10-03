using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Headsetsniper.Godot.FSharp.Annotations;
using Headsetsniper.Godot.FSharp.ShimGen;

namespace ShimGen.Tests;

internal static class IntegrationTestUtil
{
    public static string RunShimGen(string implPath, string? fsSourceDir = null, string? outDirOverride = null)
    {
        var outDir = outDirOverride ?? TestHelpers.CreateTempDir();
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

        // Ensure the attribute assembly is next to the impl assembly to help resolution
        var implDir = Path.GetDirectoryName(implPath)!;
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var targetAnn = Path.Combine(implDir, Path.GetFileName(annPath));
        if (!File.Exists(targetAnn)) File.Copy(annPath, targetAnn, overwrite: true);
        var args = fsSourceDir == null
            ? $"\"{exe}\" \"{implPath}\" \"{outDir}\""
            : $"\"{exe}\" \"{implPath}\" \"{outDir}\" \"{fsSourceDir}\"";

        var res = ProcessUtil.Run("dotnet", args);
        Assert.That(res.ExitCode, Is.EqualTo(0), $"ShimGen failed. Stdout:\n{res.Stdout}\nStderr:\n{res.Stderr}");
        return outDir;
    }
}
