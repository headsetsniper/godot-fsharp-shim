using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class RoslynCodeGenerator
{
    private static IEnumerable<MemberDeclarationSyntax> BuildLifecycleAndReadyMembers(ScriptSpec spec)
    {
        var list = new List<MemberDeclarationSyntax>();
        if (spec.HasEnterTree) list.Add(BuildExprOverride("void", "_EnterTree", "_impl.EnterTree()"));

        if (spec.HasReady)
        {
            var method = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "_Ready")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
            var stmts = BuildReadyStatements(spec);
            method = method.WithBody(SyntaxFactory.Block(stmts));
            list.Add(method);
        }

        if (spec.HasExitTree) list.Add(BuildExprOverride("void", "_ExitTree", "_impl.ExitTree()"));
        if (spec.HasProcess) list.Add(BuildExprOverride("void", "_Process", "_impl.Process(delta)", ("double", "delta")));
        if (spec.HasPhysicsProcess) list.Add(BuildExprOverride("void", "_PhysicsProcess", "_impl.PhysicsProcess(delta)", ("double", "delta")));
        if (spec.HasInput) list.Add(BuildExprOverride("void", "_Input", "_impl.Input(@event)", ("Godot.InputEvent", "@event")));
        if (spec.HasUnhandledInput) list.Add(BuildExprOverride("void", "_UnhandledInput", "_impl.UnhandledInput(@event)", ("Godot.InputEvent", "@event")));
        if (spec.HasNotification) list.Add(BuildExprOverride("void", "_Notification", "_impl.Notification(what)", ("long", "what")));
        return list;
    }

    private static IEnumerable<StatementSyntax> BuildReadyStatements(ScriptSpec spec)
    {
        var stmts = new List<StatementSyntax>();
        var shimDisplayLiteral = Escape(spec.ClassName);
        var implDisplayLiteral = Escape(spec.ImplType.FullName ?? spec.ImplType.Name ?? spec.ClassName);
        stmts.Add(SyntaxFactory.ParseStatement($"if (_impl is IGdScript<{spec.BaseTypeName}> gd)\n    gd.Node = this;\n"));
        stmts.AddRange(BuildNodePathWiring(spec, shimDisplayLiteral, implDisplayLiteral));
        stmts.AddRange(BuildPreloadWiring(spec, shimDisplayLiteral, implDisplayLiteral));
        stmts.AddRange(BuildAutoConnects(spec));
        stmts.Add(SyntaxFactory.ParseStatement("_impl.Ready();\n"));
        return stmts;
    }

    private static IEnumerable<StatementSyntax> BuildNodePathWiring(ScriptSpec spec, string shim, string impl)
    {
        foreach (var np in spec.NodePathMembers)
        {
            var pathExpr = string.IsNullOrEmpty(np.Path) ? $"nameof({np.Name})" : $"\"{Escape(np.Path!)}\"";
            yield return SyntaxFactory.ParseStatement($"var __n_{np.Name} = GetNodeOrNull<{GetTypeDisplayName(np.MemberType)}>(new NodePath({pathExpr}));\n");
            if (np.IsOption)
                yield return SyntaxFactory.ParseStatement($"_impl.{np.Name} = __n_{np.Name} == null ? Microsoft.FSharp.Core.FSharpOption<{GetTypeDisplayName(np.MemberType)}>.None : Microsoft.FSharp.Core.FSharpOption<{GetTypeDisplayName(np.MemberType)}>.Some(__n_{np.Name});\n");
            else
            {
                yield return SyntaxFactory.ParseStatement($"if (__n_{np.Name} == null) throw new System.InvalidOperationException(\"[shimgen][{shim}] Missing required NodePath for {np.Name} on {impl}\");\n");
                yield return SyntaxFactory.ParseStatement($"_impl.{np.Name} = __n_{np.Name};\n");
            }
        }
    }

    private static IEnumerable<StatementSyntax> BuildPreloadWiring(ScriptSpec spec, string shim, string impl)
    {
        foreach (var pl in spec.PreloadMembers)
        {
            var sanitizedPath = Escape(pl.Path ?? string.Empty);
            var memberNameLiteral = Escape(pl.Name);
            var memberKind = pl.IsProperty ? "property" : "field";
            var loadVar = $"__p_{pl.Name}";
            yield return SyntaxFactory.ParseStatement($"var {loadVar} = ResourceLoader.Load<{GetTypeDisplayName(pl.MemberType)}>(\"{sanitizedPath}\");\n");
            yield return SyntaxFactory.ParseStatement($"if ({loadVar} == null) throw new System.InvalidOperationException(\"[shimgen][{shim}] Missing preload resource \\\"{sanitizedPath}\\\" for {memberKind} \\\"{memberNameLiteral}\\\" on {impl}\");\n");
            if (pl.IsOption)
                yield return SyntaxFactory.ParseStatement($"_impl.{pl.Name} = Microsoft.FSharp.Core.FSharpOption<{GetTypeDisplayName(pl.MemberType)}>.Some({loadVar});\n");
            else
                yield return SyntaxFactory.ParseStatement($"_impl.{pl.Name} = {loadVar};\n");
        }
    }

    private static IEnumerable<StatementSyntax> BuildAutoConnects(ScriptSpec spec)
    {
        foreach (var ac in spec.AutoConnects)
        {
            var pnames = Enumerable.Range(0, ac.ParamTypes.Length).Select(i => "arg" + i).ToArray();
            var paramDecls = string.Join(", ", ac.ParamTypes.Select(GetTypeDisplayName).Zip(pnames, (t, n) => t + " " + n));
            var argList = string.Join(", ", pnames);
            var connect = $"GetNodeOrNull<Node>(new NodePath(\"{Escape(ac.Path)}\"))?.Connect(\"{Escape(ac.Signal)}\", Callable.From(({paramDecls}) => _impl.{ac.HandlerName}({argList})))";
            yield return SyntaxFactory.ParseStatement(connect.Replace("( )", "()") + ";\n");
        }
    }

    private static MethodDeclarationSyntax BuildExprOverride(string returnType, string name, string expr, params (string type, string id)[] parms)
    {
        var method = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(returnType), name)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
        if (parms.Length > 0)
        {
            var ps = parms.Select(p => SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.id)).WithType(SyntaxFactory.ParseTypeName(p.type)));
            method = method.WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(ps)));
        }
        method = method
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression(expr)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        return method;
    }
}
