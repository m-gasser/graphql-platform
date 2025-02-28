using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HotChocolate.Types.Analyzers.Inspectors;

public sealed class OperationInspector : IAttributeWithMetadataInspector
{
    public string FullyQualifiedMetadataName => WellKnownAttributes.DataLoaderAttribute;

    public bool Predicate(SyntaxNode syntaxNode, CancellationToken cancellationToken) =>
        syntaxNode is MethodDeclarationSyntax { AttributeLists.Count: > 0 };

    public SyntaxInfo? Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        foreach (var attribute in context.Attributes)
        {
            if (context.TargetSymbol is IMethodSymbol {IsStatic:true} methodSymbol
                && attribute.AttributeClass is {} attributeClass)
            {
                var attributeContainingTypeSymbol = attributeClass.ContainingType;
                var fullName = attributeContainingTypeSymbol.ToDisplayString();
                var operationType = ParseOperationType(fullName);

                if(operationType == OperationType.No)
                {
                    continue;
                }

                return new OperationInfo(
                    operationType,
                    methodSymbol.ContainingType.ToDisplayString(),
                    methodSymbol.Name);
            }
        }

        return null;
    }

    private OperationType ParseOperationType(string attributeName)
    {
        if (attributeName.Equals(WellKnownAttributes.QueryAttribute, StringComparison.Ordinal))
        {
            return OperationType.Query;
        }

        if (attributeName.Equals(WellKnownAttributes.MutationAttribute, StringComparison.Ordinal))
        {
            return OperationType.Mutation;
        }

        if (attributeName.Equals(WellKnownAttributes.SubscriptionAttribute, StringComparison.Ordinal))
        {
            return OperationType.Subscription;
        }

        return OperationType.No;
    }
}
