using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using HotChocolate.Types.Analyzers.Helpers;
using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static System.StringComparison;
using static HotChocolate.Types.Analyzers.WellKnownAttributes;

namespace HotChocolate.Types.Analyzers.Inspectors;

public class ObjectTypeExtensionInfoInspector(string fullyQualifiedAttributeName) : IAttributeWithMetadataInspector
{
    public string FullyQualifiedMetadataName => fullyQualifiedAttributeName;

    public bool Predicate(SyntaxNode syntaxNode, CancellationToken cancellationToken) =>
        syntaxNode is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    public SyntaxInfo? Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var diagnostics = ImmutableArray<Diagnostic>.Empty;

        OperationType? operationType = null;
        if (!IsObjectTypeExtension(context, out var possibleType, out var classSymbol, out var runtimeType)
            && !IsOperationType(context, out possibleType, out classSymbol, out operationType))
        {
            return null;
        }

        if (!possibleType.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            diagnostics = diagnostics.Add(
                Diagnostic.Create(
                    Errors.ObjectTypePartialKeywordMissing,
                    Location.Create(possibleType.SyntaxTree, possibleType.Span)));
        }

        if (!possibleType.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            diagnostics = diagnostics.Add(
                Diagnostic.Create(
                    Errors.ObjectTypeStaticKeywordMissing,
                    Location.Create(possibleType.SyntaxTree, possibleType.Span)));
        }

        var members = classSymbol.GetMembers();
        var resolvers = new Resolver[members.Length];
        Resolver? nodeResolver = null;
        var i = 0;

        foreach (var member in members)
        {
            if (member.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary } methodSymbol)
                {
                    if (methodSymbol.Skip())
                    {
                        continue;
                    }

                    if (methodSymbol.IsNodeResolver())
                    {
                        nodeResolver = CreateNodeResolver(context, classSymbol, methodSymbol, ref diagnostics);
                    }
                    else
                    {
                        resolvers[i++] = CreateResolver(context, classSymbol, methodSymbol);
                        continue;
                    }
                }

