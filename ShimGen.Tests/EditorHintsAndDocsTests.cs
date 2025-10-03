using System.IO;
using System.Linq;
using NUnit.Framework;
using Headsetsniper.Godot.FSharp.ShimGen;

namespace ShimGen.Tests;

[TestFixture]
public class EditorHintsAndDocsTests
{
    [OneTimeSetUp]
    public void BeforeAll()
    {
        FsBatchComponent.BuildForFixture(typeof(EditorHintsAndDocsTests));
    }

    [OneTimeTearDown]
    public void AfterAll()
    {
        FsBatchComponent.CleanupForFixture(typeof(EditorHintsAndDocsTests));
    }

    [Test]
    [FsCase("Rangey", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Rangey", BaseTypeName="Godot.Node")>]
type RangeyImpl() =
    [<ExportRange(0.0, 10.0, 0.5, true)>]
    member val Speed : single = 0.0f with get, set
    member _.Ready() = ()
""")]
    public void Export_Range_Hint_Is_Emitted()
    {
        var outDir = FsBatch.GetOutDir<EditorHintsAndDocsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Rangey.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.Range, \"0,10,0.5,1\")]", src);
    }

    [Test]
    [FsCase("Paths", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Paths", BaseTypeName="Godot.Node")>]
type PathsImpl() =
    [<ExportFile("*.png,*.jpg")>]
    member val ImagePath : string = null with get, set
    [<ExportDir>]
    member val Folder : string = null with get, set
    member _.Ready() = ()
""")]
    public void Export_File_And_Dir_Hints_Are_Emitted()
    {
        var outDir = FsBatch.GetOutDir<EditorHintsAndDocsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Paths.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.File, \"*.png,*.jpg\")] public System.String ImagePath", src);
        StringAssert.Contains("[Export(PropertyHint.Dir)] public System.String Folder", src);
    }

    [Test]
    [FsCase("Hints", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Hints", BaseTypeName="Godot.Node")>]
type HintsImpl() =
    [<ExportResourceType("Texture2D")>]
    member val TexturePath : string = null with get, set
    [<ExportEnumList("A,B,C")>]
    member val Choice : string = null with get, set
    [<ExportMultiline>]
    member val Description : string = null with get, set
    member _.Ready() = ()
""")]
    public void Export_ResourceType_And_EnumList_And_Multiline()
    {
        var outDir = FsBatch.GetOutDir<EditorHintsAndDocsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Hints.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.ResourceType, \"Texture2D\")] public System.String TexturePath", src);
        StringAssert.Contains("[Export(PropertyHint.Enum, \"A,B,C\")] public System.String Choice", src);
        StringAssert.Contains("[Export(PropertyHint.MultilineText)] public System.String Description", src);
    }

    [Test]
    [FsCase("Visuals", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Visuals", BaseTypeName="Godot.Node")>]
type VisualsImpl() =
    [<ExportColorNoAlpha>]
    member val Tint : Color = new Color() with get, set
    [<ExportLayerMask2DRender>]
    member val Layers : int = 0 with get, set
    member _.Ready() = ()
""")]
    public void Export_ColorNoAlpha_And_LayerMask2D()
    {
        var outDir = FsBatch.GetOutDir<EditorHintsAndDocsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Visuals.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[Export(PropertyHint.ColorNoAlpha)] public Godot.Color Tint", src);
        StringAssert.Contains("[Export(PropertyHint.Layers2DRender)] public System.Int32 Layers", src);
    }

    [Test]
    [FsCase("Docs", """
namespace Game

open Godot
open Headsetsniper.Godot.FSharp.Annotations
[<GodotScript(ClassName="Docs", BaseTypeName="Godot.Node")>]
type DocsImpl() =
    [<ExportCategory("Movement")>]
    [<ExportSubgroup("Speed", Prefix="spd_")>]
    [<ExportTooltip("Units per second")>]
    member val Speed : single = 0.0f with get, set
    member _.Ready() = ()
""")]
    public void Export_Category_Subgroup_Tooltip_Are_Emitted()
    {
        var outDir = FsBatch.GetOutDir<EditorHintsAndDocsTests>();
        var path = Directory.EnumerateFiles(outDir!, "Docs.cs", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(path, Is.Not.Null);
        var src = File.ReadAllText(path!);
        StringAssert.Contains("[ExportCategory(\"Movement\")]", src);
        StringAssert.Contains("[ExportSubgroup(\"Speed\", Prefix=\"spd_\")]", src);
        StringAssert.Contains("[ExportTooltip(\"Units per second\")]", src);
        StringAssert.Contains("[Export] public System.Single Speed", src);
    }
}
