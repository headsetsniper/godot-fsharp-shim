using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.Annotations;
using Headsetsniper.Godot.FSharp.ShimGen;

namespace ShimGen.Tests;

[TestFixture]
public class ExportTypesAndDefaultsTests
{
    [OneTimeSetUp]
    public void BeforeAll()
    {
        FsBatchComponent.BuildForFixture(typeof(ExportTypesAndDefaultsTests));
    }

    [OneTimeTearDown]
    public void AfterAll()
    {
        FsBatchComponent.CleanupForFixture(typeof(ExportTypesAndDefaultsTests));
    }

    public enum TestEnum { A = 0, B = 1 }

    [Flags]
    public enum TestFlags
    {
        None = 0,
        One = 1 << 0,
        Two = 1 << 1,
        Three = One | Two
    }

    [Test]
    [FsCase("Foo", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Foo", BaseTypeName="Godot.Node2D")>]
type FooImpl() =
    member val Speed : int = 220 with get, set
    member _.Ready() = ()
    member _.Process(delta: double) = ()
""")]
    public void Exports_Primitive_Properties()
    {
        var outDir = FsBatch.GetOutDir<ExportTypesAndDefaultsTests>();
        var fooPath = Directory.EnumerateFiles(outDir!, "Foo.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(fooPath);
        StringAssert.Contains("[Export] public System.Int32 Speed", src);

        var stubsAsm = typeof(Godot.Node2D).Assembly;
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var shimDll = TestHelpers.CompileCSharp(src, new[] {
            TestHelpers.RefFromAssembly(stubsAsm),
            TestHelpers.RefFromPath(annPath),
            MetadataReference.CreateFromFile(FsBatch.GetImplAssemblyPath<ExportTypesAndDefaultsTests>()!)
        }, asmName: "FooShim");
        Assert.That(File.Exists(shimDll), Is.True);
    }

    [Test]
    [FsCase("Baz", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Baz", BaseTypeName="Godot.Node")>]
type BazImpl() =
    member val V2 : Vector2 = new Vector2() with get, set
    member val V3 : Vector3 = new Vector3() with get, set
    member val C : Color = new Color() with get, set
    member _.Ready() = ()
""")]
    public void Exports_Godot_Struct_Types()
    {
        var outDir = FsBatch.GetOutDir<ExportTypesAndDefaultsTests>();
        var bazPath = Directory.EnumerateFiles(outDir!, "Baz.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(bazPath, Is.Not.Null, "Baz.cs not generated");
        var src = File.ReadAllText(bazPath!);
        StringAssert.Contains("[Export] public Godot.Vector2 V2", src);
        StringAssert.Contains("[Export] public Godot.Vector3 V3", src);
        StringAssert.Contains("[Export] public Godot.Color C", src);
    }

    [Test]
    [FsCase("Bar", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Bar", BaseTypeName="Godot.Node")>]
type BarImpl() =
    member val A : int = 0 with get, set
    member val S : string = null with get, set
    member val O : obj = null with get, set
    member _.Ready() = ()
""")]
    public void Exports_Only_Primitive_Properties()
    {
        var outDir = FsBatch.GetOutDir<ExportTypesAndDefaultsTests>();
        var barPath = Directory.EnumerateFiles(outDir!, "Bar.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(barPath, Is.Not.Null, "Bar.cs not generated");
        var src = File.ReadAllText(barPath!);
        StringAssert.Contains("[Export] public System.Int32 A", src);
        StringAssert.Contains("[Export] public System.String S", src);
        StringAssert.DoesNotContain("object O", src);
    }

    [Test]
    [FsCase("Prim", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Prim", BaseTypeName="Godot.Node")>]
type PrimImpl() =
    member val I : int = 0 with get, set
    member val F : single = 0.0f with get, set
    member val D : double = 0.0 with get, set
    member val B : bool = false with get, set
    member val S : string = null with get, set
    member _.Ready() = ()
""")]
    public void Exports_Primitives_And_Strings()
    {
        var outDir = FsBatch.GetOutDir<ExportTypesAndDefaultsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Prim.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Prim.cs not generated");
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export] public System.Int32 I", src);
        StringAssert.Contains("[Export] public System.Single F", src);
        StringAssert.Contains("[Export] public System.Double D", src);
        StringAssert.Contains("[Export] public System.Boolean B", src);
        StringAssert.Contains("[Export] public System.String S", src);
    }

