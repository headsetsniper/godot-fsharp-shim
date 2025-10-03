using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.Annotations;

namespace ShimGen.Tests;

[TestFixture]
public class ExportTypesAndDefaultsTests
{
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
    public void Exports_Primitive_Properties()
    {
        var impl = IntegrationTestUtil.BuildImplAssembly();
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var fooPath = Directory.EnumerateFiles(outDir, "Foo.cs", SearchOption.AllDirectories).First();
        var src = File.ReadAllText(fooPath);
        StringAssert.Contains("[Export] public System.Int32 Speed", src);

        var stubsAsm = typeof(Godot.Node2D).Assembly;
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var shimDll = TestHelpers.CompileCSharp(src, new[] {
            TestHelpers.RefFromAssembly(stubsAsm),
            TestHelpers.RefFromPath(annPath),
            MetadataReference.CreateFromFile(impl)
        }, asmName: "FooShim");
        Assert.That(File.Exists(shimDll), Is.True);
    }

    [Test]
    public void Exports_Godot_Struct_Types()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Baz\", BaseTypeName=\"Godot.Node\")]",
            "  public class BazImpl {",
            "    public Vector2 V2 { get; set; }",
            "    public Vector3 V3 { get; set; }",
            "    public Color C { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "BazImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var bazPath = Directory.EnumerateFiles(outDir, "Baz.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(bazPath, Is.Not.Null, "Baz.cs not generated");
        var src = File.ReadAllText(bazPath!);
        StringAssert.Contains("[Export] public Godot.Vector2 V2", src);
        StringAssert.Contains("[Export] public Godot.Vector3 V3", src);
        StringAssert.Contains("[Export] public Godot.Color C", src);
    }

    [Test]
    public void Exports_Only_Primitive_Properties()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Bar\", BaseTypeName=\"Godot.Node\")]",
            "  public class BarImpl {",
            "    public int A {get;set;}",
            "    public string S {get;set;}",
            "    public object O {get;set;}",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "BarImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var barPath = Directory.EnumerateFiles(outDir, "Bar.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(barPath, Is.Not.Null, "Bar.cs not generated");
        var src = File.ReadAllText(barPath!);
        StringAssert.Contains("[Export] public System.Int32 A", src);
        StringAssert.Contains("[Export] public System.String S", src);
        StringAssert.DoesNotContain("object O", src);
    }

    [Test]
    public void Exports_Primitives_And_Strings()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Prim\", BaseTypeName=\"Godot.Node\")]",
            "  public class PrimImpl {",
            "    public int I { get; set; }",
            "    public float F { get; set; }",
            "    public double D { get; set; }",
            "    public bool B { get; set; }",
            "    public string S { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "PrimImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Prim.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Prim.cs not generated");
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export] public System.Int32 I", src);
        StringAssert.Contains("[Export] public System.Single F", src);
        StringAssert.Contains("[Export] public System.Double D", src);
        StringAssert.Contains("[Export] public System.Boolean B", src);
        StringAssert.Contains("[Export] public System.String S", src);
    }

    [Test]
    public void Exports_Enum_Type()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Enumy\", BaseTypeName=\"Godot.Node\")]",
            "  public class EnumyImpl {",
            "    public ShimGen.Tests.ExportTypesAndDefaultsTests.TestEnum Mode { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath), MetadataReference.CreateFromFile(typeof(ExportTypesAndDefaultsTests).Assembly.Location) }, asmName: "EnumyImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Enumy.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Enumy.cs not generated");
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export] public ShimGen.Tests.ExportTypesAndDefaultsTests.TestEnum Mode", src);
    }

    [Test]
    public void Exports_Arrays_And_Enums()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Qux\", BaseTypeName=\"Godot.Node\")]",
            "  public class QuxImpl {",
            "    public int[] Numbers { get; set; }",
            "    public string[] Names { get; set; }",
            "    public ShimGen.Tests.ExportTypesAndDefaultsTests.TestEnum Mode { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath), MetadataReference.CreateFromFile(typeof(ExportTypesAndDefaultsTests).Assembly.Location) }, asmName: "QuxImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var quxPath = Directory.EnumerateFiles(outDir, "Qux.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(quxPath, Is.Not.Null, "Qux.cs not generated");
        var src = File.ReadAllText(quxPath!);
        StringAssert.Contains("[Export] public System.Int32[] Numbers", src);
        StringAssert.Contains("[Export] public System.String[] Names", src);
        StringAssert.Contains("[Export] public ShimGen.Tests.ExportTypesAndDefaultsTests.TestEnum Mode", src);
    }

    [Test]
    public void Exports_Flags_Enum_With_Flags_Hint()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Flaggy\", BaseTypeName=\"Godot.Node\")]",
            "  public class FlaggyImpl {",
            "    public ShimGen.Tests.ExportTypesAndDefaultsTests.TestFlags Mask { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath), MetadataReference.CreateFromFile(typeof(ExportTypesAndDefaultsTests).Assembly.Location) }, asmName: "FlaggyImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Flaggy.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Flaggy.cs not generated");
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.Flags, \"None,One,Two,Three\")]", src);
        StringAssert.Contains("public ShimGen.Tests.ExportTypesAndDefaultsTests.TestFlags Mask", src);
    }

    [Test]
    public void Exports_Collections_And_Resources()
    {
        var code = string.Join("\n", new[]{
            "using System.Collections.Generic; using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Bag\", BaseTypeName=\"Godot.Node\")]",
            "  public class BagImpl {",
            "    public List<int> Numbers { get; set; }",
            "    public Dictionary<string,int> Map { get; set; }",
            "    public Texture2D Texture { get; set; }",
            "    public PackedScene Scene { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "BagImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Bag.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export] public System.Collections.Generic.List<System.Int32> Numbers", src);
        StringAssert.Contains("[Export] public System.Collections.Generic.Dictionary<System.String, System.Int32> Map", src);
        StringAssert.Contains("[Export] public Godot.Texture2D Texture", src);
        StringAssert.Contains("[Export] public Godot.PackedScene Scene", src);
    }

    [Test]
    public void Exports_Godot_Math_And_Engine_Types()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Typer\", BaseTypeName=\"Godot.Node\")]",
            "  public class TyperImpl {",
            "    public Basis B { get; set; }",
            "    public Rect2 R { get; set; }",
            "    public Transform2D T2 { get; set; }",
            "    public Transform3D T3 { get; set; }",
            "    public NodePath P { get; set; }",
            "    public StringName S { get; set; }",
            "    public RID Id { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "TyperImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Typer.cs", SearchOption.AllDirectories).FirstOrDefault();
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
    public void Export_Default_Value_Comes_From_Impl()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Def\", BaseTypeName=\"Godot.Node\")]",
            "  public class DefImpl {",
            "    public int A { get; set; } = 123;",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "DefImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Def.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null, "Def.cs not generated");
        var src = File.ReadAllText(path!);
        var shimDll = TestHelpers.CompileCSharp(src, new[] {
            TestHelpers.RefFromAssembly(stubs),
            TestHelpers.RefFromPath(annPath),
            MetadataReference.CreateFromFile(impl)
        }, asmName: "DefShim");

        // Ensure the impl assembly is resolvable when the shim's constructor runs
        var shimDir = Path.GetDirectoryName(shimDll)!;
        var targetImplPath = Path.Combine(shimDir, Path.GetFileName(impl));
        if (!File.Exists(targetImplPath)) File.Copy(impl, targetImplPath, overwrite: true);
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
