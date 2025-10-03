using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public static class TestHelpers
{
    public static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "shimgen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string CompileCSharp(string code, IEnumerable<MetadataReference>? extraRefs = null, string? asmName = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp12));
        var coreDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(Path.Combine(coreDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
        };
        if (extraRefs != null) refs.AddRange(extraRefs);

        var compilation = CSharpCompilation.Create(
            asmName ?? ("Asm_" + Guid.NewGuid().ToString("N")),
            new[] { syntaxTree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var dir = CreateTempDir();
        var path = Path.Combine(dir, (asmName ?? "Asm") + ".dll");
        var emit = compilation.Emit(path);
        if (!emit.Success)
        {
            var diagnostics = string.Join(Environment.NewLine, emit.Diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException("Compilation failed:\n" + diagnostics);
        }
        return path;
    }

    public static MetadataReference RefFromAssembly(Assembly asm) => MetadataReference.CreateFromFile(asm.Location);
    public static MetadataReference RefFromPath(string path) => MetadataReference.CreateFromFile(path);

    public static string RefPathFromAssembly(Assembly asm) => asm.Location;

    public static string CompileFSharp(string code, IEnumerable<string>? extraRefPaths = null, string? asmName = null)
    {
        var dir = CreateTempDir();
        var name = asmName ?? ("Asm_" + Guid.NewGuid().ToString("N"));
        var projPath = Path.Combine(dir, name + ".fsproj");
        var srcPath = Path.Combine(dir, "Impl.fs");

        File.WriteAllText(srcPath, code);

        var refsXml = string.Empty;
        if (extraRefPaths != null)
        {
            refsXml = string.Join("\n", extraRefPaths.Select(p =>
                    "    <Reference Include=\"" + Path.GetFileNameWithoutExtension(p) + "\"><HintPath>" + p + "</HintPath></Reference>"));
        }

        var projXml = "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                      "    <PropertyGroup>\n" +
                      "        <TargetFramework>net8.0</TargetFramework>\n" +
                      "        <GenerateDocumentationFile>false</GenerateDocumentationFile>\n" +
                      $"        <AssemblyName>{name}</AssemblyName>\n" +
                      "        <ImplicitUsings>enable</ImplicitUsings>\n" +
                      "    </PropertyGroup>\n" +
                      "    <ItemGroup>\n" +
                      "        <Compile Include=\"Impl.fs\" />\n" +
                      "    </ItemGroup>\n" +
                      "    <ItemGroup>\n" +
                      refsXml + "\n" +
                      "    </ItemGroup>\n" +
                      "</Project>";
        File.WriteAllText(projPath, projXml);

        var psi = new ProcessStartInfo("dotnet", "build -c Debug")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            throw new InvalidOperationException($"F# build failed. Stdout:\n{stdout}\nStderr:\n{stderr}");
        }
        var outDll = Path.Combine(dir, "bin", "Debug", "net8.0", name + ".dll");
        if (!File.Exists(outDll))
            throw new FileNotFoundException("F# build succeeded but output DLL not found", outDll);
        return outDll;
    }
}
