using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class RoslynCodeGenerator
{
    private static MemberDeclarationSyntax? BuildExportPropertyMember(ScriptSpec spec, System.Reflection.PropertyInfo p)
    {
        var (isOpt, optInner) = TryUnwrapFSharpOption(p.PropertyType);
        var exportTypeName = GetTypeDisplayName(isOpt ? optInner! : p.PropertyType);
        var typeSyntax = SyntaxFactory.ParseTypeName(exportTypeName);
        var (getAccessor, setAccessor) = BuildPropertyAccessors(p, isOpt, optInner);
        var prop = SyntaxFactory.PropertyDeclaration(typeSyntax, p.Name)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[] { getAccessor, setAccessor })));
        var preAttrs = BuildPreExportAttributes(p);
        var exportAttr = BuildExportAttributeFor(p);
        var allAttrLists = new List<AttributeListSyntax>(preAttrs)
        {
            SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(exportAttr))
        };
        return prop.WithAttributeLists(SyntaxFactory.List(allAttrLists));
    }

    private static (AccessorDeclarationSyntax get, AccessorDeclarationSyntax set) BuildPropertyAccessors(System.Reflection.PropertyInfo p, bool isOpt, Type? optInner)
    {
        string getExpr = isOpt ? $"_impl.{p.Name} is null ? default : _impl.{p.Name}.Value" : $"_impl.{p.Name}";
        string setExpr = isOpt ? $"_impl.{p.Name} = Microsoft.FSharp.Core.FSharpOption<{GetTypeDisplayName(optInner!)}>.Some(value)" : $"_impl.{p.Name} = value";
        var getAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression(getExpr)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        var setAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression(setExpr)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        return (getAccessor, setAccessor);
    }

    private static List<AttributeListSyntax> BuildPreExportAttributes(System.Reflection.PropertyInfo p)
    {
        var lists = new List<AttributeListSyntax>();
        IEnumerable<System.Reflection.CustomAttributeData> Attrs() => p.GetCustomAttributesData();
        System.Reflection.CustomAttributeData? GetAttr(string fullName) => Attrs().FirstOrDefault(a => a.AttributeType.FullName == fullName);

        void AddSimpleStringAttr(string name, string text)
        {
            var attr = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(name))
                .WithArgumentList(SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(text))))));
            lists.Add(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attr)));
        }

        var cat = GetAttr(Annotations.Known.Types.ExportCategoryAttribute);
        if (cat is not null)
        {
            var name = cat.ConstructorArguments.Count > 0 ? (cat.ConstructorArguments[0].Value as string ?? string.Empty) : string.Empty;
            AddSimpleStringAttr("ExportCategory", Escape(name));
        }

        var sub = GetAttr(Annotations.Known.Types.ExportSubgroupAttribute);
        if (sub is not null)
        {
            var name = sub.ConstructorArguments.Count > 0 ? (sub.ConstructorArguments[0].Value as string ?? string.Empty) : string.Empty;
            var prefix = sub.NamedArguments.FirstOrDefault(na => na.MemberName == "Prefix").TypedValue.Value as string;
            var args = new List<AttributeArgumentSyntax>
            {
                SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(Escape(name))))
            };
            if (!string.IsNullOrEmpty(prefix))
            {
                args.Add(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(Escape(prefix!))))
                    .WithNameEquals(SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("Prefix"))));
            }
            var attr = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("ExportSubgroup"))
                .WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(args)));
            lists.Add(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attr)));
        }

        var tip = GetAttr(Annotations.Known.Types.ExportTooltipAttribute);
        if (tip is not null)
        {
            var text = tip.ConstructorArguments.Count > 0 ? (tip.ConstructorArguments[0].Value as string ?? string.Empty) : string.Empty;
            AddSimpleStringAttr("ExportTooltip", Escape(text));
        }
        return lists;
    }

    private static AttributeSyntax BuildExportAttributeFor(System.Reflection.PropertyInfo p)
    {
        IEnumerable<System.Reflection.CustomAttributeData> Attrs() => p.GetCustomAttributesData();
        System.Reflection.CustomAttributeData? GetAttr(string fullName) => Attrs().FirstOrDefault(a => a.AttributeType.FullName == fullName);
        bool HasAttr(string fullName) => Attrs().Any(a => a.AttributeType.FullName == fullName);

        AttributeSyntax BuildExportAttr(string? hintKind = null, string? hintText = null)
        {
            if (string.IsNullOrEmpty(hintKind)) return SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Export"));
            var args = new List<AttributeArgumentSyntax>
            {
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("PropertyHint"),
                        SyntaxFactory.IdentifierName(hintKind!)))
            };
            if (hintText != null)
                args.Add(SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(hintText))));
            return SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Export"))
                .WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(args)));
        }

        var rangeAttr = GetAttr(Annotations.Known.Types.ExportRangeAttribute);
        if (rangeAttr is not null)
        {
            double min = 0, max = 0, step = 0; bool slider = false;
            var ctor = rangeAttr.ConstructorArguments;
            if (ctor.Count >= 1) min = Convert.ToDouble(ctor[0].Value);
            if (ctor.Count >= 2) max = Convert.ToDouble(ctor[1].Value);
            if (ctor.Count >= 3) step = Convert.ToDouble(ctor[2].Value);
            if (ctor.Count >= 4 && ctor[3].ArgumentType == typeof(bool)) slider = (bool)ctor[3].Value!;
            return BuildExportAttr("Range", $"{min},{max},{step},{(slider ? 1 : 0)}");
        }
        if (GetAttr(Annotations.Known.Types.ExportFileAttribute) is System.Reflection.CustomAttributeData fileAttr)
        {
            var filter = fileAttr.ConstructorArguments.Count > 0
                ? (fileAttr.ConstructorArguments[0].Value as string ?? string.Empty)
                : (fileAttr.NamedArguments.FirstOrDefault(na => na.MemberName == "Filter").TypedValue.Value as string ?? string.Empty);
            return BuildExportAttr("File", Escape(filter));
        }
        if (HasAttr(Annotations.Known.Types.ExportDirAttribute))
            return BuildExportAttr("Dir", null);
        if (GetAttr(Annotations.Known.Types.ExportResourceTypeAttribute) is System.Reflection.CustomAttributeData resAttr)
        {
            var typeName = resAttr.ConstructorArguments.Count > 0 ? (resAttr.ConstructorArguments[0].Value as string ?? string.Empty) : string.Empty;
            return BuildExportAttr("ResourceType", Escape(typeName));
        }
        if (p.PropertyType == typeof(string) && HasAttr(Annotations.Known.Types.ExportMultilineAttribute))
            return BuildExportAttr("MultilineText", null);
        if (p.PropertyType == typeof(string) && GetAttr(Annotations.Known.Types.ExportEnumListAttribute) is System.Reflection.CustomAttributeData enumList)
        {
            var values = enumList.ConstructorArguments.Count > 0 ? (enumList.ConstructorArguments[0].Value as string ?? string.Empty) : string.Empty;
            return BuildExportAttr("Enum", Escape(values));
        }
        if (p.PropertyType.FullName == KnownGodot.Color && HasAttr(Annotations.Known.Types.ExportColorNoAlphaAttribute))
            return BuildExportAttr("ColorNoAlpha", null);
        if (HasAttr(Annotations.Known.Types.ExportLayerMask2DRenderAttribute))
            return BuildExportAttr("Layers2DRender", null);
        if (p.PropertyType.IsEnum && p.PropertyType.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "System.FlagsAttribute"))
        {
            string hintList = string.Join(',', Enum.GetNames(p.PropertyType));
            return BuildExportAttr("Flags", hintList);
        }
        return SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Export"));
    }
}
