using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
}