                if (member is IPropertySymbol)
                {
                    resolvers[i++] = new Resolver(
                        classSymbol.Name,
                        member,
                        ResolverResultKind.Pure,
                        ImmutableArray<ResolverParameter>.Empty,
                        member.GetMemberBindings());
                }
            }
        }

        if (i > 0 && i < resolvers.Length)
        {
            Array.Resize(ref resolvers, i);
        }

        if (runtimeType is not null)
        {
            var syntaxInfo = new ObjectTypeExtensionInfo(
                classSymbol,
                runtimeType,
                nodeResolver,
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
        else
        {
            var syntaxInfo = new RootTypeExtensionInfo(
                classSymbol,
                operationType!.Value,
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
    }

    private bool IsObjectTypeExtension(
        GeneratorAttributeSyntaxContext context,
        [NotNullWhen(true)] out ClassDeclarationSyntax? resolverTypeSyntax,
        [NotNullWhen(true)] out INamedTypeSymbol? resolverTypeSymbol,
        [NotNullWhen(true)] out INamedTypeSymbol? runtimeType)
    {
        if (fullyQualifiedAttributeName is ObjectTypeAttribute or ObjectTypeAttributeGeneric)
        {
            foreach (var attribute in context.Attributes)
            {
                if (attribute.AttributeConstructor is not { } attributeSymbol)
                {
                    continue;
                }

                var attributeContainingTypeSymbol = attributeSymbol.ContainingType;

                if (fullyQualifiedAttributeName is ObjectTypeAttribute or ObjectTypeAttributeGeneric &&
                    attributeContainingTypeSymbol.TypeArguments.Length == 1 &&
                    attributeContainingTypeSymbol.TypeArguments[0] is INamedTypeSymbol rt &&
                    context.TargetSymbol is INamedTypeSymbol rts &&
                    context.TargetNode is ClassDeclarationSyntax possibleType)
                {
                    resolverTypeSyntax = possibleType;
                    resolverTypeSymbol = rts;
                    runtimeType = rt;
                    return true;
                }
            }
        }

        resolverTypeSyntax = null;
        resolverTypeSymbol = null;
        runtimeType = null;
        return false;
    }

    private bool IsOperationType(
        GeneratorAttributeSyntaxContext context,
        [NotNullWhen(true)] out ClassDeclarationSyntax? resolverTypeSyntax,
        [NotNullWhen(true)] out INamedTypeSymbol? resolverTypeSymbol,
        [NotNullWhen(true)] out OperationType? operationType)
    {
        if (fullyQualifiedAttributeName is QueryTypeAttribute or MutationAttribute or SubscriptionTypeAttribute
            && context.TargetNode is ClassDeclarationSyntax possibleType
            && context.TargetSymbol is INamedTypeSymbol possibleSymbol
            && (possibleSymbol.IsStatic && possibleType.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))))
        {
            if (fullyQualifiedAttributeName is QueryTypeAttribute)
            {
                resolverTypeSyntax = possibleType;
                resolverTypeSymbol = possibleSymbol;
                operationType = OperationType.Query;
                return true;
            }

            if (fullyQualifiedAttributeName is MutationTypeAttribute)
            {
                resolverTypeSyntax = possibleType;
                resolverTypeSymbol = possibleSymbol;
                operationType = OperationType.Mutation;
                return true;
            }

            if (fullyQualifiedAttributeName is SubscriptionTypeAttribute)
            {
                resolverTypeSyntax = possibleType;
                resolverTypeSymbol = possibleSymbol;
                operationType = OperationType.Subscription;
                return true;
            }
        }

        resolverTypeSyntax = null;
        resolverTypeSymbol = null;
        operationType = null;
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
            resolverMethod.GetMemberBindings());
    }

    private static Resolver CreateNodeResolver(
        GeneratorAttributeSyntaxContext context,
        INamedTypeSymbol resolverType,
        IMethodSymbol resolverMethod,
        ref ImmutableArray<Diagnostic> diagnostics)
    {
        var compilation = context.SemanticModel.Compilation;
        var parameters = resolverMethod.Parameters;
        var resolverParameters = new ResolverParameter[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = ResolverParameter.Create(parameters[i], compilation);

            if (parameter.Kind == ResolverParameterKind.Argument)
            {
                if (parameter.Name != "id" && parameter.Key != "id")
                {
                    var location = parameters[i].Locations[0];

                    diagnostics = diagnostics.Add(
                        Diagnostic.Create(
                            Errors.InvalidNodeResolverArgumentName,
                            Location.Create(location.SourceTree!, location.SourceSpan)));
                }
            }

            if (parameter.Kind is ResolverParameterKind.Unknown && (parameter.Name == "id" || parameter.Key == "id"))
            {
                parameter = new ResolverParameter(parameter.Parameter, parameter.Key, ResolverParameterKind.Argument);
            }

            resolverParameters[i] = parameter;
        }

        if (resolverParameters.Count(t => t.Kind == ResolverParameterKind.Argument) > 1)
        {
            var location = resolverMethod.Locations[0];

            diagnostics = diagnostics.Add(
                Diagnostic.Create(
                    Errors.TooManyNodeResolverArguments,
                    Location.Create(location.SourceTree!, location.SourceSpan)));
        }

        return new Resolver(
            resolverType.Name,
            resolverMethod,
            resolverMethod.GetResultKind(),
            resolverParameters.ToImmutableArray(),
            resolverMethod.GetMemberBindings(),
            isNodeResolver: true);
    }
}

file static class Extensions
{
    public static bool IsNodeResolver(this IMethodSymbol methodSymbol)
    {
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (attribute.AttributeClass.IsOrInheritsFrom(NodeResolverAttribute))
            {
                return true;
            }
        }

        return false;
    }

    public static bool Skip(this IMethodSymbol methodSymbol)
    {
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (attribute.AttributeClass.IsOrInheritsFrom(
                DataLoaderAttribute,
                QueryAttribute,
                MutationAttribute,
                SubscriptionAttribute))
            {
                return true;
            }
        }

        return false;
    }

    public static ImmutableArray<MemberBinding> GetMemberBindings(this ISymbol member)
    {
        var bindings = ImmutableArray.CreateBuilder<MemberBinding>();

        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString().Equals(BindFieldAttribute, Ordinal) ?? false)
            {
                var name = attribute.ConstructorArguments[0].Value?.ToString();

                if (name is not null)
                {
                    bindings.Add(new MemberBinding(name, MemberBindingKind.Field));
                }
            }
            else if (attribute.AttributeClass?.ToDisplayString().Equals(BindMemberAttribute, Ordinal) ?? false)
            {
                var name = attribute.ConstructorArguments[0].Value?.ToString();

                if (name is not null)
                {
                    bindings.Add(new MemberBinding(name, MemberBindingKind.Property));
                }
            }
        }

        return bindings.ToImmutable();
    }
}
