using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Headsetsniper.Godot.FSharp.ShimGen;

internal static partial class RoslynCodeGenerator
{
    private static IEnumerable<MemberDeclarationSyntax> BuildSignalMembers(ScriptSpec spec)
    {
        var list = new List<MemberDeclarationSyntax>();
        foreach (var sig in spec.Signals)
        {
            TypeSyntax actionType;
            if (sig.ParamTypes.Length == 0)
            {
                actionType = SyntaxFactory.ParseTypeName("System.Action");
            }
            else
            {
                var typeArgs = sig.ParamTypes.Select(t => SyntaxFactory.ParseTypeName(GetTypeDisplayName(t)));
                actionType = SyntaxFactory.GenericName("System.Action").WithTypeArgumentList(SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(typeArgs)));
            }

            var eventVar = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(sig.Name));
            var eventDecl = SyntaxFactory.EventFieldDeclaration(
                    SyntaxFactory.VariableDeclaration(actionType).WithVariables(SyntaxFactory.SingletonSeparatedList(eventVar)))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithEventKeyword(SyntaxFactory.Token(SyntaxKind.EventKeyword))
                .WithAttributeLists(
                    SyntaxFactory.SingletonList(
                        SyntaxFactory.AttributeList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Signal"))))));
            list.Add(eventDecl);

            var method = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Emit" + sig.Name)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            if (sig.ParamTypes.Length > 0)
            {
                var ps = sig.ParamTypes.Select((t, i) => SyntaxFactory.Parameter(SyntaxFactory.Identifier(sig.ParamNames[i]))
                    .WithType(SyntaxFactory.ParseTypeName(GetTypeDisplayName(t))));
                method = method.WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(ps)));
            }

            ExpressionSyntax invokeExpr;
            if (sig.ParamTypes.Length == 0)
            {
                invokeExpr = SyntaxFactory.ConditionalAccessExpression(
                    SyntaxFactory.IdentifierName(sig.Name),
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("Invoke"))));
            }
            else
            {
                var args = sig.ParamNames.Select(n => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(n)));
                invokeExpr = SyntaxFactory.ConditionalAccessExpression(
                    SyntaxFactory.IdentifierName(sig.Name),
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("Invoke")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args))));
            }

            method = method
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(invokeExpr))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            list.Add(method);
        }
        return list;
    }
}
