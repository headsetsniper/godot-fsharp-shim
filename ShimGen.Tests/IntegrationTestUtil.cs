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
    public static string BuildImplAssembly(string className = "FooImpl", string baseType = KnownGodot.Node2D)
    {
        var code =
            "using Godot;\n" +
            "using Headsetsniper.Godot.FSharp.Annotations;\n" +
            "namespace Game\n" +
            "{\n" +
            $"    [GodotScript(ClassName=\"Foo\", BaseTypeName=\"{baseType}\")]\n" +
            $"    public class {className}\n" +
            "    {\n" +
            "        public int Speed { get; set; } = 220;\n" +
            "        public void Ready() { }\n" +
            "        public void Process(double delta) { }\n" +
            "    }\n" +
            "}\n";
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node2D).Assembly;
        return TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "GameImpl");
    }

    public static string BuildImplAssemblyFs(string className = "FooImpl", string baseType = KnownGodot.Node2D)
    {
        var code = string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Godot",
            "open Headsetsniper.Godot.FSharp.Annotations",
            $"[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"{baseType}\")>]",
            $"type {className}() =",
            "    member val Speed : int = 220 with get, set",
            "    member _.Ready() = ()",
            "    member _.Process(delta: double) = ()"
        }) + "\n";
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var refs = new[]{
            TestHelpers.RefPathFromAssembly(typeof(Godot.Node2D).Assembly),
            annPath
        };
        return TestHelpers.CompileFSharp(code, refs, asmName: "GameImplFs");
    }

    public static string RunShimGenFs(string implPath, string? fsSourceDir = null, string? outDirOverride = null)
        => RunShimGen(implPath, fsSourceDir, outDirOverride);

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
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var p = System.Diagnostics.Process.Start(psi)!;
        p.WaitForExit();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        Assert.That(p.ExitCode, Is.EqualTo(0), $"ShimGen failed. Stdout:\n{stdout}\nStderr:\n{stderr}");
        return outDir;
    }

    public static (string dir, string file) CreateTempFsSource(string? content = null)
    {
        var dir = TestHelpers.CreateTempDir();
        var file = Path.Combine(dir, "Game.fs");
        var fs = content ?? string.Join("\n", new[]{
            "namespace Game",
            "",
            "open Headsetsniper.Godot.FSharp.Annotations",
            "",
            "[<GodotScript(ClassName=\"Foo\", BaseTypeName=\"Godot.Node2D\")>]",
            "type FooImpl() =",
            "    do ()"
        }) + "\n";
        File.WriteAllText(file, fs);
        return (dir, file);
    }
}
