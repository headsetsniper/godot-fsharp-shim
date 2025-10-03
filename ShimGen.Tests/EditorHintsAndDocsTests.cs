using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.Annotations;

namespace ShimGen.Tests;

[TestFixture]
public class EditorHintsAndDocsTests
{
    [Test]
    public void Export_Range_Hint_Is_Emitted()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Rangey\", BaseTypeName=\"Godot.Node\")]",
            "  public class RangeyImpl {",
            "    [ExportRange(0, 10, 0.5, true)] public float Speed { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "RangeyImpl");
        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Rangey.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.Range, \"0,10,0.5,1\")]", src);
    }

    [Test]
    public void Export_File_And_Dir_Hints_Are_Emitted()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Paths\", BaseTypeName=\"Godot.Node\")]",
            "  public class PathsImpl {",
            "    [ExportFile(\"*.png,*.jpg\")] public string ImagePath { get; set; }",
            "    [ExportDir] public string Folder { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "PathsImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Paths.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.File, \"*.png,*.jpg\")] public System.String ImagePath", src);
        StringAssert.Contains("[Export(PropertyHint.Dir)] public System.String Folder", src);
    }

    [Test]
    public void Export_ResourceType_And_EnumList_And_Multiline()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Hints\", BaseTypeName=\"Godot.Node\")]",
            "  public class HintsImpl {",
            "    [ExportResourceType(\"Texture2D\")] public string TexturePath { get; set; }",
            "    [ExportEnumList(\"A,B,C\")] public string Choice { get; set; }",
            "    [ExportMultiline] public string Description { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "HintsImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Hints.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.ResourceType, \"Texture2D\")] public System.String TexturePath", src);
        StringAssert.Contains("[Export(PropertyHint.Enum, \"A,B,C\")] public System.String Choice", src);
        StringAssert.Contains("[Export(PropertyHint.MultilineText)] public System.String Description", src);
    }

    [Test]
    public void Export_ColorNoAlpha_And_LayerMask2D()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Visuals\", BaseTypeName=\"Godot.Node\")]",
            "  public class VisualsImpl {",
            "    [ExportColorNoAlpha] public Color Tint { get; set; }",
            "    [ExportLayerMask2DRender] public int Layers { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "VisualsImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Visuals.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.ColorNoAlpha)] public Godot.Color Tint", src);
        StringAssert.Contains("[Export(PropertyHint.Layers2DRender)] public System.Int32 Layers", src);
    }

    [Test]
    public void Export_Category_Subgroup_Tooltip_Are_Emitted()
    {
        var code = string.Join("\n", new[]{
            "using Godot; using Headsetsniper.Godot.FSharp.Annotations;",
            "namespace Game {",
            "  [GodotScript(ClassName=\"Docs\", BaseTypeName=\"Godot.Node\")]",
            "  public class DocsImpl {",
            "    [ExportCategory(\"Movement\")]",
            "    [ExportSubgroup(\"Speed\", Prefix=\"spd_\")]",
            "    [ExportTooltip(\"Units per second\")]",
            "    public float Speed { get; set; }",
            "    public void Ready(){}",
            "  }",
            "}"
        });
        var annPath = Assembly.GetAssembly(typeof(GodotScriptAttribute))!.Location;
        var stubs = typeof(Godot.Node).Assembly;
        var impl = TestHelpers.CompileCSharp(code, new[] { TestHelpers.RefFromAssembly(stubs), TestHelpers.RefFromPath(annPath) }, asmName: "DocsImpl");

        var outDir = IntegrationTestUtil.RunShimGen(impl);
        var path = Directory.EnumerateFiles(outDir, "Docs.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[ExportCategory(\"Movement\")]", src);
        StringAssert.Contains("[ExportSubgroup(\"Speed\", Prefix=\"spd_\")]", src);
        StringAssert.Contains("[ExportTooltip(\"Units per second\")]", src);
        StringAssert.Contains("[Export] public System.Single Speed", src);
    }
}
