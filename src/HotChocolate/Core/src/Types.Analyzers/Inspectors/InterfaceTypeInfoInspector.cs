using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using HotChocolate.Types.Analyzers.Helpers;
using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HotChocolate.Types.Analyzers.Inspectors;

public class InterfaceTypeInfoInspector(string fullyQualifiedAttributeName) : IAttributeWithMetadataInspector
{
    public string FullyQualifiedMetadataName => fullyQualifiedAttributeName;

    public bool Predicate(SyntaxNode syntaxNode, CancellationToken cancellationToken) =>
        syntaxNode is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    public SyntaxInfo? Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray<Diagnostic>.Empty;

        if (!IsInterfaceType(context, out var possibleType, out var classSymbol, out var runtimeType))
        {
            return null;
        }

        if (!possibleType.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            diagnostics = diagnostics.Add(
                Diagnostic.Create(
                    Errors.InterfaceTypePartialKeywordMissing,
                    Location.Create(possibleType.SyntaxTree, possibleType.Span)));
        }

        if (!possibleType.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            diagnostics = diagnostics.Add(
                Diagnostic.Create(
                    Errors.InterfaceTypeStaticKeywordMissing,
                    Location.Create(possibleType.SyntaxTree, possibleType.Span)));
        }

        var members = classSymbol.GetMembers();
        var resolvers = new Resolver[members.Length];
        var i = 0;

        foreach (var member in members)
        {
            if (member.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary } methodSymbol)
                {
                    resolvers[i++] = CreateResolver(context, classSymbol, methodSymbol);
                    continue;
                }

                if (member is IPropertySymbol)
                {
                    resolvers[i++] = new Resolver(
                        classSymbol.Name,
                        member,
                        ResolverResultKind.Pure,
                        ImmutableArray<ResolverParameter>.Empty,
                        ImmutableArray<MemberBinding>.Empty);
                }
            }
        }

        if (i > 0 && i < resolvers.Length)
        {
            Array.Resize(ref resolvers, i);
        }

        var syntaxInfo = new InterfaceTypeExtensionInfo(
            classSymbol,
            runtimeType,
            possibleType,
            i == 0
                ? ImmutableArray<Resolver>.Empty
                : resolvers.ToImmutableArray());

        if (diagnostics.Length > 0)
        {
            syntaxInfo.AddDiagnosticRange(diagnostics);
        }

        return syntaxInfo;
    }

    private bool IsInterfaceType(
        GeneratorAttributeSyntaxContext context,
        [NotNullWhen(true)] out ClassDeclarationSyntax? resolverTypeSyntax,
        [NotNullWhen(true)] out INamedTypeSymbol? resolverTypeSymbol,
        [NotNullWhen(true)] out INamedTypeSymbol? runtimeType)
    {
        foreach (var attribute in context.Attributes)
        {
            if (attribute.AttributeConstructor is not { } attributeSymbol)
            {
                continue;
            }

            var attributeContainingTypeSymbol = attributeSymbol.ContainingType;

            if (fullyQualifiedAttributeName is WellKnownAttributes.InterfaceTypeAttribute or WellKnownAttributes.InterfaceTypeAttributeGeneric
                && attributeContainingTypeSymbol.TypeArguments.Length == 1
                && attributeContainingTypeSymbol.TypeArguments[0] is INamedTypeSymbol rt &&
                context.TargetNode is ClassDeclarationSyntax possibleType
                && context.TargetSymbol is INamedTypeSymbol rts)
            {
                resolverTypeSyntax = possibleType;
                resolverTypeSymbol = rts;
                runtimeType = rt;
                return true;
            }
        }

        resolverTypeSyntax = null;
        resolverTypeSymbol = null;
        runtimeType = null;
        return false;
    }

    private static Resolver CreateResolver(
        GeneratorAttributeSyntaxContext context,
        INamedTypeSymbol resolverType,
        IMethodSymbol resolverMethod)
    {
        var compilation = context.SemanticModel.Compilation;
        var parameters = resolverMethod.Parameters;
        var resolverParameters = new ResolverParameter[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            resolverParameters[i] = ResolverParameter.Create(parameters[i], compilation);
        }

        return new Resolver(
            resolverType.Name,
            resolverMethod,
            resolverMethod.GetResultKind(),
            resolverParameters.ToImmutableArray(),
            ImmutableArray<MemberBinding>.Empty);
    }
}
