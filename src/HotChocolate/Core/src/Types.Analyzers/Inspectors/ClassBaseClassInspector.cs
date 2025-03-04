using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using HotChocolate.Types.Analyzers.Filters;
using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeInfo = HotChocolate.Types.Analyzers.Models.TypeInfo;

namespace HotChocolate.Types.Analyzers.Inspectors;

public class ClassBaseClassInspector : ISyntaxInspector
{
    public IReadOnlyList<ISyntaxFilter> Filters => [ClassWithBaseClass.Instance];

    public bool TryHandle(
        GeneratorSyntaxContext context,
        [NotNullWhen(true)] out SyntaxInfo? syntaxInfo)
    {
        if (context.Node is ClassDeclarationSyntax { BaseList: { Types.Count: > 0 } baseList, TypeParameterList: null, } possibleType)
        {
            var relevantBaseTypes = WellKnownTypes.TypeClass
                .SelectMany(n => context.SemanticModel.Compilation.GetTypesByMetadataName(n))
                .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var baseTypeSyntax in baseList.Types)
            {
                if(context.SemanticModel.GetSymbolInfo(baseTypeSyntax.Type).Symbol is ITypeSymbol baseTypeSymbol &&
                    relevantBaseTypes.Any(t => context.SemanticModel.Compilation.HasImplicitConversion(baseTypeSymbol, t)))
                {
                    var model = context.SemanticModel.GetDeclaredSymbol(possibleType);
                    if (model is { IsAbstract: false, })
                    {
                        var typeDisplayString = model.ToDisplayString();
                        syntaxInfo = new TypeInfo(typeDisplayString);
                        return true;
                    }
                }
            }
            var relevantExtensionBaseTypes = WellKnownTypes.TypeExtensionClass
                .SelectMany(n => context.SemanticModel.Compilation.GetTypesByMetadataName(n))
                .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var baseTypeSyntax in baseList.Types)
            {
                if(context.SemanticModel.GetSymbolInfo(baseTypeSyntax.Type).Symbol is ITypeSymbol baseTypeSymbol &&
                    relevantExtensionBaseTypes.Any(t => context.SemanticModel.Compilation.HasImplicitConversion(baseTypeSymbol, t)))
                {
                    var model = context.SemanticModel.GetDeclaredSymbol(possibleType);
                    if (model is { IsAbstract: false, })
                    {
                        var typeDisplayString = model.ToDisplayString();
                        syntaxInfo = new TypeExtensionInfo(typeDisplayString, false);
                        return true;
                    }
                }
            }

            var relevantDataLoaderBaseTypes = context.SemanticModel.Compilation.GetTypesByMetadataName(WellKnownTypes.DataLoader)
                .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var baseTypeSyntax in baseList.Types)
            {
                if(context.SemanticModel.GetSymbolInfo(baseTypeSyntax.Type).Symbol is ITypeSymbol baseTypeSymbol &&
                    relevantDataLoaderBaseTypes.Any(t => context.SemanticModel.Compilation.HasImplicitConversion(baseTypeSymbol, t)))
                {
                    var model = context.SemanticModel.GetDeclaredSymbol(possibleType);
                    if (model is { IsAbstract: false, })
                    {
                        var typeDisplayString = model.ToDisplayString();
                        syntaxInfo = new RegisterDataLoaderInfo(typeDisplayString);
                        return true;
                    }
                }
            }
        }

        syntaxInfo = null;
        return false;
    }
}
