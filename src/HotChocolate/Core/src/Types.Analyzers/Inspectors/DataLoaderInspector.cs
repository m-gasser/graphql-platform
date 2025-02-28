using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HotChocolate.Types.Analyzers.Inspectors;

public sealed class DataLoaderInspector : IAttributeWithMetadataInspector
{
    public string FullyQualifiedMetadataName => WellKnownAttributes.DataLoaderAttribute;

    public bool Predicate(SyntaxNode syntaxNode, CancellationToken cancellationToken) =>
        syntaxNode is MethodDeclarationSyntax { AttributeLists.Count: > 0 };

    public SyntaxInfo? Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var methodSyntax = (MethodDeclarationSyntax)context.TargetNode;
        foreach (var attribute in context.Attributes)
        {
            if (context.TargetSymbol is IMethodSymbol methodSymbol
                && attribute.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax attributeSyntax
                && attribute.AttributeConstructor is {} attributeConstructor)
            {
                return new DataLoaderInfo(
                    attributeSyntax,
                    attributeConstructor,
                    methodSymbol,
                    methodSyntax);
            }
        }

        return null;
    }
}
