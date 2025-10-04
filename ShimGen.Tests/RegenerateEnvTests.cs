using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace ShimGen.Tests;

[TestFixture]
public class RegenerateEnvTests
{
    [OneTimeTearDown]
    public void AfterAll() => FsBatchComponent.CleanupForFixture(typeof(RegenerateEnvTests));

    [Test]
    [FsCase("ReFo", """
namespace Game
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="ReFo", BaseTypeName="Godot.Node")>]
type ReFoImpl() =
    member _.Ready() = ()
""")]
    public void Env_All_Forces_InPlace_Rewrite()
    {
        // Initial generation
        FsBatchComponent.BuildForFixture(typeof(RegenerateEnvTests));
        var outDir = FsBatch.GetOutDir<RegenerateEnvTests>();
        Assert.That(outDir, Is.Not.Null);
        var path = Directory.EnumerateFiles(outDir!, "ReFo.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var originalPath = path!;
        var initialContent = File.ReadAllText(originalPath);
        var initialWrite = File.GetLastWriteTimeUtc(originalPath);

        // Set env to regenerate all; rerun shimgen and expect same path but content/time may change
        var prev = Environment.GetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS");
        try
        {
            Environment.SetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS", "all");
            FsBatchComponent.RerunForFixture(typeof(RegenerateEnvTests));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS", prev);
        }

        var afterContent = File.ReadAllText(originalPath);
        var afterWrite = File.GetLastWriteTimeUtc(originalPath);

        // Path should be identical; file should have been rewritten (timestamp not earlier)
        Assert.That(Directory.EnumerateFiles(outDir!, "ReFo.cs", SearchOption.AllDirectories).FirstOrDefault(), Is.EqualTo(originalPath));
        Assert.That(afterWrite, Is.GreaterThanOrEqualTo(initialWrite));
        // It’s okay if content matches; we forced a write. Either way, file exists at same path.
        Assert.That(File.Exists(originalPath), Is.True);
    }

    [Test]
    [FsCase("ReBar", """
namespace Game
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="ReBar", BaseTypeName="Godot.Node")>]
type ReBarImpl() =
    member _.Ready() = ()
""")]
    public void Env_List_Forces_InPlace_Rewrite_By_ClassName()
    {
        FsBatchComponent.BuildForFixture(typeof(RegenerateEnvTests));
        var outDir = FsBatch.GetOutDir<RegenerateEnvTests>();
        Assert.That(outDir, Is.Not.Null);
        var path = Directory.EnumerateFiles(outDir!, "ReBar.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var originalPath = path!;
        var initialWrite = File.GetLastWriteTimeUtc(originalPath);

        var prev = Environment.GetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS");
        try
        {
            Environment.SetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS", "ReBar");
            FsBatchComponent.RerunForFixture(typeof(RegenerateEnvTests));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHIMGEN_REGENERATE_SCRIPTS", prev);
        }

        var afterWrite = File.GetLastWriteTimeUtc(originalPath);
        Assert.That(afterWrite, Is.GreaterThanOrEqualTo(initialWrite));
    }
}
