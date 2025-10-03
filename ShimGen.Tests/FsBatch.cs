using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ShimGen.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class FsCaseAttribute : Attribute
{
    public FsCaseAttribute(string scriptClassName, string fsCode)
    {
        ScriptClassName = scriptClassName;
        FsCode = fsCode;
    }

    public string ScriptClassName { get; }
    public string FsCode { get; }
}

internal sealed class FsFixtureInfo
{
    public required string TempDir { get; init; }
    public required string ImplAssemblyPath { get; init; }
    public required string OutDir { get; init; }
    public required IReadOnlyDictionary<string, string> ScriptClassToFile { get; init; }
}

internal static class FsBatchRegistry
{
    private static readonly ConcurrentDictionary<Type, FsFixtureInfo> _fixtures = new();

    public static void Register(Type t, FsFixtureInfo info) => _fixtures[t] = info;
    public static FsFixtureInfo? Get(Type t) => _fixtures.TryGetValue(t, out var v) ? v : null;
    public static void Unregister(Type t)
    {
        _fixtures.TryRemove(t, out _);
    }
}

public static class FsBatchComponent
{
    public static void BuildForFixture(Type fixtureType)
    {
        // Intentionally quiet by default; flip ProcessUtil echo if you need live streaming
        var methods = fixtureType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttributes(typeof(TestAttribute), inherit: true).Any());

        var fsCases = methods
            .SelectMany(m => m.GetCustomAttributes(typeof(FsCaseAttribute), inherit: false).Cast<FsCaseAttribute>())
            .ToArray();

        if (fsCases.Length == 0)
        {
            return; // Nothing to do for this fixture
        }

        var byClass = fsCases
            .GroupBy(c => c.ScriptClassName)
            .Select(g => g.First())
            .ToList();
        var rerunCount = fsCases.Length - byClass.Count; // extra FsCase attachments request reruns

        var tempDir = TestHelpers.CreateTempDir();
        var files = new Dictionary<string, string>();
        foreach (var c in byClass)
        {
            var fileName = c.ScriptClassName + ".fs";
            var filePath = Path.Combine(tempDir, fileName);
            File.WriteAllText(filePath, EnsureTrailingNewline(c.FsCode));
            files[c.ScriptClassName] = filePath;
        }

        var projPath = Path.Combine(tempDir, "Fixture.fsproj");
        var relIncludes = string.Join("\n", files.Values.Select(f => $"    <Compile Include=\"{Path.GetFileName(f)}\" />"));

        var annPath = Assembly.GetAssembly(typeof(Headsetsniper.Godot.FSharp.Annotations.GodotScriptAttribute))!.Location;
        var testAsmPath = Assembly.GetExecutingAssembly().Location; // contains Godot stubs

        var refsXml = string.Join("\n", new[] { annPath, testAsmPath }.Select(p =>
            $"    <Reference Include=\"{Path.GetFileNameWithoutExtension(p)}\"><HintPath>{p}</HintPath></Reference>"));

        var projXml = "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                      "  <PropertyGroup>\n" +
                      "    <TargetFramework>net8.0</TargetFramework>\n" +
                      "    <GenerateDocumentationFile>false</GenerateDocumentationFile>\n" +
                      "    <ImplicitUsings>enable</ImplicitUsings>\n" +
                      "  </PropertyGroup>\n" +
                      "  <ItemGroup>\n" +
                      relIncludes + "\n" +
                      "  </ItemGroup>\n" +
                      "  <ItemGroup>\n" +
                      refsXml + "\n" +
                      "  </ItemGroup>\n" +
                      "</Project>";
        File.WriteAllText(projPath, projXml);

        var build = ProcessUtil.Run("dotnet", "build -c Debug", tempDir);
        if (build.ExitCode != 0)
        {
            Assert.Fail($"F# build failed. Stdout:\n{build.Stdout}\nStderr:\n{build.Stderr}");
        }
        var implPath = FindOutputAssembly(tempDir);
        var outDir = IntegrationTestUtil.RunShimGen(implPath, fsSourceDir: tempDir);
        // If tests declared additional FsCase entries beyond the unique classes, treat them as rerun requests
        for (int i = 0; i < rerunCount; i++)
        {
            IntegrationTestUtil.RunShimGen(implPath, fsSourceDir: tempDir, outDirOverride: outDir);
        }

        var info = new FsFixtureInfo
        {
            TempDir = tempDir,
            ImplAssemblyPath = implPath,
            OutDir = outDir,
            ScriptClassToFile = files
        };
        FsBatchRegistry.Register(fixtureType, info);
    }

    public static void CleanupForFixture(Type fixtureType)
    {
        // silent cleanup
        var info = FsBatchRegistry.Get(fixtureType);
        if (info != null)
        {
            TryDeleteDir(info.OutDir);
            TryDeleteDir(info.TempDir);
        }
        FsBatchRegistry.Unregister(fixtureType);
    }

    public static void RerunForFixture(Type fixtureType)
    {
        var info = FsBatchRegistry.Get(fixtureType);
        Assert.That(info, Is.Not.Null, $"No FsBatch info registered for fixture {fixtureType.Name}");
        IntegrationTestUtil.RunShimGen(info!.ImplAssemblyPath, fsSourceDir: info.TempDir, outDirOverride: info.OutDir);
    }

    private static string EnsureTrailingNewline(string s) => s.EndsWith("\n") ? s : (s + "\n");

    private static string FindOutputAssembly(string workingDir)
    {
        var outDll = Path.Combine(workingDir, "bin", "Debug", "net8.0", Path.GetFileName(workingDir) + ".dll");
        if (!File.Exists(outDll))
        {
            var binDir = Path.Combine(workingDir, "bin", "Debug", "net8.0");
            var candidate = Directory.EnumerateFiles(binDir, "*.dll", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (candidate == null)
                throw new FileNotFoundException("F# build succeeded but output DLL not found", outDll);
            outDll = candidate;
        }
        return outDll;
    }

    private static void TryDeleteDir(string? dir)
    {
        try
        {
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}

public static class FsBatch
{
    public static string? FindGenerated<TFixture>(string scriptClassName)
    {
        var info = FsBatchRegistry.Get(typeof(TFixture));
        if (info == null) return null;
        var path = Directory.EnumerateFiles(info.OutDir, scriptClassName + ".cs", SearchOption.AllDirectories).FirstOrDefault();
        return path;
    }

    public static string? GetOutDir<TFixture>()
    {
        var info = FsBatchRegistry.Get(typeof(TFixture));
        return info?.OutDir;
    }

    public static string? GetImplAssemblyPath<TFixture>()
    {
        var info = FsBatchRegistry.Get(typeof(TFixture));
        return info?.ImplAssemblyPath;
    }

    public static string? GetFsPath<TFixture>(string scriptClassName)
    {
        var info = FsBatchRegistry.Get(typeof(TFixture));
        if (info == null) return null;
        return info.ScriptClassToFile.TryGetValue(scriptClassName, out var path) ? path : null;
    }
}
