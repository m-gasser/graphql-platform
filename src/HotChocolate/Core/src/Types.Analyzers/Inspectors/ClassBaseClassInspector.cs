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
        if (context.Node is not ClassDeclarationSyntax { BaseList: { Types.Count: > 0 } baseList, TypeParameterList: null, } possibleType)
        {
            syntaxInfo = null;
            return false;
        }

        var baseTypeSymbols = baseList.Types
            .Select(t => context.SemanticModel.GetSymbolInfo(t.Type).Symbol)
            .OfType<ITypeSymbol>()
            .ToList();
        if (baseTypeSymbols.All(t => t.ContainingNamespace.ConstituentNamespaces[0].Name is not ("HotChocolate" or "GreenDonut")))
        {
            syntaxInfo = null;
            return false;
        }

        foreach (var baseTypeSymbol in baseTypeSymbols)
        {
            if (WellKnownTypes.TypeClass
                .SelectMany(n => context.SemanticModel.Compilation.GetTypesByMetadataName(n))
                .Any(t => context.SemanticModel.Compilation.HasImplicitConversion(baseTypeSymbol, t)))
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

        foreach (var baseTypeSymbol in baseTypeSymbols)
        {
            if (WellKnownTypes.TypeExtensionClass
                .SelectMany(n => context.SemanticModel.Compilation.GetTypesByMetadataName(n))
                .Any(t => context.SemanticModel.Compilation.HasImplicitConversion(baseTypeSymbol, t)))
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

        foreach (var baseTypeSymbol in baseTypeSymbols)
        {
            if (context.SemanticModel.Compilation
                .GetTypesByMetadataName(WellKnownTypes.DataLoader)
                .Any(t => context.SemanticModel.Compilation.HasImplicitConversion(baseTypeSymbol, t)))
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

        syntaxInfo = null;
        return false;
    }
}
