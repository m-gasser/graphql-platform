using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using HotChocolate.Types.Analyzers.Filters;
using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeInfo = HotChocolate.Types.Analyzers.Models.TypeInfo;

namespace HotChocolate.Types.Analyzers.Inspectors;

public class ClassBaseClassInspector : ISyntaxInspector
{
    private static readonly Regex stripGenericArgumentsRegex = new(@"[^<]+<(.*)>", RegexOptions.Compiled);

    public ImmutableArray<ISyntaxFilter> Filters { get; } = [ClassWithBaseClass.Instance];

    public IImmutableSet<SyntaxKind> SupportedKinds { get; } = [SyntaxKind.ClassDeclaration];

    private static readonly ConcurrentDictionary<string, bool> cachedIsRelevantByTypeName = new();

    public bool TryHandle(
        GeneratorSyntaxContext context,
        [NotNullWhen(true)] out SyntaxInfo? syntaxInfo)
    {
        if (context.Node is not ClassDeclarationSyntax { BaseList: { Types.Count: > 0 } baseList, TypeParameterList: null, } possibleType)
        {
            syntaxInfo = null;
            return false;
        }

        if (!TryGetRelevantBaseTypes(baseList, context.SemanticModel, out var baseTypeSymbols))
        {
            syntaxInfo = null;
            return false;
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

    private bool TryGetRelevantBaseTypes(BaseListSyntax baseListSyntax, SemanticModel semanticModel, out ImmutableArray<ITypeSymbol> baseTypeSymbols)
    {
        var foundBaseTypeSymbols = ImmutableArray.CreateBuilder<ITypeSymbol>();
        foreach (var baseTypeSyntax in baseListSyntax.Types)
        {
            var typeName = stripGenericArgumentsRegex.Replace(baseTypeSyntax.ToString(), "");
            bool isRelevantBaseType = cachedIsRelevantByTypeName.GetOrAdd(typeName,
                static (_, args) =>
                {
                    var baseTypeSymbol = args.SemanticModel.GetTypeInfo(args.BaseTypeSyntax.Type).Type;
                    return MightBeRelevantType(baseTypeSymbol);
                },
                (BaseTypeSyntax: baseTypeSyntax, SemanticModel: semanticModel));

            if (isRelevantBaseType)
            {
                var baseTypeSymbol = semanticModel.GetTypeInfo(baseTypeSyntax).Type;
                if (baseTypeSymbol is not null)
                {
                    foundBaseTypeSymbols.Add(baseTypeSymbol);
                }
            }
        }

        baseTypeSymbols = foundBaseTypeSymbols.ToImmutable();
        return baseTypeSymbols.Length > 0;

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
    }
}
