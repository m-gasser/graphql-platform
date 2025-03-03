using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static HotChocolate.Types.Analyzers.WellKnownAttributes;
using TypeInfo = HotChocolate.Types.Analyzers.Models.TypeInfo;

namespace HotChocolate.Types.Analyzers.Inspectors;

public sealed class TypeAttributeInspector(string fullyQualifiedAttributeName) : IAttributeWithMetadataInspector
{
    public string FullyQualifiedMetadataName => fullyQualifiedAttributeName;

    public bool Predicate(SyntaxNode syntaxNode, CancellationToken cancellationToken) =>
        syntaxNode is BaseTypeDeclarationSyntax { AttributeLists.Count: > 0, };

    public SyntaxInfo? Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var possibleType = (BaseTypeDeclarationSyntax)context.TargetNode;
        foreach (var attributeSyntax in context.Attributes)
        {
            var attributeSymbol = attributeSyntax.AttributeConstructor;

            if (attributeSymbol is null)
            {
                continue;
            }

            var attributeContainingTypeSymbol = attributeSymbol.ContainingType;

            if (fullyQualifiedAttributeName is ExtendObjectTypeAttribute or ExtendObjectTypeAttributeGeneric &&
                context.SemanticModel.GetDeclaredSymbol(possibleType) is { } typeExt)
            {
                return new TypeExtensionInfo(
                    typeExt.ToDisplayString(),
                    possibleType.Modifiers.Any(t => t.IsKind(SyntaxKind.StaticKeyword)));
            }

            if (attributeContainingTypeSymbol.TypeArguments.Length == 0 &&
                TypeAttributes.Contains(fullyQualifiedAttributeName) &&
                context.SemanticModel.GetDeclaredSymbol(possibleType) is { } type)
            {
                if (fullyQualifiedAttributeName is QueryTypeAttribute)
                {
                    if (type.IsStatic && possibleType.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        return null;
                    }

                    return new TypeExtensionInfo(
                        type.ToDisplayString(),
                        possibleType.Modifiers.Any(t => t.IsKind(SyntaxKind.StaticKeyword)),
                        OperationType.Query);
                }

                if (fullyQualifiedAttributeName is MutationTypeAttribute)
                {
                    if (type.IsStatic && possibleType.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        return null;
                    }

                    return new TypeExtensionInfo(
                        type.ToDisplayString(),
                        possibleType.Modifiers.Any(t => t.IsKind(SyntaxKind.StaticKeyword)),
                        OperationType.Mutation);
                }

                if (fullyQualifiedAttributeName is SubscriptionTypeAttribute)
                {
                    if (type.IsStatic && possibleType.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        return null;
                    }

                    return new TypeExtensionInfo(
                        type.ToDisplayString(),
                        possibleType.Modifiers.Any(t => t.IsKind(SyntaxKind.StaticKeyword)),
                        OperationType.Subscription);
                }

                return new TypeInfo(type.ToDisplayString());
            }
        }

        return null;
    }
}
