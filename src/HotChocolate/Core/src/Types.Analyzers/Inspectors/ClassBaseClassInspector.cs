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
    public ImmutableArray<ISyntaxFilter> Filters { get; } = [ClassWithBaseClass.Instance];

    public IImmutableSet<SyntaxKind> SupportedKinds { get; } = [SyntaxKind.ClassDeclaration];

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
            .Select(t => context.SemanticModel.GetTypeInfo(t.Type).Type)
            .Where(t => MightBeRelevantType(t))
            .ToList();
        if (!baseTypeSymbols.Any())
        {
            syntaxInfo = null;
            return false;
        }

        static bool MightBeRelevantType(ITypeSymbol? typeSymbol)
        {
            return typeSymbol switch
            {
                null => false,
                { ContainingNamespace: { } ns } when IsRelevantNamespace(ns) => true,
                { BaseType: { } baseType } => MightBeRelevantType(baseType),
                { AllInterfaces: { Length: > 0 } interfaces } => interfaces.Any(i => IsRelevantNamespace(i.ContainingNamespace)),
                _ => false
            };

            static bool IsRelevantNamespace(INamespaceSymbol namespaceSymbol)
            {
                var ns = namespaceSymbol.ToDisplayString();
                return ns.StartsWith("HotChocolate") || ns.StartsWith("GreenDonut");
            }
        }

        var model = context.SemanticModel.GetDeclaredSymbol(possibleType);

        foreach (var baseTypeSymbol in baseTypeSymbols)
        {
            if (WellKnownTypes.TypeClass
                .SelectMany(n => context.SemanticModel.Compilation.GetTypesByMetadataName(n))
                .Any(t => context.SemanticModel.Compilation.HasImplicitConversion(baseTypeSymbol, t)))
            {
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