    [Test]
    [FsCase("Enumy", """
namespace ShimGen.Tests

open System

namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Enumy", BaseTypeName="Godot.Node")>]
type EnumyImpl() =
    member val Mode : ShimGen.Tests.ExportTypesAndDefaultsTests.TestEnum = ShimGen.Tests.ExportTypesAndDefaultsTests.TestEnum.A with get, set
    member _.Ready() = ()
""")]
    public void Exports_Enum_Type()
    {
        var outDir = FsBatch.GetOutDir<ExportTypesAndDefaultsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Enumy.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Enumy.cs not generated");
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export] public ShimGen.Tests.ExportTypesAndDefaultsTests.TestEnum Mode", src);
    }

    [Test]
    [FsCase("Qux", """
namespace ShimGen.Tests

open System

namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Qux", BaseTypeName="Godot.Node")>]
type QuxImpl() =
    member val Numbers : int array = Array.empty with get, set
    member val Names : string array = Array.empty with get, set
    member val Mode : ShimGen.Tests.ExportTypesAndDefaultsTests.TestEnum = ShimGen.Tests.ExportTypesAndDefaultsTests.TestEnum.A with get, set
    member _.Ready() = ()
""")]
    public void Exports_Arrays_And_Enums()
    {
        var outDir = FsBatch.GetOutDir<ExportTypesAndDefaultsTests>();
        var quxPath = Directory.EnumerateFiles(outDir!, "Qux.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(quxPath, Is.Not.Null, "Qux.cs not generated");
        var src = File.ReadAllText(quxPath!);
        StringAssert.Contains("[Export] public System.Int32[] Numbers", src);
        StringAssert.Contains("[Export] public System.String[] Names", src);
        StringAssert.Contains("[Export] public ShimGen.Tests.ExportTypesAndDefaultsTests.TestEnum Mode", src);
    }

    [Test]
    [FsCase("Flaggy", """
namespace ShimGen.Tests

open System

namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Flaggy", BaseTypeName="Godot.Node")>]
type FlaggyImpl() =
    member val Mask : ShimGen.Tests.ExportTypesAndDefaultsTests.TestFlags = ShimGen.Tests.ExportTypesAndDefaultsTests.TestFlags.None with get, set
    member _.Ready() = ()
""")]
    public void Exports_Flags_Enum_With_Flags_Hint()
    {
        var outDir = FsBatch.GetOutDir<ExportTypesAndDefaultsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Flaggy.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Flaggy.cs not generated");
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.Flags, \"None,One,Two,Three\")]", src);
        StringAssert.Contains("public ShimGen.Tests.ExportTypesAndDefaultsTests.TestFlags Mask", src);
    }

    [Test]
    [FsCase("Bag", """
namespace Game

open System.Collections.Generic
open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Bag", BaseTypeName="Godot.Node")>]
type BagImpl() =
    member val Numbers : List<int> = new List<int>() with get, set
    member val Map : Dictionary<string,int> = new Dictionary<string,int>() with get, set
    member val Texture : Texture2D = null with get, set
    member val Scene : PackedScene = null with get, set
    member _.Ready() = ()
""")]
    public void Exports_Collections_And_Resources()
    {
        var outDir = FsBatch.GetOutDir<ExportTypesAndDefaultsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Bag.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export] public System.Collections.Generic.List<System.Int32> Numbers", src);
        StringAssert.Contains("[Export] public System.Collections.Generic.Dictionary<System.String, System.Int32> Map", src);
        StringAssert.Contains("[Export] public Godot.Texture2D Texture", src);
        StringAssert.Contains("[Export] public Godot.PackedScene Scene", src);
    }

    [Test]
    [FsCase("Typer", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Typer", BaseTypeName="Godot.Node")>]
type TyperImpl() =
    member val B : Basis = new Basis() with get, set
    member val R : Rect2 = new Rect2() with get, set
    member val T2 : Transform2D = new Transform2D() with get, set
    member val T3 : Transform3D = new Transform3D() with get, set
    member val P : NodePath = new NodePath("") with get, set
    member val S : StringName = new StringName("") with get, set
    member val Id : RID = new RID() with get, set
    member _.Ready() = ()
""")]
    public void Exports_Godot_Math_And_Engine_Types()
    {
        var outDir = FsBatch.GetOutDir<ExportTypesAndDefaultsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Typer.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export] public Godot.Basis B", src);
        StringAssert.Contains("[Export] public Godot.Rect2 R", src);
        StringAssert.Contains("[Export] public Godot.Transform2D T2", src);
        StringAssert.Contains("[Export] public Godot.Transform3D T3", src);
        StringAssert.Contains("[Export] public Godot.NodePath P", src);
        StringAssert.Contains("[Export] public Godot.StringName S", src);
        StringAssert.Contains("[Export] public Godot.RID Id", src);
    }

    [Test]
    [FsCase("Def", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Def", BaseTypeName="Godot.Node")>]
type DefImpl() =
    member val A : int = 123 with get, set
    member _.Ready() = ()
""")]
    public void Export_Default_Value_Comes_From_Impl()
    {
        var outDir = FsBatch.GetOutDir<ExportTypesAndDefaultsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Def.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Def.cs not generated");
        var src = File.ReadAllText(path!);
        var stubsAsm = typeof(Godot.Node).Assembly;
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var implPath = FsBatch.GetImplAssemblyPath<ExportTypesAndDefaultsTests>()!;
        var shimDll = TestHelpers.CompileCSharp(src, new[] {
            TestHelpers.RefFromAssembly(stubsAsm),
            TestHelpers.RefFromPath(annPath),
            MetadataReference.CreateFromFile(implPath)
        }, asmName: "DefShim");

        // Ensure the impl assembly is resolvable when the shim's constructor runs
        var shimDir = Path.GetDirectoryName(shimDll)!;
        var targetImplPath = Path.Combine(shimDir, Path.GetFileName(implPath));
        if (!File.Exists(targetImplPath)) File.Copy(implPath, targetImplPath, overwrite: true);
        _ = Assembly.LoadFrom(targetImplPath);

        var asm = Assembly.LoadFrom(shimDll);
        var shimType = asm.GetType("Generated.Def");
        Assert.That(shimType, Is.Not.Null);
        var o = Activator.CreateInstance(shimType!);
        var p = shimType!.GetProperty("A");
        Assert.That(p, Is.Not.Null);
        var val = (int)p!.GetValue(o!)!;
        Assert.That(val, Is.EqualTo(123));
    }
}
