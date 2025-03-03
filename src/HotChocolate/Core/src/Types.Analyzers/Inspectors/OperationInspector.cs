using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HotChocolate.Types.Analyzers.Inspectors;

public sealed class OperationInspector(OperationType operationType) : IAttributeWithMetadataInspector
{
    public string FullyQualifiedMetadataName => operationType switch
    {
        OperationType.Query => WellKnownAttributes.QueryAttribute,
        OperationType.Mutation => WellKnownAttributes.MutationAttribute,
        OperationType.Subscription => WellKnownAttributes.SubscriptionAttribute,
        _ => throw new ArgumentOutOfRangeException(nameof(operationType), operationType, null)
    };

    public bool Predicate(SyntaxNode syntaxNode, CancellationToken cancellationToken) =>
        syntaxNode is MethodDeclarationSyntax { AttributeLists.Count: > 0 };

    public SyntaxInfo? Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not IMethodSymbol { IsStatic: true } methodSymbol)
        {
            return null;
        }

        return new OperationInfo(
            operationType,
            methodSymbol.ContainingType.ToDisplayString(),
            methodSymbol.Name);
    }
}
